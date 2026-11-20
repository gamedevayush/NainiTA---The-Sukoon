using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Both hands on the steering wheel or the handlebar grips (user, 2026-11-20: "right and left hand pose while
    /// driving, like steering for car and handle for bike scooty"), and both feet on the floor / the pedals / the footpegs
    /// (user, 2026-11-20 night: "bike pe and car me bethne ka animation theek nahi" - every Mixamo sitting clip, Driving
    /// included, is a chair pose with the shins straight down, so in a car seat 0.28 m over the floor the feet went
    /// through the floor and out under the car, and on a bike they hung in the air off the pegs).
    /// After the animation (Mixamo clip or the procedural riding pose) has posed the limb, a two-bone IK turns the upper
    /// bone and the lower bone so the wrist / ankle lands on its target: the elbow bends toward a hint below and outside
    /// the shoulder, the knee toward a hint ahead of and above the hip. A foot keeps the turn the clip gave it. The hand
    /// targets are children of the steering wheel / handlebar, so the hands turn with it; the foot targets sit on the
    /// vehicle. Set up from Resources/VehicleSetup (the Vehicle Setup tool places them). Runs after HumanoidRig (it writes
    /// the bones in LateUpdate).
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class HandIK : MonoBehaviour
    {
        public Transform left, right;   // where the wrists go
        public Transform footLeft, footRight;   // where the ankles go
        [Range(0f, 1f)] public float weight = 1f;
        /// <summary>The vehicle (for the elbow / knee hints: in its space).</summary>
        public Transform vehicle;
        /// <summary>How far the back may lean forward from upright, degrees (-1 = as the clip holds it). The motorbike pack's
        /// riding clips are made on a sport bike, chest well forward; on an Indian commuter / scooty the rider sits nearly
        /// upright (owner, 20 Nov night: "pehla itna jhuka kyun hai"). The back is straightened to this first, then Reach
        /// leans it only as far as the grips need.</summary>
        public float maxBend = -1f;
        /// <summary>How far the back may lean further to reach the wheel / the grips (degrees), and how much longer the arm
        /// may be drawn out for the rest (x its own length). The town's own 12 characters have short arms (shoulder to
        /// wrist 0.18-0.39 m, measured 20 Nov: ClipPoseReport.Arms), so the reach alone hunched them 30-40 deg over a
        /// commuter's bars; a little longer forearm reads far better than a rider folded over the tank.</summary>
        public float maxReachLean = 20f, maxArmStretch = 1.35f;
        Animator anim;
        HumanoidRig rig;
        // the arm bones' own offsets from their parents, kept to stretch from (and put back) every frame
        readonly System.Collections.Generic.Dictionary<Transform, Vector3> rest = new System.Collections.Generic.Dictionary<Transform, Vector3>();

        void LateUpdate()
        {
            if (weight <= 0f || (!left && !right && !footLeft && !footRight && maxBend < 0f)) return;
            if (!anim) anim = GetComponentInChildren<Animator>();
            if (!anim || !anim.isHuman) return;
            Transform v = vehicle ? vehicle : transform;
            // legs first: the hips do not move, so the arms are unaffected. A foot target is where a nominal sole puts the
            // ankle (HumanoidRig.NominalSole*); this character's own sole lifts / lowers it
            if (!rig) rig = GetComponentInChildren<HumanoidRig>();
            Vector3 lift = rig ? v.up * rig.SoleLift : Vector3.zero;
            if (footLeft) Leg(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, footLeft.position + lift, -v.right, v);
            if (footRight) Leg(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, footRight.position + lift, v.right, v);
            if (maxBend >= 0f) Upright(v);
            foreach (var kv in rest) if (kv.Key) kv.Key.localPosition = kv.Value;   // own arm lengths while Reach measures
            if (left && right) Reach(left.position, right.position);
            // the elbow's side: below the shoulder, out to the side and a little back
            if (left) { Stretch(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, left.position); Solve(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, left.position, -v.up * 0.35f - v.right * 0.3f - v.forward * 0.15f); }
            if (right) { Stretch(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, right.position); Solve(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, right.position, -v.up * 0.35f + v.right * 0.3f - v.forward * 0.15f); }
        }

        /// <summary>Draw the forearm and the hand out along their bones (up to maxArmStretch) when the target is beyond the
        /// arm's own reach; otherwise their own lengths. The skin follows the bones, so the arm reads longer.</summary>
        void Stretch(HumanBodyBones up, HumanBodyBones lo, HumanBodyBones hd, Vector3 target)
        {
            Transform a = anim.GetBoneTransform(up), b = anim.GetBoneTransform(lo), c = anim.GetBoneTransform(hd);
            if (!a || !b || !c) return;
            if (!rest.ContainsKey(b)) rest[b] = b.localPosition;
            if (!rest.ContainsKey(c)) rest[c] = c.localPosition;
            b.localPosition = rest[b];
            c.localPosition = rest[c];
            if (maxArmStretch <= 1f) return;
            float len = Vector3.Distance(a.position, b.position) + Vector3.Distance(b.position, c.position);
            if (len < 1e-4f) return;
            float k = Mathf.Clamp(Vector3.Distance(a.position, target) / (len * 0.97f), 1f, maxArmStretch);
            k = Mathf.Lerp(1f, k, weight);
            b.localPosition = rest[b] * k;
            c.localPosition = rest[c] * k;
        }

        void OnDisable()
        {
            // the arms back to their own lengths when the hands leave the wheel
            foreach (var kv in rest) if (kv.Key) kv.Key.localPosition = kv.Value;
        }

        /// <summary>How far the back leans forward now: the hips-to-neck line against the vehicle's up, in its side plane
        /// (+ = forward), degrees.</summary>
        public float BendNow()
        {
            if (!anim) anim = GetComponentInChildren<Animator>();
            if (!anim || !anim.isHuman) return 0f;
            Transform v = vehicle ? vehicle : transform;
            Transform h = anim.GetBoneTransform(HumanBodyBones.Hips), n = anim.GetBoneTransform(HumanBodyBones.Neck);
            if (!n) n = anim.GetBoneTransform(HumanBodyBones.Head);
            if (!h || !n) return 0f;
            Vector3 t = Vector3.ProjectOnPlane(n.position - h.position, v.right);
            return Vector3.SignedAngle(v.up, t, v.right);
        }

        /// <summary>Straighten the back to maxBend: the spine and the chest turn back together about the line across the
        /// body, as far as the clip leans past it. Measured again after each turn (three passes): the spine turns about a
        /// point above the hips and the pelvis keeps its own tilt, so one turn of the full difference fell short (a
        /// pillion stayed at 12-24 deg for a 5 deg limit, 20 Nov).</summary>
        void Upright(Transform v)
        {
            Transform sp = anim.GetBoneTransform(HumanBodyBones.Spine), ch = anim.GetBoneTransform(HumanBodyBones.Chest);
            if (!sp) return;
            for (int k = 0; k < 3; k++)
            {
                float over = BendNow() - maxBend;
                if (over <= 0.5f) return;
                over = Mathf.Min(over * 1.15f, 50f) * weight;
                if (ch && ch != sp)
                {
                    sp.rotation = Quaternion.AngleAxis(-over * 0.5f, v.right) * sp.rotation;
                    ch.rotation = Quaternion.AngleAxis(-over * 0.5f, v.right) * ch.rotation;
                }
                else sp.rotation = Quaternion.AngleAxis(-over, v.right) * sp.rotation;
            }
        }

        /// <summary>Lean the back toward the grips / the wheel when the arms cannot reach them from where the clip holds the
        /// shoulders (a motorbike's grips are well ahead of an upright rider: 22 cm short on bike 13, 20 Nov): the spine
        /// and the chest turn forward together about the line across the hips, up to 35°, until the shoulders are an arm's
        /// length (less 4 %) from the hands' targets.</summary>
        void Reach(Vector3 l, Vector3 r)
        {
            Transform sp = anim.GetBoneTransform(HumanBodyBones.Spine), ch = anim.GetBoneTransform(HumanBodyBones.Chest);
            Transform ua = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm), ub = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform fa = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm), hd = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            if (!sp || !ua || !ub || !fa || !hd) return;
            float arm = (Vector3.Distance(ua.position, fa.position) + Vector3.Distance(fa.position, hd.position)) * 0.96f;   // nearly straight arms before any lean
            Vector3 sh = (ua.position + ub.position) * 0.5f, tg = (l + r) * 0.5f;
            float need = Vector3.Distance(sh, tg) - arm;
            if (need <= 0.005f) return;
            Vector3 pivot = sp.position, a = sh - pivot, b = tg - pivot;
            Vector3 axis = Vector3.Cross(a, b);
            if (axis.sqrMagnitude < 1e-8f || a.magnitude < 0.05f) return;
            // the angle that brings the shoulders `need` closer, on the arc they swing round the spine (two passes: the
            // arc is not a straight line to the grips)
            float deg = 0f;
            for (int k = 0; k < 2; k++)
            {
                float step = Mathf.Clamp(need / a.magnitude * Mathf.Rad2Deg, 0f, maxReachLean - deg);
                if (step <= 0.1f) break;
                deg += step;
                var q = Quaternion.AngleAxis(step * weight, axis.normalized);
                if (ch && ch != sp) { sp.rotation = Quaternion.AngleAxis(step * 0.5f * weight, axis.normalized) * sp.rotation; ch.rotation = Quaternion.AngleAxis(step * 0.5f * weight, axis.normalized) * ch.rotation; }
                else sp.rotation = q * sp.rotation;
                sh = (ua.position + ub.position) * 0.5f;
                need = Vector3.Distance(sh, tg) - arm;
                a = sh - pivot;
                if (need <= 0.005f) break;
            }
        }

        /// <summary>A leg: the knee forward and up (and a little out: round a bike's tank), the foot's own turn kept.</summary>
        void Leg(HumanBodyBones up, HumanBodyBones lo, HumanBodyBones ft, Vector3 target, Vector3 outward, Transform v)
        {
            var foot = anim.GetBoneTransform(ft);
            if (!foot) return;
            Quaternion keep = foot.rotation;
            Solve(up, lo, ft, target, v.forward * 0.6f + v.up * 0.25f + outward * 0.12f);
            foot.rotation = Quaternion.Slerp(foot.rotation, keep, weight);
        }

        void Solve(HumanBodyBones up, HumanBodyBones lo, HumanBodyBones hd, Vector3 target, Vector3 hintOffset)
        {
            Transform a = anim.GetBoneTransform(up), b = anim.GetBoneTransform(lo), c = anim.GetBoneTransform(hd);
            if (!a || !b || !c) return;
            Vector3 pa = a.position;
            float lab = Vector3.Distance(pa, b.position), lbc = Vector3.Distance(b.position, c.position);
            if (lab < 1e-4f || lbc < 1e-4f) return;
            Vector3 toT = target - pa;
            float d = Mathf.Clamp(toT.magnitude, 0.05f, (lab + lbc) * 0.999f);
            Vector3 dir = toT.normalized;
            Vector3 h = hintOffset;
            Vector3 bend = (h - Vector3.Dot(h, dir) * dir);
            if (bend.sqrMagnitude < 1e-6f) bend = hintOffset.normalized;
            bend.Normalize();
            float cosA = Mathf.Clamp((lab * lab + d * d - lbc * lbc) / (2f * lab * d), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            Vector3 elbow = pa + (dir * cosA + bend * sinA) * lab;
            Quaternion ua = a.rotation, la = b.rotation;
            a.rotation = Quaternion.FromToRotation(b.position - pa, elbow - pa) * a.rotation;
            b.rotation = Quaternion.FromToRotation(c.position - b.position, pa + dir * d - b.position) * b.rotation;
            if (weight < 1f)
            {
                a.rotation = Quaternion.Slerp(ua, a.rotation, weight);
                b.rotation = Quaternion.Slerp(la, b.rotation, weight);
            }
        }
    }
}
