using UnityEngine;

namespace Nainital
{
    /// <summary>Snow View ropeway cabin shuttling along the cable (with sag) between Mallital and Snow View.</summary>
    public class Ropeway : MonoBehaviour
    {
        public Vector3[] cable;      // polyline: lower station .. pylons .. upper station
        public Transform cabin;
        public float speed = 5f;
        public float pause = 8f;
        public float sag = 6f;

        float s, dir = 1f, wait, length;
        float[] cum;

        /// <summary>Waiting at a station (RopewayRide: boarding and getting off).</summary>
        public bool Docked => wait > 0f;
        public bool AtTop => length > 0f && s >= length - 0.01f;
        public void Hold(float seconds) => wait = Mathf.Max(wait, seconds);

        void Start()
        {
            pause = Mathf.Max(pause, 15f);   // time enough to walk up and board
            if (cable == null || cable.Length < 2 || !cabin) { enabled = false; return; }
            cum = new float[cable.Length];
            for (int i = 1; i < cable.Length; i++) cum[i] = cum[i - 1] + Vector3.Distance(cable[i - 1], cable[i]);
            length = cum[cable.Length - 1];
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

        void Update()
        {
            if (wait > 0f) { wait -= Time.deltaTime; return; }
            s += dir * speed * Time.deltaTime;
            if (s >= length || s <= 0f)
            {
                s = Mathf.Clamp(s, 0f, length);
                dir = -dir;
                wait = pause;
            }
            Vector3 p = Eval(s);
            Vector3 ahead = Eval(Mathf.Clamp(s + 1f, 0f, length)) - Eval(Mathf.Clamp(s - 1f, 0f, length));
            ahead.y = 0;
            cabin.position = p;
            if (ahead.sqrMagnitude > 1e-4f)
                cabin.rotation = Quaternion.LookRotation(ahead.normalized, Vector3.up) * Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 0.8f) * 1.5f);
        }
    }
}
