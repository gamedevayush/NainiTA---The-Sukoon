using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// The owner's rule as a test (22 Nov 2026: "NPC ki gaadi thukne ke baad bhi accelerator diye jaari hain... thuke ruke
    /// or back krke nikaal le"): moving traffic taxis on straight, clear stretches get a thing put 35 cm before the bumper
    /// (so they must hit it): a post, a corner, a wall over the lane, the whole road shut, a parked car - twice each.
    /// Measured: how long the accelerator stayed down while touching, the stop, how far it backed, whether it got out
    /// (past it, or turned round and away). Bridge (Play): call TrafficCrashTest.Run -> Bridge/traffic_crash.txt.
    /// </summary>
    public static class TrafficCrashTest
    {
        enum Kind { Post, Corner, Wall, Shut, Car }

        sealed class Case
        {
            public Kind kind;
            public int road, start, dir;
            public TrafficCar car;
            public CarController body;
            public Vector3 half, boxC, at, way, hitAt;
            public GameObject thing;
            public Collider[] thingCols;
            public float t0, placedT, touchT = -1f, offT = -1f, stopT = -1f, outT = -1f, pressed, pressRun, pressLongest, backed;
            public int dir0;
            public bool placed, done, shot;
        }

        static readonly List<Case> cases = new List<Case>();
        static int lastFrame;
        static string LogPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../Bridge/traffic_crash.txt"));

        static void Out(string s)
        {
            Debug.Log("[Nainital] crash " + s);
            File.AppendAllText(LogPath, s + "\n");
        }

        public static void Run()
        {
            if (!EditorApplication.isPlaying) throw new System.Exception("enter Play first (PlayTest.Begin)");
            var tm = Object.FindAnyObjectByType<TrafficManager>();
            var net = tm.network;
            File.WriteAllText(LogPath, "");
            Stop();
            var spots = Spots(net, 10);
            if (spots.Count < 5) { Out($"crash test done: only {spots.Count} clear straight stretches found"); return; }
            var lib = VehicleLibrary.Instance;
            for (int k = 0; k < spots.Count; k++)
            {
                var (r, i, d) = spots[k];
                var go = VehicleFactory.Create(CarType.Taxi, Px.CarWhite, tm.material, true, "CrashTest Taxi " + k, lib ? lib.Pick(CarType.Taxi, 0.3f) : -1);
                go.transform.SetParent(tm.transform, true);
                var car = go.AddComponent<TrafficCar>();
                car.type = CarType.Taxi;
                car.cruise = 9f;
                car.Begin(net, r, i, d);
                var box = go.GetComponent<BoxCollider>();
                cases.Add(new Case
                {
                    kind = (Kind)(k % 5), road = r, start = i, dir = d, dir0 = d, car = car, body = go.GetComponent<CarController>(),
                    half = box.size * 0.5f, boxC = box.center, t0 = Time.time,
                });
            }
            // the camera on the first, for the picture of it backing out
            if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.SetOverride(cases[0].car.transform, 8f);
            lastFrame = -1;
            Out($"crash test: {cases.Count} cars");
            EditorApplication.update += Tick;
        }

        public static void Stop()
        {
            EditorApplication.update -= Tick;
            foreach (var c in cases)
            {
                if (c.car) Object.Destroy(c.car.gameObject);
                if (c.thing) Object.Destroy(c.thing);
            }
            cases.Clear();
        }

        /// <summary>Straight, fairly level, clear two-way stretches (40 m), well apart and away from the player's traffic.</summary>
        static List<(int r, int i, int d)> Spots(RoadNetwork net, int wanted)
        {
            var found = new List<(int, int, int)>();
            var taken = new List<Vector3>();
            var player = PlayerController.Instance.transform.position;
            int mask = ~((1 << 2) | (1 << 4) | (1 << 8) | (1 << 9));
            var terrain = Terrain.activeTerrain;
            var onRoad = new int[net.roads.Length];   // (all ten stood on road 0 once: two to a road, so ten cars try five roads at least)
            for (int pass = 0; pass < 2 && found.Count < wanted; pass++)
                for (int r = 0; r < net.roads.Length && found.Count < wanted; r++)
                {
                    var road = net.roads[r];
                    var p = road.pts;
                    if (road.oneWay != 0 || p.Length < 20 || road.width < (pass == 0 ? 6f : 4.5f)) continue;
                    for (int i = 4; i + 12 < p.Length && found.Count < wanted; i += 3)
                    {
                        if (onRoad[r] >= 2) break;
                        if ((p[i] - player).sqrMagnitude < 250f * 250f) continue;
                        bool apart = true;
                        foreach (var t in taken) if ((t - p[i]).sqrMagnitude < 90f * 90f) { apart = false; break; }
                        if (!apart) continue;
                        Vector3 all = p[i + 10] - p[i], flat = new Vector3(all.x, 0f, all.z);
                        if (flat.magnitude < 25f || Mathf.Abs(all.y) > flat.magnitude * 0.07f) continue;
                        bool good = true;
                        Vector3 left = Vector3.Cross(flat.normalized, Vector3.up);
                        float off = Mathf.Max(1.1f, road.width * 0.25f);
                        for (int k = 0; k < 10 && good; k++)
                        {
                            Vector3 step = p[i + k + 1] - p[i + k];
                            step.y = 0f;
                            if (Vector3.Angle(step, flat) > 8f) { good = false; break; }
                            foreach (float side in new[] { off, -off })
                            {
                                Vector3 q = p[i + k] + left * side;
                                q.y = terrain ? terrain.SampleHeight(q) + terrain.transform.position.y : q.y;
                                foreach (var c in Physics.OverlapBox(q + Vector3.up * 1.2f, new Vector3(1f, 0.6f, 2.2f), Quaternion.LookRotation(flat.normalized), mask, QueryTriggerInteraction.Ignore))
                                    if (!(c is TerrainCollider)) { good = false; break; }
                            }
                        }
                        if (!good) continue;
                        found.Add((r, i, 1));
                        onRoad[r]++;
                        taken.Add(p[i]);
                    }
                }
            return found;
        }

        static void Place(Case c)
        {
            var t = c.car.transform;
            Vector3 f = new Vector3(t.forward.x, 0f, t.forward.z).normalized, right = Vector3.Cross(Vector3.up, f);
            Vector3 bumper = t.position + f * (c.half.z + 0.35f);
            if (c.kind == Kind.Car)
            {
                var tm = Object.FindAnyObjectByType<TrafficManager>();
                var lib = VehicleLibrary.Instance;
                c.thing = VehicleFactory.Create(CarType.Hatchback, Px.CarWhite, tm.material, true, "CrashTest parked", lib ? lib.Pick(CarType.Hatchback, 0.4f) : -1);
                var box = c.thing.GetComponent<BoxCollider>();
                c.thing.transform.SetPositionAndRotation(bumper + f * (box.size.z * 0.5f), Quaternion.LookRotation(f));
            }
            else
            {
                c.thing = GameObject.CreatePrimitive(PrimitiveType.Cube);
                c.thing.name = "CrashTest " + c.kind;
                float width = c.kind == Kind.Wall ? 2.4f : c.kind == Kind.Shut ? 24f : 0.35f;
                float aside = c.kind == Kind.Corner ? c.half.x - 0.2f : 0f;
                c.thing.transform.SetPositionAndRotation(bumper + f * 0.2f + right * aside + Vector3.up * 1f, Quaternion.LookRotation(f));
                c.thing.transform.localScale = new Vector3(width, 3f, 0.4f);
            }
            Physics.SyncTransforms();
            c.thingCols = c.thing.GetComponentsInChildren<Collider>();
            c.at = c.thing.transform.position;
            c.way = f;
            c.placed = true;
            c.placedT = Time.time;
        }

        static bool NoseOn(Case c)
        {
            var t = c.car.transform;
            Vector3 at = t.TransformPoint(c.boxC + new Vector3(0f, 0f, c.half.z));
            var hits = Physics.OverlapBox(at, new Vector3(c.half.x, c.half.y * 0.8f, 0.09f), t.rotation, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits) foreach (var own in c.thingCols) if (h == own) return true;
            return false;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            float now = Time.time, dt = Time.deltaTime;
            bool all = true;
            foreach (var c in cases)
            {
                if (c.done) continue;
                if (!c.car || now - c.t0 > 40f) { c.done = true; continue; }
                all = false;
                var t = c.car.transform;
                float fwd = c.body.ForwardSpeed;
                if (!c.placed)
                {
                    if ((now - c.t0 > 0.8f && fwd > 3.5f) || now - c.t0 > 6f) Place(c);
                    continue;
                }
                bool nose = NoseOn(c);
                if (c.touchT < 0f && (nose || c.car.HitsHere > 0)) { c.touchT = now; c.hitAt = t.position; }
                if (c.touchT < 0f) continue;
                float gas = c.car.Throttle;
                if (c.offT < 0f && gas <= 0.05f) c.offT = now;
                // (rolling back, a positive pedal is the brake)
                if (nose && gas > 0.05f && fwd > -1f) { c.pressed += dt; c.pressRun += dt; c.pressLongest = Mathf.Max(c.pressLongest, c.pressRun); }
                else c.pressRun = 0f;
                if (c.stopT < 0f && Mathf.Abs(fwd) < 0.5f) { c.stopT = now; c.hitAt = t.position; }
                if (c.stopT < 0f) continue;
                if (fwd < -0.2f) c.backed = Mathf.Max(c.backed, Vector3.Dot(c.hitAt - t.position, c.way));
                if (!c.shot && c == cases[0] && c.backed > 0.8f)
                {
                    c.shot = true;
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Application.dataPath, "../Shots/play_crash_backing.png")));
                }
                float past = Vector3.Dot(t.position - c.at, c.way);
                bool round = past > c.half.z + 1.5f;
                bool away = c.car.Dir != c.dir0 && past < -10f && Vector3.Dot(t.forward, c.way) < -0.5f && fwd > 1f;
                if (round || away) { c.outT = now; c.done = true; }
            }
            if (!all) return;
            EditorApplication.update -= Tick;
            var sb = new StringBuilder();
            int good = 0;
            foreach (var c in cases)
            {
                bool hit = c.touchT >= 0f, got = c.outT >= 0f;
                bool ok = hit && got && c.pressLongest <= 0.15f && c.backed >= 0.8f;
                if (ok) good++;
                sb.Append($"  {(ok ? "ok  " : "FAIL")} {c.kind,-6} road {c.road}: ");
                if (!hit) sb.Append("never touched it");
                else
                    sb.Append($"accelerator off {(c.offT >= 0f ? (c.offT - c.touchT).ToString("F2") + " s" : "NEVER")} after the touch, pressed on it {c.pressed:F2} s (longest {c.pressLongest:F2}), " +
                              $"stopped {(c.stopT >= 0f ? (c.stopT - c.touchT).ToString("F2") + " s" : "never")}, backed {c.backed:F1} m, " +
                              $"{(got ? (c.car && c.car.Dir != c.dir0 ? "turned round and away" : "round it") + $" in {c.outT - c.touchT:F1} s" : "DID NOT GET OUT")}, hits {(c.car ? c.car.HitsHere : -1)}");
                if (!ok && c.car) sb.Append($" | ({c.car.transform.position.x:F1},{c.car.transform.position.z:F1}) {c.car.State}");
                sb.Append('\n');
            }
            sb.Append($"crash test done: {good} of {cases.Count} hit, stopped, backed and got out");
            Out(sb.ToString());
            if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.SetOverride(null, 0f);
        }
    }
}
