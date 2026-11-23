using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// A street animal killed (owner, 24 Nov 2026: "animals ko koi hit kre to vo bhaage, pr agar jayda hit krdiya to
    /// marjayenge with ragdoll"): rigid bodies on the bones Tools/animals/rig_animal.py builds (hips, spine, chest, neck,
    /// head, leg_XY1..3 for FL / FR / BL / BR, tail1..3), the animal's real mass shared over them (cow 350 kg, dog 22, cat 4,
    /// monkey 8), capsules sized from the bones and the body's own height, joints that let a leg fold and the neck drop, and
    /// the whole body thrown with the hit's velocity. The legacy Animation, the CharacterController and the Animal brain go
    /// off; AnimalManager takes the body away later (out of sight, or after a few minutes).
    /// </summary>
    public class AnimalRagdoll : MonoBehaviour
    {
        public static int Killed;
        public Transform Hips { get; private set; }
        public float DiedAt { get; private set; }

        readonly List<Rigidbody> bodies = new List<Rigidbody>();
        int bodyLayer;

        static float MassOf(AnimalKind k) => k == AnimalKind.Cow ? 350f : k == AnimalKind.Dog ? 22f : k == AnimalKind.Monkey ? 8f : 4f;

        /// <summary>Kill `a` as a ragdoll thrown at `velocity`. Null when its bones are not there (the body then just stops).</summary>
        public static AnimalRagdoll Kill(Animal a, Vector3 velocity)
        {
            if (!a) return null;
            var r = a.gameObject.AddComponent<AnimalRagdoll>();
            if (!r.Build(a, velocity)) { Destroy(r); return null; }
            Killed++;
            return r;
        }

        static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                int bar = n.LastIndexOf(':');
                if (bar >= 0) n = n.Substring(bar + 1);
                if (n == name) return t;
            }
            return null;
        }

        bool Build(Animal a, Vector3 v)
        {
            var model = a.transform.Find("Model") ?? a.transform;
            Hips = Find(model, "hips");
            var spine = Find(model, "spine");
            var chest = Find(model, "chest");
            var neck = Find(model, "neck");
            var head = Find(model, "head");
            if (!Hips || !chest) return false;
            DiedAt = Time.time;

            // nothing else moves these bones now
            var anim = model.GetComponentInChildren<Animation>(true);
            if (anim) { anim.Stop(); anim.enabled = false; }
            var cc = a.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            var motor = a.GetComponent<CharacterMotor>();
            if (motor) motor.enabled = false;
            foreach (var s in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) s.updateWhenOffscreen = true;
            bodyLayer = a.gameObject.layer;   // 9, like the townsfolk: the cars' feelers and the camera see it as before

            var spec = a.spec ?? Animal.SpecOf(a.kind);
            float mass = MassOf(a.kind);
            Vector3 R = a.transform.right, U = a.transform.up, F = a.transform.forward;
            float H = spec.height, torsoR = H * 0.2f, legR = H * 0.045f;

            // the trunk: hips -> spine -> chest, fat capsules along the back
            var hipsRb = Body(Hips, mass * 0.26f, null, F, R, 0f, 0f, 0f);
            Capsule(Hips, Hips.position, spine ? spine.position : chest.position, torsoR);
            Rigidbody trunk = hipsRb;
            if (spine)
            {
                trunk = Body(spine, mass * 0.22f, hipsRb, F, R, -15f, 15f, 12f);
                Capsule(spine, spine.position, chest.position, torsoR);
            }
            var chestRb = Body(chest, mass * 0.22f, trunk, F, R, -15f, 15f, 12f);
            Capsule(chest, chest.position, neck ? neck.position : chest.position + F * torsoR, torsoR);
            Rigidbody top = chestRb;
            if (neck)
            {
                top = Body(neck, mass * 0.05f, chestRb, R, F, -50f, 30f, 30f);
                Capsule(neck, neck.position, head ? head.position : neck.position + F * 0.2f * H, torsoR * 0.55f);
            }
            if (head)
            {
                Body(head, mass * 0.06f, top, R, F, -30f, 30f, 25f);
                var tip = head.childCount > 0 ? head.GetChild(0).position : head.position + F * torsoR;
                Capsule(head, head.position, tip, torsoR * 0.5f);
            }
            // four legs of three parts each: the upper part swings, the knee / hock folds one way
            foreach (var k in new[] { "FL", "FR", "BL", "BR" })
            {
                var l1 = Find(model, "leg_" + k + "1");
                var l2 = Find(model, "leg_" + k + "2");
                var l3 = Find(model, "leg_" + k + "3");
                if (!l1 || !l2) continue;
                var parentRb = k[0] == 'F' ? chestRb : hipsRb;
                var up = Body(l1, mass * 0.03f, parentRb, R, F, -45f, 45f, 20f);
                Capsule(l1, l1.position, l2.position, legR * 1.3f);
                var lo = Body(l2, mass * 0.012f, up, R, F, k[0] == 'F' ? -5f : -100f, k[0] == 'F' ? 100f : 5f, 5f);
                Capsule(l2, l2.position, l3 ? l3.position : l2.position - U * 0.1f * H, legR);
            }
            // the tail: light, floppy
            Rigidbody prev = hipsRb;
            for (int i = 1; i <= 3; i++)
            {
                var t = Find(model, "tail" + i);
                if (!t) break;
                prev = Body(t, mass * 0.005f, prev, R, U, -40f, 40f, 40f);
                var tip = t.childCount > 0 ? t.GetChild(0).position : t.position - F * 0.1f * H;
                Capsule(t, t.position, tip, legR * 0.7f);
            }
            foreach (var b in bodies)
            {
                b.linearVelocity = v;
                b.angularVelocity = Random.insideUnitSphere * 1.5f;
            }
            return true;
        }

        Rigidbody Body(Transform bone, float m, Rigidbody parent, Vector3 axisW, Vector3 swingW, float lo, float hi, float swing)
        {
            bone.gameObject.layer = bodyLayer;
            var b = bone.gameObject.AddComponent<Rigidbody>();
            b.mass = Mathf.Max(0.05f, m);
            b.interpolation = RigidbodyInterpolation.Interpolate;
            b.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            b.linearDamping = 0.05f;
            b.angularDamping = 0.9f;
            b.maxDepenetrationVelocity = 3f;
            bodies.Add(b);
            if (parent)
            {
                var j = bone.gameObject.AddComponent<CharacterJoint>();
                j.connectedBody = parent;
                j.axis = Snap(bone.InverseTransformDirection(axisW));
                j.swingAxis = Snap(bone.InverseTransformDirection(swingW));
                j.lowTwistLimit = new SoftJointLimit { limit = lo };
                j.highTwistLimit = new SoftJointLimit { limit = hi };
                j.swing1Limit = new SoftJointLimit { limit = swing };
                j.swing2Limit = new SoftJointLimit { limit = swing };
                j.enableProjection = true;
                j.enablePreprocessing = false;
            }
            return b;
        }

        static Vector3 Snap(Vector3 d)
        {
            float x = Mathf.Abs(d.x), y = Mathf.Abs(d.y), z = Mathf.Abs(d.z);
            if (x >= y && x >= z) return new Vector3(Mathf.Sign(d.x), 0f, 0f);
            if (y >= z) return new Vector3(0f, Mathf.Sign(d.y), 0f);
            return new Vector3(0f, 0f, Mathf.Sign(d.z));
        }

        /// <summary>A capsule on `bone` from a to b (world), radius r (world), along the bone-space axis nearest a-b.</summary>
        static void Capsule(Transform bone, Vector3 a, Vector3 b, float r)
        {
            Vector3 la = bone.InverseTransformPoint(a), lb = bone.InverseTransformPoint(b);
            Vector3 d = lb - la;
            float x = Mathf.Abs(d.x), y = Mathf.Abs(d.y), z = Mathf.Abs(d.z);
            int ax = x >= y && x >= z ? 0 : y >= z ? 1 : 2;
            float s = Mathf.Max(1e-5f, Mathf.Abs(bone.lossyScale.x));
            var c = bone.gameObject.AddComponent<CapsuleCollider>();
            c.direction = ax;
            c.center = (la + lb) * 0.5f;
            c.radius = r / s;
            c.height = Mathf.Abs(d[ax]) + 2f * c.radius;
        }
    }
}
