using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>Road edges (user, 2026-11-11: "road ke side grass direct na ho, dono ke beech blend ho"). A hill road's
    /// tarmac does not stop in a ruled line: it crumbles out into grit and dust over the last metre. Beside every asphalt
    /// road a strip runs 1.3 m out over the ground, with a cut-out ragged edge, a few loose bits beyond it, and dust towards
    /// its end. Under it the terrain is baked the same way (process_data.py: grit, then earth, then grass or leaf litter).</summary>
    public static partial class WorldBuilder
    {
        const float FringeWidth = 1.1f;
        static Material roadEdgeMat;
        static Dictionary<Vector2Int, MeshBuilder> fringeChunks;

        /// <summary>Asphalt that frays out across u (0 at the road, 1 at the far edge) and repeats along v like the road.</summary>
        static Material RoadEdgeMaterial()
        {
            string src = "Assets/Nainital/Data/PBR/asphalt_02_albedo.png", texPath = Gen + "/T_AsphaltEdge.png", matPath = Gen + "/M_AsphaltEdge.mat";
            const int W = 192, H = 1024;
            var asphalt = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            bool ok = File.Exists(src) && asphalt.LoadImage(File.ReadAllBytes(src));
            Color32[] sp = ok ? asphalt.GetPixels32() : null;
            int sw = ok ? asphalt.width : 1, sh = ok ? asphalt.height : 1;
            var rnd = new System.Random(2611);
            // periodic noise along v so the edge tiles without a seam: a few whole-cycle waves with random phases
            int[] cyc = { 3, 5, 8, 13, 21, 34 };
            float[] amp = { 0.30f, 0.22f, 0.16f, 0.12f, 0.08f, 0.05f }, ph = new float[cyc.Length];
            for (int k = 0; k < cyc.Length; k++) ph[k] = (float)rnd.NextDouble() * Mathf.PI * 2f;
            const int G = 64;   // a wrapping lattice for the loose bits
            var lat = new float[G * G];
            for (int k = 0; k < lat.Length; k++) lat[k] = (float)rnd.NextDouble();
            float Lat(float x, float y)
            {
                int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
                float fx = x - x0, fy = y - y0;
                float a = lat[((y0 % G + G) % G) * G + (x0 % G + G) % G], b = lat[((y0 % G + G) % G) * G + ((x0 + 1) % G + G) % G];
                float c = lat[(((y0 + 1) % G + G) % G) * G + (x0 % G + G) % G], d = lat[(((y0 + 1) % G + G) % G) * G + ((x0 + 1) % G + G) % G];
                return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
            }
            var px = new Color32[W * H];
            var dust = new Color(0.50f, 0.47f, 0.42f);
            for (int y = 0; y < H; y++)
            {
                float t = y / (float)H * Mathf.PI * 2f, wave = 0f;
                for (int k = 0; k < cyc.Length; k++) wave += amp[k] * Mathf.Sin(cyc[k] * t + ph[k]);
                float edge = Mathf.Clamp(0.5f + wave * 0.55f, 0.18f, 0.88f);   // where the tarmac gives out
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)(W - 1);
                    float fine = Lat(x * 0.3125f, y * 0.3125f) - 0.5f;               // a ragged line, not a smooth wave (64 cells per 1024 rows: tiles)
                    float e = edge + fine * 0.12f;
                    float a = Mathf.Clamp01((e - u) * 18f + 0.5f);
                    float bits = Lat(x * 1.1f + 7f, y * 0.875f + 3f);                 // loose lumps of tarmac past the edge (tiles too)
                    if (u > e && u < e + 0.22f && bits > 0.8f) a = 1f;
                    Color c = ok ? (Color)sp[(y * sh / H) * sw + (x * sw / 1024) % sw] : new Color(0.3f, 0.3f, 0.3f);
                    // old, dusty tarmac and grit at the foot of the slab: paler and browner than the road above it
                    c = Color.Lerp(c, c * 0.72f + dust * 0.5f, 0.4f + 0.45f * Mathf.SmoothStep(0f, 1f, u / Mathf.Max(e, 0.2f)));
                    c.a = a;
                    px[y * W + x] = c;
                }
            }
            Object.DestroyImmediate(asphalt);
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = true;
            imp.mipMapsPreserveCoverage = true;
            imp.alphaTestReferenceValue = 0.5f;
            imp.wrapModeU = TextureWrapMode.Clamp;   // across: no bleed from the far edge into the road seam
            imp.wrapModeV = TextureWrapMode.Repeat;  // along: repeats with the road
            imp.anisoLevel = 8;
            imp.SaveAndReimport();
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, matPath); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.12f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static MeshBuilder FringeChunk(Vector2 p)
        {
            var key = new Vector2Int(Mathf.FloorToInt(p.x / 250f), Mathf.FloorToInt(p.y / 250f));
            if (!fringeChunks.TryGetValue(key, out var mb)) { mb = new MeshBuilder(); fringeChunks[key] = mb; }
            return mb;
        }

        /// <summary>The frayed strips either side of one road. path/edges are the road's own centre points and kerb
        /// vertices (left and right), vs its v coordinates.</summary>
        static void AddRoadFringe(WorldCtx ctx, List<Vector2> path, List<Vector3> lefts, List<Vector3> rights, List<float> vs)
        {
            if (fringeChunks == null) fringeChunks = new Dictionary<Vector2Int, MeshBuilder>();
            for (int side = 0; side < 2; side++)
            {
                var edge = side == 0 ? lefts : rights;
                Vector3 prevIn = default, prevOut = default;
                bool prevOk = false;
                for (int i = 0; i < path.Count; i++)
                {
                    Vector2 tan = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                    Vector2 nrm = new Vector2(-tan.y, tan.x) * (side == 0 ? 1f : -1f);   // outwards on this side
                    // the strip lies on the ground at the foot of the road's edge (the slab stands RoadLift above it);
                    // none where the ground falls or rises sharply (a bank or a wall)
                    Vector2 i2 = new Vector2(edge[i].x, edge[i].z) + nrm * 0.05f;
                    Vector3 inn = new Vector3(i2.x, ctx.H(i2) + 0.045f, i2.y);
                    Vector2 o = i2 + nrm * FringeWidth;
                    float g = ctx.H(o);
                    Vector3 outer = new Vector3(o.x, g + 0.04f, o.y);
                    bool ok = !ctx.InLake(o) && !NearJunction(ctx, path[i]) && inn.y > edge[i].y - 0.5f && outer.y > inn.y - 0.8f && outer.y < inn.y + 1.0f;
                    if (i > 0 && ok && prevOk)
                    {
                        var mb = FringeChunk(path[i]);
                        Vector2 u0i = new Vector2(0f, vs[i - 1]), u0o = new Vector2(1f, vs[i - 1]), u1i = new Vector2(0f, vs[i]), u1o = new Vector2(1f, vs[i]);
                        mb.Tri(prevIn, prevOut, outer, u0i, u0o, u1o, Vector3.up);
                        mb.Tri(prevIn, outer, inn, u0i, u1o, u1i, Vector3.up);
                    }
                    prevIn = inn; prevOut = outer; prevOk = ok;
                }
            }
        }

        static void EmitRoadFringes(WorldCtx ctx, Transform parent)
        {
            if (fringeChunks == null || fringeChunks.Count == 0) return;
            if (!roadEdgeMat) roadEdgeMat = RoadEdgeMaterial();
            int n = 0;
            foreach (var kv in fringeChunks)
            {
                ctx.MeshObject($"Road edges {kv.Key.x},{kv.Key.y}", parent, kv.Value, roadEdgeMat, null, false);
                n += kv.Value.VertexCount / 3;
            }
            Debug.Log($"[Nainital] road edges: {n} triangles in {fringeChunks.Count} chunks");
            fringeChunks = null;
        }
    }
}
