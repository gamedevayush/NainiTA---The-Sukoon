using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>Mallital's lakefront as 2025 videos show it: red tiles underfoot, a maroon iron railing of squares within
    /// squares at the water, a green iron fence round the children's park, and cafes and souvenir shops along the lake side
    /// of the Capitol building.</summary>
    public static partial class WorldBuilder
    {
        const float MallitalFrontRadius = 240f;
        static Material redTileMat;

        /// <summary>On Mallital's lakefront (round the Flats, short of Mall Road's own railing)?</summary>
        static bool InMallitalFront(WorldCtx ctx, Vector2 p)
        {
            var flats = ctx.Poi("Nainital Stadium");
            return flats.HasValue && Vector2.Distance(p, flats.Value) < MallitalFrontRadius && !NearMallRoad(ctx, p, 40f);
        }

        /// <summary>Square terracotta tiles in pale grout, a little uneven in tone (4 x 4 per texture).</summary>
        static Material RedTileMaterial()
        {
            string texPath = Gen + "/T_MallitalTiles.png", matPath = Gen + "/M_MallitalTiles.mat";
            const int S = 256;
            var px = new Color32[S * S];
            var rnd = new System.Random(4242);
            var tone = new float[16];
            for (int i = 0; i < 16; i++) tone[i] = 0.86f + (float)rnd.NextDouble() * 0.22f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int tx = x / 64, ty = y / 64, lx = x % 64, ly = y % 64;
                    bool grout = lx < 3 || ly < 3;
                    float n = Mathf.PerlinNoise(x * 0.09f, y * 0.09f) * 0.12f + Mathf.PerlinNoise(x * 0.5f, y * 0.5f) * 0.06f;
                    Color c = grout ? new Color(0.62f, 0.58f, 0.54f) : new Color(0.66f, 0.3f, 0.22f) * (tone[ty * 4 + tx] + n - 0.09f);
                    c.a = 1f;
                    px[y * S + x] = c;
                }
            var tex = SaveTexture(px, S, S, texPath, false, false);
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, matPath); }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.35f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>The lakefront railing: a frame round squares within squares, joined at the middle.</summary>
        static Ink SquareLoopPanel()
        {
            var k = new Ink(512, 256);
            const float t = 3.4f;
            k.Rect(5f, 0.01f, 0.03f, 0.99f, 0.97f);
            for (int c = 0; c < 4; c++)
            {
                float x0 = c / 4f + 0.03f, x1 = (c + 1) / 4f - 0.03f, mx = (x0 + x1) * 0.5f, hx = (x1 - x0) * 0.24f;
                k.Rect(t, x0, 0.12f, x1, 0.88f);
                k.Rect(t, mx - hx, 0.33f, mx + hx, 0.67f);
                k.Line(t, new Vector2(x0, 0.5f), new Vector2(mx - hx, 0.5f));
                k.Line(t, new Vector2(mx + hx, 0.5f), new Vector2(x1, 0.5f));
            }
            return k;
        }

        static void BuildMallitalLakefront(WorldCtx ctx)
        {
            var flats = ctx.Poi("Nainital Stadium");
            if (!flats.HasValue) return;
            var parent = new GameObject("Mallital lakefront").transform;
            parent.SetParent(ctx.worldRoot, false);

            // ---- the maroon railing at the water
            var maroon = new Color(0.32f, 0.08f, 0.07f);
            var panelMat = IronMaterial("MallitalSquares", SquareLoopPanel(), maroon);
            var panels = new MallChunks(ctx, parent, "Mallital railing", panelMat, true, 0.16f);
            var posts = new MeshBuilder();
            var col = new MeshBuilder();
            Vector2 postUV = Atlas.SolidUV(Pal.BoatRed);
            var rail = Resample(Offset(ctx.lake, 1.2f), 2.4f, true);
            int n = 0;
            for (int i = 0; i + 1 < rail.Count; i++)
            {
                Vector2 a = rail[i], b = rail[i + 1];
                if (!InMallitalFront(ctx, a) || !InMallitalFront(ctx, b) || NearJetty(ctx, a, 5f) || NearJetty(ctx, b, 5f)) continue;
                float ya = ctx.H(a), yb = ctx.H(b);
                Vector3 a3 = V(a, ya + 0.08f), b3 = V(b, yb + 0.08f);
                Vector3 outward = Flat(OutwardAt(ctx, a));
                Panel(panels.At(a)[0], a3, b3, 1.0f, outward);
                posts.Box(V(a, ya) + Vector3.up * 0.58f, new Vector3(0.11f, 1.16f, 0.11f), Quaternion.LookRotation(Flat(b - a)), postUV);
                posts.Box(V(a, ya) + Vector3.up * 1.18f, new Vector3(0.16f, 0.06f, 0.16f), Quaternion.LookRotation(Flat(b - a)), postUV);
                BlockBothWays(col, a3, b3, b3 + Vector3.up * 1.1f, a3 + Vector3.up * 1.1f, outward);
                n++;
            }
            panels.Flush();
            ctx.MeshObject("Mallital railing posts", parent, posts, ctx.mats.atlas, col);

            // ---- a green iron fence round the parks by the Capitol, with a gap to walk in
            var cap = CapitolBox(ctx);
            Vector2 near = cap.HasValue ? cap.Value.c : flats.Value;
            var fence = new MeshBuilder();
            var fcol = new MeshBuilder();
            Vector2 green = Atlas.SolidUV(Pal.BoatGreen);   // bright painted green, as in the 2025 video
            int parks = 0;
            foreach (var ar in ctx.world.areas)
            {
                if (ar.kind != "park") continue;
                var poly = Geo.ToPoints(ar.pts);
                if (poly.Length < 3) continue;
                Vector2 pc = Vector2.zero;
                foreach (var q in poly) pc += q;
                pc /= poly.Length;
                if (Vector2.Distance(pc, near) > 150f) continue;
                parks++;
                int gateEdge = 0;
                float gd = float.MaxValue;
                for (int i = 0; i < poly.Length; i++)
                {
                    float d = ctx.LakeDistance((poly[i] + poly[(i + 1) % poly.Length]) * 0.5f);
                    if (d < gd) { gd = d; gateEdge = i; }   // the gate opens on the promenade
                }
                for (int i = 0; i < poly.Length; i++)
                {
                    Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                    float len = Vector2.Distance(a, b);
                    if (len < 0.5f) continue;
                    Vector2 dir = (b - a) / len;
                    var rot = Quaternion.LookRotation(Flat(dir));
                    for (float s = 0f; s < len; s += 0.16f)
                    {
                        if (i == gateEdge && Mathf.Abs(s - len * 0.5f) < 1.6f) continue;
                        Vector2 q = a + dir * s;
                        float y = ctx.H(q);
                        bool post = Mathf.Repeat(s, 2.4f) < 0.16f;
                        fence.Box(V(q, y) + Vector3.up * (post ? 0.65f : 0.58f), post ? new Vector3(0.08f, 1.3f, 0.08f) : new Vector3(0.025f, 1.16f, 0.025f), rot, green);
                    }
                    foreach (float hgt in new[] { 0.25f, 1.05f })
                    {
                        if (i == gateEdge)
                        {
                            float g0 = len * 0.5f - 1.6f, g1 = len * 0.5f + 1.6f;
                            if (g0 > 0.2f) fence.Box(V(a + dir * (g0 * 0.5f), ctx.H(a + dir * (g0 * 0.5f))) + Vector3.up * hgt, new Vector3(0.04f, 0.04f, g0), rot, green);
                            if (len - g1 > 0.2f) fence.Box(V(a + dir * ((g1 + len) * 0.5f), ctx.H(a + dir * ((g1 + len) * 0.5f))) + Vector3.up * hgt, new Vector3(0.04f, 0.04f, len - g1), rot, green);
                        }
                        else fence.Box(V((a + b) * 0.5f, ctx.H((a + b) * 0.5f)) + Vector3.up * hgt, new Vector3(0.04f, 0.04f, len), rot, green);
                    }
                    if (i != gateEdge) fcol.Box(V((a + b) * 0.5f, ctx.H((a + b) * 0.5f)) + Vector3.up * 0.65f, new Vector3(0.1f, 1.3f, len), rot, Vector2.zero);
                }
            }
            ctx.MeshObject("Children's park fence", parent, fence, ctx.mats.atlas, fcol);
            Debug.Log($"[Nainital] Mallital lakefront: {n} railing panels, {parks} park fences");
        }

        /// <summary>Cafes and souvenir shops along the Capitol's lake side, opening onto the promenade.</summary>
        static void CapitolLakeShops(WorldCtx ctx, Transform parent, MeshBuilder mb, Font font, Vector2 c2, Vector2 f2, float W, float D)
        {
            Vector2 r2 = new Vector2(f2.y, -f2.x);
            Vector2 side = ctx.LakeDistance(c2 + r2 * (W * 0.5f)) < ctx.LakeDistance(c2 - r2 * (W * 0.5f)) ? r2 : -r2;
            Vector2 along = new Vector2(-side.y, side.x);
            var rot = Quaternion.LookRotation(Flat(side));
            string[] names = { "CHAI & COFFEE", "I LOVE NAINITAL", "MOMOS", "SOUVENIRS", "CAFE" };
            int[] awn = { Pal.BoatRed, Pal.Awning, Pal.BoatGreen, Pal.Canopy, Pal.BoatRed };
            var holder = new GameObject("Capitol lake-side shops").transform;
            holder.SetParent(parent, false);
            int bays = Mathf.Clamp(Mathf.RoundToInt(D / 3.6f), 3, 5);
            for (int i = 0; i < bays; i++)
            {
                float s = ((i + 0.5f) / bays - 0.5f) * (D - 1f);
                Vector2 q = c2 + side * (W * 0.5f + 0.06f) + along * s;
                float y = ctx.H(q);
                Vector3 p = V(q, y), out3 = Flat(side), al3 = Flat(along);
                float bw = D / bays - 0.5f;
                mb.Box(p + Vector3.up * 1.35f, new Vector3(bw, 2.5f, 0.08f), rot, Atlas.SolidUV(Pal.IronBlack));                 // glass front
                mb.Box(p + Vector3.up * 1.35f + out3 * 0.05f, new Vector3(bw - 0.4f, 2.2f, 0.04f), rot, Atlas.SolidUV(Pal.WindowGlow));   // lit inside
                mb.Box(p + Vector3.up * 3.0f + out3 * 0.08f, new Vector3(bw + 0.2f, 0.6f, 0.08f), rot, Atlas.SolidUV(i % 2 == 0 ? Pal.IronBlack : Pal.TempleRed));
                Label(holder, font, names[i % names.Length], p + Vector3.up * 3.0f + out3 * 0.14f, out3, 0.24f, i % 2 == 0 ? new Color(1f, 0.85f, 0.4f) : Color.white);
                Vector2 aw = Atlas.SolidUV(awn[i % awn.Length]);
                mb.QuadDouble(p + Vector3.up * 2.6f - al3 * (bw * 0.5f), p + Vector3.up * 2.6f + al3 * (bw * 0.5f),
                              p + Vector3.up * 2.15f + al3 * (bw * 0.5f) + out3 * 1.2f, p + Vector3.up * 2.15f - al3 * (bw * 0.5f) + out3 * 1.2f, new Rect(aw.x, aw.y, 0f, 0f), Vector3.up);
                mb.Box(p + Vector3.up * 0.5f + out3 * 0.45f, new Vector3(bw * 0.6f, 1f, 0.5f), rot, Atlas.SolidUV(Pal.WoodLight));   // counter
            }
        }
    }
}
