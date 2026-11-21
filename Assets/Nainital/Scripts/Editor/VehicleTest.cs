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
            int n = 0, standing = 0, person = 0, vehicle = 0, other = 0, clear = 0;
            int mask = ~((1 << 2) | (1 << 4));
            var others = new System.Collections.Generic.List<string>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                var tc = t.GetComponent<TrafficCar>(); var tb = t.GetComponent<TrafficBike>();
                if (!tc && !tb) continue;
                n++;
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
            Debug.Log($"[Nainital] jam report: {n} traffic, {standing} standing - in front: person {person}, vehicle {vehicle}, other {other}, nothing {clear}" + (others.Count > 0 ? " | other: " + string.Join("; ", others) : ""));
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
            Vector3 v = Vector3.Cross(Vector3.up, best.transform.forward) * 9f;   // a car from the side at ~32 km/h
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
            string s = $"t {Time.time:F0}s traffic cars {n}: mean speed {(n > 0 ? sum / n : 0):F1} m/s, standing {still}, knocks taken {hits}, upside down {flipped}, wrecked {wrecked}; bikes {bikes} (down {bikesDown})";
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
