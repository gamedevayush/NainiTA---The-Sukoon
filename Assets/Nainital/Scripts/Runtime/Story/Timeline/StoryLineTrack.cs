using UnityEngine.Timeline;

namespace Nainital
{
    /// <summary>The dialogue track of a story timeline: StoryLineClips (subtitle + voice).</summary>
    [TrackColor(0.95f, 0.75f, 0.25f)]
    [TrackClipType(typeof(StoryLineClip))]
    public class StoryLineTrack : TrackAsset { }
}
