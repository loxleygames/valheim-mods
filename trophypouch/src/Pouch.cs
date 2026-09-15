using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace TrophyPouch
{
    /// Trophies live in a tally on the character, not in the bag. Saved in the character file, so
    /// they survive death and never take a slot. Crafting and building see them as if they were held.
    public static class Pouch
    {
        const string Key = "TrophyPouch";
        static readonly Dictionary<string, int> s_counts = new Dictionary<string, int>();   // prefab name -> count
        static readonly Dictionary<string, string> s_prefabByShared = new Dictionary<string, string>();
        static Player s_loadedFor;
        public static bool Bypass;   // set while we move an item out of the pouch into the bag
        public static bool Dirty = true;

        public static IEnumerable<KeyValuePair<string, int>> All => s_counts;
        public static int Total { get { int n = 0; foreach (var v in s_counts.Values) n += v; return n; } }

        public static bool IsTrophy(ItemDrop.ItemData item) => item?.m_shared != null && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy;
        public static bool IsLocal(Inventory inv) => Player.m_localPlayer && Player.m_localPlayer.GetInventory() == inv;

        static void EnsureLoaded()
        {
            var p = Player.m_localPlayer;
            if (!p || s_loadedFor == p) return;
            s_loadedFor = p;
            s_counts.Clear();
            if (p.m_customData.TryGetValue(Key, out var data))
                foreach (var entry in data.Split(','))
                {
                    var kv = entry.Split(':');
                    if (kv.Length == 2 && int.TryParse(kv[1], out var n) && n > 0) s_counts[kv[0]] = n;
                }
            Dirty = true;
        }

        static void Save()
        {
            var p = Player.m_localPlayer;
            if (!p) return;
            var sb = new StringBuilder();
            foreach (var kv in s_counts) { if (sb.Length > 0) sb.Append(','); sb.Append(kv.Key).Append(':').Append(kv.Value); }
            p.m_customData[Key] = sb.ToString();
            Dirty = true;
        }

        public static string PrefabName(ItemDrop.ItemData item)
        {
            if (item.m_dropPrefab) return item.m_dropPrefab.name;
            return SharedToPrefab(item.m_shared.m_name);
        }

        static string SharedToPrefab(string shared)
        {
            if (s_prefabByShared.Count == 0 && ObjectDB.instance)
                foreach (var go in ObjectDB.instance.m_items)
                {
                    var d = go ? go.GetComponent<ItemDrop>() : null;
                    if (d && IsTrophy(d.m_itemData)) s_prefabByShared[d.m_itemData.m_shared.m_name] = go.name;
                }
            return s_prefabByShared.TryGetValue(shared, out var p) ? p : null;
        }

        public static void Add(ItemDrop.ItemData item, int amount)
        {
            EnsureLoaded();
            var name = PrefabName(item);
            if (name == null || amount <= 0) return;
            s_counts.TryGetValue(name, out var n);
            s_counts[name] = n + amount;
            Save();
        }

        public static int Count(string sharedName)
        {
            EnsureLoaded();
            var prefab = SharedToPrefab(sharedName);
            return prefab != null && s_counts.TryGetValue(prefab, out var n) ? n : 0;
        }

        public static int Remove(string sharedName, int amount)
        {
            EnsureLoaded();
            var prefab = SharedToPrefab(sharedName);
            if (prefab == null || !s_counts.TryGetValue(prefab, out var n)) return 0;
            int take = Mathf.Min(n, amount);
            if (n - take <= 0) s_counts.Remove(prefab); else s_counts[prefab] = n - take;
            Save();
            return take;
        }

        /// One trophy out of the pouch and into the bag.
        public static bool Take(string prefab)
        {
            EnsureLoaded();
            var p = Player.m_localPlayer;
            if (!p || !s_counts.TryGetValue(prefab, out var n) || n <= 0) return false;
            var go = ObjectDB.instance.GetItemPrefab(prefab);
            if (!go) return false;
            Bypass = true;
            bool ok;
            try { ok = p.GetInventory().AddItem(go, 1); }
            finally { Bypass = false; }
            if (!ok) { p.Message(MessageHud.MessageType.Center, "$msg_noroom"); return false; }
            if (n - 1 <= 0) s_counts.Remove(prefab); else s_counts[prefab] = n - 1;
            Save();
            return true;
        }

        /// Move any trophies already in the bag into the pouch (once per spawn).
        public static void Sweep(Player p)
        {
            var inv = p.GetInventory();
            foreach (var item in new List<ItemDrop.ItemData>(inv.GetAllItems()))
            {
                if (!IsTrophy(item)) continue;
                Add(item, item.m_stack);
                inv.RemoveItem(item);
            }
        }

        // ---- capture: trophies never land in the bag ----

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), typeof(ItemDrop.ItemData))]
        static class CaptureAdd
        {
            static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
            {
                if (Bypass || !IsLocal(__instance) || !IsTrophy(item)) return true;
                Add(item, item.m_stack);
                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(Inventory), "AddItem", typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int), typeof(bool))]
        static class CaptureAddAt
        {
            static bool Prefix(Inventory __instance, ItemDrop.ItemData item, int amount, ref bool __result)
            {
                if (Bypass || !IsLocal(__instance) || !IsTrophy(item)) return true;
                amount = Mathf.Min(amount, item.m_stack);
                Add(item, amount);
                item.m_stack -= amount;
                __result = true;
                return false;
            }
        }

        static bool s_swept;
        [HarmonyPatch(typeof(Player), "Update")]
        static class SweepOnSpawn
        {
            static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer) { return; }
                if (!s_swept && TrophyPouchPlugin.SweepOnLoad.Value) { s_swept = true; Sweep(__instance); }
            }
        }
        [HarmonyPatch(typeof(Player), "Awake")]
        static class ResetSweep { static void Postfix() => s_swept = false; }

        // ---- pooling: crafting, building and altars count the pouch ----

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.CountItems))]
        static class PoolCount
        {
            static void Postfix(Inventory __instance, string name, ref int __result)
            {
                if (!Bypass && IsLocal(__instance)) __result += Count(name);
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.HaveItem), typeof(string), typeof(bool))]
        static class PoolHave
        {
            static void Postfix(Inventory __instance, string name, ref bool __result)
            {
                if (!__result && !Bypass && IsLocal(__instance)) __result = Count(name) > 0;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem), typeof(string), typeof(int), typeof(int), typeof(bool))]
        static class PoolRemove
        {
            static void Prefix(Inventory __instance, string name, ref int amount)
            {
                if (!IsLocal(__instance) || amount <= 0 || Count(name) == 0) return;
                // Bag first (vanilla will take those), then the pouch covers the rest.
                Bypass = true;
                int inBag;
                try { inBag = __instance.CountItems(name); }
                finally { Bypass = false; }
                int fromPouch = Mathf.Max(0, amount - inBag);
                if (fromPouch > 0) { Remove(name, fromPouch); amount -= fromPouch; }
            }
        }
    }
}
