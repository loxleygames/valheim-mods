using System.Text;
using HarmonyLib;
using UnityEngine;

namespace InventoryReforged
{
    /// Logs the player-panel hierarchy once, so placements can be done against real numbers.
    [HarmonyPatch(typeof(InventoryGui), "Show", typeof(Container), typeof(int))]
    static class LayoutDump
    {
        static bool s_done;
        static void Postfix(InventoryGui __instance)
        {
            if (s_done || !Plugin.DebugLayout.Value) return;
            s_done = true;
            var sb = new StringBuilder("[IR layout]\n");
            Dump(__instance.m_player, 0, sb);
            Dump(__instance.m_container, 0, sb);
            Debug.Log(sb.ToString());
        }

        static void Dump(Transform t, int depth, StringBuilder sb)
        {
            if (depth > 3) return;
            var rt = t as RectTransform;
            sb.Append(' ', depth * 2).Append(t.name);
            if (rt) sb.Append($"  pos={rt.anchoredPosition} size={rt.sizeDelta} rect={rt.rect.size} aMin={rt.anchorMin} aMax={rt.anchorMax} pivot={rt.pivot} active={t.gameObject.activeSelf}");
            foreach (var c in t.GetComponents<Component>()) if (c && !(c is Transform)) sb.Append(" [").Append(c.GetType().Name).Append(']');
            sb.Append('\n');
            for (int i = 0; i < t.childCount; i++) Dump(t.GetChild(i), depth + 1, sb);
        }
    }
}
