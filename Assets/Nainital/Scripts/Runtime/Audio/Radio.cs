using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;

namespace Nainital
{
    /// <summary>
    /// The car radio (owner, 21 Nov 2026, FREEROAM #9: "Spotify cannot be used ... a local music folder, like SA's User
    /// Tracks: Android - the game's Music folder, iPhone - a folder in the Files app, PC - any folder; plus as many free-
    /// licence stations as the licences allow"). Plays while the player drives a car / bus / auto or rides a bike, fades
    /// out on getting off. R on a PC (or the phone's Radio app, the touch drive panel's button) goes to the next station;
    /// the last one is "Radio band" (off).
    /// Stations: "Apne Gaane" - the player's own files (PC: Documents\NainiTA\Music; Android: the game's files/Music;
    /// iPhone: the game's Documents/Music, visible in the Files app - Editor/IosFileSharing.cs), shuffled; then every folder
    /// under Resources/Radio/ (a station per folder, its name from Resources/Radio/stations.txt "folder = Name"; tracks
    /// with a free licence only, credited in Resources/Radio/CREDITS.txt). A station keeps "playing" while you are on
    /// another: coming back starts where it would be by now, like GTA.
    /// Every time he gets into a vehicle (owner, 23 Nov 2026: "koi bhi vehicle me random song, and bus me bus.mp3 hi hmesha
    /// from random place"): his own songs, a random one, from somewhere in its middle (a radio that was already on) - not
    /// the station of the last ride, nor "Radio band"; R still changes it for this ride. No own songs: the first bundled
    /// station. A bus has its own radio instead (BusRadio: his bus.mp3), driven or ridden.
    /// </summary>
    public class Radio : MonoBehaviour
    {
        public static Radio Instance { get; private set; }
        public static void Ensure() { if (!Instance) Instance = new GameObject("Radio").AddComponent<Radio>(); }

        class Station { public string key, folder; public AudioClip[] clips = new AudioClip[0]; public bool user; public float start; public List<string> files; }
        readonly List<Station> stations = new List<Station>();
        int current;
        AudioSource src;
        float vol;
        int userIndex = -1;
        bool inVehicle, loading, fromMiddle;
        CarController busFor;
        int userFails;   // own files in a row that would not play (an .m4a on a PC): after all of them, the first station
        Coroutine userLoad;

        public static string UserFolder
        {
            get
            {
                if (Application.isMobilePlatform) return Path.Combine(Application.persistentDataPath, "Music");
                return Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "NainiTA", "Music");
            }
        }

