using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace QuickStash
{
    /// A Stash button on the player's inventory panel, cloned from the chest's Take All button.
    [HarmonyPatch(typeof(InventoryGui), "Awake")]
    static class InventoryGui_Awake_Patch
    {
        static void Postfix(InventoryGui __instance)
        {
            if (!QuickStashPlugin.ShowButton.Value || !__instance.m_takeAllButton) return;

            var button = Object.Instantiate(__instance.m_takeAllButton, __instance.m_player);
            button.name = "QuickStashButton";
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => { if (Player.m_localPlayer) Stash.Run(Player.m_localPlayer); });

            var tmp = button.GetComponentInChildren<TMPro.TMP_Text>();
            if (tmp) tmp.text = "Stash";
            var legacy = button.GetComponentInChildren<Text>();
            if (legacy) legacy.text = "Stash";

            // Top-right corner of the player panel, tucked in beside the weight/armour readout.
            var rt = button.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(QuickStashPlugin.ButtonX.Value, QuickStashPlugin.ButtonY.Value);
        }
    }
}
