using System;
using System.Collections;
using UnityEngine;

namespace Nainital
{
    /// <summary>A cutscene (Resources/Story/&lt;id&gt;.json, Cutscene) as a mission step: done when it ends.</summary>
    public class CutsceneStep : MissionStep
    {
        readonly string id;
        bool done, started;
        public CutsceneStep(string id) { this.id = id; text = ""; }
        public override void Begin()
        {
            done = false; started = false;
            // a frame later: a cutscene that just ended (the step before) has cleaned up by then
            StoryMissions.Run(Start());
        }
        IEnumerator Start()
        {
            yield return null;
            while (Cutscene.Playing) yield return null;
            started = Cutscene.Play(id, () => done = true);
            if (!started) { Debug.LogWarning("[Nainital] mission: cutscene " + id + " did not start"); done = true; }
        }
        public override bool Done() => done;
    }

    /// <summary>A story scene made in Unity Timeline (StoryDirector `id` in the story scene `scene`, loaded additively when
    /// needed) as a mission step: done when it ends.</summary>
    public class TimelineStep : MissionStep
    {
        readonly string scene, id;
        bool done;
        public TimelineStep(string scene, string id) { this.scene = scene; this.id = id; text = ""; }
        public override void Begin() { done = false; StoryMissions.Run(Start()); }
        IEnumerator Start()
        {
            yield return StoryDirector.LoadScene(scene);
            while (StoryDirector.Current || Cutscene.Playing) yield return null;
            if (!StoryDirector.All.TryGetValue(id, out var d) || !d || !d.Play(() => done = true))
            {
                Debug.LogWarning($"[Nainital] mission: story timeline {id} ({scene}) did not start");
                done = true;
            }
        }
        public override bool Done() => done;
    }

    /// <summary>A set piece in code as a step: done when its coroutine ends.</summary>
    public class HookStep : MissionStep
    {
        readonly Func<IEnumerator> run;
        bool done;
        Coroutine co;
        public HookStep(string text, Func<IEnumerator> run) { this.text = text; this.run = run; }
        public override void Begin() { done = false; co = StoryMissions.Run(Wrap()); }
        IEnumerator Wrap() { var e = run(); if (e != null) yield return e; done = true; }
        public override void End() { if (co != null && !done) StoryMissions.Stop(co); co = null; }
        public override bool Done() => done;
    }

    /// <summary>Done when a condition holds (a tutorial: walked, ran, opened the map ...).</summary>
    public class WhenStep : MissionStep
    {
        readonly Func<bool> done;
        readonly Func<Vector3?> target;
        readonly Func<string> progress;
        readonly Action begin, end;
        public WhenStep(string text, Func<bool> done, Func<Vector3?> target = null, Func<string> progress = null, Action begin = null, Action end = null)
        { this.text = text; this.done = done; this.target = target; this.progress = progress; this.begin = begin; this.end = end; }
        public override Vector3? Target => target?.Invoke();
        public override string Progress => progress?.Invoke() ?? "";
        public override void Begin() => begin?.Invoke();
        public override void End() => end?.Invoke();
        public override bool Done() => done();
    }

    /// <summary>
    /// Walk behind someone (bible 0.2: "Uncle ke peeche-peeche chalna, doori rakhni hai"): the target is the leader; too
    /// far and a warning shows, further and the mission fails. Done when `arrived` says so.
    /// </summary>
    public class FollowStep : MissionStep
    {
        readonly Func<Transform> leader;
        readonly Func<bool> arrived;
        readonly float warn, fail;
        readonly string failText;
        float farFor;
        public FollowStep(string text, Func<Transform> leader, Func<bool> arrived, float warn = 16f, float fail = 30f, string failText = "Uncle peeche chhoot gaye")
        { this.text = text; this.leader = leader; this.arrived = arrived; this.warn = warn; this.fail = fail; this.failText = failText; }
        float Dist
        {
            get
            {
                var l = leader?.Invoke();
                if (!l || !P) return 0f;
                Vector3 d = l.position - P.transform.position; d.y *= 0.3f;
                return d.magnitude;
            }
        }
        public override Vector3? Target { get { var l = leader?.Invoke(); return l ? l.position : (Vector3?)null; } }
        public override float Radius => 2f;
        public override void Begin() => farFor = 0f;
        public override string Progress => Dist > warn ? Loc.L("(Paas raho!)", "(Keep up!)") : "";
        public override string Failed
        {
            get
            {
                farFor = Dist > fail ? farFor + Time.deltaTime : 0f;
                return farFor > 1.5f ? failText : null;
            }
        }
        public override bool Done() => arrived();
    }
}
