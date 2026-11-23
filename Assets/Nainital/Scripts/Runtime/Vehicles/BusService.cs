using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// The timetabled government buses (BusLines; owner, 24 Nov 2026). Many buses, few on the road: every trip is a
    /// "ghost" - where it is follows from its departure time along its planned route (driving ~7 m/s, standing at each stop)
    /// - and only the trips near the player are real buses (TrafficCar on a bus_roadways, BusPassenger so he can ride, the
    /// LED destination boards). A real bus out of sight and far goes back to a ghost at the place it had reached; a ghost
    /// coming near (out of view, or far enough not to pop) becomes a real bus there. At most RealMax real buses (phones 2);
    /// each counts as two cars in the town's traffic (TrafficManager), whose own random buses stop while this runs.
    /// Riding one (BusPassenger), the stops are called out: "... aa gaya" about 4 s before a stop and "Agla stop: ..." as it moves off one, each signed "Seva mein, Uttarakhand Parivahan Nigam, Nainital"
    /// (a ding-dong, then the Hindi voice: Resources/Audio/announce/&lt;stop&gt;_next / _here, Tools/buses/make_announce.ps1).
    /// </summary>
    public class BusService : MonoBehaviour
    {
        public static BusService Instance { get; private set; }
        public static int Real { get; private set; }

        public int realMax = 4;
        public float ghostSpeed = 7f;          // m/s along the route, junctions and climbs included
        public float stopDwell = 18f;          // s at a stop
        public float firstDwell = 40f;         // s at the first stop (boarding at the bus stand)
        public float spawnNear = 260f, despawnFar = 340f;

        class Route
        {
            public BusLines.Line line;
            public List<(int r, int d, int i)> keys;
            public Vector3[] pts;        // the line driven
            public float[] s;            // metres along it at each point
            public int[] step;           // which key each point belongs to
            public int[] idx;            // that road's point index
            public float[] stopS;        // metres along it at each stop
            public Vector3 goal;
            public float Length => s.Length > 0 ? s[s.Length - 1] : 0f;
        }

        class Trip
        {
            public Route route;
            public float t0;             // its departure (Time.time)
            public TrafficCar bus;
            public int nextStop = 1;     // for the real bus: the stop it is heading for
            public float progress;       // metres along the route (the real bus's, kept up)
            public bool standing;        // stood at a stop and not yet off again
            public float stoodAt;
            public float endedAt = -1f;
            public int callIdx = -1;     // the stop last called out to the player
        }

        readonly Dictionary<string, Route> routes = new Dictionary<string, Route>();
        readonly List<Trip> trips = new List<Trip>();
        float start, nextLocalCheck, timer;
        int outRota, inRota;
        float nextOut, nextIn;
        readonly Dictionary<string, float> nextLocal = new Dictionary<string, float>();
        bool ready;
        TrafficManager tm;
        int busModel = -1;

        /// <summary>One BusService (GameManager.Awake).</summary>
        public static BusService Ensure()
        {
            if (Instance) return Instance;
            return new GameObject("Bus Service").AddComponent<BusService>();
        }

        void Awake()
        {
            Instance = this;
            if (Application.isMobilePlatform) realMax = 2;
        }

        void OnDestroy() { if (Instance == this) { Instance = null; Real = 0; } }

        IEnumerator Start()
        {
            // the road network is there a moment after the scene starts
            while (!(tm = FindAnyObjectByType<TrafficManager>()) || !tm.network || tm.network.roads == null || tm.network.roads.Length == 0)
                yield return new WaitForSeconds(1f);
            var lib = VehicleLibrary.Instance;
            if (lib) for (int k = 0; k < lib.entries.Length; k++) if (lib.entries[k] != null && lib.entries[k].name == "bus_roadways") busModel = k;
            int ok = 0;
            foreach (var l in BusLines.All)
            {
                var r = Plan(tm.network, l);
                if (r != null) { routes[l.id] = r; ok++; }
                yield return null;   // a route a frame
            }
            Debug.Log($"[Nainital] bus service: {ok}/{BusLines.All.Length} routes planned, bus model {busModel}");
            start = Time.time;
            // the day already under way: the trips that left in the last while are out on their routes now
            foreach (var l in BusLines.All)
            {
                if (!routes.TryGetValue(l.id, out var r) || l.every <= 0f) continue;
                float dur = Duration(r);
                for (float t = start + l.offset - l.every; t > start - dur; t -= l.every) AddTrip(r, t);
                nextLocal[l.id] = start + l.offset;
            }
            for (int k = 0; k < BusLines.OutRota.Length; k++)
            {
                var ro = Get(BusLines.OutRota[k]);
                var ri = Get(BusLines.InRota[k]);
                float t = start - (k + 1) * BusLines.IntercityEvery;
                if (ro != null && t > start - Duration(ro)) AddTrip(ro, t);
                if (ri != null && t + BusLines.IntercityEvery * 0.5f > start - Duration(ri)) AddTrip(ri, t + BusLines.IntercityEvery * 0.5f);
            }
            nextOut = start + 30f;
            nextIn = start + 30f + BusLines.IntercityEvery * 0.5f;
            ready = true;
        }

        Route Get(string id) => routes.TryGetValue(id, out var r) ? r : null;

        void AddTrip(Route r, float t0) => trips.Add(new Trip { route = r, t0 = t0 });

        // ------------------------------------------------------------------ planning
        static Route Plan(RoadNetwork net, BusLines.Line l)
        {
            int n = l.stops.Length;
            var rs = new int[n];
            var ix = new int[n];
            var at = new Vector3[n];
            for (int k = 0; k < n; k++)
            {
                float best = float.MaxValue;
                for (int ri = 0; ri < net.roads.Length; ri++)
                {
                    var road = net.roads[ri];
                    if (road.width < 4.5f || road.pts.Length < 3) continue;   // a road a bus fits on
                    if (l.stops[k].road != null && road.name != l.stops[k].road) continue;
                    for (int i = 1; i < road.pts.Length - 1; i++)
                    {
                        float d = new Vector2(road.pts[i].x - l.stops[k].at.x, road.pts[i].z - l.stops[k].at.y).sqrMagnitude;
                        if (d < best) { best = d; rs[k] = ri; ix[k] = i; at[k] = road.pts[i]; }
                    }
                }
                if (Mathf.Sqrt(best) > 60f) Debug.LogWarning($"[Nainital] bus line {l.id}: stop {l.stops[k].name} is {Mathf.Sqrt(best):F0} m from a bus road");
            }
            var keys = new List<(int r, int d, int i)>();
            int way = 0;
            for (int k = 0; k + 1 < n; k++)
            {
                var leg = RoadPlanner.PlanEntries(net, rs[k], ix[k], rs[k + 1], ix[k + 1], way);
                if (leg == null && way != 0)   // no way on the way it came: it turns round at this stop
                {
                    leg = RoadPlanner.PlanEntries(net, rs[k], ix[k], rs[k + 1], ix[k + 1], 0);
                    if (leg != null) Debug.Log($"[Nainital] bus line {l.id}: turns round at {l.stops[k].name}");
                }
                if (leg == null) { Debug.LogWarning($"[Nainital] bus line {l.id}: no way from {l.stops[k].name} to {l.stops[k + 1].name}"); return null; }
                for (int s = 0; s < leg.Count; s++)
                {
                    if (s == 0 && keys.Count > 0 && keys[keys.Count - 1].r == leg[0].r && keys[keys.Count - 1].d == leg[0].d) continue;   // the same road on
                    keys.Add(leg[s]);
                }
                way = leg[leg.Count - 1].d;
            }
            // the line: each road from its entry, the way it is driven, to the point nearest the next road's entry (the last: the goal)
            var pts = new List<Vector3>();
            var stepOf = new List<int>();
            var idxOf = new List<int>();
            for (int j = 0; j < keys.Count; j++)
            {
                var (r, d, i0) = keys[j];
                var q = net.roads[r].pts;
                Vector3 target = j + 1 < keys.Count ? net.roads[keys[j + 1].r].pts[keys[j + 1].i] : at[n - 1];
                int bestI = i0;
                float bd = float.MaxValue;
                for (int i = i0; i >= 0 && i < q.Length; i += d)
                {
                    float dd = (q[i] - target).sqrMagnitude;
                    if (dd < bd) { bd = dd; bestI = i; }
                }
                for (int i = i0; ; i += d)
                {
                    if (pts.Count == 0 || (pts[pts.Count - 1] - q[i]).sqrMagnitude > 0.25f) { pts.Add(q[i]); stepOf.Add(j); idxOf.Add(i); }
                    if (i == bestI) break;
                }
            }
            var R = new Route { line = l, keys = keys, pts = pts.ToArray(), step = stepOf.ToArray(), idx = idxOf.ToArray(), goal = at[n - 1] };
            R.s = new float[R.pts.Length];
            for (int i = 1; i < R.pts.Length; i++) R.s[i] = R.s[i - 1] + Vector3.Distance(R.pts[i - 1], R.pts[i]);
            // each stop: the nearest point of the line, in order along it
            R.stopS = new float[n];
            int from = 0;
            for (int k = 0; k < n; k++)
            {
                int bi = from;
                float bd = float.MaxValue;
                for (int i = from; i < R.pts.Length; i++)
                {
                    float d = (R.pts[i] - at[k]).sqrMagnitude;
                    if (d < bd) { bd = d; bi = i; }
                }
                R.stopS[k] = R.s[bi];
                from = bi;
            }
            Debug.Log($"[Nainital] bus line {l.id}: {keys.Count} roads, {R.Length / 1000f:F1} km, {n} stops");
            return R;
        }

        float Duration(Route r) => r.Length / ghostSpeed + firstDwell + stopDwell * Mathf.Max(0, r.stopS.Length - 2);

        /// <summary>Metres along the route `e` seconds after it left (standing time at the stops counted).</summary>
        float ProgressAt(Route r, float e)
        {
            if (e <= firstDwell) return 0f;
            e -= firstDwell;
            float s = 0f;
            for (int k = 1; k < r.stopS.Length; k++)
            {
                float leg = (r.stopS[k] - s) / ghostSpeed;
                if (e <= leg) return s + e * ghostSpeed;
                e -= leg;
                s = r.stopS[k];
                if (k < r.stopS.Length - 1) { if (e <= stopDwell) return s; e -= stopDwell; }
            }
            return r.Length;
        }

        /// <summary>The seconds after leaving at which it is `s` metres along (the inverse of ProgressAt, on the move).</summary>
        float TimeAt(Route r, float s)
        {
            float t = firstDwell, at = 0f;
            for (int k = 1; k < r.stopS.Length; k++)
            {
                if (s <= r.stopS[k]) return t + (s - at) / ghostSpeed;
                t += (r.stopS[k] - at) / ghostSpeed + (k < r.stopS.Length - 1 ? stopDwell : 0f);
                at = r.stopS[k];
            }
            return t;
        }

        int IndexAt(Route r, float s)
        {
            int lo = 0, hi = r.s.Length - 1;
            while (lo < hi) { int m = (lo + hi + 1) / 2; if (r.s[m] <= s) lo = m; else hi = m - 1; }
            return lo;
        }

        // ------------------------------------------------------------------ the timetable and the ghosts
        void Update()
        {
            if (!ready) return;
            float now = Time.time;
            // departures
            foreach (var l in BusLines.All)
            {
                if (l.every <= 0f || !nextLocal.TryGetValue(l.id, out var t) || now < t) continue;
                var r = Get(l.id);
                if (r != null) AddTrip(r, now);
                nextLocal[l.id] = t + l.every;
            }
            if (now >= nextOut) { var r = Get(BusLines.OutRota[outRota++ % BusLines.OutRota.Length]); if (r != null) AddTrip(r, now); nextOut += BusLines.IntercityEvery; }
            if (now >= nextIn) { var r = Get(BusLines.InRota[inRota++ % BusLines.InRota.Length]); if (r != null) AddTrip(r, now); nextIn += BusLines.IntercityEvery; }

            timer -= Time.deltaTime;
            bool scan = timer <= 0f;
            if (scan) timer = 0.5f;
            var p = PlayerController.Instance;
            var cam = Camera.main;
            Vector3 c = p ? p.transform.position : Vector3.zero;
            int real = 0;
            foreach (var tr in trips) if (tr.bus) real++;
            for (int k = trips.Count - 1; k >= 0; k--)
            {
                var tr = trips[k];
                if (tr.bus) { RealStep(tr, c, cam, scan); if (tr.bus) continue; }
                if (tr.endedAt >= 0f) { trips.RemoveAt(k); continue; }
                float e = now - tr.t0;
                tr.progress = ProgressAt(tr.route, e);
                if (tr.progress >= tr.route.Length - 0.5f) { trips.RemoveAt(k); continue; }   // there: the run is over
                if (!scan || !p || !cam || real >= realMax) continue;
                Vector3 g = tr.route.pts[IndexAt(tr.route, tr.progress)];
                float d = Vector3.Distance(g, c);
                if (d > spawnNear) continue;
                Vector3 vp = cam.WorldToViewportPoint(g);
                bool inView = vp.z > 0 && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f;
                if (inView && d < 160f) continue;   // never pop up in front of him
                if (MakeReal(tr)) real++;
            }
            Real = real;
        }

        bool MakeReal(Trip tr)
        {
            var r = tr.route;
            int pi = IndexAt(r, tr.progress);
            int j = r.step[pi];
            var (road, dir, _) = r.keys[j];
            int idx = Mathf.Clamp(r.idx[pi], 1, tm.network.roads[road].pts.Length - 2);
            Vector3 pos = tm.network.roads[road].pts[idx];
            foreach (var hit in Physics.OverlapSphere(pos + Vector3.up, 4f, 1 << VehicleFactory.VehicleLayer, QueryTriggerInteraction.Ignore))
                return false;   // something stands there now: next time
            var go = VehicleFactory.Create(CarType.Bus, Px.CarWhite, tm.material, true, "Bus " + r.line.id, busModel);
            if (!go) return false;
            go.transform.SetParent(transform, true);
            var t = go.AddComponent<TrafficCar>();
            t.type = CarType.Bus;
            t.cruise = 9f;
            t.Begin(tm.network, road, idx, dir);
            var steps = new List<(int r, int d)>();
            for (int s = j; s < r.keys.Count; s++) steps.Add((r.keys[s].r, r.keys[s].d));
            t.FollowRoute(steps, r.goal);
            go.AddComponent<BusPassenger>();
            BusDisplay.Fit(go, BusLines.DisplayText(r.line));
            tr.bus = t;
            tr.nextStop = 1;
            while (tr.nextStop < r.stopS.Length && r.stopS[tr.nextStop] < tr.progress - 5f) tr.nextStop++;
            if (tr.progress < 1f) { t.StopForBoarding(Mathf.Max(5f, firstDwell - (Time.time - tr.t0))); tr.standing = true; }
            return true;
        }

        void RealStep(Trip tr, Vector3 c, Camera cam, bool scan)
        {
            var r = tr.route;
            var bus = tr.bus;
            Vector3 bp = bus.transform.position;
            // how far along: the nearest point of the line a little ahead of where it was
            int from = IndexAt(r, tr.progress), bi = from;
            float bd = float.MaxValue;
            for (int i = Mathf.Max(0, from - 2); i < Mathf.Min(r.pts.Length, from + 40); i++)
            {
                float d = (r.pts[i] - bp).sqrMagnitude;
                if (d < bd) { bd = d; bi = i; }
            }
            if (r.s[bi] > tr.progress) tr.progress = r.s[bi];
            bool aboard = BusPassenger.Carries(bus.gameObject);
            // the stops: stand a while at each, call them out to him when he rides
            if (tr.nextStop < r.stopS.Length)
            {
                float to = r.stopS[tr.nextStop] - tr.progress;
                // "X aa gaya": about 4 s before it pulls up there (owner, 24 Nov 2026: "stops aane se 4 second pehle")
                float lead = Mathf.Max(8f, bus.Speed * 4f + 8f);
                if (aboard && to < lead && tr.callIdx != tr.nextStop * 2) { tr.callIdx = tr.nextStop * 2; Call(r.line.stops[tr.nextStop].name, false); }
                bool last = tr.nextStop == r.stopS.Length - 1;
                if (to < 8f)
                {
                    if (!last) bus.StopForBoarding(stopDwell);
                    tr.nextStop++;
                    tr.standing = true;
                    tr.stoodAt = Time.time;
                }
            }
            // "Agla stop: Y": as soon as it moves off a stop ("jaise hi kisi stop se chle to agla station")
            if (tr.standing && Time.time - tr.stoodAt > 2f && bus.Speed > 1.5f)
            {
                tr.standing = false;
                if (aboard && tr.nextStop < r.stopS.Length && tr.callIdx != tr.nextStop * 2 + 1)
                {
                    tr.callIdx = tr.nextStop * 2 + 1;
                    Call(r.line.stops[tr.nextStop].name, true);
                }
            }
            if (tr.endedAt < 0f && (bus.Arrived || tr.progress >= r.Length - 3f)) tr.endedAt = Time.time;
            if (!scan) return;
            // back to a ghost: far and out of sight (never with him aboard); at the end of its run it goes after a while
            float dist = Vector3.Distance(bp, c);
            Vector3 vp = cam ? cam.WorldToViewportPoint(bp) : Vector3.back;
            bool inView = vp.z > 0 && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f;
            bool edge = r.line.stops[r.line.stops.Length - 1].edge;
            bool done = tr.endedAt >= 0f && (edge || Time.time - tr.endedAt > 45f);
            if (aboard)
            {
                if (done && edge) BusPassenger.Evict(bus.gameObject);   // the end of the map: off he gets
                else return;
            }
            if ((dist > despawnFar && !inView) || (done && (!inView || dist > 120f)))
            {
                if (!done) tr.t0 = Time.time - TimeAt(r, tr.progress);   // the ghost goes on from here
                Destroy(bus.gameObject);
                tr.bus = null;
            }
        }

        /// <summary>The conductor's call: next = "Agla stop: X" (leaving a stop), else "X aa gaya" (at it).</summary>
        static void Call(string stop, bool next)
        {
            string slug = stop.ToLowerInvariant().Replace(" / ", "_").Replace(' ', '_');
            // every call signs off "Seva mein: Uttarakhand Parivahan Nigam, Nainital" (owner, 24 Nov 2026)
            if (next) GameEvents.ShowBanner("Agla stop: " + stop, "Seva mein - Uttarakhand Parivahan Nigam, Nainital", new Color(1f, 0.6f, 0.1f));
            else GameEvents.ShowBanner(stop + " aa gaya", "Utarna ho to E - Seva mein Uttarakhand Parivahan Nigam, Nainital", new Color(1f, 0.6f, 0.1f));
            var au = AudioManager.Instance;
            if (!au) return;
            var cam = Camera.main;
            Vector3 at = cam ? cam.transform.position : Vector3.zero;
            var voice = Resources.Load<AudioClip>("Audio/announce/" + slug + (next ? "_next" : "_here"));
            var chime = au.Clip("announce_chime") ?? au.Clip("click");
            if (chime) au.PlayAt(chime, at, 0.6f, 1f, 30f);
            if (voice && Instance) Instance.StartCoroutine(Later(voice, chime ? 1.05f : 0f));   // the voice after the ding-dong
        }

        static IEnumerator Later(AudioClip clip, float wait)
        {
            yield return new WaitForSeconds(wait);
            var au = AudioManager.Instance;
            var cam = Camera.main;
            if (au && cam) au.PlayAt(clip, cam.transform.position, 0.95f, 1f, 30f);
        }

        /// <summary>A line per trip (BusServiceTest): line, ghost / real, metres along, the next stop.</summary>
        public string Report()
        {
            var sb = new System.Text.StringBuilder($"bus service: {routes.Count} routes, {trips.Count} trips, {Real} real (max {realMax})");
            foreach (var r in routes.Values) sb.Append($"\n  route {r.line.id}: {r.Length / 1000f:F1} km, {r.keys.Count} roads, ~{Duration(r) / 60f:F0} min");
            foreach (var tr in trips)
                sb.Append($"\n  trip {tr.route.line.id} {(tr.bus ? "REAL" : "ghost")} {tr.progress:F0}/{tr.route.Length:F0} m next stop {(tr.nextStop < tr.route.line.stops.Length ? tr.route.line.stops[tr.nextStop].name : "-")}");
            return sb.ToString();
        }
    }
}
