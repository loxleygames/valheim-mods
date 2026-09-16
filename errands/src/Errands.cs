using System;
using System.Collections.Generic;
using System.Linq;

namespace Errands
{
    public enum Verb { Kill, Gather, Craft }

    /// One line in a biome's errand table. Targets are prefab names; they're resolved against the game
    /// at ObjectDB load and anything that doesn't exist is dropped with a log line, so a typo here costs
    /// an errand, not a crash.
    public class ErrandDef
    {
        public Verb Verb;
        public string[] Prefabs;
        public int Min, Max, Tier;

        public ErrandDef(Verb verb, string prefabs, int min, int max, int tier)
        {
            Verb = verb; Prefabs = prefabs.Split('/'); Min = min; Max = max; Tier = tier;
        }

        /// What progress is matched against: prefab names for kills, shared item names otherwise.
        public string[] Keys;
        /// Localisation token shown to the player, from the first target.
        public string Display;
    }

    /// An errand a player has accepted. Serialised into Player.m_customData one per line.
    public class Errand
    {
        public Heightmap.Biome Biome;
        public int Day;
        public Verb Verb;
        public string[] Keys;
        public string Display;
        public int Need, Have, Tier;
        /// Who accepted it from the dvergr; a shared copy keeps the sharer's ID.
        public long Owner;
        /// Which dvergr gave it (his persistent id) and what he's called.
        public int Giver;
        public string GiverName = "";

        public string Id => $"{(int)Biome}:{Day}:{Owner}:{Giver}";

        public string Serialize() =>
            string.Join("|", (int)Biome, Day, (int)Verb, string.Join("+", Keys), Display, Need, Have, Tier, Owner, Giver, GiverName);

        public static Errand Parse(string line)
        {
            var f = line.Split('|');
            if (f.Length < 8) return null;
            return new Errand
            {
                Biome = (Heightmap.Biome)int.Parse(f[0]), Day = int.Parse(f[1]), Verb = (Verb)int.Parse(f[2]),
                Keys = f[3].Split('+'), Display = f[4], Need = int.Parse(f[5]), Have = int.Parse(f[6]), Tier = int.Parse(f[7]),
                Owner = f.Length > 8 && long.TryParse(f[8], out var o) ? o : 0L,
                Giver = f.Length > 9 && int.TryParse(f[9], out var g) ? g : 0,
                GiverName = f.Length > 10 ? f[10] : "",
            };
        }

        public string VerbWord => Verb == Verb.Kill ? "Kill" : Verb == Verb.Gather ? "Bring" : "Craft";
        public string Title => $"{VerbWord} {Need} {Localization.instance.Localize(Display)}";
    }

