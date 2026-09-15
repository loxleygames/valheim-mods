using HarmonyLib;
using UnityEngine;

namespace ComfyFishing
{
    public static class Reel
    {
        static readonly AccessTools.FieldRef<FishingFloat, float> LineLength = AccessTools.FieldRefAccess<FishingFloat, float>("m_lineLength");
        static readonly AccessTools.FieldRef<FishingFloat, Rigidbody> Body = AccessTools.FieldRefAccess<FishingFloat, Rigidbody>("m_body");
        static readonly AccessTools.FieldRef<FishingFloat, ZNetView> View = AccessTools.FieldRefAccess<FishingFloat, ZNetView>("m_nview");
        static readonly AccessTools.FieldRef<FishingFloat, Fish> Nibbler = AccessTools.FieldRefAccess<FishingFloat, Fish>("m_nibbler");
        static readonly AccessTools.FieldRef<FishingFloat, float> NibbleTime = AccessTools.FieldRefAccess<FishingFloat, float>("m_nibbleTime");
        static readonly AccessTools.FieldRef<FishingFloat, float> SkillTimer = AccessTools.FieldRefAccess<FishingFloat, float>("m_fishingSkillImproveTimer");

        static Character Owner(FishingFloat f) => Traverse.Create(f).Method("GetOwner").GetValue<Character>();
        static Transform RodTop(FishingFloat f, Character o) => Traverse.Create(f).Method("GetRodTop", o).GetValue<Transform>();
        static void SetCatch(FishingFloat f, Fish fish) => Traverse.Create(f).Method("SetCatch", fish).GetValue();
        static void Say(FishingFloat f, string msg, bool prio) => Traverse.Create(f).Method("Message", msg, prio).GetValue();

        /// Hook on a bite: wider window than vanilla, or no click at all.
        static void TryHook(FishingFloat f, Character owner)
        {
            var nibbler = Nibbler(f);
            if (nibbler == null || f.GetCatch() != null) return;
            if (Time.time - NibbleTime(f) > ComfyFishingPlugin.HookWindow.Value) { Nibbler(f) = null; return; }
            if (!ComfyFishingPlugin.AutoHook.Value && !owner.IsBlocking()) return;
            Say(f, "$msg_fishing_hooked", true);
            SetCatch(f, nibbler);
            Nibbler(f) = null;
            Game.instance.IncrementPlayerStat(PlayerStatType.FishHooked);
        }

        [HarmonyPatch(typeof(FishingFloat), "FixedUpdate")]
        static class ComfyReel
        {
            static bool Prefix(FishingFloat __instance)
            {
                var f = __instance;
                var nview = View(f);
                if (!nview.IsOwner()) return true;
                var owner = Owner(f);
                if (!owner) return true;

                var fish = f.GetCatch();
                if (!fish)
                {
                    TryHook(f, owner);
                    return true; // no fish on: vanilla handles casting, waiting and reeling the empty line
                }

                var rodTop = RodTop(f, owner);
                if (!rodTop) return true;
                if (owner.InAttack() || owner.IsDrawingBow())
                {
                    fish.OnHooked(null);
                    nview.Destroy();
                    return false;
                }

                float dt = Time.fixedDeltaTime;
                float skill = owner.GetSkillFactor(Skills.SkillType.Fishing);
                float speed = Mathf.Lerp(ComfyFishingPlugin.ReelSpeed.Value, ComfyFishingPlugin.ReelSpeedMaxSkill.Value, skill);
                speed *= 1f + 0.2f * (Rods.TierOf(owner) - 1);
                var drop = fish.GetComponent<ItemDrop>();
                int quality = drop ? drop.m_itemData.m_quality : 1;
                speed /= 1f + 0.15f * (quality - 1); // big fish take longer, they don't take stamina

                float before = LineLength(f);
                LineLength(f) = Mathf.Max(0f, before - speed * dt);
                if ((int)LineLength(f) != (int)before) Say(f, LineLength(f).ToString("0m"), false);

                SkillTimer(f) += dt * 2f;
                if (SkillTimer(f) > 1f) { SkillTimer(f) = 0f; owner.RaiseSkill(Skills.SkillType.Fishing); }

                if (LineLength(f) <= 0.5f)
                {
                    Say(f, FishingFloat.Catch(fish, owner), true);
                    SetCatch(f, null);
                    fish.OnHooked(null);
                    nview.Destroy();
                    return false;
                }

                // Keep float and fish on the line; the line never breaks while something's on it.
                var body = Body(f);
                Utils.Pull(body, fish.transform.position, 0.5f, f.m_moveForce, 0.5f, 0.3f);
                Utils.Pull(body, rodTop.position, LineLength(f), f.m_moveForce, 1f, 0.3f);
                float dist = (rodTop.position - f.transform.position).magnitude;
                f.m_rodLine.SetSlack((1f - Utils.LerpStep(LineLength(f) / 2f, LineLength(f), dist)) * f.m_maxLineSlack);
                return false;
            }
        }

        /// The bite arrives at the float's owner: refuse it if their skill or rod isn't up to the bait.
        [HarmonyPatch(typeof(FishingFloat), nameof(FishingFloat.RPC_Nibble))]
        static class GateBite
        {
            static bool Prefix(FishingFloat __instance, bool correctBait)
            {
                if (!correctBait || !ComfyFishingPlugin.GateBySkill.Value) return true;
                var owner = Owner(__instance);
                if (!owner) return true;
                var (skill, rod) = Baits.Requirement(__instance.GetBait());
                int have = Mathf.FloorToInt(owner.GetSkillLevel(Skills.SkillType.Fishing));
                if (have < skill) { Say(__instance, $"Something bites, but it's too strong for you (fishing {skill})", true); return false; }
                if (Rods.TierOf(owner) < rod) { Say(__instance, "Something bites, but your rod isn't up to it", true); return false; }
                return true;
            }
        }
    }
}
