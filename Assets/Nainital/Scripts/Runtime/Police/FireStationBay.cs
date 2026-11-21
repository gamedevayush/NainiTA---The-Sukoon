using UnityEngine;

namespace Nainital
{
    /// <summary>The fire station's bay (LandmarkBuilder.FireStation): forward = out onto the road. The 101 tender drives out
    /// from here when the fire is far enough and nobody is looking at the bay, and goes back here after (FireTruck).</summary>
    public class FireStationBay : MonoBehaviour
    {
        public static FireStationBay Instance { get; private set; }
        void OnEnable() => Instance = this;
        void OnDisable() { if (Instance == this) Instance = null; }

        /// <summary>The bay is on the screen (then the tender cannot just appear in it).</summary>
        public bool Seen()
        {
            var cam = Camera.main;
            if (!cam) return false;
            Vector3 vp = cam.WorldToViewportPoint(transform.position + Vector3.up * 1.5f);
            return vp.z > 0f && vp.z < 160f && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f;
        }
    }
}
