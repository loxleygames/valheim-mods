using System;
using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Errands
{
    /// Fixed uniques: a vanilla item cloned, renamed, tinted, given one trick. Five per biome: three only
    /// chests drop, two only the dvergr sells. Passives are SE_Stats effects worn while equipped.
    /// Upgradable at the forge for marks; never craftable from scratch (the recipe needs the weapon itself
    /// and level 1 is refused outright).
    public static class Uniques
    {
        class Def
        {
            public Heightmap.Biome Biome;
            public bool Vendor;
            public string Prefab;
            public string[] Bases;
            public string Name, Desc, Passive, Trick;
            public Color Tint;
            public Action<ItemDrop.ItemData.SharedData> Tweak;
            public Action<SE_Stats> Stats;
            public string Material;
        }

        static readonly List<Def> s_defs = new List<Def>
        {
            new Def { Biome = Heightmap.Biome.Meadows, Prefab = "ErrandsHush", Bases = new[] { "Bow" },
                Name = "Alvíss's Hush", Desc = "Strung with something that doesn't creak.", Passive = "Bows +10, stealth +20%",
                Tint = new Color(0.45f, 0.6f, 0.4f), Material = "LeatherScraps",
                Stats = s => { s.m_skillLevel = Skills.SkillType.Bows; s.m_skillLevelModifier = 10f; s.m_stealthModifier = 0.2f; } },
            new Def { Biome = Heightmap.Biome.Meadows, Prefab = "ErrandsNeckbiter", Trick = "Backstab x1.5", Bases = new[] { "KnifeFlint" },
                Name = "Neckbiter", Desc = "Named for what it does, not what it's made of.", Passive = "Knives +10",
                Tint = new Color(0.6f, 0.65f, 0.5f), Material = "LeatherScraps",
                Tweak = sh => sh.m_backstabBonus *= 1.5f,
                Stats = s => { s.m_skillLevel = Skills.SkillType.Knives; s.m_skillLevelModifier = 10f; } },
            new Def { Biome = Heightmap.Biome.Meadows, Prefab = "ErrandsAlvissCloak", Bases = new[] { "CapeDeerHide" },
                Name = "Alvíss's Cloak", Desc = "Smells of woodsmoke and old rain.", Passive = "Stealth +15%, carry weight +25",
                Tint = new Color(0.45f, 0.55f, 0.4f), Material = "LeatherScraps",
                Stats = s => { s.m_stealthModifier = 0.15f; s.m_addMaxCarryWeight = 25f; } },
            new Def { Biome = Heightmap.Biome.Meadows, Vendor = true, Prefab = "ErrandsHornbeam", Trick = "Heavy knockback", Bases = new[] { "Club" },
                Name = "Hornbeam Cudgel", Desc = "Heavier than it looks. Alvíss cut it before Thor kept him talking till sunrise.", Passive = "Health regen +50%",
                Tint = new Color(0.55f, 0.45f, 0.3f), Material = "LeatherScraps",
                Tweak = sh => { sh.m_attackForce *= 1.5f; sh.m_damages.m_blunt += 4f; },
                Stats = s => s.m_healthRegenMultiplier = 1.5f },
            new Def { Biome = Heightmap.Biome.Meadows, Vendor = true, Prefab = "ErrandsWayfarer", Bases = new[] { "ArmorLeatherLegs" },
                Name = "Wayfarer's Wraps", Desc = "Alvíss walked far in these. Nobody knows from where.", Passive = "Move speed +5%",
                Tint = new Color(0.6f, 0.5f, 0.35f), Material = "LeatherScraps",
                Stats = s => s.m_speedModifier = 0.05f },

            new Def { Biome = Heightmap.Biome.BlackForest, Prefab = "ErrandsGrudge", Trick = "Spirit on every hit. The forest's undead feel it", Bases = new[] { "SwordBronze" },
                Name = "Dvalinn's Grudge", Desc = "The dead of the forest know this edge.", Passive = null,
                Tint = new Color(0.55f, 0.6f, 1f), Material = "Bronze",
                Tweak = sh => { sh.m_damages.m_spirit += 10f; sh.m_damagesPerLevel.m_spirit += 2f; } },
            new Def { Biome = Heightmap.Biome.BlackForest, Prefab = "ErrandsTrollfear", Trick = "Heavy knockback", Bases = new[] { "MaceBronze" },
                Name = "Trollfear", Desc = "Trolls don't fear it. They should.", Passive = "Clubs +10",
                Tint = new Color(0.5f, 0.55f, 0.85f), Material = "Bronze",
                Tweak = sh => { sh.m_attackForce *= 1.6f; sh.m_damages.m_blunt += 5f; },
                Stats = s => { s.m_skillLevel = Skills.SkillType.Clubs; s.m_skillLevelModifier = 10f; } },
            new Def { Biome = Heightmap.Biome.BlackForest, Prefab = "ErrandsEmber", Trick = "Never burns out", Bases = new[] { "Torch" },
                Name = "Dvalinn's Ember", Desc = "Lit before the forest was black. Still going.", Passive = "Never burns out",
                Tint = new Color(0.7f, 0.6f, 1f), Material = "Bronze",
                Tweak = sh => { sh.m_useDurability = false; sh.m_damages.m_fire += 5f; } },
            new Def { Biome = Heightmap.Biome.BlackForest, Vendor = true, Prefab = "ErrandsRootcleaver", Trick = "Chops harder", Bases = new[] { "AxeBronze" },
                Name = "Rootcleaver", Desc = "Dvalinn's make. He held the trees responsible for something.", Passive = "Woodcutting +15",
                Tint = new Color(0.7f, 0.6f, 0.45f), Material = "Bronze",
                Tweak = sh => { sh.m_damages.m_chop += 15f; sh.m_damagesPerLevel.m_chop += 3f; },
                Stats = s => { s.m_skillLevel = Skills.SkillType.WoodCutting; s.m_skillLevelModifier = 15f; } },
            new Def { Biome = Heightmap.Biome.BlackForest, Vendor = true, Prefab = "ErrandsCopperhide", Trick = "Parries send things flying", Bases = new[] { "ShieldBronzeBuckler" },
                Name = "Copperhide", Desc = "Small. Loud. Sends things flying.", Passive = "Blocking +10",
                Tint = new Color(0.55f, 0.6f, 0.95f), Material = "Bronze",
                Tweak = sh => sh.m_deflectionForce += 25f,
                Stats = s => { s.m_skillLevel = Skills.SkillType.Blocking; s.m_skillLevelModifier = 10f; } },

            new Def { Biome = Heightmap.Biome.Swamp, Prefab = "ErrandsDrownedBell", Trick = "Spirit on every hit", Bases = new[] { "MaceIron" },
                Name = "Drowned Bell", Desc = "Rings once for every draugr. It's been ringing a while.", Passive = null,
                Tint = new Color(0.45f, 0.55f, 0.45f), Material = "Iron",
                Tweak = sh => { sh.m_damages.m_spirit += 12f; sh.m_damagesPerLevel.m_spirit += 2f; } },
            new Def { Biome = Heightmap.Biome.Swamp, Prefab = "ErrandsMirecloak", Bases = new[] { "CapeTrollHide" },
                Name = "Mirecloak", Desc = "Nár wore it into the bog. He came out. It didn't dry.", Passive = "Poison resistance, stealth +10%",
                Tint = new Color(0.4f, 0.5f, 0.35f), Material = "Iron",
                Stats = s => { Resist(s, HitData.DamageType.Poison); s.m_stealthModifier = 0.1f; } },
            new Def { Biome = Heightmap.Biome.Swamp, Prefab = "ErrandsRegret", Trick = "Poison on every hit", Bases = new[] { "SwordIron" },
                Name = "Nár's Regret", Desc = "Quenched in the bog. Never quite dried.", Passive = "Poison resistance",
                Tint = new Color(0.35f, 0.5f, 0.35f), Material = "Iron",
                Tweak = sh => { sh.m_damages.m_poison += 15f; sh.m_damagesPerLevel.m_poison += 3f; },
                Stats = s => Resist(s, HitData.DamageType.Poison) },
            new Def { Biome = Heightmap.Biome.Swamp, Vendor = true, Prefab = "ErrandsBogwarden", Trick = "Block +25%", Bases = new[] { "ShieldIronTower" },
                Name = "Bogwarden", Desc = "Held a door once. The door is gone. It isn't.", Passive = "Blocking +20",
                Tint = new Color(0.4f, 0.5f, 0.4f), Material = "Iron",
                Tweak = sh => { sh.m_blockPower *= 1.25f; sh.m_blockPowerPerLevel *= 1.25f; },
                Stats = s => { s.m_skillLevel = Skills.SkillType.Blocking; s.m_skillLevelModifier = 20f; } },
            new Def { Biome = Heightmap.Biome.Swamp, Vendor = true, Prefab = "ErrandsNarsPick", Trick = "Digs harder", Bases = new[] { "PickaxeIron" },
                Name = "Nár's Pick", Desc = "Nár's pick. It knows where the silver is. It won't tell you.", Passive = "Pickaxes +15",
                Tint = new Color(0.5f, 0.6f, 0.5f), Material = "Iron",
                Tweak = sh => { sh.m_damages.m_pickaxe += 10f; sh.m_damagesPerLevel.m_pickaxe += 3f; },
                Stats = s => { s.m_skillLevel = Skills.SkillType.Pickaxes; s.m_skillLevelModifier = 15f; } },

            new Def { Biome = Heightmap.Biome.Mountain, Prefab = "ErrandsFang", Trick = "Frost on every hit", Bases = new[] { "KnifeSilver" },
                Name = "Frosti's Fang", Desc = "Short, quiet, and colder than the wind.", Passive = "Knives +15",
                Tint = new Color(0.85f, 0.95f, 1f), Material = "Silver",
                Tweak = sh => { sh.m_damages.m_frost += 10f; sh.m_damagesPerLevel.m_frost += 2f; },
                Stats = s => { s.m_skillLevel = Skills.SkillType.Knives; s.m_skillLevelModifier = 15f; } },
            new Def { Biome = Heightmap.Biome.Mountain, Prefab = "ErrandsBreath", Trick = "Frost on every hit", Bases = new[] { "SpearWolfFang" },
                Name = "Frosti's Breath", Desc = "Cold enough to keep.", Passive = "Frost resistance",
                Tint = new Color(0.8f, 0.9f, 1f), Material = "Silver",
                Tweak = sh => { sh.m_damages.m_frost += 20f; sh.m_damagesPerLevel.m_frost += 4f; },
                Stats = s => Resist(s, HitData.DamageType.Frost) },
            new Def { Biome = Heightmap.Biome.Mountain, Prefab = "ErrandsDrakehide", Bases = new[] { "CapeWolf" },
                Name = "Drakehide Mantle", Desc = "Frosti swore it was a drake. It was probably a wolf.", Passive = "Health regen +25%",
                Tint = new Color(0.8f, 0.85f, 0.95f), Material = "Silver",
                Stats = s => s.m_healthRegenMultiplier = 1.25f },
            new Def { Biome = Heightmap.Biome.Mountain, Vendor = true, Prefab = "ErrandsRidgerunner", Bases = new[] { "BowHuntsman" },
                Name = "Ridgerunner", Desc = "Frosti walked these peaks before there were wolves.", Passive = "Bows +15, running costs 20% less stamina",
                Tint = new Color(0.75f, 0.8f, 0.85f), Material = "Silver",
                Stats = s => { s.m_skillLevel = Skills.SkillType.Bows; s.m_skillLevelModifier = 15f; s.m_runStaminaDrainModifier = -0.2f; } },
            new Def { Biome = Heightmap.Biome.Mountain, Vendor = true, Prefab = "ErrandsSummit", Trick = "Frost on every hit, heavy knockback", Bases = new[] { "SledgeIron" },
                Name = "Summitbreaker", Desc = "Brought a peak down once. Frosti was standing on it.", Passive = null,
                Tint = new Color(0.75f, 0.85f, 1f), Material = "Silver",
                Tweak = sh => { sh.m_damages.m_frost += 15f; sh.m_damagesPerLevel.m_frost += 3f; sh.m_attackForce *= 1.3f; } },

            new Def { Biome = Heightmap.Biome.Plains, Prefab = "ErrandsHunger", Trick = "Attacks cost 30% less stamina", Bases = new[] { "AtgeirBlackmetal" },
                Name = "Bömburr's Hunger", Desc = "It doesn't tire. Neither should you.", Passive = "Polearms +15",
                Tint = new Color(0.9f, 0.75f, 0.4f), Material = "BlackMetal",
                Tweak = sh => { sh.m_attack.m_attackStamina *= 0.7f; sh.m_secondaryAttack.m_attackStamina *= 0.7f; },
                Stats = s => { s.m_skillLevel = Skills.SkillType.Polearms; s.m_skillLevelModifier = 15f; } },
            new Def { Biome = Heightmap.Biome.Plains, Prefab = "ErrandsGoblinsRuin", Trick = "Fire on every hit", Bases = new[] { "SwordBlackmetal" },
                Name = "Goblin's Ruin", Desc = "Fulings named it. Bömburr kept the name.", Passive = "Swords +10",
                Tint = new Color(0.95f, 0.8f, 0.45f), Material = "BlackMetal",
                Tweak = sh => { sh.m_damages.m_fire += 10f; sh.m_damagesPerLevel.m_fire += 2f; },
                Stats = s => { s.m_skillLevel = Skills.SkillType.Swords; s.m_skillLevelModifier = 10f; } },
            new Def { Biome = Heightmap.Biome.Plains, Prefab = "ErrandsBarleycloak", Bases = new[] { "CapeLox" },
                Name = "Barleycloak", Desc = "Woven from a harvest nobody got to eat.", Passive = "Carry weight +50",
                Tint = new Color(0.95f, 0.85f, 0.5f), Material = "BlackMetal",
                Stats = s => s.m_addMaxCarryWeight = 50f },
            new Def { Biome = Heightmap.Biome.Plains, Vendor = true, Prefab = "ErrandsLoxhide", Trick = "Block +20%, harder parry", Bases = new[] { "ShieldBlackmetalTower" },
                Name = "Loxhide Wall", Desc = "A lox stood behind this once. Briefly.", Passive = null,
                Tint = new Color(0.8f, 0.65f, 0.4f), Material = "BlackMetal",
                Tweak = sh => { sh.m_blockPower *= 1.2f; sh.m_blockPowerPerLevel *= 1.2f; sh.m_deflectionForce += 30f; } },
            new Def { Biome = Heightmap.Biome.Plains, Vendor = true, Prefab = "ErrandsLash", Trick = "Backstab x1.5", Bases = new[] { "KnifeBlackMetal" },
                Name = "Deathsquito Lash", Desc = "Fast enough to catch one. Sharp enough to matter.", Passive = "Knives +15",
                Tint = new Color(0.85f, 0.7f, 0.4f), Material = "BlackMetal",
                Tweak = sh => sh.m_backstabBonus *= 1.5f,
                Stats = s => { s.m_skillLevel = Skills.SkillType.Knives; s.m_skillLevelModifier = 15f; } },

            new Def { Biome = Heightmap.Biome.Mistlands, Prefab = "ErrandsSeekersting", Trick = "Lightning on every hit", Bases = new[] { "KnifeSkollAndHati" },
                Name = "Seekersting", Desc = "Two blades. One grudge.", Passive = "Knives +10",
                Tint = new Color(0.8f, 0.6f, 1f), Material = "Eitr",
                Tweak = sh => { sh.m_damages.m_lightning += 10f; sh.m_damagesPerLevel.m_lightning += 2f; },
                Stats = s => { s.m_skillLevel = Skills.SkillType.Knives; s.m_skillLevelModifier = 10f; } },
            new Def { Biome = Heightmap.Biome.Mistlands, Prefab = "ErrandsReginsMantle", Bases = new[] { "CapeFeather" },
                Name = "Regin's Mantle", Desc = "Regin did not fall. He descended.", Passive = "Eitr regen +20%, stealth +10%",
                Tint = new Color(0.75f, 0.6f, 0.95f), Material = "Eitr",
                Stats = s => { s.m_eitrRegenMultiplier = 1.2f; s.m_stealthModifier = 0.1f; } },
            new Def { Biome = Heightmap.Biome.Mistlands, Prefab = "ErrandsAnswer", Trick = "Lightning where Mistwalker had frost", Bases = new[] { "SwordMistwalker" },
                Name = "Regin's Answer", Desc = "Regin was asked a question once. This was the reply.", Passive = "Eitr regen +30%",
                Tint = new Color(0.75f, 0.55f, 1f), Material = "Eitr",
                Tweak = sh => { sh.m_damages.m_lightning += sh.m_damages.m_frost + 20f; sh.m_damagesPerLevel.m_lightning += sh.m_damagesPerLevel.m_frost + 3f; sh.m_damages.m_frost = 0f; sh.m_damagesPerLevel.m_frost = 0f; sh.m_attackStatusEffect = null; },
                Stats = s => s.m_eitrRegenMultiplier = 1.3f },
            new Def { Biome = Heightmap.Biome.Mistlands, Vendor = true, Prefab = "ErrandsMistpiercer", Trick = "Clears the mist while held", Bases = new[] { "SpearCarapace" },
                Name = "Mistpiercer", Desc = "The mist parts for it. Grudgingly.", Passive = "Clears the mist while held",
                Tint = new Color(0.85f, 0.8f, 1f), Material = "Eitr",
                Tweak = sh => sh.m_equipStatusEffect = EquipEffectOf("Demister") },
            new Def { Biome = Heightmap.Biome.Mistlands, Vendor = true, Prefab = "ErrandsTickbane", Trick = "Chops harder", Bases = new[] { "AxeJotunBane" },
                Name = "Tickbane", Desc = "The trees here are wrong. So are the ticks. This handles both.", Passive = "Axes +15",
                Tint = new Color(0.7f, 0.55f, 0.9f), Material = "Eitr",
                Tweak = sh => { sh.m_damages.m_chop += 10f; sh.m_damagesPerLevel.m_chop += 2f; },
                Stats = s => { s.m_skillLevel = Skills.SkillType.Axes; s.m_skillLevelModifier = 15f; } },

            new Def { Biome = Heightmap.Biome.AshLands, Prefab = "ErrandsCinderwake", Trick = "Fire on every hit", Bases = new[] { "BowAshlands" },
                Name = "Cinderwake", Desc = "Still warm. Always warm.", Passive = "Fire resistance",
                Tint = new Color(1f, 0.55f, 0.35f), Material = "FlametalNew",
                Tweak = sh => { sh.m_damages.m_fire += 15f; sh.m_damagesPerLevel.m_fire += 3f; },
                Stats = s => Resist(s, HitData.DamageType.Fire) },
            new Def { Biome = Heightmap.Biome.AshLands, Prefab = "ErrandsAshwake", Trick = "Fire on every hit", Bases = new[] { "SwordNiedhogg", "SwordBlackmetal" },
                Name = "Ashwake", Desc = "Forged in the only forge that never needed lighting.", Passive = "Swords +10",
                Tint = new Color(1f, 0.6f, 0.4f), Material = "FlametalNew",
                Tweak = sh => { sh.m_damages.m_fire += 10f; sh.m_damagesPerLevel.m_fire += 2f; },
                Stats = s => { s.m_skillLevel = Skills.SkillType.Swords; s.m_skillLevelModifier = 10f; } },
            new Def { Biome = Heightmap.Biome.AshLands, Prefab = "ErrandsBrokkrsMantle", Bases = new[] { "CapeAsh", "CapeLox" },
                Name = "Brokkr's Mantle", Desc = "Singed at every hem. Brokkr called that seasoning.", Passive = "Health regen +30%",
                Tint = new Color(0.95f, 0.5f, 0.4f), Material = "FlametalNew",
                Stats = s => s.m_healthRegenMultiplier = 1.3f },
            new Def { Biome = Heightmap.Biome.AshLands, Vendor = true, Prefab = "ErrandsBrokkr", Trick = "Heavy knockback", Bases = new[] { "MaceEldner", "MaceFlametal", "MaceSilver" },
                Name = "Brokkr's Hammer", Desc = "Brokkr made a better one once, for a god who never paid.", Passive = "Clubs +15",
                Tint = new Color(1f, 0.6f, 0.4f), Material = "FlametalNew",
                Tweak = sh => sh.m_attackForce *= 1.5f,
                Stats = s => { s.m_skillLevel = Skills.SkillType.Clubs; s.m_skillLevelModifier = 15f; } },
            new Def { Biome = Heightmap.Biome.AshLands, Vendor = true, Prefab = "ErrandsMorgensWall", Trick = "Block +20%", Bases = new[] { "ShieldFlametalTower", "ShieldBlackmetalTower" },
                Name = "Morgen's Wall", Desc = "A morgen hit it. Once.", Passive = "Blocking +15",
                Tint = new Color(1f, 0.55f, 0.45f), Material = "FlametalNew",
                Tweak = sh => { sh.m_blockPower *= 1.2f; sh.m_blockPowerPerLevel *= 1.2f; },
                Stats = s => { s.m_skillLevel = Skills.SkillType.Blocking; s.m_skillLevelModifier = 15f; } },
        };

        static readonly HashSet<string> s_recipes = new HashSet<string>();
        static readonly Dictionary<string, string> s_names = new Dictionary<string, string>();

        public static List<string> All(Heightmap.Biome b) { var l = Find(b, false); l.AddRange(Find(b, true)); return l; }
        public static List<string> ChestUniques(Heightmap.Biome b) => Find(b, vendor: false);
        public static List<string> VendorUniques(Heightmap.Biome b) => Find(b, vendor: true);
        public static string DisplayName(string prefab) => s_names.TryGetValue(prefab, out var n) ? n : prefab;

        static List<string> Find(Heightmap.Biome b, bool vendor)
        {
            var list = new List<string>();
            foreach (var d in s_defs)
                if (d.Biome == b && d.Vendor == vendor && s_names.ContainsKey(d.Prefab)) list.Add(d.Prefab);
            return list;
        }

        /// A chest unique for this biome, leaning hard toward ones the player hasn't held yet.
        public static string RollChestUnique(Player player, Heightmap.Biome b)
        {
            var all = ChestUniques(b);
            if (all.Count == 0) return null;
            var fresh = all.FindAll(p =>
            {
                var go = ObjectDB.instance.GetItemPrefab(p);
                return go && !player.IsKnownMaterial(go.GetComponent<ItemDrop>().m_itemData.m_shared.m_name);
            });
            var pool = fresh.Count > 0 && UnityEngine.Random.value < 0.8f ? fresh : all;
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        static void Resist(SE_Stats s, HitData.DamageType type) =>
            s.m_mods.Add(new HitData.DamageModPair { m_type = type, m_modifier = HitData.DamageModifier.Resistant });

        static StatusEffect EquipEffectOf(string itemPrefab)
        {
            var go = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(itemPrefab) : null;
            return go ? go.GetComponent<ItemDrop>().m_itemData.m_shared.m_equipStatusEffect : null;
        }

        public static void AddItems()
        {
            foreach (var d in s_defs)
            {
                string baseName = null;
                foreach (var b in d.Bases)
                    if (PrefabManager.Instance.GetPrefab(b)) { baseName = b; break; }
                if (baseName == null)
                {
                    Jotunn.Logger.LogWarning($"Errands: no base prefab for {d.Name} ({string.Join("/", d.Bases)}); skipped");
                    continue;
                }

                var station = d.Biome == Heightmap.Biome.Meadows ? CraftingStations.Workbench : CraftingStations.Forge;
                var reqs = new List<RequirementConfig>
                {
                    new RequirementConfig(d.Prefab, 1, 0, false),
                    new RequirementConfig(Rewards.MarkPrefab, 0, 5, false),
                };
                if (ObjectDB.instance && ObjectDB.instance.GetItemPrefab(d.Material)) reqs.Add(new RequirementConfig(d.Material, 0, 3, false));

                var item = new CustomItem(d.Prefab, baseName, new ItemConfig
                {
                    Name = d.Name,
                    // Stat passives describe themselves in the tooltip; the trick gets its own line in words.
                    Description = d.Desc + (d.Trick != null ? "\n\n<color=#c8a25a>" + d.Trick + "</color>" : ""),
                    CraftingStation = station,
                    MinStationLevel = 1,
                    Requirements = reqs.ToArray(),
                });
                var shared = item.ItemDrop.m_itemData.m_shared;
                shared.m_maxQuality = 4;
                // Dvergr-made: none of the base item's movement penalty.
                shared.m_movementModifier = 0f;
                // Not part of the base item's set: no set bonus, no set line in the tooltip.
                shared.m_setName = "";
                shared.m_setSize = 0;
                shared.m_setStatusEffect = null;
                d.Tweak?.Invoke(shared);

                if (d.Stats != null)
                {
                    var se = ScriptableObject.CreateInstance<SE_Stats>();
                    se.name = "SE_" + d.Prefab;
                    se.m_name = d.Name;
                    se.m_tooltip = "";
                    se.m_icon = shared.m_icons.Length > 0 ? shared.m_icons[0] : null;
                    d.Stats(se);
                    ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(se, fixReference: false));
                    shared.m_equipStatusEffect = se;
                }

                Tint.Apply(item.ItemPrefab, d.Tint);
                if (shared.m_icons.Length > 0 && shared.m_icons[0]) shared.m_icons = new[] { Tint.Sprite(shared.m_icons[0], d.Tint) };

                ItemManager.Instance.AddItem(item);
                s_recipes.Add(d.Prefab);
                s_names[d.Prefab] = d.Name;
            }
        }

        /// Vanilla ends the effect block with a newline and starts the next line with another; close the gap.
        [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip), typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool))]
        static class TidyTooltip
        {
            static void Postfix(ItemDrop.ItemData item, ref string __result)
            {
                if (item?.m_shared?.m_equipStatusEffect == null || !item.m_shared.m_equipStatusEffect.name.StartsWith("SE_Errands")) return;
                __result = __result.Replace("</color>\n\n$item_movement_modifier", "</color>\n$item_movement_modifier");
            }
        }

        /// Level 1 of a unique is never craftable; only upgrades go through.
        [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), typeof(Recipe), typeof(bool), typeof(int), typeof(int))]
        static class NoFreshCraft
        {
            static bool Prefix(Recipe recipe, int qualityLevel, ref bool __result)
            {
                if (qualityLevel > 1 || recipe == null || !recipe.m_item || !s_recipes.Contains(recipe.m_item.gameObject.name)) return true;
                __result = false;
                return false;
            }
        }
    }
}
