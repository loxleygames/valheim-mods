using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace TrophyPouch
{
    /// A button beside the inventory opens a list of what's in the pouch; each row has a Take button.
    public static class PouchPanel
    {
        static GameObject s_panel;
        static Transform s_content;
        static Button s_button;
        static bool s_open;

        [HarmonyPatch(typeof(InventoryGui), "Awake")]
        static class MakeButton
        {
            static void Postfix(InventoryGui __instance)
            {
                if (!__instance.m_takeAllButton) return;
                s_button = Object.Instantiate(__instance.m_takeAllButton, __instance.m_player);
                s_button.name = "TrophyPouchButton";
                s_button.onClick = new Button.ButtonClickedEvent();
                s_button.onClick.AddListener(Toggle);
                var tmp = s_button.GetComponentInChildren<TMPro.TMP_Text>();
                if (tmp) { tmp.text = "T"; tmp.alignment = TMPro.TextAlignmentOptions.Center; tmp.enableAutoSizing = false; tmp.fontSize = 20f; }
                var rt = s_button.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(38f, 38f);
                rt.anchoredPosition = new Vector2(TrophyPouchPlugin.ButtonX.Value, TrophyPouchPlugin.ButtonY.Value);
                var tip = s_button.GetComponent<UITooltip>();
                if (tip) { tip.m_text = "Trophy pouch"; tip.m_topic = ""; }
                s_panel = null;
                s_open = false;
            }
        }

        static void Toggle()
        {
            s_open = !s_open;
            if (s_open) { Build(); Refresh(); }
            if (s_panel) s_panel.SetActive(s_open);
        }

        static void Build()
        {
            if (s_panel) return;
            var gui = InventoryGui.instance;
            s_panel = GUIManager.Instance.CreateWoodpanel(gui.m_player, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(240f, 0f), 300f, gui.m_player.rect.height, false);
            s_panel.name = "TrophyPouchPanel";
            // Hang it off the inventory's top-right corner so its top is level with the inventory.
            var prt = s_panel.GetComponent<RectTransform>();
            prt.pivot = new Vector2(0f, 1f);
            prt.anchoredPosition = new Vector2(90f, 0f);
            GUIManager.Instance.CreateText("Trophies", s_panel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f),
                GUIManager.Instance.AveriaSerifBold, 20, GUIManager.Instance.ValheimOrange, true, Color.black, 260f, 30f, false);
            var scroll = GUIManager.Instance.CreateScrollView(s_panel.transform, false, true, 8f, 4f, GUIManager.Instance.ValheimScrollbarHandleColorBlock, new Color(0f, 0f, 0f, 0.3f), 270f, gui.m_player.rect.height - 60f);
            var srt = scroll.GetComponent<RectTransform>();
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 1f);
            srt.pivot = new Vector2(0.5f, 1f);
            srt.anchoredPosition = new Vector2(0f, -44f);
            s_content = scroll.GetComponentInChildren<VerticalLayoutGroup>().transform;
        }

        static void Refresh()
        {
            if (!s_content) return;
            for (int i = s_content.childCount - 1; i >= 0; i--) Object.Destroy(s_content.GetChild(i).gameObject);
            var rows = new List<KeyValuePair<string, int>>(Pouch.All);
            rows.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            foreach (var kv in rows) Row(kv.Key, kv.Value);
            if (rows.Count == 0)
                GUIManager.Instance.CreateText("Nothing yet.", s_content, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero,
                    GUIManager.Instance.AveriaSerif, 16, Color.grey, true, Color.black, 240f, 30f, false);
            Pouch.Dirty = false;
        }

        static void Row(string prefab, int count)
        {
            var go = ObjectDB.instance.GetItemPrefab(prefab);
            var drop = go ? go.GetComponent<ItemDrop>() : null;
            if (!drop) return;

            var row = new GameObject(prefab, typeof(RectTransform), typeof(LayoutElement));
            row.transform.SetParent(s_content, false);
            row.GetComponent<LayoutElement>().preferredHeight = 40f;
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(250f, 40f);

            var iconGo = new GameObject("icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(row.transform, false);
            var irt = iconGo.GetComponent<RectTransform>();
            irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0f, 0.5f);
            irt.anchoredPosition = new Vector2(4f, 0f);
            irt.sizeDelta = new Vector2(32f, 32f);
            iconGo.GetComponent<Image>().sprite = drop.m_itemData.GetIcon();

            string label = Localization.instance.Localize(drop.m_itemData.m_shared.m_name) + "  ×" + count;
            GUIManager.Instance.CreateText(label, row.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(44f, 0f),
                GUIManager.Instance.AveriaSerif, 15, Color.white, true, Color.black, 140f, 36f, false)
                .GetComponent<RectTransform>().pivot = new Vector2(0f, 0.5f);

            var take = GUIManager.Instance.CreateButton("Take", row.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-4f, 0f), 56f, 30f);
            take.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
            take.GetComponent<Button>().onClick.AddListener(() => { Pouch.Take(prefab); Refresh(); });
        }

        /// Keep the list current while it's open, and drop it when the inventory closes.
        [HarmonyPatch(typeof(InventoryGui), "Update")]
        static class Tick
        {
            static void Postfix()
            {
                if (!s_panel) return;
                bool visible = InventoryGui.IsVisible();
                if (!visible && s_open) { s_open = false; s_panel.SetActive(false); }
                if (s_open && Pouch.Dirty) Refresh();
            }
        }
    }
}
