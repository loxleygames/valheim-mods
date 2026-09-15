using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace InventoryReforged
{
    /// One key: every item in your inventory that a nearby chest already holds goes into that chest.
    /// Each chest is asked through the game's own "Place stacks" RPC, so the chest owner does the in-use
    /// check and hands over ownership before anything moves.
    public static class Stash
    {
        public static readonly HashSet<Container> Pending = new HashSet<Container>();
        public static int Moved;
        static float s_lastRun;

        public static void Run(Player player)
        {
            if (Time.time - s_lastRun < 0.5f) return;
            s_lastRun = Time.time;

            var containers = FindNearby(player.transform.position, Plugin.StashRange.Value);
            if (containers.Count == 0)
            {
                player.Message(MessageHud.MessageType.Center, "No chests nearby");
                return;
            }
            Moved = 0;
            Pending.Clear();
            foreach (var c in containers)
            {
                Pending.Add(c);
                c.StackAll();
            }
            player.StartCoroutine(Report(player));
        }

        static System.Collections.IEnumerator Report(Player player)
        {
            yield return new WaitForSeconds(1.5f);
            Pending.Clear();
            player.Message(MessageHud.MessageType.Center, Moved > 0 ? $"Stashed {Moved} items" : "Nothing to stash");
        }

        public static List<Container> FindNearby(Vector3 pos, float range)
        {
            var found = new List<Container>();
            var seen = new HashSet<Container>();
            foreach (var col in Physics.OverlapSphere(pos, range, LayerMask.GetMask("piece", "item")))
            {
                var c = col.GetComponentInParent<Container>();
                if (!c || !seen.Add(c)) continue;
                if (!c.GetComponent<ZNetView>()?.IsValid() ?? true) continue;
                if (c.m_wagon || c.GetComponentInParent<Ship>()) continue;
                if (Plugin.StashPlayerBuiltOnly.Value)
                {
                    var piece = c.GetComponent<Piece>();
                    if (!piece || !piece.IsPlacedByPlayer()) continue;
                }
                if (c.m_checkGuardStone && !PrivateArea.CheckAccess(c.transform.position, 0f, false)) continue;
                found.Add(c);
            }
            return found;
        }

        /// Which of the player's items are allowed to leave: not equipped, not favourited, not hotbar (if configured).
        public static bool CanLeave(ItemDrop.ItemData item, bool respectHotbar)
        {
            if (item.m_equipped || Favourites.Is(item) || SharedChest.IsLocked(item)) return false;
            if (respectHotbar && Plugin.StashKeepHotbar.Value && item.m_gridPos.y == 0) return false;
            return true;
        }

        /// Same rule as Inventory.StackAll (chest must already hold the item) with our exclusions and no message spam.
        public static int StackInto(Inventory chest, Inventory from)
        {
            int moved = 0;
            foreach (var item in new List<ItemDrop.ItemData>(from.GetAllItems()))
            {
                if (!CanLeave(item, respectHotbar: true)) continue;
                if (!chest.ContainsItemByName(item.m_shared.m_name)) continue;
                int stack = item.m_stack;
                if (chest.AddItem(item))
                {
                    from.RemoveItem(item);
                    moved += stack;
                }
            }
            return moved;
        }
    }

    /// The chest owner granted our stack: do it with our rules instead of the vanilla ones.
    [HarmonyPatch(typeof(Container), "RPC_StackResponse")]
    static class Container_RPC_StackResponse_Patch
    {
        static bool Prefix(Container __instance, bool granted)
        {
            if (!Stash.Pending.Remove(__instance)) return true;
            if (!granted || !Player.m_localPlayer) return false;
            var nview = __instance.GetComponent<ZNetView>();
            if (nview && nview.IsValid() && !nview.IsOwner())
            {
                // Someone has it open: the owner kept ownership, so each stack goes through them.
                SharedChest.PutMatching(__instance, Player.m_localPlayer.GetInventory());
                return false;
            }
            int n = Stash.StackInto(__instance.GetInventory(), Player.m_localPlayer.GetInventory());
            if (n > 0)
            {
                Stash.Moved += n;
                InventoryGui.instance.m_moveItemEffects.Create(__instance.transform.position, Quaternion.identity);
            }
            return false;
        }
    }

    /// Vanilla "Place stacks" on an open chest also respects favourites.
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.StackAll))]
    static class Inventory_StackAll_Favourites
    {
        static bool Prefix(Inventory __instance, Inventory fromInventory, bool message, ref int __result)
        {
            if (!Player.m_localPlayer || fromInventory != Player.m_localPlayer.GetInventory()) return true;
            if (SharedChest.IsGuest(__instance, out _)) return true; // handled by SharedChest
            int moved = 0;
            foreach (var item in new List<ItemDrop.ItemData>(fromInventory.GetAllItems()))
            {
                if (!Stash.CanLeave(item, respectHotbar: false)) continue;
                if (!__instance.ContainsItemByName(item.m_shared.m_name)) continue;
                int stack = item.m_stack;
                if (__instance.AddItem(item)) { fromInventory.RemoveItem(item); moved += stack; }
            }
            if (message)
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, moved > 0 ? "$msg_stackall " + moved : "$msg_stackall_none");
            Game.instance.IncrementPlayerStat(PlayerStatType.PlaceStacks);
            __result = moved;
            return false;
        }
    }

    /// A Stash button on the player's inventory panel, cloned from the chest's Take All button.
    [HarmonyPatch(typeof(InventoryGui), "Awake")]
    static class StashButton
    {
        static void Postfix(InventoryGui __instance)
        {
            if (!Plugin.StashButton.Value || !__instance.m_takeAllButton) return;

            var button = Object.Instantiate(__instance.m_takeAllButton, __instance.m_player);
            button.name = "IR_StashButton";
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => { if (Player.m_localPlayer) Stash.Run(Player.m_localPlayer); });

            var tmp = button.GetComponentInChildren<TMPro.TMP_Text>();
            if (tmp) { tmp.text = "»"; tmp.alignment = TMPro.TextAlignmentOptions.Center; tmp.enableAutoSizing = false; tmp.fontSize = 22f; }
            var legacy = button.GetComponentInChildren<Text>();
            if (legacy) legacy.text = "»";

            // A small square just under the Weight box, hung off the panel's bottom-right corner like the vanilla boxes are.
            var rt = button.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(Plugin.StashButtonSize.Value, Plugin.StashButtonSize.Value);
            rt.anchoredPosition = new Vector2(Plugin.StashButtonX.Value, Plugin.StashButtonY.Value);
            var tip = button.GetComponent<UITooltip>();
            if (tip) { tip.m_text = "Stash to nearby chests"; tip.m_topic = ""; }
        }
    }
}
