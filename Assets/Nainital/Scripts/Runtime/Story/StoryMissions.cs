using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// The story's chain of missions (owner, 26 Nov 2026: "doc waale missions bnaane shuru kro" - exactly as
    /// docs/Nainital_Story_Mission_Bible.docx section B lists them; decisions and status: docs/STORY.md). Each chapter's
    /// file adds its missions in the bible's order (Chapter0.cs ...); MissionManager runs them. Only the first mission
    /// not yet passed is open: its marker shows, or, when it follows straight on (autoStart), it begins by itself a moment
    /// after the one before passes. A New Game begins the prologue at once. Added by MissionManager at runtime.
    /// </summary>
    public class StoryMissions : MonoBehaviour
    {
        public static StoryMissions Instance { get; private set; }
        static readonly List<Mission> chain = new List<Mission>();
        static Mission pending;
        static float pendingAt;
        static bool newGame, checkedContinue;

        void Awake() => Instance = this;

        public static Coroutine Run(IEnumerator e) => Instance && e != null ? Instance.StartCoroutine(e) : null;
        public static void Stop(Coroutine c) { if (Instance && c != null) Instance.StopCoroutine(c); }

        /// <summary>MissionManager's catalogue: every chapter's missions, in order.</summary>
        public static void Build(List<Mission> into)
        {
            chain.Clear();
            Chapter0.Build(chain);
            into.AddRange(chain);
            Debug.Log($"[Nainital] story: {chain.Count} missions, next = {(Next != null ? Next.id : "none")}");
        }

        /// <summary>The first story mission not passed yet (null: all done).</summary>
        public static Mission Next
        {
            get { foreach (var m in chain) if (!GameState.IsDone(m.id)) return m; return null; }
        }

        public static bool IsNext(Mission m) => m != null && m == Next;

        /// <summary>A new game: the prologue starts as soon as the menu has gone.</summary>
        public static void NewGame()
        {
            newGame = true;
            StoryFlags.ResetAll();
        }

        /// <summary>MissionManager: one passed. The next one begins by itself if it follows straight on.</summary>
        public static void Passed(Mission m)
        {
            var n = Next;
            if (n != null && n.autoStart) { pending = n; pendingAt = Time.time + (m.quiet ? 0.1f : 1.2f); }
        }

        void Update()
        {
            var mm = MissionManager.Instance;
            if (!mm || chain.Count == 0) return;
            bool playing = !FrontEnd.Instance || FrontEnd.Instance.Current == FrontEnd.State.Playing;
            if (!playing) return;
            if (newGame)
            {
                newGame = false;
                checkedContinue = true;
                pending = chain[0]; pendingAt = Time.time + 0.4f;
            }
            else if (!checkedContinue)
            {
                // a saved game in the middle of a run of scenes (no marker of their own): carry on with the next one
                checkedContinue = true;
                var n = Next;
                if (n != null && n.autoStart && n != chain[0]) { pending = n; pendingAt = Time.time + 1.5f; }
            }
            // a scene that ended black waits for the next one; if none comes, the black lifts
            if (StoryDirector.HoldBlackUntil > 0f && !StoryDirector.Current && Time.unscaledTime > StoryDirector.HoldBlackUntil)
            {
                StoryDirector.HoldBlackUntil = 0f;
                if (StoryUI.Instance) StoryUI.Instance.StartCoroutine(StoryUI.Instance.Fade(0f, 1f));
            }
            if (pending != null && Time.time >= pendingAt && mm.Active == null && !Cutscene.Playing && !StoryDirector.Current)
            {
                var m = pending; pending = null;
                if (!GameState.IsDone(m.id)) mm.Begin(m);
            }
        }
    }
}
