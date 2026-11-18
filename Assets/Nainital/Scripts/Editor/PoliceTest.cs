using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Play-mode checks of the police and ambulance system through the bridge ("call PoliceTest.KillNearest", "capture x").
    /// Report() writes the whole state (stars, units, officers, incidents, ambulance, held traffic, bikes) to the log and
    /// to Bridge/police_report.txt.
    /// </summary>
    public static class PoliceTest
    {
        static void Need() { if (!EditorApplication.isPlaying) throw new System.Exception("not playing"); }
        static string Arg() { try { return File.ReadAllText(Path.Combine(Application.dataPath, "../Bridge/arg.txt")).Trim(); } catch { return ""; } }

        static NpcWalker NearestWalker(float max)
        {
            var p = PlayerController.Instance;
            NpcWalker best = null;
            float bd = max * max;
            foreach (var w in Object.FindObjectsByType<NpcWalker>(FindObjectsSortMode.None))
            {
                if (w.InCar || !w.isActiveAndEnabled) continue;
                var h = w.GetComponent<Health>();
                if (h && h.IsDead) continue;
                float d = (w.transform.position - p.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = w; }
            }
            return best;
        }

        /// <summary>The player punches the nearest townsperson (a hit: 1 star).</summary>
        public static void HitNearest()
        {
            Need();
            var p = PlayerController.Instance;
            var w = NearestWalker(60f);
            if (!w) throw new System.Exception("nobody within 60 m");
            w.GetComponent<Health>().Damage(12f, p.transform.position, p.gameObject);
            Debug.Log($"[Nainital] police test: hit {w.npcName} at {w.transform.position:F0}");
        }

        /// <summary>The player kills the nearest townsperson (2 stars, an accident scene, the ambulance).</summary>
        public static void KillNearest()
        {
            Need();
            var p = PlayerController.Instance;
            var w = NearestWalker(80f);
            if (!w) throw new System.Exception("nobody within 80 m");
            w.GetComponent<Health>().Damage(999f, p.transform.position, p.gameObject);
            Debug.Log($"[Nainital] police test: killed {w.npcName} at {w.transform.position:F0}");
        }

        /// <summary>Someone dies near the player with nobody to blame (an accident: police scene car, ambulance, no stars).</summary>
        public static void AccidentNearest()
        {
            Need();
            var w = NearestWalker(80f);
            if (!w) throw new System.Exception("nobody within 80 m");
            w.GetComponent<Health>().Damage(999f, w.transform.position + Vector3.forward, null);
            Debug.Log($"[Nainital] police test: accident, {w.npcName} died at {w.transform.position:F0}");
        }

        public static void Stars3() { Need(); var ws = WantedSystem.Ensure(); for (int i = 0; i < 3; i++) ws.Kill(PlayerController.Instance.transform.position, false); }
        public static void Clear() { Need(); if (WantedSystem.Instance) WantedSystem.Instance.Clear("test clear"); }
        public static void Amar() { Need(); var h = PlayerController.Instance.Health; h.invulnerable = true; h.Revive(1f); }

        /// <summary>Time runs Bridge/arg.txt times faster (1 = normal) to wait out the response.</summary>
        public static void Speed() { Need(); float k = float.TryParse(Arg(), out var v) ? v : 1f; Time.timeScale = Mathf.Clamp(k, 0.1f, 4f); }

        static void Look(Vector3 target, float back)
        {
            var p = PlayerController.Instance;
            Vector3 d = target - p.transform.position;
            d.y = 0f;
            if (back > 0f && d.magnitude > back)
            {
                Vector3 at = target - d.normalized * back;
                if (Physics.Raycast(at + Vector3.up * 40f, Vector3.down, out var g, 120f, ~((1 << 8) | (1 << 9) | (1 << 10) | (1 << 2)), QueryTriggerInteraction.Ignore)) at = g.point + Vector3.up * 0.2f;
                p.GetComponent<CharacterMotor>().Teleport(at);
                d = target - at;
                d.y = 0f;
            }
            if (d.sqrMagnitude > 0.01f) p.transform.rotation = Quaternion.LookRotation(d.normalized);
            if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.SnapBehind();
        }

        /// <summary>Face the nearest police unit (walk up to arg metres from it if it is further).</summary>
        public static void FaceCop()
        {
            Need();
            var p = PlayerController.Instance;
            Transform best = null;
            float bd = float.MaxValue;
            foreach (var o in PoliceOfficer.All)
            {
                if (!o || o.Dead) continue;
                float d = (o.transform.position - p.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = o.transform; }
            }
            if (!best) throw new System.Exception("no police");
            Look(best.position, float.TryParse(Arg(), out var b) ? b : 0f);
        }

        /// <summary>Stand arg metres (default 12) from the 108 ambulance, looking at it.</summary>
        public static void FaceAmbulance()
        {
            Need();
            var pm = PoliceManager.Instance;
            Ambulance a = null;
            if (pm) foreach (var x in pm.Ambulances) if (x) a = x;
            if (!a) throw new System.Exception("no ambulance");
            Look(a.transform.position, float.TryParse(Arg(), out var b) ? b : 12f);
        }

        /// <summary>Stand arg metres (default 9) from the nearest traffic two-wheeler, looking at it.</summary>
        public static void FaceBike()
        {
            Need();
            var p = PlayerController.Instance;
            TrafficBike best = null;
            float bd = float.MaxValue;
            foreach (var t in Object.FindObjectsByType<TrafficBike>(FindObjectsSortMode.None))
            {
                float d = (t.transform.position - p.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = t; }
            }
            if (!best) throw new System.Exception("no traffic bike");
            Look(best.transform.position, float.TryParse(Arg(), out var b) ? b : 9f);
        }

        /// <summary>The player's car, 11 m behind the nearest traffic two-wheeler in its lane, at 16 m/s into it.</summary>
        public static void RamBike()
        {
            Need();
            var car = CarController.Driving;
            if (!car) throw new System.Exception("not driving");
            TrafficBike best = null;
            float bd = float.MaxValue;
            foreach (var t in Object.FindObjectsByType<TrafficBike>(FindObjectsSortMode.None))
            {
                if (t.Down) continue;
                float d = (t.transform.position - car.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = t; }
            }
            if (!best) throw new System.Exception("no traffic bike");
            var rb = car.GetComponent<Rigidbody>();
            Vector3 f = best.transform.forward;
            f.y = 0f;
            f.Normalize();
            rb.position = best.transform.position - f * 11f + Vector3.up * 0.4f;
            rb.rotation = Quaternion.LookRotation(f, Vector3.up);
            rb.linearVelocity = f * 16f + best.GetComponent<BikeRide>().Speed * f;
            rb.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
            Debug.Log($"[Nainital] police test: ramming {best.name} at {best.transform.position:F0}");
        }

        /// <summary>The nearest traffic two-wheelers: down or riding, the rider's health.</summary>
        public static void BikeReport()
        {
            Need();
            var p = PlayerController.Instance;
            var sb = new StringBuilder("[Nainital] bikes:");
            foreach (var t in Object.FindObjectsByType<TrafficBike>(FindObjectsSortMode.None))
            {
                var br = t.GetComponent<BikeRide>();
                sb.Append($"\n  {t.name} d {Vector3.Distance(t.transform.position, p.transform.position):F0} down {t.Down} crashed {br.Crashed} v {br.Speed:F1} up {Vector3.Dot(t.transform.up, Vector3.up):F2}");
            }
            foreach (var w in Object.FindObjectsByType<NpcWalker>(FindObjectsSortMode.None))
            {
                var h = w.GetComponent<Health>();
                if (h && h.current < h.max && (w.transform.position - p.transform.position).sqrMagnitude < 60f * 60f)
                    sb.Append($"\n  hurt {w.npcName} hp {h.current:F0} dead {h.IsDead}");
            }
            Debug.Log(sb.ToString());
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../Bridge/police_report.txt")), sb.ToString());
        }

        /// <summary>Everything about the police and the ambulance now.</summary>
        public static void Report()
        {
            Need();
            var sb = new StringBuilder();
            var p = PlayerController.Instance;
            Vector3 pp = p.transform.position;
            var ws = WantedSystem.Instance;
            sb.AppendLine($"t {Time.time:F1}  player {p.CurrentMode} hp {p.Health.current:F0} at {pp:F0}  stars {(ws ? ws.Stars : -1)} civ {(ws ? ws.CivKills : 0)} cop {(ws ? ws.CopKills : 0)} unseen {(ws ? ws.SinceSeen : 0):F1}s money {GameState.Money}");
            var pm = PoliceManager.Instance;
            if (pm)
            {
                foreach (var c in pm.Cars)
                {
                    if (!c) continue;
                    var d = c.Driver;
                    sb.Append($"  car {c.Current} d {Vector3.Distance(c.transform.position, pp):F0} v {(d ? d.Speed : 0):F1} left {(d ? d.RouteLeft : 0):F0} route {(d && d.HasRoute)} arrived {(d && d.Arrived)} usable {c.Usable} crew");
                    foreach (var o in c.crew) if (o) sb.Append($" [{o.displayName} {o.Current}{(o.Dead ? " DEAD" : "")}]");
                    sb.AppendLine($" lights {(c.lights && c.lights.lights)} siren {(c.lights && c.lights.Sounding)}");
                    sb.AppendLine("     " + (d ? d.State : ""));
                }
                foreach (var b in pm.Bikes)
                {
                    if (!b) continue;
                    var br = b.GetComponent<BikeRide>();
                    sb.Append($"  bike {b.Current} d {Vector3.Distance(b.transform.position, pp):F0} v {(br ? br.Speed : 0):F1} crashed {(br && br.Crashed)} crew");
                    foreach (var o in b.crew) if (o) sb.Append($" [{o.Current}{(o.Dead ? " DEAD" : "")}]");
                    sb.AppendLine();
                }
                foreach (var a in pm.Ambulances)
                    if (a) sb.AppendLine($"  ambulance {a.Phase} d {Vector3.Distance(a.transform.position, pp):F0} to-incident {(a.incident != null ? Vector3.Distance(a.transform.position, a.incident.pos) : -1):F0} v {a.GetComponent<RouteDriver>().Speed:F1} siren {(a.lights && a.lights.Sounding)}  /  {a.GetComponent<RouteDriver>().State}");
            }
            foreach (var o in PoliceOfficer.All)
                if (o && !o.ride) sb.AppendLine($"  on foot {o.displayName} {o.Current}{(o.Dead ? " DEAD" : "")} d {Vector3.Distance(o.transform.position, pp):F0}");
            var im = IncidentManager.Instance;
            if (im)
                foreach (var inc in im.Active)
                    sb.AppendLine($"  incident at {inc.pos:F0} age {inc.age:F0} bodies {inc.bodies.Count} police {(inc.police ? inc.police.Current.ToString() : "-")} ambulance {(inc.ambulance ? inc.ambulance.Phase : inc.ambulanceSent ? "sent/none" : "-")} done {inc.done}");
            int held = 0, cars = 0;
            foreach (var t in Object.FindObjectsByType<TrafficCar>(FindObjectsSortMode.None))
            {
                cars++;
                if (t.Speed < 0.5f && IncidentManager.HoldAhead(t.transform.position, t.transform.forward, out _)) held++;
            }
            int bikes = 0, riders = 0;
            foreach (var t in Object.FindObjectsByType<TrafficBike>(FindObjectsSortMode.None)) { bikes++; if (t.GetComponentInChildren<NpcWalker>()) riders++; }
            sb.AppendLine($"  traffic cars {cars} held at accident {held}; two-wheelers {bikes} with riders {riders}; sirens {EmergencyLights.All.Count}");
            string s = sb.ToString();
            Debug.Log("[Nainital] police report\n" + s);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../Bridge/police_report.txt")), s);
        }
    }
}
