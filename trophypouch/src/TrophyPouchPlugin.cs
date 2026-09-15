using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace TrophyPouch
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    public class TrophyPouchPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "games.loxley.trophypouch";
        public const string PluginName = "TrophyPouch";
        public const string PluginVersion = "0.1.0";

        public static ConfigEntry<bool> SweepOnLoad;
        public static ConfigEntry<float> ButtonX;
        public static ConfigEntry<float> ButtonY;

        private void Awake()
        {
            SweepOnLoad = Config.Bind("General", "SweepOnLoad", true, "Move trophies already in your bag into the pouch when you spawn.");
            ButtonX = Config.Bind("General", "ButtonX", 33f, "Pouch button centre, relative to the bottom-right corner of the inventory panel.");
            ButtonY = Config.Bind("General", "ButtonY", 118f, "Pouch button centre, relative to the bottom-right corner of the inventory panel.");
            new Harmony(PluginGUID).PatchAll();
        }
    }
}
