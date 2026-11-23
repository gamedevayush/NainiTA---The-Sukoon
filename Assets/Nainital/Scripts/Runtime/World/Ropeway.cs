using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// The Mallital - Snow View ropeway: two cabins on two tracks, a jig-back as the real one (owner, 23 Nov 2026:
    /// "ropeways interactable bnaao... proper both sides entry"; the 2024 ride video D:\Unity\ref\yt_nov23\rope): when one
    /// cabin is up at Snow View the other is down at Mallital, they pass each other half way, wait at the stations together
    /// with their doors to the platform, then swap. cable = the line between the stations (pylons included); each cabin
    /// rides `trackGap / 2` to its own side of it. Sag on the long spans.
    /// </summary>
    public class Ropeway : MonoBehaviour
    {
        public Vector3[] cable;      // polyline: lower station .. pylons .. upper station
        public Transform cabin;      // cabin 0: starts at the lower station
        public Transform cabin2;     // cabin 1: starts at the upper station (optional)
        public float trackGap = 0f;  // metres between the two tracks
        public float speed = 5f;
        public float pause = 8f;
        public float sag = 6f;

        float s, dir = 1f, wait, length;
        float[] cum;

        /// <summary>Waiting at the stations (RopewayRide: boarding and getting off).</summary>
        public bool Docked => wait > 0f;
        /// <summary>Cabin 0 is at the top (and cabin 1 at the bottom).</summary>
        public bool AtTop => length > 0f && s >= length - 0.01f;
        /// <summary>Cabin `i` is at the top station.</summary>
        public bool AtTopOf(int i) => i == 0 ? AtTop : length > 0f && s <= 0.01f;
        public Transform Cabin(int i) => i == 0 ? cabin : cabin2;
        public float Wait => wait;
        public void Hold(float seconds) => wait = Mathf.Max(wait, seconds);

        void Start()
        {
            pause = Mathf.Max(pause, 20f);   // time enough to walk up the platform and board
            if (cable == null || cable.Length < 2 || !cabin) { enabled = false; return; }
            cum = new float[cable.Length];
            for (int i = 1; i < cable.Length; i++) cum[i] = cum[i - 1] + Vector3.Distance(cable[i - 1], cable[i]);
            length = cum[cable.Length - 1];
            Place();
        }

        Vector3 Eval(float d)
        {
            for (int i = 1; i < cable.Length; i++)
            {
                if (d <= cum[i] || i == cable.Length - 1)
                {
                    float segLen = cum[i] - cum[i - 1];
                    float t = segLen > 0 ? Mathf.Clamp01((d - cum[i - 1]) / segLen) : 0f;
                    Vector3 p = Vector3.Lerp(cable[i - 1], cable[i], t);
                    p.y -= sag * 4f * t * (1f - t) * Mathf.Clamp01(segLen / 300f);
                    return p;
                }
            }
            return cable[cable.Length - 1];
        }

        /// <summary>The cable's level heading at d (from the lower station to the upper).</summary>
        Vector3 Ahead(float d)
        {
            Vector3 a = Eval(Mathf.Clamp(d + 1f, 0f, length)) - Eval(Mathf.Clamp(d - 1f, 0f, length));
            a.y = 0f;
            return a.sqrMagnitude > 1e-4f ? a.normalized : Vector3.forward;
        }

        void Update()
        {
            if (wait > 0f) { wait -= Time.deltaTime; Place(); return; }
            // ease out of and into the stations
            float edge = Mathf.Min(s, length - s);
            float v = speed * Mathf.Clamp(edge / 12f + 0.15f, 0.15f, 1f);
            s += dir * v * Time.deltaTime;
            if (s >= length || s <= 0f)
            {
                s = Mathf.Clamp(s, 0f, length);
                dir = -dir;
                wait = pause;
            }
            Place();
        }

        void Place()
        {
            Put(cabin, s, +1f);
            if (cabin2) Put(cabin2, length - s, -1f);
        }

        void Put(Transform c, float d, float side)
        {
            if (!c) return;
            Vector3 ahead = Ahead(d);
            Vector3 right = Vector3.Cross(Vector3.up, ahead);
            float swing = Docked ? 0f : Mathf.Sin(Time.time * 0.8f + side) * 1.5f;
            c.position = Eval(d) + right * side * trackGap * 0.5f;
            c.rotation = Quaternion.LookRotation(ahead, Vector3.up) * Quaternion.Euler(0, 0, swing);
        }
    }
}
