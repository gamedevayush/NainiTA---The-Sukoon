using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Nainital
{
    public enum StoryPose { Stand, Sit, Drive, Knocked, Lying }

    /// <summary>A story actor's body for a stretch of the timeline: standing, sitting, at the wheel, knocked down, lying
    /// still; a gesture once at the clip's start (Talk, Nod, Shake, Wave, Angry, Yell, Hit ...). On a StoryPoseTrack bound
    /// to the StoryActor.</summary>
    [Serializable]
    public class StoryPoseClip : PlayableAsset, ITimelineClipAsset
    {
        public StoryPose pose = StoryPose.Stand;
        [Tooltip("Played once when the clip starts (empty: none)")]
        public string gesture;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var p = ScriptPlayable<StoryPoseBehaviour>.Create(graph);
            p.GetBehaviour().clip = this;
            return p;
        }
    }
}
