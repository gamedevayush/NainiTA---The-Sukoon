using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// A person in a story set (a timeline's scene, Story_C*.unity): only its look (a CharacterLibrary id) and its place
    /// are saved. In the editor it shows that character's model (a preview, not saved) so the set can be blocked out and
    /// animated in the Timeline window; in Play mode it becomes a real HumanoidRig person (Cutscene.MakePerson) under this
    /// transform, posed by its StoryPoseTrack. Move / turn this object (or key it on an Animation track) to move him.
    /// seat >= 0: sits in that seat of the StoryVehicle above it in the hierarchy (hands on the wheel for the driver).
    /// </summary>
    [ExecuteAlways]
    public class StoryActor : MonoBehaviour
    {
        public string actorName;
        public string lookId;
        public bool female, elder, child;
        [Tooltip("The seat of the StoryVehicle above it (hands on the wheel, feet on the floor); -1 = none")]
        public int seat = -1;

        public HumanoidRig Rig { get; private set; }
        public GameObject Person => person;
        GameObject person, preview;
        StoryPose current = (StoryPose)(-1);

        void OnEnable()
        {
            if (Application.isPlaying) Ensure();
            else Preview();
        }

        /// <summary>The real person (Play mode), made once.</summary>
        /// <summary>The character's model saved in the set (a prefab instance named "Model", its body animated by the
        /// timeline's "Body" track): what the editor shows is what plays. Without it, a HumanoidRig person is made.</summary>
        public Transform Model => transform.Find("Model");

        public void Ensure()
        {
            if (!Application.isPlaying || person || Model) return;
            var old = transform.Find("Preview");
            if (old) Destroy(old.gameObject);
            person = Cutscene.MakePerson("Person " + actorName, lookId, female, elder, child, out var rig);
            Rig = rig;
            person.transform.SetParent(transform, false);
            person.transform.localPosition = Vector3.zero;
            person.transform.localRotation = Quaternion.identity;
            current = (StoryPose)(-1);
            if (seat >= 0)
            {
                var v = GetComponentInParent<StoryVehicle>(true);
                if (v) { v.Ensure(); if (v.Parts && v.Parts.HasSetup && seat < v.Parts.SeatCount) v.Parts.Sit(person.transform, rig, seat); }
            }
        }

        void Preview()
        {
            if (preview || Model) return;
            var t = transform.Find("Preview");
            if (t) { preview = t.gameObject; return; }
            var lib = CharacterLibrary.Instance;
            var e = lib ? lib.Find(lookId) : null;
            if (e == null || !e.prefab) return;
            preview = Instantiate(e.prefab, transform, false);
            preview.name = "Preview";
            foreach (var tr in preview.GetComponentsInChildren<Transform>(true)) tr.gameObject.hideFlags = HideFlags.DontSave;
            foreach (var c in preview.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            // a seated person's preview: lowered so it reads as sitting (the real one sits in Play mode)
            if (seat >= 0) preview.transform.localPosition = new Vector3(0f, -0.45f, 0f);
        }

        public void ApplyPose(StoryPose p)
        {
            if (!Rig || p == current) return;
            current = p;
            if (p != StoryPose.Lying && Rig.dead) Rig.Revive();
            Rig.seated = p == StoryPose.Sit || p == StoryPose.Drive;
            Rig.driving = p == StoryPose.Drive;
            Rig.knocked = p == StoryPose.Knocked;
            if (p == StoryPose.Lying) { Rig.knocked = false; Rig.LieDead(); }
        }

        public void Gesture(string name) { if (Rig) Rig.PlayGesture(name, 0f); }

        void LateUpdate()
        {
            if (!Application.isPlaying || !Rig) return;
            Rig.speed = 0f;   // the library rigs read their speed every frame: a story person stands still unless keyed
            Rig.grounded = true;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 0.85f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, 0.25f);
            Gizmos.DrawLine(transform.position + Vector3.up * 0.1f, transform.position + Vector3.up * 0.1f + transform.forward * 0.6f);
        }
    }
}
