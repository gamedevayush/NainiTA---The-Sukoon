using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// The showcase reel (owner, 27 Nov 2026: "popular jagah ke milaake total 10 day and 10 night cinematics... log ho,
    /// traffic, kutte... kitni details hain isme... best quality"): ten places, each shot by day and by night, a slow camera
    /// move of 8 s, 4K 60 fps, people / traffic / animals boosted round the camera. Play mode, bridge:
    ///   call CinemaReel.Stills  (arg: shot names or numbers, empty = all) - Shots/cine_[name]_[a|b|c].png, framing check
    ///   call CinemaReel.Record  (arg as above) - D:/NainiTA---The-Sukoon/Cinematics/[name].mp4
    ///   call CinemaReel.Reel    - Cinematics/NainiTA_showreel_4k.mp4 (the day shots, then the night ones)
    /// </summary>
    public static class CinemaReel
    {
        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        /// <summary>Ten places: anchors from the map's own places (GameManager's blips) or a road point.</summary>
        static List<CinemaShot> Shots()
        {
            var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            Vector3 Place(string n)
            {
                if (gm && gm.blipNames != null)
                    for (int i = 0; i < gm.blipNames.Length; i++) if (gm.blipNames[i] == n) return StoryWalker.Ground(gm.blipPositions[i] + Vector3.up * 200f);
                Debug.LogWarning("[Nainital] cinema: no place " + n);
                return Vector3.zero;
            }
            Vector3 At(float x, float z) => StoryWalker.Ground(new Vector3(x, 2000f, z));
            // Walk / Road: a dolly along the nearest footpath (people) or road (traffic), radius = metres before the point,
            // a1 = after, height, side = off the path, a0 = turn of the heading (180 = the other way). Orbit: round the place.
            var W = CinemaShot.Move.Walk; var R = CinemaShot.Move.Road;
            var b = new List<(CinemaShot s, float day, float night)>
            {
                (new CinemaShot { name = "mall_road", anchor = At(647.07f, -350.72f), move = W, radius = 7f, a1 = 7f, height = 2.1f, lookH = 1.7f, side = 1.2f }, 17.2f, 20.5f),
                (new CinemaShot { name = "mall_road_tallital", anchor = At(833.0f, -895.8f), move = W, radius = 7f, a1 = 7f, height = 2.2f, lookH = 1.7f, side = 1.0f, a0 = 180f }, 11f, 21f),
                (new CinemaShot { name = "tallital_bus_stand", anchor = Place("Tallital Bus Stand"), move = R, radius = 8f, a1 = 8f, height = 3.2f, lookH = 1.6f, side = 3.8f }, 11.5f, 20.2f),
                (new CinemaShot { name = "bada_bazar", anchor = Place("Bada Bazar"), move = W, radius = 6f, a1 = 6f, height = 2.0f, lookH = 1.7f, side = 0.8f }, 12f, 20.8f),
                (new CinemaShot { name = "tibetan_market", anchor = Place("Tibetan Market"), move = W, radius = 6f, a1 = 6f, height = 2.0f, lookH = 1.7f, side = 1.0f }, 15.5f, 19.8f),
                (new CinemaShot { name = "naina_devi_chowk", anchor = Place("Naina Devi Mandir"), move = W, radius = 6f, a1 = 6f, height = 2.2f, lookH = 2.4f, side = 1.2f }, 10.5f, 21f),
                (new CinemaShot { name = "mallital_road", anchor = Place("Chhota Mandir"), move = R, radius = 8f, a1 = 8f, height = 2.8f, lookH = 1.6f, side = 3.6f }, 14.5f, 21.5f),
                (new CinemaShot { name = "boat_house_promenade", anchor = Place("Boat House Club"), move = W, radius = 7f, a1 = 7f, height = 2.3f, lookH = 1.6f, side = 1.2f }, 16.8f, 20f),
                (new CinemaShot { name = "snow_view", anchor = Place("Snow View"), move = W, radius = 6f, a1 = 6f, height = 2.2f, lookH = 1.8f, side = 1.0f }, 13f, 20.6f),
                (new CinemaShot { name = "lake_crane", anchor = At(470f, -330f), move = CinemaShot.Move.Orbit, radius = 260f, height = 90f, a0 = 200f, a1 = 225f, lookH = 0f, settle = 6f }, 16.5f, 20.3f),
            };
            var list = new List<CinemaShot>();
            int n = 1;
            foreach (var (s, day, _) in b) { var c = JsonUtility.FromJson<CinemaShot>(JsonUtility.ToJson(s)); c.name = $"{n++:00}_day_{s.name}"; c.hour = day; list.Add(c); }
            foreach (var (s, _, night) in b) { var c = JsonUtility.FromJson<CinemaShot>(JsonUtility.ToJson(s)); c.name = $"{n++:00}_night_{s.name}"; c.hour = night; list.Add(c); }
            return list;
        }

        static List<CinemaShot> Pick()
        {
            string arg;
            try { arg = File.ReadAllText(Path.Combine(Root, "Bridge/arg.txt")).Trim(); } catch { arg = ""; }
            var all = Shots();
            if (string.IsNullOrEmpty(arg) || arg == "all") return all;
            var want = arg.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            return all.Where(s => want.Any(w => s.name.StartsWith(w) || s.name.Contains(w))).ToList();
        }

        static void Start(bool video)
        {
            if (!Application.isPlaying) { Debug.Log("[Nainital] cinema: Play mode first (PlayTest.Begin)"); return; }
            if (CinemaRunner.Busy) { Debug.Log("[Nainital] cinema: already running"); return; }
            var shots = Pick();
            string dir = video ? Path.Combine(Root, "Cinematics") : Path.Combine(Root, "Shots");
            Directory.CreateDirectory(dir);
            var go = new GameObject("Cinema runner");
            var r = go.AddComponent<CinemaRunner>();
            r.shots = shots; r.video = video; r.outDir = dir; r.ffmpeg = FFmpeg();
            r.StartCoroutine(r.Run());
            Debug.Log($"[Nainital] cinema: {(video ? "recording" : "stills of")} {shots.Count} shots -> {dir}");
        }

        /// <summary>The scene's camera timeline "CinematicAnimation" recorded at 4K 60 fps, cloudy, best graphics: arg "day",
        /// "night", "both" (default), "test" = the first 5 s by day (a speed check). -> Cinematics/CinematicAnimation_[day|night]_4k60.mp4</summary>
        public static void Timeline()
        {
            if (!Application.isPlaying) { Debug.Log("[Nainital] cinema: Play mode first (PlayTest.Begin)"); return; }
            if (CinemaRunner.Busy) { Debug.Log("[Nainital] cinema: already running"); return; }
            string arg;
            try { arg = File.ReadAllText(Path.Combine(Root, "Bridge/arg.txt")).Trim().ToLowerInvariant(); } catch { arg = ""; }
            string dir = Path.Combine(Root, "Cinematics");
            Directory.CreateDirectory(dir);
            // "tl:<name>": another timeline's director (default CinematicAnimation); "clear": a clear sky (default cloudy)
            string tlName = "CinematicAnimation";
            foreach (var t in arg.Split(' '))
                if (t.StartsWith("tl:"))
                    foreach (var d in UnityEngine.Object.FindObjectsByType<UnityEngine.Playables.PlayableDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        if (string.Equals(d.name, t.Substring(3), StringComparison.OrdinalIgnoreCase)) tlName = d.name;
            var go = new GameObject("Cinema runner");
            var r = go.AddComponent<CinemaRunner>();
            r.timelineName = tlName; r.video = true; r.outDir = dir; r.ffmpeg = FFmpeg();
            r.weather = arg.Split(' ').Contains("clear") ? WeatherSystem.Kind.Clear : WeatherSystem.Kind.Cloudy;
            if (tlName == "MorningShot") r.playerAt = PlaceAt("The Flats", new Vector3(-59.6f, 749.6f, 50.2f));   // Mallital, up the lake in view
            r.pipeline = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            r.hours = arg == "day" || arg == "test" ? new[] { 15.5f } : arg == "night" ? new[] { 21f } : new[] { 15.5f, 21f };
            var director = GameObject.Find(tlName)?.GetComponent<UnityEngine.Playables.PlayableDirector>();
            if (director) r.shotBounds = ShotBounds(director);
            if (r.shotBounds != null) Debug.Log("[Nainital] cinema: shots " + string.Join(" | ", r.shotBounds.Select(b => b.ToString("F2"))));
            // "shots [day|night] [3 7 ...] [test]": the shots, one clip each by day (15:30) / night (21:00) / both, then the film
            if (arg.StartsWith("shots"))
            {
                var tok = arg.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();
                r.hours = tok.Contains("day") ? new[] { 15.5f } : tok.Contains("night") ? new[] { 21f } : new[] { 15.5f, 21f };
                r.onlyShots = tok.Where(x => !x.StartsWith("h")).Select(x => int.TryParse(x, out var n) ? n : 0).Where(n => n > 0).ToArray();
                r.maxSeconds = tok.Contains("test") ? 2f : 0f;
                var hs = tok.Where(x => x.Length > 1 && x[0] == 'h' && char.IsDigit(x[1]))
                    .Select(x => float.Parse(x.Substring(1), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                if (hs.Length > 0) r.hours = hs;   // "shots h6.25": at that hour
            }
            r.maxSeconds = arg == "test" ? 5f : 0f;
            if (arg.StartsWith("frames"))
            {
                // "frames 12 55 h10.5 h21": seconds, and hours with an h (default 16 and 21)
                var tok = arg.Substring(6).Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                float F(string x) => float.TryParse(x, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : -1f;
                var ts = tok.Where(x => !x.StartsWith("h")).Select(F).Where(v => v >= 0f).ToArray();
                var hs = tok.Where(x => x.StartsWith("h")).Select(x => F(x.Substring(1))).Where(v => v >= 0f).ToArray();
                r.hours = hs.Length > 0 ? hs : new[] { 16f, 21f };
                r.stillTimes = ts.Length > 0 ? ts : new[] { 12f, 55f };
            }
            r.StartCoroutine(r.Run());
            Debug.Log($"[Nainital] cinema: timeline {tlName}, {arg} -> {dir}");
        }

        /// <summary>Why no shadows: the sun, the pipeline, the camera as Unity sees them, and the same view rendered with the
        /// sun's shadows on and off (Shots/diag_sh_on.png, diag_sh_off.png, the mean difference logged). Play mode. arg: an
        /// hour (default 13).</summary>
        public static void ShadowDiag()
        {
            if (!Application.isPlaying) { Debug.Log("[Nainital] diag: Play mode first"); return; }
            float hour = 13f;
            try { float.TryParse(File.ReadAllText(Path.Combine(Root, "Bridge/arg.txt")).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out hour); } catch { }
            try { var h0 = File.ReadAllText(Path.Combine(Root, "Bridge/arg.txt")).Trim().Split(' ')[0]; float.TryParse(h0, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out hour); } catch { }
            if (hour <= 0f) hour = 13f;
            var dn = DayNightCycle.Instance;
            var pre = dn && dn.sun ? dn.sun.transform.eulerAngles : Vector3.zero;
            if (dn) { dn.timeOfDay = hour; dn.paused = true; dn.Apply(); }
            var cam = Camera.main;
            var sun = dn ? dn.sun : null;
            var all = UnityEngine.Object.FindObjectsByType<DayNightCycle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log($"[Nainital] diag: {all.Length} DayNightCycle, hour {(dn ? dn.timeOfDay : -1):F2} elev {(dn ? dn.SunElevation : -99):F1} sun rot before {pre:F0} after {(sun ? sun.transform.eulerAngles : Vector3.zero):F0} parent {(sun && sun.transform.parent ? sun.transform.parent.name + " " + sun.transform.parent.eulerAngles.ToString("F0") : "-")}");
            // the timeline's camera at a time: "13 t12"
            string targ = "";
            try { targ = File.ReadAllText(Path.Combine(Root, "Bridge/arg.txt")); } catch { }
            int ti = targ.IndexOf(" t");
            if (ti >= 0 && float.TryParse(targ.Substring(ti + 2).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var tt))
            {
                var go = GameObject.Find("CinematicAnimation");
                var pd = go ? go.GetComponent<UnityEngine.Playables.PlayableDirector>() : null;
                if (pd) { pd.Stop(); pd.time = tt; pd.Evaluate(); if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.enabled = false; }
            }
            var rs = RenderSettings.sun;
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            var sb = new System.Text.StringBuilder("[Nainital] diag:");
            sb.Append($" quality {QualitySettings.names[QualitySettings.GetQualityLevel()]} QS.shadows {QualitySettings.shadows}");
            sb.Append($" | pipeline {(urp ? urp.name : "-")} mainShadows {(urp && urp.supportsMainLightShadows)} soft {(urp && urp.supportsSoftShadows)} dist {(urp ? urp.shadowDistance : 0)} casc {(urp ? urp.shadowCascadeCount : 0)} res {(urp ? urp.mainLightShadowmapResolution : 0)}");
            sb.Append($" | dn.sun {(sun ? sun.name : "-")} RS.sun {(rs ? rs.name : "-")} same {(sun == rs)}");
            if (sun) sb.Append($" | sun enabled {sun.enabled}/{sun.gameObject.activeInHierarchy} type {sun.type} shadows {sun.shadows} strength {sun.shadowStrength:F2} int {sun.intensity:F2} rot {sun.transform.eulerAngles:F0} fwd {sun.transform.forward:F2} cull {sun.cullingMask} bias {sun.shadowBias:F2}/{sun.shadowNormalBias:F2} mode {sun.lightmapBakeType} renderMode {sun.renderMode}");
            if (sun && sun.TryGetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>(out var ald)) sb.Append($" | ald usePipe {ald.usePipelineSettings} soft {ald.softShadowQuality}");
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) sb.Append($" | dir light {l.name} on {l.isActiveAndEnabled} int {l.intensity:F2} sh {l.shadows}");
            if (cam)
            {
                var cd = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                sb.Append($" | cam {cam.name} near {cam.nearClipPlane} far {cam.farClipPlane} renderShadows {(cd ? cd.renderShadows : false)} renderer {(cd ? cd.scriptableRenderer?.GetType().Name : "-")} pos {cam.transform.position:F0}");
            }
            Debug.Log(sb.ToString());
            if (!cam || !sun) return;
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            Color32[] Shot(string f)
            {
                cam.targetTexture = rt; cam.Render(); cam.Render();
                RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply(); RenderTexture.active = null;
                cam.targetTexture = null;
                File.WriteAllBytes(Path.Combine(Root, "Shots", f), tex.EncodeToPNG());
                return tex.GetPixels32();
            }
            var was = sun.shadows;
            sun.shadows = LightShadows.Soft;
            var a = Shot("diag_sh_on.png");
            sun.shadows = LightShadows.None;
            var b = Shot("diag_sh_off.png");
            sun.shadows = was;
            double d = 0; int changed = 0;
            for (int i = 0; i < a.Length; i++) { int e = Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b); d += e; if (e > 12) changed++; }
            Debug.Log($"[Nainital] diag: shadows on vs off - mean diff {d / a.Length:F2}, {100f * changed / a.Length:F1}% pixels changed");
            UnityEngine.Object.Destroy(tex); rt.Release(); UnityEngine.Object.Destroy(rt);
        }

        static Vector3 PlaceAt(string name, Vector3 fallback)
        {
            var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            if (gm && gm.blipNames != null)
                for (int i = 0; i < gm.blipNames.Length; i++) if (gm.blipNames[i] == name) return gm.blipPositions[i];
            return fallback;
        }

        /// <summary>The owner's morning scene (27 Nov 2026: "6:30 ke paas... jahan color gradient aara hai sky me... lake ke
        /// water se upr naina peak tk krte huye sky dikhayenge..morning shot"): a Timeline "MorningShot" on its own director
        /// in Nainital.unity (Assets/Nainital/Cinematics/MorningShot.playable). The camera starts a metre over the lake's
        /// water looking down at it, rises 42 m and drifts 40 m up the lake while tilting up past the town to Naina Peak and
        /// the sky, 14 s. Five keys on plain position / rotation / field-of-view curves (smooth), so they can be moved in the
        /// Timeline window. Edit mode. Bridge: call CinemaReel.MakeMorningShot, arg "force" remakes it over any edits.</summary>
        public static void MakeMorningShot()
        {
            if (Application.isPlaying) { Debug.Log("[Nainital] morning: leave Play first"); return; }
            string arg;
            try { arg = File.ReadAllText(Path.Combine(Root, "Bridge/arg.txt")).Trim().ToLowerInvariant(); } catch { arg = ""; }
            const string folder = "Assets/Nainital/Cinematics", path = folder + "/MorningShot.playable";
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Timeline.TimelineAsset>(path) && arg != "force")
            { Debug.Log("[Nainital] morning: " + path + " is there already (arg force remakes it)"); return; }
            if (!UnityEditor.AssetDatabase.IsValidFolder(folder)) UnityEditor.AssetDatabase.CreateFolder("Assets/Nainital", "Cinematics");
            var cam = Camera.main;
            if (!cam) { Debug.LogWarning("[Nainital] morning: no Main Camera"); return; }
            var dn = UnityEngine.Object.FindAnyObjectByType<DayNightCycle>();
            float water = dn ? dn.valleyHeight : 746.4f;
            Vector3 peak = PlaceAt("Naini Peak (China Peak)", new Vector3(-1340.49f, 1414.53f, 1478.78f));
            // a point well out on the water in the lake's south-east half, looking up the lake: 6 m of water under it and
            // water all round for 35 m (the first try sat by the shore: a dark bank filled the left of the frame)
            float Bed(Vector3 q) => StoryWalker.Ground(q + Vector3.up * 300f).y;
            bool Open(Vector3 q)
            {
                if (Bed(q) > water - 6f) return false;
                for (int k = 0; k < 12; k++) if (Bed(q + Quaternion.Euler(0f, k * 30f, 0f) * Vector3.forward * 35f) > water - 0.5f) return false;
                return true;
            }
            Vector3 s = new Vector3(549f, water, -757f), mid = new Vector3(420f, water, -560f);
            for (int i = 0; i < 60 && !Open(s); i++) s = Vector3.MoveTowards(s, mid + (mid - s).normalized * 300f, 8f);
            float bed = StoryWalker.Ground(s + Vector3.up * 300f).y;
            Vector3 flat = peak - s; flat.y = 0f;
            Vector3 d = flat.normalized;
            float dist = flat.magnitude, yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float elevEnd = Mathf.Atan2(peak.y - (water + 42f), dist - 40f) * Mathf.Rad2Deg;
            // (time, height over the water, metres up the lake, pitch: + looks down, yaw drift, field of view)
            var keys = new (float t, float h, float f, float pitch, float yawOff, float fov)[]
            {
                (0f, 1.0f, 0f, 20f, -2f, 52f),
                (3.5f, 3.5f, 7f, 15f, -1f, 50f),
                (7f, 13f, 17f, 3f, 0f, 48f),
                (10.5f, 29f, 29f, -elevEnd - 1.5f, 0.5f, 46f),
                (14f, 42f, 40f, -elevEnd - 4f, 1f, 44f),
            };
            var clip = new AnimationClip { name = "Lake to Naina Peak", frameRate = 60f };
            string[] props = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z", "localEulerAnglesRaw.x", "localEulerAnglesRaw.y", "localEulerAnglesRaw.z" };
            var cs = new AnimationCurve[7];
            for (int k = 0; k < 7; k++) cs[k] = new AnimationCurve();
            foreach (var k in keys)
            {
                Vector3 pos = s + Vector3.up * k.h + d * k.f;
                cs[0].AddKey(k.t, pos.x); cs[1].AddKey(k.t, pos.y); cs[2].AddKey(k.t, pos.z);
                cs[3].AddKey(k.t, k.pitch); cs[4].AddKey(k.t, yaw + k.yawOff); cs[5].AddKey(k.t, 0f);
                cs[6].AddKey(k.t, k.fov);
            }
            foreach (var c in cs)
                for (int i = 0; i < c.length; i++)
                {
                    UnityEditor.AnimationUtility.SetKeyLeftTangentMode(c, i, UnityEditor.AnimationUtility.TangentMode.ClampedAuto);
                    UnityEditor.AnimationUtility.SetKeyRightTangentMode(c, i, UnityEditor.AnimationUtility.TangentMode.ClampedAuto);
                }
            for (int k = 0; k < 6; k++) UnityEditor.AnimationUtility.SetEditorCurve(clip, UnityEditor.EditorCurveBinding.FloatCurve("", typeof(Transform), props[k]), cs[k]);
            UnityEditor.AnimationUtility.SetEditorCurve(clip, UnityEditor.EditorCurveBinding.FloatCurve("", typeof(Camera), "field of view"), cs[6]);

            UnityEditor.AssetDatabase.DeleteAsset(path);
            var tl = ScriptableObject.CreateInstance<UnityEngine.Timeline.TimelineAsset>();
            UnityEditor.AssetDatabase.CreateAsset(tl, path);
            UnityEditor.AssetDatabase.AddObjectToAsset(clip, tl);
            var tr = tl.CreateTrack<UnityEngine.Timeline.AnimationTrack>(null, "Camera");
            tr.trackOffset = UnityEngine.Timeline.TrackOffset.ApplyTransformOffsets;
            tr.position = Vector3.zero; tr.eulerAngles = Vector3.zero;
            var tc = tr.CreateClip(clip);
            tc.start = 0; tc.duration = keys[keys.Length - 1].t; tc.displayName = "Lake to Naina Peak";
            if (tc.asset is UnityEngine.Timeline.AnimationPlayableAsset apa) { apa.removeStartOffset = false; apa.applyFootIK = false; }

            var go = GameObject.Find("MorningShot");
            if (!go) go = new GameObject("MorningShot");
            var pd = go.GetComponent<UnityEngine.Playables.PlayableDirector>();
            if (!pd) pd = go.AddComponent<UnityEngine.Playables.PlayableDirector>();
            pd.playableAsset = tl;
            pd.playOnAwake = false;
            pd.extrapolationMode = UnityEngine.Playables.DirectorWrapMode.None;
            var anim = cam.GetComponent<Animator>();
            if (!anim) anim = cam.gameObject.AddComponent<Animator>();
            pd.SetGenericBinding(tr, anim);
            UnityEditor.EditorUtility.SetDirty(tl);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(go.scene);
            Debug.Log($"[Nainital] morning: {path} - start {s:F0} (water {water:F1}, lake bed {bed:F1}), to Naina Peak {peak:F0} {dist:F0} m away, yaw {yaw:F1}, end pitch {-elevEnd - 8f:F1}");
        }

        public static void Stills() => Start(false);
        public static void Record() => Start(true);
        public static void State() => Debug.Log("[Nainital] cinema: busy=" + CinemaRunner.Busy + " last: " + CinemaRunner.Progress);

        /// <summary>The camera's cuts in a timeline: two keys under 0.05 s apart with a jump of over 2 m or 15 degrees between
        /// them (a recorded / keyed camera path, the owner's way of making shots). Returns 0, the cuts, the end.</summary>
        static float[] ShotBounds(UnityEngine.Playables.PlayableDirector pd)
        {
            var cuts = new List<float> { 0f };
            float dur = (float)pd.duration;
            if (pd.playableAsset is UnityEngine.Timeline.TimelineAsset tl)
                foreach (var tr in tl.GetOutputTracks())
                {
                    if (!(tr is UnityEngine.Timeline.AnimationTrack at)) continue;
                    var bound = pd.GetGenericBinding(at) as Component;
                    if (bound && !bound.GetComponent<Camera>()) continue;   // the camera's track only
                    var clips = new List<(AnimationClip c, double off)>();
                    if (at.infiniteClip) clips.Add((at.infiniteClip, 0));
                    foreach (var tc in at.GetClips()) if (tc.animationClip) clips.Add((tc.animationClip, tc.start - tc.clipIn));
                    foreach (var (clip, off) in clips)
                    {
                        AnimationCurve C(string prop)
                        {
                            foreach (var b in UnityEditor.AnimationUtility.GetCurveBindings(clip))
                                if (b.path == "" && b.propertyName == prop) return UnityEditor.AnimationUtility.GetEditorCurve(clip, b);
                            return null;
                        }
                        var px = C("m_LocalPosition.x"); var py = C("m_LocalPosition.y"); var pz = C("m_LocalPosition.z");
                        var rx = C("localEulerAnglesRaw.x"); var ry = C("localEulerAnglesRaw.y");
                        if (px == null || py == null || pz == null) continue;
                        var keys = px.keys;
                        for (int i = 0; i + 1 < keys.Length; i++)
                        {
                            float a = keys[i].time, b = keys[i + 1].time;
                            if (b - a > 0.05f) continue;
                            var pa = new Vector3(px.Evaluate(a), py.Evaluate(a), pz.Evaluate(a));
                            var pb = new Vector3(px.Evaluate(b), py.Evaluate(b), pz.Evaluate(b));
                            float rot = rx != null && ry != null ? Mathf.Max(Mathf.Abs(Mathf.DeltaAngle(rx.Evaluate(a), rx.Evaluate(b))), Mathf.Abs(Mathf.DeltaAngle(ry.Evaluate(a), ry.Evaluate(b)))) : 0f;
                            if ((pb - pa).magnitude > 2f || rot > 15f) cuts.Add((float)(b + off));
                        }
                    }
                }
            var list = cuts.Where(t => t >= 0f && t < dur - 0.05f).Distinct().OrderBy(t => t).ToList();
            list.Add(dur);
            return list.ToArray();
        }

        /// <summary>ffmpeg: on the PATH, else the winget package (Gyan.FFmpeg).</summary>
        public static string FFmpeg()
        {
            foreach (var d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
                try { var f = Path.Combine(d.Trim(), "ffmpeg.exe"); if (File.Exists(f)) return f; } catch { }
            string pk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages");
            if (Directory.Exists(pk))
                foreach (var d in Directory.GetDirectories(pk, "Gyan.FFmpeg*"))
                {
                    var f = Directory.GetFiles(d, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();
                    if (f != null) return f;
                }
            return "ffmpeg";
        }

        /// <summary>The clips joined: the day ones, then the night ones, a short fade between each.</summary>
        public static void Reel()
        {
            string dir = Path.Combine(Root, "Cinematics");
            var clips = Directory.GetFiles(dir, "*.mp4").Where(f => !Path.GetFileName(f).StartsWith("NainiTA")).OrderBy(f => f).ToList();
            if (clips.Count == 0) { Debug.Log("[Nainital] cinema: no clips"); return; }
            string list = Path.Combine(dir, "reel_list.txt");
            File.WriteAllLines(list, clips.Select(c => "file '" + c.Replace("\\", "/") + "'"));
            string outFile = Path.Combine(dir, "NainiTA_showreel_4k.mp4");
            var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(FFmpeg(),
                $"-y -loglevel error -f concat -safe 0 -i \"{list}\" -c copy -movflags +faststart \"{outFile}\"") { UseShellExecute = false, CreateNoWindow = true });
            p.WaitForExit();
            Debug.Log($"[Nainital] cinema: reel {outFile} ({clips.Count} clips), ffmpeg exit {p.ExitCode}");
        }
    }
}
