using UnityEngine;

namespace Nainital
{
    /// <summary>A chair at a restaurant table: E to sit and order (Restaurant, ShopUI).</summary>
    public class RestaurantSeat : Interactable
    {
        public Restaurant restaurant;
        public Transform table;
        public int index;
        [System.NonSerialized] public GameObject diner;
        [System.NonSerialized] public bool taken;

        public override string Prompt => "Baitho aur order karo";
        public override bool CanInteract(PlayerController p) => !diner && !ShopUI.IsOpen;

        public override void Interact(PlayerController p)
        {
            if (!restaurant) return;
            taken = true;
            p.SitOn(transform);
            ShopUI.OpenMenu(restaurant, this);
        }

        void Update()
        {
            if (!taken) return;
            var p = PlayerController.Instance;
            if (!p || p.CurrentMode != PlayerController.Mode.Seated) taken = false;   // got up
        }
    }
}