        /// <summary>PC only: more folders for "Apne Gaane", one path a line, in Documents\NainiTA\music_folders.txt (the
        /// owner's answer was "PC = any folder"; 22 Nov he added a folder of his own songs). Read with their sub-folders.</summary>
        public static string FoldersFile => Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "NainiTA", "music_folders.txt");

        /// <summary>Every folder "Apne Gaane" plays from: the game's Music folder, then (PC) those in FoldersFile.</summary>
        public static List<string> UserFolders()
        {
            var l = new List<string> { UserFolder };
            if (Application.isMobilePlatform) return l;
            try
            {
                if (File.Exists(FoldersFile))
                    foreach (var raw in File.ReadAllLines(FoldersFile))
                    {
                        string f = raw.Trim().Trim('"');
                        if (f.Length == 0 || f.StartsWith("#") || !Directory.Exists(f) || l.Contains(f)) continue;
                        l.Add(f);
                    }
            }
            catch { }
            return l;
        }

        /// <summary>How many of the player's own songs there are (the phone's Radio app shows it).</summary>
        public int UserCount => stations.Count > 0 ? UserFiles().Count : 0;

        public string CurrentKey => current < stations.Count ? stations[current].key : "Radio band";
        public IReadOnlyList<string> StationKeys { get { var l = new List<string>(); foreach (var s in stations) l.Add(s.key); l.Add("Radio band"); return l; } }
        public int CurrentIndex => current;

        void Awake()
        {
            Instance = this;
            src = gameObject.AddComponent<AudioSource>();
            src.spatialBlend = 0f;
            src.loop = false;
            src.playOnAwake = false;
            try { Directory.CreateDirectory(UserFolder); } catch { }
            stations.Add(new Station { key = "Apne Gaane", user = true });
            // the bundled free-licence stations
            var names = new Dictionary<string, string>();
            var list = Resources.Load<TextAsset>("Radio/stations");
            if (list)
                foreach (var line in list.text.Split('\n'))
                {
                    var kv = line.Split('=');
                    if (kv.Length == 2) names[kv[0].Trim()] = kv[1].Trim();
                }
            foreach (var kv in names)
            {
                var clips = Resources.LoadAll<AudioClip>("Radio/" + kv.Key);
                if (clips.Length == 0) continue;
                stations.Add(new Station { key = kv.Value, folder = kv.Key, clips = clips, start = Random.Range(0f, 600f) });
            }
            int first = stations.Count > 1 ? 1 : 0;
            if (!PlayerPrefs.HasKey("nt_radio") && HasUserSongs()) first = 0;   // own songs first when there are some
            current = Mathf.Clamp(PlayerPrefs.GetInt("nt_radio", first), 0, stations.Count);
        }

        void Update()
        {
            var drv = CarController.Driving;
            bool bus = drv != null && drv.type == CarType.Bus;   // a bus plays its own bus.mp3 (BusRadio)
            if (bus && busFor != drv) BusRadio.On(drv.gameObject);
            busFor = bus ? drv : null;
            bool now = (drv != null && !bus) || PlayerBike.Riding != null;
            if (now && !inVehicle)
            {
                inVehicle = true;
                current = UserFiles().Count > 0 ? 0 : stations.Count > 1 ? 1 : 0;   // his own songs, every ride
                Tune(false);
            }
            else if (!now && inVehicle) { inVehicle = false; }
            var kb = Keyboard.current;
            if (inVehicle && kb != null && kb.rKey.wasPressedThisFrame && !GameInput.UiBlocking) Next();
            float target = inVehicle && current < stations.Count ? 0.55f * GameSettings.Master : 0f;
            vol = Mathf.MoveTowards(vol, target, Time.unscaledDeltaTime * 0.8f);
            src.volume = vol;
            if (vol <= 0.001f && src.isPlaying && !inVehicle) src.Pause();
            if (inVehicle && current < stations.Count && !src.isPlaying && !loading) PlayNext();
        }

        static bool HasUserSongs()
        {
            foreach (var folder in UserFolders())
            {
                try
                {
                    foreach (var f in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
                    {
                        string e = Path.GetExtension(f).ToLowerInvariant();
                        if (e == ".mp3" || e == ".ogg" || e == ".wav") return true;
                    }
                }
                catch { }
            }
            return false;
        }

        public void Next() => Select((current + 1) % (stations.Count + 1));

        public void Select(int i)
        {
            current = Mathf.Clamp(i, 0, stations.Count);
            PlayerPrefs.SetInt("nt_radio", current);
            Tune(true);
        }

        void Tune(bool announce)
        {
            src.Stop();
            if (current >= stations.Count)
            {
                if (announce) GameEvents.ShowToast(Loc.T("Radio band"), 1.5f);
                return;
            }
            var s = stations[current];
            if (announce || inVehicle) GameEvents.ShowToast(Loc.F("Radio: {0}", Loc.T(s.key)), 2f);
            if (s.user)
            {
                // a random one of his songs from a random place in it
                int n = UserFiles().Count;
                userIndex = n > 0 ? Random.Range(0, n) - 1 : -1;
                fromMiddle = true;
                PlayUser();
                return;
            }
            // where this station would be by now: its tracks played back to back since it "started"
            float total = 0f;
            foreach (var c in s.clips) total += c.length;
            if (total <= 0f) return;
            float t = Mathf.Repeat(Time.unscaledTime + s.start, total);
            for (int k = 0; k < s.clips.Length; k++)
            {
                if (t < s.clips[k].length)
                {
                    src.clip = s.clips[k];
                    src.time = t;
                    src.Play();
                    s.files = null;
                    stationTrack = k;
                    return;
                }
                t -= s.clips[k].length;
            }
        }

        int stationTrack;
        void PlayNext()
        {
            var s = stations[current];
            if (s.user) { PlayUser(); return; }
            if (s.clips.Length == 0) return;
            stationTrack = (stationTrack + 1) % s.clips.Length;
            src.clip = s.clips[stationTrack];
            src.time = 0f;
            src.Play();
        }

        // ------------------------------------------------------------------ the player's own music
        /// <summary>The player's own songs, looked up once (every folder of UserFolders) and shuffled.</summary>
        List<string> UserFiles()
        {
            var s = stations[0];
            if (s.files == null)
            {
                s.files = new List<string>();
                foreach (var folder in UserFolders())
                {
                    try
                    {
                        foreach (var f in Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories))
                        {
                            string e = Path.GetExtension(f).ToLowerInvariant();
                            if (e == ".mp3" || e == ".ogg" || e == ".wav" || e == ".m4a" || e == ".aac") s.files.Add(f);
                        }
                    }
                    catch { }
                }
                for (int i = s.files.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (s.files[i], s.files[j]) = (s.files[j], s.files[i]); }
            }
            return s.files;
        }

        void PlayUser()
        {
            var s = stations[0];
            UserFiles();
            if (s.files.Count == 0)
            {
                GameEvents.ShowToast(Application.isMobilePlatform ? "Apne gaane game ke Music folder mein daalo (Files app > NainiTA > Music)" : "Apne gaane Documents\\NainiTA\\Music mein daalo", 4f);
                current = stations.Count > 1 ? 1 : stations.Count;
                if (current < stations.Count) Tune(false);
                return;
            }
            if (userFails >= s.files.Count)
            {
                userFails = 0;
                GameEvents.ShowToast("Ye gaane chal nahi paaye (mp3, ogg ya wav daalo)", 4f);
                current = stations.Count > 1 ? 1 : stations.Count;
                if (current < stations.Count) Tune(false);
                return;
            }
            userIndex = (userIndex + 1) % s.files.Count;
            if (userLoad != null) StopCoroutine(userLoad);
            userLoad = StartCoroutine(LoadUser(s.files[userIndex]));
        }

        IEnumerator LoadUser(string path)
        {
            loading = true;
            string e = Path.GetExtension(path).ToLowerInvariant();
            var type = e == ".mp3" ? AudioType.MPEG : e == ".ogg" ? AudioType.OGGVORBIS : e == ".wav" ? AudioType.WAV : AudioType.ACC;
            // a real file URI: "file:///C:/Users/Some%20Name/..." (a plain "file://" + a Windows path with a space fails)
            string uri;
            try { uri = new System.Uri(path).AbsoluteUri; } catch { uri = "file://" + path; }
            using (var req = UnityWebRequestMultimedia.GetAudioClip(uri, type))
            {
                // the whole song, kept compressed in memory (a few MB): a streamed clip cannot start from its middle
                ((DownloadHandlerAudioClip)req.downloadHandler).compressed = true;
                yield return req.SendWebRequest();
                loading = false;
                if (req.result != UnityWebRequest.Result.Success) { userFails++; yield break; }   // Update moves on to the next file
                var clip = DownloadHandlerAudioClip.GetContent(req);
                if (!clip) { userFails++; yield break; }
                userFails = 0;
                if (current != 0) yield break;
                if (src.clip && src.clip != clip && stations[0].user && src.clip.name.StartsWith("user:")) Destroy(src.clip);
                clip.name = "user:" + Path.GetFileNameWithoutExtension(path);
                src.clip = clip;
                src.time = fromMiddle ? Random.Range(0f, Mathf.Max(0f, clip.length - 10f)) : 0f;
                fromMiddle = false;
                src.Play();
                Debug.Log($"[Nainital] radio: {clip.name} ({clip.length:F0} s) from {src.time:F0} s");
                GameEvents.ShowToast(Loc.F("Ab baj raha hai: {0}", Path.GetFileNameWithoutExtension(path)), 2.5f);
            }
        }

    }
}
