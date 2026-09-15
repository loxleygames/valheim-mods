using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Hourglass
{
    /// A buildable hourglass. Use it and the sun stops at noon for everyone until someone uses it again.
    ///
    /// The world clock keeps running underneath (days still pass); only the time of day each client
    /// sees is pinned, the same way the game's own `tod` debug command does it. That also means no
    /// night spawns while it's held. State is a vanilla global key, so it syncs through the server
    /// without the server needing the mod.
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    public class HourglassPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "games.loxley.hourglass";
        public const string PluginName = "Hourglass";
        public const string PluginVersion = "0.1.0";

        public const string PrefabName = "LoxleyHourglass";
        public const string Key = "loxley_hourglass";

        public static ConfigEntry<float> TimeOfDay;

        private void Awake()
        {
            TimeOfDay = Config.Bind("General", "TimeOfDay", 0.5f,
                "Where the sun is held. 0 = midnight, 0.25 = dawn, 0.5 = noon, 0.75 = dusk.");

            PrefabManager.OnVanillaPrefabsAvailable += AddPiece;
            new Harmony(PluginGUID).PatchAll();
        }

        private void AddPiece()
        {
            // The Ward is a small runed stone that glows: the closest vanilla shape to a time-magic object.
            var prefab = PrefabManager.Instance.CreateClonedPrefab(PrefabName, "guard_stone");
            foreach (var area in prefab.GetComponentsInChildren<PrivateArea>(true)) Object.DestroyImmediate(area);
            prefab.AddComponent<HourglassPiece>();
            HourglassModel.Build(prefab);

            // Build-menu icon: hourglass_icon.png beside the DLL, else keep the Ward's.
            var iconPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Info.Location), "hourglass_icon.png");
            var icon = System.IO.File.Exists(iconPath) ? Jotunn.Utils.AssetUtils.LoadSpriteFromFile(iconPath) : null;

            PieceManager.Instance.AddPiece(new CustomPiece(prefab, fixReference: true, new PieceConfig
            {
                Name = "Hourglass",
                Icon = icon,
                Description = "Holds the sun at noon for everyone until used again. Good for building.",
                PieceTable = PieceTables.Hammer,
                Category = PieceCategories.Misc,
                CraftingStation = CraftingStations.Workbench,
                Requirements = new[]
                {
                    new RequirementConfig("FineWood", 6, 0, true),
                    new RequirementConfig("Resin", 4, 0, true),
                    new RequirementConfig("GreydwarfEye", 2, 0, true),
                }
            }));

            PrefabManager.OnVanillaPrefabsAvailable -= AddPiece;
        }

        public static bool IsHeld => ZoneSystem.instance && ZoneSystem.instance.GetGlobalKey(Key);
    }

    /// An hourglass built from primitives, borrowing the Ward's stone material so it lights like everything else.
    public static class HourglassModel
    {
        public static void Build(GameObject prefab)
        {
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (var l in prefab.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l);
            foreach (var ps in prefab.GetComponentsInChildren<ParticleSystem>(true)) Object.DestroyImmediate(ps.gameObject);

            var wood = MaterialOf("wood_pole2") ?? MaterialOf("guard_stone");
            var glass = MaterialOf("crystal_wall_1x1") ?? wood;
            var sand = MaterialOf("piece_beehive") ?? wood;

            var root = new GameObject("hourglass");
            root.transform.SetParent(prefab.transform, false);
            root.layer = prefab.layer;

            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0f), new Vector3(0.54f, 0.04f, 0.54f), wood);
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.80f, 0f), new Vector3(0.54f, 0.04f, 0.54f), wood);
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.845f, 0f), new Vector3(0.16f, 0.015f, 0.16f), wood);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f;
                Part(root, PrimitiveType.Cylinder, new Vector3(Mathf.Cos(a) * 0.21f, 0.42f, Mathf.Sin(a) * 0.21f), new Vector3(0.05f, 0.34f, 0.05f), wood);
            }
            Part(root, PrimitiveType.Sphere, new Vector3(0f, 0.21f, 0f), new Vector3(0.24f, 0.16f, 0.24f), sand);
            // One vessel: a pinched profile revolved round the axis.
            Lathe(root, "glass", glass, new[]
            {
                (0.08f, 0.10f), (0.12f, 0.15f), (0.20f, 0.175f), (0.30f, 0.14f), (0.38f, 0.06f),
                (0.42f, 0.03f),
                (0.46f, 0.06f), (0.54f, 0.14f), (0.64f, 0.175f), (0.72f, 0.15f), (0.76f, 0.10f),
            });
        }

        static string Describe(Material m) => m ? $"{m.name}/{m.shader.name}/tex={(m.mainTexture ? m.mainTexture.name : "none")}" : "null";

        /// A textured material from a vanilla prefab, skipping the snow-cover mesh every piece carries.
        static Material MaterialOf(string prefabName)
        {
            var prefab = PrefabManager.Instance.GetPrefab(prefabName);
            if (!prefab) return null;
            Material fallback = null;
            foreach (var r in prefab.GetComponentsInChildren<MeshRenderer>(true))
                foreach (var m in r.sharedMaterials)
                {
                    if (!m || m.shader.name.Contains("Snow") || m.name.ToLower().Contains("snow")) continue;
                    if (m.mainTexture) return m;
                    fallback = fallback ? fallback : m;
                }
            return fallback;
        }

        /// Revolve a (height, radius) profile into a closed mesh. Smoothed with Catmull-Rom so the
        /// hand-placed points become a curve.
        static void Lathe(GameObject parent, string name, Material mat, (float y, float r)[] profile)
        {
            const int segments = 28, perSpan = 4;
            var pts = new System.Collections.Generic.List<Vector2>();
            for (int i = 0; i < profile.Length - 1; i++)
            {
                Vector2 P(int k) { k = Mathf.Clamp(k, 0, profile.Length - 1); return new Vector2(profile[k].r, profile[k].y); }
                Vector2 p0 = P(i - 1), p1 = P(i), p2 = P(i + 1), p3 = P(i + 2);
                for (int j = 0; j < perSpan; j++)
                {
                    float t = j / (float)perSpan, t2 = t * t, t3 = t2 * t;
                    pts.Add(0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            pts.Add(new Vector2(profile[profile.Length - 1].r, profile[profile.Length - 1].y));

            int rings = pts.Count;
            var verts = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var tris = new System.Collections.Generic.List<int>();
            for (int ring = 0; ring < rings; ring++)
                for (int sgm = 0; sgm <= segments; sgm++)
                {
                    float a = sgm / (float)segments * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(a) * pts[ring].x, pts[ring].y, Mathf.Sin(a) * pts[ring].x));
                    uvs.Add(new Vector2(sgm / (float)segments, ring / (float)(rings - 1)));
                }
            int stride = segments + 1;
            for (int ring = 0; ring < rings - 1; ring++)
                for (int sgm = 0; sgm < segments; sgm++)
                {
                    int a = ring * stride + sgm, b = a + 1, c = a + stride, d = c + 1;
                    tris.AddRange(new[] { a, c, b, b, c, d });
                }
            // Flat caps top and bottom.
            foreach (int ring in new[] { 0, rings - 1 })
            {
                int centre = verts.Count;
                verts.Add(new Vector3(0f, pts[ring].y, 0f));
                uvs.Add(new Vector2(0.5f, 0.5f));
                for (int sgm = 0; sgm < segments; sgm++)
                {
                    int a = ring * stride + sgm, b = a + 1;
                    if (ring == 0) tris.AddRange(new[] { centre, b, a }); else tris.AddRange(new[] { centre, a, b });
                }
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject(name);
            go.layer = parent.layer;
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            if (mat) mr.sharedMaterial = mat;
        }

        static void Part(GameObject parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = type.ToString();
            go.layer = parent.layer;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (mat) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }

    public class HourglassPiece : MonoBehaviour, Interactable, Hoverable
    {
        static readonly System.Collections.Generic.List<HourglassPiece> All = new System.Collections.Generic.List<HourglassPiece>();

        private void Awake()
        {
            All.Add(this);
            var wnt = GetComponent<WearNTear>();
            if (wnt) wnt.m_onDestroyed += OnDestroyed;
        }

        private void OnDestroy() => All.Remove(this);

        /// Smashed or picked up: if it was the last one around, time flows again.
        private void OnDestroyed()
        {
            All.Remove(this);
            if (All.Count == 0 && HourglassPlugin.IsHeld)
            {
                ZoneSystem.instance.RemoveGlobalKey(HourglassPlugin.Key);
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "Time flows again");
            }
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold) return false;
            if (HourglassPlugin.IsHeld)
            {
                ZoneSystem.instance.RemoveGlobalKey(HourglassPlugin.Key);
                user.Message(MessageHud.MessageType.Center, "Time flows again");
            }
            else
            {
                ZoneSystem.instance.SetGlobalKey(HourglassPlugin.Key);
                user.Message(MessageHud.MessageType.Center, "Time stands still");
            }
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;
        public string GetHoverName() => "Hourglass";
        public float GetHoverOffset() => 0f;

        public string GetHoverText()
        {
            string action = HourglassPlugin.IsHeld ? "Let time flow" : "Stop time";
            return Localization.instance.Localize($"Hourglass\n[<color=yellow><b>$KEY_Use</b></color>] {action}");
        }
    }

    /// Pin the day fraction while the key is set; hand control back when it's cleared.
    [HarmonyPatch(typeof(EnvMan), "FixedUpdate")]
    static class HoldTime
    {
        static bool s_wasHeld;

        static void Prefix(EnvMan __instance)
        {
            bool held = HourglassPlugin.IsHeld;
            if (held)
            {
                __instance.m_debugTimeOfDay = true;
                __instance.m_debugTime = HourglassPlugin.TimeOfDay.Value;
            }
            else if (s_wasHeld)
            {
                __instance.m_debugTimeOfDay = false;
            }
            s_wasHeld = held;
        }
    }
}
