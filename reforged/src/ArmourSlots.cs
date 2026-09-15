using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace InventoryReforged
{
    /// A fifth inventory row whose first four slots are head / chest / legs / cape.
    /// Equipped armour lives there; unequipped armour is pushed back into the main grid.
    /// The slots are real inventory slots, so durability and repair work exactly as vanilla.
    /// The rest of the row is hidden and never used for auto-placement.
    public static class ArmourSlots
    {
        public const int Row = 4;
        public const int Rows = 5;
        static readonly ItemDrop.ItemData.ItemType[] Slots =
        {
            ItemDrop.ItemData.ItemType.Helmet,
            ItemDrop.ItemData.ItemType.Chest,
            ItemDrop.ItemData.ItemType.Legs,
            ItemDrop.ItemData.ItemType.Shoulder,
        };
        static readonly string[] Labels = { "Head", "Chest", "Legs", "Cape" };

        static readonly AccessTools.FieldRef<Inventory, int> Height = AccessTools.FieldRefAccess<Inventory, int>("m_height");
        static readonly AccessTools.FieldRef<InventoryGrid, List<InventoryElement>> Elements = AccessTools.FieldRefAccess<InventoryGrid, List<InventoryElement>>("m_elements");

        static bool On => Plugin.ArmourSlots.Value;
        static bool IsLocal(Inventory inv) => On && Player.m_localPlayer && Player.m_localPlayer.GetInventory() == inv;

        public static int SlotFor(ItemDrop.ItemData item)
        {
            for (int i = 0; i < Slots.Length; i++) if (Slots[i] == item.m_shared.m_itemType) return i;
            return -1;
        }

        static bool InSlot(ItemDrop.ItemData item) => item.m_gridPos.y == Row;

        /// Move an item to a grid position, going through Remove/Add so listeners fire.
        static void Relocate(Inventory inv, ItemDrop.ItemData item, Vector2i pos)
        {
            inv.RemoveItem(item);
            if (!inv.AddItem(item, pos)) inv.AddItem(item);
        }

        /// Keep the row honest: equipped armour in its slot, anything unequipped out of it.
        static void Reconcile(Player player)
        {
            var inv = player.GetInventory();
            foreach (var item in new List<ItemDrop.ItemData>(inv.GetAllItems()))
            {
                int slot = SlotFor(item);
                if (item.m_equipped && slot >= 0)
                {
                    var want = new Vector2i(slot, Row);
                    if (item.m_gridPos == want) continue;
                    var occupant = inv.GetItemAt(want.x, want.y);
                    if (occupant != null)
                    {
                        // Only an unequipped item can be in the way; push it out to the main grid first.
                        var old = occupant.m_gridPos;
                        inv.RemoveItem(occupant);
                        if (!inv.AddItem(occupant)) { inv.AddItem(occupant, old); continue; }
                    }
                    Relocate(inv, item, want);
                }
                else if (InSlot(item) && !item.m_equipped)
                {
                    var old = item.m_gridPos;
                    inv.RemoveItem(item);
                    if (!inv.AddItem(item)) inv.AddItem(item, old);
                }
            }
        }

        [HarmonyPatch(typeof(Player), "Awake")]
        static class GrowInventory
        {
            static void Postfix(Player __instance)
            {
                if (On) __instance.GetInventory().SetHeight(Rows);
            }
        }

        [HarmonyPatch(typeof(Player), "Update")]
        static class ReconcileEachFrame
        {
            static void Postfix(Player __instance)
            {
                if (On && __instance == Player.m_localPlayer) Reconcile(__instance);
            }
        }

        // Auto-placement (pickups, take all, stash, crafting output) must never land in the armour row.
        // These vanilla helpers scan m_height rows; shrink it to the main grid while they run.
        static class MainGridOnly
        {
            public static void Prefix(Inventory __instance, ref int __state)
            {
                __state = -1;
                if (!IsLocal(__instance)) return;
                __state = Height(__instance);
                Height(__instance) = Row;
            }
            public static void Postfix(Inventory __instance, int __state)
            {
                if (__state >= 0) Height(__instance) = __state;
            }
        }

        [HarmonyPatch(typeof(Inventory), "FindEmptySlot")]
        static class Gate1 { static void Prefix(Inventory __instance, ref int __state) => MainGridOnly.Prefix(__instance, ref __state); static void Postfix(Inventory __instance, int __state) => MainGridOnly.Postfix(__instance, __state); }
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetEmptySlots))]
        static class Gate2 { static void Prefix(Inventory __instance, ref int __state) => MainGridOnly.Prefix(__instance, ref __state); static void Postfix(Inventory __instance, int __state) => MainGridOnly.Postfix(__instance, __state); }
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.HaveEmptySlot))]
        static class Gate3 { static void Prefix(Inventory __instance, ref int __state) => MainGridOnly.Prefix(__instance, ref __state); static void Postfix(Inventory __instance, int __state) => MainGridOnly.Postfix(__instance, __state); }
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.CanAddItem), typeof(ItemDrop.ItemData), typeof(int))]
        static class Gate4 { static void Prefix(Inventory __instance, ref int __state) => MainGridOnly.Prefix(__instance, ref __state); static void Postfix(Inventory __instance, int __state) => MainGridOnly.Postfix(__instance, __state); }

        /// Only the right kind of armour may be dropped on a slot; dropping it there equips it.
        [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.DropItem))]
        static class DropRules
        {
            static bool Prefix(InventoryGrid __instance, ItemDrop.ItemData item, Vector2i pos, ref bool __result)
            {
                if (!IsLocal(__instance.GetInventory()) || pos.y != Row) return true;
                if (pos.x >= Slots.Length || SlotFor(item) != pos.x) { __result = false; return false; }
                return true;
            }

            static void Postfix(InventoryGrid __instance, Vector2i pos, bool __result)
            {
                if (!__result || !IsLocal(__instance.GetInventory()) || pos.y != Row) return;
                var landed = __instance.GetInventory().GetItemAt(pos.x, pos.y);
                if (landed != null && !landed.m_equipped) Player.m_localPlayer.EquipItem(landed);
            }
        }

        /// Make room for the row on the panel, and label the slots / hide the unused ones once the grid builds them.
        [HarmonyPatch(typeof(InventoryGui), "Awake")]
        static class GrowPanel
        {
            static void Postfix(InventoryGui __instance)
            {
                if (!On) return;
                float grow = Plugin.ArmourRowOffset.Value;
                __instance.m_player.sizeDelta += new Vector2(0f, grow);
                __instance.m_container.anchoredPosition += new Vector2(0f, -grow);
            }
        }

        [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
        static class DressRow
        {
            static void Postfix(InventoryGrid __instance)
            {
                if (!IsLocal(__instance.GetInventory())) return;
                foreach (var el in Elements(__instance))
                {
                    if (!el || el.Position.y != Row) continue;
                    if (el.Position.x >= Slots.Length)
                    {
                        if (el.gameObject.activeSelf) el.gameObject.SetActive(false);
                        continue;
                    }
                    var label = el.transform.Find("binding")?.GetComponent<TMP_Text>();
                    if (label && !label.enabled)
                    {
                        label.enabled = true;
                        label.text = Labels[el.Position.x];
                        label.fontSize *= 0.8f;
                    }
                }
            }
        }
    }
}
