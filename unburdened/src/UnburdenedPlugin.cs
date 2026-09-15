using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace Unburdened
{
    /// Armour no longer slows you down. The movement penalty is removed at the source (the item data),
    /// so run speed, jump and dodge stamina, and the tooltip all agree. Bonuses are kept.
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class UnburdenedPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "games.loxley.unburdened";
        public const string PluginName = "Unburdened";
        public const string PluginVersion = "0.1.0";

        public static ConfigEntry<bool> Armour;
        public static ConfigEntry<bool> Shields;
        public static ConfigEntry<bool> Weapons;
        public static ConfigEntry<float> Keep;

        private void Awake()
        {
            Armour = Config.Bind("General", "Armour", true, "Remove the movement penalty from helmets, chests, legs and capes.");
            Shields = Config.Bind("General", "Shields", false, "Also from shields (tower shields carry a big one).");
            Weapons = Config.Bind("General", "Weapons", false, "Also from weapons and tools.");
            Keep = Config.Bind("General", "Keep", 0f, "Fraction of the penalty to keep. 0 = none, 0.5 = half, 1 = vanilla.");
            new Harmony(PluginGUID).PatchAll();
        }

        static bool Covered(ItemDrop.ItemData.ItemType t)
        {
            switch (t)
            {
                case ItemDrop.ItemData.ItemType.Helmet:
                case ItemDrop.ItemData.ItemType.Chest:
                case ItemDrop.ItemData.ItemType.Legs:
                case ItemDrop.ItemData.ItemType.Shoulder:
                    return Armour.Value;
                case ItemDrop.ItemData.ItemType.Shield:
                    return Shields.Value;
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                case ItemDrop.ItemData.ItemType.Bow:
                case ItemDrop.ItemData.ItemType.Tool:
                case ItemDrop.ItemData.ItemType.Torch:
                    return Weapons.Value;
                default:
                    return false;
            }
        }

        public static void Apply(ObjectDB db)
        {
            if (db == null || db.m_items == null) return;
            foreach (var go in db.m_items)
            {
                var drop = go ? go.GetComponent<ItemDrop>() : null;
                var shared = drop?.m_itemData?.m_shared;
                if (shared == null || shared.m_movementModifier >= 0f || !Covered(shared.m_itemType)) continue;
                shared.m_movementModifier *= Keep.Value;
            }
        }

        [HarmonyPatch(typeof(ObjectDB), "Awake")]
        static class OnDbAwake { static void Postfix(ObjectDB __instance) => Apply(__instance); }

        [HarmonyPatch(typeof(ObjectDB), "CopyOtherDB")]
        static class OnDbCopy { static void Postfix(ObjectDB __instance) => Apply(__instance); }
    }
}
