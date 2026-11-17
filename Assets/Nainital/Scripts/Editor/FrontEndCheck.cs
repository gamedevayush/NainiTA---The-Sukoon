using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Plays the whole front end from the boot scene at a phone's screen size and saves a screenshot at each stage:
    /// splash, language, loading, main menu, settings, the intro (four moments), the HUD, the "more" tray, pause.
    /// FrontEndCheck.Run(folder) from the bridge; the shots land in the folder, then play stops.
    /// </summary>
    [InitializeOnLoad]
    public static class FrontEndCheck
    {
        const string Key = "nt_fe_check", DirKey = "nt_fe_check_dir";
        static double t0, mark;
        static int step;
        static string dir;
        static bool started;

        static FrontEndCheck()
        {
            if (SessionState.GetInt(Key, 0) == 1) EditorApplication.update += Tick;
        }

        public static void Run(string folder, bool firstStart = true)
        {
            Directory.CreateDirectory(folder);
            SessionState.SetInt(Key, 1);
            SessionState.SetString(DirKey, folder);
            PlayerPrefs.SetInt(PlayTest.TouchPref, 1);
            PlayerPrefs.DeleteKey(PlayTest.SkipMenuPref);
            if (firstStart) PlayerPrefs.DeleteKey("nt_lang_set");
            PlayerPrefs.Save();
            PlayerSettings.runInBackground = true;
            PlayModeWindow.SetViewType(PlayModeWindow.PlayModeViewTypes.GameView);
            PlayModeWindow.SetCustomRenderingResolution(2316, 1080, "Phone 2316x1080");
            UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(FrontEndSetup.BootScenePath);
            EditorApplication.update -= Tick;   // (re-added after the domain reload by the static constructor, if there is one)
            EditorApplication.update += Tick;
            started = false;
            EditorApplication.isPlaying = true;
        }

        static void Shot(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"{step:00}_{name}.png"));

        static bool Tap(string objectName)
        {
            foreach (var tb in Object.FindObjectsByType<TapButton>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (tb.name == objectName) { tb.onClick?.Invoke(); return true; }
            return false;
        }

        static void Next() { step++; mark = EditorApplication.timeSinceStartup; }
        static double Since => EditorApplication.timeSinceStartup - mark;

        static void Tick()
        {
            if (!EditorApplication.isPlaying) { if (started) Finish(); return; }
            if (!started) { started = true; t0 = mark = EditorApplication.timeSinceStartup; dir = SessionState.GetString(DirKey, "."); step = 0; }
            var fe = FrontEnd.Instance;
            switch (step)
            {
                case 0: if (Since > 1.7) { Shot("splash_mid"); Next(); } break;
                case 1: if (Since > 1.3) { Shot("splash_full"); Next(); } break;
                case 2: if (GameObject.Find("Language") && Since > 1.2) { Shot("language"); Next(); } else if (Since > 6) Next(); break;
                case 3: if (Since > 0.3) { Tap("Card_" + UiKit.Spaced("HINDI")); Next(); } break;
                case 4: if (Since > 2.5) { Shot("loading_1"); Next(); } break;
                case 5: if (Since > 5.5) { Shot("loading_2"); Next(); } break;
                case 6: if (fe && fe.Current == FrontEnd.State.Menu && !Boot.Instance) { Next(); } else if (Since > 120) Finish(); break;
                case 7: if (Since > 2.5) { Shot("main_menu"); Next(); } break;
                case 8: if (Since > 0.2) { Tap("Item_SETTINGS"); Next(); } break;
                case 9: if (Since > 1.0) { Shot("settings"); Next(); } break;
                case 10: if (Since > 0.2) { foreach (var t in Object.FindObjectsByType<TapButton>(FindObjectsSortMode.None)) if (t.name == "Tab2") t.onClick(); Next(); } break;
                case 11: if (Since > 0.6) { Shot("settings_graphics"); Next(); } break;
                case 12: if (Since > 0.2) { Tap("Item_CREDITS"); Next(); } break;
                case 13: if (Since > 0.8) { Shot("credits"); Next(); } break;
                case 14: if (Since > 0.2) { if (!Tap("Item_CONTINUE")) Tap("Item_NEW GAME"); Next(); } break;
                case 15: if (fe && fe.Current == FrontEnd.State.Intro && Since > 0.5) { Next(); } break;
                case 16: if (Since > 0.35) { Shot("intro_a_top"); Next(); } break;
                case 17: if (Since > 0.75) { Shot("intro_b_drop"); Next(); } break;
                case 18: if (Since > 0.75) { Shot("intro_c_swing"); Next(); } break;
                case 19: if (Since > 0.6) { Shot("intro_d_land"); Next(); } break;
                case 20: if (fe && fe.Current == FrontEnd.State.Playing && Since > 1.5) { Shot("hud"); Next(); } break;
                case 21: if (Since > 0.2) { var more = GameObject.Find("Icon_More"); if (more) more.GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); Next(); } break;
                case 22: if (Since > 0.5) { Shot("hud_tray"); Next(); } break;
                case 23: if (Since > 0.2) { HUD.Instance.TogglePause(); Next(); } break;
                case 24: if (Since > 1.2) { Shot("pause"); Next(); } break;
                case 25: if (Since > 1.0) Finish(); break;
            }
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            SessionState.SetInt(Key, 0);
            started = false;
            UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = null;
            PlayerPrefs.DeleteKey(PlayTest.TouchPref);
            PlayerPrefs.Save();
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            Debug.Log("[Nainital] front end check done: " + dir);
        }
    }
}
