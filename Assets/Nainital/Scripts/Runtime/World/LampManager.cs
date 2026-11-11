using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// ~1000 street lamps glow through the emissive atlas; the nearest ones get a real sodium-orange
    /// point light (pooled and moved around the camera) so roads get warm pools of light at night.
    /// </summary>
    public class LampManager : MonoBehaviour
    {
        public Vector3[] lampHeads;
        public int poolSize = 36;
        public float range = 26f;
        public float intensity = 22f;
        public Color color = new Color(1f, 0.70f, 0.38f);

        static readonly int GlowId = Shader.PropertyToID("_NtLampGlow");
        Light[] pool;
        int[] nearest;
        float[] best;
        float timer;

        void Start()
        {
            if (Application.isMobilePlatform) poolSize = Mathf.Min(poolSize, 16);
            pool = new Light[poolSize];
            nearest = new int[poolSize];
            best = new float[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject("LampLight" + i);
                go.transform.SetParent(transform, false);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = range;
                l.color = color;
                l.intensity = 0f;
                l.shadows = LightShadows.None;
                l.enabled = false;
                pool[i] = l;
                nearest[i] = -1;
            }
        }

        void Update()
        {
            if (lampHeads == null || lampHeads.Length == 0 || pool == null) return;
            var dn = DayNightCycle.Instance;
            float on = dn ? Mathf.Clamp01((dn.NightFactor - 0.2f) / 0.4f) : 0f;
            Shader.SetGlobalFloat(GlowId, on);   // halos and pools of light on the roads (Shaders/LampGlow)
            timer -= Time.deltaTime;
            if (timer <= 0f && on > 0f)
            {
                timer = 0.3f;
                var cam = Camera.main;
                Vector3 c = cam ? cam.transform.position + cam.transform.forward * 15f : Vector3.zero;
                for (int i = 0; i < pool.Length; i++) { nearest[i] = -1; best[i] = float.MaxValue; }
                for (int i = 0; i < lampHeads.Length; i++)
                {
                    float d = (lampHeads[i] - c).sqrMagnitude;
                    if (d >= best[best.Length - 1]) continue;
                    int k = best.Length - 1;
                    while (k > 0 && best[k - 1] > d) { best[k] = best[k - 1]; nearest[k] = nearest[k - 1]; k--; }
                    best[k] = d;
                    nearest[k] = i;
                }
                for (int i = 0; i < pool.Length; i++)
                    if (nearest[i] >= 0) pool[i].transform.position = lampHeads[nearest[i]] + Vector3.down * 0.4f;
            }
            for (int i = 0; i < pool.Length; i++)
            {
                bool en = on > 0f && nearest[i] >= 0;
                if (pool[i].enabled != en) pool[i].enabled = en;
                // fade the farthest pooled lights so swapping is invisible
                float fade = 1f - Mathf.Clamp01((i - pool.Length * 0.7f) / (pool.Length * 0.3f));
                pool[i].intensity = intensity * on * fade;
            }
        }
    }
}
