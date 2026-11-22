using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// A person thrown as a real body (owner, 22 Nov 2026: "speed me kisi bhi vehicle se exit krte hain to ragdoll bnke
    /// girega..health jayegi based on speed"): rigid bodies on the humanoid's bones carrying a person's real mass shares
    /// (hips, torso, head, upper / lower arms and legs), capsules sized to the bones, joints with a person's limits (the
    /// knees and elbows bend one way: Unity's RagdollBuilder table), built from the standing pose (built seated, the knees
    /// would stay locked bent), thrown with a velocity. Remove() takes it all off again and gives the bones back to the
    /// Animator (the caller then plays the getting-up clip). Any HumanoidRig with a humanoid Animator: the player now.
    /// </summary>
    public class Ragdoll : MonoBehaviour
    {
        public Transform Hips { get; private set; }
        public Transform Head { get; private set; }
        public float HipsSpeed => hipsRb ? hipsRb.linearVelocity.magnitude : 0f;

        HumanoidRig rig;
        Animator anim;
        Rigidbody hipsRb;
        readonly List<CharacterJoint> joints = new List<CharacterJoint>();
        readonly List<Collider> colliders = new List<Collider>();
        readonly List<Rigidbody> bodies = new List<Rigidbody>();
        readonly List<(SkinnedMeshRenderer r, bool was)> skins = new List<(SkinnedMeshRenderer, bool)>();
        readonly List<Behaviour> paused = new List<Behaviour>();

        /// <summary>Make the body of `rig` a ragdoll of `mass` kg moving at `velocity`. Null if it has no humanoid Animator
        /// (the caller falls back to the knocked-down clip).</summary>
        public static Ragdoll Throw(HumanoidRig rig, Vector3 velocity, float mass = 70f)
        {
            var a = rig ? rig.Anim : null;
            if (!a || !a.isHuman || !a.GetBoneTransform(HumanBodyBones.Hips)) return null;
            var r = rig.gameObject.AddComponent<Ragdoll>();
            r.Build(rig, a, velocity, mass);
            return r;
        }

        static bool Has(Animator a, string p)
        {
            foreach (var q in a.parameters) if (q.name == p) return true;
            return false;
        }

        void Build(HumanoidRig r, Animator a, Vector3 v, float mass)
        {
            rig = r;
            anim = a;
            // 1. the standing pose, evaluated now (the base layer's Move at rest, no action layer)
            foreach (var p in new[] { "Seated", "Driving", "Ride", "Pillion", "Dead", "Knocked", "Rising", "Swim", "Aim" }) if (Has(a, p)) a.SetBool(p, false);
            if (Has(a, "Grounded")) a.SetBool("Grounded", true);
            for (int l = 1; l < a.layerCount; l++) a.SetLayerWeight(l, 0f);
            a.Play("Move", 0, 0f);
            a.Update(0f);
            // 2. nothing else moves these bones now: the rig's procedural pose, the Animator, the hands / feet IK
            foreach (var h in rig.GetComponentsInParent<HandIK>(true)) if (h.enabled) { h.enabled = false; paused.Add(h); }
            foreach (var h in rig.GetComponentsInChildren<HandIK>(true)) if (h.enabled) { h.enabled = false; paused.Add(h); }
            rig.enabled = false;
            a.enabled = false;
            foreach (var s in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { skins.Add((s, s.updateWhenOffscreen)); s.updateWhenOffscreen = true; }   // flown away from its root, it is still drawn

            // the bodies on the person's own layer (the player's / the townsfolk's): the camera, the ground rays and the
            // vehicles' feelers treat the ragdoll as that person (on another layer the camera pulled in to 0.4 m, inside
            // the body: it vanished from view)
            var cc = rig.GetComponentInParent<CharacterController>();
            bodyLayer = cc ? cc.gameObject.layer : rig.gameObject.layer;

            Transform B(HumanBodyBones b) => a.GetBoneTransform(b);
            Hips = B(HumanBodyBones.Hips);
            Head = B(HumanBodyBones.Head);
            Transform spine = B(HumanBodyBones.Spine) ?? B(HumanBodyBones.Chest);
            Transform neck = B(HumanBodyBones.Neck) ?? Head;
            Transform ulL = B(HumanBodyBones.LeftUpperLeg), llL = B(HumanBodyBones.LeftLowerLeg), ftL = B(HumanBodyBones.LeftFoot);
            Transform ulR = B(HumanBodyBones.RightUpperLeg), llR = B(HumanBodyBones.RightLowerLeg), ftR = B(HumanBodyBones.RightFoot);
            Transform uaL = B(HumanBodyBones.LeftUpperArm), laL = B(HumanBodyBones.LeftLowerArm), hdL = B(HumanBodyBones.LeftHand);
            Transform uaR = B(HumanBodyBones.RightUpperArm), laR = B(HumanBodyBones.RightLowerArm), hdR = B(HumanBodyBones.RightHand);
            Transform root = rig.transform;
            Vector3 R = root.right, U = root.up, F = root.forward;
            // sizes from the body's own height (a child is smaller)
            float k = Head && ftL ? Mathf.Clamp((Head.position.y - ftL.position.y) / 1.55f, 0.5f, 1.3f) : 1f;
            // a person's mass shares (the hands go with the forearms, the feet with the shins)
            float[] share = { 0.15f, 0.32f, 0.07f, 0.027f, 0.022f, 0.10f, 0.055f };
            float sum = share[0] + share[1] + share[2] + 2f * (share[3] + share[4] + share[5] + share[6]);
            float M(int i) => mass * share[i] / sum;

            // the pelvis: across the hips
            hipsRb = Body(Hips, M(0), null, R, F, 0f, 0f, 0f);
            if (ulL && ulR) Capsule(Hips, ulL.position, ulR.position, 0.11f * k, true);
            else Sphere(Hips, Hips.position, 0.13f * k);
            // the torso: spine to the neck, shoulders wide
            Rigidbody torso = hipsRb;
            if (spine && neck && spine != Hips)
            {
                torso = Body(spine, M(1), hipsRb, R, F, -20f, 20f, 10f);
                Box(spine, spine.position, neck.position, uaL && uaR ? Vector3.Distance(uaL.position, uaR.position) + 0.08f * k : 0.36f * k, 0.2f * k, R);
            }
            if (Head) { Body(Head, M(2), torso, R, F, -40f, 25f, 25f); Sphere(Head, Head.position + U * 0.09f * k, 0.11f * k); }
            // the arms
            foreach (var (ua, la, hd) in new[] { (uaL, laL, hdL), (uaR, laR, hdR) })
            {
                if (!ua || !la) continue;
                var up = Body(ua, M(3), torso, R, F, -70f, 10f, 50f);
                Capsule(ua, ua.position, la.position, 0.045f * k, false);
                if (!hd) continue;
                Body(la, M(4), up, F, U, -90f, 0f, 0f);
                Capsule(la, la.position, hd.position + (hd.position - la.position).normalized * 0.08f * k, 0.04f * k, false);
            }
            // the legs
            foreach (var (ul, ll, ft) in new[] { (ulL, llL, ftL), (ulR, llR, ftR) })
            {
                if (!ul || !ll) continue;
                var th = Body(ul, M(5), hipsRb, R, F, -20f, 70f, 30f);
                Capsule(ul, ul.position, ll.position, 0.075f * k, false);
                if (!ft) continue;
                Body(ll, M(6), th, R, F, -80f, 0f, 0f);
                Capsule(ll, ll.position, ft.position - U * 0.05f * k, 0.055f * k, false);
            }
            // 3. thrown
            foreach (var b in bodies)
            {
                b.linearVelocity = v;
                b.angularVelocity = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f));
            }
        }

        int bodyLayer;
        readonly List<(GameObject go, int layer)> layers = new List<(GameObject, int)>();

        Rigidbody Body(Transform bone, float m, Rigidbody parent, Vector3 axisW, Vector3 swingW, float lo, float hi, float swing)
        {
            layers.Add((bone.gameObject, bone.gameObject.layer));
            bone.gameObject.layer = bodyLayer;
            var b = bone.gameObject.AddComponent<Rigidbody>();
            b.mass = m;
            b.interpolation = RigidbodyInterpolation.Interpolate;
            b.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            b.linearDamping = 0.05f;
            b.angularDamping = 0.8f;
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
                joints.Add(j);
            }
            return b;
        }

        /// <summary>The bone-space axis nearest to a direction, with its sign.</summary>
        static Vector3 Snap(Vector3 d)
        {
            float x = Mathf.Abs(d.x), y = Mathf.Abs(d.y), z = Mathf.Abs(d.z);
            if (x >= y && x >= z) return new Vector3(Mathf.Sign(d.x), 0f, 0f);
            if (y >= z) return new Vector3(0f, Mathf.Sign(d.y), 0f);
            return new Vector3(0f, 0f, Mathf.Sign(d.z));
        }

        static int Axis(Vector3 d)
        {
            float x = Mathf.Abs(d.x), y = Mathf.Abs(d.y), z = Mathf.Abs(d.z);
            return x >= y && x >= z ? 0 : y >= z ? 1 : 2;
        }

        static float Scale(Transform t) => Mathf.Max(1e-5f, Mathf.Abs(t.lossyScale.x));

        /// <summary>A capsule on `bone` from a to b (world), radius r (world). across: a and b are its two sides (the pelvis).</summary>
        void Capsule(Transform bone, Vector3 a, Vector3 b, float r, bool across)
        {
            Vector3 la = bone.InverseTransformPoint(a), lb = bone.InverseTransformPoint(b);
            Vector3 d = lb - la;
            int ax = Axis(d);
            var c = bone.gameObject.AddComponent<CapsuleCollider>();
            c.direction = ax;
            float rl = r / Scale(bone);
            Vector3 mid = (la + lb) * 0.5f;
            c.center = mid;
            c.radius = rl;
            c.height = Mathf.Abs(d[ax]) + (across ? 2f * rl : rl);
            colliders.Add(c);
        }

        void Sphere(Transform bone, Vector3 at, float r)
        {
            var c = bone.gameObject.AddComponent<SphereCollider>();
            c.center = bone.InverseTransformPoint(at);
            c.radius = r / Scale(bone);
            colliders.Add(c);
        }

        /// <summary>A box on `bone` from a to b (world: its length), `width` along the body's right, `depth` the third way.</summary>
        void Box(Transform bone, Vector3 a, Vector3 b, float width, float depth, Vector3 rightW)
        {
            Vector3 la = bone.InverseTransformPoint(a), lb = bone.InverseTransformPoint(b);
            int along = Axis(lb - la);
            int side = Axis(bone.InverseTransformDirection(rightW));
            if (side == along) side = (along + 1) % 3;
            int third = 3 - along - side;
            float s = Scale(bone);
            var c = bone.gameObject.AddComponent<BoxCollider>();
            c.center = (la + lb) * 0.5f;
            Vector3 size = Vector3.zero;
            size[along] = Mathf.Abs((lb - la)[along]);
            size[side] = width / s;
            size[third] = depth / s;
            c.size = size;
            colliders.Add(c);
        }

        /// <summary>The bodies off again (at once: nothing left for the next physics step), the bones back to the Animator and
        /// the rig.</summary>
        public void Remove()
        {
            foreach (var j in joints) if (j) DestroyImmediate(j);
            foreach (var c in colliders) if (c) DestroyImmediate(c);
            foreach (var b in bodies) if (b) DestroyImmediate(b);
            joints.Clear(); colliders.Clear(); bodies.Clear();
            foreach (var (go, layer) in layers) if (go) go.layer = layer;
            layers.Clear();
            foreach (var (r, was) in skins) if (r) r.updateWhenOffscreen = was;
            foreach (var h in paused) if (h) h.enabled = true;
            if (anim) anim.enabled = true;
            if (rig) rig.enabled = true;
            Destroy(this);
        }
    }
}
