using System.Collections.Generic;
using UnityEngine;

namespace Moorings
{
    public class MooringPost : MonoBehaviour, Interactable, Hoverable
    {
        public static readonly List<MooringPost> All = new List<MooringPost>();

        private ZNetView m_nview;
        private LineRenderer m_line;

        private void Awake()
        {
            m_nview = GetComponent<ZNetView>();
            var wnt = GetComponent<WearNTear>();
            if (wnt) wnt.m_onDestroyed += OnPostDestroyed;
            All.Add(this);
            MakeCoil();
        }

        /// World-space bounds of the post's mesh: correct whichever axis the mesh was modelled along.
        private Bounds WorldBounds()
        {
            var r = GetComponentInChildren<MeshRenderer>();
            if (r) return r.bounds;
            return new Bounds(transform.position + Vector3.up, new Vector3(0.5f, 2f, 0.5f));
        }

        private float PostRadius() { var b = WorldBounds(); return Mathf.Min(b.extents.x, b.extents.z); }

        public float TieHeight() =>
            MooringsPlugin.LineHeight.Value >= 0f ? MooringsPlugin.LineHeight.Value : WorldBounds().max.y - transform.position.y - 0.4f;

        /// A few turns of rope around the post where the line ties on, drawn as a helix.
        private void MakeCoil()
        {
            var go = new GameObject("coil");
            go.transform.SetParent(transform, false);
            var coil = go.AddComponent<LineRenderer>();
            coil.useWorldSpace = false;
            coil.loop = false;
            coil.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var renderer = GetComponentInChildren<MeshRenderer>();
            if (renderer) coil.material = renderer.sharedMaterial;

            const int turns = 4, perTurn = 32;
            float r = MooringsPlugin.CoilRadius.Value > 0f ? MooringsPlugin.CoilRadius.Value : PostRadius() + 0.02f;
            float h = TieHeight();
            float pitch = 0.055f;
            int count = turns * perTurn + 1;
            float ease = perTurn / 4f; // points over which each end tucks into the wood
            coil.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)perTurn * Mathf.PI * 2f;
                float y = h - (turns * pitch) / 2f + (i / (float)perTurn) * pitch;
                // Ends spiral inward so the rope disappears into the post rather than stopping in the air.
                float edge = Mathf.Min(i, count - 1 - i) / ease;
                float tuck = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge));
                float rr = r - tuck * 0.05f;
                coil.SetPosition(i, new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr));
            }
            // And thin out over the same stretch.
            float edgeT = ease / (count - 1);
            coil.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(edgeT, 1f), new Keyframe(1f - edgeT, 1f), new Keyframe(1f, 0f));
            coil.widthMultiplier = 0.05f;
        }

        private void OnDestroy() => All.Remove(this);

        private void OnPostDestroyed()
        {
            var ship = GetMooredShip();
            if (ship) Mooring.Release(ship.GetComponent<ZNetView>());
        }

        private Ship GetMooredShip() => Mooring.FindShip(Mooring.LinkOf(m_nview));

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || !m_nview.IsValid()) return false;

            var moored = GetMooredShip();
            if (moored)
            {
                Mooring.Release(moored.GetComponent<ZNetView>());
                user.Message(MessageHud.MessageType.Center, "Cast off");
                return true;
            }
            if (Mooring.LinkOf(m_nview) != 0)
            {
                // Tied to a boat that isn't loaded (or is gone). Let go of the line.
                m_nview.ClaimOwnership();
                m_nview.GetZDO().Set(Mooring.LinkHash, 0);
            }

            var ship = Mooring.FindNearestShip(transform.position, MooringsPlugin.MoorRange.Value);
            if (!ship)
            {
                user.Message(MessageHud.MessageType.Center, "No boat close enough");
                return false;
            }
            if (Mooring.IsMoored(ship))
            {
                user.Message(MessageHud.MessageType.Center, "That boat is already moored");
                return false;
            }
            Mooring.Tie(m_nview, ship.GetComponent<ZNetView>());
            user.Message(MessageHud.MessageType.Center, "Moored");
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

        public string GetHoverName() => "Mooring post";

        public float GetHoverOffset() => 0f;

        public string GetHoverText()
        {
            var ship = GetMooredShip();
            string action = ship ? "Cast off" : "Moor nearest boat";
            return Localization.instance.Localize($"Mooring post\n[<color=yellow><b>$KEY_Use</b></color>] {action}");
        }

        // Rope from the post to the boat, for everyone who can see it.
        private void Update()
        {
            var ship = GetMooredShip();
            if (!ship)
            {
                if (m_line) m_line.enabled = false;
                return;
            }
            if (!m_line) m_line = MakeLine();
            m_line.enabled = true;
            m_line.SetPosition(0, transform.position + Vector3.up * TieHeight());
            m_line.SetPosition(1, ship.transform.position + Vector3.up * 0.5f);
        }

        private LineRenderer MakeLine()
        {
            var line = gameObject.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = 0.05f;
            line.endWidth = 0.05f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Borrow the pole's own wood material — reads as hemp rope at this width and needs no shader lookup.
            var renderer = GetComponentInChildren<MeshRenderer>();
            if (renderer) line.material = renderer.sharedMaterial;
            return line;
        }
    }
}
