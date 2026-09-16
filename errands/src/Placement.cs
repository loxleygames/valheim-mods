using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Errands
{
    /// Rides on vanilla ruin placement. ZoneSystem.SpawnLocation runs once per location, on the server,
    /// in Full mode (zone loaded straight away) or Ghost mode (zone generated ahead of the player and
    /// its objects thrown away, ZDOs kept). Either way the dvergr becomes a persistent ZDO in the
    /// zone, so it works for unexplored zones of existing worlds too. Deterministic from the seed
    /// vanilla hands us, so a given ruin has him or it doesn't, for everyone.
    [HarmonyPatch(typeof(ZoneSystem), "SpawnLocation")]
    static class ZoneSystem_SpawnLocation_Patch
    {
        static void Postfix(ZoneSystem.ZoneLocation location, int seed, Vector3 pos, Quaternion rot,
            ZoneSystem.SpawnMode mode, List<GameObject> spawnedGhostObjects)
        {
            if (mode != ZoneSystem.SpawnMode.Full && mode != ZoneSystem.SpawnMode.Ghost) return;
            if (location == null) return;

            var biome = WorldGenerator.instance.GetBiome(pos);
            if (!Biomes.RuinSet(biome).Contains(location.m_prefabName)) return;
            if (new System.Random(seed ^ 0x45524e44).NextDouble() >= ErrandsPlugin.RuinChance.Value) return;

            Placement.Spawn(pos, rot, biome, mode == ZoneSystem.SpawnMode.Ghost, spawnedGhostObjects, seed);
            ZLog.Log($"Errands: dvergr placed in {location.m_prefabName} at {pos}");
        }
    }

    public static class Placement
    {
        public static GameObject Spawn(Vector3 pos, Quaternion rot, Heightmap.Biome biome, bool ghost = false, List<GameObject> ghostList = null, int seed = 0)
        {
            var prefab = ZNetScene.instance.GetPrefab(ErrandsPlugin.DvergrPrefab);
            if (!prefab)
            {
                ZLog.LogWarning("Errands: dvergr prefab missing from ZNetScene");
                return null;
            }
            pos.y = ZoneSystem.instance.GetGroundHeight(pos);

            if (ghost) ZNetView.StartGhostInit();
            var go = UnityEngine.Object.Instantiate(prefab, pos, rot);
            var id = seed != 0 ? new System.Random(seed).Next(1, int.MaxValue) : UnityEngine.Random.Range(1, int.MaxValue);
            Dvergr.SetBiome(go, biome, id);
            if (ghost)
            {
                ghostList?.Add(go);
                ZNetView.FinishGhostInit();
            }
            return go;
        }
    }
}
