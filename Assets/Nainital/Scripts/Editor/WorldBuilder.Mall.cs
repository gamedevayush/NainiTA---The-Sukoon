using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nainital.EditorTools
{
    /// <summary>Mall Road as Street View (Aug 2022) shows it. Upper Mall is the two-lane motor road against a row of shops
    /// and hotels. Lower Mall is a narrower road with white edge lines all the way from Tallital to Mallital, with a parapet
    /// and green iron railing on its lake side under big trees. Between them a mossy stone wall, ~1.6 m high at Tallital
    /// and ~2.2 m from the middle on (process_data.py: MALL_STEP_*), carries heavy cast-iron railing.</summary>
    public static partial class WorldBuilder
    {
        public const string MallUpper = "Upper Mall Road", MallLower = "Lower Mall Road";
        const float MallHalf = 3f;            // half a carriageway (process_data.py: MALL_WIDTH)
        const float LowerHalf = 2.4f;         // Lower Mall's centre line to its lake parapet: it is narrower than Upper (process_data.py: LOWER_HALF)
        const float MallStrip = 1.55f;        // Upper Mall's edge to the outer face of the wall
        const float ShopSetback = 2.6f;       // Upper Mall's edge to the shop fronts (process_data.py: SHOP_SETBACK)
        const float PlinthH = 0.35f;

        static bool IsMallDeck(string name) => name == MallUpper || name == MallLower;

        sealed class MallLine
        {
            public List<Vector2> p;
            public Vector2[] arr, tan, side;
            public float[] y, t;   // t: 0 at the south end, 1 at the north end
            public float[] lakeW, hillW;   // Lower Mall only: paved width from the centre line to each side
            public int Count => p.Count;
        }

        static WorldCtx roadCellsFor;
        static HashSet<long> roadCells;

        /// <summary>Is p on or within a few metres of any road? (4 m cells, built once per world)</summary>
        internal static bool NearAnyRoad(WorldCtx ctx, Vector2 p)
        {
            if (roadCellsFor != ctx)
            {
                roadCellsFor = ctx;
                roadCells = new HashSet<long>();
                foreach (var r in ctx.world.roads)
                {
                    var line = Geo.ToPoints(r.pts);
                    if (line.Length < 2) continue;
                    int rc = Mathf.CeilToInt((r.width * 0.5f + 5f) / 4f);
                    foreach (var q in Resample(line, 2f, false))
                    {
                        int cx = Mathf.FloorToInt(q.x / 4f), cz = Mathf.FloorToInt(q.y / 4f);
                        for (int dz = -rc; dz <= rc; dz++)
                            for (int dx = -rc; dx <= rc; dx++) roadCells.Add(((long)(cx + dx) << 32) ^ (uint)(cz + dz));
                    }
                }
            }
            return roadCells.Contains(((long)Mathf.FloorToInt(p.x / 4f) << 32) ^ (uint)Mathf.FloorToInt(p.y / 4f));
        }

        /// <summary>Shop-sign text that is hidden behind trees and walls (the built-in font material draws on top of everything).</summary>
        public static Material SignTextMaterial(Font font)
        {
            var sh = Shader.Find("Nainital/Text3D");
            if (!sh || !font) return font ? font.material : null;
            string path = Gen + "/M_SignText.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = sh;
            m.mainTexture = font.material.mainTexture;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Texture2D SaveTexture(Color32[] px, int w, int h, string path, bool clamp, bool alphaCut)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.alphaIsTransparency = alphaCut;
            imp.mipmapEnabled = true;
            imp.mipMapsPreserveCoverage = alphaCut;
            imp.alphaTestReferenceValue = 0.4f;
            imp.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            imp.anisoLevel = 4;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Grey cobbles of mixed tone in dark joints, as on the promenade (10 x 10 stones per tile).</summary>
        static Material CobbleMaterial()
        {
            var photo = PhotoMaterial("cobblestone_floor_08", "M_MallCobblePhoto", new Color(0.92f, 0.92f, 0.9f), 0.45f);
            if (photo) return photo;
            const int S = 512, C = 10;
            var rnd = new System.Random(909);
            var pts = new Vector2[C, C];
            var tone = new float[C, C];
            for (int y = 0; y < C; y++)
                for (int x = 0; x < C; x++)
                {
                    pts[x, y] = new Vector2(x + 0.2f + (float)rnd.NextDouble() * 0.6f, y + 0.2f + (float)rnd.NextDouble() * 0.6f);
                    tone[x, y] = (float)rnd.NextDouble();
                }
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    var q = new Vector2((x + 0.5f) * C / S, (y + 0.5f) * C / S);
                    int cx = Mathf.FloorToInt(q.x), cy = Mathf.FloorToInt(q.y);
                    float d1 = 9f, d2 = 9f, t1 = 0f;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int gx = cx + dx, gy = cy + dy, wx = (gx % C + C) % C, wy = (gy % C + C) % C;
                            float d = Vector2.Distance(q, pts[wx, wy] + new Vector2(gx - wx, gy - wy));
                            if (d < d1) { d2 = d1; d1 = d; t1 = tone[wx, wy]; }
                            else if (d < d2) d2 = d;
                        }
                    float edge = d2 - d1;
                    float n = Mathf.PerlinNoise(x * 0.07f, y * 0.07f) * 0.08f + Mathf.PerlinNoise(x * 0.31f + 40f, y * 0.29f) * 0.05f;
                    float g = (0.4f + t1 * 0.2f + n) * Mathf.Lerp(0.74f, 1f, Mathf.Clamp01(edge / 0.2f));
                    if (edge < 0.055f) g = 0.2f + n * 0.5f;
                    px[y * S + x] = new Color(g * 1.02f, g, g * 0.96f, 1f);
                }
            var tex = SaveTexture(px, S, S, Gen + "/T_MallCobble.png", false, false);
            string path = Gen + "/M_MallCobble.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.12f);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Groups meshes into map cells; optional distance culling for fine detail.</summary>
        sealed class MallChunks
        {
            readonly WorldCtx ctx;
            readonly Transform parent;
            readonly string name;
            readonly Material mat;
            readonly bool shadows;
            readonly float cull;
            readonly Dictionary<Vector2Int, MeshBuilder[]> cells = new Dictionary<Vector2Int, MeshBuilder[]>();

            public MallChunks(WorldCtx c, Transform p, string n, Material m, bool castShadows, float cullHeight = 0f)
            {
                ctx = c; parent = p; name = n; mat = m; shadows = castShadows; cull = cullHeight;
            }

            public MeshBuilder[] At(Vector2 q)
            {
                var key = new Vector2Int(Mathf.FloorToInt(q.x / 90f), Mathf.FloorToInt(q.y / 90f));
                if (!cells.TryGetValue(key, out var b)) cells[key] = b = new[] { new MeshBuilder(), new MeshBuilder() };
                return b;
            }

            public void Flush()
            {
                foreach (var kv in cells)
                {
                    var go = ctx.MeshObject($"{name} {kv.Key.x},{kv.Key.y}", parent, kv.Value[0], mat, kv.Value[1], shadows);
                    var r = go.GetComponent<MeshRenderer>();
                    if (!r || cull <= 0f) continue;
                    var group = go.AddComponent<LODGroup>();
                    group.SetLODs(new[] { new LOD(cull, new Renderer[] { r }) });
                    group.RecalculateBounds();
                }
            }
        }

        static Vector3 V(Vector2 q, float y) => new Vector3(q.x, y, q.y);
        static Vector3 Flat(Vector2 d) => new Vector3(d.x, 0f, d.y);

        static Vector2[] RoadPoints(WorldCtx ctx, string name)
        {
            foreach (var r in ctx.world.roads)
                if (r.name == name) return Geo.ToPoints(r.pts);
            return null;
        }

        static int NearestIndex(IList<Vector2> line, Vector2 p)
        {
            int best = 0;
            float bd = float.MaxValue;
            for (int i = 0; i < line.Count; i++)
            {
                float d = (line[i] - p).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        /// <summary>The part of a road between two fractions of its length, measured from the south end.</summary>
        static Vector2[] MallPart(Vector2[] raw, float t0, float t1)
        {
            var r = Resample(raw, 2f, false);
            if (r[0].y > r[r.Count - 1].y) r.Reverse();
            int a = Mathf.FloorToInt(t0 * (r.Count - 1)), b = Mathf.CeilToInt(t1 * (r.Count - 1));
            return r.GetRange(a, b - a + 1).ToArray();
        }

        /// <summary>A Mall road as evenly spaced samples; side[] points away from the other road.</summary>
        static MallLine MallPath(WorldCtx ctx, string name, string other, float step)
        {
            var raw = RoadPoints(ctx, name);
            var rawOther = RoadPoints(ctx, other);
            if (raw == null || rawOther == null || raw.Length < 2 || rawOther.Length < 2) return null;
            var m = new MallLine { p = Resample(raw, step, false) };
            var oth = Resample(rawOther, 3f, false);
            int n = m.Count;
            m.arr = m.p.ToArray();
            m.tan = new Vector2[n];
            m.side = new Vector2[n];
            m.y = new float[n];
            m.t = new float[n];
            bool southFirst = m.p[0].y < m.p[n - 1].y;
            var h = new float[n];
            float sum = 0f;
            for (int i = 0; i < n; i++)
            {
                m.t[i] = southFirst ? i / (n - 1f) : 1f - i / (n - 1f);
                m.tan[i] = (m.p[Mathf.Min(i + 1, n - 1)] - m.p[Mathf.Max(i - 1, 0)]).normalized;
                m.side[i] = new Vector2(-m.tan[i].y, m.tan[i].x);
                sum += Vector2.Dot(m.p[i] - oth[NearestIndex(oth, m.p[i])], m.side[i]);
                h[i] = ctx.H(m.p[i]);
            }
            if (sum < 0f)
                for (int i = 0; i < n; i++) m.side[i] = -m.side[i];
            for (int i = 0; i < n; i++)
            {
                float t = 0f;
                for (int k = -2; k <= 2; k++) t += h[Mathf.Clamp(i + k, 0, n - 1)];
                m.y[i] = t / 5f;
            }
            return m;
        }

        // ------------------------------------------------------------------ cast-iron panels (alpha-cut textures)
        sealed class Ink
        {
            readonly int w, h;
            readonly byte[] a;
            public Ink(int width, int height) { w = width; h = height; a = new byte[w * h]; }

            public void Seg(Vector2 p, Vector2 q, float t)
            {
                p = new Vector2(p.x * w, p.y * h);
                q = new Vector2(q.x * w, q.y * h);
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(p.x, q.x) - t - 1)), x1 = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(p.x, q.x) + t + 1));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(p.y, q.y) - t - 1)), y1 = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(p.y, q.y) + t + 1));
                Vector2 ab = q - p;
                float len2 = Mathf.Max(ab.sqrMagnitude, 1e-4f);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        var c = new Vector2(x + 0.5f, y + 0.5f);
                        float s = Mathf.Clamp01(Vector2.Dot(c - p, ab) / len2);
                        float d = Vector2.Distance(c, p + ab * s);
                        byte v = (byte)(Mathf.Clamp01(t + 0.5f - d) * 255f);
                        if (v > a[y * w + x]) a[y * w + x] = v;
                    }
            }

            public void Line(float t, params Vector2[] pts)
            {
                for (int i = 0; i + 1 < pts.Length; i++) Seg(pts[i], pts[i + 1], t);
            }

            public void Rect(float t, float x0, float y0, float x1, float y1)
            {
                Line(t, new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1), new Vector2(x0, y0));
            }

            /// <summary>An arc or spiral. rx/ry are the start radii, shrinking by 'taper' over the sweep (degrees).</summary>
            public void Arc(float t, Vector2 c, float rx, float ry, float from, float sweep, float taper = 0f)
            {
                int n = Mathf.Max(8, Mathf.CeilToInt(Mathf.Abs(sweep) / 12f));
                Vector2 last = default;
                for (int i = 0; i <= n; i++)
                {
                    float f = i / (float)n, a2 = (from + sweep * f) * Mathf.Deg2Rad, k = 1f - taper * f;
                    var q = c + new Vector2(Mathf.Cos(a2) * rx * k, Mathf.Sin(a2) * ry * k);
                    if (i > 0) Seg(last, q, t);
                    last = q;
                }
            }

            public Texture2D Bake(Color col, string path)
            {
                var px = new Color32[w * h];
                Color32 c = col;
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(c.r, c.g, c.b, a[i]);
                return SaveTexture(px, w, h, path, true, true);
            }
        }

        /// <summary>Heavy railing on the wall between the roads: a lattice band over big C-scrolls.</summary>
        static Ink FiligreePanel()
        {
            var k = new Ink(512, 256);
            const float t = 2.6f;
            k.Rect(5f, 0.012f, 0.02f, 0.988f, 0.98f);
            k.Line(4f, new Vector2(0f, 0.72f), new Vector2(1f, 0.72f));
            k.Line(3f, new Vector2(0f, 0.9f), new Vector2(1f, 0.9f));
            for (int i = 0; i < 12; i++)
            {
                float x = (i + 0.5f) / 12f;
                k.Arc(t, new Vector2(x, 0.81f), 0.034f, 0.068f, 0f, 360f);
                k.Line(t, new Vector2(x - 0.04f, 0.72f), new Vector2(x + 0.04f, 0.9f));
                k.Line(t, new Vector2(x + 0.04f, 0.72f), new Vector2(x - 0.04f, 0.9f));
            }
            for (int c = 0; c < 3; c++)
            {
                float cx = (c + 0.5f) / 3f;
                const float R = 0.075f, H = 0.26f;
                foreach (float s in new[] { -1f, 1f })
                {
                    var o = new Vector2(cx + s * 0.082f, 0.4f);
                    k.Arc(t + 1f, o, R, H, s > 0 ? 200f : -20f, s > 0 ? -560f : 560f, 0.82f);
                    k.Arc(t, new Vector2(cx + s * 0.13f, 0.14f), 0.04f, 0.09f, s > 0 ? 180f : 0f, s > 0 ? -180f : 180f);
                    k.Arc(t, new Vector2(cx + s * 0.06f, 0.63f), 0.045f, 0.075f, s > 0 ? 180f : 0f, s > 0 ? 180f : -180f);
                    k.Line(t, new Vector2(cx + s * 0.158f, 0.03f), new Vector2(cx + s * 0.158f, 0.72f));
                }
                k.Line(t, new Vector2(cx, 0.03f), new Vector2(cx, 0.72f));
                k.Arc(t, new Vector2(cx, 0.4f), 0.022f, 0.044f, 0f, 360f);
            }
            return k;
        }

        /// <summary>Lane railing: a row of linked rounded rectangles.</summary>
        static Ink ChainPanel()
        {
            var k = new Ink(512, 256);
            const float t = 3.2f;
            k.Line(5f, new Vector2(0f, 0.96f), new Vector2(1f, 0.96f));
            k.Line(5f, new Vector2(0f, 0.05f), new Vector2(1f, 0.05f));
            for (int c = 0; c < 5; c++)
            {
                float x0 = c / 5f + 0.012f, x1 = (c + 1) / 5f - 0.012f;
                const float y0 = 0.1f, y1 = 0.9f, rx = 0.03f, ry = 0.15f;
                k.Line(t, new Vector2(x0 + rx, y0), new Vector2(x1 - rx, y0));
                k.Line(t, new Vector2(x0 + rx, y1), new Vector2(x1 - rx, y1));
                k.Line(t, new Vector2(x0, y0 + ry), new Vector2(x0, y1 - ry));
                k.Line(t, new Vector2(x1, y0 + ry), new Vector2(x1, y1 - ry));
                k.Arc(t, new Vector2(x0 + rx, y0 + ry), rx, ry, 180f, 90f);
                k.Arc(t, new Vector2(x1 - rx, y0 + ry), rx, ry, 270f, 90f);
                k.Arc(t, new Vector2(x1 - rx, y1 - ry), rx, ry, 0f, 90f);
                k.Arc(t, new Vector2(x0 + rx, y1 - ry), rx, ry, 90f, 90f);
            }
            return k;
        }

        static Material IronMaterial(string name, Ink ink, Color col)
        {
            string tex = Gen + "/T_" + name + ".png", path = Gen + "/M_" + name + ".mat";
            var t = ink.Bake(col, tex);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", t);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.4f);
            m.SetFloat("_Cull", (float)CullMode.Back);   // both faces are real geometry: a back face would take the front's normal and glare white
            m.SetFloat("_Smoothness", 0.35f);
            m.SetFloat("_Metallic", 0.25f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>A see-through iron panel, one face each way; the back face is mapped so the pattern lines up.</summary>
        static void Panel(MeshBuilder mb, Vector3 a, Vector3 b, float h, Vector3 outward)
        {
            mb.Quad(a, b, b + Vector3.up * h, a + Vector3.up * h, new Rect(0f, 0f, 1f, 1f), outward);
            mb.Quad(b, a, a + Vector3.up * h, b + Vector3.up * h, new Rect(1f, 0f, -1f, 1f), -outward);
        }

        /// <summary>A box whose sides use a tiling cell of the atlas (stone, plaster).</summary>
        static void TexBox(MeshBuilder mb, Vector3 centre, Vector3 size, Quaternion rot, Rect uv)
        {
            Vector3 x = rot * Vector3.right * (size.x * 0.5f), y = Vector3.up * (size.y * 0.5f), z = rot * Vector3.forward * (size.z * 0.5f);
            void Face(Vector3 c, Vector3 u, Vector3 n) => mb.Quad(c - u - y, c + u - y, c + u + y, c - u + y, uv, n.normalized);
            Face(centre + z, x, z);
            Face(centre - z, -x, -z);
            Face(centre + x, -z, x);
            Face(centre - x, z, -x);
            Vector3 top = centre + y;
            mb.Quad(top - x - z, top + x - z, top + x + z, top - x + z, uv, Vector3.up);
        }

        // ------------------------------------------------------------------ entry
        static void BuildMallRoad(WorldCtx ctx)
        {
            var U = MallPath(ctx, MallUpper, MallLower, 2f);
            var L = MallPath(ctx, MallLower, MallUpper, 1f);
            if (U == null || L == null)
            {
                Debug.LogWarning("[Nainital] Mall Road: Upper/Lower Mall not found in world.json");
                return;
            }
            var parent = new GameObject("Mall Road").transform;
            parent.SetParent(ctx.worldRoot, false);

            var teal = new Color(0.09f, 0.27f, 0.24f);
            var filigree = IronMaterial("MallFiligree", FiligreePanel(), teal);
            var chain = IronMaterial("MallChain", ChainPanel(), teal);

            var cobble = CobbleMaterial();

            var low = new int[U.Count];
            for (int i = 0; i < U.Count; i++) low[i] = NearestIndex(L.p, U.p[i]);
            var stairs = new List<int> { U.Count / 5, U.Count * 2 / 5, U.Count * 3 / 5, U.Count * 4 / 5 };

            // Lower Mall runs right up to the wall below Upper Mall; on the lake side it stops at the parapet
            // (process_data.py: carve_mall levels the ground to match).
            L.hillW = new float[L.Count];
            L.lakeW = new float[L.Count];
            for (int i = 0; i < L.Count; i++)
            {
                L.hillW[i] = Mathf.Clamp(Geo.DistanceToPolyline(U.arr, L.p[i], false) - (MallHalf + MallStrip) + 0.3f, MallHalf, 6f);
                L.lakeW[i] = LowerHalf + 0.22f;
            }

            MallLowerDeck(ctx, parent, L);
            MallMarkings(ctx, parent, U, L);
            MallDivider(ctx, parent, U, L, low, stairs, filigree);
            MallLakeEdge(ctx, parent, L, chain);
            MallLampsAndWires(ctx, parent, U, L, stairs);
            MallSplit(ctx, parent, U, L, low);
            MallHillEdge(ctx, parent, U, cobble);
            MallShopDressing(ctx, parent, U);

            void Marker(string name, int i)
            {
                Vector2 q = U.p[i] - U.side[i] * 1.7f;
                Vector2 f = new Vector2(U.side[i].y, -U.side[i].x);   // hill on the left, lake on the right
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(q.x, U.y[i] + 1.62f, q.y);
                go.transform.rotation = Quaternion.LookRotation(new Vector3(f.x, -0.03f, f.y));
            }
            Marker("MallShotA", U.Count * 3 / 10);
            Marker("MallShotB", U.Count * 7 / 10);

            void LowerMarker(string name, float t)
            {
                int i = 0;
                for (int k = 0; k < L.Count; k++) if (Mathf.Abs(L.t[k] - t) < Mathf.Abs(L.t[i] - t)) i = k;
                Vector2 f = new Vector2(-L.side[i].y, L.side[i].x);   // hill on the left, lake on the right
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(L.p[i].x, L.y[i] + 1.62f, L.p[i].y);
                go.transform.rotation = Quaternion.LookRotation(new Vector3(f.x, -0.02f, f.y));
            }
            LowerMarker("MallShotLane", 0.3f);
            LowerMarker("MallShotLowerNorth", 0.78f);

            void StreetViewMarker(string name, MallLine m, Vector2 pano, float heading)   // a Street View pano, snapped onto its road
            {
                int i = NearestIndex(m.p, pano);
                Vector2 f = new Vector2(Mathf.Sin(heading * Mathf.Deg2Rad), Mathf.Cos(heading * Mathf.Deg2Rad));
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(m.p[i].x, m.y[i] + 0.12f + 2.4f, m.p[i].y);   // the camera on the car's roof
                go.transform.rotation = Quaternion.LookRotation(new Vector3(f.x, 0f, f.y));
            }
            StreetViewMarker("MallShotSVLowerA", L, new Vector2(497.0f, -194.1f), 307f);    // 29.38875, 79.46012
            StreetViewMarker("MallShotSVLowerB", L, new Vector2(559.8f, -273.0f), 326f);    // 29.38804, 79.46077
            StreetViewMarker("MallShotSVUpperMid", U, new Vector2(648.1f, -355.1f), 312f);  // 29.38730, 79.46168
            StreetViewMarker("MallShotSVUpperWall", U, new Vector2(699.5f, -458.3f), 357f); // 29.38637, 79.46221
            Debug.Log($"[Nainital] Mall Road: {U.Count * 2} m upper, {L.Count} m lower, {stairs.Count} stairways");
        }

        // ------------------------------------------------------------------ Lower Mall surface
        static void MallLowerDeck(WorldCtx ctx, Transform parent, MallLine L)
        {
            var road = new MallChunks(ctx, parent, "Lower Mall road", ConcreteMaterial(ctx), false);
            float v = 0f;
            for (int i = 0; i + 1 < L.Count; i++)
            {
                float seg = Vector2.Distance(L.p[i], L.p[i + 1]);
                float y0 = L.y[i] + 0.12f, y1 = L.y[i + 1] + 0.12f;
                Vector2 a0 = L.p[i] + L.side[i] * L.lakeW[i], a1 = L.p[i + 1] + L.side[i + 1] * L.lakeW[i + 1];
                Vector2 b0 = L.p[i] - L.side[i] * L.hillW[i], b1 = L.p[i + 1] - L.side[i + 1] * L.hillW[i + 1];
                var c = road.At(L.p[i]);
                float across = L.lakeW[i] + L.hillW[i];
                var uv = new Rect(0f, v / 6f, across / 6f, seg / 6f);
                c[0].Quad(V(a0, y0), V(b0, y0), V(b1, y1), V(a1, y1), uv, Vector3.up);
                c[1].Quad(V(a0, y0), V(b0, y0), V(b1, y1), V(a1, y1), Vector2.zero, Vector3.up);
                v += seg;
            }
            road.Flush();
        }

        // ------------------------------------------------------------------ road paint
        static void Strip(MeshBuilder mb, MallLine m, float off, float width, Vector2 uv, float dashOn, float dashOff, float tMax = 2f, float[] offs = null)
        {
            float run = 0f;
            for (int i = 0; i + 1 < m.Count; i++)
            {
                float seg = Vector2.Distance(m.p[i], m.p[i + 1]);
                bool on = dashOn <= 0f || Mathf.Repeat(run, dashOn + dashOff) < dashOn;
                run += seg;
                if (!on || m.t[i] >= tMax) continue;
                float y0 = m.y[i] + 0.15f, y1 = m.y[i + 1] + 0.15f;
                float o0 = offs != null ? offs[i] : off, o1 = offs != null ? offs[i + 1] : off;
                Vector2 a = m.p[i] + m.side[i] * o0, b = m.p[i + 1] + m.side[i + 1] * o1;
                Vector2 h0 = m.side[i] * (width * 0.5f), h1 = m.side[i + 1] * (width * 0.5f);
                mb.Quad(V(a - h0, y0), V(a + h0, y0), V(b + h1, y1), V(b - h1, y1), uv, Vector3.up);
            }
        }

        static void MallMarkings(WorldCtx ctx, Transform parent, MallLine U, MallLine L)
        {
            var mb = new MeshBuilder();
            Vector2 white = Atlas.SolidUV(Pal.ShirtWhite);
            Strip(mb, U, MallHalf - 0.28f, 0.14f, white, 0f, 0f);
            Strip(mb, U, -(MallHalf - 0.28f), 0.14f, white, 0f, 0f);
            Strip(mb, U, 0f, 0.11f, white, 3f, 4f);
            // Lower Mall: edge lines only, end to end; the hill-side one keeps close to the wall, leaving a footway where the
            // wall stands back
            var hillLine = new float[L.Count];
            for (int i = 0; i < L.Count; i++) hillLine[i] = -(Mathf.Min(L.hillW[i] - 0.3f, LowerHalf + 0.8f) - 0.3f);
            Strip(mb, L, LowerHalf - 0.28f, 0.14f, white, 0f, 0f);
            Strip(mb, L, 0f, 0.14f, white, 0f, 0f, 2f, hillLine);
            ctx.MeshObject("Mall road paint", parent, mb, ctx.mats.atlas, null, false);
        }

        // ------------------------------------------------------------------ wall and railing between the two roads
        static void BlockBothWays(MeshBuilder col, Vector3 a0, Vector3 a1, Vector3 b1, Vector3 b0, Vector3 outward)
        {
            col.Quad(a0, a1, b1, b0, Vector2.zero, outward);
            col.Quad(a1, a0, b0, b1, Vector2.zero, -outward);
        }

        static void MallDivider(WorldCtx ctx, Transform parent, MallLine U, MallLine L, int[] low, List<int> stairs, Material iron)
        {
            var chunks = new MallChunks(ctx, parent, "Mall divider", ctx.mats.atlas, true, 0.05f);
            var panels = new MallChunks(ctx, parent, "Mall divider iron", iron, true, 0.16f);
            var mossMat = MossyStoneMaterial();
            var moss = mossMat ? new MallChunks(ctx, parent, "Mall divider stone", mossMat, true, 0.05f) : null;
            var along = new float[U.Count];   // distance along Upper Mall, for the stone courses
            for (int i = 1; i < U.Count; i++) along[i] = along[i - 1] + Vector2.Distance(U.p[i - 1], U.p[i]);
            var rnd = new System.Random(77);
            Rect plaster = Atlas.Material(Atlas.MatStone);   // mossy rubble stone (Street View 2022)
            Vector2 grime = Atlas.SolidUV(Pal.StoneGrey), conc = Atlas.MaterialUV(Atlas.MatConcrete), green = Atlas.SolidUV(Pal.IronGreen);
            var streak = new Rect(grime, Vector2.zero);

            bool AtStair(int i)
            {
                foreach (int s in stairs) if (i == s - 1 || i == s) return true;
                return false;
            }

            for (int i = 0; i + 1 < U.Count; i++)
            {
                if (AtStair(i) || Vector2.Distance(U.p[i], L.p[low[i]]) < MallHalf * 2f + 1.5f || Vector2.Distance(U.p[i + 1], L.p[low[i + 1]]) < MallHalf * 2f + 1.5f) continue;
                var b = chunks.At(U.p[i]);
                var p = panels.At(U.p[i]);
                Vector2 s0 = U.side[i], s1 = U.side[i + 1];
                Vector2 e0 = U.p[i] - s0 * MallHalf, e1 = U.p[i + 1] - s1 * MallHalf;
                Vector2 o0 = U.p[i] - s0 * (MallHalf + MallStrip), o1 = U.p[i + 1] - s1 * (MallHalf + MallStrip);
                Vector2 r0 = o0 + s0 * 0.2f, r1 = o1 + s1 * 0.2f;
                float t0 = U.y[i] + 0.14f, t1 = U.y[i + 1] + 0.14f;
                float f0 = L.y[low[i]] - 0.3f, f1 = L.y[low[i + 1]] - 0.3f;
                Vector3 away = Flat(-s0);

                b[0].Quad(V(e0, t0), V(o0, t0), V(o1, t1), V(e1, t1), conc, Vector3.up);
                if (moss != null) WallFace(moss.At(U.p[i])[0], o0, o1, f0, f1, t0, t1, along[i], along[i + 1], away, 2.5f);
                else b[0].Quad(V(o0, f0), V(o1, f1), V(o1, t1), V(o0, t0), plaster, away);
                Vector2 g0 = o0 - s0 * 0.012f, g1 = o1 - s1 * 0.012f;
                b[0].Quad(V(g0, f0), V(g1, f1), V(g1, f1 + 0.62f), V(g0, f0 + 0.62f), grime, away);
                for (int n = 0; n < 2; n++)
                    if (rnd.NextDouble() < 0.5)
                    {
                        float at = (float)rnd.NextDouble();
                        Vector2 c = Vector2.Lerp(g0, g1, at), d = Vector2.Lerp(g0, g1, Mathf.Min(1f, at + 0.09f));
                        float y = Mathf.Lerp(t0, t1, at);
                        b[0].Quad(V(c, y - 0.1f - (float)rnd.NextDouble() * 0.9f), V(d, y - 0.1f - (float)rnd.NextDouble() * 0.9f), V(d, y - 0.04f), V(c, y - 0.04f), streak, away);
                    }
                // the wall's cap and a cast pilaster at the start of each panel
                Vector3 post = V(r0, t0);
                var rot = Quaternion.LookRotation(Flat(U.tan[i]));
                b[0].Box(post + Vector3.up * 0.04f, new Vector3(0.34f, 0.08f, 0.34f), rot, conc);
                b[0].Box(post + Vector3.up * 0.65f, new Vector3(0.18f, 1.3f, 0.18f), rot, green);
                b[0].Box(post + Vector3.up * 1.33f, new Vector3(0.27f, 0.07f, 0.27f), rot, green);
                b[0].Box(post + Vector3.up * 1.42f, new Vector3(0.12f, 0.12f, 0.12f), rot, green);
                Panel(p[0], V(r0, t0 + 0.06f), V(r1, t1 + 0.06f), 1.24f, Flat(s0));
                BlockBothWays(b[1], V(o0, f0), V(o1, f1), V(o1, t1 + 1.3f), V(o0, t0 + 1.3f), away);
                BlockBothWays(b[1], V(r0, t0), V(r1, t1), V(r1, t1 + 1.3f), V(r0, t0 + 1.3f), Flat(s0));
            }

            foreach (int si in stairs)
            {
                float top = U.y[si] + 0.14f, bottom = L.y[low[si]] + 0.1f, drop = top - bottom;
                if (drop < 0.4f) continue;
                int n = Mathf.Clamp(Mathf.RoundToInt(drop / 0.24f), 2, 10);
                float rise = drop / n, run = Mathf.Max(0.26f, (MallStrip + 0.35f) / n);
                Vector2 s = U.side[si];
                var rot = Quaternion.LookRotation(Flat(-s));
                var b = chunks.At(U.p[si]);
                for (int k = 0; k < n; k++)
                {
                    float tread = top - rise * (k + 1), foot = bottom - 0.3f;
                    Vector2 q = U.p[si] - s * (MallHalf + (k + 0.5f) * run);
                    var c = new Vector3(q.x, (tread + foot) * 0.5f, q.y);
                    var size = new Vector3(3.9f, tread - foot, run);
                    b[0].Box(c, size, rot, conc);
                    b[1].Box(c, size, rot, Vector2.zero);
                }
            }
            chunks.Flush();
            panels.Flush();
            moss?.Flush();
        }

        // ------------------------------------------------------------------ the split at Tallital
        /// <summary>Where Mall Road divides at Tallital (user's Street View): the Lake Bridge Tax booth at the tip between
        /// the two roads, a welcome banner across the road before it, and a zebra crossing.</summary>
        static void MallSplit(WorldCtx ctx, Transform parent, MallLine U, MallLine L, int[] low)
        {
            int tip = -1, south = U.t[0] < U.t[U.Count - 1] ? 1 : -1, start = south > 0 ? 0 : U.Count - 1;
            for (int k = 0; k < U.Count / 3; k++)
            {
                int i = start + south * k;
                if (Vector2.Distance(U.p[i], L.p[low[i]]) >= MallHalf * 2f + 1.5f) { tip = i; break; }
            }
            if (tip < 0) return;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var mb = new MeshBuilder();
            var col = new MeshBuilder();
            Vector2 white = Atlas.SolidUV(Pal.ShirtWhite), red = Atlas.SolidUV(Pal.TempleRed), yellow = Atlas.SolidUV(Pal.BoatYellow), blue = Atlas.SolidUV(Pal.BoatBlue), grey = Atlas.SolidUV(Pal.StoneGrey);
            Vector2 lowP = L.p[low[tip]];
            Vector2 boothP = (U.p[tip] + lowP) * 0.5f;
            Vector2 back = -U.tan[tip] * south;   // pointing back down the road, towards Tallital
            float y = (U.y[tip] + L.y[low[tip]]) * 0.5f;
            var rot = Quaternion.LookRotation(Flat(back));
            Vector3 c = V(boothP, y);
            mb.Box(c + Vector3.up * 0.1f, new Vector3(2.8f, 0.2f, 3.2f), rot, grey);
            mb.Box(c + Vector3.up * 1.3f, new Vector3(2.1f, 2.4f, 2.1f), rot, white);
            mb.Box(c + Vector3.up * 2.6f, new Vector3(2.6f, 0.18f, 2.6f), rot, red);
            Vector3 face = Flat(back);
            mb.Box(c + Vector3.up * 1.4f + face * 1.07f, new Vector3(1.3f, 0.9f, 0.04f), rot, Atlas.SolidUV(Pal.IronBlack));   // the window
            mb.Box(c + Vector3.up * 2.95f + face * 0.6f, new Vector3(2f, 0.5f, 0.06f), rot, blue);
            col.Box(c + Vector3.up * 1.3f, new Vector3(2.8f, 2.6f, 3.2f), rot, Vector2.zero);
            var holder = new GameObject("Lake Bridge Tax").transform;
            holder.SetParent(parent, false);
            Label(holder, font, "LAKE BRIDGE TAX", c + Vector3.up * 2.95f + face * 0.65f, face, 0.26f, Color.white);
            mb.Box(c + Vector3.up * 0.6f + face * 2.1f, new Vector3(0.9f, 1.2f, 0.06f), rot, yellow);   // the rate board

            // the welcome banner across the road, ~20 m before the split
            int g = Mathf.Clamp(tip - south * 10, 0, U.Count - 1);
            Vector2 gc = (U.p[g] + L.p[low[g]]) * 0.5f, gs = U.side[g];
            float span = Vector2.Distance(U.p[g], L.p[low[g]]) + MallHalf * 2f + 1.6f, gy = U.y[g];
            var grot = Quaternion.LookRotation(Flat(back));
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 pole = V(gc + gs * (s * span * 0.5f), gy);
                mb.Cylinder(pole, 0.14f, 0.12f, 7.2f, 8, grey, true);
                col.Box(pole + Vector3.up * 1.5f, new Vector3(0.3f, 3f, 0.3f), grot, Vector2.zero);
            }
            Vector3 bc = V(gc, gy) + Vector3.up * 6.3f;
            mb.Box(bc, new Vector3(span, 1.5f, 0.1f), grot, white);
            mb.Box(bc + Vector3.up * 0.82f, new Vector3(span, 0.12f, 0.12f), grot, blue);
            mb.Box(bc + Vector3.down * 0.82f, new Vector3(span, 0.12f, 0.12f), grot, blue);
            var banner = new GameObject("Mall Road banner").transform;
            banner.SetParent(parent, false);
            Label(banner, font, "MALL ROAD NAINITAL", bc + Vector3.up * 0.3f + face * 0.06f, face, 0.5f, new Color(0.75f, 0.1f, 0.1f));
            Label(banner, font, "aapka hardik swagat hai", bc + Vector3.down * 0.35f + face * 0.06f, face, 0.38f, new Color(0.1f, 0.15f, 0.55f));

            // zebra crossing across the joined road, a little before the tip
            int z = Mathf.Clamp(tip - south * 5, 0, U.Count - 1);
            Vector2 zc = (U.p[z] + L.p[low[z]]) * 0.5f, zs = U.side[z], zt = U.tan[z];
            float zw = Vector2.Distance(U.p[z], L.p[low[z]]) + MallHalf * 2f - 1f, zy = Mathf.Max(U.y[z], L.y[low[z]]) + 0.17f;
            var paint = new MeshBuilder();
            for (float a = -zw * 0.5f; a < zw * 0.5f - 0.3f; a += 1.1f)
            {
                Vector2 p0 = zc + zs * a - zt * 1.6f, p1 = zc + zs * (a + 0.55f) - zt * 1.6f, p2 = zc + zs * (a + 0.55f) + zt * 1.6f, p3 = zc + zs * a + zt * 1.6f;
                paint.Quad(V(p0, zy), V(p1, zy), V(p2, zy), V(p3, zy), white, Vector3.up);
            }
            ctx.MeshObject("Mall split paint", parent, paint, ctx.mats.atlas, null, false);
            ctx.MeshObject("Mall split (booth, banner)", parent, mb, ctx.mats.atlas, col);

            var m = new GameObject("MallShotSplit");   // the user's Street View pano at the split (29.38107, 79.46396), heading 16 deg
            m.transform.SetParent(parent, false);
            Vector2 cam = new Vector2(869.4f, -1045f);
            Vector3 eye = V(cam, ctx.H(cam) + 2.3f);
            Vector2 look = cam + new Vector2(Mathf.Sin(16f * Mathf.Deg2Rad), Mathf.Cos(16f * Mathf.Deg2Rad)) * 40f;
            m.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(V(look, ctx.H(look) + 2.6f) - eye));
        }

        // ------------------------------------------------------------------ the lake side
        static void MallLakeEdge(WorldCtx ctx, Transform parent, MallLine L, Material chain)
        {
            var solid = new MallChunks(ctx, parent, "Mall parapet", ctx.mats.atlas, true, 0.05f);
            var chainP = new MallChunks(ctx, parent, "Mall lane railing", chain, true, 0.16f);
            Rect plaster = Atlas.Facade(1, Atlas.StylePlain), stone = Atlas.Material(Atlas.MatDarkStone);
            Vector2 grime = Atlas.SolidUV(Pal.StoneGrey), green = Atlas.SolidUV(Pal.IronGreen), cap = Atlas.MaterialUV(Atlas.MatConcrete);
            Vector2 yellow = Atlas.SolidUV(Pal.BoatYellow), dark = Atlas.SolidUV(Pal.IronBlack);
            const float off = LowerHalf + 0.42f, pH = 0.55f;
            bool Here(Vector2 p) => ctx.LakeDistance(p) < 34f && !NearJetty(ctx, p, 5f);

            // a stained concrete parapet with green railing on top, hard against the road from end to end; painted on the bends
            for (int i = 0; i + 2 < L.Count; i++)
            {
                Vector2 a = L.p[i] + L.side[i] * off, c = L.p[i + 1] + L.side[i + 1] * off;
                if (!Here(a)) continue;
                var b = solid.At(a);
                float y0 = L.y[i], y1 = L.y[i + 1];
                Vector2 s0 = L.side[i], s1 = L.side[i + 1];
                Vector2 in0 = a - s0 * 0.2f, in1 = c - s1 * 0.2f, ou0 = a + s0 * 0.2f, ou1 = c + s1 * 0.2f;
                bool bend = Vector2.Dot(L.tan[Mathf.Max(i - 12, 0)], L.tan[Mathf.Min(i + 12, L.Count - 1)]) < 0.965f;
                Rect face = bend ? new Rect(i % 2 == 0 ? yellow : dark, Vector2.zero) : plaster;
                b[0].Quad(V(in0, y0), V(in1, y1), V(in1, y1 + pH), V(in0, y0 + pH), face, Flat(-s0));
                b[0].Quad(V(ou0, y0 - 1.5f), V(ou1, y1 - 1.5f), V(ou1, y1 + pH), V(ou0, y0 + pH), plaster, Flat(s0));
                b[0].Quad(V(in0, y0 + pH), V(ou0, y0 + pH), V(ou1, y1 + pH), V(in1, y1 + pH), cap, Vector3.up);
                if (!bend)
                {
                    Vector2 g0 = in0 - s0 * 0.012f, g1 = in1 - s1 * 0.012f;
                    b[0].Quad(V(g0, y0), V(g1, y1), V(g1, y1 + 0.2f), V(g0, y0 + 0.2f), grime, Flat(-s0));
                }
                if (i % 2 != 0) continue;
                Vector2 e = L.p[i + 2] + L.side[i + 2] * off;
                if (!Here(e)) continue;
                Vector3 a3 = V(a, y0 + pH), e3 = V(e, L.y[i + 2] + pH);
                var rot = Quaternion.LookRotation(Flat(L.tan[i]));
                b[0].Box(a3 + Vector3.up * 0.52f, new Vector3(0.1f, 1.04f, 0.1f), rot, green);
                b[0].Box(a3 + Vector3.up * 1.08f, new Vector3(0.16f, 0.07f, 0.16f), rot, green);
                Panel(chainP.At(a)[0], a3, e3, 1.02f, Flat(s0));
                BlockBothWays(b[1], V(a, y0), V(e, L.y[i + 2]), e3 + Vector3.up * 1.1f, a3 + Vector3.up * 1.1f, Flat(s0));
            }

            solid.Flush();
            chainP.Flush();
        }

        // ------------------------------------------------------------------ lamps and overhead wires
        static Vector3 MallTwinLamp(MeshBuilder mb, System.Random rnd, int ironPal = Pal.IronBlack)
        {
            Vector2 iron = Atlas.SolidUV(ironPal), bulb = Atlas.SolidUV(Pal.LampBulb);
            mb.Cylinder(Vector3.zero, 0.2f, 0.13f, 0.75f, 8, iron, true);
            mb.Cylinder(new Vector3(0, 0.75f, 0), 0.085f, 0.055f, 4.75f, 6, iron, true);
            mb.Sphere(new Vector3(0, 5.55f, 0), Vector3.one * 0.08f, 6, 3, iron);
            Vector3 glow = default;
            foreach (float s in new[] { -1f, 1f })
            {
                // a crook rising from the pole and curling over into a hanging lantern
                Vector3 last = new Vector3(0, 4.75f, 0);
                for (int k = 1; k <= 10; k++)
                {
                    float a = k / 10f * 200f * Mathf.Deg2Rad;
                    var q = new Vector3(s * (0.42f - Mathf.Cos(a) * 0.42f), 5.05f + Mathf.Sin(a) * 0.46f, 0);
                    mb.Limb(last, q, 0.024f, 0.022f, 3, iron, false);
                    last = q;
                }
                Vector3 curl = new Vector3(s * 0.05f, 4.6f, 0);
                for (int k = 1; k <= 6; k++)
                {
                    float a = k / 6f * 260f * Mathf.Deg2Rad, r = 0.16f * (1f - k / 9f);
                    var q = new Vector3(s * (0.2f - Mathf.Cos(a) * r), 4.6f + Mathf.Sin(a) * r, 0);
                    mb.Limb(curl, q, 0.014f, 0.012f, 3, iron, false);
                    curl = q;
                }
                Vector3 shade = last + Vector3.down * 0.34f;
                mb.Limb(last, shade + Vector3.up * 0.2f, 0.012f, 0.012f, 3, iron, false);
                mb.Cylinder(shade, 0.26f, 0.05f, 0.22f, 8, iron, true);
                glow = shade + Vector3.down * 0.05f;
                mb.Sphere(glow, new Vector3(0.1f, 0.11f, 0.1f), 6, 4, bulb);
            }
            if (rnd.NextDouble() < 0.45)   // a printed banner hung from the pole
            {
                int[] cols = { Pal.ShirtWhite, Pal.JacketRed, Pal.BoatBlue, Pal.BoatYellow };
                var uv = new Rect(Atlas.SolidUV(cols[rnd.Next(cols.Length)]), Vector2.zero);
                mb.Limb(new Vector3(0, 3.95f, 0), new Vector3(0.62f, 3.95f, 0), 0.014f, 0.014f, 3, iron, false);
                mb.QuadDouble(new Vector3(0.05f, 2.55f, 0), new Vector3(0.62f, 2.55f, 0), new Vector3(0.62f, 3.93f, 0), new Vector3(0.05f, 3.93f, 0), uv, Vector3.forward);
            }
            return glow;
        }

        static void SagWire(MeshBuilder mb, Vector3 a, Vector3 b, Vector2 uv)
        {
            float sag = Vector3.Distance(a, b) * 0.035f;
            Vector3 last = a;
            for (int s = 1; s <= 6; s++)
            {
                float t = s / 6f;
                Vector3 q = Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
                mb.Limb(last, q, 0.012f, 0.012f, 3, uv, false);
                last = q;
            }
        }

        static void MallLampsAndWires(WorldCtx ctx, Transform parent, MallLine U, MallLine L, List<int> stairs)
        {
            var lamps = new MallChunks(ctx, parent, "Mall lamps", ctx.mats.atlas, true, 0.08f);
            var rnd = new System.Random(515);

            void Lamp(Vector2 q, float y, Vector2 tan, int ironPal = Pal.IronBlack)
            {
                var basePos = V(q, y);
                var rot = Quaternion.LookRotation(Flat(new Vector2(-tan.y, tan.x)));   // arms reach along the road
                var b = lamps.At(q);
                Vector3 bulb;
                using (PropKit.At(b[0], basePos, rot)) bulb = MallTwinLamp(b[0], rnd, ironPal);
                ctx.lampHeads.Add(basePos + rot * bulb);
                b[1].Box(basePos + Vector3.up * 2.6f, new Vector3(0.3f, 5.2f, 0.3f), Vector2.zero);
            }

            for (int i = 8; i < U.Count - 8; i += 14)
            {
                int at = i;
                foreach (int s in stairs) if (Mathf.Abs(at - s) <= 2) at = s + 3;
                Lamp(U.p[at] - U.side[at] * (MallHalf + 0.72f), U.y[at] + 0.14f, U.tan[at], Pal.IronGreen);
            }
            for (int i = 14; i < L.Count - 14; i += 30)
                if (L.t[i] >= 0.45f && ctx.LakeDistance(L.p[i]) < 34f && !NearJetty(ctx, L.p[i], 6f))
                    Lamp(L.p[i] + L.side[i] * (LowerHalf + 0.42f), L.y[i] + 0.55f, L.tan[i]);
            lamps.Flush();

            var poles = new MeshBuilder();
            var wires = new MeshBuilder();
            var posters = new MeshBuilder();
            Vector2 wireUV = Atlas.SolidUV(Pal.IronBlack);
            int[] bills = { Pal.BoatYellow, Pal.ShirtWhite, Pal.JacketRed, Pal.Saffron };
            Vector3[] prev = null;
            for (int i = 18; i < U.Count - 8; i += 20)
            {
                Vector2 q = U.p[i] - U.side[i] * (MallHalf + 1.0f);
                var basePos = V(q, U.y[i] + 0.14f);
                var rot = Quaternion.LookRotation(Flat(U.tan[i])) * Quaternion.Euler(0, 90, 0);
                Vector3[] heads;
                using (PropKit.At(poles, basePos, rot)) heads = PropKit.ElectricPole(poles);
                var world = new Vector3[heads.Length];
                for (int k = 0; k < heads.Length; k++) world[k] = basePos + rot * heads[k];
                if (prev != null)
                    for (int k = 0; k < world.Length; k++)
                    {
                        SagWire(wires, prev[k], world[k], wireUV);
                        SagWire(wires, prev[k] + Vector3.down * 0.3f, world[k] + Vector3.down * 0.3f, wireUV);
                        SagWire(wires, prev[k] + Vector3.down * 0.55f, world[k] + Vector3.down * 0.55f, wireUV);
                    }
                prev = world;
                // bills pasted on the road side of the pole
                Vector3 face = Flat(U.side[i]);
                Vector3 along = Flat(U.tan[i]);
                for (int k = 0; k < 2; k++)
                {
                    float y = 1.3f + k * 0.75f;
                    var uv = new Rect(Atlas.SolidUV(bills[rnd.Next(bills.Length)]), Vector2.zero);
                    Vector3 c = basePos + face * 0.13f + Vector3.up * y;
                    posters.QuadDouble(c - along * 0.22f - Vector3.up * 0.33f, c + along * 0.22f - Vector3.up * 0.33f, c + along * 0.22f + Vector3.up * 0.33f, c - along * 0.22f + Vector3.up * 0.33f, uv, face);
                }
            }
            ctx.MeshObject("Mall poles", parent, poles, ctx.mats.atlas);
            ctx.MeshObject("Mall wires", parent, wires, ctx.mats.atlas, null, false);
            ctx.MeshObject("Mall pole bills", parent, posters, ctx.mats.atlas, null, false);
        }

        // ------------------------------------------------------------------ the hill side: paving at the shops, walls in the gaps
        // ------------------------------------------------------------------ the tall walls on Upper Mall's hill side
        /// <summary>Stretches of Upper Mall's hill side that are one tall retaining wall instead of shops (Street View 2022):
        /// t range, height, the face's distance from the centre line, painted or bare stone (process_data.py: UPPER_WALLS).</summary>
        static readonly (float t0, float t1, float h, float face, bool mural)[] UpperWalls =
        {
            (0.40f, 0.445f, 3.0f, 5.0f, false),   // rubble stone, cars parked nose-in along its foot
            (0.475f, 0.53f, 4.5f, 3.6f, true),    // painted panels, right at the road
        };

        /// <summary>Grey rubble stone with moss, from the CC0 photo (null when the photo is missing).</summary>
        static Material MossyStoneMaterial() => PhotoMaterial("old_stone_wall", "M_MallMossyStone", new Color(1.0f, 1.08f, 0.9f), 0.1f);

        /// <summary>A wall face between a and b (heights bottom/top at each end), seen from the side `toward` points to, textured in
        /// metres (`ua`/`ub`: distance along the wall at a and b) so the stones run on unbroken from one segment to the next.</summary>
        static void WallFace(MeshBuilder mb, Vector2 a, Vector2 b, float aBot, float bBot, float aTop, float bTop, float ua, float ub, Vector3 toward, float tile)
        {
            Vector2 right = new Vector2(-toward.z, toward.x);   // the viewer's right, looking at the face
            bool aLeft = Vector2.Dot(b - a, right) > 0f;
            Vector2 l = aLeft ? a : b, r = aLeft ? b : a;
            float lb = aLeft ? aBot : bBot, rb = aLeft ? bBot : aBot, lt = aLeft ? aTop : bTop, rt = aLeft ? bTop : aTop;
            float ul = aLeft ? ua : -ub, ur = aLeft ? ub : -ua;
            float bot = (lb + rb) * 0.5f, top = (lt + rt) * 0.5f;
            mb.Quad(V(l, lb), V(r, rb), V(r, rt), V(l, lt), new Rect(ul / tile, bot / tile, (ur - ul) / tile, (top - bot) / tile), toward);
        }

        static int UpperWallAt(float t)
        {
            for (int k = 0; k < UpperWalls.Length; k++) if (t >= UpperWalls[k].t0 && t <= UpperWalls[k].t1) return k;
            return -1;
        }

        /// <summary>The painted wall: panels of snow peaks, the lake with boats, sun and birds, flowers and Kumaoni aipan, in
        /// house-paint colours, weathered at the foot. Generic motifs only: no words, faces or logos. 16 m per repeat.</summary>
        static Material MuralMaterial()
        {
            const int W = 1024, H = 256;
            string texPath = Gen + "/T_MallMural.png", matPath = Gen + "/M_MallMural.mat";
            var px = new Color32[W * H];
            var rnd = new System.Random(808);
            Color[] grounds = { new Color(0.97f, 0.78f, 0.16f), new Color(0.38f, 0.66f, 0.9f), new Color(0.6f, 0.19f, 0.13f),
                                new Color(0.32f, 0.6f, 0.3f), new Color(0.93f, 0.52f, 0.62f), new Color(0.96f, 0.94f, 0.88f) };
            Color white = new Color(0.97f, 0.96f, 0.93f), ink = new Color(0.13f, 0.12f, 0.14f);
            int x0 = 0, panel = 0;
            while (x0 < W)
            {
                int pw = 110 + rnd.Next(150);
                if (W - x0 - pw < 90) pw = W - x0;
                int kind = panel % 5;
                Color g = grounds[rnd.Next(grounds.Length)];
                float phase = (float)rnd.NextDouble() * 6f;
                var spots = new List<Vector4>();   // boats / flowers: u, v, size, colour index
                for (int k = 0; k < 7; k++) spots.Add(new Vector4((float)rnd.NextDouble() * 0.8f + 0.1f, (float)rnd.NextDouble(), 0.6f + (float)rnd.NextDouble() * 0.6f, rnd.Next(4)));
                Color[] dots = { new Color(0.85f, 0.15f, 0.15f), new Color(0.98f, 0.85f, 0.2f), white, new Color(0.9f, 0.45f, 0.75f) };
                float aspect = pw / (float)H;
                for (int yy = 0; yy < H; yy++)
                    for (int xx = x0; xx < x0 + pw; xx++)
                    {
                        float u = (xx - x0) / (float)pw, v = yy / (H - 1f);
                        Color c = g;
                        switch (kind)
                        {
                            case 0:   // snow peaks over green hills
                            {
                                float ridge = 0.5f + 0.28f * Mathf.Abs(Mathf.Repeat(u * 2.3f + phase, 1f) - 0.5f) * 2f * (0.7f + 0.3f * Mathf.Sin(u * 9f + phase));
                                c = Color.Lerp(new Color(0.75f, 0.88f, 0.98f), new Color(0.42f, 0.66f, 0.92f), v);
                                if (v < ridge) c = v > ridge - 0.07f && v > 0.6f ? white : new Color(0.45f, 0.52f, 0.66f);
                                if (v < 0.36f + 0.05f * Mathf.Sin(u * 7f + phase)) c = new Color(0.25f, 0.55f, 0.27f);
                                if (v < 0.18f) c = new Color(0.18f, 0.42f, 0.2f);
                                break;
                            }
                            case 1:   // the lake, hills and boats
                            {
                                c = v > 0.62f ? new Color(0.8f, 0.9f, 0.97f) : v > 0.46f + 0.04f * Mathf.Sin(u * 11f + phase) ? new Color(0.28f, 0.52f, 0.3f) : new Color(0.2f, 0.42f, 0.66f);
                                if (v < 0.46f && Mathf.Sin(u * 60f + v * 140f) > 0.93f) c = new Color(0.55f, 0.72f, 0.88f);
                                foreach (var b in spots)
                                {
                                    float bv = 0.12f + b.y * 0.25f, du = (u - b.x) * aspect, dv = v - bv;
                                    if (Mathf.Abs(du) < 0.07f * b.z && dv > -0.03f * b.z && dv < 0f) c = dots[(int)b.w % 2];
                                    if (dv >= 0f && dv < 0.14f * b.z && Mathf.Abs(du) < (0.14f * b.z - dv) * 0.45f) c = white;
                                }
                                break;
                            }
                            case 2:   // aipan: white dots and lines on red ochre
                            {
                                c = new Color(0.6f, 0.19f, 0.13f);
                                int lx = xx - x0, ly = yy;
                                bool border = (lx > 8 && lx < pw - 8 && (Mathf.Abs(ly - 12) < 2 || Mathf.Abs(ly - (H - 13)) < 2)) || ((Mathf.Abs(lx - 12) < 2 || Mathf.Abs(lx - (pw - 13)) < 2) && ly > 8 && ly < H - 8);
                                bool dotRow = (ly == 20 || ly == H - 21 || lx == 20 || lx == pw - 21) && ((lx + ly) % 9 < 3);
                                float dx = (u - 0.5f) * aspect, dy = v - 0.5f, r = Mathf.Sqrt(dx * dx + dy * dy), th = Mathf.Atan2(dy, dx);
                                bool ring = Mathf.Abs(r - 0.32f) < 0.012f || Mathf.Abs(r - 0.22f) < 0.01f || r < 0.05f;
                                bool petals = r < 0.3f && r > 0.08f && Mathf.Abs(Mathf.Sin(th * 4f)) < 0.09f;
                                bool scallop = Mathf.Abs(r - (0.38f + 0.03f * Mathf.Cos(th * 16f))) < 0.01f;
                                if (border || dotRow || ring || petals || scallop) c = white;
                                break;
                            }
                            case 3:   // flowers on green
                            {
                                c = new Color(0.3f, 0.58f, 0.3f);
                                foreach (var f in spots)
                                {
                                    float fv = 0.35f + f.y * 0.45f, du = (u - f.x) * aspect, dv = v - fv;
                                    if (Mathf.Abs(du) < 0.008f && dv < 0f && dv > -fv + 0.05f) c = new Color(0.16f, 0.36f, 0.16f);
                                    float rr = Mathf.Sqrt(du * du + dv * dv);
                                    if (rr < 0.075f * f.z) c = dots[(int)f.w];
                                    if (rr < 0.025f * f.z) c = new Color(0.98f, 0.8f, 0.2f);
                                }
                                break;
                            }
                            default:  // sun and birds
                            {
                                c = Color.Lerp(new Color(0.98f, 0.82f, 0.25f), new Color(0.97f, 0.62f, 0.2f), 1f - v);
                                float dx = (u - 0.5f) * aspect, dy = v - 0.6f, r = Mathf.Sqrt(dx * dx + dy * dy), th = Mathf.Atan2(dy, dx);
                                if (r < 0.22f) c = new Color(0.93f, 0.38f, 0.12f);
                                else if (r < 0.36f && Mathf.Sin(th * 12f) > 0.3f) c = new Color(0.95f, 0.5f, 0.15f);
                                foreach (var b in spots)
                                {
                                    float bu = b.x, bv = 0.2f + b.y * 0.2f, du = (u - bu) * aspect, dv = v - bv;
                                    if (Mathf.Abs(dv - Mathf.Abs(du) * 0.6f) < 0.008f && Mathf.Abs(du) < 0.05f) c = ink;
                                }
                                break;
                            }
                        }
                        if (xx - x0 < 4) c = ink;   // the line between panels
                        // painted straight onto rubble stone: the courses and joints show through, the colours a little dulled
                        float stoneDev = TextureKit.PhotoDev("old_stone_wall", 256, xx, yy);
                        c = Color.Lerp(c, new Color(c.grayscale, c.grayscale, c.grayscale), 0.18f) * (1f + stoneDev * 1.6f);
                        if (stoneDev < -0.17f) c *= 0.72f;
                        // weathering: blotchy fade, damp foot, chipped paint
                        float n = Mathf.PerlinNoise(xx * 0.03f, yy * 0.05f);
                        c = Color.Lerp(c, new Color(0.62f, 0.6f, 0.56f), 0.12f + 0.18f * n);
                        if (v < 0.16f) c = Color.Lerp(c, new Color(0.25f, 0.27f, 0.22f), (0.16f - v) / 0.16f * 0.65f);
                        if (Mathf.PerlinNoise(xx * 0.31f, yy * 0.29f) > 0.82f) c = Color.Lerp(c, new Color(0.7f, 0.68f, 0.64f), 0.7f);
                        px[yy * W + xx] = c;
                    }
                x0 += pw;
                panel++;
            }
            var tex = SaveTexture(px, W, H, texPath, false, false);
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!m)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, matPath);
            }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.12f);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ the hill side of Upper Mall
        static void MallHillEdge(WorldCtx ctx, Transform parent, MallLine U, Material cobble)
        {
            var houses = new List<(Vector2 c, float w, float d)>();
            var h = ctx.world.houses;
            for (int i = 0; i + 8 < h.Length; i += 9)
            {
                var c = new Vector2(h[i], h[i + 1]);
                if (Geo.DistanceToPolyline(U.arr, c, false) < 20f) houses.Add((c, Mathf.Max(h[i + 2], h[i + 3]), Mathf.Min(h[i + 2], h[i + 3])));
            }
            var lanes = new List<(Vector2[] line, float half)>();
            foreach (var r in ctx.world.roads)
            {
                if (IsMallDeck(r.name)) continue;
                var line = Geo.ToPoints(r.pts);
                if (line.Length < 2) continue;
                foreach (var p in line)
                    if (Geo.DistanceToPolyline(U.arr, p, false) < 60f) { lanes.Add((line, r.width * 0.5f)); break; }
            }
            var named = new List<Vector2[]>();
            foreach (var b in ctx.world.osmBuildings)
            {
                var poly = Geo.ToPoints(b.pts);
                if (poly.Length >= 3 && Geo.DistanceToPolyline(U.arr, poly[0], false) < 45f) named.Add(poly);
            }

            bool Occupied(int i)
            {
                Vector2 q = U.p[i] + U.side[i] * (MallHalf + ShopSetback + 3f);
                foreach (var (c, w, d) in houses)
                    if (Mathf.Abs(Vector2.Dot(q - c, U.tan[i])) < w * 0.5f + 1.2f && Mathf.Abs(Vector2.Dot(q - c, U.side[i])) < d * 0.5f + 3f) return true;
                foreach (var poly in named) if (Geo.PointInPolygon(poly, q)) return true;
                return ctx.InKeepClear(q, 2f);
            }

            bool LaneAt(Vector2 q)
            {
                foreach (var (line, half) in lanes)
                    if (Geo.DistanceToPolyline(line, q, false) < half + 1.5f) return true;
                return false;
            }

            var pave = new MallChunks(ctx, parent, "Mall footway", cobble, false);
            var walls = new MallChunks(ctx, parent, "Mall hill wall", ctx.mats.atlas, true, 0.05f);
            var murals = new MallChunks(ctx, parent, "Mall painted wall", MuralMaterial(), true, 0.05f);
            var mossMat = MossyStoneMaterial();
            var moss = mossMat ? new MallChunks(ctx, parent, "Mall hill wall stone", mossMat, true, 0.05f) : null;
            var parking = new MallChunks(ctx, parent, "Mall wall parking", ConcreteMaterial(ctx), false);
            Rect plaster = Atlas.Facade(0, Atlas.StylePlain), stone = Atlas.Material(Atlas.MatStone);
            Vector2 grime = Atlas.SolidUV(Pal.StoneGrey), conc = Atlas.MaterialUV(Atlas.MatConcrete);
            float v = 0f;

            // one tall wall: a strip of paving in front, the face (rubble courses or painted panels), a damp foot, a cap
            void TallWall(int i, float seg, int wi)
            {
                var w = UpperWalls[wi];
                Vector2 s0 = U.side[i], s1 = U.side[i + 1];
                Vector2 a = U.p[i] + s0 * w.face, b = U.p[i + 1] + s1 * w.face;
                float y0 = U.y[i] + 0.16f, y1 = U.y[i + 1] + 0.16f;
                if (w.face - MallHalf > 0.3f)
                {
                    Vector2 e0 = U.p[i] + s0 * MallHalf, e1 = U.p[i + 1] + s1 * MallHalf;
                    parking.At(e0)[0].Quad(V(e0, y0 - 0.03f), V(a, y0 - 0.03f), V(b, y1 - 0.03f), V(e1, y1 - 0.03f), new Rect(0f, v / 6f, (w.face - MallHalf) / 6f, seg / 6f), Vector3.up);
                }
                float f0 = U.y[i] - 0.2f, f1 = U.y[i + 1] - 0.2f, top0 = U.y[i] + w.h, top1 = U.y[i + 1] + w.h;
                Vector3 toRoad = Flat(-s0);
                bool aLeft = Vector2.Dot(b - a, new Vector2(s0.y, -s0.x)) > 0f;   // seen from the road, looking at the hill
                Vector2 l = aLeft ? a : b, r = aLeft ? b : a;
                float lf = aLeft ? f0 : f1, rf = aLeft ? f1 : f0, lt = aLeft ? top0 : top1, rt = aLeft ? top1 : top0;
                var c = walls.At(a);
                if (w.mural)
                {
                    float u0 = aLeft ? v : -(v + seg);   // runs on unbroken from one segment to the next
                    murals.At(a)[0].Quad(V(l, lf), V(r, rf), V(r, rt), V(l, lt), new Rect(u0 / 16f, 0f, seg / 16f, 1f), toRoad);
                }
                else if (moss != null)
                    WallFace(moss.At(a)[0], a, b, f0, f1, top0, top1, v, v + seg, toRoad, 2.5f);
                else
                {
                    int rows = Mathf.CeilToInt(w.h + 0.2f);   // 1 m courses, two stones across a segment
                    for (int k = 0; k < rows; k++)
                        for (int m = 0; m < 2; m++)
                        {
                            float r0 = k / (float)rows, r1 = (k + 1f) / rows;
                            Vector2 p0 = Vector2.Lerp(l, r, m * 0.5f), p1 = Vector2.Lerp(l, r, (m + 1) * 0.5f);
                            float b0 = Mathf.Lerp(lf, rf, m * 0.5f), b1 = Mathf.Lerp(lf, rf, (m + 1) * 0.5f);
                            float t0 = Mathf.Lerp(lt, rt, m * 0.5f), t1 = Mathf.Lerp(lt, rt, (m + 1) * 0.5f);
                            c[0].Quad(V(p0, Mathf.Lerp(b0, t0, r0)), V(p1, Mathf.Lerp(b1, t1, r0)), V(p1, Mathf.Lerp(b1, t1, r1)), V(p0, Mathf.Lerp(b0, t0, r1)), stone, toRoad);
                        }
                }
                Vector2 g0 = a - s0 * 0.012f, g1 = b - s1 * 0.012f;
                c[0].Quad(V(g0, f0), V(g1, f1), V(g1, f1 + 0.55f), V(g0, f0 + 0.55f), grime, toRoad);
                Vector2 ra = a + s0 * 0.35f, rb = b + s1 * 0.35f;
                c[0].Quad(V(a, top0), V(b, top1), V(rb, top1), V(ra, top0), conc, Vector3.up);
                BlockBothWays(c[1], V(a, f0), V(b, f1), V(b, top1), V(a, top0), toRoad);
            }

            for (int i = 0; i + 1 < U.Count; i++)
            {
                float seg = Vector2.Distance(U.p[i], U.p[i + 1]);
                int wall = UpperWallAt(U.t[i]);
                if (wall >= 0)
                {
                    TallWall(i, seg, wall);
                    v += seg;
                    continue;
                }
                if (Occupied(i))
                {
                    Vector2 a0 = U.p[i] + U.side[i] * MallHalf, a1 = U.p[i + 1] + U.side[i + 1] * MallHalf;
                    Vector2 b0 = U.p[i] + U.side[i] * (MallHalf + ShopSetback), b1 = U.p[i + 1] + U.side[i + 1] * (MallHalf + ShopSetback);
                    float y0 = U.y[i] + 0.16f, y1 = U.y[i + 1] + 0.16f;
                    pave.At(a0)[0].Quad(V(a0, y0), V(b0, y0), V(b1, y1), V(a1, y1), new Rect(0f, v / 2.5f, ShopSetback / 2.5f, seg / 2.5f), Vector3.up);
                }
                else
                {
                    Vector2 a = U.p[i] + U.side[i] * (MallHalf + ShopSetback - 0.6f), b = U.p[i + 1] + U.side[i + 1] * (MallHalf + ShopSetback - 0.6f);
                    if (!LaneAt(a))
                    {
                        float top0 = U.y[i] + Mathf.Clamp(ctx.H(a + U.side[i] * 1.5f) - U.y[i] + 0.3f, 0.9f, 2.8f);
                        float top1 = U.y[i + 1] + Mathf.Clamp(ctx.H(b + U.side[i + 1] * 1.5f) - U.y[i + 1] + 0.3f, 0.9f, 2.8f);
                        float f0 = U.y[i] - 0.2f, f1 = U.y[i + 1] - 0.2f;
                        var c = walls.At(a);
                        Vector3 toRoad = Flat(-U.side[i]);
                        bool masonry = Mathf.PerlinNoise(i * 0.05f, 3.3f) > 0.36f;   // mostly rough stone, some of it whitewashed
                        if (masonry)
                        {
                            // tiled in 1 m courses so the stones keep a believable size
                            int rows = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(top0 - f0, top1 - f1)));
                            for (int r = 0; r < rows; r++)
                                for (int m = 0; m < 2; m++)
                                {
                                    float r0 = r / (float)rows, r1 = (r + 1f) / rows;
                                    Vector2 p0 = Vector2.Lerp(a, b, m * 0.5f), p1 = Vector2.Lerp(a, b, (m + 1) * 0.5f);
                                    float bot0 = Mathf.Lerp(f0, f1, m * 0.5f), bot1 = Mathf.Lerp(f0, f1, (m + 1) * 0.5f);
                                    float top_0 = Mathf.Lerp(top0, top1, m * 0.5f), top_1 = Mathf.Lerp(top0, top1, (m + 1) * 0.5f);
                                    c[0].Quad(V(p0, Mathf.Lerp(bot0, top_0, r0)), V(p1, Mathf.Lerp(bot1, top_1, r0)), V(p1, Mathf.Lerp(bot1, top_1, r1)), V(p0, Mathf.Lerp(bot0, top_0, r1)), stone, toRoad);
                                }
                        }
                        else
                        {
                            c[0].Quad(V(a, f0), V(b, f1), V(b, top1), V(a, top0), plaster, toRoad);
                            Vector2 g0 = a - U.side[i] * 0.012f, g1 = b - U.side[i + 1] * 0.012f;
                            c[0].Quad(V(g0, f0), V(g1, f1), V(g1, f1 + 0.7f), V(g0, f0 + 0.7f), grime, toRoad);
                        }
                        Vector2 r0b = a + U.side[i] * 0.3f, r1b = b + U.side[i + 1] * 0.3f;
                        c[0].Quad(V(a, top0), V(b, top1), V(r1b, top1), V(r0b, top0), conc, Vector3.up);
                        BlockBothWays(c[1], V(a, f0), V(b, f1), V(b, top1), V(a, top0), toRoad);
                    }
                }
                v += seg;
            }
            pave.Flush();
            walls.Flush();
            murals.Flush();
            moss?.Flush();
            parking.Flush();
        }

        // ------------------------------------------------------------------ what makes the street: plinths, goods, balconies
        static void MallShopDressing(WorldCtx ctx, Transform parent, MallLine U)
        {
            var chunks = new MallChunks(ctx, parent, "Mall shop dressing", ctx.mats.atlas, true, 0.05f);
            var rnd = new System.Random(4141);
            int[] awnings = { Pal.BoatBlue, Pal.BoatBlue, Pal.Awning, Pal.BoatYellow, Pal.Canopy, Pal.JacketRed, Pal.BoatGreen };
            int[] goods = { Pal.SareeOrange, Pal.JacketBlue, Pal.ShirtWhite, Pal.SareePink, Pal.JacketYellow, Pal.Jeans, Pal.TempleRed, Pal.Saffron, Pal.BoatGreen };
            Vector2 pole = Atlas.SolidUV(Pal.IronBlack), wood = Atlas.MaterialUV(Atlas.MatWood), tube = Atlas.SolidUV(Pal.LampBulb), conc = Atlas.MaterialUV(Atlas.MatConcrete), dark = Atlas.SolidUV(Pal.StoneGrey);
            int[] blades = { Pal.JacketRed, Pal.BoatYellow, Pal.BoatBlue, Pal.TempleRed };
            int done = 0;

            foreach (var (pos, n, width) in ctx.shopFronts)
            {
                var p2 = new Vector2(pos.x, pos.z);
                if (Geo.DistanceToPolyline(U.arr, p2, false) > 9f) continue;
                float ground = U.y[NearestIndex(U.p, p2)] + 0.16f;
                Vector3 nrm = n.normalized, along = Vector3.Cross(Vector3.up, nrm).normalized;
                Vector3 wall = new Vector3(pos.x, ground, pos.z) - nrm * 0.62f;
                float w = width * 0.98f;
                var rot = Quaternion.LookRotation(nrm);
                var cell = chunks.At(p2);
                var mb = cell[0];

                // two dark tiled steps along the whole front
                mb.Box(wall + nrm * 0.5f + Vector3.up * (PlinthH * 0.5f), new Vector3(w, PlinthH, 1f), rot, dark);
                mb.Box(wall + nrm * 1.2f + Vector3.up * (PlinthH * 0.25f), new Vector3(w, PlinthH * 0.5f, 0.4f), rot, conc);
                cell[1].Box(wall + nrm * 0.5f + Vector3.up * (PlinthH * 0.5f), new Vector3(w, PlinthH, 1f), rot, Vector2.zero);
                Vector3 floor = wall + Vector3.up * PlinthH;

                // sloping awning on two poles, with a valance along its front
                Vector3 topC = floor + Vector3.up * 2.2f, outC = floor + nrm * 1.65f + Vector3.up * 1.95f;
                Vector3 half = along * (w * 0.5f);
                var awn = new Rect(Atlas.SolidUV(awnings[rnd.Next(awnings.Length)]), Vector2.zero);
                mb.QuadDouble(topC - half, topC + half, outC + half, outC - half, awn, Vector3.up);
                mb.QuadDouble(outC - half + Vector3.down * 0.16f, outC + half + Vector3.down * 0.16f, outC + half, outC - half, awn, nrm);
                mb.Limb(outC - half * 0.95f, outC - half * 0.95f - Vector3.up * 2.3f, 0.028f, 0.028f, 4, pole, false);
                mb.Limb(outC + half * 0.95f, outC + half * 0.95f - Vector3.up * 2.3f, 0.028f, 0.028f, 4, pole, false);
                mb.Box(topC + nrm * 0.55f + Vector3.down * 0.13f, new Vector3(w * 0.8f, 0.05f, 0.08f), rot, tube);

                // shawls, caps and bags hanging from the awning's edge: narrow pieces with gaps, so the shop shows through
                int hang = Mathf.Clamp(Mathf.RoundToInt(w / 0.42f), 4, 16);
                for (int k = 0; k < hang; k++)
                {
                    if (rnd.NextDouble() < 0.3) continue;
                    float x = (k + 0.5f) / hang - 0.5f, cw = 0.06f + (float)rnd.NextDouble() * 0.08f, ch = 0.3f + (float)rnd.NextDouble() * 0.65f;
                    Vector3 c = outC - nrm * 0.16f + along * (x * w) + Vector3.down * 0.16f;
                    var uv = new Rect(Atlas.SolidUV(goods[rnd.Next(goods.Length)]), Vector2.zero);
                    mb.QuadDouble(c - along * cw + Vector3.down * ch, c + along * cw + Vector3.down * ch, c + along * cw, c - along * cw, uv, nrm);
                }

                // goods heaped on a table at the front of the plinth
                Vector3 table = floor + nrm * 0.62f;
                float tw = w * 0.72f;
                mb.Box(table + Vector3.up * 0.38f, new Vector3(tw, 0.76f, 0.8f), rot, wood);
                int piles = Mathf.Clamp(Mathf.RoundToInt(tw / 0.34f), 4, 16);
                for (int k = 0; k < piles; k++)
                {
                    float x = ((k + 0.5f) / piles - 0.5f) * tw, h = 0.08f + (float)rnd.NextDouble() * 0.26f;
                    Vector3 c = table + along * x + nrm * (float)((rnd.NextDouble() - 0.5) * 0.45) + Vector3.up * (0.76f + h * 0.5f);
                    mb.Box(c, new Vector3(0.3f, h, 0.28f), rot, Atlas.SolidUV(goods[rnd.Next(goods.Length)]));
                }
                cell[1].Box(table + Vector3.up * 0.5f, new Vector3(tw, 1f, 0.8f), rot, Vector2.zero);

                // a tall vertical sign fixed to the wall on some shops
                if (rnd.NextDouble() < 0.38)
                {
                    float sd = rnd.Next(2) == 0 ? 1f : -1f;
                    Vector3 at = wall + along * (w * 0.5f - 0.22f) * sd + nrm * 0.42f + Vector3.up * 4.4f;
                    mb.Box(at, new Vector3(0.1f, 2.2f, 0.66f), rot, Atlas.SolidUV(blades[rnd.Next(blades.Length)]));
                }
                done++;
            }

            // long balconies on the upper floors of the row
            var h3 = ctx.world.houses;
            Vector2[] railCols = { Atlas.SolidUV(Pal.ShirtWhite), Atlas.SolidUV(Pal.ShirtWhite), Atlas.MaterialUV(Atlas.MatWood), Atlas.SolidUV(Pal.IronGreen) };
            int balconies = 0;
            for (int i = 0; i + 8 < h3.Length; i += 9)
            {
                var c = new Vector2(h3[i], h3[i + 1]);
                int levels = (int)h3[i + 5];
                if (levels < 3 || Geo.DistanceToPolyline(U.arr, c, false) > 16f) continue;
                float w = h3[i + 2], d = h3[i + 3];
                var q = Quaternion.Euler(0, h3[i + 4], 0);
                Vector3 f = q * Vector3.forward, r = q * Vector3.right;
                float street = U.y[NearestIndex(U.p, c)] - 0.25f;
                Vector2 rail = railCols[rnd.Next(railCols.Length)];
                var mb = chunks.At(c)[0];
                Vector3 front = new Vector3(c.x, 0f, c.y) + f * (d * 0.5f);
                for (int k = 2; k < levels; k++)
                {
                    Vector3 mid = front + Vector3.up * (street + 3f * k) + f * 0.5f;
                    float bw = w * 0.94f;
                    mb.Box(mid, new Vector3(bw, 0.13f, 1f), q, conc);
                    mb.Box(mid + f * 0.46f + Vector3.up * 1.02f, new Vector3(bw, 0.06f, 0.06f), q, rail);
                    mb.Box(mid + f * 0.46f + Vector3.up * 0.2f, new Vector3(bw, 0.04f, 0.04f), q, rail);
                    int posts = Mathf.Max(2, Mathf.RoundToInt(bw / 0.7f));
                    for (int p = 0; p <= posts; p++)
                        mb.Box(mid + f * 0.46f + r * ((p / (float)posts - 0.5f) * bw) + Vector3.up * 0.55f, new Vector3(0.05f, 0.95f, 0.05f), q, rail);
                    foreach (float s in new[] { -1f, 1f })
                        mb.Box(mid + r * (s * bw * 0.5f) + Vector3.up * 0.55f, new Vector3(0.05f, 0.95f, 0.95f), q, rail);
                    balconies++;
                }
            }
            chunks.Flush();
            Debug.Log($"[Nainital] Mall Road: {done} shops dressed, {balconies} balconies");
        }
    }
}
