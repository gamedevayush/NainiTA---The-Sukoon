using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// A vehicle in a story set: only its model's name (VehicleLibrary) and its place are saved. The editor shows the
    /// model (not saved) so the set can be animated in the Timeline window (key this object's position / rotation on an
    /// Animation track); in Play mode the model is made under it, with the police / 108 light bar and siren if asked, and
    /// its wheels turn with how fast the timeline moves it. Its colliders are off: the story moves it, nothing pushes it.
    /// </summary>
    [ExecuteAlways]
    public class StoryVehicle : MonoBehaviour
    {
        public enum Emergency { None, Police, Ambulance }

        [Tooltip("A VehicleLibrary entry's name (Ambulance108, Bolero ...); empty: any of the type")]
        public string model;
        public CarType type = CarType.Sedan;
        public int paint = Px.CarWhite;
        public Emergency emergency;

        public GameObject Model { get; private set; }
        public VehicleParts Parts { get; private set; }
        Vector3 lastPos;

        void OnEnable() => Ensure();

        public void Ensure()
        {
            if (Model) return;
            var old = transform.Find("Model");
            if (old)
            {
                if (Application.isPlaying) Destroy(old.gameObject);
                else { Model = old.gameObject; Parts = Model.GetComponentInChildren<VehicleParts>(); return; }
            }
            int idx = string.IsNullOrEmpty(model) ? -1 : PoliceManager.ModelIndex(model);
            Model = VehicleFactory.Create(type, paint, PoliceManager.Mat, false, "Model", idx);
            if (!Model) return;
            Model.transform.SetParent(transform, false);
            Model.transform.localPosition = Vector3.zero;
            Model.transform.localRotation = Quaternion.identity;
            if (!Application.isPlaying) foreach (var t in Model.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;
            Parts = Model.GetComponentInChildren<VehicleParts>();
            if (Parts) Parts.storyTime = true;
            var rb = Model.GetComponent<Rigidbody>();
            if (rb) rb.isKinematic = true;
            foreach (var c in Model.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            if (Application.isPlaying && emergency != Emergency.None) Dress();
            lastPos = transform.position;
        }

        /// <summary>The model made again (after `model` changed). Inspector: the component's menu > Rebuild model.</summary>
        [ContextMenu("Rebuild model")]
        public void Rebuild()
        {
            if (Model) { if (Application.isPlaying) Destroy(Model); else DestroyImmediate(Model); }
            Model = null; Parts = null;
            Ensure();
        }

        void Dress()
        {
            var kit = PoliceKit.Instance;
            bool police = emergency == Emergency.Police;
            PoliceManager.Letters(Model, kit ? (police ? kit.policeLetters : kit.ambulanceLetters) : null);
            var l = police
                ? EmergencyLights.Fit(Model, kit ? kit.policeRoof : new Vector3(0f, 1.95f, -0.1f), kit ? kit.policeRoofWidth : 1.3f, true)
                : EmergencyLights.Fit(Model, kit ? kit.ambulanceRoof : new Vector3(0f, 2.75f, 1.6f), kit ? kit.ambulanceRoofWidth : 1.6f, false);
            l.lights = true; l.siren = true; l.storyTime = true;
            var lamps = Model.GetComponent<VehicleLamps>();
            if (lamps) lamps.always = true;
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || !Parts) return;
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f) Parts.speed = Vector3.Dot(transform.position - lastPos, transform.forward) / dt;
            lastPos = transform.position;
        }
    }
}
