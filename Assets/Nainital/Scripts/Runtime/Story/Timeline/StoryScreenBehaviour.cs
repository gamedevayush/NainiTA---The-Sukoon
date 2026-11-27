using UnityEngine;
using UnityEngine.Playables;

namespace Nainital
{
    /// <summary>Applies a StoryScreenClip to the story UI (Play mode).</summary>
    public class StoryScreenBehaviour : PlayableBehaviour
    {
        public StoryScreenClip clip;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!Application.isPlaying || clip == null) return;
            var ui = StoryUI.Instance;
            if (!ui) return;
            double dur = playable.GetDuration();
            float u = dur > 1e-4 ? Mathf.Clamp01((float)(playable.GetTime() / dur)) : 1f;
            u = UiKit.EaseInOut(u);
            ui.FadeNow(Mathf.Lerp(clip.fadeFrom, clip.fadeTo, u));
            ui.BlurNow(Mathf.Lerp(clip.blurFrom, clip.blurTo, u));
            ui.Bars(clip.bars);
        }
    }
}
