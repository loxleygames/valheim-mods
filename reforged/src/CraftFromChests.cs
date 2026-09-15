using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace InventoryReforged
{
    /// Nearby chests count as part of your inventory for crafting, building, and feeding stations.
    ///
    /// Crafting, building, fuel and fireworks all go through three calls on the player's inventory:
    /// CountItems(name), HaveItem(name), RemoveItem(name, amount). Those are pooled with nearby chests,
    /// but only while inside a crafting/building/station code path, so unrelated "do you have X" checks
    /// (quests, tutorials, ammo) stay honest. Ore and food are different: stations take an item *object*
    /// from your inventory, so for those one item is pulled from a chest into your inventory first.
    public static class CraftFromChests
    {
        static int s_depth;      // > 0 while a pooled code path is running
        static bool s_bypass;    // set while we do our own inventory arithmetic inside a patch
        static bool Active => Plugin.CraftFromChests.Value && s_depth > 0 && !s_bypass;

        static bool IsLocal(Inventory inv) => Player.m_localPlayer && Player.m_localPlayer.GetInventory() == inv;

        static float s_cacheTime = -1f;
        static List<Container> s_cache = new List<Container>();

        /// Player-built chests in range that nobody else has open right now.
        public static List<Container> Nearby()
        {
            var p = Player.m_localPlayer;
            if (!p) return new List<Container>();
            if (Time.time - s_cacheTime > 0.5f)
            {
                s_cacheTime = Time.time;
                s_cache = Stash.FindNearby(p.transform.position, Plugin.CraftRange.Value);
                s_cache.RemoveAll(c =>
                {
                    var nview = c.GetComponent<ZNetView>();
                    return !nview.IsOwner() && nview.GetZDO().GetInt(ZDOVars.s_inUse) == 1;
                });
            }
            return s_cache;
        }

        static int ChestCount(string name, int quality, bool wl)
        {
            int n = 0;
            foreach (var c in Nearby()) n += c.GetInventory().CountItems(name, quality, wl);
            return n;
        }

        static void ChestRemove(string name, int amount, int quality, bool wl)
        {
            foreach (var c in Nearby())
            {
                if (amount <= 0) return;
                var inv = c.GetInventory();
                int have = inv.CountItems(name, quality, wl);
                if (have <= 0) continue;
                int take = Mathf.Min(have, amount);
                c.GetComponent<ZNetView>().ClaimOwnership();
                inv.RemoveItem(name, take, quality, wl);
                amount -= take;
            }
        }

        /// Move one `name` item from a chest into the player's inventory. Returns the item now held.
        public static ItemDrop.ItemData PullOne(Humanoid user, string name)
        {
            var mine = user.GetInventory();
            foreach (var c in Nearby())
            {
                var inv = c.GetInventory();
                var item = inv.GetItem(name);
                if (item == null) continue;
                if (!mine.CanAddItem(item, 1)) { user.Message(MessageHud.MessageType.Center, "$msg_noroom"); return null; }
                c.GetComponent<ZNetView>().ClaimOwnership();
                var clone = item.Clone();
                clone.m_stack = 1;
                inv.RemoveItem(item, 1);
                if (mine.AddItem(clone)) return mine.GetItem(name);
                inv.AddItem(clone);
                return null;
            }
            return null;
        }

        static ItemDrop.ItemData FirstOwned(Humanoid user, IEnumerable<ItemDrop> froms)
        {
            foreach (var f in froms) if (f) { var it = user.GetInventory().GetItem(f.m_itemData.m_shared.m_name); if (it != null) return it; }
            return null;
        }

        static void PullCookable(Humanoid user, IEnumerable<ItemDrop> froms)
        {
            if (!Plugin.CraftFromChests.Value || !user || user != Player.m_localPlayer) return;
            if (FirstOwned(user, froms) != null) return;
            foreach (var f in froms) if (f && PullOne(user, f.m_itemData.m_shared.m_name) != null) return;
        }

        // ---- pooled inventory calls ----

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.CountItems))]
        static class PoolCount
        {
            static void Postfix(Inventory __instance, string name, int quality, bool matchWorldLevel, ref int __result)
            {
                if (Active && IsLocal(__instance)) __result += ChestCount(name, quality, matchWorldLevel);
            }
        }

        /// Smelter.CanUseItems counts ore this way; pooling it makes the switch usable with ore only in chests.
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.CountItemsByName))]
        static class PoolCountByName
        {
            static void Postfix(Inventory __instance, string[] names, int quality, bool matchWorldLevel, ref int __result)
            {
                if (!Active || !IsLocal(__instance)) return;
                foreach (var n in names) __result += ChestCount(n, quality, matchWorldLevel);
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.HaveItem), typeof(string), typeof(bool))]
        static class PoolHave
        {
            static void Postfix(Inventory __instance, string name, bool matchWorldLevel, ref bool __result)
            {
                if (!__result && Active && IsLocal(__instance)) __result = ChestCount(name, -1, matchWorldLevel) > 0;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem), typeof(string), typeof(int), typeof(int), typeof(bool))]
        static class PoolRemove
        {
            static bool Prefix(Inventory __instance, string name, int amount, int itemQuality, bool worldLevelBased)
            {
                if (!Active || !IsLocal(__instance)) return true;
                s_bypass = true;
                try
                {
                    int own = __instance.CountItems(name, itemQuality, worldLevelBased);
                    int fromSelf = Mathf.Min(own, amount);
                    if (fromSelf > 0) __instance.RemoveItem(name, fromSelf, itemQuality, worldLevelBased);
                    if (amount - fromSelf > 0) ChestRemove(name, amount - fromSelf, itemQuality, worldLevelBased);
                }
                finally { s_bypass = false; }
                return false;
            }
        }

        // ---- the code paths where pooling is on ----

        static void Enter() => s_depth++;
        static void Exit() => s_depth--;

        [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), typeof(Recipe), typeof(bool), typeof(int), typeof(int))]
        static class Scope1 { static void Prefix() => Enter(); static void Postfix() => Exit(); }
        [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), typeof(Piece), typeof(Player.RequirementMode))]
        static class Scope2 { static void Prefix() => Enter(); static void Postfix() => Exit(); }
        [HarmonyPatch(typeof(Player), nameof(Player.GetFirstRequiredItem))]
        static class Scope3 { static void Prefix() => Enter(); static void Postfix() => Exit(); }
        [HarmonyPatch(typeof(Player), nameof(Player.ConsumeResources))]
        static class Scope4 { static void Prefix() => Enter(); static void Postfix() => Exit(); }
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.SetupRequirement))]
        static class Scope5 { static void Prefix() => Enter(); static void Postfix() => Exit(); }
        [HarmonyPatch(typeof(Smelter), "OnAddFuel")]
        static class Scope6 { static void Prefix() => Enter(); static void Postfix() => Exit(); }
        [HarmonyPatch(typeof(Smelter), nameof(Smelter.CanUseItems))]
        static class Scope7 { static void Prefix() => Enter(); static void Postfix() => Exit(); }
        [HarmonyPatch(typeof(CookingStation), "OnAddFuelSwitch")]
        static class Scope8 { static void Prefix() => Enter(); static void Postfix() => Exit(); }
        [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.CanUseItems))]
        static class Scope9 { static void Prefix() => Enter(); static void Postfix() => Exit(); }
        [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.Interact))]
        static class Scope10 { static void Prefix() => Enter(); static void Postfix() => Exit(); }
        [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.UseItem))]
        static class Scope11 { static void Prefix() => Enter(); static void Postfix() => Exit(); }
        [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.CanUseItems))]
        static class Scope12 { static void Prefix() => Enter(); static void Postfix() => Exit(); }

        // ---- ore and food: pull one into the player's hands before the station looks ----

        static IEnumerable<ItemDrop> Froms(List<Smelter.ItemConversion> l) { foreach (var c in l) yield return c.m_from; }
        static IEnumerable<ItemDrop> Froms(List<CookingStation.ItemConversion> l) { foreach (var c in l) yield return c.m_from; }
        static IEnumerable<ItemDrop> Froms(List<Fermenter.ItemConversion> l) { foreach (var c in l) yield return c.m_from; }

        [HarmonyPatch(typeof(Smelter), "OnAddOre")]
        static class PullOre
        {
            static void Prefix(Smelter __instance, Humanoid user, ItemDrop.ItemData item)
            {
                if (item == null) PullCookable(user, Froms(__instance.m_conversion));
            }
        }

        [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.Interact))]
        static class PullFood
        {
            static void Prefix(CookingStation __instance, Humanoid user, bool hold)
            {
                if (!hold) PullCookable(user, Froms(__instance.m_conversion));
            }
        }

        [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.Interact))]
        static class PullBrew
        {
            static void Prefix(Fermenter __instance, Humanoid user, bool hold)
            {
                if (!hold) PullCookable(user, Froms(__instance.m_conversion));
            }
        }
    }
}
