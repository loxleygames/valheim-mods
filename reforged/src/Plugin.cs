using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace InventoryReforged
{
    /// Three inventory features that belong together:
    ///   Stash        - one key puts your inventory into nearby chests that already hold each item
    ///   Shared chests - two players can have the same chest open; non-owners act through the owner
    ///   Armour slots  - head / chest / legs / cape live in their own row, still real inventory (repairable)
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGUID = "games.loxley.inventoryreforged";
        public const string PluginName = "InventoryReforged";
        public const string PluginVersion = "0.1.0";

        public static ConfigEntry<KeyboardShortcut> StashKey;
        public static ConfigEntry<float> StashRange;
        public static ConfigEntry<bool> StashKeepHotbar;
        public static ConfigEntry<bool> StashPlayerBuiltOnly;
        public static ConfigEntry<bool> StashButton;
        public static ConfigEntry<float> StashButtonX;
        public static ConfigEntry<float> StashButtonY;
        public static ConfigEntry<float> StashButtonSize;
        public static ConfigEntry<bool> SharedChests;
        public static ConfigEntry<bool> ArmourSlots;
        public static ConfigEntry<float> ArmourRowOffset;

        private void Awake()
        {
            StashKey = Config.Bind("Stash", "Key", new KeyboardShortcut(KeyCode.P), "Stash everything into nearby chests.");
            StashRange = Config.Bind("Stash", "Range", 10f, "How far (m) to look for chests.");
            StashKeepHotbar = Config.Bind("Stash", "KeepHotbar", true, "Leave items on the hotbar alone.");
            StashPlayerBuiltOnly = Config.Bind("Stash", "PlayerBuiltOnly", true, "Ignore chests you didn't build (dungeon chests, etc).");
            StashButton = Config.Bind("Stash", "ShowButton", true, "Add a Stash button to the inventory screen.");
            StashButtonX = Config.Bind("Stash", "ButtonX", -10f, "Button offset from the top-right of the inventory panel.");
            StashButtonY = Config.Bind("Stash", "ButtonY", -6f, "Button offset from the top-right of the inventory panel.");
            StashButtonSize = Config.Bind("Stash", "ButtonSize", 30f, "Button width and height (px).");
            SharedChests = Config.Bind("SharedChests", "Enabled", true, "Let more than one player use a chest at the same time. Everyone needs the mod.");
            ArmourSlots = Config.Bind("ArmourSlots", "Enabled", true, "Extra row with head / chest / legs / cape slots.");
            ArmourRowOffset = Config.Bind("ArmourSlots", "PanelGrow", 74f, "How much taller (px) to make the inventory panel for the armour row.");

            new Harmony(PluginGUID).PatchAll();
        }

        private void Update()
        {
            if (!Player.m_localPlayer || !StashKey.Value.IsDown()) return;
            if (Console.IsVisible() || Chat.instance?.HasFocus() == true || TextInput.IsVisible() || Menu.IsVisible()) return;
            Stash.Run(Player.m_localPlayer);
        }
    }
}
