using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Mallital's four places of worship, built from photographs (user, 2026-11-14: "mandir, jama masjid, church aur
    /// gurudwara dekh ke proper real banao; dont use from our assets, create karo"). References: the user's video stills
    /// and Wikimedia Commons photos kept in D:\Unity\ref (Naina Devi's octagonal sanctum under two red tin roofs; the
    /// white marble Jama Masjid facing the Flats with one tall minaret and a ribbed dome; St. Francis' stone gable front
    /// with its red spire; the white Gurudwara Sri Guru Singh Sabha with its dome behind a yellow arch).
    /// Local axes: +z is the front, y = 0 the top of the plinth.
    /// </summary>
    public static partial class LandmarkBuilder
    {
        /// <summary>Lowers the terrain inside a rectangle (half length along the building, half depth to the front) to y,
        /// easing back over 4 m: the mound that stood in front of the Jama Masjid.</summary>
        internal static void CutGround(WorldCtx ctx, Vector2 c, Vector2 front, float halfLen, float halfDepth, float y)
        {
            var t = ctx.terrain;
            var td = t.terrainData;
            int res = td.heightmapResolution;
            Vector3 tp = t.transform.position, sz = td.size;
            float cx = (res - 1) / sz.x, cz = (res - 1) / sz.z, R = halfLen + 6f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((c.x - R - tp.x) * cx), 0, res - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((c.x + R - tp.x) * cx), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((c.y - R - tp.z) * cz), 0, res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((c.y + R - tp.z) * cz), 0, res - 1);
            int w = x1 - x0 + 1, h = z1 - z0 + 1;
            var hs = td.GetHeights(x0, z0, w, h);
            Vector2 along = new Vector2(front.y, -front.x);
            float target = Mathf.Clamp01((y - tp.y) / sz.y);
            for (int zi = 0; zi < h; zi++)
                for (int xi = 0; xi < w; xi++)
                {
                    var q = new Vector2(tp.x + (x0 + xi) / cx, tp.z + (z0 + zi) / cz) - c;
                    float u = Mathf.Abs(Vector2.Dot(q, along)) - halfLen, v = Mathf.Abs(Vector2.Dot(q, front)) - halfDepth;
                    float outside = Mathf.Max(u, v);
                    if (outside > 4f || hs[zi, xi] <= target) continue;
                    hs[zi, xi] = Mathf.Lerp(target, hs[zi, xi], Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(outside / 4f)));
                }
            td.SetHeights(x0, z0, hs);
        }

        /// <summary>An eight-sided upright piece with flat faces to the front, back and sides.</summary>
        static void Oct(MeshBuilder mb, Vector3 b, float r0, float r1, float h, Vector2 uv, bool capTop = true, bool capBottom = false) =>
            mb.Cylinder(b, r0, r1, h, 8, uv, capTop, capBottom, 1f, Mathf.PI / 8f);

        static void Kalash(MeshBuilder mb, Vector3 at, float s, Vector2 gold)
        {
            mb.Sphere(at + Vector3.up * (0.3f * s), new Vector3(0.34f, 0.3f, 0.34f) * s, 8, 4, gold);
            mb.Sphere(at + Vector3.up * (0.72f * s), new Vector3(0.22f, 0.2f, 0.22f) * s, 8, 4, gold);
            mb.Cylinder(at + Vector3.up * (0.86f * s), 0.1f * s, 0f, 0.8f * s, 6, gold, false);
        }

        /// <summary>A window on a wall facing +z: a glass pane with a pointed or round head.</summary>
        static void ArchPane(MeshBuilder mb, Vector3 c, float w, float h, float rise, Vector2 uv, Vector3 n)
        {
            Vector3 r = Vector3.Cross(Vector3.up, n).normalized * (w * 0.5f), u = Vector3.up * (h * 0.5f);
            mb.Quad(c - r - u, c + r - u, c + r + u, c - r + u, uv, n);
            mb.Tri(c - r + u, c + r + u, c + u + Vector3.up * rise, uv, n);
        }

        // ------------------------------------------------------------------ Naina Devi
        static void NainaDeviTemple(MeshBuilder mb, float depth)
        {
            Plinth(mb, new Vector3(28, 0, 22), depth, M(Atlas.MatMarble));
            Vector2 white = S(Pal.TempleWhite), red = S(Pal.TempleRed), orange = M(Atlas.MatTempleOrange), gold = M(Atlas.MatGold),
                    yellow = S(Pal.BoatYellow), marble = M(Atlas.MatMarble), dark = S(Pal.IronBlack), glass = M(Atlas.MatGlass), wood = S(Pal.WoodDark);

            // the sanctum: eight-sided, a wide red tin roof on golden posts, a white drum, a steep red spire
            Vector3 c = new Vector3(-1f, 0f, -1f);
            Oct(mb, c, 5.2f, 5.2f, 0.5f, marble);
            for (int k = 0; k < 3; k++) mb.Box(c + new Vector3(0f, 0.08f + k * 0.16f, 5.9f - k * 0.32f), new Vector3(3.2f, 0.16f, 0.34f), marble);   // steps
            Oct(mb, c + Vector3.up * 0.5f, 3.0f, 3.0f, 3.4f, white);
            float face = 3.0f * Mathf.Cos(Mathf.PI / 8f);
            mb.Box(c + new Vector3(0f, 2.1f, face + 0.02f), new Vector3(1.7f, 3.0f, 0.06f), gold);     // door frame
            mb.Box(c + new Vector3(0f, 1.95f, face + 0.05f), new Vector3(1.2f, 2.6f, 0.06f), dark);
            for (int k = 0; k < 8; k++)
            {
                float a = Mathf.PI / 8f + k * Mathf.PI / 4f;
                mb.Cylinder(c + new Vector3(Mathf.Cos(a) * 5.0f, 0.5f, Mathf.Sin(a) * 5.0f), 0.09f, 0.09f, 3.3f, 6, gold, false);
            }
            Oct(mb, c + Vector3.up * 3.72f, 5.85f, 5.85f, 0.14f, yellow, true, true);                     // fascia under the eave
            Oct(mb, c + Vector3.up * 3.86f, 5.8f, 2.7f, 1.5f, red, false);
            Oct(mb, c + Vector3.up * 5.3f, 2.68f, 2.68f, 0.16f, yellow);
            Oct(mb, c + Vector3.up * 5.36f, 2.6f, 2.6f, 1.7f, white);
            foreach (var n in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })      // a small niche on four faces
                ArchPane(mb, c + Vector3.up * 6.2f + n * (2.6f * Mathf.Cos(Mathf.PI / 8f) + 0.02f), 0.4f, 0.5f, 0.25f, S(Pal.Concrete), n);
            // the spire in the photos is tall and steep: about one and a half times the drum's width
            Oct(mb, c + Vector3.up * 7.0f, 3.2f, 3.2f, 0.12f, yellow, true, true);
            Oct(mb, c + Vector3.up * 7.1f, 3.15f, 0f, 6.2f, red, false);
            for (int k = 0; k < 4; k++) mb.Sphere(c + Vector3.up * (13.3f + k * 0.34f), Vector3.one * (0.24f - k * 0.045f), 8, 4, gold);
            mb.Cylinder(c + Vector3.up * 14.5f, 0.05f, 0f, 0.5f, 5, gold, false);
            Oct(mb, c + Vector3.up * 0.5f, 3.06f, 3.06f, 0.7f, S(Pal.SareePink));   // the pink band at the foot of the walls

            // the bell gate before the sanctum (photos): two square pillars striped white and red, a golden arch curling at
            // both ends with a wheel at each end and in the middle, rows of bells hanging under it
            Vector3 bg = c + new Vector3(0f, 0f, 8.4f);
            Vector2 stripeR = S(Pal.TempleRed), stripeW = white;
            foreach (float x in new[] { -2.1f, 2.1f })
            {
                for (int k = 0; k < 7; k++)
                    mb.Box(bg + new Vector3(x, 0.25f + k * 0.5f, 0f), new Vector3(0.62f, 0.5f, 0.62f), (k % 2 == 0) ? stripeW : stripeR);
                mb.Box(bg + new Vector3(x, 3.65f, 0f), new Vector3(0.8f, 0.2f, 0.8f), white);
            }
            Vector3 lastA = default;
            for (int k = 0; k <= 16; k++)
            {
                float a = k / 16f;
                float lift = Mathf.Sin(a * Mathf.PI) * 0.7f + Mathf.Pow(Mathf.Abs(a - 0.5f) * 2f, 6f) * 0.55f;
                Vector3 q = bg + new Vector3((a - 0.5f) * 5.6f, 3.95f + lift, 0f);
                if (k > 0) mb.Limb(lastA, q, 0.26f, 0.26f, 8, gold);
                lastA = q;
            }
            foreach (float x in new[] { -2.8f, 0f, 2.8f })
                mb.Sphere(bg + new Vector3(x, x == 0f ? 4.7f : 4.55f, 0.2f), new Vector3(0.42f, 0.42f, 0.08f), 12, 4, gold);
            for (int row = 0; row < 2; row++)
                for (float x = -1.6f; x <= 1.61f; x += 0.4f)
                {
                    float top = 3.85f + Mathf.Sin((x / 5.6f + 0.5f) * Mathf.PI) * 0.7f - row * 0.15f;
                    mb.Limb(bg + new Vector3(x, top, row * 0.25f - 0.12f), bg + new Vector3(x, top - 0.6f - row * 0.3f, row * 0.25f - 0.12f), 0.012f, 0.012f, 3, dark, false);
                    mb.Cylinder(bg + new Vector3(x, top - 0.95f - row * 0.3f, row * 0.25f - 0.12f), 0.16f, 0.08f, 0.32f, 8, gold, true, true);
                }

            // the row of small shrines along the lake front, each a little pagoda: white walls, a red flared roof, a white
            // drum with a yellow band, a steep red spire, a gold finial (the lake-side photos show four spires in a row)
            void SmallShrine(Vector3 at, float sc)
            {
                Oct(mb, at, 1.5f * sc, 1.5f * sc, 0.35f, marble);
                Oct(mb, at + Vector3.up * 0.35f, 1.1f * sc, 1.1f * sc, 2.2f * sc, white);
                mb.Box(at + new Vector3(0f, 0.35f + 0.85f * sc, 1.04f * sc), new Vector3(0.7f * sc, 1.5f * sc, 0.05f), dark);
                float y0 = 0.35f + 2.2f * sc;
                Oct(mb, at + Vector3.up * y0, 2.05f * sc, 0.95f * sc, 0.8f * sc, red, false);
                Oct(mb, at + Vector3.up * (y0 + 0.8f * sc), 0.95f * sc, 0.95f * sc, 0.75f * sc, white);
                Oct(mb, at + Vector3.up * (y0 + 1.5f * sc), 1.02f * sc, 1.02f * sc, 0.1f, yellow, true, true);
                Oct(mb, at + Vector3.up * (y0 + 1.58f * sc), 1.0f * sc, 0f, 2.3f * sc, red, false);
                Kalash(mb, at + Vector3.up * (y0 + 3.75f * sc), 0.45f * sc, gold);
            }
            foreach (float x in new[] { -11.5f, -7.5f, 10.5f }) SmallShrine(new Vector3(x, 0f, 8.6f), x == -7.5f ? 1.15f : 0.95f);

            // the smaller shrine to one side: orange walls, two red roofs with yellow edges
            Vector3 h = new Vector3(-9.5f, 0f, -4f);
            mb.Box(h + Vector3.up * 0.2f, new Vector3(6f, 0.4f, 6f), marble);
            mb.Box(h + Vector3.up * 1.9f, new Vector3(4.4f, 3.0f, 4.4f), orange);
            mb.Box(h + new Vector3(0f, 1.6f, 2.22f), new Vector3(2.0f, 2.2f, 0.06f), dark);
            mb.Box(h + Vector3.up * 3.47f, new Vector3(6.5f, 0.14f, 6.5f), yellow);
            mb.Pyramid(h + Vector3.up * 3.54f, 6.4f, 6.4f, 1.3f, red);
            mb.Box(h + Vector3.up * 4.9f, new Vector3(2.2f, 1.0f, 2.2f), orange);
            mb.Box(h + Vector3.up * 5.45f, new Vector3(3.5f, 0.12f, 3.5f), yellow);
            mb.Pyramid(h + Vector3.up * 5.5f, 3.4f, 3.4f, 1.9f, red);
            Kalash(mb, h + Vector3.up * 7.3f, 0.6f, gold);

            // the long two-storey dharamshala behind, wood above stone, a red roof with a spire at its middle
            Vector3 d = new Vector3(8.2f, 0f, -6.8f);
            mb.Box(d + Vector3.up * 1.7f, new Vector3(10.5f, 3.4f, 6f), white);
            mb.Box(d + Vector3.up * 4.9f, new Vector3(10.5f, 3.0f, 6f), wood);
            mb.Box(d + new Vector3(0f, 3.45f, 3.6f), new Vector3(10.9f, 0.14f, 1.3f), white);                  // gallery
            mb.Box(d + new Vector3(0f, 4.3f, 4.2f), new Vector3(10.9f, 0.08f, 0.08f), red);
            for (float x = -5.2f; x <= 5.21f; x += 1.3f) mb.Box(d + new Vector3(x, 3.9f, 4.2f), new Vector3(0.07f, 0.8f, 0.07f), red);
            for (int k = 0; k < 5; k++)
            {
                float x = -4.2f + k * 2.1f;
                mb.Quad(d + new Vector3(x - 0.65f, 4.1f, 3.02f), d + new Vector3(x + 0.65f, 4.1f, 3.02f), d + new Vector3(x + 0.65f, 5.9f, 3.02f), d + new Vector3(x - 0.65f, 5.9f, 3.02f), glass, Vector3.forward);
                if (k != 2) mb.Quad(d + new Vector3(x - 0.5f, 1.0f, 3.02f), d + new Vector3(x + 0.5f, 1.0f, 3.02f), d + new Vector3(x + 0.5f, 2.6f, 3.02f), d + new Vector3(x - 0.5f, 2.6f, 3.02f), glass, Vector3.forward);
            }
            mb.Box(d + Vector3.up * 6.46f, new Vector3(12f, 0.12f, 7.6f), yellow);
            mb.Pyramid(d + Vector3.up * 6.5f, 11.8f, 7.4f, 1.9f, red);
            foreach (float x in new[] { -4.2f, 0f, 4.2f })   // small spires along its ridge, the middle one tallest (photos)
            {
                float big = x == 0f ? 1f : 0.7f;
                mb.Box(d + new Vector3(x, 8.2f, 0f), new Vector3(1.6f * big, 0.9f * big, 1.6f * big), white);
                mb.Pyramid(d + new Vector3(x, 8.2f + 0.45f * big, 0f), 2.2f * big, 2.2f * big, 2.8f * big, red);
                Kalash(mb, d + new Vector3(x, 8.2f + 3.2f * big, 0f), 0.4f * big, gold);
            }
            for (float x = -5.2f; x <= 5.21f; x += 0.9f)   // the warm lit windows of the upper floor show from the lake at dusk
                mb.Box(d + new Vector3(x, 4.95f, 3.03f), new Vector3(0.6f, 1.2f, 0.03f), S(Pal.WindowGlow));

            // a white chhatri at the water's side, the peepal on its chabutra, flag poles with red flags
            Vector3 ch = new Vector3(6f, 0f, 8.3f);
            mb.Box(ch + Vector3.up * 0.15f, new Vector3(2.6f, 0.3f, 2.6f), marble);
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) mb.Cylinder(ch + new Vector3(x, 0.3f, z), 0.1f, 0.09f, 2.1f, 6, white, false);
            mb.Box(ch + Vector3.up * 2.45f, new Vector3(2.7f, 0.14f, 2.7f), white);
            mb.Sphere(ch + Vector3.up * 2.5f, new Vector3(1.15f, 1.0f, 1.15f), 10, 4, white, 0f, 90f);
            Kalash(mb, ch + Vector3.up * 3.4f, 0.4f, gold);
            Vector3 t = new Vector3(-8f, 0f, 6f);
            mb.Cylinder(t, 1.9f, 1.9f, 0.6f, 10, white, true);
            mb.Cylinder(t, 0.9f, 0.55f, 5.5f, 8, S(Pal.Trunk), false);
            mb.Sphere(t + new Vector3(0f, 8.5f, 0f), new Vector3(5f, 3.6f, 4.8f), 9, 5, S(Pal.Oak));
            mb.Sphere(t + new Vector3(2.3f, 7.4f, 1.5f), new Vector3(3.2f, 2.4f, 3f), 8, 4, S(Pal.OakLight));
            mb.Sphere(t + new Vector3(-2.1f, 7.8f, -1.4f), new Vector3(3.1f, 2.5f, 3.2f), 8, 4, S(Pal.OakLight));
            foreach (var fp in new[] { new Vector3(3.6f, 0f, 3.5f), new Vector3(-12.5f, 0f, 9.5f), new Vector3(12.5f, 0f, 9.5f) })
            {
                mb.Cylinder(fp, 0.07f, 0.05f, 10f, 6, red, true);
                Flag(mb, fp + new Vector3(0.1f, 9.4f, 0f), 2.8f, Pal.TempleRed);
            }
            // the courtyard's edge: a low marble wall with a golden rail (open at the front corner where the gate lane comes in)
            void Edge(Vector3 ea, Vector3 eb)
            {
                Vector3 em = (ea + eb) * 0.5f, ed = eb - ea;
                var eq = Quaternion.LookRotation(ed.normalized);
                mb.Box(em + Vector3.up * 0.25f, new Vector3(0.3f, 0.5f, ed.magnitude), eq, marble);
                mb.Box(em + Vector3.up * 0.95f, new Vector3(0.06f, 0.06f, ed.magnitude), eq, gold);
                for (float es = 0.6f; es < ed.magnitude; es += 1.2f) mb.Box(ea + ed.normalized * es + Vector3.up * 0.72f, new Vector3(0.06f, 0.46f, 0.06f), eq, gold);
            }
            Edge(new Vector3(-13.8f, 0f, 10.8f), new Vector3(13.8f, 0f, 10.8f));
            Edge(new Vector3(13.8f, 0f, 10.8f), new Vector3(13.8f, 0f, -10.8f));
            Edge(new Vector3(13.8f, 0f, -10.8f), new Vector3(-13.8f, 0f, -10.8f));
            Edge(new Vector3(-13.8f, 0f, -10.8f), new Vector3(-13.8f, 0f, 1f));
        }

        // ------------------------------------------------------------------ Gurudwara Sri Guru Singh Sabha
        static void SinghSabhaGurudwara(MeshBuilder mb, float depth)
        {
            Plinth(mb, new Vector3(20, 0, 24), depth, M(Atlas.MatMarble));
            Vector2 white = S(Pal.TempleWhite), glass = M(Atlas.MatGlass), blue = S(Pal.BoatBlue), yellow = S(Pal.BoatYellow), gold = M(Atlas.MatGold),
                    dark = S(Pal.IronBlack), grey = S(Pal.ShirtWhite), marble = M(Atlas.MatMarble);   // grey: the domes' off-white
            // the hall: two floors, the upper one behind a balcony with a white balustrade
            mb.Box(new Vector3(0f, 3.5f, -4f), new Vector3(12f, 7f, 10f), white);
            mb.Box(new Vector3(0f, 1.45f, 1.03f), new Vector3(1.9f, 2.9f, 0.06f), dark);                         // the open door, the darbar within
            mb.Box(new Vector3(0f, 1.3f, 1.05f), new Vector3(1.1f, 1.4f, 0.04f), S(Pal.Saffron));
            foreach (float x in new[] { -4.2f, -2.4f, 2.4f, 4.2f })
                mb.Quad(new Vector3(x - 0.6f, 0.9f, 1.02f), new Vector3(x + 0.6f, 0.9f, 1.02f), new Vector3(x + 0.6f, 2.8f, 1.02f), new Vector3(x - 0.6f, 2.8f, 1.02f), glass, Vector3.forward);
            // the steel shed over the forecourt
            foreach (float x in new[] { -4.3f, 4.3f }) foreach (float z in new[] { 1.5f, 6.2f }) mb.Cylinder(new Vector3(x, 0f, z), 0.08f, 0.08f, 3.2f, 6, dark, false);
            var shed = new Rect(grey.x, grey.y, 0f, 0f);
            mb.QuadDouble(new Vector3(-4.8f, 3.2f, 6.6f), new Vector3(-4.8f, 3.2f, 1.1f), new Vector3(0f, 3.9f, 1.1f), new Vector3(0f, 3.9f, 6.6f), shed, new Vector3(-0.2f, 1f, 0f));
            mb.QuadDouble(new Vector3(4.8f, 3.2f, 1.1f), new Vector3(4.8f, 3.2f, 6.6f), new Vector3(0f, 3.9f, 6.6f), new Vector3(0f, 3.9f, 1.1f), shed, new Vector3(0.2f, 1f, 0f));
            mb.Box(new Vector3(0f, 4.25f, 1.06f), new Vector3(9.6f, 0.9f, 0.08f), blue);                           // the blue name board
            mb.Box(new Vector3(0f, 4.78f, 1.7f), new Vector3(12.8f, 0.18f, 1.7f), white);                          // balcony
            mb.Box(new Vector3(0f, 5.72f, 2.45f), new Vector3(12.8f, 0.1f, 0.16f), white);
            for (float x = -6.2f; x <= 6.21f; x += 0.4f) mb.Cylinder(new Vector3(x, 4.87f, 2.45f), 0.06f, 0.045f, 0.82f, 5, white, false);
            foreach (float x in new[] { -3.6f, -1.2f, 1.2f, 3.6f }) ArchPane(mb, new Vector3(x, 5.75f, 1.02f), 1.0f, 1.3f, 0.45f, glass, Vector3.forward);
            foreach (float x in new[] { -5.9f, 5.9f })                                                              // corner kiosks on the balcony
            {
                Oct(mb, new Vector3(x, 4.87f, 2.0f), 0.8f, 0.8f, 1.9f, white);
                Oct(mb, new Vector3(x, 6.77f, 2.0f), 1.0f, 1.0f, 0.12f, white, true, true);
                mb.Sphere(new Vector3(x, 6.89f, 2.0f), new Vector3(0.82f, 0.8f, 0.82f), 10, 4, grey, 0f, 90f);
                mb.Cylinder(new Vector3(x, 7.6f, 2.0f), 0.05f, 0f, 0.6f, 5, gold, false);
            }
            mb.Box(new Vector3(0f, 7.1f, -4f), new Vector3(12.5f, 0.2f, 10.5f), white);                            // cornice
            // the upper room, set back, and the dome over it
            mb.Box(new Vector3(0f, 8.8f, -4.5f), new Vector3(8.4f, 3.2f, 7f), white);
            foreach (float x in new[] { -2.6f, 0f, 2.6f }) ArchPane(mb, new Vector3(x, 8.6f, -0.98f), 1.1f, 1.3f, 0.5f, glass, Vector3.forward);
            mb.Box(new Vector3(0f, 10.45f, -4.5f), new Vector3(8.9f, 0.18f, 7.5f), white);
            mb.Box(new Vector3(0f, 10.95f, -0.85f), new Vector3(8.9f, 0.08f, 0.12f), white);                       // terrace rail
            for (float x = -4.2f; x <= 4.21f; x += 0.42f) mb.Cylinder(new Vector3(x, 10.54f, -0.85f), 0.05f, 0.04f, 0.4f, 5, white, false);
            mb.Cylinder(new Vector3(0f, 10.54f, -4.5f), 2.75f, 2.75f, 0.9f, 16, white, true);
            mb.Sphere(new Vector3(0f, 11.44f, -4.5f), new Vector3(2.95f, 2.75f, 2.95f), 16, 6, grey, 0f, 90f);
            for (int k = 0; k < 16; k++)   // the dome's ribs
            {
                float a = k * Mathf.PI / 8f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 last = new Vector3(0f, 11.44f, -4.5f) + dir * 2.97f;
                for (int s = 1; s <= 5; s++)
                {
                    float la = s * 17f * Mathf.Deg2Rad;
                    Vector3 p = new Vector3(0f, 11.44f + Mathf.Sin(la) * 2.77f, -4.5f) + dir * (Mathf.Cos(la) * 2.97f);
                    mb.Limb(last, p, 0.045f, 0.045f, 4, white, false);
                    last = p;
                }
            }
            Kalash(mb, new Vector3(0f, 14.1f, -4.5f), 0.8f, gold);
            foreach (float x in new[] { -3.9f, 3.9f }) foreach (float z in new[] { -7.7f, -1.3f })
            {
                mb.Cylinder(new Vector3(x, 10.54f, z), 0.4f, 0.4f, 0.7f, 8, white, true);
                mb.Sphere(new Vector3(x, 11.24f, z), new Vector3(0.5f, 0.55f, 0.5f), 8, 4, grey, 0f, 90f);
                mb.Cylinder(new Vector3(x, 11.75f, z), 0.04f, 0f, 0.45f, 5, gold, false);
            }
            // the yellow arch over the gate, the Nishan Sahib either side, the iron fence on its marble plinth
            const float gz = 10.4f;
            foreach (float x in new[] { -4.9f, 4.9f })
            {
                mb.Box(new Vector3(x, 1.5f, gz), new Vector3(1.0f, 3.0f, 0.3f), yellow);
                mb.Sphere(new Vector3(x, 2.1f, gz + 0.17f), new Vector3(0.36f, 0.36f, 0.04f), 10, 4, blue);       // the khanda roundel
                Flag(mb, new Vector3(x * 1.22f, 0f, gz), 11f, Pal.Saffron);
                mb.Box(new Vector3(x * 1.62f, 0.4f, gz), new Vector3(4.0f, 0.8f, 0.5f), marble);
                mb.Box(new Vector3(x * 1.62f, 2.1f, gz), new Vector3(4.0f, 0.06f, 0.06f), dark);
                for (float s = -1.9f; s <= 1.91f; s += 0.24f) mb.Box(new Vector3(x * 1.62f + s, 1.5f, gz), new Vector3(0.04f, 1.4f, 0.04f), dark);
            }
            Vector3 prev = default;
            for (int k = 0; k <= 18; k++)
            {
                float a = k / 18f * Mathf.PI;
                Vector3 p = new Vector3(Mathf.Cos(a) * 4.9f, 3.0f + Mathf.Sin(a) * 3.3f, gz);
                if (k > 0)
                {
                    Vector3 m = (prev + p) * 0.5f, dd = p - prev;
                    mb.Box(m, new Vector3(dd.magnitude + 0.06f, 0.95f, 0.28f), Quaternion.Euler(0f, 0f, Mathf.Atan2(dd.y, dd.x) * Mathf.Rad2Deg), yellow);
                }
                prev = p;
            }
        }

        // ------------------------------------------------------------------ St. Francis Catholic Church
        static void StFrancisChurch(MeshBuilder mb, float depth)
        {
            Plinth(mb, new Vector3(18, 0, 26), depth, M(Atlas.MatStone));
            // the walls are a pale grey-buff stone in the photo (the atlas's stone cell is a dark rubble: the concrete cell is nearer)
            Vector2 stone = M(Atlas.MatConcrete), white = S(Pal.TempleWhite), red = S(Pal.TempleRed), glass = M(Atlas.MatGlass), wood = S(Pal.WoodDark),
                    cap = M(Atlas.MatConcrete), dark = S(Pal.IronBlack);
            var redR = new Rect(red.x, red.y, 0f, 0f);
            // the nave: stone walls, a steep red roof, a gable to the road
            mb.Box(new Vector3(0f, 3f, -2f), new Vector3(9f, 6f, 16f), stone);
            mb.Tri(new Vector3(-4.5f, 6f, 6f), new Vector3(4.5f, 6f, 6f), new Vector3(0f, 10.2f, 6f), stone, Vector3.forward);
            mb.Tri(new Vector3(-4.5f, 6f, -10f), new Vector3(4.5f, 6f, -10f), new Vector3(0f, 10.2f, -10f), stone, Vector3.back);
            mb.QuadDouble(new Vector3(-5.0f, 5.72f, 5.9f), new Vector3(-5.0f, 5.72f, -10.4f), new Vector3(0f, 10.36f, -10.4f), new Vector3(0f, 10.36f, 5.9f), redR, new Vector3(-1f, 1f, 0f));
            mb.QuadDouble(new Vector3(5.0f, 5.72f, -10.4f), new Vector3(5.0f, 5.72f, 5.9f), new Vector3(0f, 10.36f, 5.9f), new Vector3(0f, 10.36f, -10.4f), redR, new Vector3(1f, 1f, 0f));
            foreach (float sx in new[] { -1f, 1f })   // white coping up the gable
                mb.Limb(new Vector3(sx * 4.75f, 5.95f, 6.06f), new Vector3(0f, 10.42f, 6.06f), 0.13f, 0.13f, 4, white);
            mb.Box(new Vector3(0f, 10.62f, 6f), new Vector3(0.5f, 0.45f, 0.5f), white);
            mb.Box(new Vector3(0f, 11.5f, 6f), new Vector3(0.17f, 1.4f, 0.17f), white);
            mb.Box(new Vector3(0f, 11.72f, 6f), new Vector3(0.85f, 0.17f, 0.17f), white);
            // the shield and the two name bands on the gable
            mb.Box(new Vector3(0f, 8.25f, 6.05f), new Vector3(2.0f, 0.3f, 0.06f), white);
            mb.Box(new Vector3(0f, 7.3f, 6.05f), new Vector3(1.1f, 1.25f, 0.08f), white);
            mb.Box(new Vector3(0f, 7.35f, 6.1f), new Vector3(0.14f, 0.8f, 0.04f), red);
            mb.Box(new Vector3(0f, 7.5f, 6.1f), new Vector3(0.55f, 0.14f, 0.04f), red);
            mb.Box(new Vector3(0f, 6.35f, 6.05f), new Vector3(2.9f, 0.3f, 0.06f), white);
            // the white porch with its pointed doorway
            mb.Box(new Vector3(0f, 1.9f, 6.5f), new Vector3(3.8f, 3.8f, 1.1f), white);
            mb.Tri(new Vector3(-1.9f, 3.8f, 7.05f), new Vector3(1.9f, 3.8f, 7.05f), new Vector3(0f, 5.0f, 7.05f), white, Vector3.forward);
            mb.QuadDouble(new Vector3(-2.05f, 3.72f, 7.15f), new Vector3(-2.05f, 3.72f, 5.95f), new Vector3(0f, 5.1f, 5.95f), new Vector3(0f, 5.1f, 7.15f), redR, new Vector3(-1f, 1f, 0f));
            mb.QuadDouble(new Vector3(2.05f, 3.72f, 5.95f), new Vector3(2.05f, 3.72f, 7.15f), new Vector3(0f, 5.1f, 7.15f), new Vector3(0f, 5.1f, 5.95f), redR, new Vector3(1f, 1f, 0f));
            ArchPane(mb, new Vector3(0f, 1.25f, 7.07f), 2.1f, 2.5f, 0.95f, S(Pal.BoatYellow), Vector3.forward);
            ArchPane(mb, new Vector3(0f, 1.2f, 7.09f), 1.7f, 2.4f, 0.8f, wood, Vector3.forward);
            // buttress pinnacles at the front corners, white-capped
            foreach (float x in new[] { -4.5f, 4.5f })
            {
                Oct(mb, new Vector3(x, 0f, 6f), 0.62f, 0.55f, 7.3f, stone);
                Oct(mb, new Vector3(x, 7.3f, 6f), 0.66f, 0f, 1.7f, white, false);
            }
            for (int i = 0; i < 4; i++)   // lancet windows down both sides
                foreach (float x in new[] { -4.52f, 4.52f })
                    ArchPane(mb, new Vector3(x, 3.2f, -8f + i * 3.6f), 0.9f, 2.6f, 0.7f, glass, x > 0 ? Vector3.right : Vector3.left);
            // the bell tower beside the nave, under a tall red spire
            Vector3 tw = new Vector3(6.3f, 0f, 1.5f);   // on the left as you face the door (seen from the front, +x is to the left)
            mb.Box(tw + Vector3.up * 6.5f, new Vector3(3.8f, 13f, 3.8f), stone);
            foreach (var n in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                ArchPane(mb, tw + Vector3.up * 10.6f + n * 1.92f, 0.9f, 1.8f, 0.6f, dark, n);
            mb.Box(tw + Vector3.up * 13.05f, new Vector3(4.2f, 0.2f, 4.2f), white);
            mb.Pyramid(tw + Vector3.up * 13.15f, 4.0f, 4.0f, 1.5f, red);
            Oct(mb, tw + Vector3.up * 13.6f, 1.9f, 0f, 9.4f, red, false);
            mb.Box(tw + Vector3.up * 23.5f, new Vector3(0.14f, 1.3f, 0.14f), white);
            mb.Box(tw + Vector3.up * 23.75f, new Vector3(0.75f, 0.14f, 0.14f), white);
            // the fence at the road: stone pillars, red pickets, open at the middle
            float[] px = { -8f, -5f, -2f, 2f, 5f, 8f };
            for (int k = 0; k < px.Length; k++)
            {
                mb.Box(new Vector3(px[k], 0.8f, 11.5f), new Vector3(0.7f, 1.6f, 0.7f), cap);
                mb.Pyramid(new Vector3(px[k], 1.6f, 11.5f), 0.9f, 0.9f, 0.45f, cap);
                if (k == px.Length - 1 || k == 2) continue;
                mb.Box(new Vector3((px[k] + px[k + 1]) * 0.5f, 0.45f, 11.5f), new Vector3(px[k + 1] - px[k], 0.07f, 0.07f), red);
                mb.Box(new Vector3((px[k] + px[k + 1]) * 0.5f, 1.05f, 11.5f), new Vector3(px[k + 1] - px[k], 0.07f, 0.07f), red);
                for (float x = px[k] + 0.5f; x < px[k + 1] - 0.4f; x += 0.24f) mb.Box(new Vector3(x, 0.8f, 11.5f), new Vector3(0.07f, 1.35f, 0.05f), red);
            }
        }

        // ------------------------------------------------------------------ Jama Masjid
        static void JamaMasjid(MeshBuilder mb, float depth)
        {
            const float W = 30f, D = 12f, F = 4.3f;   // width, depth, storey height
            Plinth(mb, new Vector3(W + 4f, 0f, D + 5f), depth, M(Atlas.MatMarble));
            Vector2 white = S(Pal.TempleWhite), dark = S(Pal.IronBlack), jali = S(Pal.StoneGrey), gold = S(Pal.Gold), wood = S(Pal.WoodDark), shade = S(Pal.Concrete);
            float zf = D * 0.5f;
            mb.Box(new Vector3(0f, F, 0f), new Vector3(W, F * 2f, D), white);

            void Jali(Vector3 jc, Vector3 jn)   // a bay: black outline, white panel, a pierced screen under a cusped head
            {
                var jq = Quaternion.LookRotation(jn);
                mb.Box(jc + jn * 0.03f, new Vector3(1.75f, 3.1f, 0.05f), jq, dark);
                mb.Box(jc + jn * 0.06f, new Vector3(1.55f, 2.9f, 0.05f), jq, white);
                mb.Box(jc + Vector3.down * 0.35f + jn * 0.09f, new Vector3(0.95f, 1.9f, 0.05f), jq, jali);
                ArchPane(mb, jc + Vector3.up * 0.9f + jn * 0.12f, 0.95f, 0.6f, 0.55f, jali, jn);
            }
            void Chajja(float cy, float cz, float cw, float xc)   // a projecting slab with a row of lobes under its edge
            {
                mb.Box(new Vector3(xc, cy, cz + 0.45f), new Vector3(cw + 0.9f, 0.16f, 0.9f), white);
                for (float lx = xc - cw * 0.5f + 0.2f; lx <= xc + cw * 0.5f - 0.1f; lx += 1.0f)
                    mb.Sphere(new Vector3(lx, cy - 0.08f, cz + 0.86f), new Vector3(0.5f, 0.34f, 0.1f), 6, 3, white, -90f, 0f);
            }
            void Turret(Vector3 tb, float tr, float th)   // the little kiosks along every parapet
            {
                mb.Cylinder(tb, tr, tr * 0.9f, th, 6, white, false);
                mb.Cylinder(tb + Vector3.up * th, tr * 1.6f, tr * 1.6f, 0.07f, 6, white, true, true);
                mb.Sphere(tb + Vector3.up * (th + 0.07f), new Vector3(tr * 1.5f, tr * 2.0f, tr * 1.5f), 6, 3, white, 0f, 90f);
                mb.Cylinder(tb + Vector3.up * (th + 0.07f + tr * 1.9f), 0.03f, 0f, 0.28f, 4, gold, false);
            }

            for (int floor = 0; floor < 2; floor++)
            {
                float y0 = floor * F;
                Chajja(y0 + F - 0.1f, zf, W, 0f);
                for (int b = 0; b < 12; b++)
                {
                    float x = -W * 0.5f + 1.25f + b * 2.5f;
                    if (Mathf.Abs(x) < 3.2f) continue;   // the portal
                    Jali(new Vector3(x, y0 + 1.95f, zf), Vector3.forward);
                }
                foreach (float sx in new[] { -1f, 1f })
                    for (int b = 0; b < 4; b++)
                        Jali(new Vector3(sx * W * 0.5f, y0 + 1.95f, -zf + 1.5f + b * 3f), sx > 0 ? Vector3.right : Vector3.left);
            }
            // the parapet with its row of kiosks
            float roof = F * 2f;
            mb.Box(new Vector3(0f, roof + 0.45f, zf - 0.12f), new Vector3(W, 0.9f, 0.24f), white);
            mb.Box(new Vector3(0f, roof + 0.45f, -zf + 0.12f), new Vector3(W, 0.9f, 0.24f), white);
            foreach (float sx in new[] { -1f, 1f }) mb.Box(new Vector3(sx * (W * 0.5f - 0.12f), roof + 0.45f, 0f), new Vector3(0.24f, 0.9f, D), white);
            for (int b = 0; b <= 12; b++)
            {
                float x = -W * 0.5f + b * 2.5f;
                if (Mathf.Abs(x) < 3.4f || b == 0) continue;
                Turret(new Vector3(x, roof + 0.9f, zf - 0.12f), 0.17f, 0.9f);
            }
            // the portal: taller than the wall, a deep pointed arch round the door, slim turrets at its sides
            float p = zf + 1.1f;
            mb.Box(new Vector3(0f, 5.3f, zf + 0.5f), new Vector3(6.2f, 10.6f, 1.2f), white);
            mb.Box(new Vector3(0f, 5.0f, p + 0.01f), new Vector3(4.6f, 9.2f, 0.04f), dark);
            mb.Box(new Vector3(0f, 5.0f, p + 0.03f), new Vector3(4.4f, 9.0f, 0.04f), white);
            ArchPane(mb, new Vector3(0f, 3.4f, p + 0.06f), 3.4f, 6.6f, 2.0f, shade, Vector3.forward);
            ArchPane(mb, new Vector3(0f, 1.6f, p + 0.08f), 2.0f, 3.2f, 1.0f, wood, Vector3.forward);
            mb.Box(new Vector3(0f, 11.05f, zf + 0.5f), new Vector3(6.2f, 0.9f, 0.3f), white);
            foreach (float x in new[] { -3.1f, 3.1f })
            {
                Oct(mb, new Vector3(x, 0f, zf + 1.0f), 0.42f, 0.36f, 11.6f, white);
                Turret(new Vector3(x, 11.6f, zf + 1.0f), 0.3f, 1.0f);
            }
            for (float x = -2f; x <= 2.01f; x += 1f) Turret(new Vector3(x, 11.5f, zf + 0.5f), 0.14f, 0.6f);
            // the upper prayer room, set back, an arcade along its front, and the ribbed dome over it
            Vector3 u = new Vector3(-2.5f, roof, -1.2f);   // seen from the Flats: the dome right of the middle, the minaret at the left end
            mb.Box(u + Vector3.up * 1.6f, new Vector3(19f, 3.2f, 7f), white);
            for (int b = 0; b < 7; b++) ArchPane(mb, u + new Vector3(-7.8f + b * 2.6f, 1.25f, 3.52f), 1.5f, 1.7f, 0.75f, shade, Vector3.forward);
            Chajja(roof + 3.1f, u.z + 3.5f, 19f, u.x);
            mb.Box(u + new Vector3(0f, 3.5f, 3.4f), new Vector3(19f, 0.6f, 0.2f), white);
            for (float x = -9.5f; x <= 9.51f; x += 2.375f) Turret(u + new Vector3(x, 3.8f, 3.4f), 0.15f, 0.7f);
            Vector3 dm = u + new Vector3(-2f, 3.2f, 0f);
            Oct(mb, dm, 3.2f, 3.2f, 1.5f, white);
            mb.Cylinder(dm + Vector3.up * 1.5f, 3.35f, 3.35f, 0.14f, 16, white, true, true);
            mb.Sphere(dm + Vector3.up * 1.64f, new Vector3(3.2f, 3.5f, 3.2f), 16, 6, white, 0f, 90f);
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI / 8f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 last = dm + Vector3.up * 1.64f + dir * 3.22f;
                for (int s = 1; s <= 5; s++)
                {
                    float la = s * 17f * Mathf.Deg2Rad;
                    Vector3 q = dm + Vector3.up * (1.64f + Mathf.Sin(la) * 3.52f) + dir * (Mathf.Cos(la) * 3.22f);
                    mb.Limb(last, q, 0.05f, 0.05f, 4, shade, false);
                    last = q;
                }
            }
            mb.Sphere(dm + Vector3.up * 5.3f, Vector3.one * 0.3f, 8, 4, gold);
            mb.Cylinder(dm + Vector3.up * 5.5f, 0.07f, 0f, 1.0f, 5, gold, false);
            for (int k = 0; k < 8; k++)   // turrets round the dome's drum
            {
                float a = Mathf.PI / 8f + k * Mathf.PI / 4f;
                Turret(dm + new Vector3(Mathf.Cos(a) * 3.2f, 1.5f, Mathf.Sin(a) * 3.2f), 0.16f, 0.7f);
            }
            // the minaret at the corner: eight-sided to the roof, two balconies, a lantern and a pointed cap (about 21 m)
            Vector3 m = new Vector3(W * 0.5f - 1.7f, 0f, zf - 1.7f);
            Oct(mb, m, 1.55f, 1.5f, roof + 3.0f, white);
            foreach (var n in new[] { Vector3.forward, Vector3.right })
            {
                ArchPane(mb, m + Vector3.up * (roof + 1.4f) + n * (1.5f * Mathf.Cos(Mathf.PI / 8f) + 0.02f), 0.5f, 0.9f, 0.3f, shade, n);
                ArchPane(mb, m + Vector3.up * 6.2f + n * (1.52f * Mathf.Cos(Mathf.PI / 8f) + 0.02f), 0.5f, 0.9f, 0.3f, shade, n);
            }
            float y = roof + 3.0f;
            mb.Cylinder(m + Vector3.up * (y - 0.3f), 1.5f, 2.15f, 0.3f, 12, white, false);
            mb.Cylinder(m + Vector3.up * y, 2.15f, 2.15f, 0.16f, 12, white, true, true);
            mb.Cylinder(m + Vector3.up * (y + 0.16f), 2.05f, 2.05f, 0.55f, 12, white, false);
            mb.Cylinder(m + Vector3.up * (y + 0.16f), 1.02f, 0.95f, 5.0f, 12, white, false);
            y += 5.16f;
            mb.Cylinder(m + Vector3.up * (y - 0.25f), 0.95f, 1.6f, 0.25f, 12, white, false);
            mb.Cylinder(m + Vector3.up * y, 1.6f, 1.6f, 0.14f, 12, white, true, true);
            mb.Cylinder(m + Vector3.up * (y + 0.14f), 1.52f, 1.52f, 0.45f, 12, white, false);
            mb.Cylinder(m + Vector3.up * (y + 0.14f), 0.78f, 0.74f, 2.3f, 10, white, false);
            for (int k = 0; k < 4; k++)
            {
                Vector3 n = Quaternion.Euler(0f, k * 90f, 0f) * Vector3.forward;
                ArchPane(mb, m + Vector3.up * (y + 1.2f) + n * 0.78f, 0.4f, 0.9f, 0.3f, shade, n);
            }
            y += 2.44f;
            mb.Cylinder(m + Vector3.up * y, 1.08f, 1.08f, 0.18f, 12, white, true, true);
            mb.Sphere(m + Vector3.up * (y + 0.18f), new Vector3(0.85f, 1.35f, 0.85f), 12, 5, white, 0f, 90f);
            mb.Sphere(m + Vector3.up * (y + 1.7f), Vector3.one * 0.2f, 6, 4, gold);
            mb.Cylinder(m + Vector3.up * (y + 1.85f), 0.05f, 0f, 0.7f, 5, gold, false);
        }
    }
}
