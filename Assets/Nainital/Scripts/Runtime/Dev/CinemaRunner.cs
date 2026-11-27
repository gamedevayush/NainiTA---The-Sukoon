using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

namespace Nainital
{

    /// <summary>One shot of the showcase reel (Editor/CinemaReel): a place, the hour, a slow camera move.</summary>
    [System.Serializable]
    public class CinemaShot
    {
        public enum Move { Orbit, Dolly, Walk, Road }
        public string name;
        public Vector3 anchor;          // the place (on the ground)
        public Move move;
        public float radius, height, a0, a1, lookH = 1.6f, duration = 8f, hour, settle = 16f;
        public Vector3 dir;             // Dolly: along this, from -radius to +a1 metres
        public float side = 1.4f;       // Walk / Road: the camera this far to the side of the path it follows
        /// <summary>The camera at u (0..1) of the move: an eased orbit round the place, or a dolly along a road.</summary>
        public void Pose(float u, out Vector3 pos, out Vector3 look)
        {
            float e = u * u * (3f - 2f * u);
            if (move == Move.Orbit && dir == Vector3.zero)
            {
                float a = Mathf.Lerp(a0, a1, e) * Mathf.Deg2Rad;
                pos = anchor + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
                pos.y = Mathf.Max(StoryWalker.Ground(pos + Vector3.up * 200f).y + 1.2f, anchor.y + height);
                look = anchor + Vector3.up * lookH;
            }
            else
            {
                Vector3 d = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
                pos = anchor + d * Mathf.Lerp(-radius, a1, e);
                pos.y = StoryWalker.Ground(pos + Vector3.up * 200f).y + height;
                look = pos + d * 12f + Vector3.up * (lookH - height);
            }
        }
    }

    /// <summary>
    /// Runs the showcase reel's shots in Play mode (Editor/CinemaReel makes it; a dev tool, nothing in the game uses it): for each shot the player (hidden) is put at the place
    /// so its people, traffic and animals come round it, the hour set and held, then the camera moves along the shot's path.
    /// Stills: three 1280x720 frames (start, middle, end) to check the framing. Video: every frame rendered at 3840x2160
    /// with the game clock stepped exactly 1/60 s a frame (Time.captureFramerate), read back from the GPU and piped to
    /// ffmpeg (NVENC H.264, high quality) - smooth 60 fps however slowly the frames render.
    /// </summary>
    public class CinemaRunner : MonoBehaviour
    {
        public List<CinemaShot> shots;
        public bool video;
        public string outDir, ffmpeg = "ffmpeg";
        public static bool Busy;
        /// <summary>Timeline mode: record the scene's own camera timeline (the PlayableDirector on `timelineName`), once
        /// per hour in `hours`, in `weather` (owner, 27 Nov 2026: "CinematicAnimation... ek baar raat ka or ek baar day ka...
        /// best graphics... cloudy... realistic"). maxSeconds &gt; 0: only that much (a speed test).</summary>
        public string timelineName;
        public float[] hours;
        public WeatherSystem.Kind weather = WeatherSystem.Kind.Cloudy;
        public float maxSeconds;
        /// <summary>Timeline mode, stills only: the timeline at these seconds, one 4K PNG each (owner: "2-3 frames... then I'll decide").</summary>
        public float[] stillTimes;
        /// <summary>Cloud in the sky, the sun through it: how grey the light may get (WeatherSystem.OvercastCap), and how dark
        /// the shadows at least are (DayNightCycle.ShadowMin).</summary>
        public float overcastCap = 0.15f, shadowMin = 0.85f;
        /// <summary>The PC render pipeline (Assets/Settings/PC_RPAsset: soft shadows, SSAO, extra lights' shadows), set by
        /// CinemaReel: the editor on Android runs the Mobile one (hard shadows, one cascade), and the PC quality level is
        /// not allowed there.</summary>
        public RenderPipelineAsset pipeline;
        /// <summary>Night: the nearest street lamps (LampManager.ShadowLamps) and car headlights casting soft shadows. A lamp
        /// is six shadow maps, a headlight one: 8 + 8 fill an 8192 atlas at 1024 each.</summary>
        public int lampShadows = 8, headlightShadows = 8;
        /// <summary>Timeline video: the shots' bounds (the camera's cuts, found by CinemaReel; first 0, last the end), each
        /// shot its own clip after the town settles `shotSettle` s round its start, then all joined into the film.
        /// onlyShots (1-based): just these.</summary>
        public float[] shotBounds;
        public int[] onlyShots;
        public float shotSettle = 12f;
        /// <summary>Timeline mode: the player (the town fills in round him) kept here instead of under the camera - a shot over
        /// the lake would put him on the lake bed. Zero: under the camera.</summary>
        public Vector3 playerAt;
        /// <summary>The last shot finished (CinemaReel.State).</summary>
        public static string Progress = "";

