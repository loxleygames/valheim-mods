using UnityEngine;

namespace Moorings
{
    /// Shared state helpers. Both sides of the line are stored in ZDOs so it survives logout and syncs to everyone:
    ///   post ZDO:  Moorings_Ship (ZDOID)
    ///   ship ZDO:  Moorings_Post (ZDOID), Moorings_Point (Vector3 of the post)
    public static class Mooring
    {
        public const string PostShipKey = "Moorings_Ship";
        public const string ShipPostKey = "Moorings_Post";
        public static readonly int ShipPointHash = "Moorings_Point".GetStableHashCode();

        public static bool IsMoored(ZDO shipZdo) => shipZdo != null && shipZdo.GetZDOID(ShipPostKey) != ZDOID.None;

        public static bool IsMoored(Ship ship)
        {
            var nview = ship ? ship.GetComponent<ZNetView>() : null;
            return nview && nview.IsValid() && IsMoored(nview.GetZDO());
        }

        public static void Tie(ZNetView post, ZNetView ship)
        {
            post.ClaimOwnership();
            ship.ClaimOwnership();
            post.GetZDO().Set(PostShipKey, ship.GetZDO().m_uid);
            ship.GetZDO().Set(ShipPostKey, post.GetZDO().m_uid);
            ship.GetZDO().Set(ShipPointHash, post.transform.position);
        }

        public static void Release(ZNetView ship)
        {
            if (!ship || !ship.IsValid()) return;
            var shipZdo = ship.GetZDO();
            var postId = shipZdo.GetZDOID(ShipPostKey);
            ship.ClaimOwnership();
            shipZdo.Set(ShipPostKey, ZDOID.None);

            var post = ZNetScene.instance.FindInstance(postId);
            var postView = post ? post.GetComponent<ZNetView>() : null;
            if (postView && postView.IsValid())
            {
                postView.ClaimOwnership();
                postView.GetZDO().Set(PostShipKey, ZDOID.None);
            }
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
