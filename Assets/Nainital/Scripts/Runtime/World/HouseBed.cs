using System.Collections;
using UnityEngine;

namespace Nainital
{
    /// <summary>The bed: sleep till 07:00, health and the stamina bar full, the game saved.</summary>
    public class HouseBed : Interactable
    {
        static bool sleeping;
        public override string Prompt => "Bistar pe so jao (save)";
        public override bool CanInteract(PlayerController p) => !sleeping && !HouseUI.IsOpen && !ShopUI.IsOpen;   // the almari open: its panel first
        public override void Interact(PlayerController p) { if (!sleeping) StartCoroutine(Sleep(p)); }

        /// <summary>How many times he has slept here this session (story steps watch it).</summary>
        public static int SleepCount;

        IEnumerator Sleep(PlayerController p)
        {
            sleeping = true;
            GameInput.UiBlocking = true;
            var fade = Fade.Get();
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 1.2f) { fade.color = new Color(0, 0, 0, t); yield return null; }
            fade.color = Color.black;
            var dn = DayNightCycle.Instance;
            if (dn) dn.timeOfDay = 7f;
            if (p && p.Health) p.Health.Revive(1f);
            PlayerStats.UseEnergy(-1f);
            SaveGame.Save();
            SleepCount++;   // a story step waits for it (StoryMissions 0.2: sleep = save)
            yield return new WaitForSecondsRealtime(1.2f);
            GameEvents.ShowToast("Subah ho gayi - achhi neend! Game save ho gaya", 3.5f);
            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 1.5f) { fade.color = new Color(0, 0, 0, t); yield return null; }
            fade.color = new Color(0, 0, 0, 0);
            GameInput.UiBlocking = FrontEnd.MenuOpen;
            sleeping = false;
        }
    }
}
