using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Makes the Chapter 0 story scenes as Unity Timelines (owner, 26 Nov 2026: "timeline pe shift krdo"; docs/STORY.md):
    /// Assets/Nainital/Scenes/Story_C0.unity holds one root per scene (PlayableDirector + StoryDirector, its Set: the
    /// StoryVehicles, StoryActors, built rooms, fire, sounds and the Story Camera), the timelines and their keyframes go to
    /// Assets/Nainital/Story/Chapter0/. The first version is drawn from the places and the motion of the code version
    /// (Chapter0's highway, the lines of Tools/story/scenes via Resources/Story/c0_*.json with their voices). After that
    /// the timelines are the owner's to edit: a scene whose timeline exists is left alone unless forced
    /// (bridge: call StoryTimelineBaker.BakeChapter0, arg.txt "force" = make every one again, "force c0_0_trip" = that one).
    /// Needs the world scene open (the ground, the roads).
    /// </summary>
    public static class StoryTimelineBaker
    {
        const string StoryScene = "Assets/Nainital/Scenes/Story_C0.unity";
        const string Dir = "Assets/Nainital/Story/Chapter0";
        static string forceArg = "";

        [MenuItem("Nainital/Story/Bake Chapter 0 timelines (new ones only)")]
        public static void BakeChapter0Menu() { forceArg = ""; Bake(); }

        public static void BakeChapter0()
        {
            try { forceArg = File.ReadAllText(Path.Combine(Application.dataPath, "../Bridge/arg.txt")).Trim(); } catch { forceArg = ""; }
            if (!forceArg.StartsWith("force")) forceArg = "";
            Bake();
        }

        static bool Forced(string id) => forceArg == "force" || forceArg == "force " + id;

        static void Bake()
        {
            var world = SceneManager.GetSceneByPath(WorldBuilder.ScenePath);
            if (!world.isLoaded) world = EditorSceneManager.OpenScene(WorldBuilder.ScenePath, OpenSceneMode.Single);
            Scene story = SceneManager.GetSceneByPath(StoryScene);
            if (!story.isLoaded)
                story = File.Exists(StoryScene) ? EditorSceneManager.OpenScene(StoryScene, OpenSceneMode.Additive)
                                                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            if (string.IsNullOrEmpty(story.path)) EditorSceneManager.SaveScene(story, StoryScene);
            if (!AssetDatabase.IsValidFolder("Assets/Nainital/Story")) AssetDatabase.CreateFolder("Assets/Nainital", "Story");
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Nainital/Story", "Chapter0");
            Physics.SyncTransforms();
            if (!Chapter0.BuildRoute()) Debug.LogWarning("[Nainital] story bake: the highway route fell back to a straight line");

            int made = 0;
            made += Run(story, "c0_0_trip", Trip);
            made += Run(story, "c0_0b_sirens", Sirens);
            made += Run(story, "c0_0c_hospital", Hospital);
            made += Run(story, "c0_1_newstart", NewStart);
            made += Run(story, "c0_2_uncle", UncleScene);
            made += Run(story, "c0_2_house", HouseScene);

            EditorSceneManager.MarkSceneDirty(story);
            EditorSceneManager.SaveScene(story);
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!list.Exists(s => s.path == StoryScene)) { list.Add(new EditorBuildSettingsScene(StoryScene, true)); EditorBuildSettings.scenes = list.ToArray(); }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Nainital] story bake: {made} scene(s) made in {StoryScene} (timelines in {Dir})");
        }

        static int Run(Scene story, string id, System.Action<Cut> body)
        {
            string tlPath = $"{Dir}/{id}.playable";
            if (File.Exists(tlPath) && !Forced(id)) { Debug.Log($"[Nainital] story bake: {id} exists - left as it is (arg 'force {id}' to make it again)"); return 0; }
            foreach (var g in story.GetRootGameObjects()) if (g.name == id) Object.DestroyImmediate(g);
            if (File.Exists(tlPath)) AssetDatabase.DeleteAsset(tlPath);
            if (AssetDatabase.IsValidFolder($"{Dir}/{id}")) AssetDatabase.DeleteAsset($"{Dir}/{id}");
            AssetDatabase.CreateFolder(Dir, id);
            var c = new Cut(story, id, tlPath);
            body(c);
            c.Finish();
            Debug.Log($"[Nainital] story bake: {id} - {c.end:F1} s");
            return 1;
        }

        // ================================================================== the builder of one scene
        class Rec
        {
            public readonly List<float> t = new List<float>();
            public readonly List<Vector3> p = new List<Vector3>();
            public readonly List<Quaternion> r = new List<Quaternion>();
            public readonly List<float> f = new List<float>();

            public void Key(float time, Vector3 pos, Quaternion rot, float fov = 50f)
            {
                if (t.Count > 0 && time <= t[t.Count - 1] + 1e-4f) time = t[t.Count - 1] + 0.002f;
                if (r.Count > 0 && Quaternion.Dot(r[r.Count - 1], rot) < 0f) rot = new Quaternion(-rot.x, -rot.y, -rot.z, -rot.w);
                t.Add(time); p.Add(pos); r.Add(rot); f.Add(fov);
            }
            public void Key(float time, Transform tr, float fov = 50f) => Key(time, tr.localPosition, tr.localRotation, fov);
            public void Hold(float time) { if (t.Count > 0 && time > t[t.Count - 1] + 0.002f) Key(time, p[p.Count - 1], r[r.Count - 1], f[f.Count - 1]); }
            /// <summary>A hard cut: the old pose held to just before `time`, the new one from it.</summary>
            public void Cut(float time, Vector3 pos, Quaternion rot, float fov = 50f) { if (t.Count > 0) Hold(time - 0.004f); Key(time, pos, rot, fov); }

            public void Eval(float time, out Vector3 pos, out Quaternion rot)
            {
                if (t.Count == 0) { pos = Vector3.zero; rot = Quaternion.identity; return; }
                if (time <= t[0]) { pos = p[0]; rot = r[0]; return; }
                for (int i = 1; i < t.Count; i++)
                    if (time <= t[i]) { float u = (time - t[i - 1]) / Mathf.Max(1e-5f, t[i] - t[i - 1]); pos = Vector3.Lerp(p[i - 1], p[i], u); rot = Quaternion.Slerp(r[i - 1], r[i], u); return; }
                pos = p[p.Count - 1]; rot = r[r.Count - 1];
            }

            public AnimationClip ToClip(string name, bool fov)
            {
                var clip = new AnimationClip { name = name, frameRate = 30f };
                var cs = new AnimationCurve[fov ? 8 : 7];
                for (int k = 0; k < cs.Length; k++) cs[k] = new AnimationCurve();
                for (int i = 0; i < t.Count; i++)
                {
                    cs[0].AddKey(t[i], p[i].x); cs[1].AddKey(t[i], p[i].y); cs[2].AddKey(t[i], p[i].z);
                    cs[3].AddKey(t[i], r[i].x); cs[4].AddKey(t[i], r[i].y); cs[5].AddKey(t[i], r[i].z); cs[6].AddKey(t[i], r[i].w);
                    if (fov) cs[7].AddKey(t[i], f[i]);
                }
                foreach (var c in cs)
                    for (int i = 0; i < c.length; i++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Linear);
                        AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Linear);
                    }
                string[] props = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z", "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
                for (int k = 0; k < 7; k++) AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), props[k]), cs[k]);
                if (fov) AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Camera), "field of view"), cs[7]);
                clip.EnsureQuaternionContinuity();
                return clip;
            }
        }

        class Cut
        {
            public readonly string id, tlPath;
            public readonly GameObject root, set;
            public readonly TimelineAsset tl;
            public readonly PlayableDirector pd;
            public readonly StoryDirector sd;
            public readonly Camera cam;
            public readonly Rec camRec = new Rec();
            readonly StoryLineTrack lines;
            readonly StoryScreenTrack screen;
            readonly StoryCueTrack cues;
            readonly ActivationTrack setTrack;
            readonly List<(GameObject go, Rec rec)> moving = new List<(GameObject, Rec)>();
            readonly Dictionary<StoryActor, StoryPoseTrack> poseTracks = new Dictionary<StoryActor, StoryPoseTrack>();
            readonly Queue<Cutscene.Step> says = new Queue<Cutscene.Step>();
            readonly Dictionary<string, Cutscene.ActorDef> names = new Dictionary<string, Cutscene.ActorDef>();
            public float end;

            public Cut(Scene story, string id, string tlPath)
            {
                this.id = id; this.tlPath = tlPath;
                root = new GameObject(id);
                SceneManager.MoveGameObjectToScene(root, story);
                set = new GameObject("Set");
                set.transform.SetParent(root.transform, false);
                tl = ScriptableObject.CreateInstance<TimelineAsset>();
                AssetDatabase.CreateAsset(tl, tlPath);
                pd = root.AddComponent<PlayableDirector>();
                pd.playableAsset = tl;
                pd.playOnAwake = false;
                pd.extrapolationMode = DirectorWrapMode.Hold;
                sd = root.AddComponent<StoryDirector>();
                sd.id = id;
                sd.set = set;
                var camGo = new GameObject("Story Camera");
                camGo.transform.SetParent(set.transform, false);
                cam = camGo.AddComponent<Camera>();
                cam.depth = 50f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 4000f; cam.fieldOfView = 50f;
                sd.storyCamera = cam;
                setTrack = tl.CreateTrack<ActivationTrack>(null, "Set (shown while it plays)");
                setTrack.postPlaybackState = ActivationTrack.PostPlaybackState.Inactive;
                pd.SetGenericBinding(setTrack, set);
                screen = tl.CreateTrack<StoryScreenTrack>(null, "Screen: fade / blur / bars");
                lines = tl.CreateTrack<StoryLineTrack>(null, "Lines");
                cues = tl.CreateTrack<StoryCueTrack>(null, "Player: press / choice / QTE / look");
                // the lines of the code version (Tools/story/scenes -> Resources/Story/<id>.json): text, names, voices
                var ta = AssetDatabase.LoadAssetAtPath<TextAsset>($"Assets/Resources/Story/{id}.json");
                if (ta)
                {
                    var sc = JsonUtility.FromJson<Cutscene.StoryScript>(ta.text);
                    foreach (var a in sc.actors) names[a.name] = a;
                    foreach (var s in sc.steps) if (s.op == "say") says.Enqueue(s);
                }
            }

            // ---------------------------------------------------------------- set pieces
            public GameObject Child(string name, Transform parent = null)
            {
                var g = new GameObject(name);
                g.transform.SetParent(parent ? parent : set.transform, false);
                return g;
            }

            public StoryVehicle Vehicle(string name, string model, CarType type, int paint, StoryVehicle.Emergency em = StoryVehicle.Emergency.None, bool needSeats = false)
            {
                var g = Child(name);
                var v = g.AddComponent<StoryVehicle>();
                v.model = model; v.type = type; v.paint = paint; v.emergency = em;
                v.Rebuild();   // (AddComponent already made a default model in OnEnable, before these were set)
                for (int i = 0; i < 8 && needSeats && (!v.Parts || !v.Parts.HasSetup || v.Parts.SeatCount < 4); i++) { v.model = ""; v.Rebuild(); }
                if (v.Parts && !string.IsNullOrEmpty(v.Parts.model)) v.model = v.Parts.model;   // this exact model from now on
                return v;
            }

            public StoryActor Actor(string name, string look, bool female = false, bool elder = false, Transform parent = null, int seat = -1)
            {
                var g = Child("Actor " + name, parent);
                var a = g.AddComponent<StoryActor>();
                a.actorName = name; a.lookId = look; a.female = female; a.elder = elder; a.seat = -1;
                // the character's own model, saved in the set: the timeline's Body track animates it (editor = game)
                var lib = CharacterLibrary.Instance;
                var e = lib ? lib.Find(look) : null;
                if (e != null && e.prefab)
                {
                    var m = (GameObject)PrefabUtility.InstantiatePrefab(e.prefab, g.transform);
                    m.name = "Model";
                    m.transform.localPosition = Vector3.zero; m.transform.localRotation = Quaternion.identity;
                    var an = m.GetComponentInChildren<Animator>();   // the prefab's Animator (with the humanoid avatar) is on a child
                    if (!an) an = m.AddComponent<Animator>();
                    an.applyRootMotion = false;
                    an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    foreach (var col in m.GetComponentsInChildren<Collider>(true)) col.enabled = false;
                }
                return a;
            }

            /// <summary>A track that keys this object's position / rotation (an Animator is added for it).</summary>
            public Rec Moves(GameObject go)
            {
                var rec = new Rec();
                moving.Add((go, rec));
                return rec;
            }

            public void Show(GameObject go, float from, float to)
            {
                var tr = tl.CreateTrack<ActivationTrack>(null, "Show " + go.name);
                tr.postPlaybackState = ActivationTrack.PostPlaybackState.LeaveAsIs;
                var c = tr.CreateDefaultClip();
                c.start = from; c.duration = Mathf.Max(0.05f, to - from);
                pd.SetGenericBinding(tr, go);
                go.SetActive(false);
            }

            AudioTrack sfx;
            GameObject sfxRoot;
            public void Sound(string res, float at, Vector3? where = null, float vol = 1f)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Resources/{res}.ogg") ?? AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Resources/{res}.mp3");
                if (!clip) { Debug.LogWarning("[Nainital] story bake: no sound " + res); return; }
                var go = Child("Sound " + Path.GetFileName(res) + " " + at.ToString("F1"), sfxRoot ? sfxRoot.transform : (sfxRoot = Child("Sounds")).transform);
                if (where.HasValue) go.transform.position = where.Value;
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false; src.volume = vol; src.spatialBlend = where.HasValue ? 1f : 0f; src.minDistance = 6f; src.maxDistance = 300f;
                var tr = tl.CreateTrack<AudioTrack>(null, "Sound " + Path.GetFileName(res));
                var c = tr.CreateClip(clip);
                c.start = at;
                pd.SetGenericBinding(tr, src);
            }

            readonly Dictionary<StoryActor, (AnimationTrack tr, TimelineClip last)> bodies = new Dictionary<StoryActor, (AnimationTrack, TimelineClip)>();

            static AnimationClip Clip(string name)
            {
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath("Assets/Nainital/Characters/Anims/" + name + ".fbx"))
                    if (o is AnimationClip ac && !ac.name.StartsWith("__preview__")) return ac;
                Debug.LogWarning("[Nainital] story bake: no animation " + name);
                return null;
            }

            /// <summary>The actor's body plays a clip from `from` to `to` (loop: repeats; else holds its last frame),
            /// blending 0.3 s out of the one before. Call in time order per actor.</summary>
            public void Anim(StoryActor a, float from, float to, string clip, bool loop = true, bool fromLastFrame = false)
            {
                var model = a.transform.Find("Model");
                var ac = Clip(clip);
                if (!model || !ac) return;
                if (!bodies.TryGetValue(a, out var b))
                {
                    var tr0 = tl.CreateTrack<AnimationTrack>(null, "Body " + a.actorName);
                    tr0.trackOffset = TrackOffset.ApplyTransformOffsets;
                    tr0.position = Vector3.zero; tr0.eulerAngles = Vector3.zero;
                    pd.SetGenericBinding(tr0, model.GetComponentInChildren<Animator>());
                    b = (tr0, null);
                }
                float start = from;
                if (b.last != null)
                {
                    start = Mathf.Max((float)b.last.start + 0.05f, from - 0.3f);
                    if (b.last.end < from) b.last.duration = from - b.last.start;
                }
                var tc = b.tr.CreateClip(ac);
                tc.start = start;
                tc.duration = Mathf.Max(0.1f, to - start);
                tc.displayName = clip;
                if (fromLastFrame) { tc.clipIn = Mathf.Max(0f, ac.length - 0.05f); tc.displayName = clip + " (held)"; }
                if (tc.asset is AnimationPlayableAsset apa)
                {
                    apa.loop = loop ? AnimationPlayableAsset.LoopMode.On : AnimationPlayableAsset.LoopMode.Off;
                    apa.removeStartOffset = true;
                    apa.applyFootIK = true;
                }
                bodies[a] = (b.tr, tc);
            }

            public void Pose(StoryActor a, float from, float to, StoryPose pose, string gesture = null)
            {
                if (!poseTracks.TryGetValue(a, out var tr)) { tr = tl.CreateTrack<StoryPoseTrack>(null, "Pose " + a.actorName); pd.SetGenericBinding(tr, a); poseTracks[a] = tr; }
                var c = tr.CreateClip<StoryPoseClip>();
                c.start = from; c.duration = Mathf.Max(0.05f, to - from);
                var pc = (StoryPoseClip)c.asset; pc.pose = pose; pc.gesture = gesture;
                c.displayName = pose + (string.IsNullOrEmpty(gesture) ? "" : " + " + gesture);
            }

            public void Screen(float from, float dur, float fade0, float fade1, float blur0, float blur1, bool bars = true)
            {
                var c = screen.CreateClip<StoryScreenClip>();
                c.start = from; c.duration = Mathf.Max(0.05f, dur);
                var s = (StoryScreenClip)c.asset;
                s.fadeFrom = fade0; s.fadeTo = fade1; s.blurFrom = blur0; s.blurTo = blur1; s.bars = bars;
                c.displayName = $"fade {fade0:0.#}>{fade1:0.#}  blur {blur0:0.#}>{blur1:0.#}" + (bars ? "" : "  no bars");
            }

            public StoryCueClip Cue(StoryCueKind kind, float from, float dur, string key = null, string en = null)
            {
                var c = cues.CreateClip<StoryCueClip>();
                c.start = from; c.duration = Mathf.Max(0.05f, dur);
                var q = (StoryCueClip)c.asset; q.kind = kind; q.key = key; q.en = en;
                c.displayName = kind + (string.IsNullOrEmpty(en) ? "" : ": " + en);
                return q;
            }

            /// <summary>The next spoken line of the code version, at `at`; returns when it ends (its voice + a breath).</summary>
            public float Say(float at, float minDur = 1.6f, float fixedDur = 0f)
            {
                if (says.Count == 0) { Debug.LogWarning($"[Nainital] story bake {id}: no line left"); return at; }
                var s = says.Dequeue();
                var c = lines.CreateClip<StoryLineClip>();
                var l = (StoryLineClip)c.asset;
                l.key = s.key; l.en = s.en; l.who = s.who == "player" ? "nikhil" : s.who;
                if (string.IsNullOrEmpty(s.who)) l.kind = StoryLineKind.Narrator;
                else if (s.who == "player" || s.who == "nikhil") { l.nameKey = "Nikhil"; l.nameEn = "Nikhil"; l.nameColor = new Color(0.37f, 0.88f, 0.82f); }
                else if (names.TryGetValue(s.who, out var a))
                {
                    l.nameKey = a.nameKey; l.nameEn = a.nameEn;
                    if (!string.IsNullOrEmpty(a.color) && ColorUtility.TryParseHtmlString(a.color, out var col)) l.nameColor = col;
                }
                if (!string.IsNullOrEmpty(s.voice)) l.voice = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Resources/{s.voice}.mp3");
                float dur = fixedDur > 0f ? fixedDur : Mathf.Max(minDur, (l.voice ? l.voice.length : s.t > 0f ? s.t : 2.5f) + 0.45f);
                if (s.t > 0f && fixedDur <= 0f && !l.voice) dur = s.t;
                c.start = at; c.duration = dur;
                c.displayName = (string.IsNullOrEmpty(l.nameEn) ? "" : l.nameEn + ": ") + l.en;
                return at + dur;
            }

            public void Objective(float at, float dur, string key, string en)
            {
                var c = lines.CreateClip<StoryLineClip>();
                var l = (StoryLineClip)c.asset; l.kind = StoryLineKind.Objective; l.key = key; l.en = en;
                c.start = at; c.duration = dur; c.displayName = "Objective: " + en;
            }

            // ---------------------------------------------------------------- the camera
            public void CamAt(float t, Vector3 pos, Vector3 look, float fov, bool cut = true)
            {
                var rot = Quaternion.LookRotation((look - pos).sqrMagnitude > 1e-6f ? look - pos : Vector3.forward);
                if (cut) camRec.Cut(t, pos, rot, fov); else camRec.Key(t, pos, rot, fov);
            }

            /// <summary>The camera held still from `from` to `to` at a pose framed on a transform (its own frame).</summary>
            public void CamStill(float from, float to, Transform of, Vector3 local, Vector3 lookLocal, float fov)
            {
                CamAt(from, of.TransformPoint(local), of.TransformPoint(lookLocal), fov);
                camRec.Hold(to);
            }

            /// <summary>The camera riding a moving object's recorded path (fixed to it), sampled every 1/15 s.</summary>
            public void CamRide(float from, float to, Rec path, Vector3 local, Vector3 lookLocal, float fov, Transform parentOfPath = null)
            {
                bool first = true;
                for (float t = from; ; t += 1f / 15f)
                {
                    float tt = Mathf.Min(t, to);
                    path.Eval(tt, out var p, out var r);
                    var m = Matrix4x4.TRS(p, r, Vector3.one);
                    if (parentOfPath) m = parentOfPath.localToWorldMatrix * m;
                    Vector3 pos = m.MultiplyPoint3x4(local), look = m.MultiplyPoint3x4(lookLocal);
                    CamAt(tt, pos, look, fov, first);
                    first = false;
                    if (tt >= to) break;
                }
            }

            /// <summary>A glide from the camera's last pose to a new one over `dur` (eased).</summary>
            public void CamGlide(float from, float dur, Vector3 pos, Vector3 look, float fov)
            {
                camRec.Hold(from);
                Vector3 p0 = camRec.p[camRec.p.Count - 1]; Quaternion r0 = camRec.r[camRec.r.Count - 1]; float f0 = camRec.f[camRec.f.Count - 1];
                Quaternion r1 = Quaternion.LookRotation(look - pos);
                for (int i = 1; i <= 12; i++)
                {
                    float u = UiKit.EaseInOut(i / 12f);
                    camRec.Key(from + dur * i / 12f, Vector3.Lerp(p0, pos, u), Quaternion.Slerp(r0, r1, u), Mathf.Lerp(f0, fov, u));
                }
            }

            /// <summary>A shake on the camera's current pose, `amp` metres easing out over `dur`.</summary>
            public void CamShake(float from, float dur, float amp)
            {
                camRec.Hold(from);
                Vector3 p0 = camRec.p[camRec.p.Count - 1]; Quaternion r0 = camRec.r[camRec.r.Count - 1]; float f0 = camRec.f[camRec.f.Count - 1];
                for (float t = 0.04f; t < dur; t += 0.04f)
                {
                    float k = (1f - t / dur) * amp;
                    camRec.Key(from + t, p0 + new Vector3(Mathf.PerlinNoise(t * 31f, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t * 31f) - 0.5f, Mathf.PerlinNoise(t * 17f, 2f) - 0.5f) * 2f * k, r0, f0);
                }
                camRec.Key(from + dur, p0, r0, f0);
            }

            // ---------------------------------------------------------------- done
            public void Finish()
            {
                camRec.Hold(end);
                AddAnim(cam.gameObject, camRec, "Story Camera", true);
                foreach (var (go, rec) in moving) { rec.Hold(end); AddAnim(go, rec, go.name, false); }
                // nothing runs past the scene's end ("show to the end" clips were made long): the timeline is end long
                foreach (var tr in tl.GetOutputTracks())
                    foreach (var tc in tr.GetClips())
                        if (tc.end > end) tc.duration = System.Math.Max(0.05, end - tc.start);
                var c = setTrack.CreateDefaultClip();
                c.start = 0; c.duration = end;
                foreach (var kv in bodies) if (kv.Value.last != null && kv.Value.last.end < end) kv.Value.last.duration = end - kv.Value.last.start;
                // the people hold their last pose to the end
                foreach (var kv in poseTracks)
                {
                    TimelineClip last = null;
                    foreach (var pc in kv.Value.GetClips()) if (last == null || pc.end > last.end) last = pc;
                    if (last != null && last.end < end) last.duration = end - last.start;
                }
                set.SetActive(false);
                EditorUtility.SetDirty(tl);
                AssetDatabase.SaveAssets();
            }

            void AddAnim(GameObject go, Rec rec, string name, bool fov)
            {
                if (rec.t.Count == 0) return;
                if (!go.GetComponent<Animator>()) go.AddComponent<Animator>();
                var clip = rec.ToClip(name, fov);
                string path = $"{Dir}/{id}/{name.Replace(':', '_').Replace('/', '_')}.anim";
                AssetDatabase.CreateAsset(clip, path);
                var tr = tl.CreateTrack<AnimationTrack>(null, "Move " + name);
                tr.trackOffset = TrackOffset.ApplyTransformOffsets;
                tr.position = Vector3.zero; tr.eulerAngles = Vector3.zero;
                var tc = tr.CreateClip(clip);
                tc.start = 0;
                tc.displayName = name;
                if (tc.asset is AnimationPlayableAsset apa) { apa.removeStartOffset = false; apa.applyFootIK = false; }
                pd.SetGenericBinding(tr, go.GetComponent<Animator>());
            }
        }

        // ================================================================== shared state between the scenes
        static Vector3 crashPos; static Quaternion crashRot;
        static string familyModel;

        static int[] Seats(VehicleParts vp)
        {
            int driver = 0, front = 1, back = 2;
            if (!vp || !vp.HasSetup) return new[] { driver, front, back };
            int n = vp.SeatCount;
            for (int i = 0; i < n; i++) if (vp.PoseOf(i) == SeatPose.Drive) { driver = i; break; }
            float frontZ = float.MinValue, backX = float.MinValue;
            for (int i = 0; i < n; i++) { if (i == driver) continue; if (vp.SeatLocal(i).z > frontZ) { frontZ = vp.SeatLocal(i).z; front = i; } }
            for (int i = 0; i < n; i++) { if (i == driver || i == front) continue; if (vp.SeatLocal(i).z < frontZ - 0.4f && vp.SeatLocal(i).x > backX) { backX = vp.SeatLocal(i).x; back = i; } }
            return new[] { driver, front, back };
        }

        /// <summary>An actor put in a seat of a vehicle (its place in the vehicle's own frame).</summary>
        static void PutInSeat(StoryActor a, StoryVehicle v, int seat)
        {
            var vp = v.Parts;
            if (!vp || !vp.HasSetup || seat >= vp.SeatCount) return;
            var sd = vp.SeatData(seat);
            Vector3 w = vp.Frame.TransformPoint(sd.pos);
            Quaternion wr = vp.Frame.rotation * Quaternion.Euler(sd.euler);
            a.transform.localPosition = v.transform.InverseTransformPoint(w);
            a.transform.localRotation = Quaternion.Inverse(v.transform.rotation) * wr;
        }

        // ================================================================== 0.0 The Last Trip
        static void Trip(Cut c)
        {
            c.sd.hour = 15.5f; c.sd.endBlack = true; c.sd.weather = WeatherSystem.Kind.Cloudy;
            var car = c.Vehicle("Family car", "", CarType.Sedan, Px.CarRed, needSeats: true);
            familyModel = car.model;
            { var ins = c.Child("Inside", car.transform).AddComponent<StorySet>(); ins.kind = StorySet.Kind.CarInside; ins.Rebuild(); }
            var other = c.Vehicle("Wrong-side SUV", "car_suv_muscular", CarType.Suv, Px.CarBlack);
            var kit = PoliceKit.Instance ? PoliceKit.Instance : Resources.Load<PoliceKit>("PoliceKit");   // (in the editor the kit is not loaded yet)
            var amb = c.Vehicle("108 Ambulance", kit ? kit.ambulanceModel : "car_van", CarType.Suv, Px.CarWhite, StoryVehicle.Emergency.Ambulance);
            var pol = c.Vehicle("Police Bolero", kit ? kit.policeCarModel : "car_suv_boxy", CarType.Suv, Px.CarWhite, StoryVehicle.Emergency.Police);
            int[] seats = Seats(car.Parts);
            var papa = c.Actor("papa", "m_man_flannel", parent: car.transform, seat: seats[0]);
            var mummy = c.Actor("mummy", "goan_didi", female: true, parent: car.transform, seat: seats[1]);
            var nikhil = c.Actor("nikhil", "m_player", parent: car.transform);
            PutInSeat(papa, car, seats[0]); PutInSeat(mummy, car, seats[1]); PutInSeat(nikhil, car, seats[2]);
            mummy.transform.localPosition += new Vector3(0f, 0.18f, 0f);
            var fire = c.Child("Engine fire", car.transform);
            fire.transform.localPosition = new Vector3(0f, 0.85f, 1.7f);
            fire.AddComponent<StoryFire>();

            float sImpact = Chapter0.ImpactS, s0 = 6f, sA = sImpact - 26f;
            var carRec = c.Moves(car.gameObject); var othRec = c.Moves(other.gameObject);
            var ambRec = c.Moves(amb.gameObject); var polRec = c.Moves(pol.gameObject);
            var nikRec = c.Moves(nikhil.gameObject);
            Vector3 seatL = nikhil.transform.localPosition; Quaternion seatR = nikhil.transform.localRotation;
            nikRec.Key(0f, seatL, seatR);

            // --- the talk: its length sets the drive's pace (the car reaches the approach as the last line ends)
            float t = 0f;
            c.Screen(0f, 2.5f, 1f, 0f, 0f, 0f);
            c.Objective(0.5f, 5f, "Haldwani - Nainital highway", "Haldwani - Nainital highway");
            // first, the timings of the shots and lines (the drive is laid along them after)
            var shots = new List<(float from, float to, int kind)>();   // kinds: 0 aerial, 1 side-follow, 2 Nikhil, 3 Papa, 4 Mummy, 5 Mummy two-shot, 6 side ext
            shots.Add((0f, 5.5f, 0)); shots.Add((5.5f, 10.5f, 1));
            t = 10.5f;
            float e;
            e = c.Say(t); shots.Add((t, e, 2)); t = e;                       // Nikhil: Nainital hi mila
            e = c.Say(t); shots.Add((t, e, 3)); t = e;                       // Papa: Sita maiya
            e = c.Say(t); shots.Add((t, e, 4)); t = e;                       // Mummy: darshan
            e = c.Say(t); shots.Add((t, e, 2)); t = e;                       // Nikhil: koi Bhagwan nahi
            e = c.Say(t); shots.Add((t, e, 5)); t = e;                       // Mummy: jis din milenge
            shots.Add((t, t + 1.4f, 2)); t += 1.4f;                          // his face
            e = c.Say(t); shots.Add((t, e, 6)); t = e;                       // Papa: pahunch ke bahas
            float tA = t;
            // --- the drive to the approach, then the approach simulated
            float vD = (sA - s0) / tA;
            for (float k = 0f; k <= tA + 1e-4f; k += 1f / 15f) { Chapter0.PlaceOnRoute(car.gameObject, s0 + vD * Mathf.Min(k, tA), Chapter0.Lane, 0f); carRec.Key(Mathf.Min(k, tA), car.transform); }
            float carS = sA, othS = sImpact + 42f, v = vD, ov = 15f, tt = 0f;
            bool braked = false;
            c.Show(other.gameObject, tA, 9999f);
            Chapter0.PlaceOnRoute(other.gameObject, othS, Chapter0.Lane, 180f);
            othRec.Key(tA, other.transform);
            c.Sound("Audio/veh/horn_car_3", tA + 0.2f, other.transform.position, 1f);
            while (othS - carS > 4.4f && tt < 12f)
            {
                float dt = 1f / 30f;
                tt += dt;
                if (tt > 0.35f && !braked) { braked = true; c.Sound("Audio/veh/skid", tA + tt); c.Sound("Audio/veh/brake_squeal", tA + tt, null, 0.8f); }
                if (braked) v = Mathf.MoveTowards(v, 3.5f, dt * 9f);
                carS += v * dt; othS -= ov * dt;
                Chapter0.PlaceOnRoute(car.gameObject, carS, Chapter0.Lane + Mathf.Clamp01(tt - 0.6f) * 0.9f, braked ? -6f * Mathf.Clamp01(tt - 0.6f) : 0f);
                Chapter0.PlaceOnRoute(other.gameObject, othS, Chapter0.Lane - Mathf.Clamp01(tt - 0.8f) * 0.6f, 180f);
                if (Mathf.Abs(tt * 15f - Mathf.Round(tt * 15f)) < 0.26f) { carRec.Key(tA + tt, car.transform); othRec.Key(tA + tt, other.transform); }
            }
            float tI = tA + tt;
            carRec.Key(tI, car.transform); othRec.Key(tI, other.transform);
            c.Say(tA + 1.5f, 1.3f, 1.3f);                                      // Papa: Arre! galat side se
            Vector3 hit = (car.transform.position + other.transform.position) * 0.5f + Vector3.up * 0.7f;
            c.Sound("Audio/crash_1", tI, hit); c.Sound("Audio/veh/hit_heavy_1", tI, hit); c.Sound("Audio/veh/glass_1", tI, hit);

            // --- the hit spins it and it rolls over onto its roof ON THE ROAD, a few metres on, across its own lane (owner,
            // 27 Nov 2026: "gaadi plta do" and "gravity bhi hoti hai... road pe hi plta do": not hanging over the edge)
            Chapter0.Sample(carS + 6f, out var rp, out var tan);
            Vector3 left = Chapter0.LeftOf(tan);
            Vector3 face = Quaternion.AngleAxis(-70f, Vector3.up) * tan; face.y = 0f; face.Normalize();
            Vector3 cen = StoryWalker.Ground(rp + left * (Chapter0.Lane * 0.6f) + Vector3.up * 30f);
            // an upright frame on the ground under it, facing where its nose points: the shots after the crash are set in it
            var F = c.Child("After the crash (frame for the shots)").transform;
            F.SetPositionAndRotation(cen, Quaternion.LookRotation(face, Vector3.up));
            Quaternion upright = Quaternion.LookRotation(face, Vector3.up);
            crashRot = upright * Quaternion.Euler(-6f, 0f, 180f);      // on its roof, the nose a little down the slope
            crashPos = cen + Vector3.up * 1.55f;                         // its floor up there, the roof on the ground (the windows clear)
            Vector3 p0 = car.transform.position; Quaternion r0 = car.transform.rotation;
            Vector3 o0 = other.transform.position; Quaternion or0 = other.transform.rotation;
            // the SUV glances off, spins and stops back down the road on the hill's side, clear of the upturned car
            Vector3 oEnd = StoryWalker.Ground(o0 - left * (Chapter0.Lane * 1.6f) - tan * 7f + Vector3.up * 30f); Quaternion oRot = or0 * Quaternion.Euler(0f, 75f, 0f);
            for (int i = 1; i <= 27; i++)
            {
                float u = i / 27f, ue = UiKit.EaseOut(u);
                Quaternion yaw = Quaternion.Slerp(r0, upright, ue);
                // a roll to the left (the valley's side): +z
                carRec.Key(tI + 1.8f * u, Vector3.Lerp(p0, crashPos, ue) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 1.6f,
                           yaw * Quaternion.Euler(-6f * ue, 0f, 180f * Mathf.SmoothStep(0f, 1f, u)));
                float u2 = UiKit.EaseOut(Mathf.Min(1f, u * 1.2f));
                othRec.Key(tI + 1.8f * u, Vector3.Lerp(o0, oEnd, u2), Quaternion.Slerp(or0, oRot, u2));
            }
            car.transform.SetPositionAndRotation(crashPos, crashRot);
            c.Sound("Audio/veh/hit_heavy_2", tI + 0.8f, crashPos); c.Sound("Audio/veh/glass_1", tI + 1.6f, crashPos);
            c.Sound("Audio/veh/hit_med_1", tI + 1.8f, crashPos, 0.9f);

            // --- the camera through the talk and the approach
            foreach (var s in shots)
            {
                switch (s.kind)
                {
                    case 0: { carRec.Eval(0f, out var cp, out var cr); var m = Matrix4x4.TRS(cp, cr, Vector3.one); c.CamAt(s.from, m.MultiplyPoint3x4(new Vector3(-30, 24, 62)), m.MultiplyPoint3x4(new Vector3(0, 1, 6)), 42f); c.camRec.Hold(s.to); break; }
                    case 1: c.CamRide(s.from, s.to, carRec, new Vector3(-6.5f, 2.2f, 7.5f), new Vector3(0, 1.0f, 0), 50f); break;
                    case 2: c.CamRide(s.from, s.to, carRec, new Vector3(1.15f, 1.22f, -0.42f), new Vector3(0.48f, 1.14f, -0.76f), 34f); break;   // Nikhil, through his window
                    case 3: c.CamRide(s.from, s.to, carRec, new Vector3(1.1f, 1.24f, 0.62f), new Vector3(0.4f, 1.16f, 0.28f), 32f); break;   // Papa, his side, close
                    case 4: c.CamRide(s.from, s.to, carRec, new Vector3(-1.1f, 1.2f, 0.5f), new Vector3(-0.4f, 1.1f, 0.08f), 32f); break;   // Mummy, her side, close
                    case 5: c.CamRide(s.from, s.to, carRec, new Vector3(-1.45f, 1.35f, 0.9f), new Vector3(0.15f, 1.05f, -0.5f), 46f); break;
                    case 6: c.CamRide(s.from, s.to, carRec, new Vector3(4.5f, 1.8f, 6f), new Vector3(0, 1.0f, 0), 48f); break;
                }
            }
            c.CamRide(tA, tI + 0.2f, carRec, new Vector3(0.12f, 1.3f, 0.55f), new Vector3(0.2f, 1.05f, 30f), 58f);
            c.CamShake(tI, 0.6f, 0.3f);
            c.Screen(tI, 0.2f, 0f, 1f, 0f, 0f);
            c.Anim(papa, 0f, tI, "driving");
            c.Anim(papa, tI, tI + 30f, "car_passenger_jostled", false);
            c.Anim(mummy, 0f, tI, "sitting_idle");
            c.Anim(mummy, tI, tI + 30f, "car_passenger_jostled", false);

            // --- after: the car on its roof, burning; his calls from inside; the QTE: out through the broken side window
            // (owner, 27 Nov 2026: "vo ldka seeshe me se bhaar aaye")
            float tP = tI + 2.2f;
            c.Show(fire, tP, 9999f);
            c.CamStill(tP, tP + 2.6f, F, new Vector3(6.5f, 2.6f, -6f), new Vector3(0f, 0.6f, 0.3f), 46f);
            c.Screen(tP, 1.6f, 1f, 0f, 0.35f, 0.35f);
            // his places (world), then in the car's frame (he is its child): inside on the roof's lining by his window
            // (the car's right side, now on the frame's left), the window, out on the ground, a little further
            Vector3 inW = F.TransformPoint(new Vector3(0.3f, 0.3f, -0.75f));
            Vector3 winW = F.TransformPoint(new Vector3(0.95f, 0.32f, -0.75f));
            Vector3 outW = StoryWalker.Ground(F.TransformPoint(new Vector3(2.3f, 0f, -0.8f)));
            Quaternion outward = Quaternion.LookRotation(F.right, Vector3.up);    // head first, out of the window, to the road's side
            Vector3 Lp(Vector3 w) => car.transform.InverseTransformPoint(w);
            Quaternion Lr(Quaternion w) => Quaternion.Inverse(crashRot) * w;
            nikRec.Key(tI + 1.0f, seatL, seatR);
            nikRec.Key(tI + 1.9f, Lp(inW), Lr(outward));                         // (the screen is black: thrown down there)
            t = tP + 2.6f;
            c.CamStill(t, t + 0.1f, F, new Vector3(2.1f, 0.4f, -0.25f), new Vector3(0.4f, 0.35f, -0.75f), 42f);   // low, through the window
            float l8 = t; e = c.Say(t); t = e;
            float l9 = t; e = c.Say(t); t = e;
            c.camRec.Hold(t);
            float tQ = t + 0.3f, q = 3f;
            c.CamStill(tQ - 0.3f, tQ + q, F, new Vector3(3.6f, 1.1f, -2.4f), new Vector3(1.0f, 0.4f, -0.75f), 40f);
            var qte = c.Cue(StoryCueKind.Qte, tQ, q, "Sheesha todke bahar niklo!", "Break out through the glass!");
            qte.per = 6f; qte.decay = 9f; qte.limit = 15f; qte.flag = "c0_qte";
            // the presses drag him out through the window (scrubbed inside the QTE clip)
            nikRec.Key(tQ, Lp(inW), Lr(outward));
            nikRec.Key(tQ + q * 0.5f, Lp(winW), Lr(outward));
            nikRec.Key(tQ + q, Lp(outW), Lr(outward));
            c.Anim(nikhil, 0f, tI, "sitting_idle");
            c.Anim(nikhil, tI, tI + 1.8f, "car_passenger_jostled", false);
            c.Anim(nikhil, tI + 1.8f, tQ + q, "death_back", false, true);          // lying, as he lands
            // out: he tries to get up and falls again by the burning car; it flares
            float tC = tQ + q;
            nikRec.Key(tC + 3.6f, Lp(outW), Lr(outward));
            c.Anim(nikhil, tC, tC + 2.3f, "getting_up", false);
            c.Anim(nikhil, tC + 2.3f, tC + 3.6f, "knocked_down", false);
            c.Anim(nikhil, tC + 3.6f, tC + 4f, "death_back", false, true);
            c.CamStill(tC, tC + 3.6f, F, new Vector3(6.2f, 1.1f, 1.6f), new Vector3(2.3f, 0.7f, -0.8f), 46f);
            c.Sound("Audio/explode_1", tC + 2.4f, crashPos);
            c.CamShake(tC + 2.4f, 0.9f, 0.18f);
            // lying there: the 108 and the police come down from Nainital, sirens
            float tR = tC + 3.9f;
            Vector3 roadDir = tan;   // up the road, where the 108 and the police come from
            var down = Matrix4x4.TRS(outW, Quaternion.LookRotation(roadDir, Vector3.up), Vector3.one);
            c.CamAt(tR, down.MultiplyPoint3x4(new Vector3(0.2f, 0.45f, 0.9f)), down.MultiplyPoint3x4(new Vector3(0, 3f, 12f)), 60f);
            c.Screen(tR, 1f, 0f, 0f, 0.35f, 0.6f);
            float aStop = sImpact + 13f, pStop = sImpact + 24f, aS = aStop + 150f, pS = pStop + 165f, tk = 0f;
            c.Show(amb.gameObject, tR, 9999f); c.Show(pol.gameObject, tR, 9999f);
            while ((aS > aStop + 0.05f || pS > pStop + 0.05f) && tk < 30f)
            {
                float dt = 1f / 15f;
                float av = Mathf.Lerp(1.5f, 14f, Mathf.Clamp01((aS - aStop) / 30f)), pv = Mathf.Lerp(1.5f, 14f, Mathf.Clamp01((pS - pStop) / 30f));
                aS = Mathf.Max(aStop, aS - av * dt); pS = Mathf.Max(pStop, pS - pv * dt);
                Chapter0.PlaceOnRoute(amb.gameObject, aS, -Chapter0.Lane, 180f); Chapter0.PlaceOnRoute(pol.gameObject, pS, -Chapter0.Lane, 180f);
                ambRec.Key(tR + tk, amb.transform); polRec.Key(tR + tk, pol.transform);
                tk += dt;
            }
            c.camRec.Hold(tR + tk);
            e = c.Say(tR + tk * 0.55f, 2.5f);                                  // Mummy...
            float tEnd = Mathf.Max(e, tR + tk) + 0.3f;
            c.Screen(tEnd, 2.5f, 0f, 1f, 0.6f, 1f);
            c.end = tEnd + 2.6f;
            // the police / 108 stay where they stopped, for the next scene's start
            ambRec.Hold(c.end); polRec.Hold(c.end);
        }

        // ================================================================== 0.0b Sirens
        static void Sirens(Cut c)
        {
            c.sd.hour = 15.6f; c.sd.endBlack = true; c.sd.weather = WeatherSystem.Kind.Cloudy;
            if (crashPos == Vector3.zero) { var tr = SceneManager.GetSceneByPath(StoryScene); foreach (var g in tr.GetRootGameObjects()) if (g.name == "c0_0_trip") { var cr = g.transform.Find("Set/Family car"); if (cr) { crashPos = cr.position; crashRot = cr.rotation; } } }
            var car = c.Vehicle("Family car (crashed)", familyModel ?? "", CarType.Sedan, Px.CarRed);
            { var ins = c.Child("Inside", car.transform).AddComponent<StorySet>(); ins.kind = StorySet.Kind.CarInside; ins.Rebuild(); }
            car.transform.SetPositionAndRotation(crashPos != Vector3.zero ? crashPos : StoryWalker.Ground(Chapter0.Bend), crashRot.w == 0f && crashRot.x == 0f ? Quaternion.identity : crashRot);
            var fire = c.Child("Engine fire", car.transform);
            fire.transform.localPosition = new Vector3(0f, 0.85f, 1.7f);
            fire.AddComponent<StoryFire>().grow = 1f;
            var kit = PoliceKit.Instance ? PoliceKit.Instance : Resources.Load<PoliceKit>("PoliceKit");   // (in the editor the kit is not loaded yet)
            var pol = c.Vehicle("Police Bolero", kit ? kit.policeCarModel : "car_suv_boxy", CarType.Suv, Px.CarWhite, StoryVehicle.Emergency.Police);
            Chapter0.PlaceOnRoute(pol.gameObject, Chapter0.ImpactS + 24f, -Chapter0.Lane, 180f);
            var amb = c.Vehicle("108 Ambulance", kit ? kit.ambulanceModel : "car_van", CarType.Suv, Px.CarWhite, StoryVehicle.Emergency.Ambulance);
            var inside = c.Child("Inside", amb.transform);
            { var ins = inside.AddComponent<StorySet>(); ins.kind = StorySet.Kind.AmbulanceInside; ins.Rebuild(); }
            var ambRec = c.Moves(amb.gameObject);

            float t = 3.7f;
            float e = c.Say(t);                                                // Maa... maa...
            var look1 = c.Cue(StoryCueKind.Look, e, 7f); t = e + 7f;
            e = c.Say(t); t = e;                                               // Papa...
            e = c.Say(t, 3.5f, 3.5f); t = e;                                   // (he sobs)
            c.Cue(StoryCueKind.Look, t, 5f); t += 5f;
            c.Screen(0f, 2.5f, 1f, 0f, 0.75f, 0.75f);
            c.Screen(t, 1f, 0f, 0f, 0.75f, 1f);
            c.Screen(t + 1f, 3f, 0f, 1f, 1f, 1f);
            c.end = t + 4.1f;
            // the 108 away up the highway, the site falling behind through its back window
            float s = Chapter0.ImpactS + 10f, v = 0f;
            for (float k = 0f; k <= c.end + 1e-4f; k += 1f / 15f)
            {
                Chapter0.PlaceOnRoute(amb.gameObject, s, Chapter0.Lane, 0f);
                ambRec.Key(k, amb.transform);
                v = Mathf.MoveTowards(v, 11f, (1f / 15f) * 2.2f);
                s = Mathf.Min(s + v / 15f, Chapter0.RouteLength - 5f);
            }
            c.CamRide(0f, c.end, ambRec, new Vector3(0.1f, 1.38f, -1.7f), new Vector3(0f, 1.05f, -12f), 62f);   // at the back window, out to the road
        }

        // ================================================================== 0.0c Waking Up
        static void Hospital(Cut c)
        {
            c.sd.hour = 11f; c.sd.endBlack = true; c.sd.clearWeather = false;
            var room = c.Child("Hospital room");
            room.transform.position = StoryWalker.Ground(WantedSystem.Hospital) + Vector3.up * 140f;
            { var ins = room.AddComponent<StorySet>(); ins.kind = StorySet.Kind.HospitalRoom; ins.Rebuild(); }
            var nurse = c.Actor("nurse", "f_young_denim", female: true, parent: room.transform);
            nurse.transform.localPosition = new Vector3(-0.85f, 0f, -1.6f); nurse.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            var doc = c.Actor("doctor", "goan_sardar", parent: room.transform);
            doc.transform.localPosition = new Vector3(0.8f, 0f, -0.35f); doc.transform.localRotation = Quaternion.Euler(0f, 220f, 0f);
            var R = room.transform;
            Vector3 eye = new Vector3(0f, 1.0f, -2.3f);
            c.CamAt(0f, R.TransformPoint(eye), R.TransformPoint(new Vector3(0, 2.3f, 0.4f)), 55f);
            c.Screen(0f, 0.5f, 1f, 1f, 1f, 1f);
            c.Cue(StoryCueKind.Press, 0.5f, 0.1f, "Aankhein kholo", "Open your eyes");
            float t = 0.6f;
            c.Screen(t, 2.2f, 1f, 0f, 1f, 0.5f);
            c.camRec.Hold(t + 2.2f);
            c.CamGlide(t + 2.2f, 1.4f, R.TransformPoint(eye), R.TransformPoint(new Vector3(-0.85f, 1.5f, -1.6f)), 50f);
            t += 3.6f;
            c.Screen(t, 1f, 0f, 0f, 0.5f, 0f);
            float e = c.Say(t);                                                // nurse
            c.Anim(nurse, 0f, t, "f_idle"); c.Anim(nurse, t, e, "talking"); c.Anim(nurse, e, e + 1f, "f_idle");
            var ch = c.Cue(StoryCueKind.Choice, e, 0.1f);
            ch.flag = "c0_ask";
            ch.options.Add(new StoryCueClip.Option { key = "Meri mummy-papa kahan hain?", en = "Where are my mum and dad?", v = "ask" });
            t = e + 0.2f;
            e = c.Say(t); t = e;                                               // Nikhil asks
            c.CamGlide(t, 1.4f, R.TransformPoint(eye), R.TransformPoint(new Vector3(0.8f, 1.6f, -0.35f)), 46f);
            t += 1.4f;
            float d0 = t; e = c.Say(t); t = e;                                 // doctor 1
            float d1 = t; e = c.Say(t); t = e;                                 // doctor 2
            c.Anim(doc, 0f, d0, "m_idle"); c.Anim(doc, d0, d1, "talking"); c.Anim(doc, d1, e, "gesture_thoughtful_head_shake", false); c.Anim(doc, e, e + 1f, "m_idle");
            c.CamGlide(t, 2.2f, R.TransformPoint(eye), R.TransformPoint(new Vector3(0, 2.7f, -0.6f)), 55f);
            t += 0.6f;
            e = c.Say(t, 3.5f, 3.5f);                                          // (he cries)
            c.Screen(e - 1.5f, 1f, 0f, 0f, 0f, 0.6f);
            c.Screen(e, 3f, 0f, 1f, 0.6f, 0.6f);
            c.end = e + 3.1f;
        }

        // ================================================================== 0.1 A New Start
        static void NewStart(Cut c)
        {
            c.sd.hour = 9.2f;
            Vector3 at = StoryWalker.Ground(Chapter0.MallStart);
            Vector3 face = Chapter0.UncleSpot - Chapter0.MallStart; face.y = 0f;
            var nik = c.Actor("nikhil", "m_player");
            nik.transform.SetPositionAndRotation(at, Quaternion.LookRotation(face));
            var end = c.Child("Player end");
            end.transform.SetPositionAndRotation(at, nik.transform.rotation);
            c.sd.playerEnd = end.transform;
            var N = nik.transform;
            c.CamAt(0f, N.TransformPoint(new Vector3(-9f, 7f, -18f)), N.TransformPoint(new Vector3(0, 1.5f, 10f)), 50f);
            c.Screen(0f, 2.5f, 1f, 0f, 0f, 0f);
            c.Objective(1f, 5f, "Kuch din baad...", "A few days later...");
            c.camRec.Hold(2.5f);
            c.CamGlide(2.5f, 4.5f, N.TransformPoint(new Vector3(-3.5f, 2.6f, -7.5f)), N.TransformPoint(new Vector3(0, 1.6f, 4f)), 48f);
            c.CamGlide(7f, 1.6f, N.TransformPoint(new Vector3(0.6f, 1.7f, 2.0f)), N.TransformPoint(new Vector3(0, 1.58f, 0f)), 40f);
            float e = c.Say(8.8f);
            c.Anim(nik, 0f, e + 1f, "m_idle");
            c.Screen(e, 0.6f, 0f, 0f, 0f, 0f, false);
            c.end = e + 0.7f;
        }

        // ================================================================== 0.2 the lake: Uncle
        static void UncleScene(Cut c)
        {
            Vector3 lake = Chapter0.TowardLake(Chapter0.UncleSpot);
            Vector3 stand = StoryWalker.Ground(Chapter0.UncleSpot + lake * 0.6f);   // by the railing, on the road's side of it
            Vector3 along = Vector3.Cross(Vector3.up, lake);
            Vector3 nikAt = StoryWalker.Ground(stand - lake * 2.2f + along * 1.2f);
            var uncle = c.Actor("uncle", "m_old_sweater", elder: true);
            uncle.transform.SetPositionAndRotation(stand, Quaternion.LookRotation(lake));
            var nik = c.Actor("nikhil", "m_player");
            Vector3 toU = stand - nikAt; toU.y = 0f;
            nik.transform.SetPositionAndRotation(nikAt, Quaternion.LookRotation(toU));
            var end = c.Child("Player end");
            end.transform.SetPositionAndRotation(nikAt, nik.transform.rotation);
            c.sd.playerEnd = end.transform;
            var U = uncle.transform; var N = nik.transform;
            var uRec = c.Moves(uncle.gameObject);
            uRec.Key(0f, U);
            c.CamStill(0f, 2.2f, U, new Vector3(3.4f, 1.7f, 0.8f), new Vector3(-0.6f, 1.4f, -1.0f), 45f);
            c.Screen(0f, 1.2f, 1f, 0f, 0f, 0f);
            c.sd.clearWeather = false;
            float e = c.Say(2.2f);                                             // watching you
            c.Anim(uncle, 0f, 2.2f, "m_idle"); c.Anim(uncle, 2.2f, e, "talking"); c.Anim(uncle, e, e + 0.8f, "m_idle");
            c.camRec.Hold(e);
            // he turns to Nikhil
            Vector3 toN = nikAt - stand; toN.y = 0f;
            Quaternion r0 = U.rotation, r1 = Quaternion.LookRotation(toN);
            for (int i = 1; i <= 6; i++) uRec.Key(e + 0.6f * i / 6f, U.position, Quaternion.Slerp(r0, r1, UiKit.EaseInOut(i / 6f)));
            U.rotation = r1;
            float t = e + 0.2f;
            c.CamStill(t, t + 0.1f, N, new Vector3(0.55f, 1.65f, 1.9f), new Vector3(0, 1.58f, 0), 40f);
            float n0 = t; e = c.Say(t); t = e;                                 // Nikhil starts to tell
            c.Anim(nik, 0f, n0, "m_idle"); c.Anim(nik, n0, e, "talking"); c.Anim(nik, e, e + 1f, "m_idle");
            c.CamStill(t, t + 0.1f, U, new Vector3(-0.45f, 1.6f, 1.7f), new Vector3(0, 1.55f, 0), 38f);
            float u0 = t;
            e = c.Say(t); t = e; e = c.Say(t); t = e; e = c.Say(t); t = e;     // Uncle: sab pata hai / ghar / chalo
            c.Anim(uncle, u0, t - 1.6f, "talking"); c.Anim(uncle, t - 1.6f, t, "gesture_head_nod_yes", false); c.Anim(uncle, t, t + 0.5f, "m_idle");
            c.Screen(t, 0.5f, 0f, 0f, 0f, 0f, false);
            c.end = t + 0.6f;
        }

        // ================================================================== 0.2 the house: the landlord, Uncle gone
        static void HouseScene(Cut c)
        {
            Vector3 hf = Chapter0.HouseFront;
            var land = c.Actor("landlord", "m_old_kurta", elder: true);
            land.transform.SetPositionAndRotation(StoryWalker.Ground(hf + new Vector3(0f, 0f, 0.9f)), Quaternion.Euler(0f, 0f, 0f));
            var nik = c.Actor("nikhil", "m_player");
            Vector3 nAt = StoryWalker.Ground(hf + new Vector3(0.3f, 0f, 3.4f));
            nik.transform.SetPositionAndRotation(nAt, Quaternion.Euler(0f, 180f, 0f));
            var uncle = c.Actor("uncle", "m_old_sweater", elder: true);
            uncle.transform.SetPositionAndRotation(StoryWalker.Ground(nAt + new Vector3(-1.3f, 0f, 1.6f)), Quaternion.Euler(0f, 160f, 0f));
            var end = c.Child("Player end");
            end.transform.SetPositionAndRotation(nAt, Quaternion.Euler(0f, 0f, 0f));
            c.sd.playerEnd = end.transform;
            var N = nik.transform; var L = land.transform;
            var nRec = c.Moves(nik.gameObject);
            nRec.Key(0f, N);
            c.Screen(0f, 1.8f, 1f, 1f, 0f, 0f);
            c.Objective(0.2f, 3.5f, "Thodi der baad, Zoo Road...", "A little later, Zoo Road...");
            c.Screen(1.8f, 0.8f, 1f, 0f, 0f, 0f);
            c.CamStill(0f, 1.8f, N, new Vector3(0.65f, 1.75f, -1.7f), new Vector3(0, 1.5f, 3.2f), 45f);
            c.sd.clearWeather = false;
            float t = 2.6f;
            float e = c.Say(t); t = e;                                          // landlord: aa gaye
            c.Anim(land, 0f, 2.6f, "m_idle"); c.Anim(land, 2.6f, e, "waving", false);
            c.Anim(uncle, 0f, 30f, "m_idle");
            c.CamStill(t, t + 0.1f, N, new Vector3(0.5f, 1.65f, 1.9f), new Vector3(0, 1.58f, 0), 40f);
            float k0 = t; e = c.Say(t); t = e;                                  // Nikhil: kisse?
            c.Anim(nik, 0f, k0, "m_idle"); c.Anim(nik, k0, e, "talking"); c.Anim(land, k0, e, "m_idle");
            c.CamStill(t, t + 0.1f, L, new Vector3(-0.5f, 1.6f, 1.7f), new Vector3(0, 1.55f, 0), 38f);
            float l3 = t; e = c.Say(t); t = e;                                  // keys
            c.Anim(land, l3, e, "talking"); c.Anim(land, e, e + 1f, "m_idle"); c.Anim(nik, l3, e, "m_idle");
            // Uncle gone; Nikhil turns round to where he was
            c.Show(uncle.gameObject, 0f, t);
            Quaternion r0 = N.rotation, r1 = Quaternion.Euler(0f, 0f, 0f);
            for (int i = 1; i <= 6; i++) nRec.Key(t + 0.5f * i / 6f, N.position, Quaternion.Slerp(r0, r1, UiKit.EaseInOut(i / 6f)));
            N.rotation = r1;
            c.CamStill(t + 0.5f, t + 1.9f, N, new Vector3(0f, 1.65f, 0.25f), new Vector3(-1.0f, 1.4f, 5f), 50f);
            t += 1.9f;
            float q0 = t; e = c.Say(t); t = e;                                  // Uncle...? kahan
            c.Anim(nik, q0 - 1.9f, q0, "gesture_look_away_gesture", false); c.Anim(nik, q0, e, "talking");
            c.CamStill(t, t + 0.1f, N, new Vector3(0.6f, 1.7f, 1.8f), new Vector3(0, 1.58f, 0), 40f);
            e = c.Say(t); t = e;                                                // ajeeb hain
            c.Anim(nik, e - 2f, e + 1f, "gesture_thoughtful_head_shake", false);
            c.Screen(t, 0.5f, 0f, 0f, 0f, 0f, false);
            c.end = t + 0.6f;
        }
    }
}
