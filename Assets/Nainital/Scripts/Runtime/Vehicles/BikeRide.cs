using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// A two-wheeler (the unbranded set's bikes and scooties, the police bike): a real physics body of its real mass (the
    /// bike + 70 kg for everyone on it). Its tyres push it with at most what their grip gives (mu * m * g, forwards,
    /// braking and sideways together), the rider turns it no faster than that grip holds, a suspension follow keeps it on the
    /// ground (rays at its two ends) and upright (the lean is the model's only), and the box rides 0.3 m up so kerbs and
    /// crests do not catch it. Everything else is PhysX with real masses (owner, 22-23 Nov 2026: "ek bike waala meri bus ko
    /// hi dhakka lgaate huye peeche leaaya ... wheres physics?", "badi gaadi choti ko dhakka maarske ... based on real
    /// calculation"): a bike cannot push a bus further than its tyre grip, a bus shoves a bike aside, a hit that changes the
    /// bike's speed by more than KnockDv (or a vehicle shoving it sideways) knocks it over: from then on a free body that
    /// tumbles, slides and lies there, and whoever rode it is thrown (OnCrash). Used by the town's traffic (TrafficBike), the
    /// player (PlayerBike) and the police (PoliceBike).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BikeRide : MonoBehaviour
    {
        public float length = 2f, width = 0.9f, height = 1.3f, seatTop = 0.78f;
        public Transform model;      // the visual (leans); riders sit under it
        /// <summary>Its speed along the way it faces, m/s (0 going backwards).</summary>
        public float Speed { get; private set; }
        public bool Crashed { get; private set; }
        /// <summary>The handlebar's angle now, degrees (+ = right): what the turn rate at this speed needs (a bike turns at
        /// speed * tan(steer) / wheelbase). VehicleParts turns the handlebar and the front wheel by it.</summary>
        public float Steer { get; private set; }
        /// <summary>The model's parts and seats (the unbranded set; null for the old Splendor / scooty).</summary>
        public VehicleParts Parts { get; private set; }
        public Vector3 Forward => new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        /// <summary>Knocked over: (the velocity its riders are thrown with, what hit it or null).</summary>
        public event System.Action<Vector3, GameObject> OnCrash;
        /// <summary>The bike alone, kg (its riders come on top: Mass).</summary>
        public float bareMass = 135f;
        public float Mass => rb ? rb.mass : bareMass;

        // real numbers: tyre grip on dry asphalt, a person, the engine's pull and the brakes
        public const float Grip = 0.9f, Person = 70f, Pull = 3.2f, Brake = 7.5f;
        /// <summary>A hit that changes its velocity by more than this (m/s) sideways knocks it over; from the front / behind it
        /// takes KnockDvFront (~13 km/h: the rider puts a foot down below that).</summary>
        public const float KnockDv = 2f, KnockDvFront = 3.5f;
        const float Clearance = 0.3f;   // the box's underside over the ground while it stands on its wheels

        Rigidbody rb;
        BoxCollider box;
        float yawRate, lean, yaw, crashT, pitch, cmd, lastDrive = -1f, loadT, slipT, pushT;
        Vector3 preVel;
        Rigidbody pusher;
        static int groundMask = -1;
        static PhysicsMaterial shell;

        public static GameObject Create(VehicleLibrary.Entry e, string name)
        {
            var root = new GameObject(name ?? e.name) { layer = VehicleFactory.VehicleLayer };
            var lean = new GameObject("Lean").transform;
            lean.SetParent(root.transform, false);
            var body = Object.Instantiate(e.prefab, lean, false);
            body.name = "Body";
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = VehicleFactory.VehicleLayer;
            var box = root.AddComponent<BoxCollider>();
            var rb = root.AddComponent<Rigidbody>();
            var b = root.AddComponent<BikeRide>();
            b.model = lean;
            b.length = e.size.z;
            b.width = e.size.x;
            b.height = e.size.y;
            b.seatTop = e.size.y * 0.57f;
            bool scooty = e.name.ToLowerInvariant().Contains("scoot");
            b.bareMass = e.mass > 40f && e.mass < 400f ? e.mass : scooty ? 105f : 135f;
            b.Shape(false);
            rb.mass = b.bareMass;
            b.Upright();
            var info = root.AddComponent<VehicleInfo>();   // NpcWalker / TrafficCar read the size
            info.length = e.size.z;
            info.width = Mathf.Min(e.size.x, 0.8f);
            info.height = e.size.y;
            info.wheelRadius = 0.3f;
            info.model = e.name;
            info.mass = b.bareMass;
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
            box = GetComponent<BoxCollider>();
            if (groundMask < 0) groundMask = ~((1 << 8) | (1 << 9) | (1 << VehicleFactory.VehicleLayer) | (1 << 2) | (1 << 4));
            yaw = transform.eulerAngles.y;
        }

        /// <summary>The box: on its wheels it rides Clearance up (kerbs, crests and the road's 1.46 m height steps do not
        /// catch it; a car's bumper still meets it); knocked over, the whole bike down to the tyres.</summary>
        void Shape(bool down)
        {
            if (!box) box = GetComponent<BoxCollider>();
            float top = height * 0.85f, bottom = down ? height * 0.05f : Mathf.Min(Clearance, top - 0.4f);
            box.center = new Vector3(0f, (top + bottom) * 0.5f, 0f);
            box.size = new Vector3(Mathf.Min(width, 0.7f), top - bottom, length * 0.95f);
            if (!shell)
            {
                // the frame / fairing on the road or against a car: it slides (steel and plastic on asphalt)
                shell = new PhysicsMaterial("Bike shell") { dynamicFriction = 0.45f, staticFriction = 0.5f, bounciness = 0.05f,
                                                            frictionCombine = PhysicsMaterialCombine.Average, bounceCombine = PhysicsMaterialCombine.Minimum };
            }
            box.sharedMaterial = shell;
        }

        /// <summary>On its wheels: the rider holds it up and the suspension holds its height (no gravity on the body), the
        /// centre of mass where a loaded bike has it.</summary>
        void Upright()
        {
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.5f;
            rb.maxAngularVelocity = 30f;
            rb.automaticCenterOfMass = false;
            rb.centerOfMass = new Vector3(0f, height * 0.42f, 0f);
        }

        static readonly List<NpcWalker> aboard = new List<NpcWalker>(4);

        /// <summary>Its mass now: the bike + everyone on it (the townsfolk sit under it, the player rides it).</summary>
        void Weigh()
        {
            GetComponentsInChildren(true, aboard);
            int n = aboard.Count + (PlayerOn ? 1 : 0);
            rb.mass = bareMass + n * Person;
        }

        bool PlayerOn => PlayerBike.Riding && PlayerBike.Riding.gameObject == gameObject;

        /// <summary>Where a rider's root sits (bike space): the setup's rider seat, else astride the seat a little behind
        /// the middle.</summary>
        public Vector3 SeatLocal => Parts && Parts.HasSetup ? Parts.SeatLocal(0) : new Vector3(0f, seatTop + 0.05f - 0.51f, -length * 0.1f);

        /// <summary>Place it on the ground at p facing f, standing still.</summary>
        public void Place(Vector3 p, Vector3 f)
        {
            f.y = 0f;
            if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
            yaw = Quaternion.LookRotation(f.normalized).eulerAngles.y;
            Speed = cmd = 0f;
            Vector3 g = GroundAt(p, out _);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            transform.SetPositionAndRotation(g, rot);
            if (!rb) rb = GetComponent<Rigidbody>();
            if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            rb.position = g;
            rb.rotation = rot;
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

        /// <summary>A traffic / police rider: one physics step towards `aim` at up to targetSpeed (call from FixedUpdate). The
        /// rider works the same throttle, brakes and handlebar the player has (Machine). walkRound: unused (the player
        /// rides with Ride now; nothing turns on the spot).</summary>
        public void Drive(Vector3 aim, float targetSpeed, float dt, bool walkRound = false)
        {
            if (Crashed) return;
            lastDrive = Time.fixedTime;
            Step(aim, targetSpeed, dt, false);
        }

        /// <summary>The player riding (PlayerBike): throttle 0..1, brake 0..1, handlebar -1..1, its top speed; paddleBack:
        /// standing, the rider walks it backwards with his feet. Straight to the machine, no assists: what it runs into, it
        /// hits (owner, 22 Nov 2026: "auto braking kyun hori...khade khade mudri hai...actual drive do na usko").</summary>
        public void Ride(float throttle, float brake, float steer, float top, bool paddleBack, float dt)
        {
            if (Crashed) return;
            lastDrive = Time.fixedTime;
            Vector3 v = rb.linearVelocity;
            float vf = Vector3.Dot(new Vector3(v.x, 0f, v.z), Forward);
            float paddle = paddleBack && vf < 0.3f && throttle < 0.05f ? 0.9f : 0f;
            Machine(throttle, paddle > 0f ? 0f : brake, steer * 35f, top, paddle, dt);
        }

        // ---- the machine: real forces from the rider's hands (owner, 22 Nov 2026: "actual drive do na usko")
        /// <summary>m/s2: the engine held shut (off the throttle) slows it this much on top of the road and the air.</summary>
        const float EngineBrake = 0.6f;
        float steerDeg;
        float Wheelbase => Mathf.Max(0.9f, length * 0.66f);

        /// <summary>One physics step of the bike from its controls. throttle 0..1, brake 0..1, steerTo: the handlebar angle the
        /// rider turns towards (deg, + right; the lock he can use falls with speed), top: its top speed (the engine's pull
        /// falls to nothing there), paddle: walked backwards with the feet at this speed (m/s, 0 = no).
        /// It turns only as it rolls (omega = v tan(steer) / wheelbase), never tighter than the tyres hold (v omega <=
        /// 0.85 mu g); the engine, the brakes and the sideways grip share the tyres' grip; rolling resistance, the air
        /// (1/2 rho CdA v^2) and the engine braking slow it off the throttle; the slope pulls it (g sin); it leans as a bike
        /// must (tan(lean) = v omega / g). Standing with no throttle, the rider holds it (brake, feet down).</summary>
        void Machine(float throttle, float brake, float steerTo, float top, float paddle, float dt)
        {
            const float g = 9.81f;
            Vector3 pos = rb.position, v = rb.linearVelocity;
            preVel = v;
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            float vf = Vector3.Dot(flat, Forward);

            // the handlebar, turned at a rider's pace, and the turn it gives at this speed
            float lockDeg = Mathf.Lerp(35f, 7f, Mathf.Clamp01(Mathf.Abs(vf) / 16f));
            steerDeg = Mathf.MoveTowards(steerDeg, Mathf.Clamp(steerTo, -lockDeg, lockDeg), 150f * dt);
            Steer = steerDeg;
            float omega = vf * Mathf.Tan(steerDeg * Mathf.Deg2Rad) / Wheelbase;   // rad/s, + = right
            float omegaMax = 0.85f * Grip * g / Mathf.Max(Mathf.Abs(vf), 0.5f);
            omega = Mathf.Clamp(omega, -omegaMax, omegaMax);
            yawRate = omega * Mathf.Rad2Deg;
            yaw += yawRate * dt;
            Quaternion heading = Quaternion.Euler(0f, yaw, 0f);
            Vector3 fwd = heading * Vector3.forward;

            // the ground under both wheels: its height, its pitch, the slope's pull (+ = uphill)
            Vector3 front = GroundAt(pos + fwd * length * 0.45f, out _), rear = GroundAt(pos - fwd * length * 0.45f, out _);
            float groundY = (front.y + rear.y) * 0.5f;
            float pt = Mathf.Clamp(-Mathf.Atan2(front.y - rear.y, length * 0.9f) * Mathf.Rad2Deg, -18f, 18f);   // a kerb under one wheel is not a ramp
            pitch = Mathf.MoveTowards(pitch, pt, 40f * dt);
            float slopeSin = Mathf.Sin(-pitch * Mathf.Deg2Rad);

            // along the road and across it: what the tyres do, inside their grip; then gravity along the slope
            vf = Vector3.Dot(flat, fwd);
            Vector3 vlat = flat - fwd * vf;
            Vector3 tyre;
            if (paddle > 0f)
                tyre = fwd * (Mathf.Clamp((-paddle - vf) / dt, -1.5f, 1.5f) + g * slopeSin) - vlat / dt;   // the feet hold the slope too
            else
            {
                float drive = throttle * Pull * Mathf.Clamp01((top - vf) / (top * 0.6f));
                float resist = 0.015f * g + 0.36f * vf * vf / Mathf.Max(60f, rb.mass) + (throttle < 0.05f ? EngineBrake : 0f) + brake * Brake;
                if (Mathf.Abs(vf) < 0.25f && drive < 0.05f)
                    tyre = -flat / dt + fwd * (g * slopeSin);   // standing: held where it is, the slope too
                else
                {
                    float aLong = drive - Mathf.Sign(vf) * resist;
                    // the brakes and the road stop it, they never send it back
                    if (drive < 0.05f && Mathf.Abs(vf) > 0f && Mathf.Sign(vf + (aLong - g * slopeSin) * dt) != Mathf.Sign(vf)) aLong = -vf / dt + g * slopeSin;
                    tyre = fwd * aLong - vlat / dt;
                }
            }
            float grip = Grip * g;
            if (tyre.sqrMagnitude > grip * grip) tyre = tyre.normalized * grip;
            rb.AddForce((tyre - fwd * (g * slopeSin)) * rb.mass, ForceMode.Force);
            Speed = Mathf.Max(0f, vf);

            // the suspension: follows the ground (a fall off a wall is a fall: gravity until it is close again)
            float e = groundY - pos.y;
            float vy = e > -0.6f ? Mathf.Clamp(e * 15f, -4f, 4f) : v.y - g * dt;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, vy, rb.linearVelocity.z);
            // upright on its heading and the road's pitch: turned there by angular velocity, so contacts see the turn
            Quaternion target = heading * Quaternion.Euler(pitch, 0f, 0f);
            (target * Quaternion.Inverse(rb.rotation)).ToAngleAxis(out float ang, out Vector3 axis);
            if (ang > 180f) ang -= 360f;
            rb.angularVelocity = float.IsFinite(axis.x) && Mathf.Abs(ang) > 0.01f ? axis * (ang * Mathf.Deg2Rad / dt) : Vector3.zero;

            PushPeople(pos + flat * dt, heading);
            // the lean a turn needs (tan = v omega / g): the model leans, the body stays upright
            lean = Mathf.MoveTowards(lean, Mathf.Clamp(-Mathf.Atan2(vf * omega, g) * Mathf.Rad2Deg, -40f, 40f), 120f * dt);
            if (model) model.localRotation = Quaternion.Euler(0f, 0f, lean);
        }

        /// <summary>A rider of the town (traffic, police): from where he wants to go and how fast to the throttle, brakes and
        /// handlebar. hold: nobody on it (parked): brakes on, straight, none of a rider's steering or backing out.</summary>
        void Step(Vector3 aim, float targetSpeed, float dt, bool hold)
        {
            Vector3 pos = rb.position;
            Vector3 v = rb.linearVelocity;
            float vf = Vector3.Dot(new Vector3(v.x, 0f, v.z), Forward);
            Speed = Mathf.Max(0f, vf);
            // the speed he wants: up as the engine gives it, down at once (the brakes below get him there)
            cmd = targetSpeed > cmd ? Mathf.MoveTowards(cmd, targetSpeed, Pull * dt) : Mathf.Max(0f, targetSpeed);
            Vector3 to = aim - pos;
            to.y = 0f;
            float want = to.sqrMagnitude > 0.04f ? Vector3.SignedAngle(Forward, to, Vector3.up) : 0f;
            if (Mathf.Abs(want) > 70f) cmd = Mathf.Min(cmd, 1.5f);   // a tight turn at a walking pace
            // to turn at all it has to roll: a slow full-lock circle (nothing turns on the spot)
            if (targetSpeed > 0.2f && Mathf.Abs(want) > 25f) cmd = Mathf.Max(cmd, 1f);
            float headTo = want;   // the heading change he steers for, deg
            float paddle = 0f;

            if (hold) { cmd = 0f; headTo = 0f; }
            else if (backT > 0f)
            {
                // walked back a little with the feet, the nose coming round to where it wants to go (hit -> stop -> back -> out)
                backT -= dt;
                Vector3 q = pos - Forward * 0.25f;
                if (IntoVehicle(q, transform.rotation) || (IntoStatic(q, transform.rotation) && !IntoStatic(pos, transform.rotation))) backT = 0f;   // no room behind
                else { paddle = 1.1f; cmd = 0f; }
            }
            else
            {
                Quaternion h = Quaternion.Euler(0f, yaw + Mathf.Clamp(headTo, -5f, 5f), 0f);
                // the rider's eyes, as far ahead as it takes him to stop: what he covers before he reacts (0.75 s) and while
                // braking (v^2 / 2a) - a real body stops by its brakes, in time, not on the wall or the car (22 Nov 2026:
                // with momentum, the traffic's bikes rode into houses and a standing taxi; the kinematic ones had stopped
                // dead at the touch)
                float brakeD = Speed * Speed / (2f * Brake);
                float reach = Speed * 0.75f + brakeD + 0.6f;
                if (Speed > 0.4f && !PlayerOn && Sweep(pos, h, reach, out float dist, out var car))
                {
                    // slow enough to stop short of it; one coming at us closes the gap too
                    float toward = car ? Mathf.Max(0f, -Vector3.Dot(car.linearVelocity, h * Vector3.forward)) : 0f;
                    float allowed = Mathf.Max(0f, Mathf.Sqrt(2f * Brake * Mathf.Max(0f, dist - 0.4f - Speed * 0.2f)) - toward);
                    if (!car && avoidStatics)
                    {
                        // a wall / post on its line: steer for the nearest clear heading, its way round first - and still
                        // slow enough to stop short of it, since the turn takes time at speed (22 Nov 2026: bikes that only
                        // turned hit a street light at 6-7 m/s)
                        float sideFirst = want >= 0f ? 1f : -1f;
                        foreach (float a0 in SideSteps)
                        {
                            float a = -a0 * sideFirst;
                            if (Sweep(pos, Quaternion.Euler(0f, yaw + a, 0f), reach, out _, out _)) continue;
                            headTo = a;
                            break;
                        }
                    }
                    cmd = Mathf.Min(cmd, allowed);
                    if (car) { blockedBy = car; blockedT = Time.fixedTime; }
                }
                Vector3 ahead = pos + h * Vector3.forward * Mathf.Max(0.15f, Speed * dt * 3f);
                bool intoCar = IntoVehicle(ahead, h);   // about to touch a car: brakes (BlockedBy for TrafficBike)
                bool pinned = false;
                if (!intoCar && avoidStatics && IntoStatic(ahead, h * Quaternion.Euler(pitch, 0f, 0f)))
                {
                    // boards, the toll booth, parapets right ahead: along the nearest clear heading at a crawl
                    pinned = true;
                    float creep = Mathf.Clamp(Speed, 1.2f, 2.5f);
                    foreach (float a in SideSteps)
                    {
                        Quaternion h2 = Quaternion.Euler(0f, yaw + a, 0f);
                        Vector3 q = pos + h2 * Vector3.forward * 0.3f;
                        if (IntoStatic(q, h2) || IntoVehicle(q, h2)) continue;
                        headTo = a;
                        cmd = Mathf.Min(Mathf.Max(cmd, 1.2f), creep);
                        pinned = false;
                        break;
                    }
                }
                if (pinned || intoCar) { cmd = 0f; headTo = 0f; }
                // no way on at all: stop, then walk it back and round (the owner's rule: hit -> stop -> back -> get out)
                pinnedT = pinned && Speed < 0.5f ? pinnedT + dt : 0f;
                if (pinnedT > 0.6f) { pinnedT = 0f; backT = 1.4f; backSide = want >= 0f ? 1f : -1f; }
            }

            // the handlebar for the turn he wants (the heading error closed in ~0.4 s) at this speed; backing, turned the
            // other way so the nose comes round to his side
            float steerTo = hold ? steerDeg : paddle > 0f ? -backSide * 30f
                                        : Mathf.Atan2(headTo * Mathf.Deg2Rad * 2.5f * Wheelbase, Mathf.Max(Mathf.Abs(vf), 1.2f)) * Mathf.Rad2Deg;
            // throttle and brakes for the speed he wants: brakes in proportion (full at 2.5 m/s too fast), held at a stop
            float throttle = 0f, brake = 0f, err = cmd - vf;
            if (paddle <= 0f)
            {
                if (cmd < 0.05f && vf < 0.5f) brake = 1f;
                else if (err > -0.3f) throttle = Mathf.Clamp01(err * 1.2f + 0.3f);
                else brake = Mathf.Clamp01((-err - 0.3f) / 2.5f);
            }
            Machine(throttle, brake, steerTo, 22f, paddle, dt);
        }

        float pinnedT, backT, backSide;

        static readonly RaycastHit[] sweepHits = new RaycastHit[12];
        const int SweepMask = ~((1 << 2) | (1 << 4) | (1 << 8) | (1 << 9));

        /// <summary>A slab the bike's width and height pushed from its nose along heading h for `reach` m: the nearest wall or
        /// vehicle (car: that vehicle, else null). The ground is not a wall: the terrain, and anything facing up (a ramp, a
        /// deck). False: clear.</summary>
        bool Sweep(Vector3 pos, Quaternion h, float reach, out float dist, out Rigidbody car)
        {
            dist = reach;
            car = null;
            if (!box) return false;
            Vector3 half = new Vector3(box.size.x * 0.5f, box.size.y * 0.5f, 0.1f);
            Vector3 from = pos + h * new Vector3(box.center.x, box.center.y, box.center.z + box.size.z * 0.5f - 0.1f);
            int n = Physics.BoxCastNonAlloc(from, half, h * Vector3.forward, sweepHits, h, reach, SweepMask, QueryTriggerInteraction.Ignore);
            bool any = false;
            for (int i = 0; i < n; i++)
            {
                var hit = sweepHits[i];
                if (hit.distance <= 0f || hit.collider is TerrainCollider || hit.normal.y > 0.6f || hit.collider.transform.IsChildOf(transform)) continue;
                var o = hit.rigidbody;
                if (o == rb && o) continue;
                if (hit.distance >= dist) continue;
                dist = hit.distance;
                car = o && o.gameObject.layer == VehicleFactory.VehicleLayer ? o : null;
                any = true;
            }
            return any;
        }

        static readonly Collider[] near = new Collider[8];
        static readonly Collider[] nearCars = new Collider[8];

        /// <summary>The traffic's bikes (TrafficBike): steering round static things.</summary>
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

        /// <summary>The vehicle it stopped for (ahead, or pushing against its side), or null: TrafficBike goes round it.</summary>
        public Rigidbody BlockedBy => Time.fixedTime - blockedT < 0.25f ? blockedBy : null;
        Rigidbody blockedBy;
        float blockedT = -1f;

        /// <summary>Would the bike's box at `at` be in another vehicle? Its rider brakes before it touches one (a traffic
        /// scooty went 0.3 m into a car at a junction, the car's side out of its forward look: owner, 22 Nov 2026 "gaadiyan
        /// aar paar jaari hain"). Touching one already, only a way along or away from it is free.</summary>
        bool IntoVehicle(Vector3 at, Quaternion heading)
        {
            if (!box) box = GetComponent<BoxCollider>();
            if (!box) return false;
            int n = Physics.OverlapBoxNonAlloc(at + heading * box.center, box.size * 0.5f, nearCars, heading, 1 << VehicleFactory.VehicleLayer, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = nearCars[i];
                if (!c || c.transform.IsChildOf(transform) || (c.attachedRigidbody && c.attachedRigidbody == rb)) continue;
                if (Physics.ComputePenetration(box, transform.position, transform.rotation, c, c.transform.position, c.transform.rotation, out var outWay, out _))
                {
                    Vector3 step = at - transform.position;
                    step.y = outWay.y = 0f;
                    if (Vector3.Dot(step.normalized, outWay.normalized) >= -0.15f) continue;
                }
                blockedBy = c.attachedRigidbody;
                blockedT = Time.fixedTime;
                return true;
            }
            return false;
        }

        /// <summary>People are CharacterControllers (no mass yet): anyone standing where the bike will be next step is moved
        /// out sideways, the shorter way (owner, 21 Nov 2026: "ek gaadi mere aarpaar chala gaya").</summary>
        void PushPeople(Vector3 at, Quaternion heading)
        {
            if (!box) box = GetComponent<BoxCollider>();
            if (!box) return;
            Vector3 half = box.size * 0.5f;
            half.y += Clearance * 0.5f;
            Vector3 c = at + heading * (box.center - Vector3.up * Clearance * 0.5f);
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
            cmd = 0f;
            yawRate = 0f;
            lean = 9f;
            Steer = steerDeg = -20f;   // on its side stand the handlebar is turned to the left
            if (model) model.localRotation = Quaternion.Euler(0f, 0f, lean);
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (Crashed)
            {
                crashT += dt;
                // lies where it fell: asleep, still a 130 kg body that a car shoves (never a kinematic wall again)
                if (crashT > 6f && !rb.IsSleeping() && rb.linearVelocity.sqrMagnitude < 0.05f && rb.angularVelocity.sqrMagnitude < 0.05f) rb.Sleep();
                return;
            }
            if ((loadT -= dt) <= 0f) { loadT = 0.5f; Weigh(); }
            // nobody riding it this step (parked, pulled up, handed over): brakes on, on its wheels
            if (Time.fixedTime - lastDrive > dt * 1.5f)
            {
                float l = lean;
                Step(rb.position + Forward * 4f, 0f, dt, true);
                lean = l;   // the side stand's lean stays
                if (model) model.localRotation = Quaternion.Euler(0f, 0f, lean);
            }
            // a vehicle shoving it sideways faster than a rider can hold it up: over it goes
            Vector3 v = rb.linearVelocity;
            float side = Mathf.Abs(Vector3.Dot(new Vector3(v.x, 0f, v.z), Vector3.Cross(Vector3.up, Forward)));
            slipT = Time.fixedTime - pushT < 0.2f && side > 1.2f ? slipT + dt : 0f;
            if (slipT > 0.25f && pusher) Crash(pusher.linearVelocity, pusher.gameObject, true);
        }

        /// <summary>Tests: log every contact that changed a bike's velocity by over 1.2 m/s.</summary>
        public static bool LogHits;

        void OnCollisionEnter(Collision c) => Hit(c);
        void OnCollisionStay(Collision c) => Hit(c);

        /// <summary>A contact PhysX solved with the real masses: the change in the bike's velocity it made (impulse / mass)
        /// over KnockDv knocks it over. A vehicle pressing on it is remembered (the sideways shove, TrafficBike's way round).</summary>
        void Hit(Collision c)
        {
            if (Crashed) return;
            var o = c.rigidbody;
            bool vehicle = o && o != rb && o.gameObject.layer == VehicleFactory.VehicleLayer;
            if (vehicle)
            {
                pusher = o;
                pushT = Time.fixedTime;
                blockedBy = o;
                blockedT = Time.fixedTime;
            }
            int layer = c.collider.gameObject.layer;
            if (layer == 8 || layer == 9 || c.collider is TerrainCollider) return;   // people (no mass yet), the ground
            float dv = c.impulse.magnitude / Mathf.Max(1f, rb.mass);
            if (dv > 1.2f && LogHits)
            {
                var k = c.GetContact(0);
                float close = Mathf.Abs(Vector3.Dot(c.relativeVelocity, k.normal));
                Debug.Log($"[Nainital] bike hit: {name} by {c.collider.name} (layer {layer}{(o ? $", {o.mass:F0} kg" : ", static")}) dv {dv:F1} m/s, closing {close:F1} m/s, " +
                          $"rel {c.relativeVelocity.magnitude:F1}, sep {k.separation:F2} m, normal y {k.normal.y:F2}, speed {Speed:F1}, at {transform.position:F0}");
            }
            // a rider holds a bump from the front (a foot down) far better than a blow from the side
            Vector3 dvv = c.impulse / Mathf.Max(1f, rb.mass);
            float dvSide = Mathf.Abs(Vector3.Dot(dvv, Vector3.Cross(Vector3.up, Forward))), dvLong = Mathf.Abs(Vector3.Dot(dvv, Forward));
            if (dvSide < KnockDv && dvLong < KnockDvFront) return;
            // its riders fly on with what the bike had (a wall) or with what hit it (a vehicle)
            Vector3 throwV = vehicle ? o.linearVelocity : preVel;
            Crash(throwV, vehicle ? o.gameObject : null, true);
        }

        /// <summary>Down and no longer tumbling (it can be picked up).</summary>
        public bool Settled => !Crashed || rb.IsSleeping() || (rb.linearVelocity.sqrMagnitude < 0.5f && rb.angularVelocity.sqrMagnitude < 1f);

        /// <summary>Picked up off the ground (owner, 21 Nov 2026: "maine ek bnde ko bike se giraya..but main uski bike ko drive
        /// kyun nhi krapata"): upright on its wheels where it lies, facing the way it points, ridden again.</summary>
        public void Recover()
        {
            if (!Crashed) return;
            Crashed = false;
            crashT = 0f;
            Shape(false);
            Upright();
            Vector3 f = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (f.sqrMagnitude < 0.01f) f = Vector3.ProjectOnPlane(transform.up, Vector3.up);   // lying on its nose / tail
            Place(transform.position, f);
            lean = 0f; pitch = 0f; yawRate = 0f; Steer = 0f; backT = 0f;
            if (model) model.localRotation = Quaternion.identity;
        }

        /// <summary>Knocked over: a free body from here on (falls, tumbles, slides, lies there). physical: PhysX already gave
        /// it the hit's velocity (a real contact); else (a test, an explosion) `carVelocity` is applied as the hit.</summary>
        public void Crash(Vector3 carVelocity, GameObject by, bool physical = false)
        {
            if (Crashed) return;
            Crashed = true;
            crashT = 0f;
            Speed = cmd = 0f;
            rb.isKinematic = false;
            Shape(true);
            rb.useGravity = true;
            rb.automaticCenterOfMass = false;
            rb.centerOfMass = new Vector3(0f, height * 0.4f, 0f);
            rb.mass = bareMass;   // its riders leave it (OnCrash)
            if (!physical)
            {
                rb.linearVelocity = preVel * 0.4f + carVelocity * 0.75f + Vector3.up * 2.5f;
                rb.angularVelocity = new Vector3(Random.Range(-3f, 3f), Random.Range(-2f, 2f), Random.Range(-6f, 6f));
            }
            else
            {
                // nobody holds it up any more: it goes over to the side it was pushed / leaning to
                Vector3 side = Vector3.Cross(Vector3.up, Forward);
                float s = Vector3.Dot(rb.linearVelocity - preVel, side) < 0f ? 1f : -1f;
                rb.angularVelocity += Forward * (s * Random.Range(1.5f, 3f));
            }
            if (model) model.localRotation = Quaternion.identity;
            var vs = GetComponent<VehicleSound>();
            if (vs) vs.Fall(Mathf.Max(carVelocity.magnitude, preVel.magnitude), transform.position);
            else if (AudioManager.Instance) AudioManager.Instance.PlayAt(AudioManager.Instance.Clip("crash_1"), transform.position, 0.8f, 1.15f, 90f);
            OnCrash?.Invoke(carVelocity, by);
        }
    }
}
