using UnityEngine;

namespace Nainital
{
    /// <summary>A mission marker fades out as the camera comes within a few metres (phone play test, 2026-11-11: two
    /// givers beside the start point filled the screen with yellow).</summary>
    public class MarkerFade : MonoBehaviour
    {
        public float near = 8f, far = 16f;   // gone inside 8 m: its colour alone did not thin it enough on screen
        Renderer r;
        Material mat;   // its own copy: a property block was ignored by this URP setup (play test, 2026-11-11)
        Color baseColor;
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        public float LastK = -1f;   // the last fade, for the play test
        public Material Mat => mat;

        void Awake()
        {
            r = GetComponent<Renderer>();
            if (r && r.sharedMaterial) { mat = r.material; baseColor = mat.HasProperty(ColorId) ? mat.GetColor(ColorId) : new Color(1f, 0.85f, 0.2f, 0.35f); }
        }

        void OnDestroy() { if (mat) Destroy(mat); }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (!r || !mat || !cam) return;
            Vector3 d = transform.position - cam.transform.position;
            d.y = 0f;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(near, far, d.magnitude));
            LastK = k;
            r.enabled = k > 0.05f;
            if (!r.enabled) return;
            var c = baseColor;
            c.a *= k;
            mat.SetColor(ColorId, c);
        }
    }
}
