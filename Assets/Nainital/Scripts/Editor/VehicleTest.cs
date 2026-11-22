using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Play-mode checks of the vehicles (user, 2026-11-18: mass, crashes "like a light balloon", empty cars not reacting,
    /// seats, tyres): through the bridge, "call VehicleTest.Crash" then "call VehicleTest.Report" after ~4 s.
    /// Crash: a parked (empty, asleep) Bolero 14 m in front of the player, a WagonR 30 m behind it coming at 15 m/s into
    /// its side; both are tracked (peak speed, rise, spin, how far the parked one moved). Seat: one library vehicle beside
    /// the player with a townsperson at the wheel (Bridge/arg.txt = model name or index).
    /// </summary>
    public static class VehicleTest
    {
        static void Need() { if (!EditorApplication.isPlaying) throw new System.Exception("not playing"); }
        static string Arg() { try { return File.ReadAllText(Path.Combine(Application.dataPath, "../Bridge/arg.txt")).Trim(); } catch { return ""; } }

        static GameObject a, b;
        static Vector3 aStart, bStart;
        static float t0, aPeakV, bPeakV, aRise, bRise, aSpin, bSpin;
        static readonly StringBuilder log = new StringBuilder();

        static int Model(string name)
        {
            var lib = VehicleLibrary.Instance;
            for (int i = 0; i < lib.entries.Length; i++) if (lib.entries[i].name == name) return i;
            return -1;
        }

        static Vector3 Ground(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out var g, 80f, ~((1 << 8) | (1 << 9) | (1 << 10) | (1 << 2)), QueryTriggerInteraction.Ignore)) return g.point;
            return p;
        }

        /// <summary>Re-imports every FBX of Vehicles/New (after a NewVehicleImport setting changed: Read/Write for the dents,
        /// 21 Nov 2026), then logs how many came in readable. Not in Play. Bridge: call VehicleTest.ReimportNew</summary>
        public static void ReimportNew()
        {
            if (EditorApplication.isPlaying) throw new System.Exception("leave Play first");
            int n = 0, readable = 0;
            foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Nainital/Vehicles/New" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                n++;
                var m = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault(x => x.name.StartsWith("Body"));
                if (m && m.isReadable) readable++;
            }
            Debug.Log($"[Nainital] reimported {n} vehicle FBX, {readable} with a readable Body mesh");
        }

        /// <summary>The traffic's indicators and dipper now (owner, 21 Nov 2026): how many cars / two-wheelers signal left /
        /// right, how many of those have indicator lamps and have one lit this frame, the dipper flicks so far, and the
        /// nearest signalling car to the player. Bridge: call VehicleTest.SignalReport</summary>
        public static void SignalReport()
        {
            Need();
            int n = 0, l = 0, r = 0, withLamps = 0, lit = 0;
            VehicleLamps nearest = null; float nd = float.MaxValue;
            var pp = PlayerController.Instance ? PlayerController.Instance.transform.position : Vector3.zero;
            foreach (var vl in Object.FindObjectsByType<VehicleLamps>(FindObjectsSortMode.None))
            {
                if (!vl.GetComponent<TrafficCar>() && !vl.GetComponent<TrafficBike>()) continue;
                n++;
                if (vl.signal == 0) continue;
                if (vl.signal < 0) l++; else r++;
                var set = vl.signal < 0 ? vl.indL : vl.indR;
                if (set.Length > 0) withLamps++;
                if (System.Array.Exists(set, x => x && x.enabled)) lit++;
                float d = (vl.transform.position - pp).sqrMagnitude;
                if (d < nd) { nd = d; nearest = vl; }
            }
            Debug.Log($"[Nainital] signals: {n} traffic vehicles, {l} left / {r} right, {withLamps} of them with indicator lamps, {lit} lit this frame (blinking ~55 %), dipper flicks so far {TrafficCar.Dippers}" +
                      (nearest ? $"; nearest signalling: {nearest.name} {(nearest.signal < 0 ? "LEFT" : "RIGHT")} at {Mathf.Sqrt(nd):F0} m" : ""));
        }

        /// <summary>Stands the player 9 m behind the nearest signalling traffic vehicle, looking at it (then "capture").
        /// Bridge: call VehicleTest.FaceSignal</summary>
        public static void FaceSignal()
        {
            Need();
            var p = PlayerController.Instance;
            VehicleLamps best = null; float bd = float.MaxValue;
            foreach (var vl in Object.FindObjectsByType<VehicleLamps>(FindObjectsSortMode.None))
            {
                if (vl.signal == 0 || (!vl.GetComponent<TrafficCar>() && !vl.GetComponent<TrafficBike>())) continue;
                float d = (vl.transform.position - p.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = vl; }
            }
            if (!best) throw new System.Exception("nobody is signalling now");
            Vector3 f = best.transform.forward; f.y = 0f; f.Normalize();
            Vector3 at = Ground(best.transform.position - f * 9f + Vector3.Cross(Vector3.up, f) * 1.5f) + Vector3.up * 0.2f;
            p.GetComponent<CharacterMotor>().Teleport(at);
            Vector3 look = best.transform.position - at; look.y = 0f;
            p.transform.rotation = Quaternion.LookRotation(look.normalized);
            if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.SnapBehind();
            Debug.Log($"[Nainital] facing {best.name} signalling {(best.signal < 0 ? "LEFT" : "RIGHT")} from 9 m behind");
        }

        /// <summary>Every vehicle of the library as the game makes it (gameplay on), its collider against its body: how much
        /// of the body's length / width / height the colliders cover, the body kind (dynamic / kinematic). Flags a
        /// vehicle with no collider or one covering under 85 % of the body (owner, 21 Nov 2026: "saari gaadi ke proper
        /// collider check krna"). Bridge (Play): call VehicleTest.ColliderReport</summary>
        public static void ColliderReport()
        {
            Need();
            var lib = VehicleLibrary.Instance;
            var mat = Object.FindAnyObjectByType<TrafficManager>() ? Object.FindAnyObjectByType<TrafficManager>().material : null;
            var sb = new StringBuilder("[Nainital] colliders (cover of the body: length / width / height %; rigidbody):");
            int bad = 0;
            void One(GameObject go, string nm)
            {
                go.transform.position = new Vector3(0f, -500f, 0f);
                Physics.SyncTransforms();
                Bounds body = default, col = default; bool fb = true, fc = true;
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    // the body's meshes only: an exhaust's particles simulate in world space (their box stays at the origin)
                    if (r.gameObject.layer == 9 || !(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                    if (fb) { body = r.bounds; fb = false; } else body.Encapsulate(r.bounds);
                }
                int nc = 0;
                foreach (var c in go.GetComponentsInChildren<Collider>()) { if (c.isTrigger || c.gameObject.layer == 9) continue; nc++; if (fc) { col = c.bounds; fc = false; } else col.Encapsulate(c.bounds); }
                var rb = go.GetComponent<Rigidbody>();
                float L = nc > 0 ? col.size.z / body.size.z * 100f : 0f, W = nc > 0 ? col.size.x / body.size.x * 100f : 0f, H = nc > 0 ? col.size.y / body.size.y * 100f : 0f;
                bool flag = nc == 0 || L < 85f || H < 60f || W < 60f;
                if (flag) bad++;
                sb.AppendLine().Append($"  {nm,-20} colliders {nc}, cover L {L:F0} W {W:F0} H {H:F0}, {(rb ? (rb.isKinematic ? "kinematic" : "dynamic") : "no rigidbody")}{(flag ? "   <- CHECK" : "")}");
                Object.DestroyImmediate(go);
            }
            for (int i = 0; i < lib.entries.Length; i++)
            {
                var e = lib.entries[i];
                if (e == null || !e.prefab) continue;
                One(VehicleFactory.Create(e.type, Px.CarWhite, mat, true, "ColTest " + e.name, i), e.name);
            }
            foreach (var e in lib.bikes)
            {
                if (e == null || !e.prefab) continue;
                One(BikeRide.Create(e, "ColTest " + e.name), e.name + " (two-wheeler, moves itself: PushPeople)");
            }
            sb.AppendLine().Append($"  flagged {bad}");
            Debug.Log(sb.ToString());
        }

        /// <summary>The player put inside a moving traffic two-wheeler's box (10 cm off its middle line): 0.4 s later, is he
        /// out of it (BikeRide.PushPeople)? Logs his side distance from the bike's middle line against the box's half width
        /// + his radius. Bridge (Play): call VehicleTest.PushTest</summary>
        public static void PushTest()
        {
            Need();
            var p = PlayerController.Instance;
            TrafficBike tb = null; float bd = float.MaxValue;
            foreach (var t in Object.FindObjectsByType<TrafficBike>(FindObjectsSortMode.None))
            {
                var br = t.GetComponent<BikeRide>();
                if (t.Down || !br || br.Speed < 2f) continue;
                float d = (t.transform.position - p.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; tb = t; }
            }
            if (!tb) throw new System.Exception("no moving traffic bike");
            var box = tb.GetComponent<BoxCollider>();
            p.GetComponent<CharacterMotor>().Teleport(tb.transform.position + tb.transform.right * 0.1f + Vector3.up * 0.1f);
            Physics.SyncTransforms();
            double end = EditorApplication.timeSinceStartup + 0.4;
            void Tick()
            {
                if (!EditorApplication.isPlaying || !tb) { EditorApplication.update -= Tick; return; }
                if (EditorApplication.timeSinceStartup < end) return;
                EditorApplication.update -= Tick;
                var cc = p.GetComponent<CharacterController>();
                Vector3 local = tb.transform.InverseTransformPoint(p.transform.position);
                float need = box.size.x * 0.5f + (cc ? cc.radius : 0.3f);
                Debug.Log($"[Nainital] push test: {tb.name} at {tb.GetComponent<BikeRide>().Speed:F1} m/s - player {Mathf.Abs(local.x):F2} m off its middle line (clear at {need:F2}), along it {local.z:F2}: {(Mathf.Abs(local.x) >= need - 0.05f || Mathf.Abs(local.z) > box.size.z * 0.5f + 0.3f ? "OUT" : "STILL INSIDE")}");
            }
            EditorApplication.update += Tick;
        }

        /// <summary>Why the traffic stands: for every traffic car / two-wheeler under 0.5 m/s, what is in front of it within
        /// 9 m (a person - the townsfolk or the player -, a vehicle, something else, nothing). The jam the owner saw (21 Nov
        /// 2026: "NPC ke pass gaadi aaye to vo side hojaaye vrna jam lgjayega") shows as standing for a person. Bridge (Play):
        /// call VehicleTest.JamReport</summary>
        public static void JamReport()
        {
            Need();
            int n = 0, standing = 0, person = 0, vehicle = 0, other = 0, clear = 0, wrecked = 0;
            int mask = ~((1 << 2) | (1 << 4));
            var others = new System.Collections.Generic.List<string>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                var tc = t.GetComponent<TrafficCar>(); var tb = t.GetComponent<TrafficBike>();
                if (!tc && !tb) continue;
                n++;
                if (tc && tc.Wrecked) { wrecked++; continue; }   // burnt out: not traffic any more
                float sp = tc ? tc.Speed : t.GetComponent<BikeRide>().Speed;
                if (sp > 0.5f) continue;
                standing++;
                var info = t.GetComponent<VehicleInfo>();
                float half = info ? info.length * 0.5f : 1f;
                Vector3 f = new Vector3(t.forward.x, 0f, t.forward.z).normalized;
                var hits = Physics.SphereCastAll(t.position + Vector3.up * 0.9f + f * (half + 0.2f), 0.7f, f, 9f, mask, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                string what = "clear";
                foreach (var h in hits)
                {
                    if (h.collider.transform.IsChildOf(t)) continue;
                    int l = h.collider.gameObject.layer;
                    what = l == 8 || l == 9 ? "person" : l == VehicleFactory.VehicleLayer ? "vehicle" : "other";
                    if (what == "other") others.Add($"{h.collider.name}{(h.collider.transform.parent ? "<" + h.collider.transform.parent.name : "")} {h.distance:F1}m @{t.position:F0} [{(tc ? tc.State : "bike")}]");
                    break;
                }
                if (what == "person") person++; else if (what == "vehicle") vehicle++; else if (what == "other") other++; else clear++;
            }
            Debug.Log($"[Nainital] jam report: {n} traffic, {wrecked} wrecked, {standing} standing - in front: person {person}, vehicle {vehicle}, other {other}, nothing {clear}" +
                      $" | so far: back-offs {TrafficCar.BackOffs}, hits {TrafficCar.Hits}, hops {TrafficCar.Hops}, passes {TrafficCar.Passes}" + (others.Count > 0 ? " | other: " + string.Join("; ", others) : ""));
        }

        // ---------------------------------------------------------------- vehicle sounds (22 Nov 2026)
        /// <summary>Every vehicle's sound: its kind, engine clip (loaded?), playing near the camera, speed / pitch / volume,
        /// its own horn; vehicles without a VehicleSound; missing clips.</summary>
        public static void SoundReport()
        {
            Need();
            var cam = Camera.main;
            int all = 0, without = 0, playing = 0, noClip = 0;
            var kinds = new System.Collections.Generic.Dictionary<string, int>();
            var lines = new System.Collections.Generic.List<string>();
            var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!t.GetComponent<CarController>() && !t.GetComponent<BikeRide>()) continue;
                all++;
                var vs = t.GetComponent<VehicleSound>();
                if (!vs) { without++; continue; }
                string k = vs.kind.ToString();
                kinds[k] = kinds.TryGetValue(k, out var c) ? c + 1 : 1;
                var eng = typeof(VehicleSound).GetField("eng", bf).GetValue(vs) as AudioSource;
                var horn = typeof(VehicleSound).GetField("hornClip", bf).GetValue(vs) as AudioClip;
                float hp = (float)typeof(VehicleSound).GetField("hornPitch", bf).GetValue(vs);
                int gear = (int)typeof(VehicleSound).GetField("gear", bf).GetValue(vs);
                if (!eng || !eng.clip || !horn) noClip++;
                float d = cam ? Vector3.Distance(cam.transform.position, t.position) : -1f;
                if (eng && eng.isPlaying)
                {
                    playing++;
                    var cc = t.GetComponent<CarController>(); var br = t.GetComponent<BikeRide>();
                    float sp = cc ? Mathf.Abs(cc.ForwardSpeed) : br ? br.Speed : 0f;
                    lines.Add($"{t.name} [{k}] {eng.clip.name} {d:F0} m: {sp:F1} m/s gear {gear + 1} pitch {eng.pitch:F2} vol {eng.volume:F2} | horn {horn?.name} x{hp:F2}" + (cc ? $" slip {cc.Slip:F2}" : ""));
                }
            }
            var ks = new System.Collections.Generic.List<string>();
            foreach (var kv in kinds) ks.Add($"{kv.Key} {kv.Value}");
            Debug.Log($"[Nainital] sound report: {all} vehicles, {without} without VehicleSound, {noClip} missing a clip, {playing} engines playing | kinds: {string.Join(", ", ks)}" +
                      (lines.Count > 0 ? " | " + string.Join("; ", lines) : ""));
        }

        /// <summary>The player's car (take one first: PlayTest.TakeTraffic): 5 s flat out, 1.5 s flat out on full lock,
        /// 2 s braking; every 0.25 s the speed, gear, engine pitch / volume, tyre slip and the skid's volume.</summary>
        public static void DriveSound()
        {
            Need();
            var c = CarController.Driving;
            if (!c) throw new System.Exception("not driving (PlayTest.TakeTraffic first)");
            var vs = c.GetComponent<VehicleSound>();
            var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            // somewhere it can run: 50 m up the lane of a traffic car going well, facing the lane's way
            TrafficCar lead = null;
            foreach (var tc in Object.FindObjectsByType<TrafficCar>(FindObjectsSortMode.None))
                if (tc.gameObject != c.gameObject && tc.Speed > 7f && tc.State.StartsWith("Drive")) { lead = tc; break; }
            if (lead)
            {
                Vector3 at = lead.LaneAhead(50f, out var way);
                var crb = c.GetComponent<Rigidbody>();
                crb.linearVelocity = Vector3.zero; crb.angularVelocity = Vector3.zero;
                c.transform.SetPositionAndRotation(Ground(at) + Vector3.up * 0.5f, Quaternion.LookRotation(way, Vector3.up));
                Physics.SyncTransforms();
            }
            double t0 = EditorApplication.timeSinceStartup + 1.0, next = t0;   // a second to settle
            var sb = new System.Text.StringBuilder($"[Nainital] drive sound {c.name} [{(vs ? vs.kind.ToString() : "-")}]:");
            void Tick()
            {
                if (!EditorApplication.isPlaying || !c) { EditorApplication.update -= Tick; GameInput.TouchMove = Vector2.zero; return; }
                double t = EditorApplication.timeSinceStartup - t0;
                if (t < 0) return;
                GameInput.TouchMove = t < 5 ? new Vector2(0f, 1f) : t < 6.5 ? new Vector2(1f, 1f) : t < 8.5 ? new Vector2(0f, -1f) : Vector2.zero;
                if (EditorApplication.timeSinceStartup < next) return;
                next += 0.25;
                var eng = vs ? typeof(VehicleSound).GetField("eng", bf).GetValue(vs) as AudioSource : null;
                var skid = vs ? typeof(VehicleSound).GetField("skid", bf).GetValue(vs) as AudioSource : null;
                int gear = vs ? (int)typeof(VehicleSound).GetField("gear", bf).GetValue(vs) : 0;
                sb.Append($" | {t:F2}s {c.ForwardSpeed:F1}m/s g{gear + 1} p{(eng ? eng.pitch : 0f):F2} v{(eng ? eng.volume : 0f):F2} {(eng && eng.isPlaying ? "" : "OFF ")}slip {c.Slip:F2} skid {(skid ? skid.volume : 0f):F2}");
                if (t > 9) { EditorApplication.update -= Tick; GameInput.TouchMove = Vector2.zero; Debug.Log(sb.ToString() + $" | knocks {VehicleSound.Knocks}, last {VehicleSound.LastKnock}"); }
            }
            EditorApplication.update += Tick;
        }

        /// <summary>The player's car thrown sideways at 12 m/s (the tyres must slide): slip and the skid's volume every
        /// 0.1 s for 1.5 s.</summary>
        public static void SkidTest()
        {
            Need();
            var c = CarController.Driving;
            if (!c) throw new System.Exception("not driving");
            var vs = c.GetComponent<VehicleSound>();
            var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var crb = c.GetComponent<Rigidbody>();
            crb.linearVelocity = c.transform.right * 12f + c.transform.forward * 4f;
            double t0 = EditorApplication.timeSinceStartup, next = t0;
            var sb = new System.Text.StringBuilder($"[Nainital] skid test {c.name}:");
            void Tick()
            {
                if (!EditorApplication.isPlaying || !c) { EditorApplication.update -= Tick; return; }
                double t = EditorApplication.timeSinceStartup - t0;
                if (EditorApplication.timeSinceStartup < next) return;
                next += 0.1;
                var skid = vs ? typeof(VehicleSound).GetField("skid", bf).GetValue(vs) as AudioSource : null;
                sb.Append($" | {t:F1}s v{crb.linearVelocity.magnitude:F1} slip {c.Slip:F2} skid {(skid ? skid.volume : 0f):F2}{(skid && skid.isPlaying ? "" : " off")}");
                if (t > 1.5) { EditorApplication.update -= Tick; Debug.Log(sb.ToString()); }
            }
            EditorApplication.update += Tick;
        }

        // ---------------------------------------------------------------- getting unstuck (21 Nov 2026)
        static TrafficCar testCar;
        static GameObject testWall;
        static Vector3 testStart, testWallAt, testWallFwd;
        static Rigidbody testParked;

        static TrafficCar NearestDriving(Vector3 to, TrafficCar not = null)
        {
            TrafficCar best = null; float bd = float.MaxValue;
            foreach (var tc in Object.FindObjectsByType<TrafficCar>(FindObjectsSortMode.None))
            {
                if (tc == not || !tc.HasDriver || tc.Wrecked || tc.PullingUp || tc.Speed < 2f || !tc.State.StartsWith("Drive")) continue;
                float d = (tc.transform.position - to).sqrMagnitude;
                if (d < bd) { bd = d; best = tc; }
            }
            return best;
        }

        /// <summary>A wall across the road just ahead of the nearest moving traffic car (arg "full": the whole road and
        /// more, "half": its own lane only). Then WedgeReport now and then, WedgeClear at the end.</summary>
        public static void WedgeTest()
        {
            Need();
            WedgeClear();
            bool full = (Arg() ?? "").Trim() != "half";
            var p = PlayerController.Instance;
            testCar = NearestDriving(p.transform.position);
            if (!testCar) throw new System.Exception("no moving traffic car");
            var t = testCar.transform;
            Vector3 f = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
            var info = testCar.GetComponent<VehicleInfo>();
            float len = info ? info.length : 4.4f;
            testStart = t.position;
            testWallFwd = f;
            testWallAt = Ground(t.position + f * (len * 0.5f + 1.6f + testCar.Speed * 0.6f));
            testWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            testWall.name = "TestWall";
            testWall.transform.SetPositionAndRotation(testWallAt + Vector3.up * 1f + (full ? Vector3.zero : Vector3.Cross(Vector3.up, f) * 0f), Quaternion.LookRotation(f, Vector3.up));
            testWall.transform.localScale = new Vector3(full ? 24f : 2.4f, 2f, 0.4f);
            Physics.SyncTransforms();
            Debug.Log($"[Nainital] wedge test ({(full ? "full road" : "its lane")}): {testCar.name} at {t.position:F0} doing {testCar.Speed:F1} m/s, wall {(testWallAt - t.position).magnitude:F1} m ahead");
        }

        /// <summary>The wedge test car: its state, how far it got, whether it is past the wall.</summary>
        public static void WedgeReport()
        {
            Need();
            if (!testCar) { Debug.Log("[Nainital] wedge report: test car gone (left / hopped out of sight / destroyed)"); return; }
            Vector3 pos = testCar.transform.position;
            float past = Vector3.Dot(pos - testWallAt, testWallFwd);
            Debug.Log($"[Nainital] wedge report: {testCar.name} {testCar.State}, speed {testCar.Speed:F1}, {(pos - testStart).magnitude:F0} m from start, " +
                      $"{(past > 0f ? $"PAST the wall by {past:F1} m" : $"{-past:F1} m short of the wall")}, this car: most tries {testCar.MostTries}, hits {testCar.HitsHere}, passes {testCar.PassesHere} | all: back-offs {TrafficCar.BackOffs}, hits {TrafficCar.Hits}, hops {TrafficCar.Hops}, passes {TrafficCar.Passes}");
        }

        public static void WedgeClear()
        {
            if (testWall) Object.Destroy(testWall);
            testWall = null;
            if (testParked) Object.Destroy(testParked.gameObject);
            testParked = null;
        }

        /// <summary>A traffic car left standing in the lane of another (its driver gone, as if abandoned), 16 m ahead of it.
        /// Then WedgeReport (the moving one is the test car) - it should go round on the right.</summary>
        public static void PassTest()
        {
            Need();
            WedgeClear();
            var p = PlayerController.Instance;
            testCar = NearestDriving(p.transform.position);
            if (!testCar) throw new System.Exception("no moving traffic car");
            var other = NearestDriving(testCar.transform.position + testCar.transform.forward * 60f, testCar);
            if (!other) throw new System.Exception("no second traffic car");
            var go = other.gameObject;
            var entry = go.GetComponent<TrafficCarEntry>();
            if (entry) Object.DestroyImmediate(entry);   // (it needs the TrafficCar)
            Object.DestroyImmediate(other);   // takes its driver and passengers: an empty car
            var cc = go.GetComponent<CarController>();
            if (cc) { cc.aiThrottle = 0f; cc.aiSteer = 0f; cc.aiHandbrake = 1f; }
            var t = testCar.transform;
            Vector3 at = testCar.LaneAhead(22f, out var f);
            testParked = go.GetComponent<Rigidbody>();
            testParked.linearVelocity = Vector3.zero;
            testParked.angularVelocity = Vector3.zero;
            go.transform.SetPositionAndRotation(Ground(at) + Vector3.up * 0.4f, Quaternion.LookRotation(f, Vector3.up));
            Physics.SyncTransforms();
            testStart = t.position;
            testWallAt = go.transform.position;
            testWallFwd = f;
            Debug.Log($"[Nainital] pass test: {go.name} left standing in the lane {(at - t.position).magnitude:F0} m ahead of {testCar.name} ({testCar.Speed:F1} m/s)");
        }

        // ---------------------------------------------------------------- the player on a bike (21 Nov 2026)
        static PlayerBike testBike;

        /// <summary>The player beside the nearest traffic two-wheeler, asking for it (E): it pulls up, its people get off,
        /// the player mounts. Bridge: call VehicleTest.RideBike, then RideReport / RideGo / RideOff.</summary>
        public static void RideBike()
        {
            Need();
            var p = PlayerController.Instance;
            TrafficBike best = null; float bd = float.MaxValue;
            foreach (var tb in Object.FindObjectsByType<TrafficBike>(FindObjectsSortMode.None))
            {
                if (tb.Down || !tb.GetComponent<PlayerBike>()) continue;
                float d = (tb.transform.position - p.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = tb; }
            }
            if (!best) throw new System.Exception("no traffic bike");
            testBike = best.GetComponent<PlayerBike>();
            p.GetComponent<CharacterMotor>().Teleport(Ground(best.transform.position - best.transform.right * 2f) + Vector3.up * 0.2f);
            Physics.SyncTransforms();
            int people = best.GetComponentsInChildren<NpcWalker>(true).Length;
            testBike.Interact(p);
            Debug.Log($"[Nainital] ride: asked {best.name} ({people} on it) at {Mathf.Sqrt(bd):F0} m");
        }

        /// <summary>Who is on the test bike, the player's mode, the bike's speed and where it is.</summary>
        public static void RideReport()
        {
            Need();
            var p = PlayerController.Instance;
            if (!testBike) throw new System.Exception("no test bike (RideBike first)");
            int npcs = testBike.GetComponentsInChildren<NpcWalker>(true).Length;
            var rig = p.Rig;
            var ik = p.GetComponent<HandIK>();
            string miss = "";
            if (ik && rig)
            {
                var a = rig.GetComponentInChildren<Animator>();
                if (a && a.isHuman)
                {
                    var hl = a.GetBoneTransform(HumanBodyBones.LeftHand); var fl = a.GetBoneTransform(HumanBodyBones.LeftFoot);
                    if (ik.left && hl) miss += $" handL {(hl.position - ik.left.position).magnitude * 100f:F0}cm";
                    if (ik.footLeft && fl) miss += $" footL {(fl.position - (ik.footLeft.position + Vector3.up * rig.SoleLift)).magnitude * 100f:F0}cm";
                }
            }
            // what the player's stop-short cast sees ahead (PlayerBike.FixedUpdate)
            var bk = testBike.Bike;
            Vector3 from = Ground(testBike.transform.position + bk.Forward * (bk.length * 0.5f)) + Vector3.up * 0.75f;
            string ahead = "clear";
            foreach (var hit in Physics.SphereCastAll(from, 0.35f, bk.Forward, 6f, ~((1 << 2) | (1 << 4) | (1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(testBike.transform) || (hit.distance <= 0f ? hit.collider is TerrainCollider : hit.normal.y > 0.8f)) continue;
                ahead = $"{hit.collider.name} (layer {hit.collider.gameObject.layer}) at {hit.distance:F1} m, slope {Vector3.Angle(hit.normal, Vector3.up):F0} deg";
                break;
            }
            var brb = testBike.GetComponent<Rigidbody>();
            Debug.Log($"[Nainital] ride report: player {p.CurrentMode}, riding {(PlayerBike.Riding ? PlayerBike.Riding.name : "-")}, npcs still on the bike {npcs}, " +
                      $"traffic {(testBike.GetComponent<TrafficBike>() ? "still" : "gone")}, speed {bk.Speed:F1} m/s at {testBike.transform.position:F0}, rig riding {(rig ? rig.riding : false)}{miss}, " +
                      $"crashed {bk.Crashed}, kinematic {(brb ? brb.isKinematic : false)}, ahead {ahead}, in {GameInput.Move}");
        }

        /// <summary>Throttle on for arg seconds (default 3), then off; logs how far it went.</summary>
        public static void RideGo()
        {
            Need();
            // arg: "seconds [steer throttle]", e.g. "3" or "2 1 0" (2 s of D alone: walked round on the spot)
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var parts = (Arg() ?? "").Split(new[] { ' ', ',' }, System.StringSplitOptions.RemoveEmptyEntries);
            float secs = parts.Length > 0 && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, ci, out var s) ? s : 3f;
            Vector2 mv = parts.Length > 2 ? new Vector2(float.Parse(parts[1], ci), float.Parse(parts[2], ci)) : new Vector2(0f, 1f);
            if (!testBike) throw new System.Exception("no test bike");
            Vector3 start = testBike.transform.position;
            float yaw0 = testBike.transform.eulerAngles.y;
            double end = EditorApplication.timeSinceStartup + secs;
            GameInput.TouchMove = mv;
            void Tick()
            {
                if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
                if (EditorApplication.timeSinceStartup < end) return;
                GameInput.TouchMove = Vector2.zero;
                EditorApplication.update -= Tick;
                Debug.Log($"[Nainital] ride go: {Vector3.Distance(start, testBike.transform.position):F1} m in {secs:F1} s, speed {testBike.Bike.Speed:F1} m/s, " +
                          $"turned {Mathf.DeltaAngle(yaw0, testBike.transform.eulerAngles.y):F0} deg");
            }
            EditorApplication.update += Tick;
        }

        /// <summary>The nearest traffic two-wheeler with a rider is knocked over as by the player's car (its rider is thrown).
        /// Then RideFallen once it lies still.</summary>
        public static void KnockBike()
        {
            Need();
            var p = PlayerController.Instance;
            TrafficBike best = null; float bd = float.MaxValue;
            foreach (var tb in Object.FindObjectsByType<TrafficBike>(FindObjectsSortMode.None))
            {
                if (tb.Down || !tb.GetComponent<PlayerBike>() || tb.GetComponentsInChildren<NpcWalker>(true).Length == 0) continue;
                float d = (tb.transform.position - p.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = tb; }
            }
            if (!best) throw new System.Exception("no traffic bike with a rider");
            testBike = best.GetComponent<PlayerBike>();
            // arg = the hit's speed, m/s (default 9: a car from the side at ~32 km/h, a hard hit - they stay down)
            float sp = float.TryParse(Arg(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var a0) && a0 > 0f ? a0 : 9f;
            Vector3 v = Vector3.Cross(Vector3.up, best.transform.forward) * sp;
            testBike.Bike.Crash(v, null);
            Debug.Log($"[Nainital] knock: {best.name} at {best.transform.position:F0}, {Mathf.Sqrt(bd):F0} m away");
        }

        /// <summary>The player beside the fallen test bike: its prompt, whether E works, then E (pick it up and ride).</summary>
        public static void RideFallen()
        {
            Need();
            var p = PlayerController.Instance;
            if (!testBike) throw new System.Exception("no test bike (KnockBike first)");
            var b = testBike.Bike;
            p.GetComponent<CharacterMotor>().Teleport(Ground(testBike.transform.position - Vector3.ProjectOnPlane(testBike.transform.right, Vector3.up).normalized * 1.5f) + Vector3.up * 0.2f);
            Physics.SyncTransforms();
            float tilt = Vector3.Angle(testBike.transform.up, Vector3.up);
            string before = $"crashed {b.Crashed}, settled {b.Settled}, tilt {tilt:F0} deg, prompt \"{testBike.Prompt}\", can {testBike.CanInteract(p)}";
            testBike.Interact(p);
            Debug.Log($"[Nainital] ride fallen: {before} -> player {p.CurrentMode}, riding {(PlayerBike.Riding ? PlayerBike.Riding.name : "-")}, " +
                      $"crashed {b.Crashed}, tilt {Vector3.Angle(testBike.transform.up, Vector3.up):F0} deg, traffic {(testBike.GetComponent<TrafficBike>() ? "still" : "gone")}");
        }

        /// <summary>E on the bike: off (brakes first if it is still moving).</summary>
        public static void RideOff()
        {
            Need();
            GameInput.TouchMove = Vector2.zero;
            if (PlayerBike.Riding) PlayerBike.Riding.Interact(PlayerController.Instance);
            Debug.Log($"[Nainital] ride off: player {PlayerController.Instance.CurrentMode}");
        }

        public static void Crash()
        {
            Need();
            var p = PlayerController.Instance;
            var mat = Object.FindAnyObjectByType<TrafficManager>().material;
            // on the road nearest the player: the Bolero across it, the WagonR 30 m back along the road
            if (!RoadRoute.ReadyAny()) throw new System.Exception("no road network");
            int k = RoadRoute.Nearest(p.transform.position, 200f);
            Vector3 at = RoadRoute.Pos(k);
            Vector3 f = RoadRoute.Heading(k);
            Vector3 r = Vector3.Cross(Vector3.up, f);
            if (a) Object.Destroy(a);
            if (b) Object.Destroy(b);
            // the unbranded set since 21 Nov (the old Bolero / WagonR are gone from the library)
            a = VehicleFactory.Create(CarType.Suv, Px.CarWhite, mat, true, "Test parked SUV", Model("car_suv_boxy"));
            a.transform.SetPositionAndRotation(Ground(at) + Vector3.up * 0.3f, Quaternion.LookRotation(r));
            b = VehicleFactory.Create(CarType.Hatchback, Px.CarRed, mat, true, "Test hatch", Model("car_hatch_small"));
            int kb = RoadRoute.Nearest(at - f * 30f, 40f);
            Vector3 bp = kb >= 0 ? RoadRoute.Pos(kb) : at - f * 30f;
            Vector3 fb = (at - bp); fb.y = 0f; fb.Normalize();
            b.transform.SetPositionAndRotation(Ground(bp) + Vector3.up * 0.4f, Quaternion.LookRotation(fb));
            f = -fb;
            p.GetComponent<CharacterMotor>().Teleport(Ground(at + r * 12f + f * 4f) + Vector3.up * 0.2f);
            Vector3 look = at - p.transform.position; look.y = 0f;
            p.transform.rotation = Quaternion.LookRotation(look.normalized);
            if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.SnapBehind();
            Physics.SyncTransforms();
            var cb = b.GetComponent<CarController>();
            cb.ai = true;
            cb.aiThrottle = 0f;
            var rbb = b.GetComponent<Rigidbody>();
            rbb.WakeUp();
            rbb.linearVelocity = -f * 15f;
            aStart = a.transform.position;
            bStart = b.transform.position;
            t0 = Time.time;
            aPeakV = bPeakV = aRise = bRise = aSpin = bSpin = 0f;
            log.Clear();
            EditorApplication.update -= Track;
            EditorApplication.update += Track;
            Debug.Log($"[Nainital] crash test: parked Bolero at {aStart:F0}, WagonR at {bStart:F0} doing 15 m/s; masses {a.GetComponent<Rigidbody>().mass} / {rbb.mass}");
        }

        static void Track()
        {
            if (!EditorApplication.isPlaying || !a || !b) { EditorApplication.update -= Track; return; }
            var ra = a.GetComponent<Rigidbody>();
            var rb = b.GetComponent<Rigidbody>();
            var cb = b.GetComponent<CarController>();
            if (cb) { cb.aiThrottle = 0f; cb.aiHandbrake = 0f; }
            aPeakV = Mathf.Max(aPeakV, ra.linearVelocity.magnitude);
            bPeakV = Mathf.Max(bPeakV, Time.time - t0 > 0.3f ? rb.linearVelocity.magnitude : 0f);
            aRise = Mathf.Max(aRise, a.transform.position.y - aStart.y);
            bRise = Mathf.Max(bRise, b.transform.position.y - bStart.y);
            aSpin = Mathf.Max(aSpin, ra.angularVelocity.magnitude);
            bSpin = Mathf.Max(bSpin, rb.angularVelocity.magnitude);
            if (Time.time - t0 > 6f) EditorApplication.update -= Track;
        }

        public static void Report()
        {
            Need();
            if (!a || !b) throw new System.Exception("no crash test running");
            var ra = a.GetComponent<Rigidbody>();
            var rb = b.GetComponent<Rigidbody>();
            string s = $"t {Time.time - t0:F1}s  parked Bolero (mass {ra.mass}): peak {aPeakV:F1} m/s, rose {aRise:F2} m, spin {aSpin:F1} rad/s, moved {Vector3.Distance(a.transform.position, aStart):F1} m, now v {ra.linearVelocity.magnitude:F1} asleep {ra.IsSleeping()} kinematic {ra.isKinematic} up {Vector3.Dot(a.transform.up, Vector3.up):F2}\n" +
                       $"            hatch (mass {rb.mass}): after-hit peak {bPeakV:F1} m/s, rose {bRise:F2} m, spin {bSpin:F1} rad/s, now v {rb.linearVelocity.magnitude:F1} up {Vector3.Dot(b.transform.up, Vector3.up):F2}";
            foreach (var (go, nm) in new[] { (a, "SUV"), (b, "hatch") })
            {
                var vp = go.GetComponent<VehicleParts>();
                s += $"\n            {nm} dent: {(vp ? vp.DentedVerts : -1)} body vertices moved, deepest {(vp ? vp.DentDeepest * 100f : 0f):F1} cm";
            }
            Debug.Log("[Nainital] crash report\n" + s);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../Bridge/vehicle_report.txt")), s);
        }

        // ---- a two-wheeler against a bus: real mass (owner, 22 Nov 2026: "ek bike waala meri bus ko hi dhakka lgaate huye
        // peeche leaaya ... wheres physics?")
        static GameObject bbBus, bbBike;
        static string bbMode;
        static Vector3 bbBusStart, bbBikeStart;
        static float bbT0, bbBusPeakV, bbBusMoved, bbPen, bbBusV0, bbBusVAfter, bbBikeMoved;
        static bool bbTouched;
        static float bbTouchT, bbLastV;

        /// <summary>Bridge (Play), arg = mode [old]: "into" - a bike rides at 5 m/s into the front of a standing bus (free to
        /// roll, no handbrake) and keeps trying for 6 s; "bus2" / "bus7" - the bus rolls at ~2 / ~7 m/s into a standing bike
        /// across its way; "car7" - a 950 kg hatch at ~7 m/s into it (momentum: the car should lose about
        /// m_bike / (M + m_bike) of its speed). Writes Bridge/bikebus_report.txt after 6 s: how far the vehicle was pushed,
        /// its peak speed, the deepest bike-in-vehicle overlap, its speed before / after the contact, whether the bike was
        /// knocked over, how far it went.</summary>
        public static void BikeBus()
        {
            Need();
            var parts = Arg().ToLowerInvariant().Split(new[] { ' ', ',' }, System.StringSplitOptions.RemoveEmptyEntries);
            bbMode = parts.Length > 0 ? parts[0] : "into";
            if (bbBus) Object.Destroy(bbBus);
            if (bbBike) Object.Destroy(bbBike);
            var p = PlayerController.Instance;
            var mat = Object.FindAnyObjectByType<TrafficManager>().material;
            if (!RoadRoute.ReadyAny()) throw new System.Exception("no road network");
            int k = RoadRoute.Nearest(p.transform.position, 200f);
            Vector3 at = RoadRoute.Pos(k), f = RoadRoute.Heading(k);
            f.y = 0f;
            f.Normalize();
            Vector3 r = Vector3.Cross(Vector3.up, f);
            var lib = VehicleLibrary.Instance;
            bbBus = bbMode.StartsWith("car") ? VehicleFactory.Create(CarType.Hatchback, Px.CarRed, mat, true, "Test hatch", Model("car_hatch_small"))
                                             : VehicleFactory.Create(CarType.Bus, Px.CarWhite, mat, true, "Test bus", lib ? lib.Pick(CarType.Bus, 0.3f) : -1);
            bbBus.transform.SetPositionAndRotation(Ground(at) + Vector3.up * 0.4f, Quaternion.LookRotation(f));
            var cb = bbBus.GetComponent<CarController>();
            cb.ai = true;
            cb.aiThrottle = 0f;
            cb.aiHandbrake = bbMode == "into" ? 1f : 0f;
            BikeRide.LogHits = true;
            var info = bbBus.GetComponent<VehicleInfo>();
            float half = (info ? info.length : 10f) * 0.5f;
            var e = lib.PickBike(0.5f);
            bbBike = BikeRide.Create(e, "Test bike");
            var bike = bbBike.GetComponent<BikeRide>();
            bool into = bbMode == "into";
            bool fast = bbMode.EndsWith("7");
            float gap = into ? 7f : fast ? 9f : 4f;
            Vector3 bp = at + f * (half + gap);
            bike.Place(bp, into ? -f : r);
            if (into)
            {
                var d = bbBike.AddComponent<BikeTestDriver>();
                d.aim = at;
                d.speed = 5f;
            }
            else bike.Park();
            p.GetComponent<CharacterMotor>().Teleport(Ground(at + r * 14f + f * (half + 2f)) + Vector3.up * 0.2f);
            Vector3 look = bp - p.transform.position;
            look.y = 0f;
            p.transform.rotation = Quaternion.LookRotation(look.normalized);
            if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.SnapBehind();
            Physics.SyncTransforms();
            var rb = bbBus.GetComponent<Rigidbody>();
            rb.WakeUp();
            bbBusV0 = into ? 0f : fast ? 7.5f : 2.5f;
            if (!into) rb.linearVelocity = f * bbBusV0;
            bbBusStart = bbBus.transform.position;
            bbBikeStart = bbBike.transform.position;
            bbT0 = Time.time;
            bbBusPeakV = bbBusMoved = bbPen = bbBusVAfter = bbBikeMoved = 0f;
            bbTouched = false;
            EditorApplication.update -= BikeBusTrack;
            EditorApplication.update += BikeBusTrack;
            Debug.Log($"[Nainital] bike-bus test '{bbMode}': bus {bbBus.name} mass {rb.mass} at {at:F0}, bike {e.name} {bike.Mass:F0} kg {gap} m off its nose");
        }

        /// <summary>Film the Game view: arg "name count seconds-apart" -> Shots/play_name_00.png ... (a move seen in motion,
        /// not one pose). Bridge (Play): call VehicleTest.Film</summary>
        public static void Film()
        {
            Need();
            var a = Arg().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            string nm = a.Length > 0 ? a[0] : "film";
            int n = a.Length > 1 ? int.Parse(a[1], ci) : 12;
            double every = a.Length > 2 ? double.Parse(a[2], ci) : 0.25;
            int i = 0;
            double next = EditorApplication.timeSinceStartup;
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Shots"));
            void Tick()
            {
                if (!EditorApplication.isPlaying || i >= n) { EditorApplication.update -= Tick; return; }
                if (EditorApplication.timeSinceStartup < next) return;
                next += every;
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"play_{nm}_{i:00}.png"));
                i++;
            }
            EditorApplication.update += Tick;
        }

        /// <summary>The player's controls held: arg "seconds steer throttle" (default "3 0 1") through GameInput.TouchMove,
        /// then let go. Bridge (Play): call VehicleTest.Throttle</summary>
        public static void Throttle()
        {
            Need();
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var a = Arg().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            float secs = a.Length > 0 ? float.Parse(a[0], ci) : 3f;
            var mv = new Vector2(a.Length > 1 ? float.Parse(a[1], ci) : 0f, a.Length > 2 ? float.Parse(a[2], ci) : 1f);
            double end = EditorApplication.timeSinceStartup + secs;
            GameInput.TouchMove = mv;
            void Tick()
            {
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup >= end) { GameInput.TouchMove = Vector2.zero; EditorApplication.update -= Tick; }
            }
            EditorApplication.update += Tick;
        }

        /// <summary>The seated view: arg "toggle" (V / the Camera button) or "yaw pitch" to look round in first person.
        /// Bridge (Play): call VehicleTest.Cam</summary>
        public static void Cam()
        {
            Need();
            var a = Arg().Trim();
            var tpc = ThirdPersonCamera.Instance;
            if (a == "toggle" || a.Length == 0) GameInput.TouchCamera = true;
            else
            {
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                var p = a.Split(' ');
                tpc.LookAround(float.Parse(p[0], ci), p.Length > 1 ? float.Parse(p[1], ci) : 0f);
            }
            Debug.Log($"[Nainital] cam: first person {ThirdPersonCamera.FirstPerson} (before this frame), arg '{a}'");
        }

        static GameObject eBus;

        /// <summary>A passenger of a bus (owner, 22 Nov 2026: "bus me as passenger bethne ka bhi rkho"). Bridge (Play): call
        /// VehicleTest.BusRide - a standing bus on the road by the player, him 2.5 m from its passenger door, E
        /// (BusPassenger: walk up, climb in, sit); called again while he rides: E (off at the door). Film meanwhile.</summary>
        public static void BusRide()
        {
            Need();
            var p = PlayerController.Instance;
            if (BusPassenger.Riding) { var r0 = BusPassenger.Riding; r0.Interact(p); Debug.Log($"[Nainital] bus ride: E to get off {r0.name}, boarding {p.Boarding}"); return; }
            if (eBus) Object.Destroy(eBus);
            var mat = Object.FindAnyObjectByType<TrafficManager>().material;
            if (!RoadRoute.ReadyAny()) throw new System.Exception("no road network");
            int k = RoadRoute.Nearest(p.transform.position, 200f);
            Vector3 at = RoadRoute.Pos(k), f = RoadRoute.Heading(k);
            f.y = 0f;
            f.Normalize();
            var lib = VehicleLibrary.Instance;
            eBus = VehicleFactory.Create(CarType.Bus, Px.CarWhite, mat, true, "Test bus ride", lib ? lib.Pick(CarType.Bus, 0.3f) : -1);
            eBus.transform.SetPositionAndRotation(Ground(at) + Vector3.up * 0.4f, Quaternion.LookRotation(f));
            Physics.SyncTransforms();
            var bp = eBus.AddComponent<BusPassenger>();
            Vector3 door = bp.Point, outw = door - eBus.transform.position;
            outw.y = 0f;
            outw.Normalize();
            p.GetComponent<CharacterMotor>().Teleport(Ground(door + outw * 2.5f) + Vector3.up * 0.1f);
            Vector3 look = door - p.transform.position;
            look.y = 0f;
            p.transform.rotation = Quaternion.LookRotation(look.normalized);
            if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.SnapBehind();
            Physics.SyncTransforms();
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlaying || !bp) return;
                Debug.Log($"[Nainital] bus ride: E at {eBus.name} - prompt \"{bp.Prompt}\", can {bp.CanInteract(p)}");
                bp.Interact(p);
                Debug.Log($"[Nainital] bus ride: boarding {p.Boarding}, mode {p.CurrentMode}, riding {(BusPassenger.Riding ? BusPassenger.Riding.name : "-")}");
            };
        }

        static GameObject eCar;

        /// <summary>Getting in and out, seen (owner, 22 Nov 2026: "entry exit ka animation bhi nhi chlra"). Bridge (Play):
        /// call VehicleTest.CarE - a car (arg = model, default car_sedan_compact... any library name) on the road by the
        /// player, the player 2.5 m out from its driver's door, E (PlayerController.BoardVehicle); called again while he
        /// is in it: E (AlightVehicle). Capture frames meanwhile.</summary>
        public static void CarE()
        {
            Need();
            var p = PlayerController.Instance;
            if (p.Car)
            {
                // arg a number: the car moving at that many m/s when he presses E (a jump-out: PlayerController.BailOut)
                var c0 = p.Car;
                if (float.TryParse(Arg(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sp) && sp > 0f)
                    c0.GetComponent<Rigidbody>().linearVelocity = c0.transform.forward * sp;
                c0.Interact(p);
                Debug.Log($"[Nainital] car E: out of {c0.name} at {c0.GetComponent<Rigidbody>().linearVelocity.magnitude:F1} m/s, boarding {p.Boarding}, ragdoll {p.Ragdolled}, hp {p.Health.current:F0}");
                return;
            }
            if (eCar) Object.Destroy(eCar);
            var mat = Object.FindAnyObjectByType<TrafficManager>().material;
            if (!RoadRoute.ReadyAny()) throw new System.Exception("no road network");
            int k = RoadRoute.Nearest(p.transform.position, 200f);
            Vector3 at = RoadRoute.Pos(k), f = RoadRoute.Heading(k);
            f.y = 0f;
            f.Normalize();
            string model = Arg();
            int mi = Model(string.IsNullOrEmpty(model) ? "car_hatch_small" : model);
            eCar = VehicleFactory.Create(CarType.Hatchback, Px.CarRed, mat, true, "Test car E", mi);
            eCar.transform.SetPositionAndRotation(Ground(at) + Vector3.up * 0.3f, Quaternion.LookRotation(f));
            Physics.SyncTransforms();
            var car = eCar.GetComponent<CarController>();
            var vp = eCar.GetComponent<VehicleParts>();
            Vector3 e = vp && vp.HasSetup ? vp.EntryWorld(0) : eCar.transform.position + eCar.transform.right * 1.5f;
            Vector3 outw = e - eCar.transform.position;
            outw.y = 0f;
            outw.Normalize();
            p.GetComponent<CharacterMotor>().Teleport(Ground(e + outw * 2.5f - f * 1f) + Vector3.up * 0.1f);
            Vector3 look = e - p.transform.position;
            look.y = 0f;
            p.transform.rotation = Quaternion.LookRotation(look.normalized);
            if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.SnapBehind();
            Physics.SyncTransforms();
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlaying || !car) return;
                car.Interact(p);
                Debug.Log($"[Nainital] car E: into {eCar.name} ({(mi >= 0 ? VehicleLibrary.Instance.entries[mi].name : "default")}), doors {(vp ? vp.HasDoors : false)}, boarding {p.Boarding}");
            };
        }

        /// <summary>Log every contact that changes a two-wheeler's velocity by over 1.2 m/s ("[Nainital] bike hit: ..."), and
        /// every knock-over, for the town's traffic. Bridge (Play): call VehicleTest.LogBikeHits</summary>
        public static void LogBikeHits() { Need(); BikeRide.LogHits = true; Debug.Log("[Nainital] bike hits logged"); }

        static void BikeBusTrack()
        {
            if (!EditorApplication.isPlaying || !bbBus || !bbBike) { EditorApplication.update -= BikeBusTrack; return; }
            var rb = bbBus.GetComponent<Rigidbody>();
            var cb = bbBus.GetComponent<CarController>();
            if (cb) { cb.aiThrottle = 0f; cb.aiHandbrake = bbMode == "into" ? 1f : 0f; }   // "into": a standing bus, brakes on (the owner's case)
            Vector3 mv = bbBus.transform.position - bbBusStart;
            mv.y = 0f;
            bbBusMoved = Mathf.Max(bbBusMoved, mv.magnitude);
            bbBusPeakV = Mathf.Max(bbBusPeakV, bbMode == "into" ? rb.linearVelocity.magnitude : 0f);
            Vector3 bm = bbBike.transform.position - bbBikeStart;
            bm.y = 0f;
            bbBikeMoved = Mathf.Max(bbBikeMoved, bm.magnitude);
            var box = bbBike.GetComponent<BoxCollider>();
            float pen = 0f;
            foreach (var c in bbBus.GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger || !c.enabled) continue;
                if (Physics.ComputePenetration(box, bbBike.transform.position, bbBike.transform.rotation, c, c.transform.position, c.transform.rotation, out _, out float d))
                    pen = Mathf.Max(pen, d);
            }
            float vNow = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude;
            if ((pen > 0f || bbBike.GetComponent<BikeRide>().Crashed) && !bbTouched) { bbTouched = true; bbTouchT = Time.time; bbBusV0 = bbLastV; }   // its speed just before
            bbPen = Mathf.Max(bbPen, pen);
            if (bbTouched && Time.time - bbTouchT < 0.15f) bbBusVAfter = vNow;   // and just after the contact
            bbLastV = vNow;
            if (Time.time - bbT0 < 6f) return;
            EditorApplication.update -= BikeBusTrack;
            var bike = bbBike.GetComponent<BikeRide>();
            string s = $"bike-bus '{bbMode}': bus mass {rb.mass}, pushed {bbBusMoved:F2} m" +
                       (bbMode == "into" ? $", peak speed {bbBusPeakV:F2} m/s" : $", speed {bbBusV0:F2} -> {bbBusVAfter:F2} m/s at contact (momentum, stuck together: {bbBusV0 * rb.mass / (rb.mass + bbBike.GetComponent<BikeRide>().Mass):F2})") +
                       $"; deepest bike-in-bus {bbPen * 100f:F0} cm, touched {bbTouched}; bike {bike.Mass:F0} kg moved {bbBikeMoved:F2} m, knocked over {bike.Crashed}, speed now {bbBike.GetComponent<Rigidbody>().linearVelocity.magnitude:F1}";
            Debug.Log("[Nainital] " + s);
            File.AppendAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../Bridge/bikebus_report.txt")), s + "\n");
        }

        /// <summary>The town's traffic now: how many, how fast, how many standing still, knocks taken (wall and car hits
        /// since they appeared), upside down.</summary>
        public static void TrafficStats()
        {
            Need();
            int n = 0, still = 0, flipped = 0, hits = 0, wrecked = 0;
            float sum = 0f;
            foreach (var t in Object.FindObjectsByType<TrafficCar>(FindObjectsSortMode.None))
            {
                n++;
                sum += t.Speed;
                if (t.Speed < 0.3f) still++;
                if (Vector3.Dot(t.transform.up, Vector3.up) < 0.5f) flipped++;
                if (t.Wrecked) wrecked++;
                var d = t.GetComponent<VehicleDamage>();
                if (d) hits += d.hits;
            }
            int bikes = 0, bikesDown = 0;
            foreach (var b in Object.FindObjectsByType<TrafficBike>(FindObjectsSortMode.None)) { bikes++; if (b.Down) bikesDown++; }
            string s = $"t {Time.time:F0}s traffic cars {n}: mean speed {(n > 0 ? sum / n : 0):F1} m/s, standing {still}, knocks taken {hits}, upside down {flipped}, wrecked {wrecked}; bikes {bikes} (down {bikesDown}); overtakes {TrafficCar.Overtakes}, dropped back {TrafficCar.Aborts}";
            Debug.Log("[Nainital] " + s);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../Bridge/vehicle_report.txt")), s);
        }

        /// <summary>The player throws a punch / a kick (as the buttons would).</summary>
        public static void Punch() { Need(); PlayerController.Instance.GetComponent<Combatant>().TryAttack(false); }
        public static void Kick() { Need(); PlayerController.Instance.GetComponent<Combatant>().TryAttack(true); }

        /// <summary>Punch (arg "kick": kick), and a screenshot 0.25 s later, in the middle of the swing
        /// (Shots/play_attack.png), with the action layer's state in the log.</summary>
        public static void AttackShot()
        {
            Need();
            var p = PlayerController.Instance;
            bool kick = Arg().ToLowerInvariant().Contains("kick");
            p.GetComponent<Combatant>().TryAttack(kick);
            double at = EditorApplication.timeSinceStartup + (kick ? 0.35 : 0.22);
            void Tick()
            {
                if (EditorApplication.timeSinceStartup < at) return;
                EditorApplication.update -= Tick;
                var an = p.Rig ? p.Rig.Anim : null;
                string st = an ? $"layer1 w {an.GetLayerWeight(1):F2} punch {an.GetCurrentAnimatorStateInfo(1).IsName("Jab") || an.GetCurrentAnimatorStateInfo(1).IsName("Cross") || an.GetCurrentAnimatorStateInfo(1).IsName("Kick")} t {an.GetCurrentAnimatorStateInfo(1).normalizedTime:F2}" : "no animator";
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Application.dataPath, "../Shots/play_attack_" + (kick ? "kick" : "punch") + ".png")));
                Debug.Log("[Nainital] attack shot: " + st);
            }
            EditorApplication.update += Tick;
        }

        // ------------------------------------------------------------------ the unbranded set in the game (21 Nov 2026)
        static readonly System.Collections.Generic.List<GameObject> lineup = new System.Collections.Generic.List<GameObject>();

        /// <summary>Every vehicle of the library in the game as the traffic makes it (VehicleFactory / BikeRide with their
        /// VehicleParts), in a row along the road nearest the player, held still, each with people in its seats the way
        /// the game seats them (PedestrianManager.MakeSeated: characters measured to fit) - a car full, a bus its first
        /// eight, a two-wheeler rider and pillion; the traffic is cleared and stopped first. Then call VehicleTest.LineupShots.
        /// Bridge (the Nainital scene in Play): call VehicleTest.Lineup</summary>
        public static void Lineup()
        {
            Need();
            var p = PlayerController.Instance;
            var tm = Object.FindAnyObjectByType<TrafficManager>();
            if (tm) tm.enabled = false;
            foreach (var t in Object.FindObjectsByType<TrafficCar>(FindObjectsSortMode.None)) Object.Destroy(t.gameObject);
            foreach (var t in Object.FindObjectsByType<TrafficBike>(FindObjectsSortMode.None)) Object.Destroy(t.gameObject);
            foreach (var g in lineup) if (g) Object.Destroy(g);
            lineup.Clear();
            var lib = VehicleLibrary.Instance;
            var pm = PedestrianManager.Instance;
            var mat = tm ? tm.material : null;
            // where: Bridge/arg.txt "x,z" (an open level place - The Flats: -60,20), else along the nearest road
            Vector3 at, f;
            var xz = Arg().Split(',');
            if (xz.Length == 2 && float.TryParse(xz[0], out float ax) && float.TryParse(xz[1], out float az))
            {
                var ter = Terrain.activeTerrain;
                at = new Vector3(ax, ter ? ter.SampleHeight(new Vector3(ax, 0f, az)) + ter.transform.position.y : 0f, az);
                f = Vector3.right;
            }
            else
            {
                if (!RoadRoute.ReadyAny()) throw new System.Exception("no road network");
                int k = RoadRoute.Nearest(p.transform.position, 300f);
                at = RoadRoute.Pos(k); f = RoadRoute.Heading(k);
            }
            f.y = 0f; f.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, f);
            float along = 0f, rowLen = 55f;
            int row = 0;
            var log = new StringBuilder("[Nainital] lineup:");
            void Place(GameObject go, float len)
            {
                if (along + len + 3f > rowLen) { row++; along = 0f; }   // rows 14 m apart
                along += len * 0.5f + 1.5f;
                Vector3 pos = Ground(at + f * along - side * (row * 14f));
                go.transform.SetPositionAndRotation(pos + Vector3.up * 0.05f, Quaternion.LookRotation(f));
                along += len * 0.5f + 1.5f;
                var rb = go.GetComponent<Rigidbody>();
                if (rb) { rb.isKinematic = true; rb.linearVelocity = Vector3.zero; }
                lineup.Add(go);
            }
            for (int i = 0; i < lib.entries.Length; i++)
            {
                var e = lib.entries[i];
                if (e == null || !e.prefab || !e.parts) continue;
                var go = VehicleFactory.Create(e.type, Px.CarWhite, mat, true, "Lineup " + e.name, i);
                var car = go.GetComponent<CarController>();
                if (car) car.enabled = false;
                Place(go, e.size.z);
                var vp = go.GetComponent<VehicleParts>();
                int n = vp ? Mathf.Min(vp.SeatCount, e.type == CarType.Bus ? 8 : vp.SeatCount) : 0;
                var who = new StringBuilder();
                for (int s = 0; s < n; s++)
                {
                    var w = pm.MakeSeated(vp, s);
                    if (w) { lineup.Add(w.gameObject); who.Append(s == 0 ? "" : ", ").Append(w.lookId); }
                }
                log.Append($"\n  {e.name}: {who}");
            }
            foreach (var e in lib.bikes)
            {
                if (e == null || !e.prefab || !e.parts) continue;
                var go = BikeRide.Create(e, "Lineup " + e.name);
                var ride = go.GetComponent<BikeRide>();
                Place(go, e.size.z);
                ride.Place(go.transform.position, f);
                var vp = ride.Parts;
                var who = new StringBuilder();
                for (int s = 0; vp && s < Mathf.Min(2, vp.SeatCount); s++)
                {
                    var w = pm.MakeSeated(vp, s);
                    if (w) { lineup.Add(w.gameObject); who.Append(s == 0 ? "" : ", ").Append(w.lookId); }
                }
                log.Append($"\n  {e.name}: {who}");
            }
            // the player beside the first row, looking at it
            Vector3 mid = at + f * (rowLen * 0.5f);
            p.GetComponent<CharacterMotor>().Teleport(Ground(mid + side * 9f) + Vector3.up * 0.2f);
            p.transform.rotation = Quaternion.LookRotation(-side);
            if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.SnapBehind();
            Physics.SyncTransforms();
            Debug.Log(log.ToString());
        }

        /// <summary>The row made by Lineup, one picture per vehicle (front three-quarter from its right, a little high, and
        /// the left side), six to a sheet: Shots/lineup_N.png. Bridge: call VehicleTest.LineupShots</summary>
        public static void LineupShots()
        {
            Need();
            var cars = lineup.FindAll(g => g && (g.GetComponent<VehicleParts>()));
            const int w = 640, h = 420, per = 6;
            for (int sheet = 0; sheet * per < cars.Count; sheet++)
            {
                var tex = new Texture2D(w * 2, h * per, TextureFormat.RGB24, false);
                for (int j = 0; j < per; j++)
                {
                    int idx = sheet * per + j;
                    for (int v = 0; v < 2; v++)
                    {
                        Color32[] px;
                        if (idx < cars.Count) px = Shot(cars[idx], v, w, h);
                        else { px = new Color32[w * h]; }
                        tex.SetPixels32(v * w, (per - 1 - j) * h, w, h, px);
                    }
                }
                tex.Apply();
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, $"../Shots/lineup_{sheet}.png"));
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                Debug.Log("[Nainital] lineup shot " + path);
            }
        }

        static Color32[] Shot(GameObject g, int view, int w, int h)
        {
            var b = new Bounds(g.transform.position, Vector3.zero);
            foreach (var r in g.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            var go = new GameObject("LineupCam");
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = 32f;
            Transform t = g.transform;
            Vector3 dir = view == 0 ? (t.forward * 1.2f + t.right * 1.0f + Vector3.up * 0.45f) : (-t.right + Vector3.up * 0.18f);
            float d = b.extents.magnitude / Mathf.Sin(16f * Mathf.Deg2Rad);
            cam.transform.position = b.center + dir.normalized * d;
            cam.transform.LookAt(b.center);
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = d * 3f;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(go);
            var px = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            return px;
        }

        static GameObject seatCar;
        static Vector3 seatAt;
        static NpcWalker seatDriver;

        /// <summary>One library vehicle 7 m to the player's right, side on, a townsperson at the wheel; the player turned
        /// to look at it (then "capture").</summary>
        public static void Seat()
        {
            Need();
            var p = PlayerController.Instance;
            var lib = VehicleLibrary.Instance;
            string q = Arg();
            int i = int.TryParse(q, out var n) ? n : Model(q);
            if (i < 0 || i >= lib.entries.Length) throw new System.Exception("no model " + q);
            if (seatCar) Object.Destroy(seatCar);
            var e = lib.entries[i];
            var mat = Object.FindAnyObjectByType<TrafficManager>().material;
            if (!RoadRoute.ReadyAny()) throw new System.Exception("no road network");
            if (seatAt == Vector3.zero) seatAt = p.transform.position;
            int k = RoadRoute.Nearest(seatAt, 200f);
            Vector3 at = RoadRoute.Pos(k), f = RoadRoute.Heading(k);
            Vector3 r = Vector3.Cross(Vector3.up, f);
            seatCar = VehicleFactory.Create(e.type, Px.CarWhite, mat, true, "Seat test " + e.name, i);
            seatCar.transform.SetPositionAndRotation(at + Vector3.up * 0.1f, Quaternion.LookRotation(f));
            Physics.SyncTransforms();
            float side = e.size.x * 0.5f + Mathf.Max(4.5f, e.size.z * 0.75f);
            p.GetComponent<CharacterMotor>().Teleport(Ground(at + r * side) + Vector3.up * 0.2f);
            var pm = PedestrianManager.Instance;
            if (seatDriver) Object.Destroy(seatDriver.gameObject);
            seatDriver = pm.MakeRider(seatCar.transform, VehicleSeats.Root(seatCar));
            seatDriver.EnterCar(seatCar.transform, VehicleSeats.Root(seatCar));
            // look past the car's front so the camera, behind the player, sees the cab beside him, not his back
            Vector3 d = seatCar.transform.position + seatCar.transform.forward * (e.size.z * 0.55f) - p.transform.position;
            d.y = 0f;
            p.transform.rotation = Quaternion.LookRotation(d.normalized);
            if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.SnapBehind();
            Debug.Log($"[Nainital] seat test {e.name}: hip {VehicleSeats.Hip(seatCar):F2}, size {e.size:F2}");
        }
    }
}
