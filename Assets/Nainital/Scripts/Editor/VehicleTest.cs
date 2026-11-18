using System.IO;
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
            a = VehicleFactory.Create(CarType.Suv, Px.CarWhite, mat, true, "Test parked Bolero", Model("Bolero"));
            a.transform.SetPositionAndRotation(Ground(at) + Vector3.up * 0.3f, Quaternion.LookRotation(r));
            b = VehicleFactory.Create(CarType.Hatchback, Px.CarRed, mat, true, "Test WagonR", Model("WagonR"));
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
                       $"            WagonR (mass {rb.mass}): after-hit peak {bPeakV:F1} m/s, rose {bRise:F2} m, spin {bSpin:F1} rad/s, now v {rb.linearVelocity.magnitude:F1} up {Vector3.Dot(b.transform.up, Vector3.up):F2}";
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
