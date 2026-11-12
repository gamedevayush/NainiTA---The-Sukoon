using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Taking an AI car (E / Use). The car is not swapped for another one: it pulls up, its driver gets out and walks
    /// off, and then the player gets in and drives it (user, 2026-11-12: "running car pehle rukegi, tabhi vo bahar
    /// aayega"). TrafficCar does the stopping and the hand-over.
    /// </summary>
    [RequireComponent(typeof(TrafficCar))]
    public class TrafficCarEntry : Interactable
    {
        public CarType type;
        public int paint;
        public int model = -1;
        public Material material;

        TrafficCar traffic;
        VehicleFactory.Spec spec;
        bool asked;

        void Awake()
        {
            traffic = GetComponent<TrafficCar>();
            spec = VehicleFactory.GetSpec(type);
            radius = spec.width * 0.5f + 2.6f;
        }

        public void Rearm() => asked = false;

        public override string Prompt => asked ? "Gaadi ruk rahi hai..." : type == CarType.Bus ? "Bus rok ke baitho" : "Gaadi rok ke baitho";
        public override bool CanInteract(PlayerController p) => !asked && CarController.Driving == null && traffic && !traffic.Wrecked;
        public override Vector3 Point => transform.TransformPoint(new Vector3(spec.width * 0.5f + 0.3f, 0.5f, 0f));

        public override void Interact(PlayerController p)
        {
            if (!traffic) traffic = GetComponent<TrafficCar>();
            if (!traffic || asked) return;
            asked = true;
            traffic.RequestTakeover(p);
            GameEvents.ShowToast("Gaadi ruk rahi hai... driver utrega, phir aap baithoge", 2f);
        }
    }
}
