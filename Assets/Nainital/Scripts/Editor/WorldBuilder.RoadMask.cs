using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// The roads are part of the ground (user, 2026-11-16: "saari jitni bhi roads hain sbko terrain me bake kro with strips,
    /// to dirt effects bhi theek dikhenge, koi road separate mesh na ho, merging and mixing bhi theek hogi, texturing me
    /// geela ya gadda wgera bhi easily hojayega"). Every carriageway, footpath, junction and paving that used to be a mesh is
    /// painted into a mask over the whole map instead: RGBA = asphalt, cement concrete, grey pavers, red pavers, 0.73 m a
    /// texel (8192 on PC, 4096 on phones), anti-aliased. The terrain shader (Shaders/NainitalTerrainRoads.hlsl) draws the
    /// surfaces from it, frays the edges, and lays the dirt and the wet patches over roads and ground alike. Trodden earth
    /// paths go into the terrain's own dirt layer. The ground under each road is set to the road's surface (Carve.cs), so
    /// the terrain collider is the road. Only structures stay meshes: the Lower Mall deck, walls, railings, steps.
    /// </summary>
    public static partial class WorldBuilder
    {
        public const int RoadMaskN = 8192;
        public const int ChAsphalt = 0, ChConcrete = 1, ChGreyPaver = 2, ChRedPaver = 3;
        static byte[] roadMask;                     // RGBA32, RoadMaskN x RoadMaskN, row 0 = south
        static Vector2 roadMaskOrigin;
        static float roadMaskSize, roadMaskInvTexel;
        static readonly List<Vector3[]> dirtTris = new List<Vector3[]>();
        static int paintedTris;

        static void BeginRoadMask(WorldCtx ctx)
        {
            var t = ctx.terrain;
            roadMaskOrigin = new Vector2(t.transform.position.x, t.transform.position.z);
            roadMaskSize = t.terrainData.size.x;
            roadMaskInvTexel = RoadMaskN / roadMaskSize;
            roadMask = new byte[RoadMaskN * RoadMaskN * 4];
            dirtTris.Clear();
            paintedTris = 0;
        }

        /// <summary>Paints the up-facing triangles of mb into channel ch (2 x 2 samples a texel).</summary>
        internal static void PaintRoadSurface(MeshBuilder mb, int ch, float strength = 1f, int firstIndex = 0, int endIndex = -1)
        {
            if (roadMask == null || mb == null) return;
            var V = mb.V; var T = mb.T;
            int end = endIndex < 0 ? T.Count : Mathf.Min(endIndex, T.Count);
            for (int i = firstIndex; i + 2 < end; i += 3)
            {
                Vector3 a = V[T[i]], b = V[T[i + 1]], c = V[T[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-10f || Mathf.Abs(n.normalized.y) < 0.5f) continue;   // kerb faces and walls
                PaintTri(new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c.x, c.z), ch, strength);
            }
        }

        static void PaintTri(Vector2 a, Vector2 b, Vector2 c, int ch, float strength)
        {
            // to texel space
            a = (a - roadMaskOrigin) * roadMaskInvTexel; b = (b - roadMaskOrigin) * roadMaskInvTexel; c = (c - roadMaskOrigin) * roadMaskInvTexel;
            float area = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            if (Mathf.Abs(area) < 1e-6f) return;
            if (area < 0f) { var tmp = b; b = c; c = tmp; }
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)))), x1 = Mathf.Min(RoadMaskN - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)))), y1 = Mathf.Min(RoadMaskN - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
            if (x1 < x0 || y1 < y0) return;
            static float E(Vector2 p, Vector2 q, float x, float y) => (q.x - p.x) * (y - p.y) - (q.y - p.y) * (x - p.x);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    // 4 x 4 samples a texel (2 x 2 gave coverage in quarters only: the edges waved by up to 18 cm)
                    int hits = 0;
                    for (int s = 0; s < 16; s++)
                    {
                        float sx = x + 0.125f + 0.25f * (s & 3), sy = y + 0.125f + 0.25f * (s >> 2);
                        if (E(a, b, sx, sy) >= 0f && E(b, c, sx, sy) >= 0f && E(c, a, sx, sy) >= 0f) hits++;
                    }
                    if (hits == 0) continue;
                    int k = (y * RoadMaskN + x) * 4 + ch;
                    byte v = (byte)Mathf.Min(255, Mathf.RoundToInt(hits / 16f * strength * 255f));
                    // a triangle edge shared by two triangles is counted in both: coverage adds up to whole
                    roadMask[k] = (byte)Mathf.Min(255, roadMask[k] + v);
                }
            paintedTris++;
        }

        /// <summary>Trodden earth paths: into the terrain's dirt layer at the end of the build.</summary>
        internal static void PaintDirtPath(MeshBuilder mb)
        {
            var V = mb.V; var T = mb.T;
            for (int i = 0; i + 2 < T.Count; i += 3)
            {
                Vector3 a = V[T[i]], b = V[T[i + 1]], c = V[T[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-10f || Mathf.Abs(n.normalized.y) < 0.5f) continue;
                dirtTris.Add(new[] { a, b, c });
            }
        }

        static void PaintDirtIntoSplat(WorldCtx ctx)
        {
            if (dirtTris.Count == 0) return;
            var td = ctx.terrain.terrainData;
            int ar = td.alphamapResolution, layers = td.alphamapLayers;
            if (layers < 4) return;
            Vector3 tp = ctx.terrain.transform.position, sz = td.size;
            float sx = (ar - 1) / sz.x, sz2 = (ar - 1) / sz.z;
            int painted = 0;
            foreach (var t in dirtTris)
            {
                Vector2 a = new Vector2((t[0].x - tp.x) * sx, (t[0].z - tp.z) * sz2), b = new Vector2((t[1].x - tp.x) * sx, (t[1].z - tp.z) * sz2), c = new Vector2((t[2].x - tp.x) * sx, (t[2].z - tp.z) * sz2);
                int x0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))) - 1, 0, ar - 1), x1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))) + 1, 0, ar - 1);
                int y0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))) - 1, 0, ar - 1), y1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))) + 1, 0, ar - 1);
                int w = x1 - x0 + 1, h = y1 - y0 + 1;
                var al = td.GetAlphamaps(x0, y0, w, h);
                for (int yy = 0; yy < h; yy++)
                    for (int xx = 0; xx < w; xx++)
                    {
                        Vector2 p = new Vector2(x0 + xx, y0 + yy);
                        float d = DistToTri(p, a, b, c);
                        float cov = Mathf.Clamp01(1.2f - d);   // in the triangle: 1, fading over a texel
                        if (cov <= al[yy, xx, 3]) continue;
                        float rest = 1f - cov, sum = al[yy, xx, 0] + al[yy, xx, 1] + al[yy, xx, 2];
                        for (int l = 0; l < 3; l++) al[yy, xx, l] = sum > 1e-4f ? al[yy, xx, l] / sum * rest : (l == 0 ? rest : 0f);
                        al[yy, xx, 3] = cov;
                        painted++;
                    }
                td.SetAlphamaps(x0, y0, al);
            }
            Debug.Log($"[Nainital] dirt paths into the terrain: {dirtTris.Count} triangles, {painted} texels");
            dirtTris.Clear();
        }

        /// <summary>How far either side of an edge the mask keeps the distance (m); the shader's RoadSdfRange.</summary>
        public const float RoadSdfRange = 4f;
        /// <summary>The plain coverage mask for the Unreal port (outside Assets, so Unity does not import a second 8192).</summary>
        public static string RoadCoveragePath => Path.GetFullPath("Generated/RoadCoverage.png");

        /// <summary>
        /// The road edges straight and parallel to the road (user, iPhone bugs 2026-11-16: "road terrain texturing is not
        /// precise, its dotted and random nature, make it perfect parallel to road"; "green terrain showing up on road").
        /// A coverage mask, filtered, has a soft rim one texel wide; on the phones (4096, 1.46 m a texel, ASTC blocks) that rim
        /// went 1.5-3 m wide and stepped, and the frayed-edge noise ate dots into it. Each channel is turned into the signed
        /// distance to its edge instead (+ inside, - outside, metres, +-range over 0..255): a distance interpolates linearly
        /// between texels, so the shader can cut the edge sharp at 0 anywhere between them, at any texture size.
        /// Seeds are the texels on the edge (partly covered, or full next to empty), each with its own sub-texel distance from
        /// its coverage; every other texel takes the nearest seed (two sweeps, nearest-seed propagation) plus the way to it.
        /// </summary>
        static void CoverageToDistance(byte[] m, int N, float texel, float range)
        {
            var cov = new byte[N * N];
            var near = new int[N * N];   // the nearest seed packed as x | y << 16, -1 none
            int pad = Mathf.CeilToInt(range / texel) + 2;
            for (int ch = 0; ch < 4; ch++)
            {
                int x0 = N, y0 = N, x1 = -1, y1 = -1;
                for (int y = 0; y < N; y++)
                {
                    int row = y * N;
                    for (int x = 0; x < N; x++)
                    {
                        byte c = m[(row + x) * 4 + ch];
                        cov[row + x] = c;
                        if (c == 0) continue;
                        if (x < x0) x0 = x; if (x > x1) x1 = x;
                        if (y < y0) y0 = y; if (y > y1) y1 = y;
                    }
                }
                if (x1 < 0) continue;   // nothing in this channel: 0 = far outside
                x0 = Mathf.Max(0, x0 - pad); y0 = Mathf.Max(0, y0 - pad); x1 = Mathf.Min(N - 1, x1 + pad); y1 = Mathf.Min(N - 1, y1 + pad);

                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        int k = y * N + x;
                        byte c = cov[k];
                        bool seed = c > 0 && c < 255;
                        if (!seed)
                        {
                            bool inside = c >= 128;
                            seed = (x > 0 && (cov[k - 1] >= 128) != inside) || (x < N - 1 && (cov[k + 1] >= 128) != inside)
                                || (y > 0 && (cov[k - N] >= 128) != inside) || (y < N - 1 && (cov[k + N] >= 128) != inside);
                        }
                        near[k] = seed ? (x | (y << 16)) : -1;
                    }

                void Try(int k, int x, int y, int q)
                {
                    int s = near[q];
                    if (s < 0) return;
                    int dx = x - (s & 0xffff), dy = y - (s >> 16);
                    int cur = near[k];
                    if (cur >= 0)
                    {
                        int cx = x - (cur & 0xffff), cy = y - (cur >> 16);
                        if (cx * cx + cy * cy <= dx * dx + dy * dy) return;
                    }
                    near[k] = s;
                }
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        int k = y * N + x;
                        if (x > x0) Try(k, x, y, k - 1);
                        if (y > y0)
                        {
                            Try(k, x, y, k - N);
                            if (x > x0) Try(k, x, y, k - N - 1);
                            if (x < x1) Try(k, x, y, k - N + 1);
                        }
                    }
                    for (int x = x1 - 1; x >= x0; x--) { int k = y * N + x; Try(k, x, y, k + 1); }
                }
                for (int y = y1; y >= y0; y--)
                {
                    for (int x = x1; x >= x0; x--)
                    {
                        int k = y * N + x;
                        if (x < x1) Try(k, x, y, k + 1);
                        if (y < y1)
                        {
                            Try(k, x, y, k + N);
                            if (x < x1) Try(k, x, y, k + N + 1);
                            if (x > x0) Try(k, x, y, k + N - 1);
                        }
                    }
                    for (int x = x0 + 1; x <= x1; x++) { int k = y * N + x; Try(k, x, y, k - 1); }
                }

                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        int k = y * N + x;
                        bool inside = cov[k] >= 128;
                        float d;
                        int s = near[k];
                        if (s < 0) d = inside ? range : -range;
                        else
                        {
                            int sx = s & 0xffff, sy = s >> 16;
                            float way = Mathf.Sqrt((x - sx) * (x - sx) + (y - sy) * (y - sy)) * texel;
                            float own = SeedDistance(cov, N, sx, sy) * texel;   // the seed's own distance from the edge
                            d = inside ? own + way : own - way;
                        }
                        m[k * 4 + ch] = (byte)Mathf.Clamp(Mathf.RoundToInt(127.5f + d / range * 127.5f), 0, 255);
                    }
            }
        }

        /// <summary>
        /// A seed texel's signed distance from the edge through it, in texels (+ inside), from its coverage and the edge's
        /// direction (the coverage gradient over its 3 x 3 neighbours). Coverage alone (cov - 0.5) is right only for an edge
        /// along the texel grid: on a slanting edge it was off by up to a third of a texel, varying texel to texel, and the
        /// edges of slanting roads came out wavy (user, 2026-11-17: "thoda straight perfect do"). This is the exact distance
        /// for a straight edge crossing the texel (S. Gustavson, "Anti-aliased Euclidean distance transform", 2011).
        /// </summary>
        static float SeedDistance(byte[] cov, int N, int x, int y)
        {
            float a = cov[y * N + x] / 255f;
            float C(int dx, int dy)
            {
                int xx = Mathf.Clamp(x + dx, 0, N - 1), yy = Mathf.Clamp(y + dy, 0, N - 1);
                return cov[yy * N + xx] / 255f;
            }
            float gx = (C(1, -1) + 2f * C(1, 0) + C(1, 1)) - (C(-1, -1) + 2f * C(-1, 0) + C(-1, 1));
            float gy = (C(-1, 1) + 2f * C(0, 1) + C(1, 1)) - (C(-1, -1) + 2f * C(0, -1) + C(1, -1));
            float df;   // Gustavson's edgedf: from the texel centre to the edge, + when the centre lies outside (a < 0.5)
            float gl = Mathf.Sqrt(gx * gx + gy * gy);
            if (gl < 1e-4f || a <= 0f || a >= 1f) df = 0.5f - a;
            else
            {
                gx = Mathf.Abs(gx / gl); gy = Mathf.Abs(gy / gl);
                if (gx < gy) { float tmp = gx; gx = gy; gy = tmp; }
                if (gy < 1e-4f) df = 0.5f - a;
                else
                {
                    float a1 = 0.5f * gy / gx;
                    if (a < a1) df = 0.5f * (gx + gy) - Mathf.Sqrt(2f * gx * gy * a);
                    else if (a < 1f - a1) df = (0.5f - a) * gx;
                    else df = -0.5f * (gx + gy) + Mathf.Sqrt(2f * gx * gy * (1f - a));
                }
            }
            return -df;
        }

        static float DistToTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float area = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            float s = Mathf.Sign(area);
            float e0 = ((b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x)) * s, e1 = ((c.x - b.x) * (p.y - b.y) - (c.y - b.y) * (p.x - b.x)) * s, e2 = ((a.x - c.x) * (p.y - c.y) - (a.y - c.y) * (p.x - c.x)) * s;
            if (e0 >= 0f && e1 >= 0f && e2 >= 0f) return 0f;
            float Seg(Vector2 q0, Vector2 q1) { Vector2 d = q1 - q0; float t = Mathf.Clamp01(Vector2.Dot(p - q0, d) / Mathf.Max(d.sqrMagnitude, 1e-6f)); return Vector2.Distance(p, q0 + d * t); }
            return Mathf.Min(Seg(a, b), Mathf.Min(Seg(b, c), Seg(c, a)));
        }

        /// <summary>Writes the mask and the ground textures, and hands them to the terrain (TerrainRoads).</summary>
        static void FinishRoadMask(WorldCtx ctx)
        {
            if (roadMask == null) return;
            PaintDirtIntoSplat(ctx);
            string dir = Gen + "/Textures";
            Directory.CreateDirectory(dir);
            int covered = 0;
            for (int i = 0; i < roadMask.Length; i += 4) if (roadMask[i] + roadMask[i + 1] + roadMask[i + 2] + roadMask[i + 3] > 0) covered++;
            var tex = new Texture2D(RoadMaskN, RoadMaskN, TextureFormat.RGBA32, false, true);
            // the plain coverage, outside Assets (not imported): the Unreal port reads it (UnrealExport)
            tex.SetPixelData(roadMask, 0);
            tex.Apply(false);
            Directory.CreateDirectory(Path.GetDirectoryName(RoadCoveragePath));
            File.WriteAllBytes(RoadCoveragePath, tex.EncodeToPNG());
            // the game's mask: each channel a signed distance to its edge (see CoverageToDistance)
            var sw = System.Diagnostics.Stopwatch.StartNew();
            CoverageToDistance(roadMask, RoadMaskN, roadMaskSize / RoadMaskN, RoadSdfRange);
            Debug.Log($"[Nainital] road mask as distances to the edge: {sw.Elapsed.TotalSeconds:F1} s");
            tex.SetPixelData(roadMask, 0);
            tex.Apply(false);
            string maskPath = dir + "/T_RoadMask.png";
            File.WriteAllBytes(maskPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            roadMask = null;
            AssetDatabase.ImportAsset(maskPath, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(maskPath);
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = false;
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = false;
            imp.mipmapEnabled = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.maxTextureSize = RoadMaskN;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.ignoreMipmapLimit = true;   // a lower texture quality setting must not halve the roads' edges
            imp.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Standalone", overridden = true, maxTextureSize = RoadMaskN, format = TextureImporterFormat.BC7 });
            // phones: half the size, but the distances keep the edge straight between texels; ASTC 4x4 (not 6x6, whose
            // blocks showed as steps along the edges on the iPhone)
            foreach (var phone in new[] { "iPhone", "Android" })
                imp.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = phone, overridden = true, maxTextureSize = 4096, format = TextureImporterFormat.ASTC_4x4 });
            imp.SaveAndReimport();
            var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);

            // the surfaces, with no joints or lines in them (planar over the ground, any direction)
            var asphalt = GroundTexture(dir + "/T_GroundAsphalt.png", "Assets/Nainital/Data/PBR/asphalt_02_albedo.png", (c, x, y) => new Color(c.r * 1.5f, c.g * 1.45f, c.b * 1.36f));
            var concrete = GroundTexture(dir + "/T_GroundConcrete.png", "Assets/Nainital/Data/PBR/concrete_floor_worn_001_albedo.png", (c, x, y) => { float g = 0.36f + 0.55f * c.grayscale; return new Color(g * 1.01f, g, g * 0.97f); });
            var paver = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Nainital/Data/PBR/rectangular_paving_albedo.png");
            var asphaltN = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Nainital/Data/PBR/asphalt_02_normal.png");
            var paverN = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Nainital/Data/PBR/rectangular_paving_normal.png");
            foreach (var np in new[] { "Assets/Nainital/Data/PBR/asphalt_02_normal.png", "Assets/Nainital/Data/PBR/rectangular_paving_normal.png" })
                if (AssetImporter.GetAtPath(np) is TextureImporter ni && ni.textureType != TextureImporterType.NormalMap) { ni.textureType = TextureImporterType.NormalMap; ni.SaveAndReimport(); }
            var dirt = DirtNoise(dir + "/T_GroundDirt.png");

            var tr = ctx.terrain.GetComponent<TerrainRoads>() ?? ctx.terrain.gameObject.AddComponent<TerrainRoads>();
            tr.mask = mask; tr.asphalt = asphalt; tr.asphaltNormal = asphaltN; tr.concrete = concrete; tr.paver = paver; tr.paverNormal = paverN; tr.dirt = dirt;
            tr.rect = new Vector4(roadMaskOrigin.x, roadMaskOrigin.y, roadMaskSize, roadMaskSize);
            tr.sdfRange = RoadSdfRange;
            tr.Apply();
            EditorUtility.SetDirty(tr);
            Debug.Log($"[Nainital] roads in the terrain: {paintedTris} triangles painted, {covered} of {RoadMaskN * RoadMaskN} texels ({covered * (roadMaskSize / RoadMaskN) * (roadMaskSize / RoadMaskN) / 1e6f:F2} km2)");
        }

        static Texture2D GroundTexture(string path, string src, System.Func<Color, int, int, Color> f)
        {
            if (!File.Exists(src)) return null;
            var img = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            img.LoadImage(File.ReadAllBytes(src));
            var px = img.GetPixels();
            int w = img.width;
            for (int i = 0; i < px.Length; i++) { var c = f(px[i], i % w, i / w); c.a = 1f; px[i] = c; }
            img.SetPixels(px);
            img.Apply(false);
            File.WriteAllBytes(path, img.EncodeToPNG());
            Object.DestroyImmediate(img);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.sRGBTexture = true; imp.mipmapEnabled = true; imp.wrapMode = TextureWrapMode.Repeat; imp.filterMode = FilterMode.Trilinear; imp.anisoLevel = 4; imp.maxTextureSize = 1024;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Tileable noise: r large blotches, g fine grime, b damp patches (value noise on a wrapping lattice).</summary>
        static Texture2D DirtNoise(string path)
        {
            const int S = 512;
            float Noise(float x, float y, int cells, int seed)
            {
                float fx = x * cells, fy = y * cells;
                int ix = Mathf.FloorToInt(fx), iy = Mathf.FloorToInt(fy);
                float tx = fx - ix, ty = fy - iy;
                tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
                float H(int a, int b) { a = ((a % cells) + cells) % cells; b = ((b % cells) + cells) % cells; uint h = (uint)(a * 73856093 ^ b * 19349663 ^ seed * 83492791); h ^= h >> 13; h *= 1274126177u; h ^= h >> 16; return (h & 0xffff) / 65535f; }
                return Mathf.Lerp(Mathf.Lerp(H(ix, iy), H(ix + 1, iy), tx), Mathf.Lerp(H(ix, iy + 1), H(ix + 1, iy + 1), tx), ty);
            }
            float Fbm(float x, float y, int cells, int seed, int oct) { float s = 0f, a = 0.5f, t = 0f; for (int o = 0; o < oct; o++) { s += Noise(x, y, cells << o, seed + o) * a; t += a; a *= 0.5f; } return s / t; }
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    px[y * S + x] = new Color(Fbm(u, v, 4, 11, 4), Fbm(u, v, 16, 23, 3), Fbm(u, v, 6, 37, 4), 1f);
                }
            var t2 = new Texture2D(S, S, TextureFormat.RGBA32, false, true);
            t2.SetPixels32(px);
            t2.Apply(false);
            File.WriteAllBytes(path, t2.EncodeToPNG());
            Object.DestroyImmediate(t2);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.sRGBTexture = false; imp.mipmapEnabled = true; imp.wrapMode = TextureWrapMode.Repeat; imp.filterMode = FilterMode.Bilinear;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
