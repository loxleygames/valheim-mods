using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace TrophyPouch
{
    /// Quick-stacking into a chest also empties matching trophies out of the pouch — the trophy chest
    /// at a shared base keeps working. Hooks vanilla "Place stacks", and Inventory Reforged's Stash if present.
    public static class StashHook
    {
        /// Move every pouch trophy the chest already holds into it. Returns items moved.
        public static int PourInto(Inventory chest)
        {
            int moved = 0;
            foreach (var kv in new List<KeyValuePair<string, int>>(Pouch.All))
            {
                var go = ObjectDB.instance.GetItemPrefab(kv.Key);
                var drop = go ? go.GetComponent<ItemDrop>() : null;
                if (!drop || !chest.ContainsItemByName(drop.m_itemData.m_shared.m_name)) continue;
                int left = kv.Value;
                while (left > 0)
                {
                    var clone = drop.m_itemData.Clone();
                    clone.m_stack = Mathf.Min(left, clone.m_shared.m_maxStackSize);
                    int want = clone.m_stack;
                    bool all = chest.AddItem(clone);
                    int got = all ? want : want - clone.m_stack;
                    if (got <= 0) break;
                    Pouch.RemoveByPrefab(kv.Key, got);
                    moved += got;
                    left -= got;
                    if (!all) break;
                }
            }
            return moved;
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.StackAll))]
        static class VanillaPlaceStacks
        {
            static void Postfix(Inventory __instance, Inventory fromInventory, ref int __result)
            {
                if (!Pouch.IsLocal(fromInventory) || Pouch.IsLocal(__instance)) return;
                __result += PourInto(__instance);
            }
        }

        /// Inventory Reforged's Stash: patched by hand because it's a soft dependency.
        public static void TryHookReforged(Harmony harmony)
        {
            var stash = Type.GetType("InventoryReforged.Stash, InventoryReforged");
            var method = stash?.GetMethod("StackInto", BindingFlags.Public | BindingFlags.Static);
            if (method == null) return;
            harmony.Patch(method, postfix: new HarmonyMethod(typeof(StashHook), nameof(ReforgedPostfix)));
        }

        static void ReforgedPostfix(Inventory chest, Inventory from, ref int __result)
        {
            if (Pouch.IsLocal(from)) __result += PourInto(chest);
        }
    }
}
