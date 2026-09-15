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
            Part(root, PrimitiveType.Sphere, new Vector3(0f, 0.30f, 0f), new Vector3(0.34f, 0.38f, 0.34f), glass);
            Part(root, PrimitiveType.Sphere, new Vector3(0f, 0.54f, 0f), new Vector3(0.34f, 0.38f, 0.34f), glass);
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
