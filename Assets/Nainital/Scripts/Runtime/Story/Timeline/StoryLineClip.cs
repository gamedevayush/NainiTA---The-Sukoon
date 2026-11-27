using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Nainital
{
    public enum StoryLineKind { Say, Narrator, Objective }

    /// <summary>
    /// One line of a story timeline (owner, 26 Nov 2026: "timeline pe shift krdo"): who says it, the Hinglish key, the
    /// English (Loc gives the Hindi), its voice. The subtitle shows while the clip runs and types out over its first part;
    /// the voice starts with it. Drag the clip to move the line; its length is how long the line stays up. Objective = the
    /// big line at the top (a place, "a few days later"). Edited in the Timeline window (StoryLineTrack).
    /// </summary>
    [Serializable]
    public class StoryLineClip : PlayableAsset, ITimelineClipAsset
    {
        public StoryLineKind kind = StoryLineKind.Say;
        [Tooltip("The speaker's name as shown (Hinglish key / English); empty for a narrator line")]
        public string nameKey, nameEn;
        public Color nameColor = new Color(1f, 0.75f, 0.3f);
        [Tooltip("The line: the Hinglish (the Loc key) ...")]
        [TextArea(2, 4)] public string key;
        [Tooltip("... and the English. The Hindi comes from Tools/loc (Tools/story/build_story.py writes it).")]
        [TextArea(2, 4)] public string en;
        public AudioClip voice;
        [Tooltip("The speaker in the set (a StoryActor's name): plays the talking gesture")]
        public string who;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var p = ScriptPlayable<StoryLineBehaviour>.Create(graph);
            p.GetBehaviour().clip = this;
            return p;
        }
    }
}
