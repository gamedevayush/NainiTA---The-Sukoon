using UnityEngine.Timeline;

namespace Nainital
{
    /// <summary>A story actor's poses over the timeline (bound to its StoryActor).</summary>
    [TrackColor(0.4f, 0.8f, 0.95f)]
    [TrackBindingType(typeof(StoryActor))]
    [TrackClipType(typeof(StoryPoseClip))]
    public class StoryPoseTrack : TrackAsset { }
}
