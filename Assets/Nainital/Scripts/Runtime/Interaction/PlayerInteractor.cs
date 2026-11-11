using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>Finds the best nearby interactable and triggers it.</summary>
    public class PlayerInteractor : MonoBehaviour
    {
        public static Interactable Current { get; private set; }
        PlayerController player;

        void Awake() => player = GetComponent<PlayerController>();

        void Update()
        {
            Current = null;
            if (!player || player.CurrentMode != PlayerController.Mode.Normal || player.InWater || GameInput.UiBlocking)
            {
                if (player && player.CurrentMode == PlayerController.Mode.Boat) Current = BoatController.Driving;
                if (player && player.CurrentMode == PlayerController.Mode.Vehicle) Current = CarController.Driving;
                if (player && player.CurrentMode == PlayerController.Mode.Mount) Current = Pony.Ridden ? (Interactable)Pony.Ridden : RopewayRide.Riding;
                if (Current && GameInput.InteractPressed && !GameInput.UiBlocking) Current.Interact(player);
                return;
            }
            float best = float.MaxValue;
            Vector3 pos = transform.position;
            Vector3 fwd = transform.forward;
            foreach (var it in Interactable.All)
            {
                if (!it || !it.isActiveAndEnabled) continue;
                Vector3 d = it.Point - pos;
                if (Mathf.Abs(d.y) > 2.5f) continue;
                d.y = 0;
                float dist = d.magnitude;
                if (dist > it.radius || !it.CanInteract(player)) continue;
                float score = dist - Vector3.Dot(fwd, d.normalized) * 0.8f;
                if (score < best) { best = score; Current = it; }
            }
            if (Current && GameInput.InteractPressed) Current.Interact(player);
        }
    }
}
