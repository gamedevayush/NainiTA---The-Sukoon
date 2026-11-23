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

        enum Mode { Drive, TurnAcross, TurnBack, Path, PullUp, Wrecked }

        int road, idx, dir;
        Mode mode;
        float stuck, hornCooldown, blocked, turnTimer, shaken, pullTimer, pull, wayT, waySign, oncoming, feelSteer, stillT;
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
                               $" tries {tries}{(bump != Bump.None ? " HIT:" + bump : "")}{(passing ? " PASS" : "")}";

        // ------------------------------------------------------------------ getting unstuck
        // owner, 21 Nov 2026: "gaadi atke na... back krke kahin na kahin se nikaal hi le"; 22 Nov 2026, watching it: "NPC ki
        // gaadi thukne ke baad bhi accelerator diye jaari hain... thuke ruke or back krke nikaal le". So: the moment it
        // hits something with the end it is moving on, off the accelerator and brake; a short pause; back out the other
        // way, the wheel turned from what it hit; then a clear way round it (DrivePath). Never pressed on against it and
        // never through it (the 5-second ram and its pass-through are gone). Four times at one place on a two-way road:
        // it turns round; unseen and still stuck, it is put on its lane ahead.
        // A standing vehicle in its lane that will not move (parked, abandoned, wedged, wrecked) is passed on the right.
        int tries;
        Vector3 troubleAt;
        float freeT, passOff, passWant, passT, waitT, dodgeT, dodgeWant;
        Transform passing;
        float climbT, behindT;
        /// <summary>What it last hit with the end it was driving on (checks: TrafficLab).</summary>
        public string LastHit { get; private set; } = "-";
        /// <summary>How long it has been held to a walking pace by someone on foot in front (checks: TrafficLab).</summary>
        public float BehindWalker => behindT;
        /// <summary>How often the traffic hit something / was put back on its lane / went round a standing vehicle
        /// (checks: VehicleTest.JamReport).</summary>
        public static int Hits, Hops, Passes, BackOffs;
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
        int mostTries, hitsHere, passesHere, hopsHere;
        public int HopsHere => hopsHere;
        public int MostTries => mostTries;
        public int HitsHere => hitsHere;
        public int PassesHere => passesHere;
        /// <summary>Braking after a hit / backing out of one.</summary>
        public bool BackingOut => bump != Bump.None;
        /// <summary>The throttle it gives now (checks: TrafficLab.CrashTest).</summary>
        public float Throttle => car ? car.aiThrottle : 0f;
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

        // ------------------------------------------------------------------ a route (tests, later missions)
        List<(int r, int d)> route;
        int routeAt;
        Vector3 routeGoal;
        /// <summary>Reached the end of its route (it stands there).</summary>
        public bool Arrived { get; private set; }
        /// <summary>Where it is on its route: step / steps, the road and the way.</summary>
        public string RouteInfo => route == null ? "no route" : $"step {routeAt + 1}/{route.Count} road {road} '{net.roads[road].name}' idx {idx}/{Pts.Length} dir {dir}{(Arrived ? " ARRIVED" : "")}";
        public int Road => road;
        public int Idx => idx;
        public int Dir => dir;

        /// <summary>Drive these roads in turn (road, way), the first being the one it is on, and stop at `goal` on the last
        /// (TrafficLab.RouteDrive: bus stand to the DSA ground).</summary>
        public void FollowRoute(List<(int r, int d)> steps, Vector3 goal)
        {
            route = steps;
            routeAt = 0;
            routeGoal = goal;
            Arrived = false;
        }

        (int r, int d)? RouteWant() => route != null && routeAt + 1 < route.Count && route[routeAt].r == road ? route[routeAt + 1] : ((int, int)?)null;

        readonly List<(int r, int i, int d, int at)> branches = new List<(int r, int i, int d, int at)>();
        float branchLimit;

        /// <summary>A route's turn off the middle of this road (Khurpatal's road leaves the Kaladhungi road like that; at
        /// a road's end: Advance): the indicator from 30 m, slowed for, taken there. True: it turns by a planned way.</summary>
        bool RouteBranch(Vector3 pos)
        {
            branchLimit = float.MaxValue;
            var want = RouteWant();
            if (!want.HasValue) return false;
            net.Branches(road, idx, dir, branches);
            foreach (var b in branches)
            {
                if (b.r != want.Value.r || b.d != want.Value.d) continue;
                var p = Pts;
                var q = net.roads[b.r].pts;
                Vector3 to = p[b.at] - pos;
                to.y = 0f;
                int past = (idx - b.at) * dir;
                if (past < 0 && to.magnitude > 3f)
                {
                    if (to.magnitude > RoadChoice.PlanAhead || past < -24) return false;
                    Vector3 hin = p[b.at] - p[Mathf.Clamp(b.at - dir * 3, 0, p.Length - 1)];
                    Vector3 hout = q[Mathf.Clamp(b.i + b.d * 3, 0, q.Length - 1)] - q[b.i];
                    hin.y = hout.y = 0f;
                    float turn = Vector3.SignedAngle(hin, hout, Vector3.up);
                    plan.Branching(Mathf.Abs(turn) < RoadChoice.TurnAngle ? 0 : turn > 0f ? 1 : -1);
                    float corner = Mathf.Lerp(cruise, 2.5f, Mathf.Clamp01(Mathf.Abs(turn) / 90f));
                    branchLimit = Mathf.Sqrt(corner * corner + 2f * 1.5f * Mathf.Max(0f, to.magnitude - spec.length * 0.5f));
                    return false;
                }
                int fromRoad = road, fromIdx = idx;
                road = b.r;
                idx = b.i;
                dir = b.d;
                RouteStep();
                SharpEntry(fromRoad, fromIdx);
                return mode != Mode.Drive;
            }
            return false;
        }

        void RouteStep()
        {
            if (route == null) return;
            for (int k = routeAt + 1; k < route.Count; k++) if (route[k].r == road) { routeAt = k; return; }
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

        NpcWalker conductor;

        /// <summary>The bus of the owner's own ride (TrafficBusTest; owner, 23 Nov 2026: "mujhe left side bethaao peeche..saath
        /// me ldki and poori bus me ldkiya..bus ke driver or conductor male"): every free passenger seat taken by women and
        /// girls (the player sits first), a man at the wheel, and a man on his feet just inside the passenger door, facing
        /// it, as the conductor.</summary>
        public void FillForRide(System.Action seatThePlayer = null, bool grownOnly = true, bool lastRowEmpty = true)
        {
            var vp = GetComponent<VehicleParts>();
            var pm = PedestrianManager.Instance;
            if (!vp || !vp.HasSetup || !pm) return;
            foreach (var w in passengers) if (w) { vp.Leave(w.transform); pm.Release(w); }
            passengers.Clear();
            if (driver) { vp.Leave(driver.transform); pm.Release(driver); }
            driver = pm.MakeSeated(vp, 0, false);
            seatThePlayer?.Invoke();   // his seat first, out of an empty bus; then everyone else
            // young women on every other seat (owner, 23 Nov 2026: "sab (not old or kid) ldkiya in bus..and last row khaali")
            for (int s = 1; s < vp.SeatCount; s++)
            {
                if (!vp.IsFree(s) || (lastRowEmpty && vp.RowFromBack(s) == 0)) continue;
                var w = pm.MakeSeated(vp, s, true, grownOnly);
                if (w) passengers.Add(w);
            }
            if (passengers.Count > 0 && !carrying.Contains(this)) carrying.Add(this);
            if (vp.SeatCount < 2) return;
            // the conductor: the floor is where the seated people's roots are (their feet), the door seat 1's way in
            var d1 = vp.SeatData(1);
            float floor = float.MaxValue;
            for (int i = 1; i < vp.SeatCount; i++) floor = Mathf.Min(floor, vp.SeatData(i).pos.y);
            Vector3 inFrame = new Vector3(d1.entry.x * 0.3f, floor, d1.entry.z);
            Vector3 local = vp.SeatParent.InverseTransformPoint(vp.Frame.TransformPoint(inFrame));
            Quaternion rot = Quaternion.Inverse(vp.SeatParent.rotation) * vp.Frame.rotation * Quaternion.LookRotation(new Vector3(Mathf.Sign(d1.entry.x), 0f, 0.4f));
            if (conductor) pm.Release(conductor);
            conductor = pm.MakeStanding(vp, vp.SeatParent, local, rot, false);
            Debug.Log($"[Nainital] ride bus {name}: driver + {passengers.Count} women / girls + the conductor at {inFrame:F2} (frame), floor {floor:F2}");
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
            return pts[i] + left * (off + net.roads[road].Shift(dir, i) + pull * 1.3f - passOff);   // pull: over to the left for a siren (EmergencyLights); passOff: round a standing vehicle
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
            int fromRoad = road, fromIdx = Mathf.Clamp(idx, 0, pts.Length - 1);
            var planned = plan.Take(road, dir);
            if (planned.HasValue) { road = planned.Value.r; idx = planned.Value.i; dir = planned.Value.d; RouteStep(); SharpEntry(fromRoad, fromIdx); return; }
            var ok = RoadChoice.Onward(net, road, idx);   // onward roads, never the wrong way down a one-way road
            if (ok.Count > 0)
            {
                var c = ok[Random.Range(0, ok.Count)];
                var w = RouteWant();
                if (w.HasValue) foreach (var o in RoadChoice.Onward(net, road, idx, true)) if (o.r == w.Value.r && o.d == w.Value.d) { c = o; break; }
                road = c.r;
                idx = c.i;
                dir = c.d;
                RouteStep();
                SharpEntry(fromRoad, fromIdx);
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
            turnSignal = 1;   // a turn round: the right indicator, as on Indian roads
            prevRoad = -1;
            mode = Mode.TurnAcross;
            turnTimer = 0f;
            turnSteps = 0;
            phaseStart = transform.position;
        }

        int turnSignal = 1, prevRoad = -1, prevIdx;

        /// <summary>
        /// Onto a road that leaves at a hairpin (more than 100 degrees from where the car points: 24 such junctions, e.g. the
        /// roads that come down to the Mall at a sharp angle): steering for the lane alone, the car ran its nose into the
        /// wall on the outside of the bend, backed straight off and ran into it again (owner, 22 Nov 2026: "glt mudne pe ...
        /// back krti hai to same direction me phirse chli jaati hai"). A driver takes it in two or three goes: forward on
        /// full lock, back on the other lock, forward - the turn-round manoeuvre with the new road's heading, the edge
        /// checked against both roads. Pressed against something (`backFirst`), it starts by backing.
        /// </summary>
        void StartManoeuvre(Vector3 heading, int fromRoad, int fromIdx, bool backFirst)
        {
            heading.y = 0f;
            if (heading.sqrMagnitude < 0.01f) return;
            turnHeading = heading.normalized;
            float off = Vector3.SignedAngle(new Vector3(transform.forward.x, 0f, transform.forward.z), turnHeading, Vector3.up);
            turnSignal = off >= 0f ? 1 : -1;
            prevRoad = fromRoad;
            prevIdx = fromIdx;
            mode = backFirst ? Mode.TurnBack : Mode.TurnAcross;
            turnTimer = 0f;
            turnSteps = 0;
            phaseStart = transform.position;
            Manoeuvres++;
        }

        float wrongT;
        int astray;
        /// <summary>How often a car gave up turning and drove on the way it faced (checks: TrafficLab).</summary>
        public static int Astrays;

        /// <summary>
        /// Pointing against its lane (a turn it gave up, a shove): not on ahead the same wrong way (owner, 22 Nov 2026).
        /// It turns to its lane; after two failed goes on a two-way road it takes that road the way it faces, in that way's
        /// own lane, as a driver who cannot turn there would. True while it starts such a turn.
        /// </summary>
        bool Astray(float dt, Vector3 pos)
        {
            Vector3 w = Lane(idx + dir * 2) - Lane(idx), f = transform.forward;
            w.y = f.y = 0f;
            bool wrong = w.sqrMagnitude > 0.01f && Vector3.Angle(f, w) > 110f && !Arrived && !net.NearIsland(pos, 10f);
            wrongT = wrong ? wrongT + dt : 0f;
            if (wrongT < 0.8f) return false;
            wrongT = 0f;
            astray++;
            if (astray > 2 && route == null && net.roads[road].oneWay == 0)
            {
                dir = -dir;
                astray = 0;
                Astrays++;
                Relocate();
                return false;
            }
            if (astray > 4 && !Seen(pos) && Hop()) { astray = 0; return true; }
            if (PathToLane()) return true;
            StartManoeuvre(w, -1, 0, Blocked(transform.forward, 2f));
            return true;
        }

        float edgeFrom;

        /// <summary>How many hairpin / wedged three-point turns the traffic made (checks: TrafficLab).</summary>
        public static int Manoeuvres;

        /// <summary>Just onto a new road: if it leaves at a hairpin from where the car points, take it in goes.</summary>
        void SharpEntry(int fromRoad, int fromIdx)
        {
            if (net.NearIsland(transform.position, 10f)) return;   // round the island is the turn
            Vector3 h = Lane(idx + dir * 3) - Lane(idx);
            h.y = 0f;
            Vector3 f = new Vector3(transform.forward.x, 0f, transform.forward.z);
            if (h.sqrMagnitude > 0.01f && f.sqrMagnitude > 0.01f && Vector3.Angle(f, h) > 100f && !PathToLane())
                StartManoeuvre(h, fromRoad, fromIdx, false);
        }

        void Leave()
        {
            mode = Mode.Wrecked;
            BusPassenger.Evict(gameObject);   // the player riding in it gets off first (a child would go with it)
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
        Vector3 Aim(float lookAhead) => net.RoundIslands(transform.position, AlongLane(lookAhead), spec.width * 0.5f);

        Vector3 AlongLane(float lookAhead)
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
            local.y = 0f;
            if (local.sqrMagnitude < 0.01f) return 0f;
            // a point behind: full lock towards it (straight behind it gave next to no lock: on ahead, the same wrong way)
            if (local.z < 0.1f) return local.x < 0f ? -90f : 90f;
            return Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg * 1.15f;
        }

        /// <summary>How far the given point is from the middle of the road (for a turn in a narrow road).</summary>
        float OffCentre(Vector3 p)
        {
            float best = OffCentreOf(road, idx, p);
            if (prevRoad >= 0 && prevRoad < net.roads.Length) best = Mathf.Min(best, OffCentreOf(prevRoad, prevIdx, p));   // a hairpin: the junction is road too
            return best;
        }

        float OffCentreOf(int r, int near, Vector3 p)
        {
            var pts = net.roads[r].pts;
            float best = float.MaxValue;
            for (int k = Mathf.Max(0, near - 8); k < Mathf.Min(pts.Length - 1, near + 8); k++)
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

        // ------------------------------------------------------------------ a passenger (BusPassenger)
        float stopUntil = -1f;

        /// <summary>Stand for this long (someone climbing on / off a bus), then drive on.</summary>
        public void StopForBoarding(float seconds) => stopUntil = Mathf.Max(stopUntil, Time.time + seconds);

        /// <summary>Drive on now (he is aboard / off).</summary>
        public void GoOn() => stopUntil = -1f;

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
            if (mode == Mode.Drive) { plan.want = RouteWant(); plan.Tick(net, road, dir, pos, dt); }
            if (lampsRef) lampsRef.signal = mode == Mode.TurnAcross || mode == Mode.TurnBack || mode == Mode.Path ? turnSignal : mode == Mode.Drive ? plan.Signal : 0;

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

            if (bump != Bump.None) { Bumped(dt, pos, fwd); return; }   // it hit something: before anything else
            if (mode == Mode.TurnAcross || mode == Mode.TurnBack)
            {
                Turning(dt, pos, fwd);
                return;
            }
            if (mode == Mode.Path) { OnPath(dt, pos, fwd); return; }
            pathCd -= dt;

            // free again: well away from where the trouble began, or rolling for a while
            freeT = Mathf.Abs(fwd) > 3f ? freeT + dt : 0f;
            if (tries > 0 && (freeT > 4f || (pos - troubleAt).sqrMagnitude > 15f * 15f)) tries = 0;
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

            if (EnterIsland(pos)) return;   // a junction island ahead: the whole way round it first
            if (!this) return;
            if (RouteBranch(pos)) return;

            // keep to the lane: the next point is passed once it is close, or behind
            for (int guard = 0; guard < 4; guard++)
            {
                Vector3 lp = Lane(idx + dir), nx = lp - pos;
                nx.y = 0;
                if (nx.magnitude < 2.5f || (nx.magnitude < 9f && Vector3.Dot(nx, transform.forward) < 0f) || net.InIsland(lp, pos, spec.width * 0.5f)) { Advance(); if (mode != Mode.Drive || !this) return; }
                else break;
            }
            if (freeT > 4f) astray = 0;
            if (Astray(dt, pos)) return;

            float speed = Mathf.Abs(fwd);
            car.aiSteer = SteerTo(Aim(Mathf.Clamp(speed * 0.9f, 4.5f, 13f)));

            // bends ahead set the speed: the sharpest turn of the lane over the next ~30 m, and how soon it comes
            float targetSpeed = Mathf.Min(BendSpeed(pos), branchLimit);
            Vector3 ahead = Lane(idx + dir * 4) - pos;
            ahead.y = 0;
            float bend = Vector3.Angle(transform.forward, ahead);   // (the horn before a blind bend: Manners)
            if (shaken > 0f) { shaken -= dt; targetSpeed = 0f; }
            if (route != null && route[route.Count - 1].r == road)   // the last road of a route: stop at the goal
            {
                Vector3 g = routeGoal - pos; g.y = 0f;
                if (g.magnitude < 9f) Arrived = true;
                targetSpeed = Arrived ? 0f : Mathf.Min(targetSpeed, Mathf.Sqrt(2f * 3f * Mathf.Max(0f, g.magnitude - 6f)) + 0.5f);
            }

            // walls, railings, parked cars, the hillside, a drop at the road's edge: feelers ahead steer it off and slow it
            targetSpeed = Mathf.Min(targetSpeed, Feel(pos, speed));
            if (net.NearIsland(pos, 9f)) targetSpeed = Mathf.Min(targetSpeed, 5f);   // round a junction island at a junction's pace

            float freeSpeed = targetSpeed, aheadDist = float.MaxValue;   // the speed it would go with nobody in front (overtaking)
            // something in front? (cars, people, the player): keep a gap of about two seconds, match a car's speed
            float look = 6f + speed * 2f;
            Vector3 from = pos + Vector3.up * 1f + transform.forward * (spec.length * 0.5f + 0.3f);
            Rigidbody blocker = null;
            Transform standingPerson = null;
            bool personAhead = false;
            if (Physics.SphereCast(from, 0.9f, transform.forward, out var hit, look, obstacleMask, QueryTriggerInteraction.Ignore) &&
                !hit.collider.transform.IsChildOf(transform) &&
                !(passing && (hit.rigidbody ? hit.rigidbody.transform : hit.collider.transform) == passing && hit.distance > 0.8f))   // the one it goes round
            {
                float lead = 0f;
                if (hit.rigidbody && !hit.rigidbody.isKinematic) lead = Mathf.Max(0f, Vector3.Dot(hit.rigidbody.linearVelocity, transform.forward));
                bool person = hit.collider.gameObject.layer == 8 || hit.collider.gameObject.layer == 9;
                aheadDist = hit.distance;
                float gap = hit.distance - (person ? 3.5f : 2.5f);
                targetSpeed = gap < 1f ? 0f : Mathf.Min(targetSpeed, lead + gap * 0.45f);
                if (!person && hit.rigidbody && hit.rigidbody != rb) blocker = hit.rigidbody;
                personAhead = person;
                // someone standing in the road (a policeman at his post, the player), or walking on ahead and not moving over
                if (person && hit.collider.GetComponent<CharacterController>() is CharacterController pc && (pc.velocity.sqrMagnitude < 0.09f || behindT > 4f))
                    standingPerson = hit.collider.transform;
            }
            // the cast above never reports what it starts inside: right at the bumper is looked at by itself
            var atNose = PersonAtNose();
            if (atNose)
            {
                personAhead = true;
                // going round them it creeps, else it stands: never the pedal down against somebody
                targetSpeed = Mathf.Min(targetSpeed, passing == atNose.transform ? 1.2f : 0f);
                if (atNose.velocity.sqrMagnitude < 0.09f || behindT > 4f) standingPerson = atNose.transform;
            }
            behindT = personAhead && targetSpeed < 3f ? behindT + dt : Mathf.Max(0f, behindT - dt);
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
            if (Time.time < stopUntil) targetSpeed = 0f;   // someone getting on / off (BusPassenger)
            // a red light (TrafficSignals): the nose stops at its line, eased in at ~3 m/s2
            var sig = TrafficSignals.Instance;
            if (sig && sig.HoldAt(pos + transform.forward * (spec.length * 0.5f), transform.forward, speed, out float sigD))
                targetSpeed = Mathf.Min(targetSpeed, Mathf.Sqrt(2f * 3f * Mathf.Max(0f, sigD - 1f)));

            // a vehicle standing in the lane that will not move: after 3 s, round it on the right (nothing coming); a
            // person who stays put in the road (he did not step aside): after 6 s, round them too
            bool still = (blocker && WontMove(blocker)) || standingPerson;
            if (still && !passing && !held && !siren && oncoming < 0.2f)
            {
                waitT += dt;
                if (waitT > (standingPerson ? 6f : 3f)) StartPass(standingPerson ? standingPerson : blocker.transform, pos, standingPerson ? 0.7f : -1f);
            }
            else waitT = 0f;

            // a slower vehicle ahead (owner, 23 Nov 2026: "agar NPC driver ko aisa lgra hai ki aage waala uske comparison me
            // hlke hain or saamne se koi vehicle nhi aara to overtake krle"): 2 s held well under the speed it wants, on a
            // straight two-lane stretch with no junction in it, the other lane clear for as far as the pass takes (its own
            // way past + what an oncoming car covers meanwhile), room to pull in ahead of it - then out on the right, past,
            // and back in. Something coming after all before it is alongside: it drops back.
            if (blocker && !still && !passing && !held && !siren && Time.time >= overtakeAt && mode == Mode.Drive)
            {
                float leadV = Vector3.Dot(blocker.linearVelocity, transform.forward);
                bool slower = freeSpeed > 5f && leadV < freeSpeed - 2f && leadV < freeSpeed * 0.8f;
                slowT = slower ? slowT + dt : 0f;
                if (slowT > 2f)
                {
                    slowT = 0f;
                    overtakeAt = Time.time + 3f;   // looked: not again at once
                    if (CanOvertake(pos, blocker, leadV, freeSpeed, aheadDist))
                    {
                        StartPass(blocker.transform, pos);
                        overtaking = passing;
                        if (overtaking) { Overtakes++; overtakesHere++; }
                    }
                }
            }
            else if (!passing) slowT = 0f;
            if (overtaking)
            {
                if (!passing) overtaking = false;
                else
                {
                    Vector3 fo = transform.forward; fo.y = 0f; fo.Normalize();
                    float alongIt = Vector3.Dot(passing.position - pos, fo);
                    // not yet alongside and somebody now in the other lane close ahead: back in behind it
                    if (alongIt > 0f && !OtherLaneClear(pos, fo, Mathf.Clamp(speed * 3f + 20f, 25f, 60f), null, 0f, 0f))
                    {
                        passing = null;
                        overtaking = false;
                        overtakeAt = Time.time + 10f;
                        Aborts++;
                    }
                    else targetSpeed = Mathf.Max(targetSpeed, Mathf.Min(freeSpeed, speed + 3f));   // on the gas past it
                }
            }

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
            if (speed < 0.3f && !held && !siren && !Arrived) stuck += dt; else stuck = 0f;   // waiting at an accident (or parked at its goal) is not being stuck
            // nosed into something (a wall the ray missed, a kerb, another car's corner): pressing on and not moving
            stillT = speed < 0.4f && (frontWall < 3.5f || car.aiThrottle > 0.5f) ? stillT + dt : 0f;
            // (a touch it feels acts at once: Touched. This is for what gives no contact - a kerb under the wheels, a thing it
            // stopped short of; a steep start needs its 2 s of climbing throttle)
            if (stillT > (frontWall < 1.5f ? 0.5f : climb > 0.06f ? 2.5f : 1.2f)) { stillT = 0f; Wedged(pos); }
            var ahead2 = blocker ? blocker.GetComponent<TrafficCar>() : null;
            bool inQueue = ahead2 && Vector3.Dot(ahead2.transform.forward, transform.forward) > 0.3f;
            // two that wait for each other (nose to nose, or across each other in a junction): one backs and goes round
            if (ahead2 && !inQueue && !held && !siren && stuck > 5f && ahead2.stuck > 3f && GivesWay(ahead2) && !Blocked(-transform.forward, 2.5f))
            {
                LastHit = $"gave way to {ahead2.name} at ({pos.x:F1},{pos.z:F1})";
                stuck = 0f;
                BeginBump(1, 0f);
                return;
            }
            // held up for long: turn round - never out of a queue, never in a junction, never a coach
            if (stuck > 20f && stuck - dt <= 20f && route == null && !inQueue && spec.length <= 6.5f && net.roads[road].oneWay == 0 && JunctionEnd() < 0) TurnRound();
            if (stuck > 40f && !Seen(pos) && !Hop()) Leave();   // wedged for good: on its lane ahead once nobody is looking, else gone
        }

        // ------------------------------------------------------------------ a planned way (DrivePath)
        readonly List<DrivePath.Point> path = new List<DrivePath.Point>();
        int pathAt, pathRoad, pathIdx, pathDir;
        float pathT, pathStill, pathWait, pathCd;
        bool pathShift;
        Vector3 pathLastPos;
        /// <summary>How many planned ways the traffic drove to their end / gave up (checks: TrafficLab).</summary>
        public static int PathsDone, PathsDropped;

        /// <summary>A clear way to lane point i of the road it is on, facing along the lane.</summary>
        bool PathTo(int i)
        {
            var pts = Pts;
            if (i < 0 || i >= pts.Length) return false;
            float keepOff = passOff, keepPull = pull;
            passOff = 0f;   // the lane itself, not the dodge of the moment
            pull = 0f;
            Vector3 goal = Lane(i), way = Lane(i + dir) - Lane(i - dir);
            passOff = keepOff;
            pull = keepPull;
            way.y = 0f;
            if (way.sqrMagnitude < 0.01f) return false;
            Vector3 half = new Vector3(spec.width * 0.5f, 0f, spec.length * 0.5f);
            var box = GetComponent<BoxCollider>();
            if (box) half = new Vector3(box.size.x * 0.5f + 0.06f, 0f, box.size.z * 0.5f + 0.06f);
            if (!DrivePath.Find(rb, transform.position, transform.forward, half, spec.steer, spec.length * 0.62f, goal, way, path)) return false;
            mode = Mode.Path;
            pathAt = 1;
            pathRoad = road; pathIdx = i; pathDir = dir;
            pathT = pathStill = pathWait = 0f;
            pathShift = false;
            pathLastPos = transform.position;
            float turn = Vector3.SignedAngle(new Vector3(transform.forward.x, 0f, transform.forward.z), way, Vector3.up);
            turnSignal = Mathf.Abs(turn) < RoadChoice.TurnAngle ? 0 : turn > 0f ? 1 : -1;
            dodgeT = 0f;
            passing = null;
            passOff = 0f;
            stuck = stillT = 0f;
            return true;
        }

        /// <summary>A clear way back onto its lane 10, 15 or 6 m on (the first there is).</summary>
        bool PathToLane()
        {
            if (pathCd > 0f) return false;
            pathCd = 1f;   // a search is a few milliseconds: not every step
            Relocate();
            foreach (float metres in new[] { 10f, 15f, 6f })
            {
                int i = idx;
                float run = 0f;
                while (run < metres && i + dir >= 0 && i + dir < Pts.Length) { run += Vector3.Distance(Pts[i], Pts[i + dir]); i += dir; }
                if (i != idx && PathTo(i)) return true;
            }
            return false;
        }

        /// <summary>The lane runs into a junction island within ~14 m: onto the road it takes there now, and one way round
        /// the island to that road's lane beyond it (the side is the search's: the nearest that this body fits).</summary>
        bool EnterIsland(Vector3 pos)
        {
            if (pathCd > 0f || !net.IslandNear(pos, 11f, out var island)) return false;
            var pts = Pts;
            int end = dir > 0 ? pts.Length - 1 : 0;
            Vector3 c = island.centre;
            float Off(Vector3 p) => new Vector2(p.x - c.x, p.z - c.z).magnitude;
            bool endsThere = Off(pts[end]) < island.radius + 1f && Mathf.Abs(end - idx) <= 6;
            bool startsThere = Off(pts[Mathf.Clamp(idx, 0, pts.Length - 1)]) < island.radius + 1f;
            if (!endsThere && !startsThere) return false;
            if (endsThere)
            {
                idx = end - dir;
                Advance();
                if (!this || mode != Mode.Drive) return true;
            }
            pathCd = 1f;
            foreach (float beyond in new[] { 7f, 11f, 4.5f })
            {
                int i = idx;
                while (i + dir >= 0 && i + dir < Pts.Length && Off(Pts[i]) < island.radius + beyond) i += dir;
                if (i != idx && PathTo(i)) return true;
            }
            return false;
        }

        void EndPath(bool done)
        {
            mode = Mode.Drive;
            prevRoad = -1;
            stillT = 0f;
            if (done)
            {
                PathsDone++;
                road = pathRoad; idx = pathIdx; dir = pathDir;
                tries = 0;
                astray = 0;
                pathCd = 0.5f;
            }
            else
            {
                PathsDropped++;
                pathCd = 0f;
                Relocate();
            }
            path.Clear();
        }

        /// <summary>Along the planned way, leg by leg (a stop at each change of gear); people and moving vehicles in it are
        /// waited for, standing ones after 4 s planned round.</summary>
        void OnPath(float dt, Vector3 pos, float fwd)
        {
            pathT += dt;
            car.aiHandbrake = 0f;
            if (pathAt >= path.Count) { EndPath(true); return; }
            int gear = path[pathAt].gear, last = pathAt;
            while (last + 1 < path.Count && path[last + 1].gear == gear) last++;
            while (pathAt <= last)
            {
                Vector3 rel = pos - path[pathAt].pos;
                rel.y = 0f;
                float short_ = pathAt == last ? 0.35f : 0.1f;   // a little before a leg's end: it still rolls
                if (Vector3.Dot(rel, path[pathAt].forward * gear) < -short_) break;
                pathAt++;
            }
            if (pathAt > last)
            {
                if (pathAt >= path.Count) { EndPath(true); return; }
                pathShift = true;
            }
            if (pathShift)
            {
                if (Mathf.Abs(fwd) > 0.5f) { car.aiThrottle = -Mathf.Sign(fwd); return; }
                pathShift = false;
                gear = path[pathAt].gear;
                last = pathAt;
                while (last + 1 < path.Count && path[last + 1].gear == gear) last++;
            }

            Vector3 off = pos - path[pathAt].pos;
            off.y = 0f;
            if (off.magnitude > 3f || pathT > 25f) { EndPath(false); return; }   // knocked off it

            int aimAt = pathAt;
            while (aimAt < last && (path[aimAt].pos - pos).sqrMagnitude < 2.2f * 2.2f) aimAt++;
            Vector3 aim = path[aimAt].pos;
            if (aimAt == last) aim += path[last].forward * (gear * 1.5f);   // on through the leg's end, straight
            Vector3 local = transform.InverseTransformPoint(aim);
            float d2 = Mathf.Max(local.x * local.x + local.z * local.z, 1f);
            car.aiSteer = Mathf.Clamp(Mathf.Atan(2f * spec.length * 0.62f * local.x / d2) * Mathf.Rad2Deg * 1.15f, -spec.steer, spec.steer);

            Vector3 d = transform.forward * gear;
            Vector3 from = pos + Vector3.up + d * (spec.length * 0.5f + 0.1f);
            bool inWay = Physics.SphereCast(from, 0.6f, d, out var hit, 1.8f, obstacleMask, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(transform);
            if (inWay)
            {
                pathWait += dt;
                car.aiThrottle = Mathf.Abs(fwd) > 0.3f ? -Mathf.Sign(fwd) : 0f;
                car.aiHandbrake = Mathf.Abs(fwd) < 1f ? 1f : 0f;
                if (pathWait > 4f && hit.collider.gameObject.layer == VehicleFactory.VehicleLayer) EndPath(false);
                pathT -= dt;   // waiting is not failing
                return;
            }
            pathWait = 0f;
            float want = gear > 0 ? (last - pathAt > 4 ? 3.2f : 1.8f) : -1.6f;
            float climb = transform.forward.y * 2.5f;
            car.aiThrottle = Mathf.Clamp((want - fwd) * 0.6f + climb, -1f, 1f);

            pathStill = (pos - pathLastPos).sqrMagnitude < 0.15f * 0.15f ? pathStill + dt : 0f;
            if (pathStill == 0f) pathLastPos = pos;
            if (pathStill > 1f) { EndPath(false); Wedged(pos); }   // pressed on something the boxes did not see: the next way
        }

        bool Seen(Vector3 pos)
        {
            var cam = Camera.main;
            if (!cam) return false;
            Vector3 vp = cam.WorldToViewportPoint(pos);
            return vp.z > 0 && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f && vp.z < 120f;
        }

        // ------------------------------------------------------------------ hit: stop, back out, round it
        enum Bump { None, Brake, Pause, Out, Settle }
        Bump bump;
        int bumpEnd;   // the end that hit: +1 the front, -1 the back
        float bumpT, bumpWait, bumpSide, outWant;
        Vector3 outFrom;

        static readonly Collider[] atBumper = new Collider[8];

        /// <summary>Someone on foot within 1.2 m of the front bumper, in the car's width.</summary>
        CharacterController PersonAtNose()
        {
            Vector3 half = new Vector3(spec.width * 0.5f, 0.6f, 0.65f);
            Vector3 c = transform.position + transform.up * 0.95f + transform.forward * (spec.length * 0.5f + 0.55f);
            int n = Physics.OverlapBoxNonAlloc(c, half, atBumper, transform.rotation, (1 << 8) | (1 << 9), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (atBumper[i] is CharacterController who && who.enabled && !who.transform.IsChildOf(transform)) return who;
            return null;
        }

        void OnCollisionEnter(Collision c) => Touched(c);
        void OnCollisionStay(Collision c) => Touched(c);

        /// <summary>Contact at the end it is driving on (a wall, a pole, a board, a steep bank, a vehicle - not the road
        /// under it, not a person: CarController.People; not being run into from behind, not brushing along its side).</summary>
        void Touched(Collision c)
        {
            if (!car || Arrived || mode == Mode.Wrecked || mode == Mode.PullUp) return;
            var o = c.collider;
            if (!o || o.gameObject.layer == 8 || o.gameObject.layer == 9) return;
            float fwd = car.ForwardSpeed, half = spec.length * 0.5f;
            // the way it is driving: the pedal, unless it still rolls the other way
            int going = Mathf.Abs(fwd) > 0.6f ? (fwd > 0f ? 1 : -1) : car.aiThrottle > 0.05f ? 1 : car.aiThrottle < -0.05f ? -1 : 0;
            if (bump == Bump.Out) going = -bumpEnd;
            else if (bump != Bump.None) return;   // braking already
            else if (c.relativeVelocity.sqrMagnitude > 4f && going == 0) going = fwd >= 0f ? 1 : -1;   // the knock itself took its speed
            if (going == 0) return;
            Vector3 f = transform.forward;
            for (int i = 0; i < c.contactCount; i++)
            {
                var k = c.GetContact(i);
                if (k.normal.y > 0.6f) continue;   // the ground
                Vector3 local = transform.InverseTransformPoint(k.point);
                if (local.z * going < half * 0.6f) continue;        // not the end that leads
                if (Vector3.Dot(k.normal, f) * going > -0.35f) continue;   // along its side, or pushed from behind
                if (bump == Bump.Out) { bump = Bump.Settle; bumpT = 0f; Off(fwd); return; }   // as far back as there is room
                LastHit = $"{o.name}{(o.transform.parent ? "<" + o.transform.parent.name : "")} at ({k.point.x:F1},{k.point.y:F1},{k.point.z:F1}) at its ({local.x:F1},{local.z:F1})";
                BeginBump(going, local.x);
                return;
            }
        }

        /// <summary>Off the accelerator, the brake on.</summary>
        void Off(float fwd)
        {
            car.aiThrottle = Mathf.Abs(fwd) > 1.05f ? -Mathf.Sign(fwd) : 0f;   // (against the roll that is the brake; slower, the handbrake)
            car.aiHandbrake = 1f;
        }

        void BeginBump(int end, float side)
        {
            Vector3 pos = transform.position;
            if (tries == 0) troubleAt = pos;
            tries++;
            mostTries = Mathf.Max(mostTries, tries);
            Hits++;
            hitsHere++;
            if (mode == Mode.Path) EndPath(false);
            mode = Mode.Drive;
            bump = Bump.Brake;
            bumpEnd = end;
            bumpSide = side;
            bumpT = 0f;
            bumpWait = Random.Range(0.35f, 0.8f);   // two that met head-on do not move off in step
            stillT = climbT = wayT = dodgeT = 0f;
            passing = null;
            Off(car.ForwardSpeed);
        }

        /// <summary>The end of its road (0 / 1) whose junction it stands in, else -1.</summary>
        int JunctionEnd()
        {
            int last = Pts.Length - 1;
            int end = idx <= 5 ? 0 : idx >= last - 5 ? 1 : -1;
            if (end < 0) return -1;
            var links = net.Links(road, end);
            return links != null && links.Count > 0 ? end : -1;
        }

        /// <summary>Another road out of this junction, the one it already faces most.</summary>
        void OtherWay(int end)
        {
            int at = end == 0 ? 0 : Pts.Length - 1;
            Vector3 f = transform.forward;
            float best = float.MinValue;
            (int r, int i, int d) pick = (-1, 0, 0);
            foreach (var o in RoadChoice.Onward(net, road, at, true))
            {
                var q = net.roads[o.r].pts;
                Vector3 w = q[Mathf.Clamp(o.i + o.d * 3, 0, q.Length - 1)] - transform.position;
                w.y = 0f;
                float face = Vector3.Dot(f, w.normalized);
                if (face > best) { best = face; pick = o; }
            }
            if (pick.r < 0) return;
            road = pick.r;
            idx = pick.i;
            dir = pick.d;
            astray = 0;
        }

        /// <summary>Of two that wait for each other, the one that backs: the shorter (a coach keeps its place), else one of
        /// the two, always the same; after 15 s whichever can.</summary>
        bool GivesWay(TrafficCar other)
        {
            if (stuck > 15f) return true;
            if (Mathf.Abs(spec.length - other.spec.length) > 1f) return spec.length < other.spec.length;
            return GetInstanceID() < other.GetInstanceID();
        }

        /// <summary>Not moving though nothing was felt (a kerb under the wheels, a thing just short of the bumper): the
        /// same as a hit at the front.</summary>
        void Wedged(Vector3 pos)
        {
            if (tries >= 7 && !Seen(pos) && Hop()) return;
            if (bump == Bump.None) { LastHit = $"wedged at ({pos.x:F1},{pos.z:F1})"; BeginBump(1, 0f); }
        }

        void Bumped(float dt, Vector3 pos, float fwd)
        {
            bumpT += dt;
            int way = -bumpEnd;   // the way out
            switch (bump)
            {
                case Bump.Brake:
                    car.aiSteer = 0f;
                    Off(fwd);
                    if (Mathf.Abs(fwd) < 0.4f || bumpT > 1.5f) { bump = Bump.Pause; bumpT = 0f; }
                    return;

                case Bump.Pause:
                    Off(fwd);
                    if (bumpT < bumpWait) return;
                    bump = Bump.Out;
                    bumpT = 0f;
                    outFrom = pos;
                    outWant = 2f + 0.8f * Mathf.Min(tries - 1, 3);   // further back each time at the same place
                    BackOffs++;
                    // the nose swung from what it hit (backing, the nose goes against the wheel); head-on: towards the lane
                    float turn = Mathf.Abs(bumpSide) > 0.25f ? Mathf.Sign(bumpSide) : -Mathf.Sign(SteerTo(Aim(8f)));
                    car.aiSteer = (bumpEnd > 0 ? turn : 0f) * spec.steer * 0.8f;
                    return;

                case Bump.Out:
                    Vector3 d = transform.forward * way;
                    if ((pos - outFrom).sqrMagnitude > outWant * outWant || bumpT > 4f || Blocked(d, 0.7f) || Edge(pos, d))
                    {
                        bump = Bump.Settle;
                        bumpT = 0f;
                        Off(fwd);
                        return;
                    }
                    car.aiHandbrake = 0f;
                    car.aiThrottle = Mathf.Clamp((way * 1.7f - fwd) * 0.6f + transform.forward.y * 2.5f, -1f, 1f);
                    return;

                case Bump.Settle:
                    Off(fwd);
                    if (Mathf.Abs(fwd) > 0.4f && bumpT < 1.2f) return;
                    bump = Bump.None;
                    GetOut(pos);
                    return;
            }
        }

        /// <summary>No road under the end that leads: a drop, the lake.</summary>
        bool Edge(Vector3 pos, Vector3 d)
        {
            Vector3 at = pos + d * (spec.length * 0.5f + 0.4f);
            return !Physics.Raycast(at + Vector3.up * 1.5f, Vector3.down, out var g, 3.5f, groundMask, QueryTriggerInteraction.Ignore) || g.point.y < at.y - 0.9f;
        }

        /// <summary>Backed out: by another way, not at the same thing again.</summary>
        void GetOut(Vector3 pos)
        {
            Relocate();
            stillT = 0f;
            pathCd = 0f;
            if (tries >= 7 && !Seen(pos) && Hop()) return;
            if (tries >= 4 && tries % 4 == 0 && route == null)
            {
                // shut. In a junction never a three-point turn (a coach across it stopped the whole Mall): another of its
                // roads, not at the same thing again. On the road between: back the way it came.
                int end = JunctionEnd();
                if (end >= 0) OtherWay(end);
                else if (net.roads[road].oneWay == 0) { TurnRound(); return; }
            }
            if (PathToLane()) return;
            float away = bumpSide > 0.25f ? -1f : 1f;   // the side away from what it hit first
            float open = OpenSide(pos, new[] { away * 1.4f, away * 2.6f, -away * 1.4f, -away * 2.6f }, 6f);
            if (open != 0f) { dodgeWant = open; dodgeT = 7f; }
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
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                transform.SetPositionAndRotation(p + Vector3.up * 0.35f, rot);
                Physics.SyncTransforms();
                idx = i;
                tries = 0; stuck = 0f; stillT = 0f; bump = Bump.None; dodgeT = 0f;
                Hops++;
                hopsHere++;
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
            return tc.Wrecked || !tc.HasDriver || tc.tries > 0;
        }

        // ---- overtaking a slower vehicle (owner, 23 Nov 2026)
        float slowT, overtakeAt;
        bool overtaking;
        int overtakesHere;
        /// <summary>Overtakes started / dropped back from, by all traffic (tests).</summary>
        public static int Overtakes, Aborts;
        public bool Overtaking => overtaking;
        public int OvertakesHere => overtakesHere;

        /// <summary>May it overtake `o` (ahead at `gap` m, going leadV) wanting `want` m/s? Room beside it, no junction near,
        /// the lane straight (within 10 deg) for its own way past, the other lane clear for that + an oncoming car's way
        /// meanwhile, and room ahead of it to pull back in.</summary>
        bool CanOvertake(Vector3 pos, Rigidbody o, float leadV, float want, float gap)
        {
            var r = net.roads[road];
            var oi = o.GetComponent<VehicleInfo>();
            float ow = oi ? oi.width : 1.9f, ol = oi ? oi.length : 4.4f;
            if (r.width < spec.width + ow + 1.2f) return false;   // no room beside it
            if (net.NearIsland(pos, 45f) || (TrafficSignals.Instance && TrafficSignals.Instance.Near(pos, 60f)) || route != null && route[route.Count - 1].r == road && (routeGoal - pos).sqrMagnitude < 120f * 120f) return false;
            float passV = Mathf.Min(want, leadV + 6f), rel = passV - leadV;
            if (rel < 2f || gap > 40f) return false;
            float past = gap + ol + spec.length + 10f;   // from behind it to a car's length clear ahead of it
            float t = past / rel;
            float own = passV * t, need = own + 12f * t + 15f;   // + what a car coming at ~43 km/h covers meanwhile
            if (need > 170f) return false;   // too long a pass for a hill road
            Vector3 f = transform.forward;
            f.y = 0f;
            f.Normalize();
            // the lane straight that far, on this road (a bend is blind; the road's end is a junction)
            float covered = 0f;
            Vector3 prev = pos;
            for (int k = 1; covered < own + 10f; k++)
            {
                int i = idx + dir * k;
                if (k > 120 || i < 0 || i >= Pts.Length) return false;
                Vector3 q = Lane(i), d = q - pos;
                d.y = 0f;
                if (d.magnitude > 6f && Vector3.Angle(f, d) > 10f) return false;
                covered += Vector3.Distance(new Vector3(prev.x, 0f, prev.z), new Vector3(q.x, 0f, q.z));
                prev = q;
            }
            return OtherLaneClear(pos, f, need, o, gap + ol * 0.5f, gap + ol + spec.length + 8f);
        }

        static readonly Collider[] laneHits = new Collider[32];

        /// <summary>Nobody in the other lane ahead within `need` m (coming or standing; vehicles and the player), and - from
        /// mergeFrom to mergeTo ahead in its own lane (the gap ahead of the one it passes) - nobody to pull in on. Sides
        /// are measured from the road's middle line, so it holds while it is out in the other lane.</summary>
        bool OtherLaneClear(Vector3 pos, Vector3 f, float need, Rigidbody passed, float mergeFrom, float mergeTo)
        {
            var r = net.roads[road];
            var pts = Pts;
            Vector3 mid = pts[Mathf.Clamp(idx, 0, pts.Length - 1)];
            Vector3 right = Vector3.Cross(Vector3.up, f);
            float half = r.width * 0.5f + 0.5f;
            Vector3 c = pos + f * (need * 0.5f) + Vector3.up * 1f;
            c += right * Vector3.Dot(mid - pos, right);   // over the road's middle
            int n = Physics.OverlapBoxNonAlloc(c, new Vector3(half, 2f, need * 0.5f), laneHits, Quaternion.LookRotation(f),
                                               (1 << VehicleFactory.VehicleLayer) | (1 << 8), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var o = laneHits[i].attachedRigidbody;
                Transform t = o ? o.transform : laneHits[i].transform;
                if (t == transform || t.IsChildOf(transform) || (passed && o == passed)) continue;
                Vector3 rel = t.position - pos;
                float along = Vector3.Dot(rel, f);
                if (along < -2f) continue;   // behind
                float side = Vector3.Dot(t.position - mid, right);   // + = the other lane (left-hand traffic)
                if (side > 0.2f) return false;   // in the other lane ahead
                if (mergeTo > 0f && along > mergeFrom && along < mergeTo) return false;   // where it would pull back in
            }
            return true;
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
                prevRoad = -1;
                Relocate();
                stillT = 0f;
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
                float reach = Mathf.Max(OffCentre(pos + fw * hl + rt * hw), OffCentre(pos + fw * hl - rt * hw));
                if (turnTimer < 0.15f) edgeFrom = reach;   // already past the edge (that is how it got wedged): only further out counts
                bool edge = turnTimer > 0.6f && reach > Mathf.Max(half - 0.35f, edgeFrom + 0.1f);
                if (edge || stalled || Blocked(fw, 1.4f) || turnTimer > 8f || moved > 9f) NextPhase(Mode.TurnBack, pos);
            }
            else
            {
                car.aiSteer = -side * spec.steer;
                car.aiThrottle = fwd > 0.3f ? -0.6f : Mathf.Clamp((-1.6f - fwd) * 0.5f, -0.6f, 1f);
                float reach = Mathf.Max(OffCentre(pos - fw * hl + rt * hw), OffCentre(pos - fw * hl - rt * hw));
                if (turnTimer < 0.15f) edgeFrom = reach;
                bool edge = turnTimer > 0.6f && reach > Mathf.Max(half - 0.35f, edgeFrom + 0.1f);
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
                // a thing standing in the lane (a post, a sign, a bench's end): round it on an open side with ground, the
                // left first, the right only with nobody coming (a wall right across has no open side: the lane steers it)
                if (h.distance < 10f && dodgeT <= 0f && !passing && !(h.collider is TerrainCollider) && Vector3.Dot(-h.normal, fwd) > 0.7f && !net.NearIsland(pos, 10f))
                {
                    float open = OpenSide(pos, oncoming > 0.2f ? leftOnly : eitherSide, h.distance + 3f);
                    if (open != 0f) { dodgeWant = open; dodgeT = 3.5f; Dodges++; }
                }
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

        /// <summary>How often the traffic went round a small thing in its lane (checks: TrafficLab).</summary>
        public static int Dodges;

        static readonly RaycastHit[] clearHits = new RaycastHit[8];
        static readonly float[] eitherSide = { -1.4f, 1.4f, -2.6f, 2.6f }, leftOnly = { -1.4f, -2.6f };

        /// <summary>The first of these moves across (+ = right) with a clear path ahead and ground under it at the road's
        /// level (not off the hill, not into the lake), or 0.</summary>
        float OpenSide(Vector3 pos, float[] moves, float dist)
        {
            Vector3 fwd = transform.forward, right = transform.right;
            float hl = spec.length * 0.5f;
            foreach (float s in moves)
            {
                if (!ClearAhead(pos + Vector3.up * 0.95f + right * s + fwd * (hl * 0.9f), Mathf.Min(spec.width * 0.4f, 0.7f), fwd, dist)) continue;
                Vector3 at = pos + right * s + fwd * (hl + Mathf.Min(dist, 5f));
                if (!Physics.Raycast(at + Vector3.up * 2f, Vector3.down, out var g, 4f, groundMask, QueryTriggerInteraction.Ignore) || Mathf.Abs(g.point.y - at.y) > 0.9f) continue;
                return s;
            }
            return 0f;
        }

        /// <summary>Nothing but the ground along this path (a sphere of the car's half width).</summary>
        static bool ClearAhead(Vector3 from, float radius, Vector3 dirWorld, float dist)
        {
            if (wallMask < 0) wallMask = ~((1 << VehicleFactory.VehicleLayer) | (1 << 8) | (1 << 9) | (1 << 2) | (1 << 4));
            int n = Physics.SphereCastNonAlloc(from, radius, dirWorld, clearHits, dist, wallMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = clearHits[i].collider;
                if (c is TerrainCollider && clearHits[i].normal.y > 0.6f) continue;   // the road going up or down
                if (clearHits[i].distance <= 0f && clearHits[i].point == Vector3.zero && c is TerrainCollider) continue;
                return false;
            }
            return true;
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
            if ((targetSpeed < 0.5f && speed < 1f) || behindT > 0.8f) blocked += dt; else blocked = 0f;   // (crawling behind someone on foot is held up too: horn, dipper)
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
