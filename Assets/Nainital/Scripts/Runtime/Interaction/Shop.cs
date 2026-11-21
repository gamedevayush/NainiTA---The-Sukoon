using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// A shop the player can buy from (owner, 21 Nov 2026, FREEROAM #5: "10-15 interactable shops, each its own goods ...
    /// fake shop names at the real places - Mall Road, Bara Bazaar, Tibetan Market ... everything bought goes to the
    /// house"). Stands on the pavement in front of a shop front (placed by LandmarkBuilder.Shops); E opens ShopUI with its
    /// goods from ShopCatalog. Kept things go to Inventory (the house shows them), food can be eaten on the spot
    /// (PlayerStats.Eat), clothes shops sell the outfits.
    /// </summary>
    public class Shop : Interactable
    {
        /// <summary>The shop's (made-up) name, a Loc key.</summary>
        public string shopName = "Dukaan";
        /// <summary>Where it is ("Mall Road", "Bada Bazar", "Tibetan Market"), a Loc key.</summary>
        public string place = "Mall Road";
        /// <summary>What it sells: ShopCatalog ids.</summary>
        public string[] items = new string[0];

        public override string Prompt => shopName + " - dukaan dekho";
        public override bool CanInteract(PlayerController p) => !ShopUI.IsOpen;
        public override void Interact(PlayerController p) => ShopUI.Open(this);
    }
}
