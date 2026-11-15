using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// The HUD's own look (user, 2026-11-15: a full open-world HUD): dark glass panels with rounded corners, a lake-teal
    /// accent, marigold for what matters (money, missions), coral for health and danger, the Rajdhani typeface (Indian
    /// Type Foundry, SIL OFL, Latin + Devanagari + the rupee sign; Assets/Resources/Fonts). Every sprite is drawn here in
    /// code, anti-aliased, so the HUD needs no image files.
    /// </summary>
    public static class HudStyle
    {
        // ---- colours
        public static readonly Color Glass = new Color(0.035f, 0.05f, 0.07f, 0.74f);
        public static readonly Color GlassDeep = new Color(0.03f, 0.045f, 0.065f, 0.93f);
        public static readonly Color GlassLine = new Color(1f, 1f, 1f, 0.10f);
        public static readonly Color Teal = new Color(0.20f, 0.82f, 0.76f);
        public static readonly Color Marigold = new Color(1.00f, 0.72f, 0.16f);
        public static readonly Color Coral = new Color(0.98f, 0.36f, 0.32f);
        public static readonly Color Ink = new Color(0.97f, 0.96f, 0.93f);
        public static readonly Color InkDim = new Color(0.72f, 0.76f, 0.79f);
        public static readonly Color InkFaint = new Color(1f, 1f, 1f, 0.38f);
        public static readonly Color Good = new Color(0.45f, 0.92f, 0.55f);

        // ---- fonts
        static Font regular, bold;
        public static Font Regular => regular ? regular : regular = Load("Fonts/Rajdhani-SemiBold");
        public static Font Bold => bold ? bold : bold = Load("Fonts/Rajdhani-Bold");

        static Font Load(string path)
        {
            var f = Resources.Load<Font>(path);
            return f ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        // ---- sprites
        static Sprite rounded, roundedSmall, disc, ring, ringThin, vignette, glow, diamond, gradientH;
        /// <summary>A 9-sliced panel with 18 px corners (use Image.Type.Sliced).</summary>
        public static Sprite Rounded => rounded ? rounded : rounded = RoundedRect(64, 18);
        /// <summary>A 9-sliced pill or chip with 10 px corners.</summary>
        public static Sprite RoundedSmall => roundedSmall ? roundedSmall : roundedSmall = RoundedRect(32, 10);
        public static Sprite Disc => disc ? disc : disc = Ring(256, 0f);
        /// <summary>A ring 9% of the radius thick (gauges; Image.Type.Filled, Radial360).</summary>
        public static Sprite RingSprite => ring ? ring : ring = Ring(512, 0.09f);
        public static Sprite RingThin => ringThin ? ringThin : ringThin = Ring(512, 0.025f);
        /// <summary>Clear in the middle, opaque at the screen's edges (damage and low health).</summary>
        public static Sprite Vignette => vignette ? vignette : vignette = MakeVignette(256);
        /// <summary>A soft round glow.</summary>
        public static Sprite Glow => glow ? glow : glow = MakeGlow(128);
        public static Sprite Diamond => diamond ? diamond : diamond = MakeDiamond(64);
        /// <summary>Opaque on the left, clear on the right (a fading strip behind text).</summary>
        public static Sprite FadeRight => gradientH ? gradientH : gradientH = MakeFade(256);

        static Sprite RoundedRect(int n, int radius)
        {
            var t = NewTex(n, n);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, n - radius), cy = Mathf.Clamp(y + 0.5f, radius, n - radius);
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255f));
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
        }

        static Sprite Ring(int n, float thickness)
        {
            var t = NewTex(n, n);
            float r = n * 0.5f - 1.5f;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - n * 0.5f) * (x + 0.5f - n * 0.5f) + (y + 0.5f - n * 0.5f) * (y + 0.5f - n * 0.5f));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    if (thickness > 0f) a *= Mathf.Clamp01(d - (r - r * thickness) + 0.5f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeVignette(int n)
        {
            var t = NewTex(n, n);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(u * u * 0.85f + v * v);   // a little wider than tall, like the screen
                    // starts well out from the middle: on a 2.1:1 phone the old 0.55 reached the centre of the top edge
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 1.45f, d));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeGlow(int n)
        {
            var t = NewTex(n, n);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeDiamond(int n)
        {
            var t = NewTex(n, n);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = Mathf.Abs((x + 0.5f) / n * 2f - 1f), v = Mathf.Abs((y + 0.5f) / n * 2f - 1f);
                    float a = Mathf.Clamp01((1f - (u + v)) * n * 0.5f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeFade(int n)
        {
            var t = NewTex(n, 4);
            var px = new Color32[n * 4];
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < n; x++)
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.SmoothStep(1f, 0f, (x + 0.5f) / n) * 255f));
            t.SetPixels32(px);
            t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, n, 4), new Vector2(0.5f, 0.5f));
        }

        static Texture2D NewTex(int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        /// <summary>Money as the HUD shows it: the rupee sign and Indian digit grouping (1,23,450).</summary>
        public static string Rupees(int amount)
        {
            string s = Mathf.Abs(amount).ToString();
            if (s.Length > 3)
            {
                string last3 = s.Substring(s.Length - 3), rest = s.Substring(0, s.Length - 3);
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < rest.Length; i++)
                {
                    if (i > 0 && (rest.Length - i) % 2 == 0) sb.Append(',');
                    sb.Append(rest[i]);
                }
                s = sb + "," + last3;
            }
            return (amount < 0 ? "-" : "") + "₹" + s;
        }
    }
}
