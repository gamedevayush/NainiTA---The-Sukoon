using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>The painted side of every building (user, 2026-11-11: "map ko or realistic banao"). The old atlas gave each
    /// 3.2 m bay 64 pixels: a white cross on a flat colour. Here a bay is drawn in cell units (64 x 64 units, one unit =
    /// 5 cm) at whatever resolution the atlas has, with what the 2025-26 street videos show on Nainital's houses and
    /// hotels: weathered plaster with damp at the foot and streaks under every sill, painted timber windows (green, blue,
    /// maroon, white) set back in the wall with sills, glazing bars, curtains and iron grills, concrete sunshades,
    /// balconies with railings, washing and flower pots, panelled doors, open shops full of goods under painted boards and
    /// shut ones behind rolling shutters, and corrugated tin roofs with lapped sheets and rust. Pure functions of (u, v):
    /// no Unity objects, so the same code runs in a test harness outside the editor.</summary>
    public static class FacadeArt
    {
        public delegate float PhotoDevFn(string name, int size, int x, int y);
        public delegate Color PhotoRgbFn(string name, int size, int x, int y, Color fallback);
        /// <summary>Photo texture samplers (TextureKit's): luminance about the mean, and colour. May be null.</summary>
        public static PhotoDevFn Dev;
        public static PhotoRgbFn Rgb;

        // ------------------------------------------------------------------ noise
        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        static float VNoise(float x, float y, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            float a = Hash(x0, y0, seed), b = Hash(x0 + 1, y0, seed), c = Hash(x0, y0 + 1, seed), d = Hash(x0 + 1, y0 + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Fbm(float x, float y, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += VNoise(x, y, seed + o * 17) * amp;
                norm += amp;
                amp *= 0.5f;
                x *= 2f; y *= 2f;
            }
            return sum / norm;
        }

        static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);
        static Color Mix(Color a, Color b, float t) { t = Mathf.Clamp01(t); return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, 1f); }
        static bool In(float u, float v, float x0, float y0, float x1, float y1) => u >= x0 && u < x1 && v >= y0 && v < y1;
        static float Smooth(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }
        /// <summary>1 inside [a, b], falling to 0 over `soft` outside it.</summary>
        static float Band(float x, float a, float b, float soft) => Mathf.Clamp01(Mathf.Min(x - a, b - x) / soft + 1f);

        static readonly Color GlassDark = new Color(0.05f, 0.065f, 0.08f), GlassSky = new Color(0.40f, 0.47f, 0.54f);
        static readonly Color Lit = new Color(1f, 0.86f, 0.62f);
        static readonly Color Iron = new Color(0.09f, 0.10f, 0.10f), Concrete = new Color(0.66f, 0.64f, 0.60f);
        static readonly Color Moss = new Color(0.30f, 0.34f, 0.24f), Soot = new Color(0.16f, 0.15f, 0.14f);

        /// <summary>Paint for window and door joinery, by wall colour: Nainital's bottle green, blue, maroon, white.</summary>
        static readonly Color[] Trims =
        {
            new Color(0.90f, 0.89f, 0.85f), new Color(0.17f, 0.31f, 0.23f), new Color(0.84f, 0.80f, 0.69f), new Color(0.40f, 0.15f, 0.13f),
            new Color(0.21f, 0.33f, 0.47f), new Color(0.33f, 0.22f, 0.14f), new Color(0.88f, 0.87f, 0.83f), new Color(0.14f, 0.27f, 0.21f),
        };

        public static Color Trim(int wallIndex, Color wall)
        {
            Color t = Trims[(wallIndex * 5 + 3) % Trims.Length];
            // a pale frame on a pale wall disappears: take the dark green then
            if (Mathf.Abs(t.grayscale - wall.grayscale) < 0.16f) t = wall.grayscale > 0.55f ? Trims[1] : Trims[0];
            return t;
        }

        // ------------------------------------------------------------------ plaster
        /// <summary>Rendered wall: photo plaster at true scale, blotches, rain streaks from the top, damp and moss at the
        /// foot, and here and there the paint gone down to the render.</summary>
        public static Color Plaster(Color baseCol, float u, float v, int seed)
        {
            float blot = Fbm(u / 21f + seed * 3.1f, v / 21f, 3, seed) - 0.5f;
            float fine = Dev != null ? Mathf.Clamp(Dev("plastered_wall_04", 256, (int)(u * 6.4f) + seed * 37, (int)(v * 6.0f) + seed * 11), -0.3f, 0.3f) : 0f;
            float streak = Mathf.Pow(Fbm(u / 4.2f + seed, v / 90f, 2, seed + 5), 2.2f) * Smooth(10f, 64f, v) * 0.17f;
            Color c = Mul(baseCol, 0.93f + blot * 0.13f + fine * 0.6f - streak);
            float peel = Fbm(u / 9f + seed * 1.7f, v / 7f, 3, seed + 9);
            if ((seed & 3) == 0 && peel > 0.66f) c = Mix(c, Mul(Concrete, 0.95f + fine), (peel - 0.66f) * 5f);
            // the bay repeats on every storey, so only a faint line where each floor's dirt gathers; the real damp at the
            // foot of the building is the plinth's and the weathering shader's
            float damp = Smooth(4.5f + 2.5f * VNoise(u / 5f, seed, seed + 2), 0f, v);
            c = Mix(Mul(c, 1f - damp * 0.12f), Moss, damp * 0.07f * VNoise(u / 3f, v / 3f, seed + 4));
            return c;
        }

        // ------------------------------------------------------------------ window
        /// <summary>A timber window set back in the wall. flags: 1 = small panes, 2 = iron grill, 4 = curtains,
        /// 8 = one casement standing open (dark gap).</summary>
        static void Window(ref Color c, ref Color e, float u, float v, float x0, float y0, float x1, float y1, Color trim, Color wall, bool lit, int seed, int flags)
        {
            float w = x1 - x0, h = y1 - y0;
            // rain marks below the sill, strongest at its ends
            if (v < y0 - 3.4f && v > y0 - 17f && u > x0 - 3.5f && u < x1 + 3.5f)
            {
                float ends = Mathf.Max(Band(u, x0 - 3.2f, x0 - 1.6f, 1.2f), Band(u, x1 + 1.6f, x1 + 3.2f, 1.2f));
                float run = Mathf.Pow(VNoise(u * 0.7f, seed, seed + 21), 3f);
                float fade = 1f - (y0 - 3.4f - v) / 13.6f;
                c = Mix(c, Soot, (ends * 0.20f + run * 0.12f) * fade * fade);
            }
            // sill: a projecting slab, light on top, a shadow under it
            if (In(u, v, x0 - 3f, y0 - 5.4f, x1 + 3f, y0 - 3.2f)) c = Mul(c, 0.62f + 0.3f * (y0 - 3.2f - v) / 2.2f);
            if (In(u, v, x0 - 3f, y0 - 3.2f, x1 + 3f, y0 - 0.9f))
            {
                float topLight = Smooth(y0 - 2.0f, y0 - 0.9f, v);
                c = Mul(Mix(Concrete, wall, 0.25f), 0.86f + 0.2f * topLight + 0.05f * VNoise(u, v * 2f, seed));
            }
            // architrave: painted band round the opening, lit from the upper left
            bool arch = In(u, v, x0 - 1.7f, y0 - 0.9f, x1 + 1.7f, y1 + 1.7f) && !In(u, v, x0, y0, x1, y1);
            if (arch)
            {
                float edge = (u < x0 || v > y1) ? 1.08f : 0.86f;
                c = Mul(Mix(trim, wall, 0.12f), edge * (0.95f + 0.07f * VNoise(u * 0.8f, v * 0.8f, seed + 3)));
            }
            if (!In(u, v, x0, y0, x1, y1)) return;

            float mx = (x0 + x1) * 0.5f, ty = y0 + h * 0.66f;
            const float F = 1.15f;   // frame member, cell units
            bool frame = u < x0 + F || u > x1 - F || v < y0 + F || v > y1 - F || Mathf.Abs(u - mx) < F * 0.55f || Mathf.Abs(v - ty) < F * 0.45f;
            if (!frame && (flags & 1) != 0)
            {
                // glazing bars: two across each lower casement, one down each top light
                float lowH = ty - y0 - F, p = (v - y0 - F) / lowH;
                if (v < ty) frame = Mathf.Abs(p - 1f / 3f) < 0.022f || Mathf.Abs(p - 2f / 3f) < 0.022f;
                else frame = Mathf.Abs(u - (x0 + w * 0.25f)) < 0.3f || Mathf.Abs(u - (x0 + w * 0.75f)) < 0.3f;
            }
            if (frame)
            {
                float worn = VNoise(u * 1.6f, v * 0.5f, seed + 6);
                c = Mul(trim, 0.86f + 0.16f * worn);
                if (v < y0 + F) c = Mul(c, 0.85f);                       // the bottom rail holds the dirt
            }
            else
            {
                float g = (v - y0) / h;
                bool open = (flags & 8) != 0 && u > mx && v < ty;       // an open casement: straight into the dark room
                Color glass = Mix(GlassDark, GlassSky, Mathf.Pow(g, 1.6f) * 0.62f);
                float sheen = Mathf.Repeat((u - x0) + (v - y0) * 0.55f + seed * 3f, 19f);
                if (sheen < 2.2f) glass = Mix(glass, GlassSky, 0.22f);
                if (open) glass = Mul(GlassDark, 0.7f);
                if ((flags & 4) != 0 && !open)
                {
                    // curtains drawn back to the sides (or right across on some)
                    float side = Mathf.Min(u - x0, x1 - u) / w;
                    bool across = (seed % 5) == 0;
                    if (across || side < 0.22f)
                    {
                        Color cloth = CurtainColor(seed);
                        float fold = 0.82f + 0.18f * Mathf.Sin(u * 2.4f + seed);
                        glass = Mix(glass, Mul(cloth, fold), 0.62f);
                    }
                }
                c = glass;
                if (lit)
                {
                    float k = 0.7f + 0.3f * VNoise(u * 0.4f, v * 0.4f, seed + 8);
                    c = Mix(c, Lit, 0.10f);
                    e = Mul(Lit, k * (open ? 1.1f : 0.85f));
                }
            }
            // the reveal: the wall's own shadow across the top and one side of the opening
            float shade = Mathf.Max(Smooth(1.9f, 0f, y1 - v), Smooth(1.3f, 0f, u - x0) * 0.8f);
            c = Mul(c, 1f - shade * 0.5f);
            if ((flags & 2) != 0)
            {
                // iron grill outside the glass: uprights and two flats
                float gu = Mathf.Repeat(u - x0, 2.9f), gv1 = Mathf.Abs(v - (y0 + h * 0.3f)), gv2 = Mathf.Abs(v - (y0 + h * 0.7f));
                if (gu < 0.42f || gv1 < 0.3f || gv2 < 0.3f) { c = Mix(c, Iron, 0.9f); e = Mul(e, 0.15f); }
            }
        }

        /// <summary>Painted shop boards: the blues, reds, greens, yellows and whites of an Indian bazaar, a little faded.</summary>
        static readonly Color[] Boards =
        {
            new Color(0.14f, 0.22f, 0.44f), new Color(0.56f, 0.13f, 0.11f), new Color(0.13f, 0.32f, 0.20f), new Color(0.78f, 0.62f, 0.18f),
            new Color(0.86f, 0.85f, 0.80f), new Color(0.40f, 0.12f, 0.15f), new Color(0.30f, 0.46f, 0.60f), new Color(0.11f, 0.11f, 0.12f),
        };

        static Color CurtainColor(int seed)
        {
            switch (seed % 6)
            {
                case 0: return new Color(0.78f, 0.74f, 0.64f);
                case 1: return new Color(0.62f, 0.28f, 0.24f);
                case 2: return new Color(0.36f, 0.46f, 0.56f);
                case 3: return new Color(0.70f, 0.60f, 0.36f);
                case 4: return new Color(0.44f, 0.52f, 0.40f);
                default: return new Color(0.82f, 0.80f, 0.76f);
            }
        }

        /// <summary>Concrete sunshade over a window, and the shadow it throws down the wall.</summary>
        static void Sunshade(ref Color c, float u, float v, float x0, float x1, float y, Color wall, int seed)
        {
            if (In(u, v, x0, y - 7f, x1, y)) c = Mul(c, 0.58f + 0.42f * Smooth(0f, 7f, y - v));          // shadow below
            if (In(u, v, x0, y, x1, y + 2.6f))
                c = Mul(Mix(Concrete, wall, 0.35f), 0.80f + 0.18f * Smooth(y, y + 2.6f, v) - 0.10f * Mathf.Pow(VNoise(u * 0.9f, seed, seed + 31), 2f));
        }

        // ------------------------------------------------------------------ the bay styles
        public const int WinA = 0, WinB = 1, Shop = 2, Plain = 3, Door = 4, Balcony = 5;

        /// <summary>One facade bay. u, v: cell units 0..64, v up from the floor. kind: one of the constants above.
        /// lit: the night version (emission returned in e).</summary>
        public static Color Bay(int kind, bool lit, int wallIndex, Color wall, float u, float v, int seed, out Color e)
        {
            e = Color.black;
            Color c = Plaster(wall, u, v, seed);
            Color trim = Trim(wallIndex, wall);
            // floor line: a shallow band where the slab meets the wall, on some colours
            if ((wallIndex % 3) != 1 && v > 60.6f) c = Mul(c, v > 61.6f ? 1.06f : 0.80f);
            switch (kind)
            {
                case WinA:
                {
                    int flags = ((wallIndex + 1) % 2 == 0 ? 1 : 0) | ((seed % 3) == 0 ? 2 : 0) | 4 | ((seed % 7) == 3 ? 8 : 0);
                    Window(ref c, ref e, u, v, 19f, 19f, 45f, 48.5f, trim, wall, lit, seed, flags);
                    Sunshade(ref c, u, v, 14.5f, 49.5f, 52f, wall, seed);
                    break;
                }
                case WinB:
                {
                    int flags = ((wallIndex % 2) == 0 ? 1 : 0) | ((seed % 4) == 1 ? 2 : 0);
                    Color e1 = Color.black, e2 = Color.black;
                    Window(ref c, ref e1, u, v, 8.5f, 20f, 24.5f, 48.5f, trim, wall, lit, seed, flags | ((seed % 2) == 0 ? 4 : 0));
                    Window(ref c, ref e2, u, v, 39.5f, 20f, 55.5f, 48.5f, trim, wall, lit && Hash(wallIndex, seed, 3) > 0.3f, seed + 1, flags | 4);
                    e = e1 + e2;
                    break;
                }
                case Balcony:
                    if ((wallIndex % 4) == 3) VerandahBay(ref c, ref e, u, v, trim, wall, lit, seed);
                    else BalconyBay(ref c, ref e, u, v, trim, wall, lit, seed, wallIndex);
                    break;
                case Door: DoorBay(ref c, ref e, u, v, trim, wall, seed); break;
                case Shop: ShopBay(ref c, ref e, u, v, wall, lit, seed); break;
                default: PlainBay(ref c, u, v, wall, seed); break;
            }
            c.a = 1f;
            return c;
        }

        static void PlainBay(ref Color c, float u, float v, Color wall, int seed)
        {
            // rain-water pipe with its clips, and the stain beside it
            float pu = u - 57.5f;
            if (Mathf.Abs(pu) < 3.2f) c = Mix(c, Soot, 0.14f * (1f - Mathf.Abs(pu) / 3.2f));
            if (Mathf.Abs(pu) < 1.25f)
            {
                float round = 1f - pu * pu / 1.6f;
                c = Mul(new Color(0.34f, 0.35f, 0.36f), 0.55f + 0.6f * round);
                if (Mathf.Repeat(v, 21f) < 1.1f) c = Mul(c, 0.6f);
            }
            if ((seed % 3) == 0 && In(u, v, 24f, 43f, 40f, 51f))
            {
                // a ventilator: concrete jaali
                bool hole = Mathf.Repeat(u - 24f, 3.2f) > 1.1f && Mathf.Repeat(v - 43f, 2.7f) > 1.0f && In(u, v, 25f, 44f, 39f, 50f);
                c = hole ? Mul(GlassDark, 1.2f) : Mul(Mix(Concrete, wall, 0.4f), 0.9f);
            }
            if ((seed % 4) == 1 && In(u, v, 9f, 20f, 17f, 31f))
            {
                // electricity meter box and its conduit
                c = In(u, v, 9.6f, 20.6f, 16.4f, 30.4f) ? Mul(new Color(0.62f, 0.63f, 0.60f), 0.9f + 0.1f * VNoise(u, v, seed)) : Mul(Soot, 1.6f);
                if (In(u, v, 11f, 24f, 15f, 28.5f)) c = Mul(GlassDark, 1.6f);
            }
            if ((seed % 4) == 1 && Mathf.Abs(u - 13f) < 0.4f && v >= 31f) c = Mul(Iron, 2.2f);
        }

        static void DoorBay(ref Color c, ref Color e, float u, float v, Color trim, Color wall, int seed)
        {
            const float x0 = 21.5f, x1 = 42.5f, y1 = 45f;
            if (In(u, v, x0 - 5f, 0f, x1 + 5f, 2.4f)) c = Mul(Concrete, 0.72f + 0.2f * Smooth(0f, 2.4f, v) + 0.06f * VNoise(u, v, seed));   // the step
            if (In(u, v, x0 - 1.8f, 0f, x1 + 1.8f, y1 + 1.8f) && !In(u, v, x0, 0f, x1, y1))
                c = Mul(Mix(trim, wall, 0.12f), (u < x0 || v > y1) ? 1.06f : 0.86f);
            if (In(u, v, x0, 2.4f, x1, y1))
            {
                Color wood = (seed % 3) == 0 ? new Color(0.32f, 0.20f, 0.12f) : trim.grayscale > 0.6f ? new Color(0.36f, 0.23f, 0.14f) : trim;
                float grain = 0.86f + 0.2f * VNoise(u * 2.2f, v * 0.25f, seed + 12);
                c = Mul(wood, grain);
                float mx = (x0 + x1) * 0.5f;
                bool fan = v > y1 - 6.5f;                                   // light over the door
                if (fan)
                {
                    bool bar = v < y1 - 5.6f || v > y1 - 1f || u < x0 + 1f || u > x1 - 1f || Mathf.Abs(u - mx) < 0.4f;
                    c = bar ? Mul(wood, 0.8f) : Mix(GlassDark, GlassSky, 0.3f);
                }
                else
                {
                    if (Mathf.Abs(u - mx) < 0.28f) c = Mul(c, 0.42f);                // where the leaves meet
                    float lu = u < mx ? u - x0 : u - mx, lw = mx - x0;
                    // two sunk panels a leaf: dark groove, lighter field
                    for (int k = 0; k < 2; k++)
                    {
                        float py0 = k == 0 ? 6f : 23f, py1 = k == 0 ? 20f : 36f;
                        if (In(lu, v, 1.8f, py0, lw - 1.8f, py1))
                        {
                            bool groove = lu < 2.5f || lu > lw - 2.5f || v < py0 + 0.8f || v > py1 - 0.8f;
                            c = Mul(c, groove ? 0.62f : 1.1f);
                        }
                    }
                    if (In(u, v, mx + 1.2f, 21f, mx + 2.2f, 23.4f)) c = new Color(0.62f, 0.52f, 0.28f);   // latch
                    c = Mul(c, 1f - 0.28f * Smooth(9f, 2.4f, v));                    // scuffed and damp at the foot
                }
                c = Mul(c, 1f - 0.4f * Mathf.Max(Smooth(2f, 0f, y1 - v), Smooth(1.3f, 0f, u - x0) * 0.8f));
            }
            // the lamp over the door, and its glow on the wall
            float d = Mathf.Sqrt((u - 32f) * (u - 32f) + (v - 53f) * (v - 53f));
            if (d < 1.9f) { c = Mix(new Color(0.95f, 0.9f, 0.78f), Lit, 0.5f); e = Lit; }
            else if (In(u, v, 31.4f, 54.5f, 32.6f, 57.5f)) c = Mul(Iron, 1.6f);
            else if (d < 9f) e = Mul(Lit, 0.16f * (1f - d / 9f));
            if ((seed % 2) == 0 && In(u, v, 47f, 30f, 54f, 35f)) c = In(u, v, 47.6f, 30.6f, 53.4f, 34.4f) ? new Color(0.16f, 0.25f, 0.42f) : Color.white;   // house-number plate
        }

        static void BalconyBay(ref Color c, ref Color e, float u, float v, Color trim, Color wall, bool lit, int seed, int wallIndex)
        {
            // french door to the balcony
            const float x0 = 20f, x1 = 44f, y0 = 3.2f, y1 = 48f;
            if (In(u, v, x0 - 1.7f, y0, x1 + 1.7f, y1 + 1.7f) && !In(u, v, x0, y0, x1, y1))
                c = Mul(Mix(trim, wall, 0.12f), (u < x0 || v > y1) ? 1.07f : 0.86f);
            if (In(u, v, x0, y0, x1, y1))
            {
                float mx = (x0 + x1) * 0.5f, ty = 39.5f;
                bool frame = u < x0 + 1.3f || u > x1 - 1.3f || v > y1 - 1.2f || Mathf.Abs(u - mx) < 0.8f || Mathf.Abs(v - ty) < 0.5f || v < y0 + 1.2f;
                bool panel = v < 15f;                                                         // timber below, glass above
                if (frame) c = Mul(trim, 0.86f + 0.14f * VNoise(u * 1.6f, v * 0.5f, seed));
                else if (panel)
                {
                    float lu = u < mx ? u - x0 : u - mx, lw = mx - x0;
                    bool groove = lu < 2.6f || lu > lw - 2.6f || v < 5.4f || v > 13.6f;
                    c = Mul(trim, groove ? 0.66f : 0.95f);
                }
                else
                {
                    float g = (v - y0) / (y1 - y0);
                    Color glass = Mix(GlassDark, GlassSky, Mathf.Pow(g, 1.7f) * 0.6f);
                    if (v < ty && Mathf.Abs(v - 27f) < 0.3f) glass = Mul(trim, 0.9f);        // one glazing bar
                    float side = Mathf.Min(u - x0, x1 - u) / (x1 - x0);
                    if (side < 0.2f) glass = Mix(glass, Mul(CurtainColor(seed + 2), 0.82f + 0.18f * Mathf.Sin(u * 2.4f)), 0.6f);
                    c = glass;
                    if (lit) { c = Mix(c, Lit, 0.10f); e = Mul(Lit, 0.7f + 0.3f * VNoise(u * 0.4f, v * 0.4f, seed)); }
                }
                c = Mul(c, 1f - 0.5f * Mathf.Max(Smooth(1.9f, 0f, y1 - v), Smooth(1.3f, 0f, u - x0) * 0.8f));
            }
            Sunshade(ref c, u, v, 14f, 50f, 51.5f, wall, seed);
            // the balcony: slab, then railing in front of everything
            if (In(u, v, 5f, 0f, 59f, 3.2f)) { c = Mul(Mix(Concrete, wall, 0.3f), 0.70f + 0.26f * Smooth(0f, 3.2f, v) - 0.12f * Mathf.Pow(VNoise(u * 0.8f, seed, seed + 40), 2f)); e = Color.black; }
            bool painted = (wallIndex % 2) == 0;
            Color rail = painted ? Mul(trim, 0.9f) : Iron;
            if (In(u, v, 5f, 3.2f, 59f, 21.6f))
            {
                // a flower pot and the washing, behind the bars
                if ((seed % 3) == 1)
                {
                    float px = 10f + (seed % 4) * 2f;
                    if (In(u, v, px, 3.2f, px + 4.4f, 7f)) { c = Mul(new Color(0.58f, 0.31f, 0.22f), 0.85f + 0.2f * (u - px) / 4.4f); e = Color.black; }
                    float bd = Mathf.Sqrt((u - px - 2.2f) * (u - px - 2.2f) + (v - 9.6f) * (v - 9.6f) * 0.8f);
                    if (bd < 3.3f + VNoise(u * 2f, v * 2f, seed)) { c = Hash((int)(u * 2f), (int)(v * 2f), seed) > 0.86f ? new Color(0.74f, 0.2f, 0.26f) : Mul(new Color(0.2f, 0.36f, 0.17f), 0.75f + 0.5f * VNoise(u * 1.5f, v * 1.5f, seed)); e = Color.black; }
                }
                if ((seed % 4) == 2)
                    for (int k = 0; k < 2; k++)
                    {
                        float cx = 38f + k * 8.5f + (seed % 3), cw = 5.5f + k * 1.5f, ch = 8f + ((seed >> k) % 4);
                        if (In(u, v, cx, 20.6f - ch, cx + cw, 20.8f)) { c = Mul(CurtainColor(seed + k * 3 + 1), 0.8f + 0.2f * Mathf.Sin(u * 1.9f)); e = Color.black; }
                    }
                bool bar = Mathf.Repeat(u - 5f, 2.7f) < 0.48f || v > 20.2f || (v > 4.6f && v < 5.4f) || u < 5.8f || u > 58.2f;
                if (bar) { c = Mul(rail, v > 20.2f ? 1.05f : 0.9f); e = Color.black; }
            }
        }

        /// <summary>The enclosed verandah of the old hill houses and hotels: a timber dado, then a wall of small panes
        /// from post to post under a boarded fascia. It runs the full width of the bay, so neighbours join up.</summary>
        static void VerandahBay(ref Color c, ref Color e, float u, float v, Color trim, Color wall, bool lit, int seed)
        {
            if (v > 54.5f) { c = Mul(Mix(trim, wall, 0.2f), 0.72f + 0.2f * Smooth(54.5f, 60f, v)); return; }      // fascia board
            if (v < 2.6f) { c = Mul(Mix(Concrete, wall, 0.3f), 0.72f + 0.22f * Smooth(0f, 2.6f, v)); return; }      // the floor slab's edge
            float pu = Mathf.Repeat(u, 21.333f);
            bool post = pu < 1.5f;
            if (v < 17f)
            {
                // boarded dado: vertical planks, a rail on top
                float plank = Mathf.Repeat(u, 2.6f);
                c = Mul(trim, (plank < 0.22f ? 0.62f : 0.9f + 0.1f * VNoise(u * 0.4f, v * 0.12f, seed)) * (post ? 1.08f : 1f));
                if (v > 15.6f) c = Mul(trim, 1.1f);
                c = Mul(c, 1f - 0.2f * Smooth(7f, 2.6f, v));
                return;
            }
            // glazing: 3 lights between posts, small panes, a transom
            float lu = Mathf.Repeat(pu - 1.5f, 6.61f);
            bool bar = post || lu < 0.55f || Mathf.Abs(v - 44f) < 0.45f || v < 17.8f || v > 53.8f
                       || Mathf.Repeat(v - 17.8f, 6.55f) < 0.32f && v < 44f || Mathf.Abs(lu - 3.58f) < 0.2f;
            if (bar) { c = Mul(trim, 0.88f + 0.14f * VNoise(u * 1.6f, v * 0.5f, seed + 6)); return; }
            float g = (v - 17f) / 37.5f;
            Color glass = Mix(GlassDark, GlassSky, Mathf.Pow(g, 1.4f) * 0.7f);
            int pane = Mathf.FloorToInt(u / 3.3f) + Mathf.FloorToInt(v / 6.55f) * 31;
            if (Hash(pane, seed, 51) > 0.9f) glass = Mix(glass, GlassSky, 0.25f);                                  // an odd pane catches the sky
            if (Hash(Mathf.FloorToInt(u / 21.333f), seed, 52) > 0.5f && v < 44f)
                glass = Mix(glass, Mul(CurtainColor(seed + 4), 0.82f + 0.18f * Mathf.Sin(u * 2.2f)), 0.5f);        // a curtain behind one bay
            c = Mul(glass, 1f - 0.35f * Smooth(1.6f, 0f, 53.8f - v));
            if (lit) { c = Mix(c, Lit, 0.10f); e = Mul(Lit, 0.6f + 0.3f * VNoise(u * 0.4f, v * 0.4f, seed)); }
        }

        static void ShopBay(ref Color c, ref Color e, float u, float v, Color wall, bool lit, int seed)
        {
            bool shut = !lit && (seed % 5) < 2;                      // some shops are closed in the daytime
            Color board = Boards[(int)(Hash(seed, 7, 99) * 7.999f)];
            if (In(u, v, 3f, 0f, 61f, 40.5f))
            {
                if (shut)
                {
                    // rolling shutter: slats, a lock plate, dirt thrown up at the foot
                    Color paint = (seed % 3) == 0 ? new Color(0.36f, 0.42f, 0.47f) : (seed % 3) == 1 ? new Color(0.44f, 0.45f, 0.44f) : new Color(0.32f, 0.40f, 0.34f);
                    float slat = Mathf.Repeat(v, 1.75f) / 1.75f;
                    c = Mul(paint, 0.72f + 0.34f * Mathf.Sin(slat * Mathf.PI) + 0.08f * (VNoise(u * 0.5f, v * 0.5f, seed) - 0.5f));
                    c = Mix(c, Soot, 0.4f * Smooth(8f, 0f, v) + 0.25f * Mathf.Pow(VNoise(u * 0.4f, v * 0.12f, seed + 2), 3f));
                    if (In(u, v, 29.5f, 1.2f, 34.5f, 4.6f)) c = Mul(Iron, 2.2f);
                    if (In(u, v, 3f, 0f, 4.4f, 40.5f) || In(u, v, 59.6f, 0f, 61f, 40.5f)) c = Mul(paint, 0.55f);   // guide channels
                }
                else
                {
                    // open front: shelves of goods to the roof, a counter, strings of packets across the top
                    Color back = new Color(0.10f, 0.085f, 0.07f);
                    c = back;
                    float glow = lit ? 1f : 0.6f;
                    int shelf = Mathf.FloorToInt((v - 9f) / 6.2f);
                    float sv = Mathf.Repeat(v - 9f, 6.2f);
                    if (v >= 9f && v < 34f)
                    {
                        if (sv < 0.7f) c = new Color(0.30f, 0.22f, 0.15f);                                // the shelf board
                        else
                        {
                            int item = Mathf.FloorToInt(u / (1.5f + 1.3f * Hash(shelf, seed, 4)));
                            float hgt = 2.4f + 2.9f * Hash(item, shelf, seed + 1);
                            if (sv < 0.7f + hgt && Hash(item, shelf, seed + 2) > 0.12f)
                            {
                                Color goods = Color.HSVToRGB(Hash(item, shelf, seed), 0.22f + 0.33f * Hash(item, shelf, seed + 3), 0.30f + 0.34f * Hash(item, shelf, seed + 5));
                                float edge = Mathf.Repeat(u, 1.5f + 1.3f * Hash(shelf, seed, 4));
                                c = Mul(goods, (edge < 0.25f ? 0.6f : 1f) * (0.8f + 0.2f * sv / hgt));
                            }
                        }
                    }
                    else if (v < 9f)
                    {
                        // counter: timber top, glass front with tins behind
                        if (v > 7.8f) c = new Color(0.36f, 0.26f, 0.17f);
                        else if (Mathf.Repeat(u - 3f, 14.5f) < 0.8f || v < 0.8f) c = new Color(0.25f, 0.18f, 0.12f);
                        else c = Mix(Mul(Color.HSVToRGB(Hash((int)(u / 2.2f), 1, seed), 0.32f, 0.5f), 0.55f + 0.3f * v / 7.8f), GlassSky, 0.12f);
                    }
                    else if (v < 40.5f)
                    {
                        // packets hanging on strings
                        float su = Mathf.Repeat(u, 2.1f);
                        int pk = Mathf.FloorToInt(u / 2.1f);
                        float len = 2.2f + 3.2f * Hash(pk, seed, 6);
                        if (su > 0.3f && v > 38.6f - len && v < 38.8f) c = Color.HSVToRGB(Hash(pk, seed, 8), 0.5f, 0.66f);
                    }
                    c = Mul(c, 0.72f + 0.28f * Smooth(40.5f, 22f, v));                                     // darker up under the lintel
                    c = Mul(c, glow);
                    if (lit) e = Mul(new Color(1f, 0.93f, 0.8f), 0.38f + 0.3f * c.grayscale);
                }
                // the shutter box above the opening
                if (v > 37.8f) { c = Mul(new Color(0.40f, 0.41f, 0.41f), 0.75f + 0.25f * Mathf.Sin(Mathf.Repeat(v, 0.9f) / 0.9f * Mathf.PI)); e = Color.black; }
            }
            else if (In(u, v, 1.5f, 42.5f, 62.5f, 58.5f))
            {
                // the name board: painted tin, a border, two lines of lettering
                bool border = u < 2.5f || u > 61.5f || v < 43.5f || v > 57.5f;
                bool pale = board.grayscale > 0.6f;
                c = border ? Mul(board, pale ? 0.7f : 1.35f) : Mul(board, 0.92f + 0.12f * VNoise(u * 0.3f, v * 0.3f, seed));
                Color ink = pale ? ((seed % 2) == 0 ? new Color(0.55f, 0.10f, 0.10f) : new Color(0.10f, 0.16f, 0.40f))
                                 : (seed % 3) == 0 ? new Color(0.92f, 0.82f, 0.36f) : new Color(0.92f, 0.92f, 0.88f);
                bool letter = false;
                if (v > 49f && v < 55.5f && u > 7f && u < 57f)
                {
                    int k = Mathf.FloorToInt((u - 7f) / 3.1f);
                    float lu = Mathf.Repeat(u - 7f, 3.1f);
                    letter = Hash(k, seed, 11) > 0.16f && lu < 2.3f && !(lu > 0.75f && lu < 1.55f && Mathf.Repeat(v - 49f, 3.2f) > 1.1f && Hash(k, seed, 12) > 0.4f);
                }
                else if (v > 45.3f && v < 47.6f && u > 14f && u < 50f)
                    letter = Hash(Mathf.FloorToInt((u - 14f) / 1.3f), seed, 13) > 0.3f && Mathf.Repeat(u - 14f, 1.3f) < 0.9f;
                if (letter) c = ink;
                if (lit) e = letter ? Mul(ink, 0.9f) : Mul(board, 0.32f);
                c = Mix(c, Soot, 0.18f * Mathf.Pow(VNoise(u * 0.25f, v * 0.6f, seed + 15), 2f));
            }
            else if (v >= 40.5f && v < 42.5f) c = Mul(c, 0.7f);                                           // shadow under the board
        }

        // ------------------------------------------------------------------ roofs and materials
        /// <summary>Corrugated tin: ridges down the slope, sheets lapped in courses, rust creeping from the laps and the
        /// nail lines, a replaced sheet now and then.</summary>
        public static Color TinRoof(Color paint, float u, float v, int seed)
        {
            float ridge = Mathf.Sin(u / 64f * Mathf.PI * 2f * 40f);                      // 40 corrugations across the cell
            float shade = 0.80f + 0.20f * ridge;
            int sheet = Mathf.FloorToInt(u / 8f), course = Mathf.FloorToInt(v / 16f);
            float cv = Mathf.Repeat(v, 16f);
            float tone = 0.90f + 0.16f * Hash(sheet, course, seed);                      // each sheet weathers its own way
            Color c = Mul(paint, shade * tone);
            if (Hash(sheet, course, seed + 3) > 0.975f) c = Mul(new Color(0.55f, 0.56f, 0.56f), shade * 0.95f);   // a new bare sheet
            float rustN = Fbm(u / 7f + seed, v / 13f, 3, seed + 1);
            float rust = Mathf.Clamp01((rustN - 0.52f) * 3.2f) * 0.8f + Smooth(2.6f, 0f, cv) * 0.5f * VNoise(u * 0.6f, course, seed + 2);
            c = Mix(c, Mul(new Color(0.42f, 0.22f, 0.11f), 0.8f + 0.3f * ridge * 0.5f), rust);
            if (cv < 0.55f) c = Mul(c, 0.55f);                                           // the shadow line of the lap
            if (Mathf.Abs(cv - 2.6f) < 0.3f && Mathf.Repeat(u, 4.8f) < 0.5f) c = Mul(c, 0.45f);            // nail heads
            if (Mathf.Repeat(u, 8f) < 0.3f) c = Mul(c, 0.82f);                           // side laps
            float fine = Dev != null ? Dev("corrugated_iron_02", 256, (int)(u * 8f), (int)(v * 8f)) : 0f;
            return Mul(c, 1f + Mathf.Clamp(fine, -0.25f, 0.25f) * 0.5f);
        }
    }
}
