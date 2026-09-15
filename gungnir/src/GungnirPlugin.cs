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
        public const string PluginVersion = "0.1.1";

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
        public static readonly System.Collections.Generic.List<ThrowOrigin> InFlight = new System.Collections.Generic.List<ThrowOrigin>();
        public Vector2i Slot;
        public bool WasEquipped;
        void OnEnable() => InFlight.Add(this);
        void OnDisable() => InFlight.Remove(this);
    }

    /// Quitting with a spear in the air would lose it (projectiles aren't saved). Put it back in the bag first.
    [HarmonyPatch(typeof(Game), "Shutdown")]
    static class RecallOnShutdown
    {
        static void Prefix() => Recall();

        public static void Recall()
        {
            var player = Player.m_localPlayer;
            if (!player) return;
            foreach (var t in ThrowOrigin.InFlight.ToArray())
            {
                if (!t) continue;
                var p = t.GetComponent<Projectile>();
                var item = p ? p.m_spawnItem : null;
                if (item == null) continue;
                var inv = player.GetInventory();
                if (inv.GetItemAt(t.Slot.x, t.Slot.y) == null ? inv.AddItem(item, t.Slot) : inv.AddItem(item))
                {
                    p.m_spawnItem = null;
                    var nview = p.GetComponent<ZNetView>();
                    if (nview && nview.IsValid()) nview.Destroy(); else Object.Destroy(p.gameObject);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Game), "ContinueLogout")]
    static class RecallOnLogout
    {
        static void Prefix() => RecallOnShutdown.Recall();
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
        public static readonly int OwnerHash = "Gungnir_Owner".GetStableHashCode();
        public static readonly int SlotXHash = "Gungnir_SlotX".GetStableHashCode();
        public static readonly int SlotYHash = "Gungnir_SlotY".GetStableHashCode();
        public static readonly int EquippedHash = "Gungnir_Equipped".GetStableHashCode();

        static void Postfix(ItemDrop.ItemData item, ItemDrop __result)
        {
            var p = TagDrop.Current;
            if (!p || !__result || item != p.m_spawnItem) return;
            var origin = p.GetComponent<ThrowOrigin>();
            if (!origin) return;
            var r = __result.gameObject.AddComponent<Returning>();
            r.Slot = origin.Slot;
            r.WasEquipped = origin.WasEquipped;

            // Remember on the spear itself, so a throw-then-logout still comes home next session.
            var nview = __result.GetComponent<ZNetView>();
            if (nview && nview.IsValid())
            {
                var zdo = nview.GetZDO();
                zdo.Set(OwnerHash, Game.instance.GetPlayerProfile().GetPlayerID());
                zdo.Set(SlotXHash, origin.Slot.x);
                zdo.Set(SlotYHash, origin.Slot.y);
                zdo.Set(EquippedHash, origin.WasEquipped);
            }
        }
    }

    /// A spear that was thrown by this character and never returned: pick up where we left off.
    [HarmonyPatch(typeof(ItemDrop), "Awake")]
    static class ReattachOnLoad
    {
        static void Postfix(ItemDrop __instance)
        {
            if (__instance.GetComponent<Returning>()) return;
            var nview = __instance.GetComponent<ZNetView>();
            if (!nview || !nview.IsValid()) return;
            var zdo = nview.GetZDO();
            long owner = zdo.GetLong(AttachReturn.OwnerHash, 0L);
            if (owner == 0L || Game.instance == null || owner != Game.instance.GetPlayerProfile().GetPlayerID()) return;
            var r = __instance.gameObject.AddComponent<Returning>();
            r.Slot = new Vector2i(zdo.GetInt(AttachReturn.SlotXHash), zdo.GetInt(AttachReturn.SlotYHash));
            r.WasEquipped = zdo.GetBool(AttachReturn.EquippedHash);
        }
    }
}
