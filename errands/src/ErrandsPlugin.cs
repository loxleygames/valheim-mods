using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Errands
{
    /// Dvergr sheltering in the world's ruins hand out errands. One dvergr per biome, tinted to match.
    ///
    /// Part 1 (this file set): the NPC and its placement. The dvergr is a clone of Haldor (already a
    /// dvergr, already stands still, looks at you, talks, and mobs leave him alone) with the shop
    /// stripped out. He's dropped into vanilla ruin locations as the world generates, so he appears in
    /// unexplored zones of existing worlds too.
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    public class ErrandsPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "games.loxley.errands";
        public const string PluginName = "Errands";
        public const string PluginVersion = "0.1.0";

        public const string DvergrPrefab = "ErrandsDvergr";

        public static ConfigEntry<float> RuinChance;
        public static ConfigEntry<float> KeyHue;
        public static ConfigEntry<float> KeyTolerance;
        public static ConfigEntry<float> KeyMinSaturation;
        public static ConfigEntry<int> MaxErrands;
        public static ConfigEntry<float> RealHours;
        public static ConfigEntry<int> RepPerTier;
        public static ConfigEntry<float> PartyRange;
        public static ConfigEntry<string> JournalKey;
        public static ConfigEntry<bool> ShowTracker;
        public static ConfigEntry<float> TrackerX;
        public static ConfigEntry<float> TrackerY;
        public static KeyCode JournalKeyCode = KeyCode.J;
        public static readonly Dictionary<Heightmap.Biome, ConfigEntry<string>> Ruins = new Dictionary<Heightmap.Biome, ConfigEntry<string>>();
        public static readonly Dictionary<Heightmap.Biome, ConfigEntry<string>> Tints = new Dictionary<Heightmap.Biome, ConfigEntry<string>>();

        private void Awake()
        {
            RuinChance = Config.Bind("Placement", "RuinChance", 0.25f,
                "Chance that a listed ruin has a dvergr in it. Decided once per ruin from the world seed, so everyone sees the same ones.");

            MaxErrands = Config.Bind("Errands", "MaxErrands", 5, "How many errands you can hold at once.");
            RealHours = Config.Bind("Errands", "RealHours", 0f,
                "0: each dvergr offers one errand per in-game day. Above 0: one per this many real hours instead.");
            RepPerTier = Config.Bind("Errands", "RepPerTier", 10, "Standing gained per errand star.");
            PartyRange = Config.Bind("Errands", "PartyRange", 30f,
                "Players within this many metres share kill credit, and receive a copy of any errand one of them accepts.");
            JournalKey = Config.Bind("Errands", "JournalKey", "J", "Key that opens your errand book anywhere (a UnityEngine.KeyCode name).");
            if (System.Enum.TryParse(JournalKey.Value, true, out KeyCode k)) JournalKeyCode = k;
            ShowTracker = Config.Bind("Tracker", "Show", true, "Show the errand tracker on the HUD while you hold at least one.");
            TrackerX = Config.Bind("Tracker", "X", 24f, "Tracker inset from the right edge of the screen.");
            TrackerY = Config.Bind("Tracker", "Y", 300f, "Tracker inset from the top of the screen (below the minimap).");

            KeyHue = Config.Bind("Tint", "KeyHue", 0.505f,
                "Hue (0-1) of the cloth on Haldor's texture. Only pixels near this hue are recoloured; skin and gold are left alone.");
            KeyTolerance = Config.Bind("Tint", "KeyTolerance", 0.03f, "How far from KeyHue a pixel may be and still count as cloth.");
            KeyMinSaturation = Config.Bind("Tint", "KeyMinSaturation", 0.45f, "Greys below this saturation are never recoloured.");

            foreach (var b in Biomes.All)
            {
                Ruins[b] = Config.Bind("Placement", b + "Ruins", Biomes.DefaultRuins(b),
                    "Comma-separated vanilla location names a dvergr may shelter in. `errands_locations` in the console lists what your game has.");
                Tints[b] = Config.Bind("Dvergr", b + "Tint", Biomes.DefaultTint(b),
                    "hue,saturation,value shift applied to the cloth pixels of the dvergr's texture. Hue is 0-1 around the wheel.");
            }

            PrefabManager.OnVanillaPrefabsAvailable += AddPrefab;
            PrefabManager.OnPrefabsRegistered += Tables.Resolve;
            gameObject.AddComponent<JournalKey>();
            CommandManager.Instance.AddConsoleCommand(new SpawnCommand());
            CommandManager.Instance.AddConsoleCommand(new LocationsCommand());
            CommandManager.Instance.AddConsoleCommand(new TexDumpCommand());
            CommandManager.Instance.AddConsoleCommand(new RepCommand());
            CommandManager.Instance.AddConsoleCommand(new DoneCommand());
            CommandManager.Instance.AddConsoleCommand(new KitCommand());
            new Harmony(PluginGUID).PatchAll();
        }

        private void AddPrefab()
        {
            var prefab = PrefabManager.Instance.CreateClonedPrefab(DvergrPrefab, "Haldor");
            if (!prefab)
            {
                Logger.LogError("Haldor prefab not found; no dvergr for you.");
                return;
            }

            // Keep the Trader: it drives the stand/look/greet/talk animation and later the rep shop.
            // Empty its shelves and its Haldor lines; Dvergr fills them per instance.
            var trader = prefab.GetComponent<Trader>();
            trader.m_items.Clear();
            trader.m_useItems.Clear();
            trader.m_randomTalkConditionals.Clear();
            trader.m_name = "Dvergr";
            prefab.AddComponent<Dvergr>();

            PrefabManager.Instance.AddPrefab(new CustomPrefab(prefab, fixReference: false));
            Rewards.AddItems();
            Uniques.AddItems();
            PrefabManager.OnVanillaPrefabsAvailable -= AddPrefab;
        }
    }

    public static class Biomes
    {
        public static readonly Heightmap.Biome[] All =
        {
            Heightmap.Biome.Meadows, Heightmap.Biome.BlackForest, Heightmap.Biome.Swamp, Heightmap.Biome.Mountain,
            Heightmap.Biome.Plains, Heightmap.Biome.Mistlands, Heightmap.Biome.AshLands, Heightmap.Biome.DeepNorth,
        };

        /// The Völuspá's dvergatal: every dwarf it names. A dvergr's persistent id picks his.
        static readonly string[] s_names =
        {
            "Alvíss", "Dvalinn", "Nár", "Frosti", "Bömburr", "Regin", "Brokkr", "Nori", "Andvari", "Austri", "Vestri",
            "Norðri", "Suðri", "Bifurr", "Bafurr", "Nóri", "Án", "Ánarr", "Óinn", "Mjöðvitnir", "Vígr", "Gandálfr",
            "Vindálfr", "Þorinn", "Þrór", "Vitr", "Litr", "Nýr", "Nýráðr", "Rekkr", "Ráðsviðr", "Fíli", "Kíli",
            "Fundinn", "Náli", "Hepti", "Víli", "Hannar", "Svíurr", "Billingr", "Brúni", "Bíldr", "Buri", "Frár",
            "Hornbori", "Frægr", "Lóni", "Aurvangr", "Jari", "Eikinskjaldi", "Draupnir", "Dólgþrasir", "Hár",
            "Haugspori", "Hlévangr", "Glói", "Dóri", "Óri", "Dúfr", "Skirfir", "Virfir", "Skáfiðr", "Ái", "Álfr",
            "Yngvi", "Fjalarr", "Finnr", "Ginnarr", "Sindri", "Eitri",
        };

        public static string DvergrName(int id) => s_names[(int)((uint)id % (uint)s_names.Length)];

        /// Hue shift of the cloth away from Haldor's teal (~0.505): blue for the forest, olive for the swamp,
        /// pale for the peaks, ochre for the plains, violet for the mist, red for the ash.
        public static string DefaultTint(Heightmap.Biome b)
        {
            switch (b)
            {
                case Heightmap.Biome.Meadows: return "0,0,0";
                case Heightmap.Biome.BlackForest: return "0.2,0,-0.1";
                case Heightmap.Biome.Swamp: return "-0.15,-0.2,-0.15";
                case Heightmap.Biome.Mountain: return "0.1,-0.3,0.25";
                case Heightmap.Biome.Plains: return "-0.35,0.1,0.1";
                case Heightmap.Biome.Mistlands: return "0.3,0,-0.05";
                case Heightmap.Biome.AshLands: return "0.5,0.2,0";
                case Heightmap.Biome.DeepNorth: return "0.15,-0.5,0.3";
                default: return "0,0,0";
            }
        }

        /// Vanilla ruin locations with a floor to stand on and no dungeon entrance. Best guess from the
        /// wiki; `errands_locations` prints the real list so these can be corrected in config.
        public static string DefaultRuins(Heightmap.Biome b)
        {
            switch (b)
            {
                case Heightmap.Biome.Meadows:
                    return "WoodHouse1,WoodHouse2,WoodHouse3,WoodHouse4,WoodHouse5,WoodHouse6,WoodHouse7,WoodHouse8,WoodHouse9,WoodHouse10,WoodHouse11,WoodHouse12,WoodHouse13,WoodFarm1,WoodVillage1";
                case Heightmap.Biome.BlackForest:
                    return "Ruin1,Ruin2,StoneTowerRuins03,StoneTowerRuins07,StoneTowerRuins08,StoneTowerRuins09,StoneTowerRuins10,StoneHouse3,StoneHouse4";
                case Heightmap.Biome.Swamp:
                    return "SwampHut1,SwampHut2,SwampHut3,SwampHut4,SwampHut5,SwampRuin1,SwampRuin2";
                case Heightmap.Biome.Mountain:
                    return "StoneTowerRuins04,StoneTowerRuins05,AbandonedLogCabin02,AbandonedLogCabin03,AbandonedLogCabin04";
                case Heightmap.Biome.Plains:
                    return "StoneTower1,StoneTower3,Ruin3,StoneHenge1,StoneHenge2,StoneHenge3,StoneHenge4,StoneHenge5,StoneHenge6";
                case Heightmap.Biome.Mistlands:
                    return "Mistlands_GuardTower1_ruined_new,Mistlands_GuardTower3_ruined_new,Mistlands_Excavation1,Mistlands_Excavation2,Mistlands_Excavation3,Mistlands_Statue1,Mistlands_Statue2";
                case Heightmap.Biome.AshLands:
                    return "CharredRuins1,CharredRuins2,CharredRuins3,CharredRuins4,CharredTowerRuins1,CharredTowerRuins2,CharredTowerRuins3";
                default:
                    return "";
            }
        }

        public static HashSet<string> RuinSet(Heightmap.Biome b)
        {
            var set = new HashSet<string>();
            if (!ErrandsPlugin.Ruins.TryGetValue(b, out var cfg)) return set;
            foreach (var s in cfg.Value.Split(','))
            {
                var t = s.Trim();
                if (t.Length > 0) set.Add(t);
            }
            return set;
        }

        public static string Label(Heightmap.Biome b)
        {
            switch (b)
            {
                case Heightmap.Biome.BlackForest: return "Black Forest";
                case Heightmap.Biome.AshLands: return "Ashlands";
                case Heightmap.Biome.DeepNorth: return "Deep North";
                default: return b.ToString();
            }
        }
    }
}
