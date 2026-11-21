using UnityEngine;

namespace Nainital
{
    /// <summary>The almari: everything bought (Inventory) and the clothes to change into.</summary>
    public class HouseStorage : Interactable
    {
        public override string Prompt => "Almari kholo (saaman, kapde)";
        public override bool CanInteract(PlayerController p) => !HouseUI.IsOpen;
        public override void Interact(PlayerController p) => HouseUI.Open(transform);
    }
}
