using HarmonyLib;
using UnityEngine;

namespace Moorings
{
    /// A moored boat takes no damage from anything: collisions, wave slams, Ashlands, rain wear.
    [HarmonyPatch(typeof(WearNTear), "RPC_Damage")]
    static class WearNTear_RPC_Damage_Patch
    {
        static bool Prefix(WearNTear __instance)
        {
            var ship = __instance.GetComponent<Ship>();
            return !(ship && Mooring.IsMoored(ship));
        }
    }

    /// Rain/support wear goes through ApplyDamage directly without an RPC.
    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.ApplyDamage))]
    static class WearNTear_ApplyDamage_Patch
    {
        static bool Prefix(WearNTear __instance, ref bool __result)
        {
            var ship = __instance.GetComponent<Ship>();
            if (ship && Mooring.IsMoored(ship)) { __result = false; return false; }
            return true;
        }
    }

    /// Keep the boat near the post. It still rides the waves, it just can't drift off.
    [HarmonyPatch(typeof(Ship), nameof(Ship.CustomFixedUpdate))]
    static class Ship_CustomFixedUpdate_Patch
    {
        static void Postfix(Ship __instance, float fixedDeltaTime)
        {
            if (!__instance.IsOwner()) return;
            var nview = __instance.GetComponent<ZNetView>();
            if (!nview || !nview.IsValid()) return;
            var zdo = nview.GetZDO();
            if (!Mooring.IsMoored(zdo)) return;

            // Post gone from the world (destroyed while we were away)? Let the boat go.
            if (ZDOMan.instance.GetZDO(zdo.GetZDOID(Mooring.ShipPostKey)) == null)
            {
                Mooring.Release(nview);
                return;
            }

            var body = __instance.GetComponent<Rigidbody>();
            if (!body) return;
            Vector3 toPost = zdo.GetVec3(Mooring.ShipPointHash, __instance.transform.position) - __instance.transform.position;
            toPost.y = 0f;
            float dist = toPost.magnitude;
            float slack = MooringsPlugin.Slack.Value;
            if (dist > slack)
            {
                body.AddForce(toPost.normalized * (dist - slack) * MooringsPlugin.Pull.Value, ForceMode.Acceleration);
            }
            // Damp horizontal drift so the line doesn't twang.
            Vector3 v = body.linearVelocity;
            v.x *= 0.95f; v.z *= 0.95f;
            body.linearVelocity = v;
        }
    }

    /// Taking the rudder and pushing forward casts off automatically.
    [HarmonyPatch(typeof(Ship), nameof(Ship.Forward))]
    static class Ship_Forward_Patch
    {
        static void Prefix(Ship __instance) => CastOff(__instance);

        public static void CastOff(Ship ship)
        {
            if (!Mooring.IsMoored(ship)) return;
            Mooring.Release(ship.GetComponent<ZNetView>());
            Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "Cast off");
        }
    }

    [HarmonyPatch(typeof(Ship), nameof(Ship.Backward))]
    static class Ship_Backward_Patch
    {
        static void Prefix(Ship __instance) => Ship_Forward_Patch.CastOff(__instance);
    }
}
