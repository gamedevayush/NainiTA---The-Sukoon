using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// A built piece of a story set (the hospital room, the inside of the 108's back): made from code under this object
    /// when it is enabled - in the editor too (not saved, made again) - so it shows while the timeline is edited.
    /// Hospital room: the floor's middle at this object, the bed's head towards -z, the window on +x, the door on +z.
    /// Ambulance inside: this object at the ambulance model's origin (a child of its StoryVehicle).
    /// Car inside: a child of the car's StoryVehicle at its origin - seats where the model's seats are (its Vehicle Setup),
    /// the dashboard, the wheel on the driver's side, the console, the door panels, the floor and the roof lining
    /// (owner, 27 Nov 2026: "gaadi abhi andr se empty hai.. no seats.. proper interior bnaado").
    /// </summary>
    [ExecuteAlways]
    public class StorySet : MonoBehaviour
    {
        public enum Kind { HospitalRoom, AmbulanceInside, CarInside }
        public Kind kind;
        GameObject built;
        Kind builtKind;

        void OnEnable() => Build(false);

        /// <summary>Made again (its kind changed; the baker sets the kind after adding the component).</summary>
        [ContextMenu("Build again")]
        public void Rebuild() => Build(true);

        void Build(bool again)
        {
            if (built && !again && builtKind == kind) return;
            var old = transform.Find("Built");
            if (old)
            {
                if (!again && !built && Application.isPlaying) Destroy(old.gameObject);
                else if (Application.isPlaying) Destroy(old.gameObject);
                else DestroyImmediate(old.gameObject);
            }
            builtKind = kind;
            built = new GameObject("Built");
            built.transform.SetParent(transform, false);
            if (kind == Kind.HospitalRoom) StorySets.HospitalRoom(built.transform);
            else if (kind == Kind.AmbulanceInside) StorySets.AmbulanceInside(built.transform);
            else StorySets.CarInside(built.transform, GetComponentInParent<StoryVehicle>(true));
            if (!Application.isPlaying)
            {
                foreach (var t in built.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;
                foreach (var mf in built.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh) mf.sharedMesh.hideFlags = HideFlags.DontSave;
            }
        }
    }
}
