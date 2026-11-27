using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Nainital
{
    /// <summary>The screen over a stretch of the timeline: the black fade, the dazed blur, the cinema bars, each going
    /// from its "from" to its "to" across the clip (equal = held). Between clips the last values stay.</summary>
    [Serializable]
    public class StoryScreenClip : PlayableAsset, ITimelineClipAsset
    {
        [Range(0f, 1f)] public float fadeFrom, fadeTo;
        [Range(0f, 1f)] public float blurFrom, blurTo;
        public bool bars = true;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var p = ScriptPlayable<StoryScreenBehaviour>.Create(graph);
            p.GetBehaviour().clip = this;
            return p;
        }
    }
}
