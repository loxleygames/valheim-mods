using System.Collections.Generic;
using UnityEngine;

namespace Moorings
{
    /// Post and boat are linked by a shared random hash, not by ZDOID: ZDOIDs are reassigned when a
    /// world loads, so a stored ID goes stale. Vanilla relinks portals and spawners by hash the same way.
    ///   post ZDO:  Moorings_Link (int, 0 = nothing tied)
    ///   ship ZDO:  Moorings_Link (int), Moorings_Point (Vector3 of the post)
    public static class Mooring
    {
        public static readonly int LinkHash = "Moorings_Link".GetStableHashCode();
        public static readonly int PointHash = "Moorings_Point".GetStableHashCode();

        public static int LinkOf(ZNetView view) => view && view.IsValid() ? view.GetZDO().GetInt(LinkHash, 0) : 0;

        public static bool IsMoored(ZDO shipZdo) => shipZdo != null && shipZdo.GetInt(LinkHash, 0) != 0;

        public static bool IsMoored(Ship ship) => ship && IsMoored(ship.GetComponent<ZNetView>()?.GetZDO());

        public static void Tie(ZNetView post, ZNetView ship)
        {
            int link = Random.Range(1, int.MaxValue);
            post.ClaimOwnership();
            ship.ClaimOwnership();
            post.GetZDO().Set(LinkHash, link);
            ship.GetZDO().Set(LinkHash, link);
            ship.GetZDO().Set(PointHash, post.transform.position);
        }

        public static void Release(ZNetView ship)
        {
            if (!ship || !ship.IsValid()) return;
            int link = LinkOf(ship);
            ship.ClaimOwnership();
            ship.GetZDO().Set(LinkHash, 0);
            var post = FindPost(link);
            if (post)
            {
                var view = post.GetComponent<ZNetView>();
                view.ClaimOwnership();
                view.GetZDO().Set(LinkHash, 0);
            }
        }

        public static Ship FindShip(int link)
        {
            if (link == 0) return null;
            foreach (var updater in Ship.Instances)
                if (updater is Ship ship && LinkOf(ship.GetComponent<ZNetView>()) == link) return ship;
            return null;
        }

        public static MooringPost FindPost(int link)
        {
            if (link == 0) return null;
            foreach (var post in MooringPost.All)
                if (post && LinkOf(post.GetComponent<ZNetView>()) == link) return post;
            return null;
        }

        public static Ship FindNearestShip(Vector3 pos, float range)
        {
            Ship best = null;
            float bestDist = range;
            foreach (var updater in Ship.Instances)
            {
                if (!(updater is Ship ship)) continue;
                float d = Vector3.Distance(ship.transform.position, pos);
                if (d < bestDist) { best = ship; bestDist = d; }
            }
            return best;
        }
    }
}