    public static class Tables
    {
        static readonly Dictionary<Heightmap.Biome, List<ErrandDef>> s_defs = new Dictionary<Heightmap.Biome, List<ErrandDef>>
        {
            [Heightmap.Biome.Meadows] = new List<ErrandDef>
            {
                new ErrandDef(Verb.Kill, "Deer", 8, 12, 1),
                new ErrandDef(Verb.Kill, "Boar", 8, 12, 1),
                new ErrandDef(Verb.Kill, "Neck", 8, 12, 1),
                new ErrandDef(Verb.Kill, "Greyling", 10, 15, 1),
                new ErrandDef(Verb.Gather, "Wood", 40, 60, 1),
                new ErrandDef(Verb.Gather, "Stone", 30, 50, 1),
                new ErrandDef(Verb.Gather, "Flint", 15, 25, 1),
                new ErrandDef(Verb.Gather, "Raspberry", 20, 30, 1),
                new ErrandDef(Verb.Gather, "Mushroom", 15, 20, 1),
                new ErrandDef(Verb.Gather, "DeerHide", 6, 10, 2),
                new ErrandDef(Verb.Gather, "Feathers", 10, 20, 2),
                new ErrandDef(Verb.Craft, "ArrowFlint", 40, 60, 1),
                new ErrandDef(Verb.Craft, "Torch", 6, 10, 1),
            },
            [Heightmap.Biome.BlackForest] = new List<ErrandDef>
            {
                new ErrandDef(Verb.Kill, "Greydwarf/Greydwarf_Elite/Greydwarf_Shaman", 12, 20, 1),
                new ErrandDef(Verb.Kill, "Skeleton/Skeleton_Poison", 10, 15, 1),
                new ErrandDef(Verb.Kill, "Troll", 1, 2, 3),
                new ErrandDef(Verb.Gather, "CopperOre", 15, 25, 2),
                new ErrandDef(Verb.Gather, "TinOre", 10, 20, 2),
                new ErrandDef(Verb.Gather, "FineWood", 30, 40, 1),
                new ErrandDef(Verb.Gather, "RoundLog", 30, 40, 1),
                new ErrandDef(Verb.Gather, "Thistle", 8, 12, 1),
                new ErrandDef(Verb.Gather, "Blueberries", 20, 30, 1),
                new ErrandDef(Verb.Gather, "Resin", 20, 30, 1),
                new ErrandDef(Verb.Gather, "SurtlingCore", 3, 5, 3),
                new ErrandDef(Verb.Gather, "GreydwarfEye", 10, 20, 1),
                new ErrandDef(Verb.Craft, "Bronze", 6, 10, 2),
                new ErrandDef(Verb.Craft, "ArrowFire", 20, 40, 1),
                new ErrandDef(Verb.Craft, "BronzeNails", 40, 60, 2),
            },
            [Heightmap.Biome.Swamp] = new List<ErrandDef>
            {
                new ErrandDef(Verb.Kill, "Draugr/Draugr_Elite/Draugr_Ranged", 10, 15, 1),
                new ErrandDef(Verb.Kill, "Blob/BlobElite", 6, 10, 1),
                new ErrandDef(Verb.Kill, "Leech", 6, 10, 1),
                new ErrandDef(Verb.Kill, "Surtling", 5, 8, 2),
                new ErrandDef(Verb.Kill, "Wraith", 1, 2, 3),
                new ErrandDef(Verb.Kill, "Abomination", 1, 1, 3),
                new ErrandDef(Verb.Gather, "IronScrap", 15, 25, 2),
                new ErrandDef(Verb.Gather, "Guck", 8, 12, 1),
                new ErrandDef(Verb.Gather, "Bloodbag", 6, 10, 1),
                new ErrandDef(Verb.Gather, "Entrails", 6, 10, 1),
                new ErrandDef(Verb.Gather, "Ooze", 8, 12, 1),
                new ErrandDef(Verb.Gather, "AncientSeed", 2, 3, 3),
                new ErrandDef(Verb.Craft, "IronNails", 40, 60, 2),
                new ErrandDef(Verb.Craft, "ArrowIron", 20, 40, 2),
                new ErrandDef(Verb.Craft, "MeadBasePoisonResist", 2, 3, 1),
            },
            [Heightmap.Biome.Mountain] = new List<ErrandDef>
            {
                new ErrandDef(Verb.Kill, "Wolf", 6, 10, 1),
                new ErrandDef(Verb.Kill, "Hatchling", 4, 6, 2),
                new ErrandDef(Verb.Kill, "Fenring/Fenring_Cultist", 2, 3, 3),
                new ErrandDef(Verb.Kill, "StoneGolem", 1, 1, 3),
                new ErrandDef(Verb.Kill, "Ulv", 6, 10, 2),
                new ErrandDef(Verb.Gather, "SilverOre", 15, 25, 2),
                new ErrandDef(Verb.Gather, "Obsidian", 15, 25, 1),
                new ErrandDef(Verb.Gather, "FreezeGland", 6, 10, 1),
                new ErrandDef(Verb.Gather, "WolfPelt", 4, 6, 2),
                new ErrandDef(Verb.Gather, "Crystal", 3, 5, 3),
                new ErrandDef(Verb.Gather, "WolfMeat", 6, 10, 1),
                new ErrandDef(Verb.Craft, "ArrowObsidian", 40, 60, 2),
                new ErrandDef(Verb.Craft, "MeadBaseFrostResist", 2, 3, 1),
                new ErrandDef(Verb.Craft, "WolfJerky", 4, 8, 1),
            },
            [Heightmap.Biome.Plains] = new List<ErrandDef>
            {
                new ErrandDef(Verb.Kill, "Goblin/GoblinArcher/GoblinShaman", 10, 15, 1),
                new ErrandDef(Verb.Kill, "GoblinBrute", 2, 3, 3),
                new ErrandDef(Verb.Kill, "Deathsquito", 6, 10, 1),
                new ErrandDef(Verb.Kill, "Lox", 2, 3, 2),
                new ErrandDef(Verb.Kill, "BlobTar", 4, 6, 2),
                new ErrandDef(Verb.Gather, "BlackMetalScrap", 15, 25, 2),
                new ErrandDef(Verb.Gather, "Barley", 20, 30, 1),
                new ErrandDef(Verb.Gather, "Flax", 20, 30, 1),
                new ErrandDef(Verb.Gather, "Cloudberry", 20, 30, 1),
                new ErrandDef(Verb.Gather, "LoxMeat", 4, 6, 2),
                new ErrandDef(Verb.Gather, "Needle", 6, 10, 1),
                new ErrandDef(Verb.Gather, "Tar", 8, 12, 2),
                new ErrandDef(Verb.Craft, "ArrowNeedle", 40, 60, 2),
                new ErrandDef(Verb.Craft, "BreadDough", 6, 10, 1),
            },
            [Heightmap.Biome.Mistlands] = new List<ErrandDef>
            {
                new ErrandDef(Verb.Kill, "Seeker/SeekerBrood", 10, 15, 1),
                new ErrandDef(Verb.Kill, "SeekerBrute", 1, 2, 3),
                new ErrandDef(Verb.Kill, "Tick", 8, 12, 1),
                new ErrandDef(Verb.Kill, "Gjall", 1, 1, 3),
                new ErrandDef(Verb.Kill, "Hare", 4, 6, 1),
                new ErrandDef(Verb.Gather, "BlackMarble", 20, 30, 1),
                new ErrandDef(Verb.Gather, "Sap", 10, 15, 1),
                new ErrandDef(Verb.Gather, "YggdrasilWood", 20, 30, 1),
                new ErrandDef(Verb.Gather, "Softtissue", 8, 12, 2),
                new ErrandDef(Verb.Gather, "Carapace", 8, 12, 2),
                new ErrandDef(Verb.Gather, "Mandible", 2, 3, 3),
                new ErrandDef(Verb.Gather, "MushroomJotunPuffs", 8, 12, 1),
                new ErrandDef(Verb.Craft, "ArrowCarapace", 40, 60, 2),
                new ErrandDef(Verb.Craft, "YggdrasilPorridge", 4, 6, 1),
            },
            [Heightmap.Biome.AshLands] = new List<ErrandDef>
            {
                new ErrandDef(Verb.Kill, "Charred_Melee/Charred_Archer/Charred_Mage/Charred_Twitcher", 10, 15, 1),
                new ErrandDef(Verb.Kill, "Morgen", 1, 1, 3),
                new ErrandDef(Verb.Kill, "Volture", 4, 6, 1),
                new ErrandDef(Verb.Kill, "Asksvin", 2, 3, 2),
                new ErrandDef(Verb.Kill, "FallenValkyrie", 1, 1, 3),
                new ErrandDef(Verb.Gather, "Grausten", 20, 30, 1),
                new ErrandDef(Verb.Gather, "Blackwood", 20, 30, 1),
                new ErrandDef(Verb.Gather, "FlametalOreNew", 8, 12, 2),
                new ErrandDef(Verb.Gather, "CharredBone", 10, 20, 1),
                new ErrandDef(Verb.Gather, "AskHide", 4, 6, 2),
                            },
        };

