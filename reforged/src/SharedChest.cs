using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace InventoryReforged
{
    /// More than one player in a chest at once.
    ///
    /// Vanilla: opening a chest transfers ZDO ownership to you and marks it in use; a second player is refused.
    /// Here: the owner grants the second player a view without giving up ownership. That player is a *guest*:
    /// their local copy of the chest is read-only, refreshed from the ZDO, and every move they make is sent
    /// to the owner as an RPC. The owner applies it to the real inventory and replies with what actually moved,
    /// and only then does the guest touch their own inventory. No two clients ever write the same inventory.
    public static class SharedChest
    {
        const string RpcPut = "IR_Put", RpcPutResp = "IR_PutResp";
        const string RpcTake = "IR_Take", RpcTakeResp = "IR_TakeResp";
        const string RpcMove = "IR_Move", RpcRemoveOne = "IR_RemoveOne";

        static readonly AccessTools.FieldRef<InventoryGui, Container> CurrentContainer = AccessTools.FieldRefAccess<InventoryGui, Container>("m_currentContainer");
        public static Container Current => InventoryGui.instance ? CurrentContainer(InventoryGui.instance) : null;

        /// True when `inv` is the chest we have open and someone else owns it.
        public static bool IsGuest(Inventory inv, out Container container)
        {
            container = null;
            if (!Plugin.SharedChests.Value) return false;
            var c = Current;
            if (!c || c.GetInventory() != inv) return false;
            var nview = c.GetComponent<ZNetView>();
            if (!nview || !nview.IsValid()) return false;
            if (!nview.HasOwner()) { nview.ClaimOwnership(); return false; }
            if (nview.IsOwner()) return false;
            container = c;
            return true;
        }

        public static bool IsGuestOf(Container c)
        {
            if (!c || Current != c) return false;
            return IsGuest(c.GetInventory(), out _);
        }

        // ---- item serialisation: reuse the inventory save format so every item field survives ----

        static ZPackage PackItem(ItemDrop.ItemData item, int amount)
        {
            var clone = item.Clone();
            clone.m_stack = amount;
            var pkg = new ZPackage();
            pkg.Write(109);
            pkg.Write((ushort)1);
            clone.Save(pkg);
            return pkg;
        }

        static ZPackage PackInventory(Inventory inv)
        {
            var pkg = new ZPackage();
            inv.Save(pkg);
            return pkg;
        }

        static Inventory Unpack(ZPackage pkg)
        {
            var inv = new Inventory("IR_tmp", null, 8, 8);
            inv.Load(pkg);
            return inv;
        }

        /// InventoryGrid.DropItem's rules, on any pair of inventories: stack, place, or swap.
        /// Afterwards `from` holds whatever didn't move plus anything displaced by a swap.
        static void DropInto(Inventory to, Inventory from, ItemDrop.ItemData item, int amount, int x, int y)
        {
            var itemAt = to.GetItemAt(x, y);
            if (itemAt == item) return;
            if (itemAt != null && (itemAt.m_shared.m_name != item.m_shared.m_name || (item.m_shared.m_maxQuality > 1 && itemAt.m_quality != item.m_quality) || itemAt.m_shared.m_maxStackSize == 1) && item.m_stack == amount)
            {
                var back = item.m_gridPos;
                from.RemoveItem(item);
                from.MoveItemToThis(to, itemAt, itemAt.m_stack, back.x, back.y);
                to.MoveItemToThis(from, item, amount, x, y);
                return;
            }
            to.MoveItemToThis(from, item, amount, x, y);
        }

        // ---- guest side: requests ----

        class PendingPut { public ItemDrop.ItemData Item; public Inventory From; public int Amount; public Vector2i Origin; }
        class PendingTake { public Inventory To; public int X, Y; }
        static readonly Dictionary<int, PendingPut> s_puts = new Dictionary<int, PendingPut>();
        static readonly Dictionary<int, PendingTake> s_takes = new Dictionary<int, PendingTake>();
        static int s_req;

        static ZNetView View(Container c) => c.GetComponent<ZNetView>();

        /// Player inventory -> chest. x,y = -1 means "anywhere".
        public static void Put(Container c, Inventory from, ItemDrop.ItemData item, int amount, int x, int y)
        {
            int req = ++s_req;
            s_puts[req] = new PendingPut { Item = item, From = from, Amount = amount, Origin = item.m_gridPos };
            var pkg = new ZPackage();
            pkg.Write(req); pkg.Write(x); pkg.Write(y); pkg.Write(PackItem(item, amount));
            View(c).InvokeRPC(RpcPut, pkg);
        }

        /// Chest -> player inventory. toX,toY = -1 means "anywhere".
        public static void Take(Container c, ItemDrop.ItemData chestItem, int amount, Inventory to, int toX, int toY)
        {
            int req = ++s_req;
            s_takes[req] = new PendingTake { To = to, X = toX, Y = toY };
            var pkg = new ZPackage();
            pkg.Write(req); pkg.Write(chestItem.m_gridPos.x); pkg.Write(chestItem.m_gridPos.y); pkg.Write(amount);
            View(c).InvokeRPC(RpcTake, pkg);
        }

        public static void Move(Container c, ItemDrop.ItemData chestItem, int amount, int toX, int toY)
        {
            var pkg = new ZPackage();
            pkg.Write(chestItem.m_gridPos.x); pkg.Write(chestItem.m_gridPos.y); pkg.Write(toX); pkg.Write(toY); pkg.Write(amount);
            View(c).InvokeRPC(RpcMove, pkg);
        }

        public static void RemoveOne(Container c, ItemDrop.ItemData chestItem)
        {
            var pkg = new ZPackage();
            pkg.Write(chestItem.m_gridPos.x); pkg.Write(chestItem.m_gridPos.y);
            View(c).InvokeRPC(RpcRemoveOne, pkg);
        }

        // ---- guest side: responses ----

        static void OnPutResp(Container c, long sender, ZPackage pkg)
        {
            int req = pkg.ReadInt();
            int moved = pkg.ReadInt();
            var leftovers = Unpack(pkg.ReadPackage());
            if (!s_puts.TryGetValue(req, out var p)) return;
            s_puts.Remove(req);
            if (moved > 0 && p.From.ContainsItem(p.Item)) p.From.RemoveItem(p.Item, moved);
            foreach (var d in new List<ItemDrop.ItemData>(leftovers.GetAllItems()))
            {
                leftovers.RemoveItem(d);
                if (p.From.AddItem(d, p.Origin) || p.From.AddItem(d)) continue;
                DropAtFeet(d);
            }
        }

        static void OnTakeResp(Container c, long sender, ZPackage pkg)
        {
            int req = pkg.ReadInt();
            var got = Unpack(pkg.ReadPackage());
            if (!s_takes.TryGetValue(req, out var t)) return;
            s_takes.Remove(req);
            foreach (var item in new List<ItemDrop.ItemData>(got.GetAllItems()))
            {
                int amount = item.m_stack;
                if (t.X >= 0) DropInto(t.To, got, item, amount, t.X, t.Y);
                else if (t.To.AddItem(item)) got.RemoveItem(item);
                // Anything still in `got` (didn't fit, or was displaced by a swap) goes back to the chest.
            }
            foreach (var back in new List<ItemDrop.ItemData>(got.GetAllItems()))
            {
                if (IsGuestOf(c)) Put(c, got, back, back.m_stack, -1, -1);
                else DropAtFeet(back);
            }
        }

        static void DropAtFeet(ItemDrop.ItemData item)
        {
            var p = Player.m_localPlayer;
            if (!p) return;
            ItemDrop.DropItem(item, item.m_stack, p.transform.position + p.transform.forward + Vector3.up, p.transform.rotation);
        }

        // ---- owner side: apply ----

        static void OnPut(Container c, long sender, ZPackage pkg)
        {
            if (!View(c).IsOwner()) return;
            int req = pkg.ReadInt();
            int x = pkg.ReadInt(); int y = pkg.ReadInt();
            var tmp = Unpack(pkg.ReadPackage());
            int moved = 0;
            var items = tmp.GetAllItems();
            if (items.Count > 0)
            {
                var item = items[0];
                int amount = item.m_stack;
                if (x < 0)
                {
                    bool all = c.GetInventory().AddItem(item);
                    moved = all ? amount : amount - item.m_stack;
                }
                else
                {
                    DropInto(c.GetInventory(), tmp, item, amount, x, y);
                    moved = tmp.ContainsItem(item) ? amount - item.m_stack : amount;
                }
                // Whatever of the guest's item didn't move is still in their inventory; only displaced items go back.
                if (tmp.ContainsItem(item)) tmp.RemoveItem(item);
            }
            var resp = new ZPackage();
            resp.Write(req); resp.Write(moved); resp.Write(PackInventory(tmp));
            View(c).InvokeRPC(sender, RpcPutResp, resp);
        }

        static void OnTake(Container c, long sender, ZPackage pkg)
        {
            if (!View(c).IsOwner()) return;
            int req = pkg.ReadInt();
            int x = pkg.ReadInt(); int y = pkg.ReadInt(); int amount = pkg.ReadInt();
            var tmp = new Inventory("IR_tmp", null, 8, 8);
            var item = c.GetInventory().GetItemAt(x, y);
            if (item != null) tmp.MoveItemToThis(c.GetInventory(), item, Mathf.Min(amount, item.m_stack), 0, 0);
            var resp = new ZPackage();
            resp.Write(req); resp.Write(PackInventory(tmp));
            View(c).InvokeRPC(sender, RpcTakeResp, resp);
        }

        static void OnMove(Container c, long sender, ZPackage pkg)
        {
            if (!View(c).IsOwner()) return;
            int x = pkg.ReadInt(); int y = pkg.ReadInt(); int toX = pkg.ReadInt(); int toY = pkg.ReadInt(); int amount = pkg.ReadInt();
            var inv = c.GetInventory();
            var item = inv.GetItemAt(x, y);
            if (item != null) DropInto(inv, inv, item, Mathf.Min(amount, item.m_stack), toX, toY);
        }

        static void OnRemoveOne(Container c, long sender, ZPackage pkg)
        {
            if (!View(c).IsOwner()) return;
            int x = pkg.ReadInt(); int y = pkg.ReadInt();
            var item = c.GetInventory().GetItemAt(x, y);
            if (item != null) c.GetInventory().RemoveOneItem(item);
        }

        // ---- patches ----

        [HarmonyPatch(typeof(Container), "Awake")]
        static class RegisterRpcs
        {
            static void Postfix(Container __instance)
            {
                var nview = __instance.GetComponent<ZNetView>();
                if (!nview || nview.GetZDO() == null) return;
                var c = __instance;
                nview.Register<ZPackage>(RpcPut, (s, p) => OnPut(c, s, p));
                nview.Register<ZPackage>(RpcPutResp, (s, p) => OnPutResp(c, s, p));
                nview.Register<ZPackage>(RpcTake, (s, p) => OnTake(c, s, p));
                nview.Register<ZPackage>(RpcTakeResp, (s, p) => OnTakeResp(c, s, p));
                nview.Register<ZPackage>(RpcMove, (s, p) => OnMove(c, s, p));
                nview.Register<ZPackage>(RpcRemoveOne, (s, p) => OnRemoveOne(c, s, p));
            }
        }

        /// Owner: a chest that's in use is still granted, just without handing over ownership.
        [HarmonyPatch(typeof(Container), "RPC_RequestOpen")]
        static class GrantGuestOpen
        {
            static bool Prefix(Container __instance, long uid, long playerID)
            {
                if (!Plugin.SharedChests.Value) return true;
                var nview = __instance.GetComponent<ZNetView>();
                if (!nview.IsOwner()) return true;
                bool inUse = __instance.IsInUse() || (__instance.m_wagon && __instance.m_wagon.InUse());
                if (!inUse || uid == ZNet.GetUID()) return true;
                if (!Traverse.Create(__instance).Method("CheckAccess", playerID).GetValue<bool>()) return true;
                nview.InvokeRPC(uid, "RPC_OpenResponse", true);
                return false;
            }
        }

        /// InventoryGui.UpdateContainer closes the chest panel unless you own the chest. Swap that one
        /// IsOwner() call for a check that also accepts a guest, and while a guest, pull the owner's
        /// changes from the ZDO every frame instead of Container's once-a-second poll.
        [HarmonyPatch(typeof(InventoryGui), "UpdateContainer")]
        static class KeepGuestPanelOpen
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> il)
            {
                var target = AccessTools.Method(typeof(Container), nameof(Container.IsOwner));
                var repl = AccessTools.Method(typeof(SharedChest), nameof(OwnerOrGuest));
                foreach (var i in il)
                    yield return i.Calls(target) ? new CodeInstruction(OpCodes.Call, repl).WithLabels(i.labels).WithBlocks(i.blocks) : i;
            }

            static void Prefix()
            {
                var c = Current;
                if (c && IsGuestOf(c)) Traverse.Create(c).Method("Load").GetValue<bool>();
            }
        }

        public static bool OwnerOrGuest(Container c) => c.IsOwner() || (Plugin.SharedChests.Value && Current == c);

        /// Drag and drop involving a guest chest goes through the owner.
        [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.DropItem))]
        static class RouteDrops
        {
            static bool Prefix(InventoryGrid __instance, Inventory fromInventory, ItemDrop.ItemData item, int amount, Vector2i pos, ref bool __result)
            {
                var to = __instance.GetInventory();
                bool toGuest = IsGuest(to, out var c);
                bool fromGuest = IsGuest(fromInventory, out var c2);
                if (!toGuest && !fromGuest) return true;
                __result = true;
                if (toGuest && fromGuest) Move(c, item, amount, pos.x, pos.y);
                else if (toGuest) Put(c, fromInventory, item, amount, pos.x, pos.y);
                else Take(c2, item, amount, to, pos.x, pos.y);
                return false;
            }
        }

        /// Ctrl+click quick move.
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.MoveItemToThis), typeof(Inventory), typeof(ItemDrop.ItemData))]
        static class RouteQuickMove
        {
            static bool Prefix(Inventory __instance, Inventory fromInventory, ItemDrop.ItemData item, ref bool __result)
            {
                if (IsGuest(__instance, out var c)) { Put(c, fromInventory, item, item.m_stack, -1, -1); __result = true; return false; }
                if (IsGuest(fromInventory, out var c2))
                {
                    if (__instance.CanAddItem(item)) Take(c2, item, item.m_stack, __instance, -1, -1);
                    __result = true;
                    return false;
                }
                return true;
            }
        }

        /// Take all.
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.MoveAll))]
        static class RouteTakeAll
        {
            static bool Prefix(Inventory __instance, Inventory fromInventory)
            {
                if (!IsGuest(fromInventory, out var c)) return true;
                foreach (var item in new List<ItemDrop.ItemData>(fromInventory.GetAllItems()))
                    if (__instance.CanAddItem(item)) Take(c, item, item.m_stack, __instance, -1, -1);
                return false;
            }
        }

        /// Place stacks, via the button or holding E.
        static void GuestStackAll(Container c, Inventory from)
        {
            var chest = c.GetInventory();
            foreach (var item in new List<ItemDrop.ItemData>(from.GetAllItems()))
            {
                if (!Stash.CanLeave(item, respectHotbar: false)) continue;
                if (chest.ContainsItemByName(item.m_shared.m_name)) Put(c, from, item, item.m_stack, -1, -1);
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.StackAll))]
        static class RoutePlaceStacks
        {
            [HarmonyPriority(Priority.High)]
            static bool Prefix(Inventory __instance, Inventory fromInventory, ref int __result)
            {
                if (!IsGuest(__instance, out var c)) return true;
                GuestStackAll(c, fromInventory);
                __result = 0;
                return false;
            }
        }

        [HarmonyPatch(typeof(Container), nameof(Container.StackAll))]
        static class RouteHoldToStack
        {
            static bool Prefix(Container __instance)
            {
                if (!IsGuestOf(__instance) || !Player.m_localPlayer) return true;
                GuestStackAll(__instance, Player.m_localPlayer.GetInventory());
                return false;
            }
        }

        /// Eating straight from the chest.
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveOneItem))]
        static class RouteEat
        {
            static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
            {
                if (!IsGuest(__instance, out var c)) return true;
                RemoveOne(c, item);
                __result = true;
                return false;
            }
        }

        /// Dropping to the ground straight out of a guest chest would need a take first; just refuse.
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.DropItem))]
        static class RefuseDropFromGuestChest
        {
            static bool Prefix(Inventory inventory, ref bool __result)
            {
                if (!IsGuest(inventory, out _)) return true;
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "Take it into your inventory first");
                __result = false;
                return false;
            }
        }
    }
}
