using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// A test run leaves the player's save as it found it (22 Nov 2026: the bridge's Play tests of the gym, the shops and
    /// the traffic wrote into the owner's own save - muscle, fat, hunger, position, money). PlayTest.Begin takes every
    /// saved key of the game into Bridge/test_save.json; they are put back when Play has ended (EnteredEditMode: after
    /// the game's own last save), or at the next editor start when the editor went away mid-test. Play started by hand
    /// is not touched. Every step: Bridge/test_save_trace.txt.
    /// Check it without the game: "call TestSave.Probe" (an EMPTY scene, a key of its own), "call TestSave.ProbeChange"
    /// in that Play, leave Play, "call TestSave.ProbeResult".
    /// </summary>
    public static class TestSave
    {
        const string Live = "nt_testsave_live", ProbeKey = "nt_test_probe";

        static readonly string[] Ints =
        {
            "nt_money", "nt_radio", "nt_lang", "nt_lang_en", "nt_lang_set", "nt_phone_wall", "nt_invert_y", "nt_haptics", "nt_fps60", "nt_quality", ProbeKey,
        };
        static readonly string[] Floats =
        {
            "nt_stat_stamina", "nt_stat_muscle", "nt_stat_fat", "nt_stat_drive", "nt_stat_bike", "nt_stat_swim", "nt_stat_hunger",
            "nt_vol_master", "nt_vol_amb", "nt_vol_fx", "nt_look",
        };
        /// <summary>PlayTest's own switches: never the player's, so every restore takes them off. They outlived the 22 Nov
        /// power cut (PlayTest.End never ran): the owner's next Play had phone buttons and no menu.</summary>
        static readonly string[] Switches = { PlayTest.TouchPref, PlayTest.SkipMenuPref };
        static readonly string[] Strings = { "nt_save", "nt_done", "nt_inv", "nt_outfit" };

        [Serializable]
        class Entry
        {
            public string key, s;
            public int kind, i;   // kind: 0 int, 1 float, 2 string
            public float f;
            public bool has;
        }

        [Serializable]
        class Snapshot { public List<Entry> all = new List<Entry>(); }

        static string InBridge(string name) => Path.GetFullPath(Path.Combine(Application.dataPath, "../Bridge/" + name));
        static string FilePath => InBridge("test_save.json");

        static void Trace(string what)
        {
            try { File.AppendAllText(InBridge("test_save_trace.txt"), $"{DateTime.Now:HH:mm:ss.fff} {what} (playing {EditorApplication.isPlaying}, will change {EditorApplication.isPlayingOrWillChangePlaymode}, live {SessionState.GetBool(Live, false)}, file {File.Exists(FilePath)})\n"); }
            catch { }
        }

        [InitializeOnLoadMethod]
        static void Hook()
        {
            // the asset import workers are Unity processes of their own that run this too: one took the running test's
            // snapshot for a crashed test's and put it back the moment Play began (22 Nov 2026, the trace)
            if (AssetDatabase.IsAssetImportWorkerProcess()) return;
            EditorApplication.playModeStateChanged -= OnPlay;
            EditorApplication.playModeStateChanged += OnPlay;
            Trace("load");
            // a snapshot no test of this editor session took: the editor went away in the middle of one
            if (!SessionState.GetBool(Live, false)) Restore("editor start");
        }

        static void OnPlay(PlayModeStateChange s)
        {
            Trace("play state " + s);
            if (s == PlayModeStateChange.EnteredEditMode) Restore("Play ended");
        }

        /// <summary>Before a test's Play (a snapshot still waiting is the true one: kept).</summary>
        public static void Take()
        {
            Trace("take");
            if (EditorApplication.isPlaying) return;
            if (!File.Exists(FilePath))
            {
                var snap = new Snapshot();
                foreach (var k in Ints) snap.all.Add(new Entry { key = k, kind = 0, has = PlayerPrefs.HasKey(k), i = PlayerPrefs.GetInt(k, 0) });
                foreach (var k in Floats) snap.all.Add(new Entry { key = k, kind = 1, has = PlayerPrefs.HasKey(k), f = PlayerPrefs.GetFloat(k, 0f) });
                foreach (var k in Strings) snap.all.Add(new Entry { key = k, kind = 2, has = PlayerPrefs.HasKey(k), s = PlayerPrefs.GetString(k, "") });
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonUtility.ToJson(snap));
            }
            SessionState.SetBool(Live, true);
            Trace("taken");
        }

        /// <summary>A test's Play starts fed: an empty stomach blurred, darkened and greyed every test screenshot
        /// (owner, 22 Nov 2026: "ye UI itna blur or vignetti kyun hai"). Only with a snapshot to put back.</summary>
        public static void Fed()
        {
            if (EditorApplication.isPlaying || !File.Exists(FilePath)) return;
            PlayerPrefs.SetFloat("nt_stat_hunger", 1f);
            PlayerPrefs.Save();
        }

        static void Restore(string why)
        {
            if (!File.Exists(FilePath)) { SessionState.SetBool(Live, false); return; }
            var snap = JsonUtility.FromJson<Snapshot>(File.ReadAllText(FilePath));
            foreach (var e in snap.all)
            {
                if (!e.has) PlayerPrefs.DeleteKey(e.key);
                else if (e.kind == 0) PlayerPrefs.SetInt(e.key, e.i);
                else if (e.kind == 1) PlayerPrefs.SetFloat(e.key, e.f);
                else PlayerPrefs.SetString(e.key, e.s);
            }
            foreach (var k in Switches) PlayerPrefs.DeleteKey(k);
            PlayerPrefs.Save();
            File.Delete(FilePath);
            SessionState.SetBool(Live, false);
            Trace("restored: " + why);
        }

        // ------------------------------------------------------------------ the check (no game, a key of its own)
        public static void ProbeReady() => Debug.Log("[Nainital] test save: probe ready, workers out");

        public static void Probe()
        {
            if (EditorApplication.isPlaying) throw new Exception("leave Play first");
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);   // nothing of the game in it
            PlayerPrefs.SetInt(ProbeKey, 1);
            PlayerPrefs.Save();
            Take();
            EditorApplication.isPlaying = true;
        }

        public static void ProbeChange()
        {
            if (!EditorApplication.isPlaying) throw new Exception("not playing");
            PlayerPrefs.SetInt(ProbeKey, 2);
            PlayerPrefs.Save();
            Application.quitting += () => { PlayerPrefs.SetInt(ProbeKey, 3); PlayerPrefs.Save(); };   // a save as Play ends, like the game's
            Debug.Log("[Nainital] test save: probe changed");
        }

        public static void ProbeStop() => EditorApplication.isPlaying = false;

        public static void ProbeResult()
        {
            if (EditorApplication.isPlaying) throw new Exception("still playing");
            int v = PlayerPrefs.GetInt(ProbeKey, -1);
            PlayerPrefs.DeleteKey(ProbeKey);
            PlayerPrefs.Save();
            Debug.Log($"[Nainital] test save: probe {(v == 1 ? "PASS" : "FAIL")} (value {v}, wanted 1)");
        }
    }
}
