using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Both hands on the steering wheel or the handlebar grips (user, 2026-11-20: "right and left hand pose while
    /// driving, like steering for car and handle for bike scooty"). After the animation (Mixamo clip or the procedural
    /// riding pose) has posed the arm, a two-bone IK turns the upper arm and the forearm so the wrist lands on the target;
    /// the elbow bends toward a hint below and outside the shoulder. The targets are children of the steering wheel /
    /// handlebar, so the hands turn with it. Set up from Resources/VehicleSetup (the Vehicle Setup tool places them).
    /// Runs after HumanoidRig (it writes the bones in LateUpdate).
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class HandIK : MonoBehaviour
    {
        public Transform left, right;   // where the wrists go
        [Range(0f, 1f)] public float weight = 1f;
        /// <summary>The vehicle (for the elbow hints: down and outward in its space).</summary>
        public Transform vehicle;
        Animator anim;

        void LateUpdate()
        {
            if (weight <= 0f || (!left && !right)) return;
            if (!anim) anim = GetComponentInChildren<Animator>();
            if (!anim || !anim.isHuman) return;
            Transform v = vehicle ? vehicle : transform;
            if (left) Solve(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, left.position, -v.right, v);
            if (right) Solve(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, right.position, v.right, v);
        }

        void Solve(HumanBodyBones up, HumanBodyBones lo, HumanBodyBones hd, Vector3 target, Vector3 outward, Transform v)
        {
            Transform a = anim.GetBoneTransform(up), b = anim.GetBoneTransform(lo), c = anim.GetBoneTransform(hd);
            if (!a || !b || !c) return;
            Vector3 pa = a.position;
            float lab = Vector3.Distance(pa, b.position), lbc = Vector3.Distance(b.position, c.position);
            if (lab < 1e-4f || lbc < 1e-4f) return;
            Vector3 toT = target - pa;
            float d = Mathf.Clamp(toT.magnitude, 0.05f, (lab + lbc) * 0.999f);
            Vector3 dir = toT.normalized;
            // the elbow's side: below the shoulder, out to the side and a little back
            Vector3 hint = pa + (-v.up * 0.35f + outward * 0.3f - v.forward * 0.15f);
            Vector3 h = hint - pa;
            Vector3 bend = (h - Vector3.Dot(h, dir) * dir);
            if (bend.sqrMagnitude < 1e-6f) bend = -v.up;
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
