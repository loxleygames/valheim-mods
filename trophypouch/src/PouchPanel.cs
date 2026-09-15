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
            float height = TrophyPouchPlugin.PanelHeight.Value;
            s_panel = GUIManager.Instance.CreateWoodpanel(gui.m_player, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(240f, 0f), 300f, height, false);
            s_panel.name = "TrophyPouchPanel";
            // Draw before the inventory's own children (tooltip anchor included) so tooltips paint on top of us.
            s_panel.transform.SetAsFirstSibling();
            // Hang it off the inventory's top-right corner so its top is level with the inventory.
            var prt = s_panel.GetComponent<RectTransform>();
            prt.pivot = new Vector2(0f, 1f);
            prt.anchoredPosition = new Vector2(90f, 0f);
            GUIManager.Instance.CreateText("Trophies", s_panel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f),
                GUIManager.Instance.AveriaSerifBold, 20, GUIManager.Instance.ValheimOrange, true, Color.black, 260f, 30f, false);
            var scroll = GUIManager.Instance.CreateScrollView(s_panel.transform, false, true, 8f, 4f, GUIManager.Instance.ValheimScrollbarHandleColorBlock, new Color(0f, 0f, 0f, 0.3f), 270f, height - 60f);
            // Jötunn nests a Canvas in its scroll view; tooltips spawn under the nearest Canvas and get masked. Drop it.
            foreach (var c in scroll.GetComponentsInChildren<GraphicRaycaster>(true)) Object.DestroyImmediate(c);
            foreach (var c in scroll.GetComponentsInChildren<CanvasScaler>(true)) Object.DestroyImmediate(c);
            foreach (var c in scroll.GetComponentsInChildren<Canvas>(true)) Object.DestroyImmediate(c);
            var srt = scroll.GetComponent<RectTransform>();
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 1f);
            srt.pivot = new Vector2(0.5f, 1f);
            srt.anchoredPosition = new Vector2(0f, -44f);
            s_content = scroll.GetComponentInChildren<VerticalLayoutGroup>().transform;
        }

        const int PerRow = 4;

        static void Refresh()
        {
            if (!s_content) return;
            for (int i = s_content.childCount - 1; i >= 0; i--) Object.Destroy(s_content.GetChild(i).gameObject);
            var entries = new List<KeyValuePair<string, int>>(Pouch.All);
            entries.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            if (entries.Count == 0)
            {
                GUIManager.Instance.CreateText("Nothing yet.", s_content, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero,
                    GUIManager.Instance.AveriaSerif, 16, Color.grey, true, Color.black, 240f, 30f, false);
                Pouch.Dirty = false;
                return;
            }
            var grid = InventoryGui.instance.m_playerGrid;
            float space = grid.m_elementSpace;
            Transform row = null;
            for (int i = 0; i < entries.Count; i++)
            {
                if (i % PerRow == 0)
                {
                    var rowGo = new GameObject("row", typeof(RectTransform), typeof(LayoutElement));
                    rowGo.transform.SetParent(s_content, false);
                    rowGo.GetComponent<LayoutElement>().preferredHeight = space;
                    rowGo.GetComponent<RectTransform>().sizeDelta = new Vector2(PerRow * space, space);
                    row = rowGo.transform;
                }
                Slot(row, grid.m_elementPrefab, entries[i].Key, entries[i].Value, (i % PerRow) * space);
            }
            Pouch.Dirty = false;
        }

        /// One of the game's own inventory slots, showing a trophy and how many. Click to take one.
        static void Slot(Transform row, GameObject elementPrefab, string prefab, int count, float x)
        {
            var go = ObjectDB.instance.GetItemPrefab(prefab);
            var drop = go ? go.GetComponent<ItemDrop>() : null;
            if (!drop) return;
            var item = drop.m_itemData;

            var slot = Object.Instantiate(elementPrefab, row);
            slot.name = prefab;
            var rt = slot.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, 0f);

            var el = slot.GetComponent<InventoryElement>();
            if (el)
            {
                el.m_icon.enabled = true;
                el.m_icon.sprite = item.GetIcon();
                el.m_amount.enabled = true;
                el.m_amount.text = count.ToString();
                el.m_quality.enabled = false;
                el.m_equiped.enabled = false;
                el.m_queued.enabled = false;
                el.m_noteleport.enabled = false;
                el.m_food.enabled = false;
                el.m_durability.gameObject.SetActive(false);
                if (el.m_selected) el.m_selected.SetActive(false);
                if (el.m_tooltip)
                {
                    // No anchor: the tooltip pops beside the hovered slot, on the main canvas, above everything.
                    el.m_tooltip.Set(Localization.instance.Localize(item.m_shared.m_name),
                        Localization.instance.Localize(item.m_shared.m_description) + "\n\n<color=orange>" + count + "</color> in pouch. Click to take one.");
                }
            }
            var binding = slot.transform.Find("binding");
            if (binding) binding.gameObject.SetActive(false);

            // The prefab's input handlers expect the grid; a plain button on top does what we need.
            foreach (var h in slot.GetComponentsInChildren<UIInputHandler>(true)) Object.Destroy(h);
            foreach (var d in slot.GetComponentsInChildren<UIDragHandler>(true)) Object.Destroy(d);
            var button = slot.GetComponent<Button>() ?? slot.AddComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => { if (Pouch.Take(prefab)) Refresh(); });
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
