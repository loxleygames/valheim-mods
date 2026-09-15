using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace QuickStash
{
    /// One key: every item in your inventory that a nearby chest already holds goes into that chest.
    /// Uses the game's own "Place stacks" RPC per chest, so the chest owner does the in-use check and
    /// hands over ownership before anything moves — safe in multiplayer, and the other players don't
    /// need the mod.
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class QuickStashPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "games.loxley.quickstash";
        public const string PluginName = "QuickStash";
        public const string PluginVersion = "0.1.0";

        public static ConfigEntry<KeyboardShortcut> Key;
        public static ConfigEntry<float> Range;
        public static ConfigEntry<bool> KeepHotbar;
        public static ConfigEntry<bool> PlayerBuiltOnly;
        public static ConfigEntry<bool> ShowButton;
        public static ConfigEntry<float> ButtonX;
        public static ConfigEntry<float> ButtonY;

        private void Awake()
        {
            Key = Config.Bind("General", "Key", new KeyboardShortcut(KeyCode.P), "Stash everything into nearby chests.");
            Range = Config.Bind("General", "Range", 10f, "How far (m) to look for chests.");
            KeepHotbar = Config.Bind("General", "KeepHotbar", true, "Leave items on the hotbar alone.");
            PlayerBuiltOnly = Config.Bind("General", "PlayerBuiltOnly", true, "Ignore chests you didn't build (dungeon chests, etc).");
            ShowButton = Config.Bind("General", "ShowButton", true, "Add a Stash button to the inventory screen.");
            ButtonX = Config.Bind("Button", "X", -10f, "Button offset from the top-right of the inventory panel.");
            ButtonY = Config.Bind("Button", "Y", -45f, "Button offset from the top-right of the inventory panel.");

            new Harmony(PluginGUID).PatchAll();
        }

        private void Update()
        {
            if (!Player.m_localPlayer || !Key.Value.IsDown()) return;
            if (Console.IsVisible() || Chat.instance?.HasFocus() == true || TextInput.IsVisible() || Menu.IsVisible()) return;
            Stash.Run(Player.m_localPlayer);
        }
    }

    public static class Stash
    {
        // Set while our requests are in flight so the response handler knows to use our stacking rules.
        public static readonly HashSet<Container> Pending = new HashSet<Container>();
        public static int Moved;
        private static float s_lastRun;

        public static void Run(Player player)
        {
            if (Time.time - s_lastRun < 0.5f) return;
            s_lastRun = Time.time;

            var containers = FindNearby(player.transform.position, QuickStashPlugin.Range.Value);
            if (containers.Count == 0)
            {
                player.Message(MessageHud.MessageType.Center, "No chests nearby");
                return;
            }
            Moved = 0;
            Pending.Clear();
            foreach (var c in containers)
            {
                Pending.Add(c);
                c.StackAll();
            }
            // Responses come back over the network; report whatever has landed after a moment.
            player.StartCoroutine(Report(player));
        }

        private static System.Collections.IEnumerator Report(Player player)
        {
            yield return new WaitForSeconds(1.5f);
            Pending.Clear();
            player.Message(MessageHud.MessageType.Center, Moved > 0 ? $"Stashed {Moved} items" : "Nothing to stash");
        }

        public static List<Container> FindNearby(Vector3 pos, float range)
        {
            var found = new List<Container>();
            var seen = new HashSet<Container>();
            foreach (var col in Physics.OverlapSphere(pos, range, LayerMask.GetMask("piece", "item")))
            {
                var c = col.GetComponentInParent<Container>();
                if (!c || !seen.Add(c)) continue;
                if (!c.GetComponent<ZNetView>()?.IsValid() ?? true) continue;
                if (c.m_wagon || c.GetComponentInParent<Ship>()) continue;
                if (QuickStashPlugin.PlayerBuiltOnly.Value)
                {
                    var piece = c.GetComponent<Piece>();
                    if (!piece || !piece.IsPlacedByPlayer()) continue;
                }
                if (c.m_checkGuardStone && !PrivateArea.CheckAccess(c.transform.position, 0f, false)) continue;
                found.Add(c);
            }
            return found;
        }

        /// Our version of Inventory.StackAll: same rule (chest must already hold the item), plus hotbar protection
        /// and no per-chest message spam.
        public static int StackInto(Inventory chest, Inventory from)
        {
            int moved = 0;
            foreach (var item in new List<ItemDrop.ItemData>(from.GetAllItems()))
            {
                if (item.m_equipped) continue;
                if (QuickStashPlugin.KeepHotbar.Value && item.m_gridPos.y == 0) continue;
                if (!chest.ContainsItemByName(item.m_shared.m_name)) continue;
                int stack = item.m_stack;
                if (chest.AddItem(item))
                {
                    from.RemoveItem(item);
                    moved += stack;
                }
            }
            return moved;
        }
    }

    /// The chest owner granted us the stack: do it with our rules instead of the vanilla ones.
    [HarmonyPatch(typeof(Container), "RPC_StackResponse")]
    static class Container_RPC_StackResponse_Patch
    {
        static bool Prefix(Container __instance, bool granted)
        {
            if (!Stash.Pending.Remove(__instance)) return true;
            if (!granted || !Player.m_localPlayer) return false;
            int n = Stash.StackInto(__instance.GetInventory(), Player.m_localPlayer.GetInventory());
            if (n > 0)
            {
                Stash.Moved += n;
                InventoryGui.instance.m_moveItemEffects.Create(__instance.transform.position, Quaternion.identity);
            }
            return false;
        }
    }
}
