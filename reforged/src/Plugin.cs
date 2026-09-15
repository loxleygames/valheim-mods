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
        public static ConfigEntry<KeyboardShortcut> FavouriteKey;
        public static ConfigEntry<bool> StashButton;
        public static ConfigEntry<float> StashButtonX;
        public static ConfigEntry<float> StashButtonY;
        public static ConfigEntry<float> StashButtonSize;
        public static ConfigEntry<bool> SharedChests;
        public static ConfigEntry<bool> ArmourSlots;
        public static ConfigEntry<bool> CraftFromChests;
        public static ConfigEntry<float> CraftRange;
        public static ConfigEntry<bool> DebugLayout;

        private void Awake()
        {
            StashKey = Config.Bind("Stash", "Key", new KeyboardShortcut(KeyCode.P), "Stash everything into nearby chests.");
            StashRange = Config.Bind("Stash", "Range", 10f, "How far (m) to look for chests.");
            StashKeepHotbar = Config.Bind("Stash", "KeepHotbar", true, "Leave items on the hotbar alone.");
            StashPlayerBuiltOnly = Config.Bind("Stash", "PlayerBuiltOnly", true, "Ignore chests you didn't build (dungeon chests, etc).");
            FavouriteKey = Config.Bind("Stash", "FavouriteKey", new KeyboardShortcut(KeyCode.F), "Press while hovering an item to favourite it (Alt+click also works where the desktop lets it through).");
            StashButton = Config.Bind("Stash", "ShowButton", true, "Add a Stash button to the inventory screen.");
            StashButtonX = Config.Bind("Stash", "ButtonX", 33f, "Button centre, relative to the bottom-right corner of the inventory panel.");
            StashButtonY = Config.Bind("Stash", "ButtonY", 8f, "Button centre, relative to the bottom-right corner of the inventory panel.");
            StashButtonSize = Config.Bind("Stash", "ButtonSize", 38f, "Button width and height (px).");
            SharedChests = Config.Bind("SharedChests", "Enabled", true, "Let more than one player use a chest at the same time. Everyone needs the mod.");
            ArmourSlots = Config.Bind("ArmourSlots", "Enabled", true, "Extra row with head / chest / legs / cape slots.");

            CraftFromChests = Config.Bind("CraftFromChests", "Enabled", true, "Crafting, building and stations (smelter, kiln, fire, cooking, fermenter) use items from nearby chests.");
            CraftRange = Config.Bind("CraftFromChests", "Range", 15f, "How far (m) to look for chests when crafting.");
            DebugLayout = Config.Bind("Debug", "DumpLayout", false, "Log the inventory panel hierarchy once when the inventory opens.");

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
