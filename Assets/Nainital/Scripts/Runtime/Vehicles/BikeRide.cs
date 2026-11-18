using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// A two-wheeler (Hero Splendor, the scooty) moved along the ground without wheel physics: it turns at a rate its
    /// speed allows, leans into the turn, tips with the slope, and stays on the ground (raycasts front and back). Used by
    /// the town's traffic (TrafficBike, user 2026-11-18: "normal traffic me bhi bikes or scooty") and the police bike.
    /// A car the player drives that runs into it knocks it over for real: it turns into a physics body and tumbles, and
    /// whoever rode it is thrown (OnCrash).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BikeRide : MonoBehaviour
    {
        public float length = 2f, width = 0.9f, height = 1.3f, seatTop = 0.78f;
        public Transform model;      // the visual (leans); riders sit under it
        public float Speed { get; private set; }
        public bool Crashed { get; private set; }
        public Vector3 Forward => new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        /// <summary>Thrown by the player's car: (the car's velocity, the car).</summary>
        public event System.Action<Vector3, GameObject> OnCrash;

        Rigidbody rb;
        float yawRate, lean, yaw, crashT, pitch;
        static int groundMask = -1;

        public static GameObject Create(VehicleLibrary.Entry e, string name)
        {
            var root = new GameObject(name ?? e.name) { layer = VehicleFactory.VehicleLayer };
            var lean = new GameObject("Lean").transform;
            lean.SetParent(root.transform, false);
            var body = Object.Instantiate(e.prefab, lean, false);
            body.name = "Body";
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = VehicleFactory.VehicleLayer;
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, e.size.y * 0.45f, 0f);
            box.size = new Vector3(Mathf.Min(e.size.x, 0.7f), e.size.y * 0.8f, e.size.z * 0.95f);
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 130f;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var b = root.AddComponent<BikeRide>();
            b.model = lean;
            b.length = e.size.z;
            b.width = e.size.x;
            b.height = e.size.y;
            b.seatTop = e.size.y * 0.57f;
            var info = root.AddComponent<VehicleInfo>();   // NpcWalker / TrafficCar read the size
            info.length = e.size.z;
            info.width = Mathf.Min(e.size.x, 0.8f);
            info.height = e.size.y;
            info.wheelRadius = 0.3f;
            return root;
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (groundMask < 0) groundMask = ~((1 << 8) | (1 << 9) | (1 << VehicleFactory.VehicleLayer) | (1 << 2) | (1 << 4));
            yaw = transform.eulerAngles.y;
        }

        /// <summary>Where a rider's root sits (bike space): astride the seat, a little behind the middle.</summary>
        public Vector3 SeatLocal => new Vector3(0f, seatTop + 0.05f - 0.51f, -length * 0.1f);

        /// <summary>Place it on the ground at p facing f, standing still.</summary>
        public void Place(Vector3 p, Vector3 f)
        {
            f.y = 0f;
            if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
            yaw = Quaternion.LookRotation(f.normalized).eulerAngles.y;
            Speed = 0f;
            Vector3 g = GroundAt(p, out _);
            transform.SetPositionAndRotation(g, Quaternion.Euler(0f, yaw, 0f));
            Physics.SyncTransforms();
        }

        Vector3 GroundAt(Vector3 p, out bool ok)
        {
            var t = Terrain.activeTerrain;
            float top = t ? t.SampleHeight(p) + t.transform.position.y : p.y;
            if (Physics.Raycast(new Vector3(p.x, Mathf.Max(p.y, top) + 1.6f, p.z), Vector3.down, out var hit, 5f, groundMask, QueryTriggerInteraction.Ignore))
            {
                ok = true;
                return new Vector3(p.x, hit.point.y, p.z);
            }
            ok = t;
            return new Vector3(p.x, t ? top : p.y, p.z);
        }

        /// <summary>One physics step towards `aim` at up to targetSpeed (call from FixedUpdate).</summary>
        public void Drive(Vector3 aim, float targetSpeed, float dt)
        {
            if (Crashed) return;
            float acc = targetSpeed > Speed ? 3.2f : 7.5f;
            Speed = Mathf.MoveTowards(Speed, Mathf.Max(0f, targetSpeed), acc * dt);
            Vector3 to = aim - transform.position;
            to.y = 0f;
            float want = to.sqrMagnitude > 0.04f ? Vector3.SignedAngle(Forward, to, Vector3.up) : 0f;
            // standing or crawling, the rider walks it round (a U-turn on the spot); faster, the turn rate falls with speed
            float maxRate = Speed < 0.3f ? (targetSpeed > 0.2f ? 80f : 0f) : Mathf.Lerp(140f, 50f, Mathf.Clamp01(Speed / 14f));
            if (Mathf.Abs(want) > 70f) Speed = Mathf.Min(Speed, 1.5f);
            yawRate = Mathf.MoveTowards(yawRate, Mathf.Clamp(want * 2.4f, -maxRate, maxRate), 300f * dt);
            yaw += yawRate * dt;
            Quaternion heading = Quaternion.Euler(0f, yaw, 0f);
            Vector3 fwd = heading * Vector3.forward;
            Vector3 p = transform.position + fwd * Speed * dt;
            Vector3 front = GroundAt(p + fwd * length * 0.35f, out _), back = GroundAt(p - fwd * length * 0.35f, out _);
            p.y = (front.y + back.y) * 0.5f;
            float pitch = Mathf.Clamp(-Mathf.Atan2(front.y - back.y, length * 0.7f) * Mathf.Rad2Deg, -14f, 14f);   // a kerb under one wheel is not a ramp
            this.pitch = Mathf.MoveTowards(this.pitch, pitch, 40f * dt);
            pitch = this.pitch;
            rb.MovePosition(p);
            rb.MoveRotation(heading * Quaternion.Euler(pitch, 0f, 0f));
            lean = Mathf.MoveTowards(lean, Mathf.Clamp(-yawRate * Speed * 0.03f, -30f, 30f), 90f * dt);
            if (model) model.localRotation = Quaternion.Euler(0f, 0f, lean);
        }

        /// <summary>Standing on its side stand (parked, the rider off): a slight lean to the left.</summary>
        public void Park()
        {
            Speed = 0f;
            yawRate = 0f;
            lean = 9f;
            if (model) model.localRotation = Quaternion.Euler(0f, 0f, lean);
        }

        void FixedUpdate()
        {
            if (Crashed)
            {
                crashT += Time.fixedDeltaTime;
                if (crashT > 6f && !rb.isKinematic && rb.linearVelocity.sqrMagnitude < 0.05f) rb.isKinematic = true;   // lies where it fell
                return;
            }
            // the player's car about to hit it: become a real body before the contact, so the car ploughs through
            var pc = CarController.Driving;
            if (!pc) return;
            var crb = pc.GetComponent<Rigidbody>();
            if (!crb || crb.isKinematic) return;
            Vector3 rel = crb.linearVelocity - transform.forward * Speed;
            if (rel.magnitude < 2.5f) return;
            var info = pc.GetComponent<VehicleInfo>();
            float hl = (info ? info.length : 4.4f) * 0.5f + 0.5f + rel.magnitude * 0.05f, hw = (info ? info.width : 1.8f) * 0.5f + 0.5f;
            Vector3 local = pc.transform.InverseTransformPoint(transform.position + Vector3.up * 0.6f);
            if (Mathf.Abs(local.x) < hw && Mathf.Abs(local.z) < hl && local.y > -0.5f && local.y < 2.5f)
                Crash(crb.linearVelocity, pc.gameObject);
        }

        /// <summary>Knocked over: a physics body from here on (falls, tumbles, lies there).</summary>
        public void Crash(Vector3 carVelocity, GameObject by)
        {
            if (Crashed) return;
            Crashed = true;
            crashT = 0f;
            Vector3 v = transform.forward * Speed;
            Speed = 0f;
            rb.isKinematic = false;
            rb.linearVelocity = v * 0.4f + carVelocity * 0.75f + Vector3.up * 2.5f;
            rb.angularVelocity = new Vector3(Random.Range(-3f, 3f), Random.Range(-2f, 2f), Random.Range(-6f, 6f));
            if (AudioManager.Instance) AudioManager.Instance.PlayAt(AudioManager.Instance.Clip("crash_1"), transform.position, 0.8f, 1.15f, 90f);
            OnCrash?.Invoke(carVelocity, by);
        }
    }
}
