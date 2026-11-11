using UnityEngine;

namespace Nainital
{
    /// <summary>Snow View's "360" tower ride: the ring-shaped gondola waits at the foot, climbs the lattice tower turning
    /// slowly all the way round, holds at the top for the view, and comes back down.</summary>
    public class Tower360Ride : MonoBehaviour
    {
        public float bottom = 0.6f, top = 30f, cycle = 100f, turn = 36f;   // metres; seconds per ride; seconds per full turn
        public float offset;

        Vector3 home;

        void Start() => home = transform.localPosition;

        void Update()
        {
            float t = Mathf.Repeat(Time.time + offset, cycle) / cycle;
            float h = t < 0.15f ? 0f : t < 0.45f ? Mathf.SmoothStep(0f, 1f, (t - 0.15f) / 0.3f) : t < 0.7f ? 1f : Mathf.SmoothStep(1f, 0f, (t - 0.7f) / 0.3f);
            transform.localPosition = home + Vector3.up * Mathf.Lerp(bottom, top, h);
            float spin = t < 0.15f ? 0f : (Time.time + offset) * 360f / turn;
            transform.localRotation = Quaternion.Euler(0f, spin, 0f);
        }
    }
}
