using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Nainital
{
    /// <summary>
    /// A bus's own radio (owner, 23 Nov 2026: "bus ke radio pe lgaao...ye rights nhi hai...isiliye apne folder me rkhna as
    /// bus.mp3 baaki logo se kehdenge ki bus.mp3 naam se jo hoga vo bus me chlega..doppler effect na dena"; then "koi bhi
    /// vehicle me random song, and bus me bus.mp3 hi hmesha from random place"). Always the player's own bus.mp3 - never in
    /// the game or the repo (the repo is public; the song is not ours to give out): PC Documents\NainiTA\bus.mp3, phones the
    /// game's folder (Application.persistentDataPath). Read where it lies, nothing copied. On a loop, from a random place in
    /// it each time a bus's radio comes on (a radio that was already on), in the bus he rides (BusPassenger) or drives
    /// (Radio). A real 3D sound at the driver's dashboard, no Doppler. Every other vehicle: Radio, a random own song.
    /// </summary>
    public class BusRadio : MonoBehaviour
    {
        public static string SongPath => Application.isMobilePlatform
            ? Path.Combine(Application.persistentDataPath, "bus.mp3")
            : Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "NainiTA", "bus.mp3");

        static AudioClip song;
        static bool loading, missingSaid;
        AudioSource src;
        float retryT;

        /// <summary>This bus's radio on (BusPassenger when he gets aboard, Radio when he drives a bus).</summary>
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
                src.loop = true;             // the one song, round and round
                src.playOnAwake = false;
                src.spatialBlend = 1f;       // all 3D
                src.dopplerLevel = 0f;       // no Doppler (owner)
                src.spread = 30f;            // a cabin speaker, not a point
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                src.minDistance = 2.5f;      // full within 2.5 m of it; ~0.35 at the back of a bus
                src.maxDistance = 60f;
            }
            retryT = 0f;
        }

        void Update()
        {
            if (!src) return;
            src.volume = 0.9f * GameSettings.Master;
            if (src.isPlaying) return;
            if (song) { Play(); return; }
            if (loading) return;
            // not loaded (or not there yet): look again every few seconds, so a file put there while he rides starts
            if ((retryT -= Time.unscaledDeltaTime) > 0f) return;
            retryT = 3f;
            if (File.Exists(SongPath)) StartCoroutine(Load());
            else if (!missingSaid)
            {
                missingSaid = true;
                Debug.Log("[Nainital] bus radio: no " + SongPath);
                GameEvents.ShowToast("Bus radio: apna gaana 'bus.mp3' naam se yahan rakho - " + SongPath, 5f);
            }
        }

        void Play()
        {
            src.clip = song;
            // from a random place: a radio that was on before he got in (owner, 23 Nov 2026: "aisa na lge ki abhi shuru
            // hua...poore gaane ko detect kro ki kiti der ka hai or kahin se bhi chlaado")
            src.time = Random.Range(0f, Mathf.Max(0f, song.length - 10f));
            src.Play();
            Debug.Log($"[Nainital] bus radio: bus.mp3 ({song.length:F0} s) from {src.time:F0} s");
        }

        static IEnumerator Load()
        {
            loading = true;
            string uri;
            try { uri = new System.Uri(SongPath).AbsoluteUri; } catch { uri = "file://" + SongPath; }
            using (var req = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.MPEG))
            {
                ((DownloadHandlerAudioClip)req.downloadHandler).compressed = true;   // kept compressed in memory, seekable
                yield return req.SendWebRequest();
                var clip = req.result == UnityWebRequest.Result.Success ? DownloadHandlerAudioClip.GetContent(req) : null;
                if (clip && clip.length > 1f) { clip.name = "bus.mp3"; song = clip; }
                else Debug.Log("[Nainital] bus radio: could not play " + SongPath + " " + req.error);
            }
            loading = false;
        }
    }
}
