using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// A story scene, written as data (owner, 23 Nov 2026: "cutscene, dialogue and choice UI system bnaalo proper"; the
    /// missions: docs/Nainital_Story_Mission_Bible.docx). A script is JSON in Resources/Story/&lt;id&gt;.json (or built in
    /// code): its actors (a character of the library by id, or picked by man / woman / elder / child), then its steps, run
    /// one after another. While it runs the player's input, the HUD and the game camera are put aside; the story UI shows
    /// (StoryUI); at the end everything comes back. Steps (field "op"):
    ///   fade v t | bars v | blur v t | wait t | objective key en t | toast key en
    ///   cam pos look fov t of ease  - a shot; with "of" (an actor or "player") pos / look are in its own frame (x right,
    ///                                  y up, z ahead; look defaults to its face); t > 0 glides there from the shot before
    ///   orbit of r h a b t fov      - round an actor from angle a to b (degrees from its front), r metres out, h up
    ///   follow of pos look t        - the camera rides with an actor (walking) for t seconds
    ///   say who key en t voice anim - a line (who = an actor, "player" or "" for a narrator); t > 0 moves on by itself
    ///   choice flag key en opts t def - asks (opts: key en v go); the answer's v (or its index) goes into flag, its go jumps
    ///   if flag v go | label name | goto go | set flag v | end
    ///   walk who to|at speed wait | face who at|yaw | place who pos yaw | show who | hide who | anim who anim t
    ///   mash key en per decay limit flag go name - the QTE: flag "1" passed / "0" failed (then go, if given); name = a
    ///                                  ProgressHooks entry that gets 0..1 every frame (an escape scrubbed by the presses)
    /// Story missions (26 Nov 2026, docs/STORY.md):
    ///   call name wait             - a set piece in code (Hooks[name], Chapter0.cs ...); wait false: it runs on beside
    ///   sit who at v | unsit who   - an actor into seat v of a vehicle prop (VehicleParts.Sit) / out of it, standing
    ///   look t r h                 - the viewer looks round himself for t s (mouse / stick / drag), r deg left-right,
    ///                                h deg up-down round the shot's direction
    ///   shake v t                  - the camera shakes, v metres, easing out over t s
    ///   sound voice v pos at       - a clip from Resources (voice = its path), v volume; at a prop / [x,y,z]: 3D there
    ///   lie who v | knock who v    - an actor lies still (v "0" up again) / falls down (v "0" gets up)
    ///   player v pos yaw           - v "hide" / "show" the player's body, "place" him at pos facing yaw
    /// Props: Props[name] = a Transform a set piece registers (a car, a stretcher); "of", "at" and "who" find them too.
    /// Text: key = the Hinglish (Loc's key, Hindi shaped from Tools/loc/strings_story.tsv), en = the English. Positions:
    /// [x, z] stand on the ground there, [x, y, z] exact (world metres). Hold Esc to skip: the shots and lines fly past
    /// up to the next choice or QTE, which still ask.
    /// </summary>
    public class Cutscene : MonoBehaviour
    {
        // ------------------------------------------------------------------ the script's data
        [Serializable] public class Opt { public string key, en, v, go; }

        [Serializable]
        public class ActorDef
        {
            public string name;        // what the steps call it ("uncle")
            public string nameKey, nameEn;   // the name shown over its lines (Hinglish / English)
            public string look;        // a CharacterLibrary id; else picked:
            public bool female, elder, child;
            public string color;       // its name's colour, "#rrggbb"
            public float[] pos;        // where it stands at the start ([x, z] / [x, y, z]); none: hidden till placed
            public float yaw;
            public bool hidden;
        }

        [Serializable]
        public class Step
        {
            public string op;
            public string who, at, of, flag, v, go, name, key, en, voice, anim, ease;
            public float t, fov, speed, r, h, a, b, per, decay, limit, yaw = float.NaN;
            public int def;
            public float[] pos, look, to;
            public bool wait = true;
            public List<Opt> opts;
        }

        [Serializable]
        public class StoryScript
        {
            public string id, title;
            public bool skippable = true;
            /// <summary>Positions are offsets from where the player stands at the start (x right, z ahead of him; [x, y, z]:
            /// y over his feet): a scene that plays wherever he is (tests, a scene that comes to him).</summary>
            public bool relative;
            public string playerKey = "Tum", playerEn = "You";
            public List<ActorDef> actors = new List<ActorDef>();
            public List<Step> steps = new List<Step>();
        }

        public static StoryScript Load(string id)
        {
            var ta = Resources.Load<TextAsset>("Story/" + id);
            if (!ta) { Debug.LogWarning("[Nainital] story: no Resources/Story/" + id + ".json"); return null; }
            try { return JsonUtility.FromJson<StoryScript>(ta.text); }
            catch (Exception e) { Debug.LogWarning("[Nainital] story " + id + ": " + e.Message); return null; }
        }

        // ------------------------------------------------------------------ running one
        public static Cutscene Current { get; private set; }
        public static bool Playing => Current;

        /// <summary>Set pieces in code a script calls by name ("call"): the scene and the step in, a coroutine out.</summary>
        public static readonly Dictionary<string, Func<Cutscene, Step, IEnumerator>> Hooks = new Dictionary<string, Func<Cutscene, Step, IEnumerator>>();
        /// <summary>A QTE's progress (0..1 every frame) to the code that scrubs by it ("mash" name).</summary>
        public static readonly Dictionary<string, Action<float>> ProgressHooks = new Dictionary<string, Action<float>>();
        /// <summary>Named things of a set piece (vehicles, a stretcher) the steps can point the camera at or seat actors in.</summary>
        public static readonly Dictionary<string, Transform> Props = new Dictionary<string, Transform>();

        /// <summary>The hold-to-skip is on: a set piece jumps to its end state.</summary>
        public bool Skipping => skipping;
        public StoryScript Script => script;
        /// <summary>An actor's root (or a prop's) by its name in the script; null if there is none.</summary>
        public Transform ActorRoot(string who) { var a = Get(who); return a != null && a.go ? a.go.transform : null; }
        public HumanoidRig ActorRig(string who) { var a = Get(who); return a?.rig; }
        public Camera Cam => cam;
        /// <summary>The last scene that ended, and how ("done" / "skipped" / "failed").</summary>
        public static string LastId, LastEnd;

        public static Cutscene Play(string id, Action done = null)
        {
            var s = Load(id);
            return s != null ? Play(s, done) : null;
        }

        public static Cutscene Play(StoryScript s, Action done = null)
        {
            if (s == null || Current) return null;
            var go = new GameObject("Cutscene " + s.id);
            var c = go.AddComponent<Cutscene>();
            c.script = s;
            c.onDone = done;
            Current = c;
            return c;
        }

        /// <summary>Stop it now (a test, a new game): everything given back.</summary>
        public static void StopAll() { if (Current) Current.Finish("stopped"); }

        class Actor { public ActorDef def; public GameObject go; public HumanoidRig rig; public Transform head; public Coroutine walk; public float speedNow; }

        StoryScript script;
        Action onDone;
        readonly Dictionary<string, Actor> actors = new Dictionary<string, Actor>();
        readonly Dictionary<string, int> labels = new Dictionary<string, int>();
        StoryUI ui;
        Camera cam;
        ThirdPersonCamera gameCam;
        float gameFov;
        bool skipping, finished;
        int pc;
        AudioSource voiceSrc;
        Coroutine camMove;

        void Start()
        {
            ui = StoryUI.Ensure();
            cam = Camera.main;
            gameCam = ThirdPersonCamera.Instance;
            if (gameCam) gameCam.enabled = false;
            if (cam) gameFov = cam.fieldOfView;
            GameInput.StoryLock = true;
            GameInput.UiBlocking = true;
            if (HUD.Instance) HUD.Instance.SetAlpha(0f);
            voiceSrc = gameObject.AddComponent<AudioSource>();
            voiceSrc.spatialBlend = 0f;
            voiceSrc.playOnAwake = false;
            ui.SkipAllowed = script.skippable;
            ui.SkipRequested = false;
            for (int i = 0; i < script.steps.Count; i++)
                if (script.steps[i].op == "label" && !string.IsNullOrEmpty(script.steps[i].name)) labels[script.steps[i].name] = i;
            var pl = PlayerController.Instance;
            if (pl) { originPos = pl.transform.position; originRot = Quaternion.Euler(0f, pl.transform.eulerAngles.y, 0f); }
            foreach (var d in script.actors) MakeActor(d);
            Debug.Log($"[Nainital] story: {script.id} begins ({script.steps.Count} steps, {actors.Count} actors)");
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            for (pc = 0; pc < script.steps.Count && !finished; pc++)
            {
                if (ui.SkipRequested) { ui.SkipRequested = false; skipping = true; }
                var s = script.steps[pc];
                IEnumerator e = null;
                try { e = Do(s); }
                catch (Exception ex) { Debug.LogWarning($"[Nainital] story {script.id} step {pc} ({s.op}): {ex.Message}"); }
                if (e != null) yield return e;
            }
            if (!finished) Finish(skipping ? "skipped" : "done");
        }

        IEnumerator Do(Step s)
        {
            switch (s.op)
            {
                case "fade":
                    if (skipping || s.t <= 0f) ui.FadeNow(F(s.v, 1f)); else return ui.Fade(F(s.v, 1f), s.t);
                    break;
                case "bars": if (skipping) ui.BarsNow(F(s.v, 1f) > 0.5f); else ui.Bars(F(s.v, 1f) > 0.5f); break;
                case "blur": if (skipping || s.t <= 0f) ui.BlurNow(F(s.v, 1f)); else ui.Blur(F(s.v, 1f)); break;
                case "wait": return Wait(s.t);
                case "objective": ui.Objective(Text(s), s.t > 0f ? s.t : 6f); break;
                case "toast": GameEvents.ShowToast(Text(s), s.t > 0f ? s.t : 2.5f); break;
                case "cam": return Shot(s);
                case "orbit": return Orbit(s);
                case "follow": return Follow(s);
                case "say": return Say(s);
                case "choice": return Choice(s);
                case "if":
                    {
                        bool hit = string.IsNullOrEmpty(s.v) ? StoryFlags.Has(s.flag) : StoryFlags.Get(s.flag) == s.v;
                        if (hit) Jump(s.go);
                        break;
                    }
                case "goto": Jump(s.go); break;
                case "label": break;
                case "set": StoryFlags.Set(s.flag, s.v ?? "1"); break;
                case "end": Finish(skipping ? "skipped" : "done"); break;
                case "walk": return Walk(s);
                case "face": Face(s); break;
                case "place": Place(s); break;
                case "show": { var a = Get(s.who); if (a != null && a.go) a.go.SetActive(true); break; }
                case "hide": { var a = Get(s.who); if (a != null && a.go && a.def != null) a.go.SetActive(false); break; }
                case "anim": { var a = Get(s.who); if (a != null && a.rig) a.rig.PlayGesture(s.anim, s.t); break; }
                case "mash": return Mash(s);
                case "call":
                    {
                        if (string.IsNullOrEmpty(s.name) || !Hooks.TryGetValue(s.name, out var h)) { Debug.LogWarning($"[Nainital] story {script.id}: no hook '{s.name}'"); break; }
                        var e = h(this, s);
                        if (e == null) break;
                        if (s.wait) return e;
                        StartCoroutine(e);
                        break;
                    }
                case "sit": Sit(s); break;
                case "unsit": Unsit(Get(s.who)); break;
                case "look": return LookRound(s);
                case "shake": shakeAmp = Mathf.Max(shakeAmp, F(s.v, 0.15f)); shakeFade = s.t > 0f ? s.t : 0.8f; shakeLeft = shakeFade; break;
                case "sound": Sound(s); break;
                case "lie": { var a = Get(s.who); if (a != null && a.rig) { if (s.v == "0") a.rig.Revive(); else a.rig.LieDead(); } break; }
                case "knock": { var a = Get(s.who); if (a != null && a.rig) a.rig.knocked = s.v != "0"; break; }
                case "player": PlayerOp(s); break;
                default: Debug.LogWarning($"[Nainital] story {script.id}: unknown op '{s.op}' at step {pc}"); break;
            }
            return null;
        }

        static float F(string v, float fallback) => float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : fallback;
        static string Text(Step s) => Txt(s.key, s.en);
        static string Txt(string key, string en) => string.IsNullOrEmpty(key) ? Loc.T(en ?? "") : Loc.L(key, string.IsNullOrEmpty(en) ? key : en);

        void Jump(string label)
        {
            if (string.IsNullOrEmpty(label)) return;
            if (label == "end") { pc = script.steps.Count; return; }
            if (labels.TryGetValue(label, out int i)) pc = i;   // the loop's pc++ steps past the label
            else Debug.LogWarning($"[Nainital] story {script.id}: no label '{label}'");
        }

        IEnumerator Wait(float t)
        {
            for (float k = 0f; k < t && !skipping; k += Time.unscaledDeltaTime) { CheckSkip(); yield return null; }
        }

        void CheckSkip()
        {
            if (ui.SkipRequested) { ui.SkipRequested = false; skipping = true; voiceSrc.Stop(); }
        }

        // ------------------------------------------------------------------ actors
        /// <summary>A person of the character library (look id, else picked by kind) with a HumanoidRig under its root:
        /// what a scene's actors are made of, and the story's people outside a scene (StoryWalker).</summary>
        public static GameObject MakePerson(string name, string lookId, bool female, bool elder, bool child, out HumanoidRig rig)
        {
            var go = new GameObject(name) { layer = 9 };
            var cc = go.AddComponent<CharacterController>();
            var rigGo = new GameObject("Rig") { layer = 9 };
            rigGo.transform.SetParent(go.transform, false);
            rigGo.SetActive(false);
            rig = rigGo.AddComponent<HumanoidRig>();
            rig.female = female;
            rig.bodyType = child ? (female ? HumanoidRig.BodyType.Girl : HumanoidRig.BodyType.Boy)
                         : elder ? (female ? HumanoidRig.BodyType.OldWoman : HumanoidRig.BodyType.OldMan)
                         : (female ? HumanoidRig.BodyType.Woman : HumanoidRig.BodyType.Man);
            var pm = PedestrianManager.Instance;
            if (pm) rig.material = pm.material;
            var lib = CharacterLibrary.Instance;
            var look = lib ? (!string.IsNullOrEmpty(lookId) ? lib.Find(lookId) : null) ?? lib.Pick(female, child, elder) : null;
            if (look != null) CharacterLibrary.Apply(look, rig, cc);
            rigGo.SetActive(true);
            cc.enabled = false;   // a story person goes where the story puts it, through nothing
            return go;
        }

        /// <summary>Seat an actor in a vehicle prop from code (a set piece), as the "sit" step does.</summary>
        public void SitActor(string who, string vehicle, int seat) =>
            Sit(new Step { op = "sit", who = who, at = vehicle, v = seat.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        public void UnsitActor(string who) { var a = Get(who); Unsit(a); seated.Remove(a); }
        /// <summary>Shake the camera from code (a set piece's hit), as the "shake" step does.</summary>
        public void Shake(float metres, float seconds) { shakeAmp = Mathf.Max(shakeAmp, metres); shakeFade = seconds > 0f ? seconds : 0.8f; shakeLeft = shakeFade; }

        void MakeActor(ActorDef d)
        {
            if (d == null || string.IsNullOrEmpty(d.name)) return;
            var go = MakePerson("Actor " + d.name, d.look, d.female, d.elder, d.child, out var rig);
            var a = new Actor { def = d, go = go, rig = rig };
            actors[d.name] = a;
            if (d.pos != null && d.pos.Length >= 2) { go.transform.SetPositionAndRotation(At(d.pos), Quaternion.Euler(0f, Yaw(d.yaw), 0f)); }
            if (d.hidden || d.pos == null || d.pos.Length < 2) go.SetActive(false);
        }

        Actor playerActor;
        Actor Get(string who)
        {
            if (string.IsNullOrEmpty(who)) return null;
            if (who == "player")
            {
                var p = PlayerController.Instance;
                if (!p) return null;
                if (playerActor == null || !playerActor.go) playerActor = new Actor { go = p.gameObject, rig = p.Rig };
                return playerActor;
            }
            if (actors.TryGetValue(who, out var a)) return a;
            if (Props.TryGetValue(who, out var t) && t)
            {
                if (!propActors.TryGetValue(who, out var pa) || pa.go != t.gameObject) propActors[who] = pa = new Actor { go = t.gameObject };
                return pa;
            }
            return null;
        }

        readonly Dictionary<string, Actor> propActors = new Dictionary<string, Actor>();

        // ------------------------------------------------------------------ seats, the viewer's look, sounds, the player
        void Sit(Step s)
        {
            var a = Get(s.who);
            var veh = Get(s.at);
            if (a == null || !a.go || veh == null || !veh.go) { Debug.LogWarning($"[Nainital] story {script.id}: sit {s.who} in {s.at}: not found"); return; }
            var vp = veh.go.GetComponentInChildren<VehicleParts>();
            if (!vp || !vp.HasSetup) { Debug.LogWarning($"[Nainital] story {script.id}: {s.at} has no seats"); return; }
            if (!a.go.activeSelf) a.go.SetActive(true);
            int seat = Mathf.Clamp(Mathf.RoundToInt(F(s.v, 0f)), 0, vp.SeatCount - 1);
            if (a.walk != null) { StopCoroutine(a.walk); a.walk = null; }
            vp.Sit(a.go.transform, a.rig, seat);
            seated.Add(a);
        }

        readonly List<Actor> seated = new List<Actor>();

        static void Unsit(Actor a)
        {
            if (a == null || !a.go) return;
            var tr = a.go.transform;
            Vector3 p = tr.position; float yaw = tr.eulerAngles.y;
            RideSway.Off(tr);
            var ik = a.go.GetComponent<HandIK>();
            if (ik) { ik.enabled = false; ik.left = ik.right = ik.footLeft = ik.footRight = null; }
            tr.SetParent(null, true);
            tr.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
            if (a.rig) { a.rig.seated = a.rig.driving = a.rig.riding = a.rig.pillion = false; a.rig.seatRoof = float.NaN; }
        }

        float shakeAmp, shakeFade, shakeLeft;
        Vector3 shakeOff, shakeBase;

        Quaternion lookOff = Quaternion.identity, rotBase = Quaternion.identity, rotPut = Quaternion.identity;

        /// <summary>The viewer turns his head round the shot's direction (applied in LateUpdate on top of wherever the
        /// shots or a ride put the camera); drifts back to the middle when the hand rests, back to it at the end.</summary>
        IEnumerator LookRound(Step s)
        {
            if (!cam || skipping) yield break;
            float t = s.t > 0f ? s.t : 6f, rx = s.r > 0f ? s.r : 70f, ry = s.h > 0f ? s.h : 30f;
            float yaw = 0f, pitch = 0f;
            for (float k = 0f; k < t; k += Time.unscaledDeltaTime)
            {
                CheckSkip();
                if (skipping) break;
                Vector2 d = Vector2.zero;
                var mouse = UnityEngine.InputSystem.Mouse.current;
                var pad = UnityEngine.InputSystem.Gamepad.current;
                var touch = UnityEngine.InputSystem.Touchscreen.current;
                if (mouse != null) d += mouse.delta.ReadValue() * 0.11f;
                if (pad != null) d += pad.rightStick.ReadValue() * 120f * Time.unscaledDeltaTime;
                if (touch != null && touch.primaryTouch.press.isPressed) d += touch.primaryTouch.delta.ReadValue() * 0.16f;
                yaw = Mathf.Clamp(yaw + d.x, -rx, rx);
                pitch = Mathf.Clamp(pitch + d.y, -ry, ry);
                if (d.sqrMagnitude < 0.0001f) { yaw = Mathf.MoveTowards(yaw, 0f, Time.unscaledDeltaTime * 6f); pitch = Mathf.MoveTowards(pitch, 0f, Time.unscaledDeltaTime * 4f); }
                lookOff = Quaternion.Euler(-pitch, yaw, 0f);
                yield return null;
            }
            Quaternion from = lookOff;
            for (float k = 0f; k < 0.6f && !skipping; k += Time.unscaledDeltaTime) { lookOff = Quaternion.Slerp(from, Quaternion.identity, UiKit.EaseInOut(k / 0.6f)); yield return null; }
            lookOff = Quaternion.identity;
        }

        void Sound(Step s)
        {
            if (skipping || string.IsNullOrEmpty(s.voice)) return;
            var clip = Resources.Load<AudioClip>(s.voice);
            if (!clip) { Debug.LogWarning($"[Nainital] story {script.id}: no sound {s.voice}"); return; }
            float vol = F(s.v, 1f) * GameSettings.Master;
            Vector3? at = null;
            var p = Get(s.at);
            if (p != null && p.go) at = p.go.transform.position;
            else if (s.pos != null && s.pos.Length >= 2) at = At(s.pos);
            if (at.HasValue) AudioSource.PlayClipAtPoint(clip, at.Value, vol);
            else
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.spatialBlend = 0f; src.playOnAwake = false; src.clip = clip; src.volume = vol; src.Play();
                Destroy(src, clip.length + 0.2f);
            }
        }

        bool playerHidden;
        void PlayerOp(Step s)
        {
            var p = PlayerController.Instance;
            if (!p) return;
            switch (s.v)
            {
                case "hide": SetPlayerShown(p, false); break;
                case "show": SetPlayerShown(p, true); break;
                case "place":
                    if (s.pos != null && s.pos.Length >= 2) SaveGame.Place(p, At(s.pos), float.IsNaN(s.yaw) ? p.transform.eulerAngles.y : Yaw(s.yaw));
                    break;
            }
        }

        void SetPlayerShown(PlayerController p, bool on)
        {
            foreach (var r in p.GetComponentsInChildren<Renderer>(true)) r.enabled = on;
            playerHidden = !on;
        }

        Transform Head(Actor a)
        {
            if (a == null || !a.go) return null;
            return a.rig ? a.rig.Head : a.go.transform;
        }

        Vector3 FacePoint(Actor a)
        {
            var h = Head(a);
            return h ? h.position : a.go.transform.position + Vector3.up * 1.55f;
        }

        static readonly int GroundMask = ~((1 << 9) | (1 << 2));
        Vector3 originPos; Quaternion originRot = Quaternion.identity;
        Vector3 At(float[] p)
        {
            if (p == null || p.Length < 2) return Vector3.zero;
            if (script.relative)
            {
                if (p.Length >= 3) return originPos + originRot * new Vector3(p[0], p[1], p[2]);
                return Ground(originPos + originRot * new Vector3(p[0], 0f, p[1]));
            }
            if (p.Length >= 3) return new Vector3(p[0], p[1], p[2]);
            return Ground(new Vector3(p[0], 0f, p[1]));
        }

        float Yaw(float y) => script.relative ? originRot.eulerAngles.y + y : y;

        static Vector3 Ground(Vector3 p)
        {
            var o = new Vector3(p.x, 3000f, p.z);
            var pl = PlayerController.Instance;
            int mask = GroundMask & (pl ? ~(1 << pl.gameObject.layer) : ~0);
            if (Physics.Raycast(o, Vector3.down, out var hit, 6000f, mask, QueryTriggerInteraction.Ignore)) return hit.point;
            var t = Terrain.activeTerrain;
            return new Vector3(p.x, t ? t.SampleHeight(p) + t.transform.position.y : p.y, p.z);
        }

        void Place(Step s)
        {
            var a = Get(s.who);
            if (a == null || !a.go) return;
            if (!a.go.activeSelf && a.def != null) a.go.SetActive(true);
            Vector3 p = s.pos != null && s.pos.Length >= 2 ? At(s.pos) : a.go.transform.position;
            Quaternion r = float.IsNaN(s.yaw) ? a.go.transform.rotation : Quaternion.Euler(0f, Yaw(s.yaw), 0f);
            Teleport(a, p, r);
        }

        static void Teleport(Actor a, Vector3 p, Quaternion r)
        {
            var cc = a.go.GetComponent<CharacterController>();
            bool was = cc && cc.enabled;
            if (cc) cc.enabled = false;
            a.go.transform.SetPositionAndRotation(p, r);
            if (cc) cc.enabled = was;
        }

        void Face(Step s)
        {
            var a = Get(s.who);
            if (a == null || !a.go) return;
            Vector3 dir;
            if (!float.IsNaN(s.yaw)) dir = Quaternion.Euler(0f, Yaw(s.yaw), 0f) * Vector3.forward;
            else
            {
                var b = Get(s.at);
                if (b == null || !b.go) return;
                dir = b.go.transform.position - a.go.transform.position;
            }
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-4f) a.go.transform.rotation = Quaternion.LookRotation(dir);
        }

        IEnumerator Walk(Step s)
        {
            var a = Get(s.who);
            if (a == null || !a.go) return null;
            if (!a.go.activeSelf && a.def != null) a.go.SetActive(true);
            Vector3 to;
            if (!string.IsNullOrEmpty(s.at)) { var b = Get(s.at); if (b == null || !b.go) return null; to = b.go.transform.position; }
            else if (s.to != null && s.to.Length >= 2) to = At(s.to);
            else return null;
            float speed = s.speed > 0f ? s.speed : 1.35f;
            float stopShort = !string.IsNullOrEmpty(s.at) ? 1.1f : 0f;
            if (skipping) { Arrive(a, to, stopShort); return null; }
            var walk = WalkTo(a, to, speed, stopShort);
            if (a.walk != null) StopCoroutine(a.walk);
            a.walk = StartCoroutine(walk);
            return s.wait ? WaitWalk(a) : null;
        }

        IEnumerator WaitWalk(Actor a) { while (a.walk != null && !skipping) { CheckSkip(); yield return null; } }

        void Arrive(Actor a, Vector3 to, float stopShort)
        {
            Vector3 from = a.go.transform.position, d = to - from; d.y = 0f;
            Vector3 end = d.magnitude > stopShort ? to - d.normalized * stopShort : from;
            Teleport(a, Ground(end), d.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(d) : a.go.transform.rotation);
            if (a.rig) a.rig.speed = 0f;
        }

        IEnumerator WalkTo(Actor a, Vector3 to, float speed, float stopShort)
        {
            var tr = a.go.transform;
            while (true)
            {
                if (skipping) { Arrive(a, to, stopShort); break; }
                Vector3 d = to - tr.position; d.y = 0f;
                float left = d.magnitude - stopShort;
                if (left <= 0.05f) break;
                float dt = Time.unscaledDeltaTime;
                a.speedNow = Mathf.MoveTowards(a.speedNow, speed * Mathf.Clamp01(left / 0.8f + 0.25f), dt * 3f);
                Vector3 step = d / Mathf.Max(0.001f, d.magnitude) * Mathf.Min(left, a.speedNow * dt);
                Vector3 p = Ground(tr.position + step);
                Quaternion r = Quaternion.Slerp(tr.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-8f * dt));
                if (a.def == null)
                {
                    // the player: through his controller, so his feet and animation stay his own
                    var cc = a.go.GetComponent<CharacterController>();
                    if (cc && cc.enabled) cc.Move(p - tr.position); else tr.position = p;
                    tr.rotation = r;
                }
                else tr.SetPositionAndRotation(p, r);
                if (a.rig) { a.rig.speed = a.speedNow; a.rig.grounded = true; }
                yield return null;
            }
            a.speedNow = 0f;
            if (a.rig) a.rig.speed = 0f;
            a.walk = null;
        }

        void LateUpdate()
        {
            // the library rigs read their speed every frame: keep an idle actor still
            foreach (var a in actors.Values) if (a.rig && a.walk == null) { a.rig.speed = 0f; a.rig.grounded = true; }
            // the shake: on top of wherever the shots put the camera this frame
            if (cam)
            {
                Vector3 now = cam.transform.position;
                if ((now - (shakeBase + shakeOff)).sqrMagnitude > 1e-8f) shakeBase = now;   // a shot moved it: that is the new base
                if (shakeLeft > 0f)
                {
                    shakeLeft -= Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(shakeLeft / Mathf.Max(0.01f, shakeFade)) * shakeAmp, tt = Time.unscaledTime * 31f;
                    shakeOff = new Vector3(Mathf.PerlinNoise(tt, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, tt) - 0.5f, Mathf.PerlinNoise(tt, tt * 0.5f) - 0.5f) * 2f * k;
                    if (shakeLeft <= 0f) { shakeOff = Vector3.zero; shakeAmp = 0f; }
                }
                else shakeOff = Vector3.zero;
                cam.transform.position = shakeBase + shakeOff;
                // the viewer's own look (LookRound) on top of the shot's direction
                Quaternion r = cam.transform.rotation;
                if (Quaternion.Angle(r, rotPut) > 0.01f) rotBase = r;   // a shot turned it: the new base
                rotPut = rotBase * lookOff;
                cam.transform.rotation = rotPut;
            }
        }

        // ------------------------------------------------------------------ the camera
        void Frame(Step s, float[] posArr, float[] lookArr, out Vector3 pos, out Vector3 look)
        {
            var of = Get(s.of);
            if (of != null && of.go)
            {
                var tr = of.go.transform;
                pos = posArr != null && posArr.Length >= 3 ? tr.TransformPoint(new Vector3(posArr[0], posArr[1], posArr[2])) : tr.TransformPoint(new Vector3(0.6f, 1.6f, 2.4f));
                look = lookArr != null && lookArr.Length >= 3 ? tr.TransformPoint(new Vector3(lookArr[0], lookArr[1], lookArr[2])) : FacePoint(of);
            }
            else
            {
                pos = posArr != null && posArr.Length >= 3 ? At(posArr) : cam.transform.position;
                look = lookArr != null && lookArr.Length >= 3 ? At(lookArr) : pos + cam.transform.forward;
            }
        }

        IEnumerator Shot(Step s)
        {
            if (!cam) return null;
            Frame(s, s.pos, s.look, out var pos, out var look);
            float fov = s.fov > 0f ? s.fov : cam.fieldOfView;
            if (camMove != null) { StopCoroutine(camMove); camMove = null; }
            if (s.t <= 0f || skipping) { Put(pos, look, fov); return null; }
            var move = Glide(pos, look, fov, s.t, s.ease);
            if (!s.wait) { camMove = StartCoroutine(move); return null; }
            return move;
        }

        void Put(Vector3 pos, Vector3 look, float fov)
        {
            Vector3 d = look - pos;
            cam.transform.SetPositionAndRotation(pos, d.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(d) : cam.transform.rotation);
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
        }

        IEnumerator Glide(Vector3 pos, Vector3 look, float fov, float t, string ease)
        {
            Vector3 p0 = cam.transform.position;
            Quaternion r0 = cam.transform.rotation;
            Quaternion r1 = Quaternion.LookRotation((look - pos).sqrMagnitude > 1e-6f ? look - pos : cam.transform.forward);
            float f0 = cam.fieldOfView;
            for (float k = 0f; k < t; k += Time.unscaledDeltaTime)
            {
                if (skipping) break;
                CheckSkip();
                float u = k / t;
                u = ease == "linear" ? u : ease == "out" ? UiKit.EaseOut(u) : UiKit.EaseInOut(u);
                cam.transform.SetPositionAndRotation(Vector3.Lerp(p0, pos, u), Quaternion.Slerp(r0, r1, u));
                cam.fieldOfView = Mathf.Lerp(f0, fov, u);
                yield return null;
            }
            Put(pos, look, fov);
            camMove = null;
        }

        IEnumerator Orbit(Step s)
        {
            if (camMove != null) { StopCoroutine(camMove); camMove = null; }
            var of = Get(s.of);
            if (!cam || of == null || !of.go) yield break;
            float r = s.r > 0f ? s.r : 3f, h = s.h != 0f ? s.h : 1.6f, t = s.t > 0f ? s.t : 4f;
            if (s.fov > 0f) cam.fieldOfView = s.fov;
            for (float k = 0f; k <= t; k += Time.unscaledDeltaTime)
            {
                if (skipping) break;
                CheckSkip();
                float ang = Mathf.Lerp(s.a, s.b, UiKit.EaseInOut(k / t)) * Mathf.Deg2Rad;
                var tr = of.go.transform;
                Vector3 c = tr.position;
                Vector3 p = c + tr.rotation * new Vector3(Mathf.Sin(ang) * r, h, Mathf.Cos(ang) * r);
                Put(p, FacePoint(of) - Vector3.up * 0.1f, cam.fieldOfView);
                yield return null;
            }
        }

        /// <summary>The camera rides with an actor or a prop: t seconds (wait), or, with wait false, on beside the steps until
        /// the next camera step (t 0 = no end). ease "rigid": fixed to it (inside a moving car), else a soft follow.</summary>
        IEnumerator Follow(Step s)
        {
            if (camMove != null) { StopCoroutine(camMove); camMove = null; }
            if (!s.wait) { camMove = StartCoroutine(FollowRun(s, s.t > 0f ? s.t : float.MaxValue)); return null; }
            return FollowRun(s, s.t > 0f ? s.t : 3f);
        }

        IEnumerator FollowRun(Step s, float t)
        {
            var of = Get(s.of);
            if (!cam || of == null || !of.go) yield break;
            if (s.fov > 0f) cam.fieldOfView = s.fov;
            bool rigid = s.ease == "rigid";
            Vector3 vel = Vector3.zero;
            for (float k = 0f; k < t; k += Time.unscaledDeltaTime)
            {
                if (skipping && s.wait) break;
                if (s.wait) CheckSkip();
                if (!of.go) break;
                Frame(s, s.pos ?? new[] { 0.9f, 1.7f, -3.2f }, s.look, out var pos, out var look);
                Vector3 p = rigid ? pos : Vector3.SmoothDamp(cam.transform.position, pos, ref vel, 0.25f, 50f, Time.unscaledDeltaTime);
                Put(p, look, cam.fieldOfView);
                yield return null;
            }
            if (!s.wait) camMove = null;
        }

        // ------------------------------------------------------------------ talk, choices, QTE
        string NameOf(string who, out Color color)
        {
            color = HudStyle.Marigold;
            if (string.IsNullOrEmpty(who)) return "";
            if (who == "player") { color = HudStyle.Teal; return Txt(script.playerKey, script.playerEn); }
            var a = Get(who);
            if (a?.def == null) return who;
            if (!string.IsNullOrEmpty(a.def.color) && ColorUtility.TryParseHtmlString(a.def.color, out var c)) color = c;
            return string.IsNullOrEmpty(a.def.nameKey) && string.IsNullOrEmpty(a.def.nameEn) ? a.def.name : Txt(a.def.nameKey, a.def.nameEn);
        }

        IEnumerator Say(Step s)
        {
            if (skipping) yield break;
            string name = NameOf(s.who, out var col);
            float auto = s.t;
            if (!string.IsNullOrEmpty(s.voice))
            {
                var clip = Resources.Load<AudioClip>(s.voice);
                if (clip) { voiceSrc.clip = clip; voiceSrc.volume = GameSettings.Master; voiceSrc.Play(); if (auto <= 0f) auto = clip.length + 0.35f; }
            }
            var a = Get(s.who);
            if (a != null && a.rig) a.rig.PlayGesture(string.IsNullOrEmpty(s.anim) ? "Talk" : s.anim, 0f);
            yield return ui.Say(name, Text(s), col, auto, () => { CheckSkip(); return skipping; });
            if (voiceSrc.isPlaying && auto <= 0f) voiceSrc.Stop();
            // the subtitle stays up between lines; it goes when the next step is not a line
            int next = pc + 1;
            if (next >= script.steps.Count || script.steps[next].op != "say") ui.HideTalk();
        }

        IEnumerator Choice(Step s)
        {
            skipping = false;   // a choice is always asked
            if (s.opts == null || s.opts.Count == 0) yield break;
            var texts = new List<string>();
            foreach (var o in s.opts) texts.Add(Txt(o.key, o.en));
            int got = 0;
            string prompt = string.IsNullOrEmpty(s.key) && string.IsNullOrEmpty(s.en) ? null : Text(s);
            yield return ui.Choose(texts, i => got = i, s.t, s.def, prompt);
            var pick = s.opts[Mathf.Clamp(got, 0, s.opts.Count - 1)];
            if (!string.IsNullOrEmpty(s.flag)) StoryFlags.Set(s.flag, string.IsNullOrEmpty(pick.v) ? got.ToString() : pick.v);
            Debug.Log($"[Nainital] story {script.id}: choice {s.flag} = {got} ({pick.en})");
            if (!string.IsNullOrEmpty(pick.go)) Jump(pick.go);
        }

        IEnumerator Mash(Step s)
        {
            skipping = false;
            bool ok = false;
            Action<float> prog = null;
            if (!string.IsNullOrEmpty(s.name)) ProgressHooks.TryGetValue(s.name, out prog);
            yield return ui.Mash(Text(s), r => ok = r, s.per > 0f ? s.per : 7f, s.decay > 0f ? s.decay : 14f, s.limit > 0f ? s.limit : 20f, prog);
            if (!string.IsNullOrEmpty(s.flag)) StoryFlags.Set(s.flag, ok ? "1" : "0");
            Debug.Log($"[Nainital] story {script.id}: QTE {(ok ? "passed" : "failed")}");
            if (!ok && !string.IsNullOrEmpty(s.go)) Jump(s.go);
        }

        // ------------------------------------------------------------------ the end
        void Finish(string how)
        {
            if (finished) return;
            finished = true;
            StopAllCoroutines();
            LastId = script.id;
            LastEnd = how;
            foreach (var a in seated) if (a.go) RideSway.Off(a.go.transform);
            seated.Clear();
            foreach (var a in actors.Values) if (a.go) Destroy(a.go);
            actors.Clear();
            propActors.Clear();
            if (playerHidden && PlayerController.Instance) SetPlayerShown(PlayerController.Instance, true);
            if (cam && shakeOff != Vector3.zero) cam.transform.position -= shakeOff;   // no shake left in
            shakeOff = Vector3.zero; shakeLeft = 0f;
            if (ui) { ui.Clear(); ui.FadeNow(0f); }
            if (cam) { cam.fieldOfView = gameFov > 1f ? gameFov : 60f; }
            if (gameCam) { gameCam.enabled = true; gameCam.SnapBehind(); gameCam.pitch = 12f; }   // level behind him, not at the sky
            GameInput.StoryLock = false;
            GameInput.UiBlocking = FrontEnd.MenuOpen;
            if (HUD.Instance) HUD.Instance.SetAlpha(1f);
            Debug.Log($"[Nainital] story: {script.id} ends ({how})");
            if (Current == this) Current = null;
            var cb = onDone;
            Destroy(gameObject);
            cb?.Invoke();
        }

        void OnDestroy()
        {
            if (!finished && Current == this)
            {
                // destroyed from outside (scene unload): give the controls back all the same
                GameInput.StoryLock = false;
                GameInput.UiBlocking = false;
                if (gameCam) gameCam.enabled = true;
                Current = null;
            }
        }
    }
}
