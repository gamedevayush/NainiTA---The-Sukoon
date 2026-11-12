using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// AI traffic. Drives the LEFT lane (India) along the OSM roads with the same physics as the player's car
    /// (CarController: suspension, tyre grip, its own mass), so a crash pushes the lighter car more (user, 2026-11-12:
    /// "AI vehicle bina physics ke chal rahi hai, gaadiyon ka apna apna mass hoga"). It steers for a point a little way
    /// up its lane, slows for bends, brakes for anything in front, carries on at junctions, and where it has to turn
    /// round it does what a driver does on a narrow hill road: a slow turn across, back up, and away (a three-point
    /// turn), never a sudden flip. Someone drives it: a seated townsperson (PedestrianManager.MakeDriver). When the player
    /// takes it, the car first pulls up, the driver gets out and walks off, and only then does the player get in.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class TrafficCar : MonoBehaviour
    {
        public RoadNetwork net;
        public CarType type;
        public Transform[] wheels;   // CarController turns them
        public float cruise = 11f;

        enum Mode { Drive, TurnAcross, TurnBack, PullUp, Wrecked }

        int road, idx, dir;
        Mode mode;
        float stuck, hornCooldown, blocked, turnTimer, shaken, pullTimer;
        int turnSteps;
        Vector3 turnHeading;
        bool passed;
        CarController car;
        Rigidbody rb;
        VehicleFactory.Spec spec;
        NpcWalker driver;
        PlayerController taker;
        static int obstacleMask = -1;

        public float Speed => car ? Mathf.Abs(car.ForwardSpeed) : 0f;
        public bool Wrecked => mode == Mode.Wrecked;
        public bool PullingUp => mode == Mode.PullUp;
        public string State => mode + (driver ? ", driver in" : ", no driver") + $", road {road} idx {idx} dir {dir} t {turnTimer:F1} steps {turnSteps} thr {(car ? car.aiThrottle : 0f):F2} steer {(car ? car.aiSteer : 0f):F0} hb {(car ? car.aiHandbrake : 0f):F0} stuck {stuck:F0}";
        public bool HasDriver => driver;
        /// <summary>Test hook (PlayTest.UTurn): turn round here.</summary>
        public void ForceTurnRound() { if (mode == Mode.Drive && net.roads[road].oneWay == 0) TurnRound(); }

        public void Begin(RoadNetwork n, int roadIndex, int startIdx, int direction)
        {
            net = n;
            road = roadIndex;
            idx = startIdx;
            dir = n.roads[roadIndex].oneWay != 0 ? n.roads[roadIndex].oneWay : direction;
            car = GetComponent<CarController>();
            rb = GetComponent<Rigidbody>();
            spec = car.Spec;
            if (obstacleMask < 0) obstacleMask = (1 << VehicleFactory.VehicleLayer) | (1 << 8) | (1 << 9);
            Vector3 p = Ground(Lane(idx));
            Vector3 f = Lane(idx + dir) - Lane(idx);
            f.y = 0;
            var rot = f.sqrMagnitude > 0.01f ? Quaternion.LookRotation(f.normalized, Vector3.up) : transform.rotation;
            transform.SetPositionAndRotation(p + Vector3.up * 0.35f, rot);
            Physics.SyncTransforms();
            car.ai = true;
            rb.isKinematic = false;
            rb.linearVelocity = rot * Vector3.forward * cruise * 0.5f;
            mode = Mode.Drive;
            var pm = PedestrianManager.Instance;
            if (pm) driver = pm.MakeDriver(this);
        }

        /// <summary>Where the driver sits: right-hand drive, low in the seat (the walker's root; hips 0.51 m above it).</summary>
        public Vector3 DriverSeatLocal()
        {
            var info = GetComponent<VehicleInfo>();
            float W = info ? info.width : spec.width, L = info ? info.length : spec.length, H = info ? info.height : spec.height;
            float R = info ? info.wheelRadius : spec.wheelRadius;
            bool bus = type == CarType.Bus || L > 6.5f;
            float hips = bus ? 1.35f : R + (H > 1.7f ? 0.36f : 0.22f);
            return new Vector3(W * 0.22f, hips - 0.51f, bus ? L * 0.40f : 0.05f);
        }

        Vector3[] Pts => net.roads[road].pts;

        Vector3 Lane(int i)
        {
            var pts = Pts;
            i = Mathf.Clamp(i, 0, pts.Length - 1);
            int j = Mathf.Clamp(i + dir, 0, pts.Length - 1);
            if (j == i) j = Mathf.Clamp(i - dir, 0, pts.Length - 1);
            Vector3 d = (j > i ? pts[j] - pts[i] : pts[i] - pts[j]) * (dir > 0 ? 1 : -1);
            d.y = 0;
            if (d.sqrMagnitude < 1e-4f) return pts[i];
            Vector3 left = Vector3.Cross(d.normalized, Vector3.up);   // Unity: forward x up = left
            float off = net.roads[road].oneWay != 0 ? 0f : Mathf.Max(1.1f, net.roads[road].width * 0.25f);   // one-way: down the middle
            return pts[i] + left * off;
        }

        static readonly int groundMask = ~((1 << VehicleFactory.VehicleLayer) | (1 << 8) | (1 << 9) | (1 << 2) | (1 << 4));

        static Vector3 Ground(Vector3 p)
        {
            var terrain = Terrain.activeTerrain;
            float t = terrain ? terrain.SampleHeight(p) + terrain.transform.position.y : p.y;
            if (Physics.Raycast(new Vector3(p.x, Mathf.Max(p.y, t) + 2.5f, p.z), Vector3.down, out var hit, 8f, groundMask, QueryTriggerInteraction.Ignore))
                p.y = hit.point.y + 0.02f;
            else p.y = t + 0.05f;
            return p;
        }

        /// <summary>Onto the next point of the lane; at the end of a road, onto a connected one, or turn round.</summary>
        void Advance()
        {
            idx += dir;
            var pts = Pts;
            if (idx > 0 && idx < pts.Length - 1) return;
            var links = net.Links(road, idx <= 0 ? 0 : 1);
            var ok = new List<(int r, int i, int d)>();   // onward roads, never the wrong way down a one-way road
            if (links != null)
                foreach (var (r, i) in links)
                {
                    if (r == road) continue;
                    var q = net.roads[r].pts;
                    int ow = net.roads[r].oneWay;
                    if (ow != 0)
                    {
                        if ((ow > 0 && i >= q.Length - 2) || (ow < 0 && i <= 1)) continue;   // would enter at its exit end
                        ok.Add((r, i, ow));
                    }
                    else ok.Add((r, i, i <= 0 ? 1 : i >= q.Length - 1 ? -1 : (Random.value < 0.5f ? 1 : -1)));
                }
            if (ok.Count > 0)
            {
                var c = ok[Random.Range(0, ok.Count)];
                road = c.r;
                idx = c.i;
                dir = c.d;
            }
            else if (net.roads[road].oneWay == 0) TurnRound();
            else Leave();   // the end of a one-way road with nowhere to go: this car leaves
        }

        /// <summary>Start a turn round in the road: slowly across it first (TurnAcross), backing up if it is narrow.</summary>
        void TurnRound()
        {
            dir = -dir;
            idx = Mathf.Clamp(idx, 0, Pts.Length - 1);
            Vector3 h = Lane(idx + dir * 2) - Lane(idx);
            h.y = 0;
            turnHeading = h.sqrMagnitude > 0.01f ? h.normalized : -transform.forward;
            mode = Mode.TurnAcross;
            turnTimer = 0f;
            turnSteps = 0;
            phaseStart = transform.position;
        }

        void Leave()
        {
            mode = Mode.Wrecked;
            Destroy(gameObject);   // OnDestroy takes the driver too
        }

        /// <summary>The lane point nearest the car, so after a turn it picks up the lane where it is.</summary>
        void Relocate()
        {
            var pts = Pts;
            Vector3 pos = transform.position;
            int best = idx;
            float bd = float.MaxValue;
            for (int k = Mathf.Max(0, idx - 15); k < Mathf.Min(pts.Length, idx + 16); k++)
            {
                float d = (new Vector2(pts[k].x - pos.x, pts[k].z - pos.z)).sqrMagnitude;
                if (d < bd) { bd = d; best = k; }
            }
            idx = Mathf.Clamp(best, 0, pts.Length - 1);
        }

        /// <summary>The point the car steers for, lookAhead metres up the lane from where it is.</summary>
        Vector3 Aim(float lookAhead)
        {
            Vector3 prev = transform.position;
            for (int k = 1; k < 14; k++)
            {
                int i = idx + dir * k;
                if (i < 0 || i >= Pts.Length) break;
                Vector3 q = Lane(i);
                float d = Vector3.Distance(new Vector3(prev.x, 0, prev.z), new Vector3(q.x, 0, q.z));
                if (d >= lookAhead) return prev + (q - prev) * (lookAhead / Mathf.Max(d, 0.01f));
                lookAhead -= d;
                prev = q;
            }
            return prev;
        }

        float SteerTo(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            return Mathf.Atan2(local.x, Mathf.Max(local.z, 0.1f)) * Mathf.Rad2Deg * 1.15f;
        }

        /// <summary>How far the given point is from the middle of the road (for a turn in a narrow road).</summary>
        float OffCentre(Vector3 p)
        {
            var pts = Pts;
            float best = float.MaxValue;
            for (int k = Mathf.Max(0, idx - 8); k < Mathf.Min(pts.Length - 1, idx + 8); k++)
            {
                Vector3 a = pts[k], b = pts[k + 1];
                Vector2 a2 = new Vector2(a.x, a.z), b2 = new Vector2(b.x, b.z), p2 = new Vector2(p.x, p.z);
                Vector2 ab = b2 - a2;
                float t = ab.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector2.Dot(p2 - a2, ab) / ab.sqrMagnitude) : 0f;
                best = Mathf.Min(best, Vector2.Distance(p2, a2 + ab * t));
            }
            return best;
        }

        bool Blocked(Vector3 dirWorld, float dist)
        {
            Vector3 from = transform.position + Vector3.up * 0.8f + dirWorld * (spec.length * 0.5f);
            return Physics.SphereCast(from, 0.6f, dirWorld, out var hit, dist, ~((1 << 2) | (1 << 4)), QueryTriggerInteraction.Ignore)
                   && !hit.collider.transform.IsChildOf(transform) && !(hit.collider is TerrainCollider && Vector3.Angle(hit.normal, Vector3.up) < 35f);
        }

        // ------------------------------------------------------------------ the player takes the car
        /// <summary>The player wants this car: pull up, let the driver out, then hand it over (TrafficCarEntry).</summary>
        public void RequestTakeover(PlayerController p)
        {
            if (mode == Mode.Wrecked) return;
            taker = p;
            mode = Mode.PullUp;
            pullTimer = 0f;
        }

        void HandOver()
        {
            var p = taker;
            taker = null;
            if (!p || CarController.Driving != null || (p.transform.position - transform.position).sqrMagnitude > 16f * 16f)
            {
                mode = Mode.Drive;   // the player walked off: someone else drives on
                if (!driver && PedestrianManager.Instance) driver = PedestrianManager.Instance.MakeDriver(this);
                var e = GetComponent<TrafficCarEntry>();
                if (e) e.Rearm();
                return;
            }
            car.ai = false;
            car.aiThrottle = car.aiSteer = car.aiHandbrake = 0f;
            var entry = GetComponent<TrafficCarEntry>();
            if (entry) Destroy(entry);
            Destroy(this);   // TrafficManager drops it from its list; the car is the player's now
            car.Interact(p);
        }

        void DriverOut()
        {
            if (!driver) return;
            var d = driver;
            driver = null;
            // out on the driver's side (right-hand drive) unless something stands there, then the other side
            Vector3 side = transform.right;
            Vector3 door = transform.position + side * (spec.width * 0.5f + 0.75f);
            if (Physics.CheckCapsule(door + Vector3.up * 0.5f, door + Vector3.up * 1.5f, 0.32f, ~((1 << 9) | (1 << 2)), QueryTriggerInteraction.Ignore))
            {
                side = -side;
                door = transform.position + side * (spec.width * 0.5f + 0.75f);
            }
            d.LeaveCar(Ground(door), side);
            if (AudioManager.Instance) AudioManager.Instance.CarExit(transform.position);
            string[] lines = { "Arey! Meri gaadi!", "Bhai sahab, ye kya kar rahe ho?", "Le jao, le jao... police ko bataunga!", "Oye! Ruk!" };
            GameEvents.ShowToast(d.npcName + ": \"" + lines[Random.Range(0, lines.Length)] + "\"", 2.5f);
        }

        // ------------------------------------------------------------------ crashes
        /// <summary>Hit hard (VehicleDamage): a moment's pause and a blast of the horn, then on.</summary>
        public void Shaken() { if (mode == Mode.Drive) { shaken = 1.4f; hornCooldown = 0f; blocked = 2f; } }

        /// <summary>On fire (VehicleDamage): the driver jumps out and runs, the car is dead.</summary>
        public void Wreck()
        {
            mode = Mode.Wrecked;
            car.aiThrottle = 0f;
            car.aiHandbrake = 1f;
            if (driver)
            {
                var d = driver;
                driver = null;
                d.LeaveCar(Ground(transform.position + transform.right * (spec.width * 0.5f + 0.9f)), transform.right);
                d.Flee(transform.right, 4f);
            }
        }

        void OnDestroy()
        {
            if (driver) Destroy(driver.gameObject);
        }

        // ------------------------------------------------------------------ driving
        void FixedUpdate()
        {
            if (!net || !car) return;
            float dt = Time.fixedDeltaTime;
            Vector3 pos = transform.position;
            float fwd = car.ForwardSpeed;
            if (mode == Mode.Wrecked) { car.aiThrottle = 0f; car.aiHandbrake = 1f; return; }

            if (mode == Mode.PullUp)
            {
                car.aiSteer = SteerTo(Aim(5f));
                car.aiThrottle = Mathf.Abs(fwd) > 0.4f ? -Mathf.Sign(fwd) * Mathf.Clamp01(Mathf.Abs(fwd) * 0.4f + 0.3f) : 0f;
                car.aiHandbrake = Mathf.Abs(fwd) < 1.2f ? 1f : 0f;
                if (Mathf.Abs(fwd) < 0.4f)
                {
                    pullTimer += dt;
                    if (driver && pullTimer > 0.4f) DriverOut();
                    if (pullTimer > 1.4f) HandOver();
                }
                return;
            }

            if (mode == Mode.TurnAcross || mode == Mode.TurnBack)
            {
                Turning(dt, pos, fwd);
                return;
            }

            // keep to the lane: the next point is passed once it is close, or behind
            for (int guard = 0; guard < 4; guard++)
            {
                Vector3 nx = Lane(idx + dir) - pos;
                nx.y = 0;
                if (nx.magnitude < 2.5f || (nx.magnitude < 9f && Vector3.Dot(nx, transform.forward) < 0f)) { Advance(); if (mode != Mode.Drive || !this) return; }
                else break;
            }

            float speed = Mathf.Abs(fwd);
            car.aiSteer = SteerTo(Aim(Mathf.Clamp(speed * 0.9f, 4.5f, 13f)));

            // bends ahead set the speed
            Vector3 ahead = Lane(idx + dir * 4) - pos;
            ahead.y = 0;
            float bend = Vector3.Angle(transform.forward, ahead);
            float targetSpeed = Mathf.Lerp(cruise, 3.5f, Mathf.Clamp01(bend / 70f));
            if (shaken > 0f) { shaken -= dt; targetSpeed = 0f; }

            // something in front? (cars, people, the player)
            float look = 5f + speed * 1.2f;
            Vector3 from = pos + Vector3.up * 1f + transform.forward * (spec.length * 0.5f + 0.3f);
            if (Physics.SphereCast(from, 0.9f, transform.forward, out var hit, look, obstacleMask, QueryTriggerInteraction.Ignore) &&
                !hit.collider.transform.IsChildOf(transform))
                targetSpeed = hit.distance < 4f ? 0f : Mathf.Min(targetSpeed, (hit.distance - 3f) * 0.7f);

            float err = targetSpeed - fwd;
            car.aiThrottle = Mathf.Clamp(err * 0.35f, -1f, 1f);
            if (targetSpeed < 0.3f && Mathf.Abs(fwd) < 1.2f) { car.aiThrottle = 0f; car.aiHandbrake = 1f; }
            else car.aiHandbrake = 0f;

            Manners(dt, pos, targetSpeed, bend);

            // stuck (blocked for long, or wedged against something): turn round where the road allows
            if (speed < 0.3f) stuck += dt; else stuck = 0f;
            if (stuck > 20f && stuck - dt <= 20f && net.roads[road].oneWay == 0) TurnRound();   // held up for long: turn round
            if (stuck > 40f)   // wedged for good: gone once nobody is looking
            {
                var cam = Camera.main;
                Vector3 vp = cam ? cam.WorldToViewportPoint(pos) : Vector3.zero;
                bool seen = cam && vp.z > 0 && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f && vp.z < 120f;
                if (!seen) Leave();
            }
        }

        /// <summary>A three-point turn: slowly across the road on full lock, back up on the other lock when the edge (or
        /// something) is close, and away once the car faces the new way.</summary>
        void Turning(float dt, Vector3 pos, float fwd)
        {
            turnTimer += dt;
            car.aiHandbrake = 0f;
            Vector3 f = transform.forward;
            f.y = 0;
            float off = Vector3.SignedAngle(f.normalized, turnHeading, Vector3.up);
            float half = Mathf.Max(2.4f, net.roads[road].width * 0.5f);
            if (Mathf.Abs(off) < 28f || turnSteps > 8)
            {
                mode = Mode.Drive;
                Relocate();
                return;
            }
            float moved = Vector3.Distance(pos, phaseStart);
            bool stalled = turnTimer > 1.2f && Speed < 0.15f;   // pressed against something the casts missed
            float hw = spec.width * 0.5f, hl = spec.length * 0.5f;
            Vector3 fw = transform.forward, rt = transform.right;
            float side = Mathf.Abs(off) > 150f ? 1f : Mathf.Sign(off);   // straight back: swing out to the right, across the road
            if (mode == Mode.TurnAcross)
            {
                car.aiSteer = side * spec.steer;
                car.aiThrottle = Mathf.Clamp((1.8f - fwd) * 0.5f, -1f, 0.6f);
                bool edge = turnTimer > 0.6f && (OffCentre(pos + fw * hl + rt * hw) > half - 0.35f || OffCentre(pos + fw * hl - rt * hw) > half - 0.35f);
                if (edge || stalled || Blocked(fw, 1.4f) || turnTimer > 8f || moved > 9f) NextPhase(Mode.TurnBack, pos);
            }
            else
            {
                car.aiSteer = -side * spec.steer;
                car.aiThrottle = fwd > 0.3f ? -0.6f : Mathf.Clamp((-1.6f - fwd) * 0.5f, -0.6f, 1f);
                bool edge = turnTimer > 0.6f && (OffCentre(pos - fw * hl + rt * hw) > half - 0.35f || OffCentre(pos - fw * hl - rt * hw) > half - 0.35f);
                if (edge || stalled || Blocked(-fw, 1.2f) || turnTimer > 5f || moved > 3.5f) NextPhase(Mode.TurnAcross, pos);
            }
        }

        Vector3 phaseStart;

        void NextPhase(Mode m, Vector3 pos)
        {
            mode = m;
            turnTimer = 0f;
            turnSteps++;
            phaseStart = pos;
        }

        /// <summary>Hill-road manners: the horn when held up and before a blind bend; the swish when going past.</summary>
        void Manners(float dt, Vector3 pos, float targetSpeed, float bend)
        {
            var au = AudioManager.Instance;
            hornCooldown -= dt;
            float speed = Speed;
            if (targetSpeed < 0.5f && speed < 1f) blocked += dt; else blocked = 0f;
            if (au && hornCooldown <= 0f)
            {
                if (blocked > 1.2f || (bend > 45f && speed > 5f && Random.value < 0.3f))
                {
                    au.HornAt(pos + Vector3.up, type == CarType.Bus || type == CarType.Pickup);
                    hornCooldown = Random.Range(4f, 9f);
                }
                else if (bend > 45f) hornCooldown = 2.5f;   // one chance per bend
            }
            var cam = Camera.main;   // going past the listener
            if (au && cam)
            {
                float d2 = (cam.transform.position - pos).sqrMagnitude;
                if (!passed && d2 < 81f && speed > 5f) { passed = true; au.PassBy(pos, speed); }
                else if (passed && d2 > 400f) passed = false;
            }
        }
    }
}
