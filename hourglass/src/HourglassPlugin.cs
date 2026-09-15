using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Hourglass
{
    /// A buildable hourglass. Use it and the sun stops at noon for everyone until someone uses it again.
    ///
    /// The world clock keeps running underneath (days still pass); only the time of day each client
    /// sees is pinned, the same way the game's own `tod` debug command does it. That also means no
    /// night spawns while it's held. State is a vanilla global key, so it syncs through the server
    /// without the server needing the mod.
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    public class HourglassPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "games.loxley.hourglass";
        public const string PluginName = "Hourglass";
        public const string PluginVersion = "0.1.0";

        public const string PrefabName = "LoxleyHourglass";
        public const string Key = "loxley_hourglass";

        public static ConfigEntry<float> TimeOfDay;

        private void Awake()
        {
            TimeOfDay = Config.Bind("General", "TimeOfDay", 0.5f,
                "Where the sun is held. 0 = midnight, 0.25 = dawn, 0.5 = noon, 0.75 = dusk.");

            PrefabManager.OnVanillaPrefabsAvailable += AddPiece;
            new Harmony(PluginGUID).PatchAll();
        }

        private void AddPiece()
        {
            // The Ward is a small runed stone that glows: the closest vanilla shape to a time-magic object.
            var prefab = PrefabManager.Instance.CreateClonedPrefab(PrefabName, "guard_stone");
            foreach (var area in prefab.GetComponentsInChildren<PrivateArea>(true)) Object.DestroyImmediate(area);
            prefab.AddComponent<HourglassPiece>();

            PieceManager.Instance.AddPiece(new CustomPiece(prefab, fixReference: true, new PieceConfig
            {
                Name = "Hourglass",
                Description = "Holds the sun at noon for everyone until used again. Good for building.",
                PieceTable = PieceTables.Hammer,
                Category = PieceCategories.Misc,
                CraftingStation = CraftingStations.Workbench,
                Requirements = new[]
                {
                    new RequirementConfig("FineWood", 6, 0, true),
                    new RequirementConfig("Resin", 4, 0, true),
                    new RequirementConfig("GreydwarfEye", 2, 0, true),
                }
            }));

            PrefabManager.OnVanillaPrefabsAvailable -= AddPiece;
        }

        public static bool IsHeld => ZoneSystem.instance && ZoneSystem.instance.GetGlobalKey(Key);
    }

    public class HourglassPiece : MonoBehaviour, Interactable, Hoverable
    {
        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold) return false;
            if (HourglassPlugin.IsHeld)
            {
                ZoneSystem.instance.RemoveGlobalKey(HourglassPlugin.Key);
                user.Message(MessageHud.MessageType.Center, "Time flows again");
            }
            else
            {
                ZoneSystem.instance.SetGlobalKey(HourglassPlugin.Key);
                user.Message(MessageHud.MessageType.Center, "Time stands still");
            }
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;
        public string GetHoverName() => "Hourglass";
        public float GetHoverOffset() => 0f;

        public string GetHoverText()
        {
            string action = HourglassPlugin.IsHeld ? "Let time flow" : "Stop time";
            return Localization.instance.Localize($"Hourglass\n[<color=yellow><b>$KEY_Use</b></color>] {action}");
        }
    }

    /// Pin the day fraction while the key is set; hand control back when it's cleared.
    [HarmonyPatch(typeof(EnvMan), "FixedUpdate")]
    static class HoldTime
    {
        static bool s_wasHeld;

        static void Prefix(EnvMan __instance)
        {
            bool held = HourglassPlugin.IsHeld;
            if (held)
            {
                __instance.m_debugTimeOfDay = true;
                __instance.m_debugTime = HourglassPlugin.TimeOfDay.Value;
            }
            else if (s_wasHeld)
            {
                __instance.m_debugTimeOfDay = false;
            }
            s_wasHeld = held;
        }
    }
}
