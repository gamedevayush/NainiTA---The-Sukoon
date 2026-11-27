using UnityEngine;
using UnityEngine.Playables;

namespace Nainital
{
    /// <summary>Holds a StoryActor in its StoryPoseClip's pose (Play mode; the edit-time preview figure stays as it is).</summary>
    public class StoryPoseBehaviour : PlayableBehaviour
    {
        public StoryPoseClip clip;
        bool started;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!Application.isPlaying || clip == null) return;
            var a = playerData as StoryActor;
            if (!a) return;
            a.ApplyPose(clip.pose);
            if (!started)
            {
                started = true;
                if (!string.IsNullOrEmpty(clip.gesture)) a.Gesture(clip.gesture);
            }
        }

        public override void OnBehaviourPause(Playable playable, FrameData info) => started = false;
    }
}
