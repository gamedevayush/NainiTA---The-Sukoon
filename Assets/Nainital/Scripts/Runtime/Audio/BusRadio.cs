using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Nainital
{
    /// <summary>
    /// A bus's own radio (owner, 23 Nov 2026: "bus ke radio pe lgaao...ye rights nhi hai...isiliye apne folder me rkhna as
    /// bus.mp3 baaki logo se kehdenge ki bus.mp3 naam se jo hoga vo bus me chlega..doppler effect na dena"). The song is
    /// never in the game or the repo: each player puts his own file named bus.mp3 in the game's folder - PC:
    /// Documents\NainiTA\bus.mp3; Android: the game's files folder; iPhone: the game's folder in the Files app
    /// (Application.persistentDataPath). It plays, looping, from the front of the bus he rides in (from when he boards), heard
    /// all through it and fading away outside; no Doppler.
    /// </summary>
    public class BusRadio : MonoBehaviour
    {
        public static string SongPath => Application.isMobilePlatform
            ? Path.Combine(Application.persistentDataPath, "bus.mp3")
            : Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "NainiTA", "bus.mp3");

        static AudioClip song;
        static bool loading, missingSaid;
        AudioSource src;

        /// <summary>This bus's radio on (BusPassenger, when he gets aboard).</summary>
        public static void On(GameObject bus)
        {
            if (!bus) return;
            var r = bus.GetComponent<BusRadio>();
            if (!r) r = bus.AddComponent<BusRadio>();
            r.enabled = true;
        }

        void OnEnable()
        {
            if (!src)
            {
                // a real 3D sound at the driver (owner, 23 Nov 2026: "3d sound chahiye jo driver ke pass ho"): on the dashboard
                // in front of the driver's seat (the setup's seat 0; else the right-hand front), loud by him, a third as
                // loud at the back seats, fading away outside; from his direction in the first-person view
                var holder = new GameObject("Bus radio").transform;
                holder.SetParent(transform, false);
                var vp = GetComponent<VehicleParts>();
                var info = GetComponent<VehicleInfo>();
                Vector3 at;
                if (vp && vp.HasSetup && vp.SeatData(0) != null)
                    at = transform.InverseTransformPoint(vp.Frame.TransformPoint(vp.SeatData(0).pos)) + new Vector3(0f, 1.15f, 0.65f);
                else
                    at = new Vector3((info ? info.width : 2.4f) * 0.25f, 1.9f, (info ? info.length : 9f) * 0.42f);
                holder.localPosition = at;
                src = holder.gameObject.AddComponent<AudioSource>();
                src.loop = true;
                src.playOnAwake = false;
                src.spatialBlend = 1f;       // all 3D
                src.dopplerLevel = 0f;       // no Doppler (owner)
                src.spread = 30f;            // a cabin speaker, not a point
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                src.minDistance = 2.5f;      // full within 2.5 m of it; ~0.35 at the back of a bus
                src.maxDistance = 60f;
                Debug.Log($"[Nainital] bus radio on {name}: at {at:F2} (the driver's dashboard)");
            }
            if (song) Play();
            else if (!loading) StartCoroutine(Load());
        }

        void Play()
        {
            if (!src || !song) return;
            src.clip = song;
            src.volume = 0.9f * GameSettings.Master;
            if (src.isPlaying) return;
            // somewhere in the song, not its start: a radio that was on before he got in (owner, 23 Nov 2026: "aisa na lge ki
            // abhi shuru hua...poore gaane ko detect kro ki kiti der ka hai or kahin se bhi chlaado")
            src.time = Random.Range(0f, Mathf.Max(0f, song.length - 3f));
            src.Play();
        }

        float retryT;

        IEnumerator Load()
        {
            string path = SongPath;
            if (!File.Exists(path))
            {
                if (!missingSaid)
                {
                    missingSaid = true;
                    GameEvents.ShowToast("Bus radio: apna gaana 'bus.mp3' naam se yahan rakho - " + path, 5f);
                    Debug.Log("[Nainital] bus radio: no song at " + path);
                }
                yield break;
            }
            loading = true;
            using (var req = UnityWebRequestMultimedia.GetAudioClip("file://" + path.Replace('\\', '/'), AudioType.MPEG))
            {
                if (req.downloadHandler is DownloadHandlerAudioClip dh) dh.compressed = true;   // kept as mp3 in memory
                yield return req.SendWebRequest();
                loading = false;
                if (req.result != UnityWebRequest.Result.Success) { Debug.Log("[Nainital] bus radio: could not play " + path + ": " + req.error); yield break; }
                song = DownloadHandlerAudioClip.GetContent(req);
                if (song) song.name = "bus.mp3";
            }
            Debug.Log($"[Nainital] bus radio: {path}, {(song ? song.length : 0f):F0} s");
            foreach (var r in FindObjectsByType<BusRadio>(FindObjectsSortMode.None)) if (r.enabled) r.Play();
        }

        void Update()
        {
            if (src && src.isPlaying) src.volume = 0.9f * GameSettings.Master;
            // no song yet: the file may be put there while he rides (looked for every 2 s)
            if (!song && !loading && (retryT -= Time.unscaledDeltaTime) <= 0f)
            {
                retryT = 2f;
                if (File.Exists(SongPath)) StartCoroutine(Load());
            }
        }
    }
}
