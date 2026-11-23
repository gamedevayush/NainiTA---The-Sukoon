using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// A seated person moved by the ride (owner, 23 Nov 2026: "sab hlke hlke hilte rhe log..gardan peeche kamar side..random..
    /// pr poora bus me same effect..like brake pe gardan aage hojaana..ya mod pe usse react"). The vehicle's acceleration felt
    /// as inertia - braking tips the body forward, pulling away tips it back, a bend leans it out of the bend, a bump drops
    /// the head - through a spring that lags and overshoots a little, the same for everybody aboard; on top, each one's own
    /// slow sway (the waist side to side, the neck back and forth, looking about). Added by VehicleParts.Sit, off at Leave.
    /// Runs after the rig (50), before the camera (100: the first-person eye rides the head) and the hand / foot IK (10000:
    /// the hands stay on the grips).
    /// </summary>
    [DefaultExecutionOrder(60)]
    public class RideSway : MonoBehaviour
    {
        public Rigidbody vehicle;
        Transform spine, chest, neck, head;
        readonly Quaternion[] setL = new Quaternion[4], baseL = new Quaternion[4];
        Vector3 prevVel, acc;
        Vector2 lean, leanV;   // x: forward (+) / back, y: right (+) / left; degrees
        float nod, nodV, seed;
        bool found;

        /// <summary>Seated in `vehicle`'s seat: sway with it.</summary>
        public static void On(Transform person, Transform vehicleT)
        {
            if (!person || !vehicleT) return;
            var body = vehicleT.GetComponentInParent<Rigidbody>();
            if (!body) return;
            var s = person.GetComponent<RideSway>();
            if (!s) s = person.gameObject.AddComponent<RideSway>();
            s.vehicle = body;
            s.enabled = false;   // (OnEnable: a fresh start)
            s.enabled = true;
        }

        /// <summary>Out of the seat: still again.</summary>
        public static void Off(Transform person)
        {
            var s = person ? person.GetComponent<RideSway>() : null;
            if (s) s.enabled = false;
        }

        void OnEnable()
        {
            found = false;
            lean = leanV = Vector2.zero;
            nod = nodV = 0f;
            prevVel = vehicle ? vehicle.linearVelocity : Vector3.zero;
            acc = Vector3.zero;
            seed = Random.value * 100f;
        }

        void OnDisable()
        {
            Transform[] b = { spine, chest, neck, head };
            for (int i = 0; i < 4; i++) if (b[i] && b[i].localRotation == setL[i]) b[i].localRotation = baseL[i];
        }

        bool Find()
        {
            var rig = GetComponentInChildren<HumanoidRig>(true);
            var a = rig ? rig.Anim : null;
            if (!a || !a.isHuman) return false;
            spine = a.GetBoneTransform(HumanBodyBones.Spine);
            chest = a.GetBoneTransform(HumanBodyBones.Chest);
            if (!chest) chest = a.GetBoneTransform(HumanBodyBones.UpperChest);
            neck = a.GetBoneTransform(HumanBodyBones.Neck);
            head = a.GetBoneTransform(HumanBodyBones.Head);
            return spine && head;
        }

        void LateUpdate()
        {
            if (!vehicle) return;
            if (!found) { found = Find(); if (!found) { enabled = false; return; } }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            // the acceleration felt, smoothed (the physics moves it in steps)
            Vector3 v = vehicle.linearVelocity;
            Vector3 a = (v - prevVel) / dt;
            prevVel = v;
            acc = Vector3.Lerp(acc, Vector3.ClampMagnitude(a, 25f), 1f - Mathf.Exp(-dt / 0.12f));
            Transform vt = vehicle.transform;
            float along = Vector3.Dot(acc, vt.forward), side = Vector3.Dot(acc, vt.right), up = Vector3.Dot(acc, vt.up);
            // inertia: the body is left behind what the seat does (2.2 degrees per m/s2)
            var target = new Vector2(Mathf.Clamp(-along * 2.2f, -10f, 14f), Mathf.Clamp(-side * 2.2f, -14f, 14f));
            leanV += ((target - lean) * 42f - leanV * 8f) * dt;
            lean += leanV * dt;
            nodV += (up * 0.9f - nod * 60f - nodV * 7f) * dt;   // thrown up: the head drops forward, then comes back
            nod += nodV * dt;
            // each one's own sway
            float t = Time.time + seed;
            float waist = (Mathf.PerlinNoise(t * 0.25f, seed) - 0.5f) * 5f;
            float back = (Mathf.PerlinNoise(seed, t * 0.2f) - 0.5f) * 6f;
            float look = (Mathf.PerlinNoise(t * 0.12f, seed * 2f) - 0.5f) * 30f;
            Vector3 R = vt.right, F = vt.forward, U = vt.up;
            Apply(0, spine, Quaternion.AngleAxis(lean.x * 0.35f, R) * Quaternion.AngleAxis(-(lean.y * 0.35f + waist * 0.5f), F));
            Apply(1, chest, Quaternion.AngleAxis(lean.x * 0.3f, R) * Quaternion.AngleAxis(-(lean.y * 0.3f + waist * 0.5f), F));
            Apply(2, neck, Quaternion.AngleAxis(lean.x * 0.2f + back * 0.5f + nod * 0.5f, R));
            Apply(3, head, Quaternion.AngleAxis(look, U) * Quaternion.AngleAxis(lean.x * 0.25f + back * 0.5f + nod, R) * Quaternion.AngleAxis(-lean.y * 0.15f, F));
        }

        /// <summary>A world-space turn on top of the bone's pose this frame; never piled up on last frame's when nothing
        /// rewrote the bone since (the rig's far-away animation LOD skips frames).</summary>
        void Apply(int i, Transform b, Quaternion worldDelta)
        {
            if (!b) return;
            if (b.localRotation == setL[i]) b.localRotation = baseL[i];
            baseL[i] = b.localRotation;
            b.rotation = worldDelta * b.rotation;
            setL[i] = b.localRotation;
        }

        /// <summary>The lean now (degrees forward, right) - tests.</summary>
        public Vector2 Lean => lean;
    }
}
