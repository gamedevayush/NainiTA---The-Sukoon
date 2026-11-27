using UnityEngine.Timeline;

namespace Nainital
{
    /// <summary>Fade / blur / bars over the timeline (StoryScreenClips).</summary>
    [TrackColor(0.3f, 0.3f, 0.3f)]
    [TrackClipType(typeof(StoryScreenClip))]
    public class StoryScreenTrack : TrackAsset { }
}
