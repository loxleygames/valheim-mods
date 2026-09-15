using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Moorings
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    public class MooringsPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "games.loxley.moorings";
        public const string PluginName = "Moorings";
        public const string PluginVersion = "0.1.1";

        public const string PrefabName = "MooringPost";

        public static ConfigEntry<float> MoorRange;
        public static ConfigEntry<float> Slack;
        public static ConfigEntry<float> Pull;
        public static ConfigEntry<float> LineHeight;
        public static ConfigEntry<float> CoilRadius;

        private void Awake()
        {
            MoorRange = Config.Bind("General", "MoorRange", 12f,
                "How far (m) a boat can be from the post and still be tied to it.");
            Slack = Config.Bind("General", "Slack", 3f,
                "How far (m) a moored boat can drift from the post before the line pulls it back.");
            Pull = Config.Bind("General", "Pull", 6f,
                "Strength of the pull back toward the post once past the slack.");

            LineHeight = Config.Bind("General", "LineHeight", 0.7f,
                "How high up the post (m) the line is tied.");
            CoilRadius = Config.Bind("General", "CoilRadius", 0.17f,
                "Radius (m) of the rope coiled round the post. Match it to the post's thickness.");

            PrefabManager.OnVanillaPrefabsAvailable += AddMooringPost;
            new Harmony(PluginGUID).PatchAll();
        }

        private void AddMooringPost()
        {
            // Log pole 2m: fatter and rougher than the plain pole, reads as a bollard.
            var prefab = PrefabManager.Instance.CreateClonedPrefab(PrefabName, "wood_pole_log")
                      ?? PrefabManager.Instance.CreateClonedPrefab(PrefabName, "wood_pole2");
            prefab.AddComponent<MooringPost>();

            var piece = new CustomPiece(prefab, fixReference: true, new PieceConfig
            {
                Name = "Mooring post",
                Description = "Tie the nearest boat to it. A moored boat cannot be damaged.",
                PieceTable = PieceTables.Hammer,
                Category = PieceCategories.Misc,
                CraftingStation = CraftingStations.Workbench,
                Requirements = new[]
                {
                    new RequirementConfig("Wood", 6, 0, true),
                    new RequirementConfig("Resin", 2, 0, true),
                }
            });
            PieceManager.Instance.AddPiece(piece);

            PrefabManager.OnVanillaPrefabsAvailable -= AddMooringPost;
        }
    }
}
