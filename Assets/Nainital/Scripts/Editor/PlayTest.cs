using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>Play the game in the editor as a phone would (user, 2026-11-11: "unity me run krke chlaake dekhna, phir
    /// apk nikaalna"): touch controls on, a phone-shaped Game view, and steps the bridge can call one at a time
    /// ("call PlayTest.Begin", "capture name", "call PlayTest.OpenCheats" ...). Errors logged while playing go to
    /// Bridge/play_errors.txt.</summary>
    public static class PlayTest
    {
        public const string TouchPref = "nt_touch";
        static readonly string ErrPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Bridge/play_errors.txt"));

        [InitializeOnLoadMethod]
        static void Hook()
        {
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            if (!EditorApplication.isPlaying) return;
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            try { File.AppendAllText(ErrPath, $"{System.DateTime.Now:HH:mm:ss} {type}: {msg}\n{stack}\n"); } catch { }
        }

        /// <summary>Touch controls on, a 2316 x 1080 Game view (the S22 Ultra's default screen), then Play.</summary>
        public static void Begin()
        {
            PlayerPrefs.SetInt(TouchPref, 1);
            PlayerPrefs.Save();
            PlayerSettings.runInBackground = true;   // keep playing (and capturing) while the editor is not the window in front
            try { File.Delete(ErrPath); } catch { }
            PlayModeWindow.SetViewType(PlayModeWindow.PlayModeViewTypes.GameView);
            PlayModeWindow.SetCustomRenderingResolution(2316, 1080, "Phone 2316x1080");
            EditorApplication.isPlaying = true;
        }

        public static void End()
        {
            EditorApplication.isPlaying = false;
            PlayerPrefs.DeleteKey(TouchPref);
            PlayerPrefs.Save();
        }

        static void Need() { if (!EditorApplication.isPlaying) throw new System.Exception("not playing"); }

        public static void OpenCheats() { Need(); if (TouchControls.Instance) TouchControls.Instance.ToggleCheats(); else throw new System.Exception("no touch controls"); }
        public static void Udaan() { Need(); CheatCodes.Run("UDAAN"); }
        public static void Safar() { Need(); CheatCodes.Run("SAFAR"); }
        public static void FlyUp() { Need(); GameInput.TouchJumpHeld = true; }
        public static void FlyStop() { Need(); GameInput.TouchJumpHeld = false; GameInput.TouchMove = Vector2.zero; }
        public static void Forward() { Need(); GameInput.TouchMove = new Vector2(0f, 1f); }
        public static void Map() { Need(); GameInput.TouchMap = true; }
        public static void Land() { Need(); GameInput.TouchInteract = true; }

        /// <summary>Close any open phone panel (the cheat panel or the places list).</summary>
        public static void ClosePanels()
        {
            Need();
            foreach (var n in new[] { "CheatPanel", "PlacesPanel" })
            {
                var go = GameObject.Find(n);
                if (go) { if (n == "PlacesPanel") Object.Destroy(go); else go.SetActive(false); }
            }
            GameInput.UiBlocking = false;
        }

        /// <summary>Pick the first entry of the open places list (as a tap would).</summary>
        public static void PickFirstPlace()
        {
            Need();
            var go = GameObject.Find("PlacesPanel");
            if (!go) throw new System.Exception("places list not open");
            var b = go.GetComponentInChildren<UnityEngine.UI.ScrollRect>().content.GetComponentInChildren<UnityEngine.UI.Button>();
            b.onClick.Invoke();
        }

        /// <summary>Every transparent marker beam near the player: does it fade, and how far is it from the camera.</summary>
        public static void Markers()
        {
            Need();
            var cam = Camera.main;
            Debug.Log($"[Nainital] markers: Camera.main {(cam ? cam.name : "NULL")}, MarkerFade {Object.FindObjectsByType<MarkerFade>(FindObjectsSortMode.None).Length}");
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!r.sharedMaterial || !r.sharedMaterial.name.Contains("Marker")) continue;
                float d = cam ? Vector3.Distance(r.transform.position, cam.transform.position) : -1f;
                if (d > 40f) continue;
                var mf = r.GetComponent<MarkerFade>();
                Debug.Log($"[Nainital]   {r.transform.parent?.name}/{r.name} mat {r.sharedMaterial.name} shader {r.sharedMaterial.shader.name} fade {(mf != null)} k {(mf ? mf.LastK : -1f):F2} alpha {(mf && mf.Mat ? mf.Mat.GetColor("_BaseColor").a : -1f):F3} same {(mf && mf.Mat == r.sharedMaterial)} dist {d:F1} enabled {r.enabled}");
            }
        }

        /// <summary>Every renderer whose bounds a ray from the camera crosses within 40 m, through a few screen points:
        /// what is drawn there, whatever its queue.</summary>
        public static void WhatIsOnScreen()
        {
            Need();
            var cam = Camera.main;
            foreach (var vp in new[] { new Vector2(0.06f, 0.55f), new Vector2(0.86f, 0.55f), new Vector2(0.5f, 0.55f) })
            {
                var ray = cam.ViewportPointToRay(new Vector3(vp.x, vp.y, 0f));
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                    if (!r.bounds.IntersectRay(ray, out float t) || t > 40f) continue;
                    if (r.bounds.size.magnitude > 300f) continue;   // the terrain, the lake
                    Debug.Log($"[Nainital] at {vp}: {PathOf(r.transform)} t {t:F1} mat {(r.sharedMaterial ? r.sharedMaterial.name : "-")} queue {(r.sharedMaterial ? r.sharedMaterial.renderQueue : -1)} {r.GetType().Name} size {r.bounds.size}");
                }
            }
        }

        /// <summary>Every transparent thing within 25 m of the player: what fills the view.</summary>
        public static void NearTransparent()
        {
            Need();
            var p = PlayerController.Instance;
            if (!p) return;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.sharedMaterial || r.sharedMaterial.renderQueue < 2900) continue;
                if (Vector3.Distance(r.bounds.ClosestPoint(p.transform.position), p.transform.position) > 400f || r.sharedMaterial.name.StartsWith("M_Water") || r.sharedMaterial.name.StartsWith("M_Rain")) continue;
                Debug.Log($"[Nainital] near: {PathOf(r.transform)} mat {r.sharedMaterial.name} shader {r.sharedMaterial.shader.name} size {r.bounds.size}");
            }
        }

        static string PathOf(Transform t) { string s = t.name; while (t.parent) { t = t.parent; s = t.name + "/" + s; } return s; }

        /// <summary>Hide or show one kind of thing, to find what draws something odd.</summary>
        public static void TreesOff() { Need(); foreach (var t in Object.FindObjectsByType<TreeRenderer>(FindObjectsSortMode.None)) t.enabled = false; }
        public static void TreesOn() { Need(); foreach (var t in Object.FindObjectsByType<TreeRenderer>(FindObjectsSortMode.None)) t.enabled = true; }
        public static void GrassOff() { Need(); foreach (var t in Object.FindObjectsByType<GrassRenderer>(FindObjectsSortMode.None)) t.enabled = false; }
        public static void GrassOn() { Need(); foreach (var t in Object.FindObjectsByType<GrassRenderer>(FindObjectsSortMode.None)) t.enabled = true; }

        static string Arg() { try { return File.ReadAllText(Path.Combine(Application.dataPath, "../Bridge/arg.txt")).Trim(); } catch { return ""; } }

        /// <summary>Objects whose name contains Bridge/arg.txt (edit or play mode): name and position.</summary>
        public static void Where()
        {
            string q = Arg();
            int n = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!t.name.Contains(q)) continue;
                Debug.Log($"[Nainital] where {PathOf(t)} at {t.position:F1}");
                if (++n >= 25) break;
            }
            Debug.Log($"[Nainital] where '{q}': {n} shown");
        }

        /// <summary>Put the player beside the first object whose name contains Bridge/arg.txt, or at "x,y,z".</summary>
        public static void GoTo()
        {
            Need();
            string q = Arg();
            Vector3 target;
            var parts = q.Split(',');
            if (parts.Length == 3 && float.TryParse(parts[0], out var x) && float.TryParse(parts[1], out var y) && float.TryParse(parts[2], out var z)) target = new Vector3(x, y, z);
            else
            {
                Transform hit = null;
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) if (t.name.Contains(q)) { hit = t; break; }
                if (!hit) throw new System.Exception("nothing named " + q);
                target = hit.position + hit.forward * 6f;
            }
            if (Physics.Raycast(target + Vector3.up * 50f, Vector3.down, out var g, 200f, ~((1 << 8) | (1 << 9) | (1 << 2)), QueryTriggerInteraction.Ignore)) target = g.point + Vector3.up * 0.2f;
            var p = PlayerController.Instance;
            p.GetComponent<CharacterMotor>().Teleport(target);
            Debug.Log($"[Nainital] went to {target:F1} ({q})");
        }

        /// <summary>The sound beds playing now, their volumes, and how near each town zone is.</summary>
        public static void AudioReport()
        {
            Need();
            var am = AudioManager.Instance;
            if (!am) { Debug.Log("[Nainital] audio: no AudioManager"); return; }
            var f = typeof(AudioManager).GetField("beds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var z = (float[])typeof(AudioManager).GetField("zoneNear", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(am);
            var sb = new System.Text.StringBuilder("[Nainital] audio beds:");
            foreach (System.Collections.DictionaryEntry e in (System.Collections.IDictionary)f.GetValue(am))
            {
                var bed = e.Value;
                var src = (AudioSource)bed.GetType().GetField("src").GetValue(bed);
                if (src && src.isPlaying && src.volume > 0.005f) sb.Append($" {e.Key} {src.volume:F2}");
            }
            sb.Append($" | zones market {z[0]:F2} street {z[1]:F2} cafe {z[2]:F2} kids {z[3]:F2} bus {z[4]:F2} traffic {z[5]:F2}");
            sb.Append($" | sources {Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length}, traffic cars {Object.FindObjectsByType<TrafficCar>(FindObjectsSortMode.None).Length}, walkers {Object.FindObjectsByType<NpcWalker>(FindObjectsSortMode.None).Length}");
            Debug.Log(sb.ToString());
        }

        /// <summary>Daylight for a test: 11 o'clock, the clock held still.</summary>
        public static void Noon() { Need(); var d = DayNightCycle.Instance; if (d) { d.timeOfDay = 11f; d.paused = true; } }
        /// <summary>Evening on the Mall: 7 pm, held.</summary>
        public static void Evening() { Need(); var d = DayNightCycle.Instance; if (d) { d.timeOfDay = 19f; d.paused = true; } }

        /// <summary>Turn the player (and the camera behind them) to face the point or object named in Bridge/arg.txt.</summary>
        public static void Face()
        {
            Need();
            string q = Arg();
            Vector3 target;
            var parts = q.Split(',');
            if (parts.Length == 3 && float.TryParse(parts[0], out var x) && float.TryParse(parts[1], out var y) && float.TryParse(parts[2], out var z)) target = new Vector3(x, y, z);
            else
            {
                Transform hit = null;
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) if (t.name.Contains(q)) { hit = t; break; }
                if (!hit) throw new System.Exception("nothing named " + q);
                target = hit.position;
            }
            var p = PlayerController.Instance;
            Vector3 d = target - p.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f) p.transform.rotation = Quaternion.LookRotation(d.normalized);
            if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.SnapBehind();
        }

        /// <summary>Get into the nearest drivable car (as the Use button would).</summary>
        public static void EnterCar()
        {
            Need();
            var p = PlayerController.Instance;
            CarController best = null;
            float bd = 30f;
            foreach (var c in Object.FindObjectsByType<CarController>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(c.transform.position, p.transform.position);
                if (d < bd) { bd = d; best = c; }
            }
            if (!best) throw new System.Exception("no car within 30 m");
            best.Interact(p);
            Debug.Log($"[Nainital] entered {best.name} at {best.transform.position:F1}");
        }

        public static void Throttle() { Need(); GameInput.TouchMove = new Vector2(0f, 1f); }

        /// <summary>Walk up to the nearest moving traffic car and take it (the user's black-screen case).</summary>
        public static void TakeTraffic()
        {
            Need();
            var p = PlayerController.Instance;
            TrafficCarEntry best = null;
            float bd = float.MaxValue;
            foreach (var t in Object.FindObjectsByType<TrafficCarEntry>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(t.transform.position, p.transform.position);
                if (d < bd) { bd = d; best = t; }
            }
            if (!best) throw new System.Exception("no traffic car");
            p.GetComponent<CharacterMotor>().Teleport(best.Point + Vector3.up * 0.2f);
            Physics.SyncTransforms();
            Debug.Log($"[Nainital] taking {best.name} ({bd:F0} m away) at {best.transform.position:F1}");
            best.Interact(p);
        }

        /// <summary>The car being driven: where, how fast, physics state, what its wheels stand on, the camera.</summary>
        public static void CarReport()
        {
            Need();
            var c = CarController.Driving;
            var cam = Camera.main;
            if (!c) { Debug.Log("[Nainital] car: not driving"); return; }
            var rb = c.GetComponent<Rigidbody>();
            var sb = new System.Text.StringBuilder($"[Nainital] car {c.name} at {c.transform.position:F1} rot {c.transform.eulerAngles:F0} v {rb.linearVelocity:F1} kmh {c.SpeedKmh:F0} kinematic {rb.isKinematic} mass {rb.mass}");
            foreach (var col in c.GetComponentsInChildren<Collider>(true)) sb.Append($" | col {col.name} {col.GetType().Name} layer {col.gameObject.layer} enabled {col.enabled} trigger {col.isTrigger}");
            foreach (var w in c.wheels) if (w) sb.Append($" | wheel {w.name} local {w.localPosition:F2}");
            if (Physics.Raycast(c.transform.position + Vector3.up * 3f, Vector3.down, out var g, 50f, ~((1 << 10) | (1 << 8) | (1 << 9) | (1 << 2)), QueryTriggerInteraction.Ignore))
                sb.Append($" | ground {g.collider.name} at {g.point.y:F1}");
            if (cam) sb.Append($" | camera {cam.transform.position:F1} near {cam.nearClipPlane}");
            Debug.Log(sb.ToString());
        }

        /// <summary>Every library vehicle in a row beside the player, side on to the camera (texture check).</summary>
        public static void Lineup()
        {
            Need();
            var lib = VehicleLibrary.Instance;
            var p = PlayerController.Instance;
            if (!lib) throw new System.Exception("no vehicle library");
            Vector3 f = p.transform.forward; f.y = 0f; f.Normalize();
            Vector3 r = Vector3.Cross(Vector3.up, f);
            Vector3 at = p.transform.position + f * 9f - r * 18f;
            var parent = new GameObject("Lineup").transform;
            for (int i = 0; i < lib.entries.Length; i++)
            {
                var e = lib.entries[i];
                var go = VehicleFactory.CreateModel(e, e.type, Px.CarWhite, false, "Lineup " + e.name);
                go.transform.SetParent(parent, false);
                Vector3 q = at + r * 0f;
                if (Physics.Raycast(q + Vector3.up * 30f, Vector3.down, out var g, 80f, ~((1 << 10) | (1 << 8) | (1 << 9) | (1 << 2)), QueryTriggerInteraction.Ignore)) q = g.point;
                go.transform.SetPositionAndRotation(q, Quaternion.LookRotation(r));
                at += r * (e.size.z + 1.5f);
            }
            Debug.Log($"[Nainital] lineup: {lib.entries.Length} vehicles");
        }

        /// <summary>Set the clock to the hour in Bridge/arg.txt (e.g. 0.5 or 19) and hold it.</summary>
        public static void SetHour()
        {
            Need();
            var d = DayNightCycle.Instance;
            if (d && float.TryParse(Arg(), out var h)) { d.timeOfDay = h; d.paused = true; }
        }

        /// <summary>How many people and cars are out, against what the hour wants.</summary>
        public static void CrowdReport()
        {
            Need();
            float h = TownClock.Hour;
            int walkers = 0, home = 0, idle = 0, hiddenLocals = 0, locals = 0;
            foreach (var w in Object.FindObjectsByType<NpcWalker>(FindObjectsSortMode.None))
            {
                if (w.homeAtNight) { locals++; if (!w.CanInteract(PlayerController.Instance)) hiddenLocals++; continue; }
                walkers++;
                if (w.GoingHome) home++;
                if (w.idle) idle++;
            }
            var pm = Object.FindAnyObjectByType<PedestrianManager>();
            var tm = Object.FindAnyObjectByType<TrafficManager>();
            int cars = Object.FindObjectsByType<TrafficCar>(FindObjectsSortMode.None).Length;
            Debug.Log($"[Nainital] crowd at {h:F1}h: walkers {walkers} (going home {home}, browsing {idle}) of target {(pm ? Mathf.RoundToInt(pm.maxPeds * TownClock.Crowd(h)) : -1)}, " +
                      $"locals {locals} (hidden {hiddenLocals}), traffic {cars} of target {(tm ? Mathf.RoundToInt(tm.maxCars * TownClock.Traffic(h)) : -1)}, glow {Shader.GetGlobalFloat("_NtLampGlow"):F2}");
        }

        /// <summary>Street lamps within 60 m of the point in Bridge/arg.txt ("x,y,z"), from the scene's LampManager.</summary>
        public static void LampsNear()
        {
            var parts = Arg().Split(',');
            var c = new Vector3(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]));
            var lm = Object.FindAnyObjectByType<LampManager>();
            if (!lm) { Debug.Log("[Nainital] no LampManager"); return; }
            int n = 0;
            foreach (var h in lm.lampHeads) if (Vector2.Distance(new Vector2(h.x, h.z), new Vector2(c.x, c.z)) < 60f) { n++; if (n <= 12) Debug.Log($"[Nainital]   lamp at {h:F1}"); }
            Debug.Log($"[Nainital] lamps within 60 m of {c}: {n} (of {lm.lampHeads.Length})");
        }

        /// <summary>Enabled point lights within 40 m of the player: where, how bright, what colour.</summary>
        public static void LightReport()
        {
            Need();
            var p = PlayerController.Instance.transform.position;
            int n = 0, all = 0;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Point || !l.enabled) continue;
                all++;
                float d = Vector3.Distance(l.transform.position, p);
                if (d > 40f) continue;
                n++;
                if (n <= 10) Debug.Log($"[Nainital]   light {l.name} at {l.transform.position:F1} d {d:F0} int {l.intensity:F1} range {l.range:F0} col {l.color}");
            }
            Debug.Log($"[Nainital] point lights on: {all}, within 40 m: {n}, glow {Shader.GetGlobalFloat("_NtLampGlow"):F2}, night {(DayNightCycle.Instance ? DayNightCycle.Instance.NightFactor : -1):F2}");
        }

        public static void CheatGaadi() { Need(); CheatCodes.Run("GAADI"); }
        public static void CheatJeep() { Need(); CheatCodes.Run("JEEP"); }

        /// <summary>Where the player is, and what is on screen.</summary>
        public static void Report()
        {
            Need();
            var p = PlayerController.Instance;
            Debug.Log($"[Nainital] play: player at {(p ? p.transform.position.ToString("F1") : "none")}, mode {(p ? p.CurrentMode.ToString() : "-")}, " +
                      $"jetpack {Jetpack.Flying}, touch {(TouchControls.Instance != null)}, ui blocking {GameInput.UiBlocking}, fps {(1f / Mathf.Max(Time.smoothDeltaTime, 1e-4f)):F0}, screen {Screen.width}x{Screen.height}");
        }
    }
}
