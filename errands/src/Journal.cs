using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Errands
{
    /// The player's errand book, kept in Player.m_customData so it saves with the character.
    /// Kills arrive by RPC from whoever owns the dying creature; crafts from the crafting panel;
    /// gathers are read live from the inventory and taken on hand-in.
    public static class Journal
    {
        const string ActiveKey = "errands.active";
        const string TakenKey = "errands.taken";
        const string RepPrefix = "errands.rep.";
        const string KillRpc = "Errands_Kill";
        const string ShareRpc = "Errands_Share";

        static Player P => Player.m_localPlayer;

        public static int Today()
        {
            float hours = ErrandsPlugin.RealHours.Value;
            if (hours > 0f) return (int)(DateTime.UtcNow.Ticks / (TimeSpan.TicksPerHour * (double)hours));
            return EnvMan.instance ? EnvMan.instance.GetDay() : 0;
        }

        public static List<Errand> Active()
        {
            var list = new List<Errand>();
            if (!P || !P.m_customData.TryGetValue(ActiveKey, out var s) || string.IsNullOrEmpty(s)) return list;
            foreach (var line in s.Split('\n'))
            {
                var e = Errand.Parse(line);
                if (e != null) list.Add(e);
            }
            return list;
        }

        static void Save(List<Errand> list)
        {
            if (!P) return;
            P.m_customData[ActiveKey] = string.Join("\n", list.Select(e => e.Serialize()));
        }

        public static bool Taken(int giver, int day)
        {
            return P && P.m_customData.TryGetValue(TakenKey, out var s) && s.Split(';').Contains($"{giver}:{day}");
        }

        static void MarkTaken(int giver, int day)
        {
            // Only today's marks matter; older ones are dead weight.
            var keep = new List<string> { $"{giver}:{day}" };
            if (P.m_customData.TryGetValue(TakenKey, out var s))
                keep.AddRange(s.Split(';').Where(k => k.EndsWith(":" + day) && !keep.Contains(k)));
            P.m_customData[TakenKey] = string.Join(";", keep);
        }

        public static int Rep(Heightmap.Biome biome) =>
            P && P.m_customData.TryGetValue(RepPrefix + (int)biome, out var s) && int.TryParse(s, out var n) ? n : 0;

        static void AddRep(Heightmap.Biome biome, int amount) => SetRep(biome, Rep(biome) + amount);

        public static void SetRep(Heightmap.Biome biome, int value)
        {
            if (P) P.m_customData[RepPrefix + (int)biome] = value.ToString();
        }

        public static Errand TodaysOffer(Dvergr d) => P ? Tables.Offer(d.Biome, P.GetPlayerID(), Today(), d.Id, d.Name) : null;

        public static bool Full => Active().Count >= ErrandsPlugin.MaxErrands.Value;

        public static bool Accept(Errand offer)
        {
            if (!P || offer == null || Full || Taken(offer.Giver, offer.Day)) return false;
            var list = Active();
            list.Add(offer);
            Save(list);
            MarkTaken(offer.Giver, offer.Day);
            P.Message(MessageHud.MessageType.Center, "Errand taken: " + offer.Title);
            Share(offer);
            return true;
        }

        /// Everyone standing near you gets a copy in their book.
        static void Share(Errand e)
        {
            var line = e.Serialize();
            foreach (var other in NearbyPlayers(P.transform.position))
            {
                if (other == P) continue;
                var nview = other.GetComponent<ZNetView>();
                if (nview && nview.IsValid()) nview.InvokeRPC(ShareRpc, P.GetPlayerName(), line);
            }
        }

        static void OnShared(string from, string line)
        {
            var e = Errand.Parse(line);
            if (e == null || !P) return;
            var list = Active();
            if (list.Exists(x => x.Id == e.Id)) return;
            if (list.Count >= ErrandsPlugin.MaxErrands.Value)
            {
                P.Message(MessageHud.MessageType.Center, $"{from} took an errand but your hands are full");
                return;
            }
            e.Have = 0;
            list.Add(e);
            Save(list);
            P.Message(MessageHud.MessageType.Center, $"{from} shares an errand: {e.Title}");
        }

        public static List<Player> NearbyPlayers(Vector3 pos)
        {
            float r = ErrandsPlugin.PartyRange.Value;
            return Player.GetAllPlayers().FindAll(p => p && Vector3.Distance(p.transform.position, pos) <= r);
        }

        public static int Progress(Errand e)
        {
            if (e.Verb != Verb.Gather || !P) return Mathf.Min(e.Have, e.Need);
            int n = 0;
            foreach (var key in e.Keys) n += P.GetInventory().CountItems(key);
            return Mathf.Min(n, e.Need);
        }

        public static bool Complete(Errand e) => Progress(e) >= e.Need;

        public static void Abandon(Errand e)
        {
            var list = Active();
            list.RemoveAll(x => x.Id == e.Id);
            Save(list);
        }

        /// Hand in a finished errand to a dvergr of its biome. Gather errands surrender the goods.
        public static bool HandIn(Errand e, Heightmap.Biome at)
        {
            if (!P || e.Biome != at || !Complete(e)) return false;
            if (e.Verb == Verb.Gather)
            {
                int left = e.Need;
                foreach (var key in e.Keys)
                {
                    int take = Mathf.Min(left, P.GetInventory().CountItems(key));
                    if (take > 0) { P.GetInventory().RemoveItem(key, take); left -= take; }
                }
            }
            Abandon(e);
            int rep = e.Tier * ErrandsPlugin.RepPerTier.Value;
            AddRep(e.Biome, rep);
            int now = Rep(e.Biome);
            P.Message(MessageHud.MessageType.Center, $"Errand done. {Biomes.Label(e.Biome)} standing +{rep}  ({Shop.TierTag(now)} {now})");
            Rewards.Grant(P, e);
            return true;
        }

        static void Bump(Func<Errand, bool> matches, int amount)
        {
            var list = Active();
            bool changed = false;
            foreach (var e in list)
            {
                if (e.Verb == Verb.Gather || e.Have >= e.Need || !matches(e)) continue;
                e.Have = Mathf.Min(e.Need, e.Have + amount);
                changed = true;
                P.Message(MessageHud.MessageType.TopLeft, $"{e.Title}  {e.Have}/{e.Need}");
            }
            if (changed) Save(list);
        }

        /// Cheat: everything you hold counts as done (bring errands still need the goods).
        public static void CompleteAll()
        {
            var list = Active();
            foreach (var e in list) e.Have = e.Need;
            Save(list);
        }

        public static void OnKill(string prefab) => Bump(e => e.Verb == Verb.Kill && e.Keys.Contains(prefab), 1);

        public static void OnCraft(string sharedName, int count) => Bump(e => e.Verb == Verb.Craft && e.Keys.Contains(sharedName), count);

        /// "Bring" errands read the bag live, so announce them whenever the bag changes and the count went up.
        static readonly Dictionary<string, int> s_lastShown = new Dictionary<string, int>();

        public static void OnInventoryChanged()
        {
            foreach (var e in Active())
            {
                if (e.Verb != Verb.Gather) continue;
                int now = Progress(e);
                s_lastShown.TryGetValue(e.Id, out var last);
                if (now > last) P.Message(MessageHud.MessageType.TopLeft, $"{e.Title}  {now}/{e.Need}");
                s_lastShown[e.Id] = now;
            }
        }

        // ---- hooks ----

        [HarmonyPatch(typeof(Player), "OnInventoryChanged")]
        static class AnnounceGather
        {
            static void Postfix(Player __instance)
            {
                if (__instance == Player.m_localPlayer) OnInventoryChanged();
            }
        }

        [HarmonyPatch(typeof(Player), "Awake")]
        static class RegisterKillRpc
        {
            static void Postfix(Player __instance)
            {
                var nview = __instance.GetComponent<ZNetView>();
                if (nview == null) return;
                nview.Register<string>(KillRpc, (sender, prefab) =>
                {
                    if (__instance == Player.m_localPlayer) OnKill(prefab);
                });
                nview.Register<string, string>(ShareRpc, (sender, from, line) =>
                {
                    if (__instance == Player.m_localPlayer) OnShared(from, line);
                });
            }
        }

        /// Runs on the creature's owner. A player's kill credits every player near the body.
        [HarmonyPatch(typeof(Character), nameof(Character.OnDeath))]
        static class CreditKill
        {
            static void Prefix(Character __instance, HitData ___m_lastHit)
            {
                if (__instance.IsPlayer() || __instance.IsTamed()) return;
                var nview = __instance.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid() || !nview.IsOwner()) return;
                if (!(___m_lastHit?.GetAttacker() is Player attacker)) return;
                var prefab = Utils.GetPrefabName(__instance.gameObject);
                var credited = NearbyPlayers(__instance.transform.position);
                if (!credited.Contains(attacker)) credited.Add(attacker);
                foreach (var p in credited)
                {
                    var target = p.GetComponent<ZNetView>();
                    if (target && target.IsValid()) target.InvokeRPC(KillRpc, prefab);
                }
            }
        }

        /// Count what the crafting panel actually produced: inventory before vs after, upgrades excluded.
        [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
        static class CountCraft
        {
            static void Prefix(Player player, Recipe ___m_craftRecipe, ItemDrop.ItemData ___m_craftUpgradeItem, out KeyValuePair<string, int> __state)
            {
                __state = default;
                if (___m_craftRecipe == null || ___m_craftUpgradeItem != null || !player) return;
                var name = ___m_craftRecipe.m_item.m_itemData.m_shared.m_name;
                __state = new KeyValuePair<string, int>(name, player.GetInventory().CountItems(name));
            }

            static void Postfix(Player player, KeyValuePair<string, int> __state)
            {
                if (__state.Key == null || !player || player != Player.m_localPlayer) return;
                int made = player.GetInventory().CountItems(__state.Key) - __state.Value;
                if (made > 0) OnCraft(__state.Key, made);
            }
        }
    }
}
