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
            m_line.SetPosition(0, transform.position + Vector3.up * MooringsPlugin.LineHeight.Value);
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
