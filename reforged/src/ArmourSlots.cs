using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InventoryReforged
{
    /// A fifth inventory row whose first four slots are head / chest / legs / cape.
    /// Equipped armour lives there; unequipped armour is pushed back into the main grid.
    /// The slots are real inventory slots, so durability and repair work exactly as vanilla.
    /// The rest of the row is hidden and never used for auto-placement.
    public static class ArmourSlots
    {
        /// The armour row is always the last one: one past however many rows the player owns.
        static bool s_sized;
        static int RowOf(Inventory inv) => inv.GetHeight() - 1;
        static readonly ItemDrop.ItemData.ItemType[] Slots =
        {
            ItemDrop.ItemData.ItemType.Helmet,
            ItemDrop.ItemData.ItemType.Chest,
            ItemDrop.ItemData.ItemType.Legs,
            ItemDrop.ItemData.ItemType.Shoulder,
            ItemDrop.ItemData.ItemType.Utility,
        };
        static readonly string[] Labels = { "Head", "Chest", "Legs", "Cape", "Util" };
        static readonly Color SlotTint = new Color(0.85f, 0.7f, 0.45f, 1f);

        static readonly AccessTools.FieldRef<Inventory, int> Height = AccessTools.FieldRefAccess<Inventory, int>("m_height");
        static readonly AccessTools.FieldRef<InventoryGrid, List<InventoryElement>> Elements = AccessTools.FieldRefAccess<InventoryGrid, List<InventoryElement>>("m_elements");

        static bool On => Plugin.ArmourSlots.Value;
        static bool IsLocal(Inventory inv) => On && s_sized && Player.m_localPlayer && Player.m_localPlayer.GetInventory() == inv;

        public static int SlotFor(ItemDrop.ItemData item)
        {
            for (int i = 0; i < Slots.Length; i++) if (Slots[i] == item.m_shared.m_itemType) return i;
            return -1;
        }

        static bool InSlot(Inventory inv, ItemDrop.ItemData item) => item.m_gridPos.y == RowOf(inv);

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
            int row = RowOf(inv);
            foreach (var item in new List<ItemDrop.ItemData>(inv.GetAllItems()))
            {
                int slot = SlotFor(item);
                if (item.m_equipped && slot >= 0)
                {
                    var want = new Vector2i(slot, row);
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
                else if (InSlot(inv, item) && !item.m_equipped)
                {
                    var old = item.m_gridPos;
                    inv.RemoveItem(item);
                    if (!inv.AddItem(item)) inv.AddItem(item, old);
                }
            }
        }

        /// Vanilla sets the row count on spawn and when Haldor sells a row, then drops anything outside the grid.
        /// Same routine, one row taller, so the armour row is never "outside".
        [HarmonyPatch(typeof(Player), nameof(Player.SetInventorySize))]
        static class OneRowTaller
        {
            static bool Prefix(Player __instance, int rows)
            {
                if (!On) return true;
                rows = Mathf.Clamp(rows, 0, 9);
                __instance.GetInventory().SetHeight(rows + 1);
                __instance.AddUniqueKeyValue(Player.InventoryRowsKey, rows.ToString());
                InventoryGui.instance.SetInventorySize(rows + 1);
                if (__instance == Player.m_localPlayer) s_sized = true;
                __instance.DropInvalidItems();
                return false;
            }
        }

        [HarmonyPatch(typeof(Player), "Awake")]
        static class ResetOnNewPlayer
        {
            static void Postfix() => s_sized = false;
        }

        [HarmonyPatch(typeof(Player), "Update")]
        static class ReconcileEachFrame
        {
            static void Postfix(Player __instance)
            {
                if (On && __instance == Player.m_localPlayer) Reconcile(__instance);
            }
        }

        // Auto-placement (pickups, take all, stash, crafting output) must never land in the armour row,
        // and free-space maths must count only the main grid: its slots, and only the items in it.
        static readonly AccessTools.FieldRef<Inventory, List<ItemDrop.ItemData>> Items = AccessTools.FieldRefAccess<Inventory, List<ItemDrop.ItemData>>("m_inventory");

        static int MainFreeSlots(Inventory inv)
        {
            int row = RowOf(inv), used = 0;
            foreach (var it in Items(inv)) if (it.m_gridPos.y < row) used++;
            return inv.GetWidth() * row - used;
        }

        [HarmonyPatch(typeof(Inventory), "FindEmptySlot")]
        static class SlotSearchMainGridOnly
        {
            static void Prefix(Inventory __instance, ref int __state)
            {
                __state = -1;
                if (!IsLocal(__instance)) return;
                __state = Height(__instance);
                Height(__instance) = __state - 1;
            }
            static void Postfix(Inventory __instance, int __state)
            {
                if (__state >= 0) Height(__instance) = __state;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetEmptySlots))]
        static class CountMainGrid
        {
            static bool Prefix(Inventory __instance, ref int __result)
            {
                if (!IsLocal(__instance)) return true;
                __result = MainFreeSlots(__instance);
                return false;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.HaveEmptySlot))]
        static class HaveMainGridSlot
        {
            static bool Prefix(Inventory __instance, ref bool __result)
            {
                if (!IsLocal(__instance)) return true;
                __result = MainFreeSlots(__instance) > 0;
                return false;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.CanAddItem), typeof(ItemDrop.ItemData), typeof(int))]
        static class CanAddToMainGrid
        {
            static bool Prefix(Inventory __instance, ItemDrop.ItemData item, int stack, ref bool __result)
            {
                if (!IsLocal(__instance)) return true;
                if (stack <= 0) stack = item.m_stack;
                __result = __instance.FindFreeStackSpace(item.m_shared.m_name, item.m_worldLevel) + MainFreeSlots(__instance) * item.m_shared.m_maxStackSize >= stack;
                return false;
            }
        }

        /// Only the right kind of armour may be dropped on a slot; dropping it there equips it.
        [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.DropItem))]
        static class DropRules
        {
            static bool Prefix(InventoryGrid __instance, ItemDrop.ItemData item, Vector2i pos, ref bool __result)
            {
                if (!IsLocal(__instance.GetInventory()) || pos.y != RowOf(__instance.GetInventory())) return true;
                if (pos.x >= Slots.Length || SlotFor(item) != pos.x) { __result = false; return false; }
                return true;
            }

            static void Postfix(InventoryGrid __instance, Vector2i pos, bool __result)
            {
                if (!__result || !IsLocal(__instance.GetInventory()) || pos.y != RowOf(__instance.GetInventory())) return;
                var landed = __instance.GetInventory().GetItemAt(pos.x, pos.y);
                if (landed != null && !landed.m_equipped) Player.m_localPlayer.EquipItem(landed);
            }
        }

        /// Label the slots and hide the unused cells once the grid builds the row.
        /// Warm tint on the cell background so the row reads as equipment, not storage.
        static void Tint(InventoryElement el)
        {
            var img = el.GetComponent<Image>();
            if (!img)
            {
                var bkg = el.transform.Find("bkg") ?? el.transform.Find("background");
                if (bkg) img = bkg.GetComponent<Image>();
            }
            if (img) img.color = SlotTint;
        }

        [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
        static class DressRow
        {
            static void Postfix(InventoryGrid __instance)
            {
                var inv = __instance.GetInventory();
                if (!IsLocal(inv)) return;
                int row = RowOf(inv);
                foreach (var el in Elements(__instance))
                {
                    if (!el || el.Position.y != row) continue;
                    if (el.Position.x >= Slots.Length)
                    {
                        if (el.gameObject.activeSelf) el.gameObject.SetActive(false);
                        continue;
                    }
                    var label = el.transform.Find("binding")?.GetComponent<TMP_Text>();
                    if (label && !label.enabled)
                    {
                        // The binding label is sized for one digit; let it span the cell and never wrap.
                        label.enabled = true;
                        label.text = Labels[el.Position.x];
                        label.fontSize *= 0.8f;
                        label.enableWordWrapping = false;
                        label.overflowMode = TextOverflowModes.Overflow;
                        label.alignment = TextAlignmentOptions.TopLeft;
                        var lrt = label.rectTransform;
                        lrt.anchorMin = new Vector2(0f, 0f);
                        lrt.anchorMax = new Vector2(1f, 1f);
                        lrt.offsetMin = new Vector2(4f, 0f);
                        lrt.offsetMax = new Vector2(0f, -2f);
                        Tint(el);
                    }
                }
            }
        }
    }
}
