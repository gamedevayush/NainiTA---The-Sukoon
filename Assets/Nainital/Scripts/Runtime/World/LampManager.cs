using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// The town's lights at night. Every street lamp and every shop front is a light source; the nearest ones to the
    /// camera get a real point light, pooled and moved about, so the light falls properly on whatever is under it, a road
    /// that rises and falls, a wall, a car (user, 2026-11-12: a flat glow on the ground broke on uneven roads). Lamps
    /// are sodium yellow or white LED (about a third white); shop fronts throw warm or cool white light onto the street,
    /// and stay lit after the shops close. The bulbs' halos (Shaders/LampGlow) fade in with the same dusk value.
    /// </summary>
    public class LampManager : MonoBehaviour
    {
        public Vector3[] lampHeads;              // street lamps (bulb positions)
        public Color[] lampColors;               // one per lamp (sodium or LED)
        public Vector3[] shopLights;             // just outside each shop front, at the sign
        public Color[] shopColors;
        public int poolSize = 56;
        public float range = 30f;
        public float intensity = 90f;            // street lamps (1.5x the earlier 60)
        public float shopRange = 11f;
        public float shopIntensity = 26f;
        public Color color = new Color(1f, 0.70f, 0.38f);

        static readonly int GlowId = Shader.PropertyToID("_NtLampGlow");
        Light[] pool;
        int[] nearest;
        float[] best;
        float timer;

        int Count => (lampHeads?.Length ?? 0) + (shopLights?.Length ?? 0);
        Vector3 Pos(int i) => i < lampHeads.Length ? lampHeads[i] : shopLights[i - lampHeads.Length];
        bool IsShop(int i) => i >= lampHeads.Length;
        Color Col(int i)
        {
            if (!IsShop(i)) return lampColors != null && i < lampColors.Length ? lampColors[i] : color;
            int k = i - lampHeads.Length;
            return shopColors != null && k < shopColors.Length ? shopColors[k] : new Color(1f, 0.88f, 0.7f);
        }

        void Start()
        {
            if (lampHeads == null) lampHeads = new Vector3[0];
            if (Application.isMobilePlatform) poolSize = Mathf.Min(poolSize, 32);   // Forward+: many lights stay cheap
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
            if (pool == null || Count == 0) return;
            var dn = DayNightCycle.Instance;
            float on = dn ? Mathf.Clamp01((dn.NightFactor - 0.2f) / 0.4f) : 0f;
            Shader.SetGlobalFloat(GlowId, on);   // the bulbs' halos (Shaders/LampGlow)
            timer -= Time.deltaTime;
            if (timer <= 0f && on > 0f)
            {
                timer = 0.3f;
                var cam = Camera.main;
                Vector3 c = cam ? cam.transform.position + cam.transform.forward * 15f : Vector3.zero;
                for (int i = 0; i < pool.Length; i++) { nearest[i] = -1; best[i] = float.MaxValue; }
                int n = Count;
                for (int i = 0; i < n; i++)
                {
                    float d = (Pos(i) - c).sqrMagnitude;
                    if (IsShop(i)) d *= 1.6f;   // street lamps first, the nearer shop fronts too
                    if (d >= best[best.Length - 1]) continue;
                    int k = best.Length - 1;
                    while (k > 0 && best[k - 1] > d) { best[k] = best[k - 1]; nearest[k] = nearest[k - 1]; k--; }
                    best[k] = d;
                    nearest[k] = i;
                }
                for (int i = 0; i < pool.Length; i++)
                {
                    int j = nearest[i];
                    if (j < 0) continue;
                    var l = pool[i];
                    bool shop = IsShop(j);
                    l.transform.position = Pos(j) + (shop ? Vector3.zero : Vector3.down * 0.4f);
                    l.color = Col(j);
                    l.range = shop ? shopRange : range;
                }
            }
            for (int i = 0; i < pool.Length; i++)
            {
                bool en = on > 0f && nearest[i] >= 0;
                if (pool[i].enabled != en) pool[i].enabled = en;
                if (!en) continue;
                // fade the farthest pooled lights so swapping is invisible
                float fade = 1f - Mathf.Clamp01((i - pool.Length * 0.7f) / (pool.Length * 0.3f));
                pool[i].intensity = (IsShop(nearest[i]) ? shopIntensity : intensity) * on * fade;
            }
        }
    }
}
