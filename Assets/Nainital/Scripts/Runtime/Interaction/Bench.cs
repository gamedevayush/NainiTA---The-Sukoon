using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>Bench on Mall Road / viewpoints.</summary>
    public class Bench : Interactable
    {
        public Transform seat;
        public override string Prompt => "Bench pe baitho";
        public override Vector3 Point => seat ? seat.position : transform.position;
        public override void Interact(PlayerController p)
        {
            if (seat) p.SitOn(seat);
            GameEvents.ShowToast("Jheel ka nazara... (chalne ke liye move karo)", 2.5f);
        }
    }
}
