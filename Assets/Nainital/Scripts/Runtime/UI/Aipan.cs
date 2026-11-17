using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// The front end's Uttarakhandi look (user, 2026-11-17: "uttrakhandi rkhne ki koshish krna"): Aipan, the Kumaoni floor
    /// and wall art drawn in white rice paste (biswar) on red ochre (geru) - rings of dots, lotus petals, borders of lines
    /// and dots, and the Vasudhara lines that hang from a doorway. Drawn here in code, anti-aliased, so the menus need no
    /// image files; the colours of the Pichhoda (saffron), deodar green and the night over the lake go with them.
    /// </summary>
    public static class Aipan
    {
        // ---- colours
        public static readonly Color Geru = new Color(0.60f, 0.16f, 0.10f);
        public static readonly Color GeruDeep = new Color(0.36f, 0.08f, 0.05f);
        public static readonly Color Rice = new Color(0.98f, 0.95f, 0.88f);
        public static readonly Color Saffron = new Color(1.00f, 0.66f, 0.12f);
        public static readonly Color Deodar = new Color(0.09f, 0.27f, 0.20f);
        public static readonly Color Night = new Color(0.02f, 0.03f, 0.05f);

        static Sprite mandala, border, vasudhara, dotRow, corner, shade;

        /// <summary>A round Aipan chowki: lotus petals in rings of dots, white on clear (tint it).</summary>
        public static Sprite Mandala => mandala ? mandala : mandala = MakeMandala(512);
        /// <summary>A border strip to tile sideways (Image.Type.Tiled): geru with white lines, dots and diamonds.</summary>
        public static Sprite Border => border ? border : border = MakeBorder(128, 36);
        /// <summary>Vasudhara: a band with lines hanging from it, each ending in a dot, white on clear (tile sideways).</summary>
        public static Sprite Vasudhara => vasudhara ? vasudhara : vasudhara = MakeVasudhara(64, 96);
        /// <summary>A row of dots between two thin lines, white on clear (tile sideways).</summary>
        public static Sprite DotRow => dotRow ? dotRow : dotRow = MakeDotRow(48, 20);
        /// <summary>A corner motif (quarter rings of dots and petals), white on clear; flip it for the other corners.</summary>
        public static Sprite Corner => corner ? corner : corner = MakeCorner(256);
        /// <summary>Dark at the bottom, clear at the top (text over a photo).</summary>
        public static Sprite ShadeUp => shade ? shade : shade = MakeShade(8, 256);

        // ------------------------------------------------------------------ drawing
        static float Ring(float d, float r, float half) => Mathf.Clamp01(half - Mathf.Abs(d - r) + 0.5f);
        static float Dot(float d, float r) => Mathf.Clamp01(r - d + 0.5f);

        static Sprite MakeMandala(int n)
        {
            var px = new Color32[n * n];
            float c = n * 0.5f, R = n * 0.5f - 2f, s = 1f / R;   // s: one pixel in units of the radius
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float dpx = Mathf.Sqrt(dx * dx + dy * dy);   // pixels
                    float r = dpx * s;                            // 0..1
                    float th = Mathf.Atan2(dy, dx);
                    float a = 0f;
                    // the seed in the middle and its ring
                    a = Mathf.Max(a, Dot(dpx, R * 0.055f));
                    a = Mathf.Max(a, Ring(dpx, R * 0.10f, R * 0.010f));
                    // eight lotus petals, outlined, a dot in each
                    a = Mathf.Max(a, Petals(r, th, 8, 0f, 0.13f, 0.36f, 0.30f, R));
                    a = Mathf.Max(a, PetalDots(dx, dy, 8, 0f, 0.24f, 0.022f, R));
                    a = Mathf.Max(a, Ring(dpx, R * 0.40f, R * 0.008f));
                    // a ring of 24 dots
                    a = Mathf.Max(a, DotRing(dx, dy, 24, 0.445f, 0.016f, 0f, R));
                    // sixteen outer petals, between the first ones
                    a = Mathf.Max(a, Petals(r, th, 16, Mathf.PI / 16f, 0.49f, 0.68f, 0.17f, R));
                    a = Mathf.Max(a, PetalDots(dx, dy, 16, Mathf.PI / 16f, 0.585f, 0.013f, R));
                    a = Mathf.Max(a, Ring(dpx, R * 0.715f, R * 0.007f));
                    // 48 small dots, a scalloped line, 64 dots at the rim
                    a = Mathf.Max(a, DotRing(dx, dy, 48, 0.76f, 0.010f, 0f, R));
                    float sc = 0.835f + 0.035f * Mathf.Abs(Mathf.Cos(th * 12f));
                    a = Mathf.Max(a, Ring(dpx, R * sc, R * 0.0075f));
                    a = Mathf.Max(a, DotRing(dx, dy, 64, 0.94f, 0.012f, 0f, R));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            return Make(px, n, n, 0);
        }

        /// <summary>k petals between r0 and r1 (fractions of the radius), each spanning `width` of its sector at its widest,
        /// drawn as outlines a little over a pixel and a half thick.</summary>
        static float Petals(float r, float th, int k, float phase, float r0, float r1, float width, float R)
        {
            if (r < r0 - 0.02f || r > r1 + 0.02f) return 0f;
            float sector = Mathf.PI * 2f / k;
            float phi = Mathf.Repeat(th - phase + sector * 0.5f, sector) - sector * 0.5f;   // -sector/2 .. sector/2
            float u = Mathf.Clamp01((r - r0) / (r1 - r0));
            // an almond, pointed at the tip: wide near its base
            float half = Mathf.Sin(Mathf.Pow(u, 0.75f) * Mathf.PI) * sector * 0.5f * width / 0.5f * 0.5f;
            float distSide = Mathf.Abs(Mathf.Abs(phi) - half) * r * R;                   // pixels to the side line
            float a = r >= r0 && r <= r1 ? Mathf.Clamp01(R * 0.008f - distSide + 0.5f) : 0f;
            float tip = Mathf.Abs(phi) * r * R;
            a = Mathf.Max(a, Mathf.Clamp01(R * 0.008f - Mathf.Abs((r - r0) * R) + 0.5f) * (tip < half * r * R + 1f ? 1f : 0f));
            return a;
        }

        static float PetalDots(float dx, float dy, int k, float phase, float rr, float dot, float R)
        {
            float best = 0f;
            float th = Mathf.Atan2(dy, dx);
            float sector = Mathf.PI * 2f / k;
            float i = Mathf.Round((th - phase) / sector);
            float a0 = phase + i * sector;
            float cx = Mathf.Cos(a0) * rr * R, cy = Mathf.Sin(a0) * rr * R;
            float d = Mathf.Sqrt((dx - cx) * (dx - cx) + (dy - cy) * (dy - cy));
            best = Mathf.Max(best, Dot(d, dot * R));
            return best;
        }

        static float DotRing(float dx, float dy, int k, float rr, float dot, float phase, float R) => PetalDots(dx, dy, k, phase, rr, dot, R);

        static Sprite MakeBorder(int w, int h)
        {
            var px = new Color32[w * h];
            Color32 bg = Geru, fg = Rice;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f, a = 0f;
                    // two lines at each edge
                    a = Mathf.Max(a, Mathf.Clamp01(1.1f - Mathf.Abs(fy - 3.5f) + 0.5f));
                    a = Mathf.Max(a, Mathf.Clamp01(0.7f - Mathf.Abs(fy - 7.5f) + 0.5f));
                    a = Mathf.Max(a, Mathf.Clamp01(1.1f - Mathf.Abs(fy - (h - 3.5f)) + 0.5f));
                    a = Mathf.Max(a, Mathf.Clamp01(0.7f - Mathf.Abs(fy - (h - 7.5f)) + 0.5f));
                    float my = h * 0.5f;
                    // a diamond outline in the middle of the tile, a dot inside it
                    float dd = Mathf.Abs(fx - w * 0.5f) + Mathf.Abs(fy - my);
                    a = Mathf.Max(a, Mathf.Clamp01(1.0f - Mathf.Abs(dd - 9f) + 0.5f));
                    a = Mathf.Max(a, Dot(Mathf.Sqrt((fx - w * 0.5f) * (fx - w * 0.5f) + (fy - my) * (fy - my)), 2.4f));
                    // dots between the diamonds (at the tile's edges they meet the next tile's)
                    foreach (float ex in new[] { 0f, w * 0.25f, w * 0.75f, w })
                    {
                        float r = ex == 0f || ex == w ? 3f : 2f;
                        a = Mathf.Max(a, Dot(Mathf.Sqrt((fx - ex) * (fx - ex) + (fy - my) * (fy - my)), r));
                    }
                    // little ticks hanging off the inner lines, like the three-finger strokes
                    float tx = Mathf.Repeat(fx, 8f);
                    if (fy > 8f && fy < 11f || fy > h - 11f && fy < h - 8f) a = Mathf.Max(a, Mathf.Clamp01(0.6f - Mathf.Abs(tx - 4f) + 0.5f));
                    Color c = Color.Lerp(bg, fg, a);
                    px[y * w + x] = c;
                }
            return Make(px, w, h, 0, true);
        }

        static Sprite MakeVasudhara(int w, int h)
        {
            var px = new Color32[w * h];
            int[] len = { 70, 52, 34, 52, 70, 52, 34, 52 };   // eight lines a tile, long-short in a wave
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float fx = x + 0.5f, fy = h - (y + 0.5f);   // fy: down from the top
                    float a = 0f;
                    a = Mathf.Max(a, Mathf.Clamp01(1.3f - Mathf.Abs(fy - 4f) + 0.5f));
                    a = Mathf.Max(a, Mathf.Clamp01(0.8f - Mathf.Abs(fy - 10f) + 0.5f));
                    for (int i = 0; i < 8; i++)
                    {
                        float lx = 4f + i * 8f;
                        float L = len[i] * (h - 14f) / 76f + 10f;
                        if (fy > 10f && fy < L) a = Mathf.Max(a, Mathf.Clamp01(1.0f - Mathf.Abs(fx - lx) + 0.5f));
                        a = Mathf.Max(a, Dot(Mathf.Sqrt((fx - lx) * (fx - lx) + (fy - L - 2.5f) * (fy - L - 2.5f)), 2.6f));
                    }
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            return Make(px, w, h, 0, true);
        }

        static Sprite MakeDotRow(int w, int h)
        {
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f, a = 0f;
                    a = Mathf.Max(a, Mathf.Clamp01(0.6f - Mathf.Abs(fy - 2f) + 0.5f));
                    a = Mathf.Max(a, Mathf.Clamp01(0.6f - Mathf.Abs(fy - (h - 2f)) + 0.5f));
                    a = Mathf.Max(a, Dot(Mathf.Sqrt((fx - w * 0.25f) * (fx - w * 0.25f) + (fy - h * 0.5f) * (fy - h * 0.5f)), 3f));
                    a = Mathf.Max(a, Dot(Mathf.Sqrt((fx - w * 0.75f) * (fx - w * 0.75f) + (fy - h * 0.5f) * (fy - h * 0.5f)), 1.7f));
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            return Make(px, w, h, 0, true);
        }

        static Sprite MakeCorner(int n)
        {
            var px = new Color32[n * n];
            float R = n - 4f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // centred on the bottom-left corner: rings and petals spreading into the panel
                    float dx = x + 0.5f, dy = y + 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy), r = d / R, th = Mathf.Atan2(dy, dx), a = 0f;
                    a = Mathf.Max(a, Dot(d, R * 0.08f));
                    a = Mathf.Max(a, Ring(d, R * 0.15f, 1.2f));
                    a = Mathf.Max(a, Petals(r, th, 16, Mathf.PI / 16f, 0.19f, 0.46f, 0.32f, R));
                    a = Mathf.Max(a, PetalDots(dx, dy, 16, Mathf.PI / 16f, 0.31f, 0.018f, R));
                    a = Mathf.Max(a, Ring(d, R * 0.5f, 1.1f));
                    a = Mathf.Max(a, DotRing(dx, dy, 40, 0.56f, 0.014f, 0f, R));
                    float sc = 0.64f + 0.03f * Mathf.Abs(Mathf.Cos(th * 20f));
                    a = Mathf.Max(a, Ring(d, R * sc, 1.0f));
                    a = Mathf.Max(a, DotRing(dx, dy, 56, 0.74f, 0.011f, 0f, R));
                    a *= Mathf.Clamp01((0.8f - r) * 20f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            return Make(px, n, n, 0);
        }

        static Sprite MakeShade(int w, int h)
        {
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float t = (y + 0.5f) / h;                   // 0 bottom .. 1 top
                byte a = (byte)(Mathf.Pow(1f - t, 1.6f) * 255f);
                for (int x = 0; x < w; x++) px[y * w + x] = new Color32(255, 255, 255, a);
            }
            return Make(px, w, h, 0);
        }

        static Sprite Make(Color32[] px, int w, int h, int border, bool repeat = false)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 2,
            };
            if (repeat) t.wrapModeV = TextureWrapMode.Clamp;
            t.SetPixels32(px);
            t.Apply(true, true);
            return Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
    }
}
