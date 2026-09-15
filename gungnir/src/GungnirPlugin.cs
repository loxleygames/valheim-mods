using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Gungnir
{
    /// Thrown spears come back. A moment after one lands it returns to the slot it left,
    /// re-equipped if it was in your hand. No more spears at the bottom of the fjord.
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class GungnirPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "games.loxley.gungnir";
        public const string PluginName = "Gungnir";
        public const string PluginVersion = "0.1.0";

        public static ConfigEntry<float> Delay;
        public static ConfigEntry<float> OnlyBeyond;
        public static ConfigEntry<bool> ShowMessage;

        private void Awake()
        {
            Delay = Config.Bind("General", "Delay", 1.5f, "Seconds after landing before the spear returns.");
            OnlyBeyond = Config.Bind("General", "OnlyBeyond", 0f, "Only return if the spear landed further than this (m) from you. 0 = always return.");
            ShowMessage = Config.Bind("General", "ShowMessage", true, "Show a message when the spear returns.");
            new Harmony(PluginGUID).PatchAll();
        }
    }

    /// Where the spear came from, remembered from the moment it left the hand.
    public class ThrowOrigin : MonoBehaviour
    {
        public Vector2i Slot;
        public bool WasEquipped;
    }

    /// Sits on the dropped spear and brings it home.
    public class Returning : MonoBehaviour
    {
        public Vector2i Slot;
        public bool WasEquipped;
        float m_landed;
        ItemDrop m_drop;
        ZNetView m_nview;

        void Awake()
        {
            m_drop = GetComponent<ItemDrop>();
            m_nview = GetComponent<ZNetView>();
            m_landed = Time.time;
        }

        void Update()
        {
            var player = Player.m_localPlayer;
            if (!player || !m_drop || !m_nview || !m_nview.IsValid() || !m_nview.IsOwner()) return;
            if (Time.time - m_landed < GungnirPlugin.Delay.Value) return;
            float dist = Vector3.Distance(player.transform.position, transform.position);
            if (GungnirPlugin.OnlyBeyond.Value > 0f && dist < GungnirPlugin.OnlyBeyond.Value) { enabled = false; return; }

            var inv = player.GetInventory();
            var item = m_drop.m_itemData;
            bool added = inv.GetItemAt(Slot.x, Slot.y) == null ? inv.AddItem(item, Slot) : inv.AddItem(item);
            if (!added) { enabled = false; return; } // bag full: it stays where it fell

            if (WasEquipped) player.EquipItem(item, triggerEquipEffects: false);
            if (GungnirPlugin.ShowMessage.Value)
                player.Message(MessageHud.MessageType.TopLeft, "Returned: " + Localization.instance.Localize(item.m_shared.m_name), 0, item.GetIcon());
            m_nview.Destroy();
        }
    }

    /// The projectile knows its item at setup, before the throw consumes it from the inventory.
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.Setup))]
    static class RememberOrigin
    {
        static void Postfix(Projectile __instance, Character owner, ItemDrop.ItemData item)
        {
            if (__instance.m_spawnItem == null || item == null || owner != Player.m_localPlayer) return;
            var origin = __instance.gameObject.AddComponent<ThrowOrigin>();
            origin.Slot = item.m_gridPos;
            origin.WasEquipped = item.m_equipped;
        }
    }

    /// When the projectile drops its item on landing, tag the drop with the origin.
    [HarmonyPatch(typeof(Projectile), "SpawnOnHit")]
    static class TagDrop
    {
        public static Projectile Current;
        static void Prefix(Projectile __instance) => Current = __instance;
        static void Postfix() => Current = null;
    }

    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.DropItem), typeof(ItemDrop.ItemData), typeof(int), typeof(Vector3), typeof(Quaternion))]
    static class AttachReturn
    {
        static void Postfix(ItemDrop.ItemData item, ItemDrop __result)
        {
            var p = TagDrop.Current;
            if (!p || !__result || item != p.m_spawnItem) return;
            var origin = p.GetComponent<ThrowOrigin>();
            if (!origin) return;
            var r = __result.gameObject.AddComponent<Returning>();
            r.Slot = origin.Slot;
            r.WasEquipped = origin.WasEquipped;
        }
    }
}
