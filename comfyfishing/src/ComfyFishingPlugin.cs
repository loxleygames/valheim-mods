using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace ComfyFishing
{
    /// Fishing without the tug-of-war. A bite, a click, and the fish reels itself in.
    /// Skill and rod tier decide which baits (and so which fish) will take.
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    public class ComfyFishingPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "games.loxley.comfyfishing";
        public const string PluginName = "ComfyFishing";
        public const string PluginVersion = "0.1.0";

        public static ConfigEntry<bool> VanillaRodVanillaReel;
        public static ConfigEntry<bool> AutoHook;
        public static ConfigEntry<float> HookWindow;
        public static ConfigEntry<float> ReelSpeed;
        public static ConfigEntry<float> ReelSpeedMaxSkill;
        public static ConfigEntry<bool> GateBySkill;
        public static ConfigEntry<string> BaitTiers;

        private void Awake()
        {
            VanillaRodVanillaReel = Config.Bind("Reel", "VanillaRodVanillaReel", true, "Haldor's fishing rod keeps the vanilla tug-of-war; only crafted rods get the comfy reel.");
            AutoHook = Config.Bind("Reel", "AutoHook", false, "Hook the fish the moment it bites, no click needed.");
            HookWindow = Config.Bind("Reel", "HookWindow", 1.5f, "Seconds after a bite in which a click hooks the fish (vanilla: 0.5).");
            ReelSpeed = Config.Bind("Reel", "ReelSpeed", 1.5f, "Metres of line per second once hooked, at fishing skill 0.");
            ReelSpeedMaxSkill = Config.Bind("Reel", "ReelSpeedMaxSkill", 3f, "Metres of line per second once hooked, at fishing skill 100.");
            GateBySkill = Config.Bind("Tiers", "Enabled", true, "Baits need a fishing skill level and rod tier before fish will take them.");
            BaitTiers = Config.Bind("Tiers", "BaitTiers",
                "FishingBait:0:1, FishingBaitForest:10:1, FishingBaitCave:20:2, FishingBaitSwamp:25:2, FishingBaitOcean:30:2, FishingBaitPlains:40:3, FishingBaitMistlands:55:3, FishingBaitDeepNorth:70:4, FishingBaitAshlands:70:4",
                "bait prefab : fishing skill needed : rod tier needed. Rod tiers: 1 Fishing rod, 2 Bone rod, 3 Silver rod, 4 Black metal rod.");

            PrefabManager.OnVanillaPrefabsAvailable += AddRods;
            new Harmony(PluginGUID).PatchAll();
        }

        private void AddRods()
        {
            AddRod("RodBone", 2, "Bone fishing rod", "Sturdier than Haldor's. Takes swamp, cave and ocean bait. Wears with use.",
                CraftingStations.Workbench, 2, 100f, ("FineWood", 10, 5), ("BoneFragments", 4, 2), ("Resin", 5, 2));
            AddRod("RodSilver", 3, "Silver fishing rod", "Light and true. Takes plains and mistlands bait. Wears with use.",
                CraftingStations.Forge, 2, 150f, ("FineWood", 10, 5), ("Silver", 4, 2), ("Guck", 2, 1));
            AddRod("RodBlackMetal", 4, "Black metal fishing rod", "Nothing in the sea is too strong for it. Wears with use.",
                CraftingStations.Forge, 3, 200f, ("FineWood", 10, 5), ("BlackMetal", 5, 3), ("LinenThread", 5, 2));
            // Base bait without Haldor: neck tails at the cauldron. Biome baits stay vanilla (bait + trophy).
            ItemManager.Instance.AddRecipe(new CustomRecipe(new RecipeConfig
            {
                Item = "FishingBait",
                Amount = 10,
                CraftingStation = CraftingStations.Cauldron,
                Requirements = new[] { new RequirementConfig("NeckTail", 2, 0, true) },
            }));

            PrefabManager.OnVanillaPrefabsAvailable -= AddRods;
        }

        /// Crafted rods wear out (one point per cast) and upgrade to quality 4 at their station.
        private void AddRod(string name, int tier, string display, string desc, string station, int level, float durability, params (string item, int amount, int perLevel)[] cost)
        {
            var reqs = new List<RequirementConfig>();
            foreach (var (item, amount, perLevel) in cost) reqs.Add(new RequirementConfig(item, amount, perLevel, true));
            var rod = new CustomItem(name, "FishingRod", new ItemConfig
            {
                Name = display,
                Description = desc,
                CraftingStation = station,
                MinStationLevel = level,
                Requirements = reqs.ToArray(),
            });
            var shared = rod.ItemDrop.m_itemData.m_shared;
            shared.m_useDurability = true;
            shared.m_maxDurability = durability;
            shared.m_durabilityPerLevel = durability * 0.5f;
            shared.m_useDurabilityDrain = 1f;
            shared.m_maxQuality = 4;
            ItemManager.Instance.AddItem(rod);
            Rods.Tiers[name] = tier;
        }
    }

    public static class Rods
    {
        public static readonly Dictionary<string, int> Tiers = new Dictionary<string, int> { { "FishingRod", 1 } };

        public static int TierOf(Character owner)
        {
            var weapon = (owner as Humanoid)?.GetCurrentWeapon();
            var prefab = weapon?.m_dropPrefab ? weapon.m_dropPrefab.name : null;
            return prefab != null && Tiers.TryGetValue(prefab, out var t) ? t : 1;
        }
    }

    public static class Baits
    {
        static Dictionary<string, (int skill, int rod)> s_table;

        public static (int skill, int rod) Requirement(string bait)
        {
            if (s_table == null)
            {
                s_table = new Dictionary<string, (int, int)>();
                foreach (var entry in ComfyFishingPlugin.BaitTiers.Value.Split(','))
                {
                    var parts = entry.Trim().Split(':');
                    if (parts.Length == 3 && int.TryParse(parts[1], out var sk) && int.TryParse(parts[2], out var rd))
                        s_table[parts[0]] = (sk, rd);
                }
            }
            return s_table.TryGetValue(bait, out var r) ? r : (0, 1);
        }
    }
}
