using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Nainital
{
    public enum StoryCueKind { Press, Choice, Qte, Look }

    /// <summary>
    /// Where the player acts in a story timeline (StoryDirector handles them):
    ///   Press  - the timeline stops at the clip's start till one press ("Aankhein kholo"), then goes on from its end.
    ///   Choice - stops till an option is picked (the answer into the story flag), goes on from the end.
    ///   Qte    - stops; each press moves the timeline forward inside the clip, it slides back when the presses stop
    ///            (everything keyed inside the clip - the escape - is scrubbed by the presses); at 100% on from the end;
    ///            out of time: black, back to the clip's start, again.
    ///   Look   - the timeline runs on; meanwhile the viewer may turn his head (mouse / stick / drag).
    /// </summary>
    [Serializable]
    public class StoryCueClip : PlayableAsset, ITimelineClipAsset
    {
        [Serializable] public class Option { public string key, en, v; }

        public StoryCueKind kind = StoryCueKind.Press;
        [Tooltip("The prompt (Hinglish key / English)")]
        public string key, en;
        [Tooltip("QTE: fill per press, drain a second, the time limit (s)")]
        public float per = 6f, decay = 9f, limit = 15f;
        [Tooltip("Choice: the options; the picked one's v goes into this story flag")]
        public List<Option> options = new List<Option>();
        public string flag;
        [Tooltip("Look: how far he can turn, degrees left-right / up-down")]
        public float lookYaw = 55f, lookPitch = 22f;

        public ClipCaps clipCaps => ClipCaps.None;
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) => Playable.Create(graph);
    }
}
