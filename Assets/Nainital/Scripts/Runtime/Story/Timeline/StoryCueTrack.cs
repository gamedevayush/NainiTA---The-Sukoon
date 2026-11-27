using UnityEngine.Timeline;

namespace Nainital
{
    /// <summary>The player's part in a story timeline: presses, choices, the QTE, looking round (StoryCueClips).</summary>
    [TrackColor(0.95f, 0.35f, 0.3f)]
    [TrackClipType(typeof(StoryCueClip))]
    public class StoryCueTrack : TrackAsset { }
}
