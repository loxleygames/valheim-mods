using HarmonyLib;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace InventoryReforged
{
    /// Alt+click an item to favourite it. Favourites are never stashed or place-stacked.
    /// Stored in the item's custom data so it survives saves and travels with the item.
    public static class Favourites
    {
        const string Key = "IR_fav";

        public static bool Is(ItemDrop.ItemData item) => item?.m_customData != null && item.m_customData.ContainsKey(Key);

        public static void Toggle(ItemDrop.ItemData item)
        {
            if (Is(item)) item.m_customData.Remove(Key);
            else item.m_customData[Key] = "1";
        }

        static bool AltHeld() => ZInput.GetKey(KeyCode.LeftAlt) || ZInput.GetKey(KeyCode.RightAlt);

        [HarmonyPatch(typeof(InventoryGui), "OnSelectedItem")]
        static class ToggleOnAltClick
        {
            static readonly AccessTools.FieldRef<InventoryGui, GameObject> DragGo = AccessTools.FieldRefAccess<InventoryGui, GameObject>("m_dragGo");

            static bool Prefix(InventoryGui __instance, InventoryGrid grid, ItemDrop.ItemData item, InventoryGrid.Modifier mod)
            {
                if (item == null || mod != InventoryGrid.Modifier.Select || !AltHeld() || DragGo(__instance)) return true;
                if (!Player.m_localPlayer || grid.GetInventory() != Player.m_localPlayer.GetInventory()) return true;
                Toggle(item);
                return false;
            }
        }

        /// A small star on favourited items, cloned from the hotbar number label each slot already has.
        [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
        static class DrawStar
        {
            static readonly AccessTools.FieldRef<InventoryGrid, List<InventoryElement>> Elements = AccessTools.FieldRefAccess<InventoryGrid, List<InventoryElement>>("m_elements");
            static readonly Dictionary<InventoryElement, TMP_Text> s_stars = new Dictionary<InventoryElement, TMP_Text>();

            static void Postfix(InventoryGrid __instance)
            {
                var inv = __instance.GetInventory();
                if (inv == null) return;
                foreach (var el in Elements(__instance))
                {
                    if (!el) continue;
                    var item = inv.GetItemAt(el.Position.x, el.Position.y);
                    bool fav = item != null && Is(item);
                    if (!s_stars.TryGetValue(el, out var star) || !star)
                    {
                        if (!fav) continue;
                        star = MakeStar(el);
                        if (!star) continue;
                        s_stars[el] = star;
                    }
                    star.enabled = fav;
                }
            }

            static TMP_Text MakeStar(InventoryElement el)
            {
                var binding = el.transform.Find("binding");
                if (!binding) return null;
                var go = Object.Instantiate(binding.gameObject, el.transform);
                go.name = "IR_star";
                var text = go.GetComponent<TMP_Text>();
                text.text = text.font && text.font.HasCharacter('★') ? "★" : "*";
                text.color = new Color(1f, 0.85f, 0.2f);
                text.alignment = TextAlignmentOptions.TopRight;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = new Vector2(2f, 2f);
                rt.offsetMax = new Vector2(-4f, -2f);
                return text;
            }
        }
    }
}
