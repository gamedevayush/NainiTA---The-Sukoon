using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Bridge / menu helpers for the story system (Runtime/Story): play a scene in Play mode, press its keys for a test
    /// (the next line, a choice, a QTE press), stop it, report where it is. Bridge: "call StoryTest.Play" (arg.txt = the
    /// script id, default demo_uncle), "call StoryTest.Next", "call StoryTest.Pick" (arg.txt = 1..4), "call StoryTest.State".
    /// </summary>
    public static class StoryTest
    {
        static string Arg() { try { return File.ReadAllText(Path.Combine(Application.dataPath, "../Bridge/arg.txt")).Trim(); } catch { return ""; } }

        [MenuItem("Nainital/Story/Play test scene (demo_uncle)")]
        public static void PlayDemo() => Play("demo_uncle");

        public static void Play() { string id = Arg(); Play(string.IsNullOrEmpty(id) ? "demo_uncle" : id); }

        static void Play(string id)
        {
            if (!Application.isPlaying) { Debug.Log("[Nainital] story test: not in Play mode"); return; }
            Cutscene.StopAll();
            var c = Cutscene.Play(id);
            Debug.Log(c ? "[Nainital] story test: playing " + id : "[Nainital] story test: could not play " + id);
        }

        public static void Stop() { Cutscene.StopAll(); if (StoryDirector.Current) StoryDirector.Current.StopNow(); Debug.Log("[Nainital] story test: stopped"); }

        /// <summary>Play mode: play the story scene selected in the Hierarchy (a root of Story_C0 with a StoryDirector), as
        /// the game would - for the owner trying his Timeline edits (26 Nov 2026).</summary>
        [MenuItem("Nainital/Story/Play selected timeline scene (Play mode)")]
        public static void PlaySelected()
        {
            if (!Application.isPlaying) { EditorUtility.DisplayDialog("Story", "Pehle Play dabao, phir scene select karke ye menu.", "OK"); return; }
            var go = Selection.activeGameObject;
            var d = go ? go.GetComponentInParent<StoryDirector>() : null;
            if (!d) { EditorUtility.DisplayDialog("Story", "Hierarchy me Story_C0 ka koi scene (jaise c0_0_trip) select karo.", "OK"); return; }
            var mm = MissionManager.Instance;
            if (mm && mm.Active != null) mm.Abort();
            if (StoryDirector.Current) StoryDirector.Current.StopNow();
            Debug.Log(d.Play(null) ? "[Nainital] story test: playing " + d.id : "[Nainital] story test: could not play " + d.id);
        }

        [MenuItem("Nainital/Story/Stop the story scene (Play mode)")]
        public static void StopMenu() => Stop();

        /// <summary>Begin a story mission by id (arg.txt: s0_0, s0_0b ...; optional " step" after it), whatever is running.</summary>
        public static void Mission()
        {
            if (!Application.isPlaying) { Debug.Log("[Nainital] story test: not in Play mode"); return; }
            var mm = MissionManager.Instance;
            if (!mm) { Debug.Log("[Nainital] story test: no MissionManager"); return; }
            string[] a = Arg().Split(' ');
            string id = a[0];
            int step = a.Length > 1 && int.TryParse(a[1], out int k) ? k : 0;
            if (mm.Active != null) mm.Abort();
            Cutscene.StopAll();
            foreach (var m in mm.missions)
                if (m.id == id) { mm.Begin(m, step); Debug.Log($"[Nainital] story test: mission {id} from step {step}"); return; }
            Debug.Log("[Nainital] story test: no mission " + id + " (" + mm.missions.Count + " missions)");
        }

        /// <summary>Where the story is: the mission and its step, the scene playing, the UI's state.</summary>
        public static void MissionState()
        {
            var mm = MissionManager.Instance;
            var ui = StoryUI.Instance;
            var p = PlayerController.Instance;
            string act = mm && mm.Active != null ? $"{mm.Active.id} step '{mm.Step?.text}' ({mm.Step?.GetType().Name}) target {mm.CurrentTarget}" : "none";
            Debug.Log($"[Nainital] story mission: {act}; next {(StoryMissions.Next != null ? StoryMissions.Next.id : "-")}; scene {(Cutscene.Current ? Cutscene.Current.Script.id : "-")} " +
                      $"talking={(ui && ui.Talking)} choosing={(ui && ui.Choosing)} last={Cutscene.LastId}/{Cutscene.LastEnd} qte={StoryFlags.Get("c0_qte")} " +
                      $"player {(p ? p.transform.position.ToString("F1") : "-")} cam {(Camera.main ? Camera.main.transform.position.ToString("F1") : "-")}");
        }

        /// <summary>The playing scene's actors (arg.txt: names, comma separated; and a prop to measure against, after a
        /// "|"): each head's place in its own root's frame and in the prop's (to frame the camera shots).</summary>
        public static void Heads()
        {
            var c = Cutscene.Current;
            if (!c) { Debug.Log("[Nainital] heads: no scene"); return; }
            string[] a = Arg().Split('|');
            var prop = a.Length > 1 ? c.ActorRoot(a[1].Trim()) : null;
            foreach (var who in a[0].Split(','))
            {
                var root = c.ActorRoot(who.Trim()); var rig = c.ActorRig(who.Trim());
                if (!root || !rig || !rig.Head) { Debug.Log($"[Nainital] heads: {who}: none"); continue; }
                Debug.Log($"[Nainital] heads: {who} root {root.position:F2} head {root.InverseTransformPoint(rig.Head.position):F2} of root" +
                          (prop ? $", {prop.InverseTransformPoint(rig.Head.position):F2} of {a[1]}; root {prop.InverseTransformPoint(root.position):F2} of {a[1]}" : ""));
            }
        }

        /// <summary>Tutorial inputs for a test (0.1): hold sprint (arg "1"/"0"), a look swipe, the phone open / shut.</summary>
        public static void Sprint() { GameInput.TouchSprint = Arg() != "0"; GameInput.TouchMove = GameInput.TouchSprint ? new Vector2(0f, 1f) : Vector2.zero; }
        public static void LookSwipe() { GameInput.TouchLookPixels = new Vector2(900f, 60f); }
        public static void PhoneToggle() { Phone.Toggle(); }
        /// <summary>The game's language for a test shot (arg "hi" / "en"; the save snapshot of PlayTest puts it back).</summary>
        public static void Lang() { Loc.Set(Arg() == "hi" ? Loc.Lang.Hindi : Loc.Lang.English); Debug.Log("[Nainital] story test: language " + Loc.Current); }
        /// <summary>Uncle's walk faster (arg: the factor) and the player put 4 m behind him (a follow test).</summary>
        public static void FastWalk() { float.TryParse(Arg(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float k); StoryWalker.TestSpeedScale = k > 0f ? k : 1f; }
        public static void BehindUncle()
        {
            var w = Object.FindAnyObjectByType<StoryWalker>();
            var p = PlayerController.Instance;
            if (!w || !p) { Debug.Log("[Nainital] story test: no walker / player"); return; }
            Vector3 back = w.transform.position - w.transform.forward * 4f;
            SaveGame.Place(p, StoryWalker.Ground(back) + Vector3.up * 0.1f, w.transform.eulerAngles.y);
            Debug.Log($"[Nainital] story test: player behind Uncle at {back:F1}, arrived={w.Arrived} waiting={w.Waiting}");
        }

        /// <summary>Forget the story's progress (missions s0_* not done) for a clean test run.</summary>
        public static void ResetStory()
        {
            var mm = MissionManager.Instance;
            if (mm && mm.Active != null) mm.Abort();
            Cutscene.StopAll();
            int n = 0;
            if (mm) foreach (var m in mm.missions) if (GameState.IsDone(m.id)) { GameState.UnmarkDone(m.id); n++; }
            StoryFlags.ResetAll();
            Debug.Log($"[Nainital] story test: {n} missions forgotten");
        }
        public static void Next() { StoryUI.TestAdvance = true; }
        public static void Pick() { int.TryParse(Arg(), out int i); StoryUI.TestPick = Mathf.Max(0, i - 1); }
        public static void Mash() { StoryUI.TestPresses = 40; }
        public static void Skip() { if (StoryUI.Instance) StoryUI.Instance.SkipRequested = true; }

        public static void State()
        {
            var ui = StoryUI.Instance;
            Debug.Log($"[Nainital] story state: playing={Cutscene.Playing} talking={(ui && ui.Talking)} choosing={(ui && ui.Choosing)} " +
                      $"last={Cutscene.LastId}/{Cutscene.LastEnd} reply={StoryFlags.Get("demo_reply")} qte={StoryFlags.Get("demo_qte")}");
        }

        public static void Arcade()
        {
            string g = Arg();
            var game = g == "dash" ? ArcadeScreen.Game.Dash : g == "pool" ? ArcadeScreen.Game.Pool : ArcadeScreen.Game.Bandar;
            if (ArcadeScreen.Current) ArcadeScreen.Current.Close();
            ArcadeScreen.Open(game, game.ToString());
            Debug.Log("[Nainital] arcade test: " + game);
        }
        public static void ArcadeClose() { if (ArcadeScreen.Current) ArcadeScreen.Current.Close(); Debug.Log("[Nainital] arcade closed, last " + ArcadeScreen.LastResult); }
        public static void Bowl()
        {
            var lane = Object.FindFirstObjectByType<BowlingLane>();
            var p = PlayerController.Instance;
            if (!lane || !p) { Debug.Log("[Nainital] bowling test: no lane / player"); return; }
            GameState.AddMoney(100);
            BowlingLane.TestRolls = 10;
            lane.Interact(p);
            Debug.Log("[Nainital] bowling test: started at " + lane.transform.position);
        }
        public static void Rope()
        {
            foreach (var r in Object.FindObjectsByType<RopewayRide>(FindObjectsSortMode.None))
                Debug.Log($"[Nainital] rope cabin {r.index}: at {r.transform.position:F1} docked={r.rope && r.rope.Docked} top={(r.rope && r.rope.AtTopOf(r.index))} wait={(r.rope ? r.rope.Wait : -1):F1} doorLow {r.doorLow:F1} doorHigh {r.doorHigh:F1} riding={RopewayRide.Riding == r}");
        }
        /// <summary>Walk the player onto the platform at the door of cabin 0 (where it waits) and press E.</summary>
        public static void RopeBoard()
        {
            var p = PlayerController.Instance;
            foreach (var r in Object.FindObjectsByType<RopewayRide>(FindObjectsSortMode.None))
            {
                if (r.index != 0 || !r.rope) continue;
                Vector3 door = r.rope.AtTopOf(0) ? r.doorHigh : r.doorLow;
                p.GetComponent<CharacterMotor>().Teleport(door + Vector3.up * 0.2f);
                Debug.Log($"[Nainital] rope board: at {door:F1}, docked={r.rope.Docked}, can={r.CanInteract(p)}");
                GameState.AddMoney(400);
                if (r.CanInteract(p)) r.Interact(p);
            }
        }
        /// <summary>The player to an exact spot (arg x,y,z), no ground search (inside a building under a roof).</summary>
        public static void PutAt()
        {
            var n = Arg().Split(',');
            var at = new Vector3(float.Parse(n[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(n[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(n[2], System.Globalization.CultureInfo.InvariantCulture));
            PlayerController.Instance.GetComponent<CharacterMotor>().Teleport(at);
            Debug.Log("[Nainital] put at " + at);
        }
        /// <summary>The alive pass (23 Nov): wind, particles alive, the footstep surface under the player, spots near.</summary>
        public static void Alive()
        {
            var al = Object.FindFirstObjectByType<AmbientLife>();
            var sb = new System.Text.StringBuilder("[Nainital] alive:");
            sb.Append($" wind={Shader.GetGlobalFloat("_NtWind"):F2} hourP1={Shader.GetGlobalFloat("_NtHourP1"):F2}");
            if (al)
            {
                sb.Append($" steamSpots={al.steam.Length} chimneys={al.chimneys.Length}");
                foreach (var ps in al.GetComponentsInChildren<ParticleSystem>()) sb.Append($" {ps.name}={ps.particleCount}");
            }
            else sb.Append(" NO AmbientLife");
            var p = PlayerController.Instance;
            if (p) sb.Append($" surfaceAt(player)={TerrainRoads.SurfaceAt(p.transform.position)}");
            var walkers = Object.FindObjectsByType<NpcWalker>(FindObjectsSortMode.None);
            int props = 0; foreach (var w in walkers) { var hp = w.transform.GetComponentsInChildren<Transform>(); foreach (var t in hp) if (t.name == "Hand prop" && t.gameObject.activeInHierarchy) props++; }
            sb.Append($" walkers={walkers.Length} handProps={props}");
            sb.Append($" reverbZones={Object.FindObjectsByType<AudioReverbZone>(FindObjectsSortMode.None).Length}");
            Debug.Log(sb.ToString());
        }
        public static void Validate()
        {
            // every script under Resources/Story reads, and every goto / if / choice target has its label
            int bad = 0, n = 0;
            foreach (var ta in Resources.LoadAll<TextAsset>("Story"))
            {
                n++;
                Cutscene.StoryScript s = null;
                try { s = JsonUtility.FromJson<Cutscene.StoryScript>(ta.text); } catch (System.Exception e) { Debug.LogWarning($"[Nainital] story {ta.name}: {e.Message}"); bad++; continue; }
                var labels = new System.Collections.Generic.HashSet<string> { "end" };
                foreach (var st in s.steps) if (st.op == "label") labels.Add(st.name);
                foreach (var st in s.steps)
                {
                    if (!string.IsNullOrEmpty(st.go) && !labels.Contains(st.go)) { Debug.LogWarning($"[Nainital] story {ta.name}: no label '{st.go}'"); bad++; }
                    if (st.opts != null) foreach (var o in st.opts) if (!string.IsNullOrEmpty(o.go) && !labels.Contains(o.go)) { Debug.LogWarning($"[Nainital] story {ta.name}: no label '{o.go}'"); bad++; }
                }
            }
            Debug.Log($"[Nainital] story scripts: {n} read, {bad} problems");
        }
    }
}
