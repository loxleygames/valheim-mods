using System.Collections.Generic;
using System.Linq;
using Jotunn.Entities;
using UnityEngine;

namespace Errands
{
    /// `errands_spawn`: put a dvergr down in front of you, dressed for the biome you're in.
    /// For testing, and for worlds already explored to the edge.
    public class SpawnCommand : ConsoleCommand
    {
        public override string Name => "errands_spawn";
        public override string Help => "Spawn an errand dvergr in front of you";
        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            var player = Player.m_localPlayer;
            if (!player) return;
            var look = player.GetLookDir();
            look.y = 0f;
            var pos = player.transform.position + look.normalized * 2.5f;
            var rot = Quaternion.LookRotation(-look);
            var go = Placement.Spawn(pos, rot, WorldGenerator.instance.GetBiome(pos));
            Console.instance.Print(go ? $"Dvergr spawned at {go.transform.position}" : "Failed: prefab not found");
        }
    }

    /// `errands_locations [biome]`: the real vanilla location names, grouped by biome, so the ruin
    /// lists in config can be corrected against what this game version actually has.
    public class LocationsCommand : ConsoleCommand
    {
        public override string Name => "errands_locations";
        public override string Help => "List vanilla location names by biome (optionally filter by biome name)";

        public override void Run(string[] args)
        {
            var filter = args.Length > 0 ? args[0].ToLowerInvariant() : null;
            var byBiome = new SortedDictionary<string, List<string>>();
            foreach (var loc in ZoneSystem.instance.m_locations)
            {
                if (!loc.m_enable) continue;
                var key = loc.m_biome.ToString();
                if (filter != null && !key.ToLowerInvariant().Contains(filter)) continue;
                if (!byBiome.TryGetValue(key, out var list)) byBiome[key] = list = new List<string>();
                list.Add($"{loc.m_prefabName} x{loc.m_quantity}");
            }
            foreach (var kv in byBiome)
            {
                var line = $"[{kv.Key}] {string.Join(", ", kv.Value.OrderBy(s => s))}";
                Console.instance.Print(line);
                ZLog.Log("Errands locations " + line);
            }
            Console.instance.Print("Also written to LogOutput.log");
        }
    }
}

namespace Errands
{
    /// `errands_texdump`: write the dvergr's textures as PNGs beside the DLL, to pick the cloth hue.
    public class TexDumpCommand : ConsoleCommand
    {
        public override string Name => "errands_texdump";
        public override string Help => "Dump the dvergr's textures to PNG next to Errands.dll";

        public override void Run(string[] args)
        {
            var prefab = ZNetScene.instance.GetPrefab(ErrandsPlugin.DvergrPrefab);
            if (!prefab) { Console.instance.Print("Prefab missing"); return; }
            var dir = System.IO.Path.GetDirectoryName(typeof(ErrandsPlugin).Assembly.Location);
            foreach (var line in Tinting.Dump(prefab, dir)) Console.instance.Print(line);
        }
    }
}

namespace Errands
{
    /// `errands_rep <biome> <amount>`: set your standing with a biome's dvergr, for testing the shelves.
    public class RepCommand : ConsoleCommand
    {
        public override string Name => "errands_rep";
        public override string Help => "Set standing: errands_rep <biome> <amount>";
        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            if (args.Length < 2 || !System.Enum.TryParse(args[0], true, out Heightmap.Biome biome) || !int.TryParse(args[1], out var amount))
            {
                Console.instance.Print("errands_rep <Meadows|BlackForest|Swamp|Mountain|Plains|Mistlands|AshLands> <amount>");
                return;
            }
            Journal.SetRep(biome, amount);
            Console.instance.Print($"{biome} standing = {amount} ({Shop.TierTag(amount)})");
        }
    }
}

namespace Errands
{
    /// `errands_done`: mark every held kill/craft errand complete, for testing hand-ins.
    public class DoneCommand : ConsoleCommand
    {
        public override string Name => "errands_done";
        public override string Help => "Mark your held errands as complete";
        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            Journal.CompleteAll();
            Console.instance.Print("Errands marked done; talk to the dvergr.");
        }
    }
}

namespace Errands
{
    /// `errands_kit <biome|all>`: every unique for the biome, marks, three ★★★ chests, Kin standing.
    public class KitCommand : ConsoleCommand
    {
        public override string Name => "errands_kit";
        public override string Help => "Test kit: errands_kit <biome|all>";
        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            var player = Player.m_localPlayer;
            if (!player || args.Length < 1) { Console.instance.Print("errands_kit <Meadows|BlackForest|Swamp|Mountain|Plains|Mistlands|AshLands|all>"); return; }
            var biomes = new List<Heightmap.Biome>();
            if (args[0].ToLowerInvariant() == "all") biomes.AddRange(Biomes.All);
            else if (System.Enum.TryParse(args[0], true, out Heightmap.Biome b)) biomes.Add(b);
            else { Console.instance.Print("Unknown biome " + args[0]); return; }

            int given = 0;
            foreach (var biome in biomes)
            {
                foreach (var prefab in Uniques.All(biome))
                {
                    Give(player, prefab, 1, 1);
                    given++;
                }
                if (ObjectDB.instance.GetItemPrefab(Rewards.ChestPrefab(biome))) Give(player, Rewards.ChestPrefab(biome), 3, 3);
                Journal.SetRep(biome, Shop.KinRep);
            }
            Give(player, Rewards.MarkPrefab, 100, 1);
            Console.instance.Print($"{given} uniques, chests, 100 marks; standing set to Kin. Bag full? Check your feet.");
        }

        static void Give(Player player, string prefab, int amount, int quality)
        {
            var go = ObjectDB.instance.GetItemPrefab(prefab);
            if (!go) { Console.instance.Print("missing " + prefab); return; }
            if (player.GetInventory().AddItem(prefab, amount, quality, 0, 0L, "", false) == null)
            {
                var data = go.GetComponent<ItemDrop>().m_itemData.Clone();
                data.m_quality = quality;
                ItemDrop.DropItem(data, amount, player.transform.position + player.transform.forward + Vector3.up, Quaternion.identity);
            }
        }
    }
}