        static readonly List<Light> heads = new List<Light>();
        /// <summary>The nearest `n` lit headlights cast soft shadows, the rest none (0: all back to none).</summary>
        static void HeadlightShadows(Camera cam, int n)
        {
            heads.Clear();
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Spot && l.name.StartsWith("Headlight")) heads.Add(l);
            Vector3 c = cam ? cam.transform.position : Vector3.zero;
            heads.Sort((a, b) => (a.transform.position - c).sqrMagnitude.CompareTo((b.transform.position - c).sqrMagnitude));
            int k = 0;
            foreach (var l in heads)
            {
                bool sh = n > 0 && l.isActiveAndEnabled && k < n;
                if (sh) k++;
                var want = sh ? LightShadows.Soft : LightShadows.None;
                if (l.shadows != want) l.shadows = want;
            }
        }

        public IEnumerator Run()
        {
            Busy = true;
            var cam = Camera.main;
            var p = PlayerController.Instance;
            var dn = DayNightCycle.Instance;
            var tpc = ThirdPersonCamera.Instance;
            if (tpc) tpc.enabled = false;
            GameInput.StoryLock = true;
            if (HUD.Instance) HUD.Instance.SetAlpha(0f);
            if (p) foreach (var r in p.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            Boost(true);
            // the PC pipeline whatever the editor's platform (Android = Mobile: hard shadows only, one cascade, no SSAO)
            var rpWas = QualitySettings.renderPipeline;
            if (pipeline) QualitySettings.renderPipeline = pipeline;
            var sunL = dn ? dn.sun : null;
            var sunShWas = sunL ? sunL.shadows : LightShadows.None;
            if (sunL) sunL.shadows = LightShadows.Soft;
            WeatherSystem.OvercastCap = overcastCap;
            DayNightCycle.ShadowMin = shadowMin;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            float rs = urp ? urp.renderScale : 1f, sd = urp ? urp.shadowDistance : 150f;
            float lod = QualitySettings.lodBias;
            int shRes = urp ? urp.mainLightShadowmapResolution : 2048, casc = urp ? urp.shadowCascadeCount : 2;
            int addRes = urp ? urp.additionalLightsShadowmapResolution : 2048;
            LampManager.ShadowLamps = lampShadows;
            try
            {
                GameSettings.Quality = 2;
                if (urp) { urp.renderScale = 1f; urp.shadowDistance = 300f; urp.mainLightShadowmapResolution = 4096; urp.shadowCascadeCount = 4; urp.additionalLightsShadowmapResolution = 8192; }
                QualitySettings.shadowResolution = UnityEngine.ShadowResolution.VeryHigh;
                QualitySettings.lodBias = 3f;
                foreach (var t in Object.FindObjectsByType<TreeRenderer>(FindObjectsSortMode.None)) { t.maxDistance = 2600f; t.lod0Distance = 170f; }
                foreach (var g in Object.FindObjectsByType<GrassRenderer>(FindObjectsSortMode.None)) g.radius = 80f;
                Debug.Log($"[Nainital] cinema: quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}, pipeline {(urp ? urp.name : "-")}, " +
                          $"soft shadows {(urp && urp.supportsSoftShadows)}, sun {(sunL ? sunL.shadows.ToString() : "-")}, overcast cap {overcastCap}, shadow min {shadowMin}");
                var cd = cam.GetUniversalAdditionalCameraData();
                cd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cd.antialiasingQuality = AntialiasingQuality.High;
                cd.renderPostProcessing = true;
                cam.farClipPlane = Mathf.Max(cam.farClipPlane, 6000f);
                foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
                    if (v.sharedProfile)
                    {
                        if (v.sharedProfile.TryGet<DepthOfField>(out var dof)) dof.active = false;
                        if (string.IsNullOrEmpty(timelineName) && v.sharedProfile.TryGet<MotionBlur>(out var mb)) mb.active = false;
                    }
                if (!string.IsNullOrEmpty(timelineName))
                    foreach (var h in hours) yield return Timeline(h, cam, p, dn);
                else
                    foreach (var s in shots)
                    {
                        yield return Prepare(s, p, dn);
                        if (video) yield return Video(s, cam);
                        else yield return Stills(s, cam);
                    }
            }
            finally
            {
                if (urp) { urp.renderScale = rs; urp.shadowDistance = sd; urp.mainLightShadowmapResolution = shRes; urp.shadowCascadeCount = casc; urp.additionalLightsShadowmapResolution = addRes; }
                LampManager.ShadowLamps = 0;
                HeadlightShadows(cam, 0);
                if (pipeline) QualitySettings.renderPipeline = rpWas;
                if (sunL) sunL.shadows = sunShWas;
                WeatherSystem.OvercastCap = 1f;
                DayNightCycle.ShadowMin = 0f;
                QualitySettings.lodBias = lod;
                Time.captureFramerate = 0;
                cam.targetTexture = null;
                Boost(false);
                GameSettings.ApplyGraphics();
                if (p) foreach (var r in p.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
                if (tpc) tpc.enabled = true;
                GameInput.StoryLock = false;
                if (HUD.Instance) HUD.Instance.SetAlpha(1f);
                if (dn) dn.paused = false;
                Busy = false;
                Debug.Log("[Nainital] cinema: done");
            }
        }

        // more people, traffic and animals round the camera while shooting (Play mode only: the scene's values come back)
        int carsWas, bikesWas, pedsWas, animalsWas; float densWas; int[] capsWas;
        float tMin, tMax, pMin, pMax, aMin, aMax;
        void Boost(bool on)
        {
            var tm = Object.FindAnyObjectByType<TrafficManager>();
            var pm = PedestrianManager.Instance;
            var am = AnimalManager.Instance;
            if (on)
            {
                if (tm) { carsWas = tm.maxCars; bikesWas = tm.maxBikes; tMin = tm.spawnMin; tMax = tm.spawnMax; tm.maxCars = 34; tm.maxBikes = 18; tm.spawnMin = 15f; tm.spawnMax = 110f; }
                if (pm) { pedsWas = pm.maxPeds; pMin = pm.spawnMin; pMax = pm.spawnMax; pm.maxPeds = 110; pm.maxBrowsers = 30; pm.spawnMin = 4f; pm.spawnMax = 50f; }
                if (am) { densWas = am.density; animalsWas = am.maxTotal; capsWas = (int[])am.caps.Clone(); aMin = am.spawnMin; aMax = am.spawnMax; am.density = 1f; am.maxTotal = 16; am.caps = new[] { 3, 8, 3, 3 }; am.spawnMin = 8f; am.spawnMax = 40f; }
            }
            else
            {
                if (tm && carsWas > 0) { tm.maxCars = carsWas; tm.maxBikes = bikesWas; tm.spawnMin = tMin; tm.spawnMax = tMax; }
                if (pm && pedsWas > 0) { pm.maxPeds = pedsWas; pm.spawnMin = pMin; pm.spawnMax = pMax; }
                if (am && capsWas != null) { am.density = densWas; am.maxTotal = animalsWas; am.caps = capsWas; am.spawnMin = aMin; am.spawnMax = aMax; }
            }
        }

        IEnumerator Prepare(CinemaShot s, PlayerController p, DayNightCycle dn)
        {
            Time.captureFramerate = 0;
            if (dn) { dn.timeOfDay = s.hour; dn.paused = true; dn.Apply(); }
            if (WeatherSystem.Instance) WeatherSystem.Instance.SetNow(WeatherSystem.Kind.Clear);
            if (s.move == CinemaShot.Move.Walk || s.move == CinemaShot.Move.Road) FollowPath(s);
            if (p) SaveGame.Place(p, StoryWalker.Ground(s.anchor) + Vector3.up * 0.2f, 0f);
            s.Pose(0f, out var pos, out var look);
            // let the town fill in round the place (people, traffic, animals spawn round the player, never in view up close:
            // the camera looks at the sky meanwhile)
            Camera.main.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(Vector3.up, Vector3.forward));
            for (float t = 0f; t < s.settle; t += Time.unscaledDeltaTime) yield return null;
            Camera.main.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
        }

        /// <summary>A Walk shot runs along the footpath nearest its place (where the people walk), a Road shot along the road
        /// (the traffic): the path's point and its way there, the camera `side` metres off it.</summary>
        void FollowPath(CinemaShot s)
        {
            Vector3 best = s.anchor, dir = Vector3.forward; float bd = float.MaxValue;
            void Look(Vector3[] pts)
            {
                if (pts == null) return;
                for (int i = 0; i + 1 < pts.Length; i++)
                {
                    float d = (pts[i] - s.anchor).sqrMagnitude;
                    if (d < bd) { bd = d; best = pts[i]; int j = Mathf.Min(pts.Length - 1, i + 4); dir = pts[j] - pts[i]; if (dir.sqrMagnitude < 0.01f && i > 0) dir = pts[i] - pts[Mathf.Max(0, i - 4)]; }
                }
            }
            if (s.move == CinemaShot.Move.Walk && PedestrianManager.Instance && PedestrianManager.Instance.network)
                foreach (var w in PedestrianManager.Instance.network.walks) Look(w.pts);
            else
            {
                var tm = Object.FindAnyObjectByType<TrafficManager>();
                if (tm && tm.network) foreach (var r in tm.network.roads) Look(r.pts);
            }
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
            dir.Normalize();
            if (s.a0 != 0f) dir = Quaternion.AngleAxis(s.a0, Vector3.up) * dir;   // turned / reversed along the path
            Vector3 sideV = Vector3.Cross(Vector3.up, dir) * s.side;
            s.anchor = best + sideV;
            s.dir = dir;
            Debug.Log($"[Nainital] cinema: {s.name} follows the {(s.move == CinemaShot.Move.Walk ? "footpath" : "road")} at {best:F0} ({Mathf.Sqrt(bd):F0} m from the place), heading {dir:F2}");
        }

        IEnumerator Timeline(float hour, Camera cam, PlayerController p, DayNightCycle dn)
        {
            var go = GameObject.Find(timelineName);
            var pd = go ? go.GetComponent<UnityEngine.Playables.PlayableDirector>() : null;
            if (!pd || !pd.playableAsset) { Debug.LogWarning("[Nainital] cinema: no timeline on " + timelineName); yield break; }
            // only this timeline drives the camera (CinematicAnimation plays on awake and would fight a second one)
            foreach (var other in Object.FindObjectsByType<UnityEngine.Playables.PlayableDirector>(FindObjectsSortMode.None))
                if (other != pd && other.state == UnityEngine.Playables.PlayState.Playing) other.Stop();
            Time.captureFramerate = 0;
            pd.Stop();
            pd.timeUpdateMode = UnityEngine.Playables.DirectorUpdateMode.GameTime;
            pd.extrapolationMode = UnityEngine.Playables.DirectorWrapMode.None;
            pd.time = 0; pd.Evaluate();
            if (dn) { dn.timeOfDay = hour; dn.paused = true; dn.Apply(); }
            if (WeatherSystem.Instance) WeatherSystem.Instance.SetNow(weather);
            Vector3 start = cam.transform.position;
            var motor = p ? p.GetComponent<CharacterMotor>() : null;
            void Follow()
            {
                if (!p) return;
                var g = StoryWalker.Ground((playerAt != Vector3.zero ? playerAt : cam.transform.position) + Vector3.up * 200f);
                if (motor) motor.Teleport(g + Vector3.up * 0.2f); else p.transform.position = g;
            }
            Follow();
            // the town fills in round the start (the camera at the sky meanwhile: people never spawn in view up close)
            Quaternion r0 = cam.transform.rotation;
            for (float t = 0f; t < 16f; t += Time.unscaledDeltaTime) { cam.transform.SetPositionAndRotation(start, Quaternion.LookRotation(Vector3.up, Vector3.forward)); yield return null; }
            if (stillTimes != null && stillTimes.Length > 0)
            {
                string tg = hour >= 18.5f || hour < 5.5f ? "night" : "day";
                var srt = new RenderTexture(3840, 2160, 24, RenderTextureFormat.ARGB32);
                var tex = new Texture2D(3840, 2160, TextureFormat.RGB24, false);
                Time.timeScale = 1f;
                foreach (var st in stillTimes)
                {
                    pd.time = st; pd.Evaluate();
                    Follow();
                    Vector3 cp = cam.transform.position; Quaternion cr = cam.transform.rotation;
                    // the town round this point fills in (the camera at the sky meanwhile), then the frame
                    for (float t = 0f; t < 12f; t += Time.unscaledDeltaTime) { cam.transform.SetPositionAndRotation(cp, Quaternion.LookRotation(Vector3.up, Vector3.forward)); yield return null; }
                    pd.time = st; pd.Evaluate();
                    yield return null;
                    pd.time = st; pd.Evaluate();
                    cam.targetTexture = srt;
                    HeadlightShadows(cam, headlightShadows);
                    cam.Render(); cam.Render();
                    RenderTexture.active = srt;
                    tex.ReadPixels(new Rect(0, 0, 3840, 2160), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    cam.targetTexture = null;
                    string f = Path.Combine(outDir, $"{timelineName}_{tg}_{(int)hour:00}{Mathf.RoundToInt((hour % 1f) * 60f):00}_{st:000}s_4k.png");
                    File.WriteAllBytes(f, tex.EncodeToPNG());
                    Debug.Log($"[Nainital] cinema: frame {f}");
                }
                Destroy(tex); srt.Release(); Destroy(srt);
                pd.Stop();
                yield break;
            }
            string tag = hour >= 18.5f || hour < 5.5f ? "night" : hour < 8f ? "morning" : "day";
            var bounds = shotBounds != null && shotBounds.Length >= 2 ? shotBounds : new[] { 0f, (float)pd.duration };
            var made = new List<string>();
            for (int si = 0; si + 1 < bounds.Length; si++)
            {
                if (onlyShots != null && onlyShots.Length > 0 && System.Array.IndexOf(onlyShots, si + 1) < 0) continue;
                float s0 = bounds[si], s1 = bounds[si + 1];
                if (maxSeconds > 0f) s1 = Mathf.Min(s1, s0 + maxSeconds);
                // the town round this shot's start fills in first (the camera at the sky meanwhile: people never spawn in
                // view up close), so a cut to a new place does not show it filling up
                pd.time = s0; pd.Evaluate();
                Follow();
                Vector3 cp = cam.transform.position;
                for (float t = 0f; t < shotSettle; t += Time.unscaledDeltaTime) { cam.transform.SetPositionAndRotation(cp, Quaternion.LookRotation(Vector3.up, Vector3.forward)); yield return null; }
                string file = Path.Combine(outDir, $"{timelineName}_{tag}_shot{si + 1:00}_4k60{(maxSeconds > 0f ? "_test" : "")}.mp4");
                yield return Record(pd, cam, s0, s1, file, Follow, $"{tag} shot {si + 1}/{bounds.Length - 1}");
                made.Add(file);
            }
            pd.Stop();
            // the whole film: the shots joined end to end (the same encoding: no re-encode)
            if (made.Count > 1 && made.Count == bounds.Length - 1 && maxSeconds <= 0f)
            {
                string list = Path.Combine(outDir, $"{timelineName}_{tag}_shots.txt");
                var lines = new List<string>();
                foreach (var m in made) lines.Add("file '" + m.Replace("\\", "/") + "'");
                File.WriteAllLines(list, lines);
                string whole = Path.Combine(outDir, $"{timelineName}_{tag}_4k60.mp4");
                var cc = Process.Start(new ProcessStartInfo(ffmpeg, $"-y -loglevel error -f concat -safe 0 -i \"{list}\" -c copy -movflags +faststart \"{whole}\"")
                { UseShellExecute = false, CreateNoWindow = true });
                while (!cc.HasExited) yield return null;
                try { File.Delete(list); } catch { }
                Debug.Log($"[Nainital] cinema: film {whole} ({made.Count} shots), ffmpeg exit {cc.ExitCode}");
            }
        }

        /// <summary>The timeline from s0 to just before s1 at 4K 60 fps into an mp4: the director set to each frame's time
        /// (not played, so a shot never shows the next one's first frame), the whole town stepping 1/60 s a frame.</summary>
        IEnumerator Record(UnityEngine.Playables.PlayableDirector pd, Camera cam, float s0, float s1, string file, System.Action follow, string label)
        {
            const int W = 3840, H = 2160, Fps = 60;
            int frames = Mathf.Max(1, Mathf.CeilToInt((s1 - s0) * Fps - 0.01f));
            var ff = Process.Start(new ProcessStartInfo(ffmpeg,
                $"-y -loglevel error -f rawvideo -pix_fmt rgba -s {W}x{H} -r {Fps} -i - -vf vflip -c:v libx264 -preset medium -crf 15 -tune film -profile:v high -threads 0 -pix_fmt yuv420p -movflags +faststart \"{file}\"")
            { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardError = true, CreateNoWindow = true });
            var stdin = ff.StandardInput.BaseStream;
            string err = "";
            ff.ErrorDataReceived += (o, e) => { if (!string.IsNullOrEmpty(e.Data)) err += e.Data + "\n"; };
            ff.BeginErrorReadLine();
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var buf = new byte[W * H * 4];
            int written = 0, pending = 0;
            var t0 = Time.realtimeSinceStartup;
            cam.targetTexture = rt;
            // two frames still at the shot's start first: the motion blur reads the camera's last frame, which was looking
            // at the sky while the town settled (the first frame came out smeared)
            for (int w = 0; w < 3; w++) { pd.time = s0; pd.Evaluate(); yield return null; }
            Time.captureFramerate = Fps;
            for (int i = 0; i < frames; i++)
            {
                pd.time = s0 + i / (double)Fps; pd.Evaluate();
                if (i % 30 == 0) { follow(); HeadlightShadows(cam, headlightShadows); }
                yield return new WaitForEndOfFrame();
                pending++;
                AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32, r =>
                {
                    pending--;
                    if (r.hasError) return;
                    r.GetData<byte>().CopyTo(buf);
                    stdin.Write(buf, 0, buf.Length);
                    written++;
                });
                if (pending > 4) AsyncGPUReadback.WaitAllRequests();
                if (i % 600 == 0) Debug.Log($"[Nainital] cinema: {label} frame {i}/{frames}, {(Time.realtimeSinceStartup - t0):F0} s");
            }
            AsyncGPUReadback.WaitAllRequests();
            Time.captureFramerate = 0;
            cam.targetTexture = null;
            stdin.Flush(); stdin.Close();
            while (!ff.HasExited) yield return null;
            rt.Release(); Destroy(rt);
            Progress = $"{label} done";
            Debug.Log($"[Nainital] cinema: {file} - {written}/{frames} frames ({s0:F2}-{s1:F2} s) in {Time.realtimeSinceStartup - t0:F0} s, ffmpeg exit {ff.ExitCode}" + (err.Length > 0 ? " " + err : ""));
        }

        IEnumerator Stills(CinemaShot s, Camera cam)
        {
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            string[] tag = { "a", "b", "c" };
            for (int k = 0; k < 3; k++)
            {
                s.Pose(k * 0.5f, out var pos, out var look);
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
                cam.targetTexture = rt;
                yield return null;
                yield return new WaitForEndOfFrame();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                File.WriteAllBytes(Path.Combine(outDir, $"cine_{s.name}_{tag[k]}.png"), tex.EncodeToPNG());
            }
            Destroy(tex); rt.Release(); Destroy(rt);
            Debug.Log($"[Nainital] cinema: stills {s.name} at {s.anchor:F0}, {s.hour:F1} h");
        }

        IEnumerator Video(CinemaShot s, Camera cam)
        {
            const int W = 3840, H = 2160, Fps = 60;
            int frames = Mathf.RoundToInt(s.duration * Fps);
            string file = Path.Combine(outDir, s.name + ".mp4");
            var psi = new ProcessStartInfo(ffmpeg,
                $"-y -loglevel error -f rawvideo -pix_fmt rgba -s {W}x{H} -r {Fps} -i - -vf vflip -c:v libx264 -preset medium -crf 15 -tune film -profile:v high -threads 0 -pix_fmt yuv420p -movflags +faststart \"{file}\"")
            { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardError = true, CreateNoWindow = true };
            var ff = Process.Start(psi);
            var stdin = ff.StandardInput.BaseStream;
            string err = "";
            ff.ErrorDataReceived += (o, e) => { if (!string.IsNullOrEmpty(e.Data)) err += e.Data + "\n"; };
            ff.BeginErrorReadLine();
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            var buf = new byte[W * H * 4];
            int written = 0, pending = 0;
            Time.captureFramerate = Fps;   // the world steps 1/60 s a frame, whatever the frames cost
            var t0 = Time.realtimeSinceStartup;
            for (int i = 0; i < frames; i++)
            {
                float u = frames > 1 ? i / (float)(frames - 1) : 0f;
                s.Pose(u, out var pos, out var look);
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
                yield return new WaitForEndOfFrame();
                pending++;
                AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32, r =>
                {
                    pending--;
                    if (r.hasError) { Debug.LogWarning("[Nainital] cinema: a frame failed to read back"); return; }
                    r.GetData<byte>().CopyTo(buf);
                    stdin.Write(buf, 0, buf.Length);
                    written++;
                });
                if (pending > 4) AsyncGPUReadback.WaitAllRequests();
            }
            AsyncGPUReadback.WaitAllRequests();
            Time.captureFramerate = 0;
            cam.targetTexture = null;
            stdin.Flush(); stdin.Close();
            while (!ff.HasExited) yield return null;
            rt.Release(); Destroy(rt);
            Debug.Log($"[Nainital] cinema: {s.name}.mp4 - {written}/{frames} frames in {Time.realtimeSinceStartup - t0:F0} s, ffmpeg exit {ff.ExitCode}" + (err.Length > 0 ? " " + err : ""));
        }
    }
}
