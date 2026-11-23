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

        public static void Stop() { Cutscene.StopAll(); Debug.Log("[Nainital] story test: stopped"); }
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
