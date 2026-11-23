using System;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// The journey so far (user, 2026-11-17: main menu "resume, save"): where the player stood, which way they faced, the
    /// hour, the weather and the place's name, saved on Save in the pause menu, every 90 s while walking about and when the
    /// app goes to the background (a phone may close it there). Money and the missions done are saved by GameState as
    /// they change. Continue puts the player back here; New Game clears it all.
    /// </summary>
    public static class SaveGame
    {
        const string Key = "nt_save";

        public struct Data
        {
            public Vector3 pos;
            public float yaw, hour;
            public int weather;
            public string place;
            public long savedAt;      // DateTime ticks (local)
            public float playSeconds;
        }

        public static bool Exists => PlayerPrefs.HasKey(Key);

        public static Data Read()
        {
            var d = new Data { place = "", hour = 17.5f };
            var s = PlayerPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(s)) return d;
            try { d = JsonUtility.FromJson<Wire>(s).ToData(); } catch { }
            return d;
        }

        /// <summary>The last place the player stood on their feet (SaveTracker keeps it), else where they are.</summary>
        public static void Save()
        {
            var p = PlayerController.Instance;
            if (!p) return;
            var tr = SaveTracker.Instance;
            var d = new Data
            {
                pos = tr && tr.HasSafe ? tr.SafePos : p.transform.position,
                yaw = tr && tr.HasSafe ? tr.SafeYaw : p.transform.eulerAngles.y,
                hour = DayNightCycle.Instance ? DayNightCycle.Instance.timeOfDay : 17.5f,
                weather = WeatherSystem.Instance ? (int)WeatherSystem.Instance.current : 0,
                place = tr ? tr.Place : "",
                savedAt = DateTime.Now.Ticks,
                playSeconds = PlaySeconds,
            };
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Wire.From(d)));
            PlayerPrefs.Save();
        }

        /// <summary>Puts the player, the hour and the weather back as saved.</summary>
        public static void Apply()
        {
            if (!Exists) return;
            var d = Read();
            PlaySeconds = d.playSeconds;
            var p = PlayerController.Instance;
            if (p && p.CurrentMode == PlayerController.Mode.Normal) Place(p, d.pos, d.yaw);
            if (DayNightCycle.Instance) { DayNightCycle.Instance.timeOfDay = d.hour; DayNightCycle.Instance.Apply(); }
            if (WeatherSystem.Instance) WeatherSystem.Instance.current = (WeatherSystem.Kind)Mathf.Clamp(d.weather, 0, 4);   // (no toast; 4 = rain in fog)
        }

        public static void Place(PlayerController p, Vector3 pos, float yaw)
        {
            var motor = p.GetComponent<CharacterMotor>();
            if (motor) { motor.velocity = Vector3.zero; motor.Teleport(pos); }
            else p.transform.position = pos;
            p.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            p.RespawnPoint = pos;
        }

        /// <summary>A new game: no save, money and missions back to the start.</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlaySeconds = 0f;
            GameState.ResetProgress();
            Inventory.ResetAll();   // nothing bought, the default clothes (shops, 22 Nov 2026)
            PlayerPrefs.Save();
        }

        public static float PlaySeconds;

        /// <summary>"2 ghante pehle" / "2 hours ago".</summary>
        public static string Ago(long ticks)
        {
            var span = DateTime.Now - new DateTime(ticks);
            if (span.TotalMinutes < 1) return Loc.T("abhi abhi");
            if (span.TotalHours < 1) return Loc.T($"{(int)span.TotalMinutes} minute pehle");
            if (span.TotalDays < 1) return Loc.T($"{(int)span.TotalHours} ghante pehle");   // "1 ghante pehle" has its own entry
            return Loc.T($"{(int)span.TotalDays} din pehle");
        }

        public static string Clock(float hour)
        {
            int hh = Mathf.FloorToInt(hour) % 24, mm = Mathf.FloorToInt((hour - Mathf.Floor(hour)) * 60f);
            int h12 = hh % 12 == 0 ? 12 : hh % 12;
            return $"{h12}:{mm:00} {(hh < 12 ? "AM" : "PM")}";
        }

        [Serializable]
        class Wire
        {
            public float x, y, z, yaw, hour, play;
            public int weather;
            public string place;
            public long at;
            public static Wire From(Data d) => new Wire { x = d.pos.x, y = d.pos.y, z = d.pos.z, yaw = d.yaw, hour = d.hour, weather = d.weather, place = d.place, at = d.savedAt, play = d.playSeconds };
            public Data ToData() => new Data { pos = new Vector3(x, y, z), yaw = yaw, hour = hour, weather = weather, place = place ?? "", savedAt = at, playSeconds = play };
        }
    }

    /// <summary>Keeps the last spot the player stood on dry ground on foot, the place's name, the time played, and saves
    /// now and then (see SaveGame).</summary>
    public class SaveTracker : MonoBehaviour
    {
        public static SaveTracker Instance { get; private set; }
        public bool HasSafe { get; private set; }
        public Vector3 SafePos { get; private set; }
        public float SafeYaw { get; private set; }
        public string Place { get; private set; } = "";
        /// <summary>Off while the front end (menu, intro) has the screen.</summary>
        public bool Playing;
        float sample, autosave = 90f;

        void Awake() { Instance = this; }
        void OnEnable() => GameEvents.LocationChanged += OnPlace;
        void OnDisable() => GameEvents.LocationChanged -= OnPlace;
        void OnPlace(string n) => Place = n;

        void Update()
        {
            if (!Playing) return;
            SaveGame.PlaySeconds += Time.unscaledDeltaTime;
            var p = PlayerController.Instance;
            sample -= Time.deltaTime;
            if (p && sample <= 0f)
            {
                sample = 1f;
                var motor = p.GetComponent<CharacterMotor>();
                bool grounded = !motor || motor.Grounded;
                if (p.CurrentMode == PlayerController.Mode.Normal && !p.InWater && grounded && !Jetpack.Flying)
                {
                    HasSafe = true;
                    SafePos = p.transform.position;
                    SafeYaw = p.transform.eulerAngles.y;
                }
            }
            autosave -= Time.unscaledDeltaTime;
            if (autosave <= 0f) { autosave = 90f; if (HasSafe) SaveGame.Save(); }
        }

        void OnApplicationPause(bool paused) { if (paused && Playing && HasSafe) SaveGame.Save(); }
        void OnApplicationQuit() { if (Playing && HasSafe) SaveGame.Save(); }
    }
}
