using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Errands
{
    /// Base for the two pop-ups. While one is open, StoreGui reports itself visible: that one flag is what
    /// vanilla checks to free the cursor, stop the player moving and keep Escape off the pause menu.
    public abstract class Popup : MonoBehaviour
    {
        static readonly List<Popup> s_open = new List<Popup>();
        public static bool AnyOpen => s_open.Count > 0;

        protected readonly List<GameObject> m_rows = new List<GameObject>();

        protected void Show() { if (!s_open.Contains(this)) s_open.Add(this); gameObject.SetActive(true); }
        public void Hide() { s_open.Remove(this); gameObject.SetActive(false); }
        public static void CloseAll() { foreach (var p in s_open.ToArray()) p.Hide(); }

        void Update()
        {
            if (ZInput.GetKeyDown(KeyCode.Escape)) OnEscape();
            Tick();
        }

        protected virtual void OnEscape() => Hide();
        protected virtual void Tick() { }

        protected void Clear()
        {
            foreach (var r in m_rows) Destroy(r);
            m_rows.Clear();
        }

        protected static readonly Vector2 Top = new Vector2(0.5f, 1f), Bottom = new Vector2(0.5f, 0f);

        protected GameObject Text(string s, float x, float y, int size, Color colour, bool bold, float width, TextAnchor align, Vector2? anchor = null)
        {
            var a = anchor ?? Top;
            var go = GUIManager.Instance.CreateText(s, transform, a, a, new Vector2(x, y),
                bold ? GUIManager.Instance.AveriaSerifBold : GUIManager.Instance.AveriaSerif, size, colour, true, Color.black, width, 36f, false);
            var t = go.GetComponent<Text>();
            if (t) t.alignment = align;
            m_rows.Add(go);
            return go;
        }

        protected Button Button(string label, float x, float y, float w, float h, Vector2? anchor = null)
        {
            var a = anchor ?? Top;
            var go = GUIManager.Instance.CreateButton(label, transform, a, a, new Vector2(x, y), w, h);
            m_rows.Add(go);
            return go.GetComponent<Button>();
        }

        protected static T Make<T>(string name, float w, float h, Vector2 anchor, Vector2 pos) where T : Popup
        {
            var go = GUIManager.Instance.CreateWoodpanel(GUIManager.CustomGUIFront.transform, anchor, anchor, pos, w, h, false);
            go.name = name;
            go.SetActive(false);
            return go.AddComponent<T>();
        }
    }

    /// The dvergr's offer: one errand, take it or leave it.
    public class OfferDialog : Popup
    {
        static OfferDialog s_instance;
        const float W = 520f, H = 160f;
        Dvergr m_dvergr;

        public static void Show(Dvergr dvergr, Errand offer)
        {
            if (!s_instance) s_instance = Make<OfferDialog>("ErrandsOffer", W, H, new Vector2(0.5f, 0f), new Vector2(0f, 190f));
            s_instance.Fill(dvergr, offer);
            s_instance.Show();
        }

        void Fill(Dvergr dvergr, Errand offer)
        {
            Clear();
            m_dvergr = dvergr;
            Text(dvergr.Name, 0f, -20f, 18, GUIManager.Instance.ValheimOrange, true, W - 40f, TextAnchor.MiddleCenter);
            Text(JournalPanel.StandingLine(dvergr.Biome), 0f, -42f, 13, new Color(0.85f, 0.85f, 0.85f), false, W - 40f, TextAnchor.MiddleCenter);
            Text(offer.Title + Stars(offer.Tier), 0f, -68f, 20, Color.white, false, W - 40f, TextAnchor.MiddleCenter);
            var accept = Button("Accept", -150f, -118f, 130f, 40f);
            accept.onClick.AddListener(() => { Journal.Accept(offer); Hide(); });
            var decline = Button("Decline", 0f, -118f, 130f, 40f);
            decline.onClick.AddListener(Hide);
            var wares = Button("Wares", 150f, -118f, 130f, 40f);
            wares.onClick.AddListener(dvergr.OpenWares);
        }

        public static string Stars(int tier) => "  <color=#f0c040>" + new string('★', tier) + "</color>";
    }

    /// The book (journal key): what you hold, and a way to drop one.
    public class JournalPanel : Popup
    {
        static JournalPanel s_instance;
        const float W = 560f, Row = 44f;

        public static bool IsOpen => s_instance && s_instance.gameObject.activeSelf;

        /// "Standing: Meadows - 80 (Kin)", tier in its colour.
        public static string StandingLine(Heightmap.Biome b)
        {
            int rep = Journal.Rep(b);
            return $"Standing: {Biomes.Label(b)} - {rep} ({Shop.TierTag(rep)})";
        }

        public static void Toggle()
        {
            if (IsOpen) { s_instance.Hide(); return; }
            float h = 170f + Row * Mathf.Max(1, ErrandsPlugin.MaxErrands.Value);
            if (!s_instance) s_instance = Make<JournalPanel>("ErrandsJournal", W, h, new Vector2(0.5f, 0.5f), Vector2.zero);
            s_instance.Fill();
            s_instance.Show();
        }

        void Fill()
        {
            Clear();
            var active = Journal.Active();
            Text($"Errands  {active.Count}/{ErrandsPlugin.MaxErrands.Value}", 0f, -24f, 22, GUIManager.Instance.ValheimOrange, true, W - 40f, TextAnchor.MiddleCenter);
            float y = -68f;
            if (active.Count == 0)
            {
                Text("No errands. The dvergr keep to the ruins.", 0f, y, 16, Color.grey, false, W - 40f, TextAnchor.MiddleCenter);
                y -= Row;
            }
            foreach (var e in active)
            {
                bool done = Journal.Complete(e);
                Text($"{e.Title}{OfferDialog.Stars(e.Tier)}   {Journal.Progress(e)}/{e.Need}   ({Biomes.Label(e.Biome)})",
                    -45f, y, 17, done ? new Color(0.6f, 1f, 0.6f) : Color.white, false, W - 150f, TextAnchor.MiddleLeft);
                var drop = Button("Drop", W / 2f - 60f, y, 70f, 34f);
                var captured = e;
                drop.onClick.AddListener(() => { Journal.Abandon(captured); Fill(); });
                y -= Row;
            }
            // Standing and Close sit on the bottom edge; the list grows down toward them.
            var here = Player.m_localPlayer.GetCurrentBiome();
            if (System.Array.IndexOf(Biomes.All, here) >= 0)
                Text(StandingLine(here), 0f, 72f, 15, new Color(0.85f, 0.85f, 0.85f), false, W - 40f, TextAnchor.MiddleCenter, Bottom);
            var close = Button("Close", 0f, 34f, 140f, 40f, Bottom);
            close.onClick.AddListener(Hide);
        }
    }

    [HarmonyPatch(typeof(StoreGui), nameof(StoreGui.IsVisible))]
    static class StoreGui_IsVisible_Patch
    {
        static void Postfix(ref bool __result)
        {
            if (Popup.AnyOpen) __result = true;
        }
    }

    public class JournalKey : MonoBehaviour
    {
        void Update()
        {
            if (!Player.m_localPlayer) return;
            if (Console.IsVisible() || Menu.IsVisible() || InventoryGui.IsVisible() || TextInput.IsVisible() || (Chat.instance && Chat.instance.HasFocus())) return;
            if (ZInput.GetKeyDown(ErrandsPlugin.JournalKeyCode)) JournalPanel.Toggle();
        }
    }
}
