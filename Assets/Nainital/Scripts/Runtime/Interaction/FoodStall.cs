using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>Momos / maggi / chai stall: heals the player.</summary>
    public class FoodStall : Interactable
    {
        public string stallName = "Momos Stall";
        public string[] items = { "Garma garam momos", "Maggi", "Adrak wali chai", "Bun makkhan", "Bhutta (corn)" };
        public float heal = 35f;
        float nextUse;

        public int price = 40;

        public override string Prompt => stallName + " se khao (Rs " + price + ")";
        public override void Interact(PlayerController p)
        {
            if (Time.time < nextUse) { GameEvents.ShowToast("Abhi pet bhara hai, thodi der baad aana!", 2f); return; }
            if (!GameState.TrySpend(price)) { GameEvents.ShowToast("Jeb khaali hai! Mission karke paise kamao.", 2.5f); return; }
            nextUse = Time.time + 15f;
            string item = items[Random.Range(0, items.Length)];
            p.Health.Heal(heal);
            GameEvents.ShowToast(item + " khaya! +" + heal + " health  (-Rs " + price + ")", 2.5f);
            GameEvents.RaiseAte();
        }
    }
}
