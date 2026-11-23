using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// The inside of a building you walk into (owner, 23 Nov 2026, the game parlour: "interior me day time me walls kyun
    /// chmak rhi hai? ... interior hai to day and night both me same hi hoga na? light hogi bhi to gate pe ...upar lights
    /// theek lgaai hain..but neeche tk jaada light aari nhi hai"). The sky's ambient light reaches everywhere in Unity, through
    /// walls too; while the camera is inside this box (local space, `size`), DayNightCycle turns the ambient down to a
    /// room's (InteriorDim), so the room looks the same at noon and at midnight, lit by its own lamps. Its own lamps
    /// (`lights`, real lights set by the builder) burn day and night, switched on only while the camera is within
    /// `wakeDistance` (no cost elsewhere).
    /// </summary>
    public class InteriorZone : MonoBehaviour
    {
        public Vector3 size = new Vector3(10f, 5f, 10f);
        public Light[] lights;
        public float wakeDistance = 60f;

        static int insideFrame = -10;
        /// <summary>The camera was inside some interior this frame or the last.</summary>
        public static bool CameraInside => Time.frameCount - insideFrame <= 1;

        bool lit = true;

        void Start()
        {
            // the room's echo (owner, 23 Nov: "reverb"): a reverb zone the size of the room
            var echo = gameObject.GetComponent<AudioReverbZone>() ?? gameObject.AddComponent<AudioReverbZone>();
            echo.reverbPreset = AudioReverbPreset.Room;
            echo.minDistance = Mathf.Min(size.x, size.z) * 0.45f;
            echo.maxDistance = Mathf.Max(size.x, size.z) * 0.55f + 2f;
        }

        void Update()
        {
            var cam = Camera.main;
            if (!cam) return;
            Vector3 l = transform.InverseTransformPoint(cam.transform.position);
            if (Mathf.Abs(l.x) < size.x * 0.5f && Mathf.Abs(l.z) < size.z * 0.5f && l.y > -0.5f && l.y < size.y) insideFrame = Time.frameCount;
            bool near = (cam.transform.position - transform.position).sqrMagnitude < wakeDistance * wakeDistance;
            if (near != lit && lights != null)
            {
                lit = near;
                foreach (var lt in lights) if (lt) lt.enabled = near;
            }
        }
    }
}