        static bool s_resolved;

        /// Check every target against the game and fill in match keys and display names.
        public static void Resolve()
        {
            if (s_resolved || !ObjectDB.instance || !ZNetScene.instance) return;
            foreach (var kv in s_defs)
            {
                for (int i = kv.Value.Count - 1; i >= 0; i--)
                {
                    var def = kv.Value[i];
                    var keys = new List<string>();
                    foreach (var prefab in def.Prefabs)
                    {
                        if (def.Verb == Verb.Kill)
                        {
                            var go = ZNetScene.instance.GetPrefab(prefab);
                            var ch = go ? go.GetComponent<Character>() : null;
                            if (!ch) continue;
                            keys.Add(prefab);
                            if (def.Display == null) def.Display = ch.m_name;
                        }
                        else
                        {
                            var go = ObjectDB.instance.GetItemPrefab(prefab);
                            var drop = go ? go.GetComponent<ItemDrop>() : null;
                            if (!drop) continue;
                            keys.Add(drop.m_itemData.m_shared.m_name);
                            if (def.Display == null) def.Display = drop.m_itemData.m_shared.m_name;
                        }
                    }
                    if (keys.Count == 0)
                    {
                        Jotunn.Logger.LogWarning($"Errands: no such prefab {string.Join("/", def.Prefabs)} for {kv.Key}; errand dropped");
                        kv.Value.RemoveAt(i);
                        continue;
                    }
                    def.Keys = keys.ToArray();
                }
            }
            s_resolved = true;
        }

        /// The one errand a dvergr offers a given player on a given day. Seeded on the three of them, so
        /// asking twice gets the same answer and a different dvergr gives a different errand.
        public static Errand Offer(Heightmap.Biome biome, long playerId, int day, int giver, string giverName)
        {
            Resolve();
            if (!s_defs.TryGetValue(biome, out var defs) || defs.Count == 0) return null;
            var rnd = new System.Random(unchecked((int)(playerId * 31 + (long)biome * 7919 + day * 104729 + giver * 65537L)));
            var def = defs[rnd.Next(defs.Count)];
            return new Errand
            {
                Biome = biome, Day = day, Verb = def.Verb, Keys = def.Keys, Display = def.Display,
                Need = rnd.Next(def.Min, def.Max + 1), Have = 0, Tier = def.Tier, Owner = playerId,
                Giver = giver, GiverName = giverName,
            };
        }
    }
}
