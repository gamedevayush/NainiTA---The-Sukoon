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
        /// <summary>The bulbs by house doors (WorldBuilder.BuildPorchLights): small warm lights, the nearest few lit for real.</summary>
        public Vector3[] porchLights;
        public float porchRange = 8f;
        /// <summary>Coloured accent lights (WorldBuilder.Festive.cs: the bandstand's lamp, the walk's uplights, the temples,
        /// rooms): position, colour, and range / intensity each.</summary>
        public Vector3[] accentLights;
        public Color[] accentColors;
        public Vector2[] accentRanges;
        public float porchIntensity = 7f;
        static readonly Color PorchColor = new Color(1f, 0.76f, 0.46f);
        public int poolSize = 72;                // (56 left the street dark between lamps a little way off; a phone keeps 32)
        public float range = 34f;
        public float intensity = 90f;            // street lamps (1.5x the earlier 60)
        public float shopRange = 11f;
        public float shopIntensity = 26f;
        public Color color = new Color(1f, 0.70f, 0.38f);

        static readonly int GlowId = Shader.PropertyToID("_NtLampGlow");
        Light[] pool;
        int[] nearest;
        float[] best;
        float timer;

        int ShopCount => shopLights?.Length ?? 0;
        int PorchCount => porchLights?.Length ?? 0;
        int AccentStart => (lampHeads?.Length ?? 0) + ShopCount + PorchCount;
        int Count => AccentStart + (accentLights?.Length ?? 0);
        Vector3 Pos(int i) => i < lampHeads.Length ? lampHeads[i] : i < lampHeads.Length + ShopCount ? shopLights[i - lampHeads.Length]
                            : i < AccentStart ? porchLights[i - lampHeads.Length - ShopCount] : accentLights[i - AccentStart];
        bool IsShop(int i) => i >= lampHeads.Length && i < lampHeads.Length + ShopCount;
        bool IsPorch(int i) => i >= lampHeads.Length + ShopCount && i < AccentStart;
        bool IsAccent(int i) => i >= AccentStart;
        Color Col(int i)
        {
            if (IsAccent(i)) { int a = i - AccentStart; return accentColors != null && a < accentColors.Length ? accentColors[a] : color; }
            if (IsPorch(i)) return PorchColor;
            if (!IsShop(i)) return lampColors != null && i < lampColors.Length ? lampColors[i] : color;
            int k = i - lampHeads.Length;
            return shopColors != null && k < shopColors.Length ? shopColors[k] : new Color(1f, 0.88f, 0.7f);
        }

        Vector2 AccentRange(int i) { int a = i - AccentStart; return accentRanges != null && a < accentRanges.Length ? accentRanges[a] : new Vector2(6f, 8f); }

        /// <summary>The cinema camera (CinemaRunner): the nearest this many street lamps cast soft shadows (owner, 27 Nov
        /// 2026: "pole light wgera sbki shadows on hai?"). 0 = none, as in the game (a point light's shadow is six maps).
        /// Shop, porch and accent lights never: they sit inside the shop fronts and walls.</summary>
        public static int ShadowLamps;

        void Start()
        {
            if (lampHeads == null) lampHeads = new Vector3[0];
            poolSize = Mathf.Max(poolSize, 72);   // (the scene keeps the old 56 / 30 m it was built with)
            range = Mathf.Max(range, 34f);
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
                    else if (IsPorch(i)) d *= 2.6f;   // a door's bulb only close by
                    else if (IsAccent(i)) d *= 1.8f;  // the coloured accents near by
                    if (d >= best[best.Length - 1]) continue;
                    int k = best.Length - 1;
                    while (k > 0 && best[k - 1] > d) { best[k] = best[k - 1]; nearest[k] = nearest[k - 1]; k--; }
                    best[k] = d;
                    nearest[k] = i;
                }
                int shadowed = 0;
                for (int i = 0; i < pool.Length; i++)
                {
                    int j = nearest[i];
                    var l = pool[i];
                    if (j < 0) { if (l.shadows != LightShadows.None) l.shadows = LightShadows.None; continue; }
                    bool shop = IsShop(j), porch = IsPorch(j), accent = IsAccent(j);
                    bool sh = ShadowLamps > 0 && !shop && !porch && !accent && shadowed < ShadowLamps;
                    if (sh) shadowed++;
                    var want = sh ? LightShadows.Soft : LightShadows.None;
                    if (l.shadows != want) l.shadows = want;
                    l.transform.position = Pos(j) + (shop || accent ? Vector3.zero : porch ? Vector3.down * 0.1f : Vector3.down * 0.4f);
                    l.color = Col(j);
                    l.range = accent ? AccentRange(j).x : shop ? shopRange : porch ? porchRange : range;
                }
            }
            for (int i = 0; i < pool.Length; i++)
            {
                bool en = on > 0f && nearest[i] >= 0;
                if (pool[i].enabled != en) pool[i].enabled = en;
                if (!en) continue;
                // fade the farthest pooled lights so swapping is invisible
                float fade = 1f - Mathf.Clamp01((i - pool.Length * 0.7f) / (pool.Length * 0.3f));
                int j2 = nearest[i];
                pool[i].intensity = (IsAccent(j2) ? AccentRange(j2).y : IsShop(j2) ? shopIntensity : IsPorch(j2) ? porchIntensity : intensity) * on * fade;
            }
        }
    }
}
