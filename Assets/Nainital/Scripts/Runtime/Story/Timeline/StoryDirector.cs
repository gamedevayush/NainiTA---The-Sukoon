using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace Nainital
{
    /// <summary>
    /// Plays one story scene made in Unity Timeline (owner, 26 Nov 2026: "timeline pe shift krdo"; docs/STORY.md). On the
    /// scene's root in a story scene (Story_C0.unity, loaded additively), next to its PlayableDirector. Its "set" (the
    /// vehicles, people, built rooms, the story camera) is off until it plays. While it plays: the player's controls, the
    /// HUD and the game camera are put aside, the player's body is hidden (a StoryActor stands in for Nikhil); the game
    /// camera follows the set's story camera (position, rotation, field of view); StoryLineClips show their subtitles and
    /// play their voices; at a StoryCueClip it stops for the press / choice / QTE (the QTE scrubs the timeline inside the
    /// clip) and lets the viewer look round inside a Look clip; holding Esc skips to the next stop or the end. At the end
    /// the player is put at playerEnd (if set) and everything comes back.
    /// </summary>
    [RequireComponent(typeof(PlayableDirector))]
    public class StoryDirector : MonoBehaviour
    {
        public static readonly Dictionary<string, StoryDirector> All = new Dictionary<string, StoryDirector>();
        public static StoryDirector Current { get; private set; }
        /// <summary>Where each StoryActor stood at the end of the last scene that had it (the story's code goes on from there).</summary>
        public static readonly Dictionary<string, Pose> LastPoses = new Dictionary<string, Pose>();
        /// <summary>A scene that ended black keeps the screen black till the next one starts (or this time passes).</summary>
        public static float HoldBlackUntil;
        /// <summary>A story scene plays: the rest of the world is frozen (Time.timeScale 0 - traffic, people, animals, the
        /// clock, the weather, every game loop; owner, 27 Nov 2026: "baaki world freeze krdo with all loops"). The scene
        /// runs on the real clock: its timeline, its people, wheels, light bars, fire and sounds.</summary>
        public static bool WorldFrozen { get; private set; }
        static float scaleBefore = 1f;

        static void Freeze(bool on)
        {
            if (on == WorldFrozen) return;
            WorldFrozen = on;
            if (on) { scaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f; Time.timeScale = 0f; }
            else Time.timeScale = scaleBefore;
        }

        public string id;
        [Tooltip("Everything of this scene; off till it plays (its Activation track shows it while you scrub the timeline)")]
        public GameObject set;
        [Tooltip("The camera keyed in the timeline; the game camera copies it while the scene plays")]
        public Camera storyCamera;
        [Tooltip("Where the player is put when the scene ends (empty: he stays where he was)")]
        public Transform playerEnd;
        [Tooltip("The time of day while it plays (-1: as it is)")]
        public float hour = -1f;
        [Tooltip("Set the weather while it plays (at once), to `weather`")]
        public bool clearWeather = true;
        public WeatherSystem.Kind weather = WeatherSystem.Kind.Clear;
        public bool skippable = true;
        [Tooltip("The scene ends black and the next one follows: keep it black between them")]
        public bool endBlack;

        PlayableDirector dir;
        StoryUI ui;
        Camera cam;
        ThirdPersonCamera gameCam;
        float gameFov;
        AudioSource voice;
        Action onDone;
        bool finished, skipping;
        readonly List<(double start, double end, StoryCueClip c)> stops = new List<(double, double, StoryCueClip)>();
        readonly List<(double start, double end, StoryCueClip c)> looks = new List<(double, double, StoryCueClip)>();
        float lookYaw, lookPitch;
        bool clockWasPaused;

        public PlayableDirector Director => dir ? dir : dir = GetComponent<PlayableDirector>();

        void OnEnable()
        {
            dir = GetComponent<PlayableDirector>();
            if (!string.IsNullOrEmpty(id)) All[id] = this;
            // a set saved switched on (left on while editing) stays hidden in the game till its scene plays
            if (Application.isPlaying && set && set.activeSelf && Current != this) set.SetActive(false);
        }

        void OnDisable()
        {
            if (!string.IsNullOrEmpty(id) && All.TryGetValue(id, out var d) && d == this) All.Remove(id);
        }

        public bool Play(Action done)
        {
            dir = GetComponent<PlayableDirector>();
            if (Current || Cutscene.Playing || !dir || !dir.playableAsset) return false;
            Current = this;
            onDone = done;
            finished = skipping = false;
            HoldBlackUntil = 0f;
            ui = StoryUI.Ensure();
            cam = Camera.main;
            gameCam = ThirdPersonCamera.Instance;
            if (gameCam) gameCam.enabled = false;
            if (cam) gameFov = cam.fieldOfView;
            GameInput.StoryLock = true;
            GameInput.UiBlocking = true;
            if (HUD.Instance) HUD.Instance.SetAlpha(0f);
            ui.SkipAllowed = skippable;
            ui.SkipRequested = false;
            if (!voice) { voice = gameObject.AddComponent<AudioSource>(); voice.spatialBlend = 0f; voice.playOnAwake = false; }
            var p = PlayerController.Instance;
            if (p) ShowPlayer(p, false);
            if (set)
            {
                foreach (var v in set.GetComponentsInChildren<StoryVehicle>(true)) v.Ensure();
                foreach (var a in set.GetComponentsInChildren<StoryActor>(true)) a.Ensure();
            }
            if (storyCamera) storyCamera.enabled = false;   // its pose is copied to the game camera; it does not draw itself
            var dn = DayNightCycle.Instance;
            if (dn && hour >= 0f) { dn.timeOfDay = hour; dn.Apply(); }
            if (clearWeather && WeatherSystem.Instance) WeatherSystem.Instance.SetNow(weather);
            // the clock stands still while the scene plays; the town's sounds are off (the scene has its own)
            if (dn) { clockWasPaused = dn.paused; dn.paused = true; }
            AudioManager.StoryQuiet = true;
            stops.Clear(); looks.Clear();
            if (dir.playableAsset is TimelineAsset tl)
                foreach (var tr in tl.GetOutputTracks())
                    if (tr is StoryCueTrack && !tr.muted)
                        foreach (var c in tr.GetClips())
                            if (c.asset is StoryCueClip cc) (cc.kind == StoryCueKind.Look ? looks : stops).Add((c.start, c.end, cc));
            stops.Sort((a, b) => a.start.CompareTo(b.start));
            dir.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            Freeze(true);
            dir.extrapolationMode = DirectorWrapMode.Hold;
            dir.time = 0;
            dir.Play();
            lookYaw = lookPitch = 0f;
            Debug.Log($"[Nainital] story timeline: {id} begins ({dir.duration:F1} s, {stops.Count} stops)");
            StartCoroutine(Run());
            return true;
        }

        static string Txt(string key, string en) => string.IsNullOrEmpty(key) ? Loc.T(en ?? "") : Loc.L(key, string.IsNullOrEmpty(en) ? key : en);

        IEnumerator Run()
        {
            int si = 0;
            yield return null;
            while (!finished)
            {
                if (ui.SkipRequested)
                {
                    ui.SkipRequested = false;
                    double to = si < stops.Count ? stops[si].start : dir.duration;
                    if (to > dir.time) { skipping = true; dir.time = to; dir.Evaluate(); skipping = false; }
                    voice.Stop();
                    ui.HideTalk();
                }
                if (si < stops.Count && dir.time >= stops[si].start - 1e-3)
                {
                    var q = stops[si++];
                    dir.Pause();
                    dir.time = q.start;
                    dir.Evaluate();
                    yield return Stop(q);
                    dir.time = q.end;
                    dir.Evaluate();
                    dir.Resume();
                    yield return null;
                    continue;
                }
                if (dir.time >= dir.duration - 1e-3) break;
                yield return null;
            }
            Finish("done");
        }

        IEnumerator Stop((double start, double end, StoryCueClip c) q)
        {
            var c = q.c;
            string prompt = Txt(c.key, c.en);
            switch (c.kind)
            {
                case StoryCueKind.Press:
                    yield return ui.Mash(prompt, r => { }, 100f, 0.01f, 3600f);
                    break;
                case StoryCueKind.Choice:
                    {
                        var texts = new List<string>();
                        foreach (var o in c.options) texts.Add(Txt(o.key, o.en));
                        if (texts.Count == 0) break;
                        int got = 0;
                        yield return ui.Choose(texts, i => got = i, 0f, 0, string.IsNullOrEmpty(prompt) ? null : prompt);
                        var pick = c.options[Mathf.Clamp(got, 0, c.options.Count - 1)];
                        if (!string.IsNullOrEmpty(c.flag)) StoryFlags.Set(c.flag, string.IsNullOrEmpty(pick.v) ? got.ToString() : pick.v);
                        Debug.Log($"[Nainital] story timeline {id}: choice {c.flag} = {got} ({pick.en})");
                        break;
                    }
                case StoryCueKind.Qte:
                    while (true)
                    {
                        bool ok = false;
                        double a = q.start, len = q.end - q.start;
                        yield return ui.Mash(prompt, r => ok = r, c.per, c.decay, c.limit, p => { dir.time = a + p * len; dir.Evaluate(); });
                        if (!string.IsNullOrEmpty(c.flag)) StoryFlags.Set(c.flag, ok ? "1" : "0");
                        Debug.Log($"[Nainital] story timeline {id}: QTE {(ok ? "passed" : "failed")}");
                        if (ok) break;
                        yield return ui.Fade(1f, 0.5f);
                        dir.time = a; dir.Evaluate();
                        ui.Objective(Loc.L("Phir se! Tez dabao", "Again! Press faster"), 3f);
                        yield return new WaitForSecondsRealtime(0.8f);
                        yield return ui.Fade(0f, 0.8f);
                    }
                    break;
            }
        }

        // ------------------------------------------------------------------ lines (StoryLineBehaviour calls these)
        StoryLineClip curLine;

        public void LineBegin(StoryLineClip l)
        {
            curLine = l;
            if (l.kind == StoryLineKind.Objective) return;
            if (l.voice && !skipping && ui && !ui.SkipRequested) { voice.Stop(); voice.clip = l.voice; voice.volume = GameSettings.Master; voice.Play(); }
            if (!string.IsNullOrEmpty(l.who) && set)
                foreach (var a in set.GetComponentsInChildren<StoryActor>()) if (a.actorName == l.who) { a.Gesture("Talk"); break; }
        }

        public void LineFrame(StoryLineClip l, float t, float dur)
        {
            if (!ui) return;
            string text = Txt(l.key, l.en);
            if (l.kind == StoryLineKind.Objective) { ui.Objective(text, Mathf.Max(0.05f, dur - t)); return; }
            float typeFor = Mathf.Clamp(text.Length / 52f, 0.3f, Mathf.Max(0.3f, dur * 0.6f));
            string who = l.kind == StoryLineKind.Narrator ? "" : Txt(l.nameKey, l.nameEn);
            ui.LineShow(who, text, l.nameColor, Mathf.Clamp01(t / typeFor));
        }

        public void LineEnd(StoryLineClip l)
        {
            if (curLine != l) return;
            curLine = null;
            if (ui && l.kind != StoryLineKind.Objective) ui.HideTalk();
        }

        // ------------------------------------------------------------------ the camera
        void LateUpdate()
        {
            if (Current != this || finished || !cam || !storyCamera) return;
            StoryCueClip look = null;
            foreach (var q in looks) if (dir.time >= q.start && dir.time <= q.end) { look = q.c; break; }
            if (look != null)
            {
                Vector2 d = Vector2.zero;
                var mouse = UnityEngine.InputSystem.Mouse.current;
                var pad = UnityEngine.InputSystem.Gamepad.current;
                var touch = UnityEngine.InputSystem.Touchscreen.current;
                if (mouse != null) d += mouse.delta.ReadValue() * 0.11f;
                if (pad != null) d += pad.rightStick.ReadValue() * 120f * Time.unscaledDeltaTime;
                if (touch != null && touch.primaryTouch.press.isPressed) d += touch.primaryTouch.delta.ReadValue() * 0.16f;
                lookYaw = Mathf.Clamp(lookYaw + d.x, -look.lookYaw, look.lookYaw);
                lookPitch = Mathf.Clamp(lookPitch + d.y, -look.lookPitch, look.lookPitch);
                if (d.sqrMagnitude < 0.0001f) { lookYaw = Mathf.MoveTowards(lookYaw, 0f, Time.unscaledDeltaTime * 6f); lookPitch = Mathf.MoveTowards(lookPitch, 0f, Time.unscaledDeltaTime * 4f); }
            }
            else
            {
                lookYaw = Mathf.MoveTowards(lookYaw, 0f, Time.unscaledDeltaTime * 90f);
                lookPitch = Mathf.MoveTowards(lookPitch, 0f, Time.unscaledDeltaTime * 60f);
            }
            var st = storyCamera.transform;
            cam.transform.SetPositionAndRotation(st.position, st.rotation * Quaternion.Euler(-lookPitch, lookYaw, 0f));
            cam.fieldOfView = storyCamera.fieldOfView;
            cam.nearClipPlane = 0.05f;
        }

        // ------------------------------------------------------------------ the end
        public void StopNow() { if (Current == this) Finish("stopped"); }

        void Finish(string how)
        {
            if (finished) return;
            finished = true;
            StopAllCoroutines();
            if (set)
                foreach (var a in set.GetComponentsInChildren<StoryActor>(true))
                    if (!string.IsNullOrEmpty(a.actorName)) LastPoses[a.actorName] = new Pose(a.transform.position, a.transform.rotation);
            bool hasEnd = playerEnd;
            Vector3 endPos = hasEnd ? playerEnd.position : Vector3.zero;
            float endYaw = hasEnd ? playerEnd.eulerAngles.y : 0f;
            if (dir) dir.Stop();   // the set back to its saved state
            if (set) set.SetActive(false);
            if (voice) voice.Stop();
            if (ui)
            {
                ui.Clear();
                if (endBlack && how == "done") { ui.FadeNow(1f); HoldBlackUntil = Time.unscaledTime + 3f; }
                else ui.FadeNow(0f);
            }
            if (cam) cam.fieldOfView = gameFov > 1f ? gameFov : 60f;
            var dn = DayNightCycle.Instance;
            if (dn) dn.paused = clockWasPaused;
            AudioManager.StoryQuiet = false;
            Freeze(false);
            var p = PlayerController.Instance;
            if (p)
            {
                ShowPlayer(p, true);
                if (hasEnd) SaveGame.Place(p, endPos + Vector3.up * 0.1f, endYaw);
            }
            if (gameCam) { gameCam.enabled = true; gameCam.SnapBehind(); gameCam.pitch = 12f; }
            GameInput.StoryLock = false;
            GameInput.UiBlocking = FrontEnd.MenuOpen;
            if (HUD.Instance) HUD.Instance.SetAlpha(1f);
            if (Current == this) Current = null;
            Debug.Log($"[Nainital] story timeline: {id} ends ({how})");
            var cb = onDone;
            onDone = null;
            cb?.Invoke();
        }

        static void ShowPlayer(PlayerController p, bool on)
        {
            foreach (var r in p.GetComponentsInChildren<Renderer>(true)) r.enabled = on;
        }

        void OnDestroy()
        {
            if (Current == this)
            {
                AudioManager.StoryQuiet = false;
                Freeze(false);
                GameInput.StoryLock = false;
                GameInput.UiBlocking = false;
                if (gameCam) gameCam.enabled = true;
                Current = null;
            }
        }

        // ------------------------------------------------------------------ loading the story scenes
        /// <summary>Loads a story scene (additively) if it is not loaded yet.</summary>
        public static IEnumerator LoadScene(string sceneName)
        {
            var s = SceneManager.GetSceneByName(sceneName);
            if (s.IsValid() && s.isLoaded) yield break;
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op == null) { Debug.LogWarning("[Nainital] story timeline: no scene " + sceneName + " in the build settings"); yield break; }
            while (!op.isDone) yield return null;
            yield return null;
        }
    }
}
