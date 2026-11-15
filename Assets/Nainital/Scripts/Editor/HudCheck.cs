using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Puts the HUD into each of its states in play mode, for screenshots through the bridge
    /// ("call HudCheck.Desktop", then "call HudCheck.Roam", "capture roam" ...). Desktop: a 1920 x 1080 Game view without
    /// the touch controls; Phone: PlayTest.Begin (2316 x 1080, touch on).
    /// </summary>
    public static class HudCheck
    {
        static void Need() { if (!EditorApplication.isPlaying) throw new System.Exception("not playing"); }

        public static void Desktop()
        {
            PlayerPrefs.SetInt(PlayTest.TouchPref, 0);
            PlayerPrefs.Save();
            PlayerSettings.runInBackground = true;
            PlayModeWindow.SetViewType(PlayModeWindow.PlayModeViewTypes.GameView);
            PlayModeWindow.SetCustomRenderingResolution(1920, 1080, "HD 1920x1080");
            EditorApplication.isPlaying = true;
        }

        public static void Phone() => PlayTest.Begin();
        public static void Stop() => PlayTest.End();

        /// <summary>Walking about: a place's name, two notifications, money coming in.</summary>
        public static void Roam()
        {
            Need();
            CloseAll();
            GameEvents.RaiseLocation("Mallital");
            GameEvents.ShowToast("Raat ho gayi - Mall Road ki lights dekho!", 6f);
            GameEvents.ShowToast("Bhutte wale se bhutta khareeda", 6f);
            GameState.AddMoney(250);
        }

        /// <summary>A mission running (the first one), with its banner.</summary>
        public static void Mission()
        {
            Need();
            CloseAll();
            var mm = MissionManager.Instance;
            if (mm && mm.missions.Count > 0 && mm.Active == null) mm.Begin(mm.missions[0]);
        }

        /// <summary>Hurt badly: the health ring low and the red edges.</summary>
        public static void Hurt()
        {
            Need();
            var p = PlayerController.Instance;
            if (p) { p.Health.Revive(0.45f); p.Health.Damage(25f, p.transform.position + p.transform.forward, null); }
        }

        public static void Heal()
        {
            Need();
            var p = PlayerController.Instance;
            if (p) p.Health.Revive(1f);
        }

        /// <summary>In a car: spawns one (the GAADI cheat) and gets in.</summary>
        public static void Car()
        {
            Need();
            CloseAll();
            var p = PlayerController.Instance;
            if (!p || CarController.Driving) return;
            CheatCodes.Run("GAADI");
            CarController best = null;
            float bd = float.MaxValue;
            foreach (var c in Object.FindObjectsByType<CarController>(FindObjectsSortMode.None))
            {
                if (c.ai || c.HasDriver || c.wrecked) continue;
                float d = (c.transform.position - p.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = c; }
            }
            if (best) best.Interact(p);
        }

        /// <summary>Drive forward (touch throttle) so the speedometer shows a speed.</summary>
        public static void Throttle() { Need(); GameInput.TouchMove = new Vector2(0f, 1f); }
        public static void Coast() { Need(); GameInput.TouchMove = Vector2.zero; }

        public static void Banner() { Need(); GameEvents.ShowBanner("Mission poora!", "Inaam: ₹500  -  Naina Devi ke darshan ho gaye", new Color(1f, 0.72f, 0.16f)); }

        public static void Dialog() { Need(); CloseAll(); GameEvents.ShowDialog("Chaiwala", "Bhaisahab, Mall Road pe shaam ko itni bheed hoti hai ki gaadi le jaana mushkil hai. Paidal jaao, jheel ke kinare - wahi asli Nainital hai."); }

        public static void Pause()
        {
            Need();
            CloseAll();
            if (HUD.Instance) HUD.Instance.TogglePause();
            var mp = GameObject.Find("Pause/MissionsBox") ?? FindInactive("MissionsBox");
            if (mp) mp.SetActive(true);
        }

        public static void BigMap() { Need(); CloseAll(); GameInput.TouchMap = true; }

        public static void Help() { Need(); CloseAll(); var h = FindInactive("Help"); if (h) h.SetActive(true); }

        public static void CloseAll()
        {
            Need();
            if (HUD.Instance)
            {
                var pause = FindInactive("Pause");
                if (pause && pause.activeSelf) HUD.Instance.TogglePause();
                HUD.Instance.CloseDialog();
            }
            foreach (var n in new[] { "Help", "BigMap" }) { var g = FindInactive(n); if (g) g.SetActive(false); }
        }

        static GameObject FindInactive(string name)
        {
            if (!HUD.Instance) return null;
            foreach (var t in HUD.Instance.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
            return null;
        }
    }
}
