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
        float stuck, hornCooldown, blocked, turnTimer, shaken, pullTimer, pull, wayT, waySign, oncoming, feelSteer, backT, stillT;
        float frontWall = float.MaxValue;
        static int wallMask = -1;
        int turnSteps;
        Vector3 turnHeading;
        readonly TurnPlan plan = new TurnPlan();
        VehicleLamps lampsRef;
        float dipperCd;
        /// <summary>How many dipper flicks the traffic has given (checks: VehicleTest.SignalReport).</summary>
        public static int Dippers;
        /// <summary>The indicator this car shows now (-1 left, +1 right, 0 none).</summary>
        public int Signal => lampsRef ? lampsRef.signal : 0;
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
        public string State => mode + (driver ? ", driver in" : ", no driver") + $", road {road} idx {idx} dir {dir} t {turnTimer:F1} steps {turnSteps} thr {(car ? car.aiThrottle : 0f):F2} steer {(car ? car.aiSteer : 0f):F0} hb {(car ? car.aiHandbrake : 0f):F0} stuck {stuck:F0}" +
                               $" tries {tries}{(ramT > 0f ? " RAM" : "")}{(passing ? " PASS" : "")}{(ghosts.Count > 0 ? $" ghosts {ghosts.Count}" : "")}";

        // ------------------------------------------------------------------ getting unstuck
        // owner, 21 Nov 2026: "gaadi atke na... back krke kahin na kahin se nikaal hi le..vrna thok ke lejaaye..agar 4-5
        // attempt me na gyi to". Wedged (pressing on and not moving): back out, each try on another lock and a little
        // longer; the 5th time it rams its way through (full throttle up the lane, shoved over a kerb, through the static
        // thing it is pressed against - never a person); unseen and still stuck after that, it is put on its lane ahead.
        // A standing vehicle in its lane that will not move (parked, abandoned, wedged, wrecked) is passed on the right.
        int tries;
        Vector3 troubleAt;
        float ramT, slowT, shoveCd, freeT, backSteer, passOff, passWant, passT, waitT, ghostT, dodgeT, dodgeWant;
        Transform passing;
        float climbT;
        readonly List<Collider> ghosts = new List<Collider>();
        /// <summary>How often the traffic rammed through / was put back on its lane / went round a standing vehicle
        /// (checks: VehicleTest.JamReport).</summary>
        public static int Rams, Hops, Passes, BackOffs;
        public int Tries => tries;
        /// <summary>Test hook (VehicleTest.PassTest): the point of its lane about `metres` ahead and the lane's way there.</summary>
        public Vector3 LaneAhead(float metres, out Vector3 way)
        {
            int i = idx;
            Vector3 prev = transform.position;
            float run = 0f;
            for (int k = 1; k < 60 && run < metres; k++)
            {
                int j = Mathf.Clamp(idx + dir * k, 0, Pts.Length - 1);
                Vector3 q = Lane(j);
                run += Vector3.Distance(new Vector3(prev.x, 0f, prev.z), new Vector3(q.x, 0f, q.z));
                prev = q;
                i = j;
            }
            way = Lane(i + dir) - Lane(i);
            way.y = 0f;
            way = way.sqrMagnitude > 1e-4f ? way.normalized : transform.forward;
            return Lane(i);
        }
        int mostTries, ramsHere, passesHere;
        public int MostTries => mostTries;
        public int RamsHere => ramsHere;
        public int PassesHere => passesHere;
        public bool Ramming => ramT > 0f;
        public bool Passing => passing;
        public bool HasDriver => driver;
        /// <summary>An ambulance or police car is stuck against this one (RouteDriver): back off out of its way if it is
        /// in front, else pull on and over to the left.</summary>
        public void MakeWay(Vector3 from)
        {
            if (mode != Mode.Drive) return;
            Vector3 rel = from - transform.position;
            rel.y = 0f;
            waySign = Vector3.Dot(rel, transform.forward) > 0f ? -1f : 1f;
            wayT = 2.6f;
            stuck = 0f;
        }

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
            SeatPassengers();
        }

        // ------------------------------------------------------------------ passengers (the unbranded set's setups)
        readonly List<NpcWalker> passengers = new List<NpcWalker>();
        static readonly List<TrafficCar> carrying = new List<TrafficCar>();
        static int MaxPassengers => Application.isMobilePlatform ? 10 : 28;

        /// <summary>How many passengers all the traffic carries now.</summary>
        static int PassengersOut()
        {
            int n = 0;
            for (int i = carrying.Count - 1; i >= 0; i--)
            {
                if (!carrying[i]) { carrying.RemoveAt(i); continue; }
                foreach (var w in carrying[i].passengers) if (w) n++;
            }
            return n;
        }

        /// <summary>Some of the other seats taken (owner, 21 Nov: "traffic passengers by capacity"): a car mostly one or
        /// two, a taxi one more, a bus 3-10; each a character measured to fit its seat (PedestrianManager.MakeSeated).
        /// Every traffic vehicle together carries no more than MaxPassengers (a phone: 10).</summary>
        void SeatPassengers()
        {
            var vp = GetComponent<VehicleParts>();
            var pm = PedestrianManager.Instance;
            if (!vp || !vp.HasSetup || !pm) return;
            int cap = vp.SeatCount - 1;
            if (cap <= 0) return;
            int n;
            if (type == CarType.Bus) n = Random.Range(3, 11);
            else
            {
                float r = Random.value;
                n = r < 0.35f ? 0 : r < 0.7f ? 1 : r < 0.88f ? 2 : 3 + Random.Range(0, 2);
                if (type == CarType.Taxi) n++;
            }
            n = Mathf.Min(n, cap, MaxPassengers - PassengersOut());
            for (int k = 0; k < n; k++)
            {
                int s = vp.FreePassengerSeat();
                if (s < 0) break;
                var w = pm.MakeSeated(vp, s);
                if (!w) break;
                passengers.Add(w);
            }
            if (passengers.Count > 0 && !carrying.Contains(this)) carrying.Add(this);
        }

        /// <summary>Everyone in the back gets out at their own door and walks off (flee: runs a few steps first).</summary>
        void Unload(bool flee)
        {
            var vp = GetComponent<VehicleParts>();
            foreach (var w in passengers)
            {
                if (!w) continue;
                int s = vp ? vp.SeatOf(w.transform) : -1;
                Vector3 face = transform.right;
                Vector3 at = vp && s >= 0 ? vp.WayOut(s, out face) : transform.position + transform.right * (spec.width * 0.5f + 0.8f);
                w.LeaveCar(Ground(at), face);
                if (flee) w.Flee(face, 3f);
            }
            passengers.Clear();
        }

        /// <summary>Where the driver sits: right-hand drive, low in the seat (the walker's root; hips 0.51 m above it).</summary>
        public Vector3 DriverSeatLocal() => VehicleSeats.Root(gameObject);

        Vector3 OldDriverSeatLocal()
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
            return pts[i] + left * (off + pull * 1.3f - passOff);   // pull: over to the left for a siren (EmergencyLights); passOff: round a standing vehicle
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
            // the road planned (and signalled) 30 m back, else one picked now
            var planned = plan.Take(road, dir);
            if (planned.HasValue) { road = planned.Value.r; idx = planned.Value.i; dir = planned.Value.d; return; }
            var ok = RoadChoice.Onward(net, road, idx);   // onward roads, never the wrong way down a one-way road
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
            plan.TurningRound();
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
            var vp = GetComponent<VehicleParts>();
            Vector3 side = transform.right, door;
            if (vp && vp.HasSetup) door = vp.WayOut(0, out side);   // at the setup's way in, its door opening
            else
            {
                // out on the driver's side (right-hand drive) unless something stands there, then the other side
                door = transform.position + side * (spec.width * 0.5f + 0.75f);
                if (Physics.CheckCapsule(door + Vector3.up * 0.5f, door + Vector3.up * 1.5f, 0.32f, ~((1 << 9) | (1 << 2)), QueryTriggerInteraction.Ignore))
                {
                    side = -side;
                    door = transform.position + side * (spec.width * 0.5f + 0.75f);
                }
            }
            d.LeaveCar(Ground(door), side);
            Unload(false);   // the passengers get out too: the player is taking the car
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
                var vp = GetComponent<VehicleParts>();
                Vector3 side = transform.right;
                Vector3 at = vp && vp.HasSetup ? vp.WayOut(0, out side) : transform.position + transform.right * (spec.width * 0.5f + 0.9f);
                d.LeaveCar(Ground(at), side);
                d.Flee(side, 4f);
            }
            Unload(true);
        }

        void OnDestroy()
        {
            if (ghosts.Count > 0) RestoreGhosts(true);
            if (driver) Destroy(driver.gameObject);
            foreach (var w in passengers) if (w) Destroy(w.gameObject);
            passengers.Clear();
            carrying.Remove(this);
        }

        // ------------------------------------------------------------------ driving
        void FixedUpdate()
        {
            if (!net || !car) return;
            float dt = Time.fixedDeltaTime;
            Vector3 pos = transform.position;
            float fwd = car.ForwardSpeed;
            if (mode == Mode.Wrecked) { car.aiThrottle = 0f; car.aiHandbrake = 1f; if (lampsRef) lampsRef.signal = 0; return; }

            // the indicator: the turn planned ahead, right through a turn round (owner, 21 Nov 2026)
            if (!lampsRef) lampsRef = GetComponent<VehicleLamps>();
            if (mode == Mode.Drive) plan.Tick(net, road, dir, pos, dt);
            if (lampsRef) lampsRef.signal = mode == Mode.TurnAcross || mode == Mode.TurnBack ? 1 : mode == Mode.Drive ? plan.Signal : 0;

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

            // free again: well away from where the trouble began, or rolling for a while
            freeT = Mathf.Abs(fwd) > 3f ? freeT + dt : 0f;
            if (tries > 0 && (freeT > 4f || (pos - troubleAt).sqrMagnitude > 15f * 15f)) tries = 0;
            if (ghosts.Count > 0 && ramT <= 0f && (ghostT += dt) > 3f) RestoreGhosts();

            if (ramT > 0f) { Ram(dt, pos); return; }   // the 5th try: through

            if (backT > 0f)   // nosed into something: back off on the lock chosen for this try, then go again
            {
                backT -= dt;
                car.aiHandbrake = 0f;
                car.aiSteer = backSteer;   // (it was worked out from aiSteer each step, so it flipped every step: straight back)
                car.aiThrottle = fwd > 0.3f ? -1f : -0.6f;
                if (backT <= 0f) { Relocate(); stillT = 0f; }
                return;
            }

            // going round a standing vehicle: until it is behind
            if (passing)
            {
                passT += dt;
                float along = Vector3.Dot(passing.position - pos, transform.forward);
                if (along < -(spec.length * 0.5f + 3.5f) || passT > 15f) passing = null;
            }
            if (dodgeT > 0f) dodgeT -= dt;
            passOff = Mathf.MoveTowards(passOff, passing ? passWant : dodgeT > 0f ? dodgeWant : 0f, dt * 1.4f);

            if (wayT > 0f)   // making way for a siren: back away (it is in front) or on and over to the left
            {
                wayT -= dt;
                pull = 1f;
                car.aiHandbrake = 0f;
                car.aiSteer = waySign < 0f ? spec.steer * 0.5f : -spec.steer * 0.35f;
                car.aiThrottle = waySign < 0f ? (fwd > 0.3f ? -1f : -0.6f) : Mathf.Clamp((3f - fwd) * 0.4f, -1f, 0.6f);
                if (wayT <= 0f) Relocate();
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

            // bends ahead set the speed: the sharpest turn of the lane over the next ~30 m, and how soon it comes
            float targetSpeed = BendSpeed(pos);
            Vector3 ahead = Lane(idx + dir * 4) - pos;
            ahead.y = 0;
            float bend = Vector3.Angle(transform.forward, ahead);   // (the horn before a blind bend: Manners)
            if (shaken > 0f) { shaken -= dt; targetSpeed = 0f; }

            // walls, railings, parked cars, the hillside, a drop at the road's edge: feelers ahead steer it off and slow it
            targetSpeed = Mathf.Min(targetSpeed, Feel(pos, speed));

            // something in front? (cars, people, the player): keep a gap of about two seconds, match a car's speed
            float look = 6f + speed * 2f;
            Vector3 from = pos + Vector3.up * 1f + transform.forward * (spec.length * 0.5f + 0.3f);
            Rigidbody blocker = null;
            Transform standingPerson = null;
            if (Physics.SphereCast(from, 0.9f, transform.forward, out var hit, look, obstacleMask, QueryTriggerInteraction.Ignore) &&
                !hit.collider.transform.IsChildOf(transform) &&
                !(passing && (hit.rigidbody ? hit.rigidbody.transform : hit.collider.transform) == passing && hit.distance > 0.8f))   // the one it goes round
            {
                float lead = 0f;
                if (hit.rigidbody && !hit.rigidbody.isKinematic) lead = Mathf.Max(0f, Vector3.Dot(hit.rigidbody.linearVelocity, transform.forward));
                bool person = hit.collider.gameObject.layer == 8 || hit.collider.gameObject.layer == 9;
                float gap = hit.distance - (person ? 3.5f : 2.5f);
                targetSpeed = gap < 1f ? 0f : Mathf.Min(targetSpeed, lead + gap * 0.45f);
                if (!person && hit.rigidbody && hit.rigidbody != rb) blocker = hit.rigidbody;
                if (person && hit.collider.GetComponent<CharacterController>() is CharacterController pc && pc.velocity.sqrMagnitude < 0.09f)
                    standingPerson = hit.collider.transform;   // someone standing in the road (a policeman at his post, the player)
            }
            targetSpeed = AnimalManager.CowAhead(transform, spec.width, spec.length, look, targetSpeed, ref standingPerson);   // a cow in the lane: wait + honk, never round / through it (Characters/AnimalManager.cs)

            // a narrow road and a car coming the other way: over to the left and slow, like on a hill road
            oncoming = Mathf.MoveTowards(oncoming, Oncoming(pos) ? 1f : 0f, dt * 1.5f);
            if (oncoming > 0.5f) targetSpeed = Mathf.Min(targetSpeed, 5f);

            // an accident ahead (IncidentManager): stop short of it and wait; a siren behind or coming: over to the left
            bool held = IncidentManager.HoldAhead(pos, transform.forward, out float hd);
            if (held) targetSpeed = Mathf.Min(targetSpeed, Mathf.Max(0f, (hd - 10f) * 0.6f));
            bool siren = TrafficBike.SirenNear(pos, transform.forward);
            pull = Mathf.MoveTowards(pull, siren ? 1f : oncoming * 0.5f, dt * 0.7f);
            if (siren) targetSpeed = Mathf.Min(targetSpeed, 3f);

            // a vehicle standing in the lane that will not move: after 3 s, round it on the right (nothing coming); a
            // person who stays put in the road (he did not step aside): after 6 s, round them too
            bool still = (blocker && WontMove(blocker)) || standingPerson;
            if (still && !passing && !held && !siren && oncoming < 0.2f)
            {
                waitT += dt;
                if (waitT > (standingPerson ? 6f : 3f)) StartPass(standingPerson ? standingPerson : blocker.transform, pos, standingPerson ? 0.7f : -1f);
            }
            else waitT = 0f;

            float err = targetSpeed - fwd;
            car.aiThrottle = Mathf.Clamp(err * 0.35f, -1f, 1f);
            // uphill the slope takes its share: throttle for it on top (road 90's climb crawled at 0.35-0.6)
            float climb = Mathf.Max(0f, transform.forward.y);
            if (targetSpeed > 0.5f && err > -1f) car.aiThrottle = Mathf.Clamp(car.aiThrottle + climb * 2.5f, -1f, 1f);
            // a steep climb from a standstill: the speed error alone asked too little (stood on road 25's slope at 0.25)
            climbT = targetSpeed > 0.5f && speed < 0.5f ? climbT + dt : 0f;
            if (climbT > 1.2f) car.aiThrottle = Mathf.Max(car.aiThrottle, Mathf.Clamp01(0.6f + (climbT - 1.2f) * 0.5f));
            if (targetSpeed < 0.3f && Mathf.Abs(fwd) < 1.2f) { car.aiThrottle = 0f; car.aiHandbrake = 1f; }
            else car.aiHandbrake = 0f;

            Manners(dt, pos, targetSpeed, bend);

            // stuck (blocked for long, or wedged against something): turn round where the road allows
            if (speed < 0.3f && !held && !siren) stuck += dt; else stuck = 0f;   // waiting at an accident is not being stuck
            // nosed into something (a wall the ray missed, a kerb, another car's corner): pressing on and not moving
            stillT = speed < 0.4f && (frontWall < 3.5f || car.aiThrottle > 0.5f) ? stillT + dt : 0f;
            if (stillT > 2.5f) { stillT = 0f; Wedged(pos); }
            if (stuck > 20f && stuck - dt <= 20f && net.roads[road].oneWay == 0) TurnRound();   // held up for long: turn round
            if (stuck > 40f && !Seen(pos) && !Hop()) Leave();   // wedged for good: on its lane ahead once nobody is looking, else gone
        }

        bool Seen(Vector3 pos)
        {
            var cam = Camera.main;
            if (!cam) return false;
            Vector3 vp = cam.WorldToViewportPoint(pos);
            return vp.z > 0 && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f && vp.z < 120f;
        }

        /// <summary>Pressing on and not moving: tries 1-4 back out (towards the lane, straight, towards it, the other
        /// way; a little longer each time), the 5th and later ram through; from the 7th, unseen, it is put on its lane ahead.</summary>
        void Wedged(Vector3 pos)
        {
            if (tries == 0) troubleAt = pos;
            tries++;
            mostTries = Mathf.Max(mostTries, tries);
            if (tries >= 7 && !Seen(pos) && Hop()) return;
            if (tries >= 5) { StartRam(); return; }
            BackOffs++;
            // reversing with the wheel one way swings the nose the other: back out on the lock that brings the nose to the lane
            float toLane = Mathf.Sign(SteerTo(Aim(8f)));
            float[] pattern = { -1f, 0f, -1f, 1f };
            backSteer = pattern[(tries - 1) % 4] * toLane * spec.steer * 0.85f;
            backT = 1.6f + 0.4f * tries;
            if (Blocked(-transform.forward, 1.2f)) backT = 0.7f;   // something right behind: a short one
            // from the 2nd try, as a driver would: out of its lane to the right, round whatever is in it (the 3rd try
            // further out); only the whole road shut makes it ram
            if (tries >= 2)
            {
                var r = net.roads[road];
                float lane = r.oneWay != 0 ? r.width * 0.3f : Mathf.Max(1.1f, r.width * 0.25f) * 2f;
                dodgeWant = lane * (tries >= 3 ? 1.25f : 1f);
                dodgeT = backT + 7f;
            }
        }

        void StartRam()
        {
            ramT = 5f;
            slowT = 0f;
            shoveCd = 0f;
            Rams++;
            ramsHere++;
            VehicleSound.HornOf(gameObject, transform.position + Vector3.up, type == CarType.Bus || type == CarType.Pickup);
        }

        /// <summary>Full throttle up the lane. Stopped by a person (never through one); slowed to a crawl (a kerb, a bump,
        /// the hillside) it gets a shove towards the lane and up; a static thing it presses on is passed through
        /// (OnCollisionStay: Ghost) - other vehicles are just pushed.</summary>
        void Ram(float dt, Vector3 pos)
        {
            ramT -= dt;
            Vector3 aim = Aim(6f);
            car.aiHandbrake = 0f;
            car.aiSteer = SteerTo(aim);
            car.aiThrottle = 1f;
            Vector3 from = pos + Vector3.up * 1f + transform.forward * (spec.length * 0.5f + 0.3f);
            if (Physics.SphereCast(from, 0.9f, transform.forward, out _, 5f, (1 << 8) | (1 << 9), QueryTriggerInteraction.Ignore))
            {
                car.aiThrottle = 0f;
                car.aiHandbrake = 1f;
                return;
            }
            slowT = Speed < 0.6f ? slowT + dt : 0f;
            shoveCd -= dt;
            if (slowT > 0.8f && shoveCd <= 0f)
            {
                Vector3 to = aim - pos;
                to.y = 0f;
                to = to.sqrMagnitude > 0.01f ? to.normalized : transform.forward;
                rb.AddForce(to * 3f + Vector3.up * 2.2f, ForceMode.VelocityChange);
                shoveCd = 1.3f;
                slowT = 0f;
            }
            if (ramT <= 0f) { Relocate(); stillT = 0f; }
        }

        void OnCollisionEnter(Collision c) { if (ramT > 0f) Ghost(c); }
        void OnCollisionStay(Collision c) { if (ramT > 0f) Ghost(c); }

        /// <summary>Ramming: through a static thing it is pressed against from the side or the front (a wall corner, a
        /// pole, a post) - not the ground under it, not the terrain, not a person, not another vehicle.</summary>
        void Ghost(Collision c)
        {
            var o = c.collider;
            if (!o || o is TerrainCollider || ghosts.Contains(o)) return;
            int l = o.gameObject.layer;
            if (l == 8 || l == 9 || l == VehicleFactory.VehicleLayer) return;
            if (o.attachedRigidbody && !o.attachedRigidbody.isKinematic) return;
            if (c.contactCount > 0 && c.GetContact(0).normal.y > 0.5f) return;   // under it
            foreach (var m in Mine()) Physics.IgnoreCollision(m, o, true);
            ghosts.Add(o);
            ghostT = 0f;
        }

        Collider[] mine;
        Collider[] Mine()
        {
            if (mine == null)
            {
                var all = new List<Collider>();
                foreach (var m in GetComponentsInChildren<Collider>(true)) if (m && !m.isTrigger && m.attachedRigidbody == rb) all.Add(m);
                mine = all.ToArray();
            }
            return mine;
        }

        /// <summary>Solid again against whatever it went through, once it is out of it.</summary>
        void RestoreGhosts(bool force = false)
        {
            for (int g = ghosts.Count - 1; g >= 0; g--)
            {
                var o = ghosts[g];
                if (!o) { ghosts.RemoveAt(g); continue; }
                bool inside = false;
                if (!force)
                    foreach (var m in Mine())
                        if (m && Physics.ComputePenetration(m, m.transform.position, m.transform.rotation, o, o.transform.position, o.transform.rotation, out _, out _)) { inside = true; break; }
                if (inside) continue;
                foreach (var m in Mine()) if (m) Physics.IgnoreCollision(m, o, false);
                ghosts.RemoveAt(g);
            }
            ghostT = 0f;
        }

        /// <summary>Put on its lane a little ahead (only where nobody sees it, the place clear of everything but the ground).</summary>
        bool Hop()
        {
            var pts = Pts;
            for (int k = 3; k <= 9; k += 3)
            {
                int i = Mathf.Clamp(idx + dir * k, 0, pts.Length - 1);
                Vector3 p = Ground(Lane(i));
                Vector3 f = Lane(i + dir) - Lane(i);
                f.y = 0f;
                if (f.sqrMagnitude < 0.01f) continue;
                var rot = Quaternion.LookRotation(f.normalized, Vector3.up);
                Vector3 half = new Vector3(spec.width * 0.5f, spec.height * 0.4f, spec.length * 0.5f);
                int n = Physics.OverlapBoxNonAlloc(p + Vector3.up * (spec.height * 0.5f + 0.35f), half, near, rot, ~((1 << 2) | (1 << 4)), QueryTriggerInteraction.Ignore);
                bool clear = true;
                for (int j = 0; j < n && clear; j++) if (!(near[j] is TerrainCollider) && near[j].attachedRigidbody != rb) clear = false;
                if (!clear) continue;
                RestoreGhosts(true);
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                transform.SetPositionAndRotation(p + Vector3.up * 0.35f, rot);
                Physics.SyncTransforms();
                idx = i;
                tries = 0; stuck = 0f; stillT = 0f; backT = 0f; ramT = 0f; dodgeT = 0f;
                Hops++;
                return true;
            }
            return false;
        }

        /// <summary>A vehicle in the way that will not move off by itself: parked, abandoned, wrecked, wedged, a fallen
        /// or parked two-wheeler - not one waiting in a queue.</summary>
        public static bool WontMove(Rigidbody o)
        {
            if (!o) return false;
            var br = o.GetComponent<BikeRide>();
            if (br) { var tb = o.GetComponent<TrafficBike>(); return br.Speed < 0.3f && (br.Crashed || !tb || tb.Down); }
            if (o.linearVelocity.sqrMagnitude > 0.25f) return false;
            var tc = o.GetComponent<TrafficCar>();
            if (!tc) return true;
            return tc.Wrecked || !tc.HasDriver || tc.tries > 0 || tc.ramT > 0f;
        }

        void StartPass(Transform o, Vector3 pos, float width = -1f)
        {
            var info = o.GetComponent<VehicleInfo>();
            float bw = width > 0f ? width : info ? info.width : 1.9f;
            float lat = Vector3.Dot(o.position - pos, transform.right);
            float want = lat + bw * 0.5f + spec.width * 0.5f + 0.6f;
            if (want <= 0.3f) return;   // already clear of it
            var r = net.roads[road];
            float room = r.oneWay != 0 ? r.width * 0.5f : Mathf.Max(1.1f, r.width * 0.25f) * 2f + 0.8f;   // to the far lane, a little over
            passWant = Mathf.Min(want, room);
            passing = o;
            passT = 0f;
            waitT = 0f;
            Passes++;
            passesHere++;
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

        /// <summary>Speed for the road ahead: the sharpest turn within ~30 m of lane (a hairpin wants ~4 m/s, a gentle bend
        /// little less than cruise), braked for in time: v = sqrt(v_turn^2 + 2 a d) with a = 3 m/s^2.</summary>
        float BendSpeed(Vector3 pos)
        {
            float best = cruise;
            Vector3 prev = pos, prevDir = transform.forward;
            prevDir.y = 0f;
            float along = 0f;
            for (int k = 1; k < 10 && along < 34f; k++)
            {
                int i = idx + dir * k;
                if (i < 0 || i >= Pts.Length) break;
                Vector3 q = Lane(i);
                Vector3 d = q - prev;
                d.y = 0f;
                float len = d.magnitude;
                if (len < 0.5f) continue;
                d /= len;
                float turn = Vector3.Angle(prevDir, d) / Mathf.Max(len, 2f);   // degrees per metre
                float vTurn = Mathf.Lerp(cruise, 3.5f, Mathf.Clamp01(turn / 9f));   // ~9 deg/m is a hairpin
                best = Mathf.Min(best, Mathf.Sqrt(vTurn * vTurn + 2f * 3f * along));
                along += len;
                prev = q;
                prevDir = d;
            }
            return best;
        }

        /// <summary>Feelers. Straight ahead from the bumper: a wall, railing or pole in the path sets a speed it can stop
        /// in (4 m/s^2). Two short rays from the front corners, angled out: a wall close at one side nudges the wheel
        /// away (never more than a third of full lock, never a stop: on a hill road's bends the walls are always near,
        /// the lane steers it round them).</summary>
        float Feel(Vector3 pos, float speed)
        {
            if (wallMask < 0) wallMask = ~((1 << VehicleFactory.VehicleLayer) | (1 << 8) | (1 << 9) | (1 << 2) | (1 << 4));
            Vector3 fwd = transform.forward, right = transform.right;
            float hw = spec.width * 0.5f, hl = spec.length * 0.5f;
            float limit = cruise;
            float reach = 3f + speed * speed / 8f;
            Vector3 o = pos + Vector3.up * 0.6f + fwd * (hl * 0.9f);
            if (Physics.Raycast(o, fwd, out var h, reach, wallMask, QueryTriggerInteraction.Ignore) && h.normal.y < 0.6f)
            {
                frontWall = h.distance;
                limit = Mathf.Sqrt(2f * 4f * Mathf.Max(0f, h.distance - 1.8f));
            }
            else frontWall = float.MaxValue;
            float push = 0f;
            float len = Mathf.Clamp(2.5f + speed * 0.35f, 3f, 7f);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 c = pos + Vector3.up * 0.6f + fwd * (hl * 0.8f) + right * (s * hw * 0.85f);
                Vector3 d = Quaternion.AngleAxis(s * 25f, Vector3.up) * fwd;
                if (Physics.Raycast(c, d, out var w, len, wallMask, QueryTriggerInteraction.Ignore) && w.normal.y < 0.6f)
                    push -= s * (1f - w.distance / len);
            }
            feelSteer = Mathf.MoveTowards(feelSteer, Mathf.Clamp(push, -1f, 1f), Time.fixedDeltaTime * 4f);
            car.aiSteer = Mathf.Clamp(car.aiSteer + feelSteer * spec.steer * 0.33f, -spec.steer, spec.steer);
            return limit;
        }

        /// <summary>A car coming the other way close ahead on a narrow road.</summary>
        bool Oncoming(Vector3 pos)
        {
            if (net.roads[road].width > 7f) return false;
            int n = Physics.OverlapSphereNonAlloc(pos + transform.forward * 18f, 14f, near, 1 << VehicleFactory.VehicleLayer, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var rb2 = near[i].attachedRigidbody;
                if (!rb2 || rb2.transform == transform) continue;
                Vector3 rel = rb2.position - pos;
                if (Vector3.Dot(rel, transform.forward) < 2f) continue;
                if (Vector3.Dot(rb2.transform.forward, transform.forward) > -0.5f) continue;   // not coming at us
                if (Mathf.Abs(Vector3.Dot(rel, transform.right)) > 5f) continue;
                return true;
            }
            return false;
        }

        static readonly Collider[] near = new Collider[12];

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
                    VehicleSound.HornOf(gameObject, pos + Vector3.up, type == CarType.Bus || type == CarType.Pickup);   // its own horn
                    hornCooldown = Random.Range(4f, 9f);
                }
                else if (bend > 45f) hornCooldown = 2.5f;   // one chance per bend
            }
            // held up: now and then a flick of the dipper too (owner, 21 Nov 2026: "stucked hain to random dipper bhi"),
            // with the horn (22 Nov: "dipper ke saath horn bhi bjva do")
            dipperCd -= dt;
            if (blocked > 2.5f && dipperCd <= 0f)
            {
                if (lampsRef && Random.value < 0.45f)
                {
                    lampsRef.Flash();
                    Dippers++;
                    VehicleSound.HornOf(gameObject, pos + Vector3.up, type == CarType.Bus || type == CarType.Pickup);
                    hornCooldown = Random.Range(4f, 9f);
                }
                dipperCd = Random.Range(3f, 7f);
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
