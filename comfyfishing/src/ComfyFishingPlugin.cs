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
            ReelSpeed = Config.Bind("Reel", "ReelSpeed", 3f, "Metres of line per second once hooked, at fishing skill 0.");
            ReelSpeedMaxSkill = Config.Bind("Reel", "ReelSpeedMaxSkill", 6f, "Metres of line per second once hooked, at fishing skill 100.");
            GateBySkill = Config.Bind("Tiers", "Enabled", true, "Baits need a fishing skill level and rod tier before fish will take them.");
            BaitTiers = Config.Bind("Tiers", "BaitTiers",
                "FishingBait:0:1, FishingBaitForest:10:1, FishingBaitCave:20:2, FishingBaitSwamp:25:2, FishingBaitOcean:30:2, FishingBaitPlains:40:3, FishingBaitMistlands:55:3, FishingBaitDeepNorth:70:4, FishingBaitAshlands:70:4",
                "bait prefab : fishing skill needed : rod tier needed. Rod tiers: 1 Fishing rod, 2 Bone rod, 3 Silver rod, 4 Black metal rod.");

            PrefabManager.OnVanillaPrefabsAvailable += AddRods;
            new Harmony(PluginGUID).PatchAll();
        }

        private void AddRods()
        {
            AddRod("RodWood", 1, "Wooden fishing rod", "A bent branch and a length of line. Reels itself in once something bites, but only meadows and forest fish will take the bait. Wears quickly.",
                new Color(0.75f, 0.6f, 0.4f), CraftingStations.Workbench, 1, 60f, ("Wood", 10, 5), ("Resin", 4, 2), ("LeatherScraps", 2, 1));
            AddRod("RodBone", 2, "Bone fishing rod", "Fine wood spliced with bone. Reels itself in, and swamp, cave and ocean fish will take the bait.",
                new Color(0.95f, 0.9f, 0.78f), CraftingStations.Workbench, 2, 100f, ("FineWood", 10, 5), ("BoneFragments", 4, 2), ("Resin", 5, 2));
            AddRod("RodSilver", 3, "Silver fishing rod", "Light and true. Plains and mistlands fish will take the bait. Reels faster than bone.",
                new Color(0.8f, 0.88f, 1f), CraftingStations.Forge, 2, 150f, ("FineWood", 10, 5), ("Silver", 4, 2), ("Guck", 2, 1));
            AddRod("RodBlackMetal", 4, "Black metal fishing rod", "Nothing in the sea is too strong for it. Every bait, fastest reel.",
                new Color(0.45f, 0.45f, 0.5f), CraftingStations.Forge, 3, 200f, ("FineWood", 10, 5), ("BlackMetal", 5, 3), ("LinenThread", 5, 2));

            // Base bait without Haldor: neck tails at the cauldron. Biome baits stay vanilla (bait + trophy).
            ItemManager.Instance.AddRecipe(new CustomRecipe(new RecipeConfig
            {
                Item = "FishingBait",
                Amount = 10,
                CraftingStation = CraftingStations.Cauldron,
                Requirements = new[] { new RequirementConfig("NeckTail", 2, 0, true), new RequirementConfig("Mushroom", 1, 0, true) },
            }));

            PrefabManager.OnVanillaPrefabsAvailable -= AddRods;
        }

        /// Crafted rods wear out (one point per cast) and upgrade to quality 4 at their station.
        private void AddRod(string name, int tier, string display, string desc, Color tint, string station, int level, float durability, params (string item, int amount, int perLevel)[] cost)
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
            Tint.Apply(rod.ItemPrefab, tint);
            if (shared.m_icons != null && shared.m_icons.Length > 0 && shared.m_icons[0])
                shared.m_icons = new[] { Tint.Sprite(shared.m_icons[0], tint) };
            ItemManager.Instance.AddItem(rod);
            Rods.Tiers[shared.m_name] = tier;
        }
    }

    /// Recolour a cloned prefab's materials and icon so the tiers read at a glance.
    public static class Tint
    {
        public static void Apply(GameObject prefab, Color c)
        {
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!mats[i]) continue;
                    var m = new Material(mats[i]);
                    if (m.HasProperty("_Color")) m.color = c;
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
        }

        /// Copy a sprite through a RenderTexture (its texture isn't readable) and multiply by the tint.
        /// Blits may or may not be flipped depending on the graphics API, so read both ways and keep the
        /// one with content; if neither has any, keep the original icon rather than show nothing.
        public static Sprite Sprite(Sprite src, Color c)
        {
            try
            {
                var rect = src.textureRect;
                var rt = RenderTexture.GetTemporary(src.texture.width, src.texture.height, 0, RenderTextureFormat.ARGB32);
                var prev = RenderTexture.active;
                Graphics.Blit(src.texture, rt);
                RenderTexture.active = rt;
                int w = (int)rect.width, h = (int)rect.height;
                var a = new Texture2D(w, h, TextureFormat.RGBA32, false);
                a.ReadPixels(new Rect(rect.x, rect.y, w, h), 0, 0);
                var b = new Texture2D(w, h, TextureFormat.RGBA32, false);
                b.ReadPixels(new Rect(rect.x, src.texture.height - rect.y - h, w, h), 0, 0);
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);

                float Alpha(Texture2D t) { float sum = 0f; foreach (var p in t.GetPixels()) sum += p.a; return sum; }
                float aa = Alpha(a), ab = Alpha(b);
                if (aa <= 0f && ab <= 0f) return src;
                var tex = aa >= ab ? a : b;
                var px = tex.GetPixels();
                for (int i = 0; i < px.Length; i++) px[i] = new Color(px[i].r * c.r, px[i].g * c.g, px[i].b * c.b, px[i].a);
                tex.SetPixels(px);
                tex.Apply();
                return UnityEngine.Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), src.pixelsPerUnit);
            }
            catch
            {
                return src;
            }
        }
    }

    public static class Rods
    {
        /// Keyed by the item's shared name: clones keep the vanilla rod's drop prefab, so that can't be used.
        public static readonly Dictionary<string, int> Tiers = new Dictionary<string, int>();

        public static int TierOf(Character owner)
        {
            var weapon = (owner as Humanoid)?.GetCurrentWeapon();
            var key = weapon?.m_shared?.m_name;
            return key != null && Tiers.TryGetValue(key, out var t) ? t : 1;
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
