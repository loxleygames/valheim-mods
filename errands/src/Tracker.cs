using System.Text;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Errands
{
    [HarmonyLib.HarmonyPatch(typeof(Hud), "Awake")]
    static class Hud_Awake_Patch
    {
        static void Postfix() => Tracker.Ensure();
    }

    /// The objective tracker: a right-aligned list under the minimap whenever you hold an errand.
    public class Tracker : MonoBehaviour
    {
        static Tracker s_instance;
        Text m_text;
        float m_next;

        public static void Ensure()
        {
            if (s_instance || !Hud.instance || GUIManager.Instance == null) return;
            var root = Hud.instance.m_rootObject.transform;
            var go = GUIManager.Instance.CreateText("", root, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-ErrandsPlugin.TrackerX.Value, -ErrandsPlugin.TrackerY.Value),
                GUIManager.Instance.AveriaSerif, 17, Color.white, true, Color.black, 340f, 400f, false);
            go.name = "ErrandsTracker";
            var rt = go.GetComponent<RectTransform>();
            rt.pivot = new Vector2(1f, 1f);
            s_instance = go.AddComponent<Tracker>();
            s_instance.m_text = go.GetComponent<Text>();
            s_instance.m_text.alignment = TextAnchor.UpperRight;
            s_instance.m_text.lineSpacing = 1.15f;
        }

        void Update()
        {
            if (Time.time < m_next) return;
            m_next = Time.time + 0.5f;
            if (!Player.m_localPlayer || !ErrandsPlugin.ShowTracker.Value) { m_text.text = ""; return; }

            var active = Journal.Active();
            if (active.Count == 0) { m_text.text = ""; return; }

            var sb = new StringBuilder();
            sb.Append("<color=#ffb95a><b>Errands</b></color>\n");
            foreach (var e in active)
            {
                int have = Journal.Progress(e);
                bool done = have >= e.Need;
                if (done) sb.Append($"<color=#9ef09e>{e.Title}  done, any {Biomes.Label(e.Biome)} dvergr</color>\n");
                else sb.Append($"{e.Title}  <color=#dddddd>{have}/{e.Need}</color>\n");
            }
            m_text.text = sb.ToString();
        }
    }
}
