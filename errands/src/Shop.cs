using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Errands
{
    /// The dvergr's wares. Vanilla's Trader/StoreGui do the work: the shelves are TradeItems on the
    /// instance, the currency is swapped to marks while his window is open, the list is filtered by
    /// standing instead of global keys, and selling is switched off so marks stay earned.
    public static class Shop
    {
        public const int GuestRep = 30;
        public const int KinRep = 80;

        public class Shelf
        {
            public Trader.TradeItem Item;
            public int Rep;
        }

        public static string TierName(int rep) => rep >= KinRep ? "Kin" : rep >= GuestRep ? "Guest" : "Outsider";
        public static string TierColour(int rep) => rep >= KinRep ? "#7fd97f" : rep >= GuestRep ? "#e0b04a" : "#9a9a9a";
        /// "Guest" in its colour, for any rich-text field.
        public static string TierTag(int rep) => $"<color={TierColour(rep)}>{TierName(rep)}</color>";

        static readonly Dictionary<Heightmap.Biome, (string prefab, int stack, int price)[]> s_goods =
            new Dictionary<Heightmap.Biome, (string, int, int)[]>
            {
                [Heightmap.Biome.Meadows] = new[] { ("LeatherScraps", 20, 3), ("DeerHide", 10, 4), ("MeadHealthMinor", 3, 2) },
                [Heightmap.Biome.BlackForest] = new[] { ("Bronze", 5, 6), ("SurtlingCore", 5, 5), ("MeadStaminaMinor", 3, 2) },
                [Heightmap.Biome.Swamp] = new[] { ("Iron", 5, 6), ("Chain", 3, 4), ("MeadPoisonResist", 3, 3) },
                [Heightmap.Biome.Mountain] = new[] { ("Silver", 5, 6), ("Crystal", 4, 5), ("MeadFrostResist", 3, 3) },
                [Heightmap.Biome.Plains] = new[] { ("BlackMetal", 5, 6), ("Tar", 10, 4), ("MeadStaminaMedium", 3, 3) },
                [Heightmap.Biome.Mistlands] = new[] { ("Eitr", 3, 6), ("Sap", 10, 4), ("MeadEitrMinor", 3, 3) },
                [Heightmap.Biome.AshLands] = new[] { ("FlametalNew", 3, 6), ("Grausten", 20, 4), ("MeadHealthMajor", 3, 4) },
            };

        /// Build a biome's shelves onto a dvergr's Trader. Standing gates: chest at Outsider, goods and
        /// the lesser vendor unique at Guest, the better one at Kin. Uniques sell once per character.
        public static List<Shelf> Stock(Trader trader, Heightmap.Biome biome)
        {
            var shelves = new List<Shelf>();
            trader.m_items = new List<Trader.TradeItem>();

            Add(shelves, Rewards.ChestPrefab(biome), 1, 8, 0);
            if (s_goods.TryGetValue(biome, out var goods))
                foreach (var (prefab, stack, price) in goods) Add(shelves, prefab, stack, price, GuestRep);

            var vendor = Uniques.VendorUniques(biome);
            if (vendor.Count > 0) Add(shelves, vendor[0], 1, 25, GuestRep, once: true);
            if (vendor.Count > 1) Add(shelves, vendor[1], 1, 50, KinRep, once: true);

            foreach (var s in shelves) trader.m_items.Add(s.Item);
            return shelves;
        }

        static void Add(List<Shelf> shelves, string prefab, int stack, int price, int rep, bool once = false)
        {
            var go = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            var drop = go ? go.GetComponent<ItemDrop>() : null;
            if (!drop) { Jotunn.Logger.LogWarning($"Errands: shop item {prefab} missing"); return; }
            shelves.Add(new Shelf
            {
                Rep = rep,
                Item = new Trader.TradeItem
                {
                    m_prefab = drop, m_stack = stack, m_price = price,
                    m_buyKey = once ? "errands_bought_" + prefab : "",
                    // Vanilla reads these without null checks.
                    m_tooltip = "", m_name = "", m_requiredGlobalKey = "", m_incrementKey = "",
                },
            });
        }

        // ---- vanilla hooks ----

        [HarmonyPatch(typeof(Trader), nameof(Trader.GetAvailableItems))]
        static class FilterByStanding
        {
            static bool Prefix(Trader __instance, ref List<Trader.TradeItem> __result)
            {
                var dvergr = __instance.GetComponent<Dvergr>();
                if (!dvergr || dvergr.Shelves == null) return true;
                int rep = Journal.Rep(dvergr.Biome);
                __result = new List<Trader.TradeItem>();
                foreach (var s in dvergr.Shelves)
                {
                    if (s.Rep > rep) continue;
                    if (!string.IsNullOrEmpty(s.Item.m_buyKey) && Player.m_localPlayer && Player.m_localPlayer.HaveUniqueKey(s.Item.m_buyKey)) continue;
                    __result.Add(s.Item);
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(StoreGui), nameof(StoreGui.Show))]
        static class SwapCurrency
        {
            static ItemDrop s_coins;
            public static ItemDrop Coins => s_coins;

            static void Prefix(StoreGui __instance, Trader trader)
            {
                if (s_coins == null) s_coins = __instance.m_coinPrefab;
                var mark = ObjectDB.instance.GetItemPrefab(Rewards.MarkPrefab);
                __instance.m_coinPrefab = trader && trader.GetComponent<Dvergr>() && mark ? mark.GetComponent<ItemDrop>() : s_coins;
            }
        }

        /// The window shows coins three ways: the price icon per row, the pile under Buy, the sell button.
        /// For him: swap the item icon for the mark, tint anything else coin-shaped silver, hide Sell.
        [HarmonyPatch(typeof(StoreGui), "FillList")]
        static class SwapCoinIcons
        {
            static void Postfix(StoreGui __instance, Trader ___m_trader)
            {
                var coins = SwapCurrency.Coins;
                var markGo = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(Rewards.MarkPrefab) : null;
                if (!coins || !markGo) return;
                var coinSprite = coins.m_itemData.m_shared.m_icons[0];
                var markSprite = markGo.GetComponent<ItemDrop>().m_itemData.m_shared.m_icons[0];
                bool ours = ___m_trader && ___m_trader.GetComponent<Dvergr>();

                if (__instance.m_sellButton) __instance.m_sellButton.transform.parent.gameObject.SetActive(!ours);

                foreach (var img in __instance.m_rootPanel.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                {
                    if (!img.sprite) continue;
                    if (img.sprite == coinSprite) { if (ours) img.sprite = markSprite; }
                    else if (img.sprite == markSprite) { if (!ours) img.sprite = coinSprite; }
                    else if (img.sprite.name == "coin_32") { if (ours) img.sprite = markSprite; }
                }
            }
        }

        [HarmonyPatch(typeof(StoreGui), "GetSellableItem")]
        static class NoSelling
        {
            static bool Prefix(Trader ___m_trader, ref ItemDrop.ItemData __result)
            {
                if (!___m_trader || !___m_trader.GetComponent<Dvergr>()) return true;
                __result = null;
                return false;
            }
        }

        [HarmonyPatch(typeof(Trader), nameof(Trader.GetHoverText))]
        static class HoverText
        {
            static bool Prefix(Trader __instance, ref string __result)
            {
                var dvergr = __instance.GetComponent<Dvergr>();
                if (!dvergr) return true;
                __result = Localization.instance.Localize(
                    $"{__instance.m_name}  ({Shop.TierTag(Journal.Rep(dvergr.Biome))})\n[<color=yellow><b>$KEY_Use</b></color>] Talk\n[<color=yellow><b>Hold $KEY_Use</b></color>] Wares");
                return false;
            }
        }
    }
}
