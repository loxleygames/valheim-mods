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
    /// their local copy of the chest is read-only, refreshed from the ZDO every frame, and every move they
    /// make is sent to the owner as an RPC. The owner verifies the slot still holds what the guest saw,
    /// applies the move to the real inventory and replies with what actually moved; only then does the guest
    /// touch their own inventory. Items involved in a request are locked on the guest until the reply comes.
    /// No two clients ever write the same inventory.
    public static class SharedChest
    {
        const string RpcPut = "IR_Put", RpcPutResp = "IR_PutResp";
        const string RpcTake = "IR_Take", RpcTakeResp = "IR_TakeResp";
        const string RpcMove = "IR_Move";
        const string RpcEat = "IR_Eat", RpcEatResp = "IR_EatResp";
        const float Timeout = 5f;

        static readonly AccessTools.FieldRef<InventoryGui, Container> CurrentContainer = AccessTools.FieldRefAccess<InventoryGui, Container>("m_currentContainer");
        static readonly AccessTools.FieldRef<InventoryGui, ItemDrop.ItemData> DragItem = AccessTools.FieldRefAccess<InventoryGui, ItemDrop.ItemData>("m_dragItem");
        static readonly AccessTools.FieldRef<InventoryGui, Inventory> DragInventory = AccessTools.FieldRefAccess<InventoryGui, Inventory>("m_dragInventory");
        static readonly AccessTools.FieldRef<InventoryGui, int> DragAmount = AccessTools.FieldRefAccess<InventoryGui, int>("m_dragAmount");
        static readonly AccessTools.FieldRef<InventoryGui, GameObject> DragGo = AccessTools.FieldRefAccess<InventoryGui, GameObject>("m_dragGo");
        static readonly AccessTools.FieldRef<Container, ZNetView> ContainerView = AccessTools.FieldRefAccess<Container, ZNetView>("m_nview");

        public static Container Current => InventoryGui.instance ? CurrentContainer(InventoryGui.instance) : null;

        /// The container's own ZNetView (carts and ship holds keep theirs on a parent object).
        static ZNetView View(Container c) => c ? ContainerView(c) : null;

        static void SetupDrag(ItemDrop.ItemData item, Inventory inv, int amount) =>
            Traverse.Create(InventoryGui.instance).Method("SetupDragItem", item, inv, amount).GetValue();

        /// True when `inv` is the chest we have open and someone else owns it.
        public static bool IsGuest(Inventory inv, out Container container)
        {
            container = null;
            if (!Plugin.SharedChests.Value) return false;
            var c = Current;
            if (!c || c.GetInventory() != inv) return false;
            var nview = View(c);
            if (!nview || !nview.IsValid()) return false;
            if (!nview.HasOwner())
            {
                // Nobody owns it: take it, but pull the latest contents first so we don't save a stale copy over them.
                Traverse.Create(c).Method("Load").GetValue<bool>();
                nview.ClaimOwnership();
                return false;
            }
            if (nview.IsOwner()) return false;
            container = c;
            return true;
        }

        public static bool IsGuestOf(Container c) => c && Current == c && IsGuest(c.GetInventory(), out _);

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

        static string NameOf(ItemDrop.ItemData item) => item.m_shared.m_name;

        /// InventoryGrid.DropItem's rules, on any pair of inventories: stack, place, or swap.
        /// Afterwards `from` holds whatever didn't move plus anything displaced by a swap.
        static void DropInto(Inventory to, Inventory from, ItemDrop.ItemData item, int amount, int x, int y)
        {
            var itemAt = to.GetItemAt(x, y);
            if (itemAt == item) return;
            if (itemAt != null && !CanStack(itemAt, item) && item.m_stack == amount)
            {
                var back = item.m_gridPos;
                from.RemoveItem(item);
                from.MoveItemToThis(to, itemAt, itemAt.m_stack, back.x, back.y);
                to.MoveItemToThis(from, item, amount, x, y);
                return;
            }
            to.MoveItemToThis(from, item, amount, x, y);
        }

        static bool CanStack(ItemDrop.ItemData a, ItemDrop.ItemData b) =>
            a.m_shared.m_name == b.m_shared.m_name && (b.m_shared.m_maxQuality <= 1 || a.m_quality == b.m_quality) && a.m_shared.m_maxStackSize > 1;

        // ---- guest side: locks ----

        class PendingPut { public ItemDrop.ItemData Item; public Inventory From; public int Amount; public Vector2i Origin; public float Sent; }
        class PendingTake { public Container Chest; public Vector2i Slot; public Inventory To; public int X, Y; public bool Drop; public float Sent; }
        static readonly Dictionary<int, PendingPut> s_puts = new Dictionary<int, PendingPut>();
        static readonly Dictionary<int, PendingTake> s_takes = new Dictionary<int, PendingTake>();
        static readonly HashSet<ItemDrop.ItemData> s_lockedItems = new HashSet<ItemDrop.ItemData>();
        static readonly HashSet<Vector2i> s_lockedChestSlots = new HashSet<Vector2i>();
        static bool s_eating;
        static float s_eatSent;
        static int s_req;

        public static bool IsLocked(ItemDrop.ItemData item) => item != null && s_lockedItems.Contains(item);
        static bool IsLockedChestSlot(Inventory inv, Vector2i pos) => IsGuest(inv, out _) && s_lockedChestSlots.Contains(pos);

        static void ExpireLocks()
        {
            float now = Time.time;
            var dead = new List<int>();
            foreach (var kv in s_puts) if (now - kv.Value.Sent > Timeout) dead.Add(kv.Key);
            foreach (var k in dead) { s_lockedItems.Remove(s_puts[k].Item); s_puts.Remove(k); }
            dead.Clear();
            foreach (var kv in s_takes) if (now - kv.Value.Sent > Timeout) dead.Add(kv.Key);
            foreach (var k in dead) { s_lockedChestSlots.Remove(s_takes[k].Slot); s_takes.Remove(k); }
            if (s_eating && now - s_eatSent > Timeout) s_eating = false;
        }

        // ---- guest side: requests ----

        /// Player inventory -> chest. x,y = -1 means "anywhere".
        public static void Put(Container c, Inventory from, ItemDrop.ItemData item, int amount, int x, int y)
        {
            if (IsLocked(item)) return;
            int req = ++s_req;
            s_puts[req] = new PendingPut { Item = item, From = from, Amount = amount, Origin = item.m_gridPos, Sent = Time.time };
            s_lockedItems.Add(item);
            var pkg = new ZPackage();
            pkg.Write(req); pkg.Write(x); pkg.Write(y); pkg.Write(PackItem(item, amount));
            View(c).InvokeRPC(RpcPut, pkg);
        }

        /// Chest -> player inventory (toX,toY = -1 means "anywhere") or, with drop, chest -> ground.
        public static void Take(Container c, ItemDrop.ItemData chestItem, int amount, Inventory to, int toX, int toY, bool drop = false)
        {
            if (s_lockedChestSlots.Contains(chestItem.m_gridPos)) return;
            int req = ++s_req;
            s_takes[req] = new PendingTake { Chest = c, Slot = chestItem.m_gridPos, To = to, X = toX, Y = toY, Drop = drop, Sent = Time.time };
            s_lockedChestSlots.Add(chestItem.m_gridPos);
            var pkg = new ZPackage();
            pkg.Write(req); pkg.Write(chestItem.m_gridPos.x); pkg.Write(chestItem.m_gridPos.y); pkg.Write(amount); pkg.Write(NameOf(chestItem));
            View(c).InvokeRPC(RpcTake, pkg);
        }

        public static void Move(Container c, ItemDrop.ItemData chestItem, int amount, int toX, int toY)
        {
            if (s_lockedChestSlots.Contains(chestItem.m_gridPos)) return;
            var pkg = new ZPackage();
            pkg.Write(chestItem.m_gridPos.x); pkg.Write(chestItem.m_gridPos.y); pkg.Write(toX); pkg.Write(toY); pkg.Write(amount); pkg.Write(NameOf(chestItem));
            View(c).InvokeRPC(RpcMove, pkg);
        }

        public static void Eat(Container c, ItemDrop.ItemData chestItem)
        {
            if (s_eating || s_lockedChestSlots.Contains(chestItem.m_gridPos)) return;
            s_eating = true;
            s_eatSent = Time.time;
            var pkg = new ZPackage();
            pkg.Write(chestItem.m_gridPos.x); pkg.Write(chestItem.m_gridPos.y); pkg.Write(NameOf(chestItem));
            View(c).InvokeRPC(RpcEat, pkg);
        }

        /// Everything in `from` that the chest already holds, sent one request per stack.
        public static void PutMatching(Container c, Inventory from)
        {
            var chest = c.GetInventory();
            foreach (var item in new List<ItemDrop.ItemData>(from.GetAllItems()))
            {
                if (!Stash.CanLeave(item, respectHotbar: false)) continue;
                if (chest.ContainsItemByName(NameOf(item))) Put(c, from, item, item.m_stack, -1, -1);
            }
        }

        // ---- guest side: responses ----

        static void OnPutResp(Container c, long sender, ZPackage pkg)
        {
            int req = pkg.ReadInt();
            int moved = pkg.ReadInt();
            var leftovers = Unpack(pkg.ReadPackage());
            if (!s_puts.TryGetValue(req, out var p)) return;
            s_puts.Remove(req);
            s_lockedItems.Remove(p.Item);
            if (moved > 0 && p.From.ContainsItem(p.Item)) p.From.RemoveItem(p.Item, Mathf.Min(moved, p.Item.m_stack));
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
            s_lockedChestSlots.Remove(t.Slot);
            foreach (var item in new List<ItemDrop.ItemData>(got.GetAllItems()))
            {
                if (t.Drop) { got.RemoveItem(item); DropAtFeet(item); continue; }
                // Target slot: only stack or place, never swap (the guest's inventory may have changed meanwhile).
                if (t.X >= 0)
                {
                    var at = t.To.GetItemAt(t.X, t.Y);
                    if (at == null || CanStack(at, item)) t.To.MoveItemToThis(got, item, item.m_stack, t.X, t.Y);
                }
                if (got.ContainsItem(item) && item.m_stack > 0 && t.To.AddItem(item)) got.RemoveItem(item);
            }
            // Anything left didn't fit: back to the chest, or the ground if we no longer have it open.
            foreach (var back in new List<ItemDrop.ItemData>(got.GetAllItems()))
            {
                got.RemoveItem(back);
                if (IsGuestOf(c)) PutLoose(c, back);
                else DropAtFeet(back);
            }
        }

        /// Send an item we hold in no inventory back to the chest.
        static void PutLoose(Container c, ItemDrop.ItemData item)
        {
            var pkg = new ZPackage();
            pkg.Write(0); pkg.Write(-1); pkg.Write(-1); pkg.Write(PackItem(item, item.m_stack));
            View(c).InvokeRPC(RpcPut, pkg);
        }

        static void OnEatResp(Container c, long sender, ZPackage pkg)
        {
            s_eating = false;
            var got = Unpack(pkg.ReadPackage());
            var player = Player.m_localPlayer;
            foreach (var item in new List<ItemDrop.ItemData>(got.GetAllItems()))
            {
                if (player && player.CanConsumeItem(item) && player.ConsumeItem(got, item)) continue;
                got.RemoveItem(item);
                if (!player || !player.GetInventory().AddItem(item)) DropAtFeet(item);
            }
        }

        static void DropAtFeet(ItemDrop.ItemData item)
        {
            var p = Player.m_localPlayer;
            if (!p || item.m_stack <= 0) return;
            var drop = ItemDrop.DropItem(item, item.m_stack, p.transform.position + p.transform.forward + Vector3.up, p.transform.rotation);
            if (drop) drop.OnPlayerDrop();
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
            if (req == 0)
            {
                // A loose return with nothing to reply to: anything that didn't fit lands by the chest.
                foreach (var d in new List<ItemDrop.ItemData>(tmp.GetAllItems()))
                    ItemDrop.DropItem(d, d.m_stack, c.transform.position + Vector3.up, Quaternion.identity);
                return;
            }
            var resp = new ZPackage();
            resp.Write(req); resp.Write(moved); resp.Write(PackInventory(tmp));
            View(c).InvokeRPC(sender, RpcPutResp, resp);
        }

        static void OnTake(Container c, long sender, ZPackage pkg)
        {
            if (!View(c).IsOwner()) return;
            int req = pkg.ReadInt();
            int x = pkg.ReadInt(); int y = pkg.ReadInt(); int amount = pkg.ReadInt(); string name = pkg.ReadString();
            var tmp = new Inventory("IR_tmp", null, 8, 8);
            var item = c.GetInventory().GetItemAt(x, y);
            if (item != null && NameOf(item) == name) tmp.MoveItemToThis(c.GetInventory(), item, Mathf.Min(amount, item.m_stack), 0, 0);
            var resp = new ZPackage();
            resp.Write(req); resp.Write(PackInventory(tmp));
            View(c).InvokeRPC(sender, RpcTakeResp, resp);
        }

        static void OnMove(Container c, long sender, ZPackage pkg)
        {
            if (!View(c).IsOwner()) return;
            int x = pkg.ReadInt(); int y = pkg.ReadInt(); int toX = pkg.ReadInt(); int toY = pkg.ReadInt(); int amount = pkg.ReadInt(); string name = pkg.ReadString();
            var inv = c.GetInventory();
            var item = inv.GetItemAt(x, y);
            if (item != null && NameOf(item) == name) DropInto(inv, inv, item, Mathf.Min(amount, item.m_stack), toX, toY);
        }

        static void OnEat(Container c, long sender, ZPackage pkg)
        {
            if (!View(c).IsOwner()) return;
            int x = pkg.ReadInt(); int y = pkg.ReadInt(); string name = pkg.ReadString();
            var tmp = new Inventory("IR_tmp", null, 8, 8);
            var item = c.GetInventory().GetItemAt(x, y);
            if (item != null && NameOf(item) == name) tmp.MoveItemToThis(c.GetInventory(), item, 1, 0, 0);
            var resp = new ZPackage();
            resp.Write(PackInventory(tmp));
            View(c).InvokeRPC(sender, RpcEatResp, resp);
        }

        // ---- patches: setup ----

        [HarmonyPatch(typeof(Container), "Awake")]
        static class RegisterRpcs
        {
            static void Postfix(Container __instance)
            {
                var nview = View(__instance);
                if (!nview || nview.GetZDO() == null) return;
                var c = __instance;
                nview.Register<ZPackage>(RpcPut, (s, p) => OnPut(c, s, p));
                nview.Register<ZPackage>(RpcPutResp, (s, p) => OnPutResp(c, s, p));
                nview.Register<ZPackage>(RpcTake, (s, p) => OnTake(c, s, p));
                nview.Register<ZPackage>(RpcTakeResp, (s, p) => OnTakeResp(c, s, p));
                nview.Register<ZPackage>(RpcMove, (s, p) => OnMove(c, s, p));
                nview.Register<ZPackage>(RpcEat, (s, p) => OnEat(c, s, p));
                nview.Register<ZPackage>(RpcEatResp, (s, p) => OnEatResp(c, s, p));
            }
        }

        /// Owner: an in-use chest is still granted for open and for stacking, just without handing over ownership.
        static bool GrantWithoutOwnership(Container c, long uid, long playerID, string response)
        {
            if (!Plugin.SharedChests.Value) return true;
            var nview = View(c);
            if (!nview || !nview.IsOwner()) return true;
            bool inUse = c.IsInUse() || (c.m_wagon && c.m_wagon.InUse());
            if (!inUse || uid == ZNet.GetUID()) return true;
            if (!Traverse.Create(c).Method("CheckAccess", playerID).GetValue<bool>()) return true;
            nview.InvokeRPC(uid, response, true);
            return false;
        }

        [HarmonyPatch(typeof(Container), "RPC_RequestOpen")]
        static class GrantGuestOpen
        {
            static bool Prefix(Container __instance, long uid, long playerID) => GrantWithoutOwnership(__instance, uid, playerID, "RPC_OpenResponse");
        }

        [HarmonyPatch(typeof(Container), "RPC_RequestStack")]
        static class GrantGuestStack
        {
            static bool Prefix(Container __instance, long uid, long playerID) => GrantWithoutOwnership(__instance, uid, playerID, "RPC_StackResponse");
        }

        /// A stack grant on a chest we don't own: send each matching stack through the owner instead of writing locally.
        [HarmonyPatch(typeof(Container), "RPC_StackResponse")]
        static class RouteStackGrant
        {
            [HarmonyPriority(Priority.Low)]
            static bool Prefix(Container __instance, bool granted)
            {
                var nview = View(__instance);
                if (!granted || !nview || !nview.IsValid() || nview.IsOwner() || !Player.m_localPlayer) return true;
                PutMatching(__instance, Player.m_localPlayer.GetInventory());
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
                ExpireLocks();
                var c = Current;
                if (c && IsGuestOf(c)) Traverse.Create(c).Method("Load").GetValue<bool>();
            }
        }

        public static bool OwnerOrGuest(Container c) => c.IsOwner() || (Plugin.SharedChests.Value && Current == c);

        /// The chest's item objects are recreated on every sync; keep a drag pointing at the same slot.
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.Load), typeof(ZPackage))]
        static class KeepDragAcrossReload
        {
            static void Postfix(Inventory __instance)
            {
                var gui = InventoryGui.instance;
                if (!gui || DragItem(gui) == null || DragInventory(gui) != __instance) return;
                var c = Current;
                if (!c || c.GetInventory() != __instance) return;
                var old = DragItem(gui);
                var now = __instance.GetItemAt(old.m_gridPos.x, old.m_gridPos.y);
                if (now == null || NameOf(now) != NameOf(old)) { SetupDrag(null, null, 1); return; }
                SetupDrag(now, __instance, Mathf.Min(now.m_stack, DragAmount(gui)));
            }
        }

        // ---- patches: guest actions ----

        /// Drag and drop involving a guest chest goes through the owner. Locked items and slots are ignored.
        [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.DropItem))]
        static class RouteDrops
        {
            static bool Prefix(InventoryGrid __instance, Inventory fromInventory, ItemDrop.ItemData item, int amount, Vector2i pos, ref bool __result)
            {
                var to = __instance.GetInventory();
                bool toGuest = IsGuest(to, out var c);
                bool fromGuest = IsGuest(fromInventory, out var c2);
                if (!toGuest && !fromGuest) return IsLocked(item) ? Refuse(ref __result) : true;
                __result = true;
                if (IsLocked(item) || (fromGuest && s_lockedChestSlots.Contains(item.m_gridPos)) || (toGuest && s_lockedChestSlots.Contains(pos))) return false;

                if (toGuest && fromGuest) Move(c, item, amount, pos.x, pos.y);
                else if (toGuest) Put(c, fromInventory, item, amount, pos.x, pos.y);
                else
                {
                    // Chest -> player. Dropping onto a different item is a swap: send ours to the chest slot and
                    // the displaced chest item comes back into the slot we vacated.
                    var at = to.GetItemAt(pos.x, pos.y);
                    if (at != null && !CanStack(at, item) && !IsLocked(at) && !Player.m_localPlayer.IsItemEquiped(at))
                        Put(c2, to, at, at.m_stack, item.m_gridPos.x, item.m_gridPos.y);
                    else
                        Take(c2, item, amount, to, pos.x, pos.y);
                }
                return false;
            }

            static bool Refuse(ref bool result) { result = false; return false; }
        }

        /// Clicking a locked item, or a locked chest slot, does nothing until the reply arrives.
        [HarmonyPatch(typeof(InventoryGui), "OnSelectedItem")]
        static class IgnoreLockedClicks
        {
            [HarmonyPriority(Priority.High)]
            static bool Prefix(InventoryGrid grid, ItemDrop.ItemData item, Vector2i pos)
            {
                if (IsLocked(item)) return false;
                if (IsLockedChestSlot(grid.GetInventory(), pos)) return false;
                return true;
            }
        }

        /// Right-click on a guest chest item: ask the owner for one, eat it when it arrives.
        [HarmonyPatch(typeof(InventoryGui), "OnRightClickItem")]
        static class RouteEat
        {
            static bool Prefix(InventoryGrid grid, ItemDrop.ItemData item)
            {
                if (item == null || !IsGuest(grid.GetInventory(), out var c)) return true;
                if (Player.m_localPlayer && Player.m_localPlayer.CanConsumeItem(item)) Eat(c, item);
                return false;
            }
        }

        /// Ctrl+click quick move.
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.MoveItemToThis), typeof(Inventory), typeof(ItemDrop.ItemData))]
        static class RouteQuickMove
        {
            static bool Prefix(Inventory __instance, Inventory fromInventory, ItemDrop.ItemData item)
            {
                if (IsGuest(__instance, out var c)) { Put(c, fromInventory, item, item.m_stack, -1, -1); return false; }
                if (IsGuest(fromInventory, out var c2))
                {
                    if (__instance.CanAddItem(item)) Take(c2, item, item.m_stack, __instance, -1, -1);
                    return false;
                }
                return !IsLocked(item);
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
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.StackAll))]
        static class RoutePlaceStacks
        {
            [HarmonyPriority(Priority.High)]
            static bool Prefix(Inventory __instance, Inventory fromInventory, ref int __result)
            {
                if (!IsGuest(__instance, out var c)) return true;
                PutMatching(c, fromInventory);
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
                PutMatching(__instance, Player.m_localPlayer.GetInventory());
                return false;
            }
        }

        /// Dragging a chest item out onto the world: the owner removes it, we spawn it at our feet.
        [HarmonyPatch(typeof(InventoryGui), "OnDropOutside")]
        static class RouteDropOutside
        {
            static bool Prefix(InventoryGui __instance)
            {
                if (!DragGo(__instance) || !IsGuest(DragInventory(__instance), out var c)) return true;
                var item = DragItem(__instance);
                if (item != null && !s_lockedChestSlots.Contains(item.m_gridPos)) Take(c, item, DragAmount(__instance), null, -1, -1, drop: true);
                SetupDrag(null, null, 1);
                return false;
            }
        }

        /// A locked item can't be dropped or used until its request resolves.
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.DropItem))]
        static class NoDropWhileLocked
        {
            static bool Prefix(Inventory inventory, ItemDrop.ItemData item, ref bool __result)
            {
                if (IsGuest(inventory, out _) || IsLocked(item)) { __result = false; return false; }
                return true;
            }
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
        static class NoUseWhileLocked
        {
            static bool Prefix(ItemDrop.ItemData item) => !IsLocked(item);
        }
    }
}
