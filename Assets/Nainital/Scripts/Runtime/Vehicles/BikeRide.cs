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
        /// <summary>The handlebar's angle now, degrees (+ = right): what the turn rate at this speed needs (a bike turns at
        /// speed * tan(steer) / wheelbase). VehicleParts turns the handlebar and the front wheel by it.</summary>
        public float Steer { get; private set; }
        /// <summary>The model's parts and seats (the unbranded set; null for the old Splendor / scooty).</summary>
        public VehicleParts Parts { get; private set; }
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
            info.model = e.name;
            if (e.parts)
            {
                // the unbranded set: wheels spin, the handlebar steers with Wheel_F under it, the rider sits where the
                // setup says (on the leaning body), the setup's lamps (the headlight on the handlebar turns with it)
                var parts = root.AddComponent<VehicleParts>();
                parts.Init(body.transform, e.name, lean);
                b.Parts = parts;
                info.wheelRadius = parts.wheelRadius;
                var s0 = parts.SeatData(0);
                if (s0 != null && s0.surface > 0f) b.seatTop = s0.surface;
                VehicleFactory.LampMaterials(out var hm, out var tm);
                parts.BuildLamps(hm, tm, out var heads, out var tails);
                if (heads.Length > 0) { var vl = root.AddComponent<VehicleLamps>(); vl.lamps = heads[0]; vl.more = heads; parts.BuildIndicators(hm, out vl.indL, out vl.indR); }
                if (tails.Length > 0) root.AddComponent<VehicleFX>().tails = tails;
            }
            root.AddComponent<VehicleSound>();   // its own engine (it had none), horn, the fall
            return root;
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (groundMask < 0) groundMask = ~((1 << 8) | (1 << 9) | (1 << VehicleFactory.VehicleLayer) | (1 << 2) | (1 << 4));
            yaw = transform.eulerAngles.y;
        }

        /// <summary>Where a rider's root sits (bike space): the setup's rider seat, else astride the seat a little behind
        /// the middle.</summary>
        public Vector3 SeatLocal => Parts && Parts.HasSetup ? Parts.SeatLocal(0) : new Vector3(0f, seatTop + 0.05f - 0.51f, -length * 0.1f);

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

        /// <summary>One physics step towards `aim` at up to targetSpeed (call from FixedUpdate). walkRound: standing, the
        /// rider walks it round on the spot even with nowhere to go ahead (the player, facing a wall).</summary>
        public void Drive(Vector3 aim, float targetSpeed, float dt, bool walkRound = false)
        {
            if (Crashed) return;
            float acc = targetSpeed > Speed ? 3.2f : 7.5f;
            Speed = Mathf.MoveTowards(Speed, Mathf.Max(0f, targetSpeed), acc * dt);
            Vector3 to = aim - transform.position;
            to.y = 0f;
            float want = to.sqrMagnitude > 0.04f ? Vector3.SignedAngle(Forward, to, Vector3.up) : 0f;
            // standing or crawling, the rider walks it round (a U-turn on the spot); faster, the turn rate falls with speed
            float maxRate = Speed < 0.3f ? (targetSpeed > 0.2f || walkRound ? 80f : 0f) : Mathf.Lerp(140f, 50f, Mathf.Clamp01(Speed / 14f));
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
            if (backT > 0f && BackOut(dt)) return;
            bool intoCar = IntoVehicle(p, heading);
            bool pinned = false;
            if (!intoCar && avoidStatics && IntoStatic(p, heading * Quaternion.Euler(pitch, 0f, 0f)))
            {
                // a kinematic body went through boards, the toll booth, parapets: along the nearest clear heading instead
                intoCar = true;
                pinned = true;
                float creep = Mathf.Clamp(Speed, 1.2f, 2.5f);
                foreach (float a in SideSteps)
                {
                    Quaternion h2 = Quaternion.Euler(0f, yaw + a, 0f);
                    Vector3 q = transform.position + h2 * Vector3.forward * creep * dt;
                    q.y = p.y;
                    if (IntoStatic(q, h2) || IntoVehicle(q, h2)) continue;
                    // it turns only a few degrees a step: clear as it will really stand, not just as it will end up
                    float yawNow = yaw + Mathf.Clamp(a, -200f * dt, 200f * dt);
                    Quaternion h3 = Quaternion.Euler(0f, yawNow, 0f);
                    if (IntoStatic(q, h3 * Quaternion.Euler(pitch, 0f, 0f))) continue;
                    yaw = yawNow;
                    heading = h3;
                    p = q;
                    Speed = creep;
                    intoCar = false;
                    pinned = false;
                    break;
                }
            }
            if (intoCar && avoidStatics && IntoStatic(transform.position, heading * Quaternion.Euler(pitch, 0f, 0f)) && !IntoStatic(transform.position, transform.rotation))
            {
                // nor swung round into it on the spot (22 Nov 2026: into the Army Rest wall at the narrow lane's corner)
                yaw = transform.eulerAngles.y;
                yawRate = 0f;
                heading = Quaternion.Euler(0f, yaw, 0f);
            }
            // no way on at all: stop, then walk it back and round (the owner's rule: hit -> stop -> back -> get out)
            pinnedT = pinned ? pinnedT + dt : 0f;
            if (pinnedT > 0.6f) { pinnedT = 0f; backT = 1.4f; backSide = want >= 0f ? 1f : -1f; }
            if (intoCar) { p = transform.position; Speed = 0f; }   // a kinematic body: nothing else stops it going into a car
            rb.MovePosition(p);
            rb.MoveRotation(heading * Quaternion.Euler(pitch, 0f, 0f));
            PushPeople(p, heading);
            lean = Mathf.MoveTowards(lean, Mathf.Clamp(-yawRate * Speed * 0.03f, -30f, 30f), 90f * dt);
            if (model) model.localRotation = Quaternion.Euler(0f, 0f, lean);
            // the handlebar for this turn rate: tan(steer) = yaw rate * wheelbase / speed (walked round: full lock)
            float wb = length * 0.66f;
            float want2 = Mathf.Atan2(yawRate * Mathf.Deg2Rad * wb, Mathf.Max(Speed, 0.6f)) * Mathf.Rad2Deg;
            Steer = Mathf.MoveTowards(Steer, Mathf.Clamp(want2, -35f, 35f), 120f * dt);
        }

        float pinnedT, backT, backSide;

        /// <summary>Walked backwards a little, the nose coming round to where it wants to go; false: no room behind.</summary>
        bool BackOut(float dt)
        {
            backT -= dt;
            float yawNow = yaw + backSide * 45f * dt;
            Quaternion h = Quaternion.Euler(0f, yawNow, 0f);
            Vector3 q = transform.position - Forward * 1.1f * dt;
            q.y = GroundAt(q, out _).y;
            // (already touching it, it may move away from it: only never deeper into a vehicle)
            if (IntoVehicle(q, h) || (IntoStatic(q, h) && !IntoStatic(transform.position, transform.rotation))) { backT = 0f; return false; }
            yaw = yawNow;
            yawRate = 0f;
            Speed = 0f;
            rb.MovePosition(q);
            rb.MoveRotation(h * Quaternion.Euler(pitch, 0f, 0f));
            return true;
        }

        static readonly Collider[] near = new Collider[8];
        static readonly Collider[] nearCars = new Collider[8];
        BoxCollider box;

        /// <summary>The traffic's bikes (TrafficBike): solid to static things, steering round them.</summary>
        [System.NonSerialized] public bool avoidStatics;
        static readonly float[] SideSteps = { -20f, 20f, -40f, 40f, -65f, 65f, -90f, 90f, -125f, 125f };
        static readonly Collider[] nearStatic = new Collider[8];

        /// <summary>Its own wall test where it stands now (checks: TrafficLab.Inside).</summary>
        public bool InStaticNow => IntoStatic(transform.position, transform.rotation);

        bool IntoStatic(Vector3 at, Quaternion rot)
        {
            if (!box) box = GetComponent<BoxCollider>();
            if (!box) return false;
            Vector3 half = new Vector3(box.size.x * 0.45f, 0.45f, box.size.z * 0.43f);   // 0.4-1.3 m up: clear of kerbs and the road, not of low stalls
            int n = Physics.OverlapBoxNonAlloc(at + rot * new Vector3(box.center.x, 0.85f, box.center.z), half, nearStatic, rot,
                                               ~((1 << 2) | (1 << 4) | (1 << 8) | (1 << 9) | (1 << VehicleFactory.VehicleLayer)), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (!(nearStatic[i] is TerrainCollider) && !nearStatic[i].transform.IsChildOf(transform)) return true;
            return false;
        }

        /// <summary>The vehicle it stopped against in its last step (TrafficBike goes round it), or null.</summary>
        public Rigidbody BlockedBy { get; private set; }

        /// <summary>Would this step take the bike's box into another vehicle? It moves itself (kinematic), so physics never
        /// stopped it: a traffic scooty went 0.3 m into a car at a junction, the car's side out of its forward look (owner,
        /// 22 Nov 2026: "gaadiyan aar paar jaari hain"; TrafficLab.Overlaps). Touching one already, only a step along or
        /// away from it is allowed - never deeper.</summary>
        bool IntoVehicle(Vector3 at, Quaternion heading)
        {
            BlockedBy = null;
            if (!box) box = GetComponent<BoxCollider>();
            if (!box) return false;
            int n = Physics.OverlapBoxNonAlloc(at + heading * box.center, box.size * 0.48f, nearCars, heading, 1 << VehicleFactory.VehicleLayer, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = nearCars[i];
                if (!c || c.transform.IsChildOf(transform) || (c.attachedRigidbody && c.attachedRigidbody == rb)) continue;
                if (Physics.ComputePenetration(box, box.transform.position, box.transform.rotation, c, c.transform.position, c.transform.rotation, out var outWay, out _))
                {
                    Vector3 step = at - transform.position;
                    step.y = outWay.y = 0f;
                    if (Vector3.Dot(step.normalized, outWay.normalized) >= -0.15f) continue;
                }
                BlockedBy = c.attachedRigidbody;
                return true;
            }
            return false;
        }

        /// <summary>The bike moves itself (a kinematic body): nothing physical stops it at a person, so it went straight
        /// through the player (owner, 21 Nov 2026: "ek gaadi mere aarpaar chala gaya"). Anyone (the player, a townsperson)
        /// standing in its box at its new place is shoved out sideways, the shorter way, as a car's body would.</summary>
        void PushPeople(Vector3 at, Quaternion heading)
        {
            if (!box) box = GetComponent<BoxCollider>();
            if (!box) return;
            Vector3 half = box.size * 0.5f;
            Vector3 c = at + heading * box.center;
            int n = Physics.OverlapBoxNonAlloc(c, half, near, heading, (1 << 8) | (1 << 9), QueryTriggerInteraction.Ignore);
            Vector3 right = heading * Vector3.right;
            for (int i = 0; i < n; i++)
            {
                var cc = near[i] as CharacterController;
                if (!cc) cc = near[i].GetComponentInParent<CharacterController>();
                if (!cc || !cc.enabled || cc.transform.IsChildOf(transform)) continue;   // its rider and pillion sit on it
                Vector3 rel = cc.transform.position - c;
                float lat = Vector3.Dot(rel, right);
                float need = half.x + cc.radius + 0.03f - Mathf.Abs(lat);
                if (need <= 0f) continue;
                cc.Move(right * (lat >= 0f ? need : -need));
            }
        }

        /// <summary>Standing on its side stand (parked, the rider off): a slight lean to the left.</summary>
        public void Park()
        {
            Speed = 0f;
            yawRate = 0f;
            lean = 9f;
            Steer = -20f;   // on its side stand the handlebar is turned to the left
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

        /// <summary>Down and no longer tumbling (it can be picked up).</summary>
        public bool Settled => !Crashed || rb.isKinematic || (rb.linearVelocity.sqrMagnitude < 0.5f && rb.angularVelocity.sqrMagnitude < 1f);

        /// <summary>Picked up off the ground (owner, 21 Nov 2026: "maine ek bnde ko bike se giraya..but main uski bike ko drive
        /// kyun nhi krapata"): upright on its wheels where it lies, facing the way it points, moving itself again.</summary>
        public void Recover()
        {
            if (!Crashed) return;
            if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            rb.isKinematic = true;
            Crashed = false;
            crashT = 0f;
            Vector3 f = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (f.sqrMagnitude < 0.01f) f = Vector3.ProjectOnPlane(transform.up, Vector3.up);   // lying on its nose / tail
            Place(transform.position, f);
            lean = 0f; pitch = 0f; yawRate = 0f; Steer = 0f;
            if (model) model.localRotation = Quaternion.identity;
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
            var vs = GetComponent<VehicleSound>();
            if (vs) vs.Fall(carVelocity.magnitude, transform.position);
            else if (AudioManager.Instance) AudioManager.Instance.PlayAt(AudioManager.Instance.Clip("crash_1"), transform.position, 0.8f, 1.15f, 90f);
            OnCrash?.Invoke(carVelocity, by);
        }
    }
}
