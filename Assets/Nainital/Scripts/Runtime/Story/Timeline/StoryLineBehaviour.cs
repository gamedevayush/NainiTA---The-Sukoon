using UnityEngine;
using UnityEngine.Playables;

namespace Nainital
{
    /// <summary>A StoryLineClip while it plays: tells the StoryDirector that runs the timeline (Play mode only).</summary>
    public class StoryLineBehaviour : PlayableBehaviour
    {
        public StoryLineClip clip;
        bool on;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!Application.isPlaying || clip == null) return;
            var d = StoryDirector.Current;
            if (!d) return;
            if (!on) { on = true; d.LineBegin(clip); }
            d.LineFrame(clip, (float)playable.GetTime(), (float)playable.GetDuration());
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if (!on) return;
            on = false;
            var d = StoryDirector.Current;
            if (d) d.LineEnd(clip);
        }
    }
}
