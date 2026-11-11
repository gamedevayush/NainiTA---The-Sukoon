using UnityEngine;

namespace Nainital
{
    /// <summary>Lets the player take over an AI traffic car (E / Use): the AI car is swapped for a drivable one.</summary>
    public class TrafficCarEntry : Interactable
    {
        public CarType type;
        public int paint;
        public int model = -1;   // VehicleLibrary entry: the car the player takes is the one they saw
        public Material material;

        VehicleFactory.Spec spec;

        void Awake()
        {
            spec = VehicleFactory.GetSpec(type);
            radius = spec.width * 0.5f + 2.2f;
        }

        public override string Prompt => type == CarType.Bus ? "Bus me baitho" : "Gaadi me baitho";
        public override bool CanInteract(PlayerController p) => CarController.Driving == null && material;
        public override Vector3 Point => transform.TransformPoint(new Vector3(spec.width * 0.5f + 0.3f, 0.5f, 0f));

        public override void Interact(PlayerController p)
        {
            // the AI car goes first: if it stood there one more physics step, the new car would be born inside its
            // collider and be pushed out through the road (it then fell for ever: black screen, speed climbing to 300)
            Vector3 pos = transform.position;
            Quaternion rot = transform.rotation;
            gameObject.SetActive(false);
            Destroy(gameObject);   // TrafficManager drops destroyed cars from its list
            var go = VehicleFactory.Create(type, paint, material, true, "Taken " + type, model);
            go.transform.SetPositionAndRotation(pos + Vector3.up * 0.08f, rot);
            Physics.SyncTransforms();
            var car = go.GetComponent<CarController>();
            if (car) car.Interact(p);
        }
    }
}
