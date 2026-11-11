using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nainital.EditorTools
{
    /// <summary>The road west to Khurpatal (user, 2026-11-11): water in the small lakes (Sariyatal, Khurpatal), and the
    /// Woodland falls below Sariyatal - white water running down a rock face into a pool. The lookouts, the pony stand and
    /// the lakeside shops are in LandmarkBuilder.</summary>
    public static partial class WorldBuilder
    {
        static void BuildPonds(WorldCtx ctx)
        {
            if (ctx.world.ponds == null) return;
            var parent = new GameObject("Small lakes (Sariyatal, Khurpatal ...)").transform;
            parent.SetParent(ctx.worldRoot, false);
            int n = 0;
            foreach (var p in ctx.world.ponds)
            {
                if (p.level <= 0f) continue;
                var poly = Geo.ToPoints(p.pts);
                if (poly.Length < 3) continue;
                if (Geo.SignedArea(poly) < 0) System.Array.Reverse(poly);
                float y = p.level - ctx.world.meta.hMin;
                var water = Offset(poly, 1.5f);
                var tris = Geo.Triangulate(water);
                var wmb = new MeshBuilder();
                for (int i = 0; i + 2 < tris.Length; i += 3)
                {
                    Vector2 a = water[tris[i]], b = water[tris[i + 1]], c = water[tris[i + 2]];
                    wmb.Tri(new Vector3(a.x, y, a.y), new Vector3(b.x, y, b.y), new Vector3(c.x, y, c.y), Vector2.zero, Vector3.up);
                }
                var go = ctx.MeshObject(string.IsNullOrEmpty(p.name) ? "Pond" : p.name, parent, wmb, ctx.mats.water, null, false);
                go.layer = 4;
                n++;
            }
            Debug.Log($"[Nainital] small lakes with water: {n}");
        }

        /// <summary>Downward white streaks with soft edges, for the falling water (alpha-blended).</summary>
        static Material WaterfallMaterial()
        {
            string texPath = Gen + "/T_Waterfall.png", matPath = Gen + "/M_Waterfall.mat";
            const int W = 128, H = 256;
            var px = new Color32[W * H];
            var rnd = new System.Random(5150);
            var lane = new float[W];
            for (int x = 0; x < W; x++) lane[x] = 0.55f + 0.45f * Mathf.PerlinNoise(x * 0.11f, 3.1f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float streak = Mathf.PerlinNoise(x * 0.19f, y * 0.018f), foam = Mathf.PerlinNoise(x * 0.4f, y * 0.09f);
                    float a = Mathf.Clamp01(lane[x] * (0.45f + streak * 0.7f)) * (0.75f + 0.25f * foam);
                    float w = 0.82f + 0.18f * foam;
                    px[y * W + x] = new Color(w, w + 0.02f, w + 0.05f, a);
                }
            var tex = SaveTexture(px, W, H, texPath, false, false);
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, matPath); }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", new Color(0.92f, 0.95f, 1f, 0.85f));
            m.SetFloat("_Surface", 1f);   // transparent
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", (float)CullMode.Off);
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>The falls: from the Sariyatal Falls point the water runs down the steepest way, a sheet over a dark rock
        /// face, into a pool with boulders round it.</summary>
        static void BuildWaterfall(WorldCtx ctx)
        {
            var at = ctx.Poi("Sariyatal Falls");
            if (!at.HasValue) return;
            var parent = new GameObject("Sariyatal Falls").transform;
            parent.SetParent(ctx.worldRoot, false);
            // the steepest way down
            Vector2 c = at.Value, down = Vector2.down;
            float best = 0f;
            for (int k = 0; k < 24; k++)
            {
                Vector2 d = new Vector2(Mathf.Cos(k * Mathf.PI / 12f), Mathf.Sin(k * Mathf.PI / 12f));
                float drop = ctx.H(c) - ctx.H(c + d * 12f);
                if (drop > best) { best = drop; down = d; }
            }
            Vector2 side = new Vector2(-down.y, down.x);
            var water = new MeshBuilder();
            var rock = new MeshBuilder();
            const float width = 3.2f, len = 26f;
            int steps = 26;
            float v = 0f;
            for (int i = 0; i < steps; i++)
            {
                float t0 = i / (float)steps * len, t1 = (i + 1) / (float)steps * len;
                Vector2 a = c + down * t0, b = c + down * t1;
                float ya = ctx.H(a) + 0.25f, yb = ctx.H(b) + 0.25f;
                float wa = width * (0.8f + 0.25f * Mathf.Sin(i * 0.9f)), wb = width * (0.8f + 0.25f * Mathf.Sin((i + 1) * 0.9f));
                float seg = Vector3.Distance(V(a, ya), V(b, yb));
                water.Quad(V(a - side * wa * 0.5f, ya), V(a + side * wa * 0.5f, ya), V(b + side * wb * 0.5f, yb), V(b - side * wb * 0.5f, yb),
                           new Rect(0f, -(v + seg) / 6f, 1f, seg / 6f), Vector3.up);
                v += seg;
                // the wet rock either side
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector2 r0 = a + side * (s * (wa * 0.5f + 0.6f)), r1 = b + side * (s * (wb * 0.5f + 0.6f));
                    rock.Box(V((r0 + r1) * 0.5f, (ya + yb) * 0.5f - 0.2f), new Vector3(1.4f, 0.9f, seg + 0.4f), Quaternion.LookRotation(Flat(down)), Atlas.MaterialUV(Atlas.MatDarkStone));
                }
            }
            var wgo = ctx.MeshObject("Falling water", parent, water, WaterfallMaterial(), null, false);
            wgo.AddComponent<WaterfallFlow>().speed = 1.4f;
            ctx.MeshObject("Falls rock", parent, rock, ctx.mats.atlas, rock, true);
            // the pool at the foot, and boulders round it
            Vector2 foot = c + down * (len + 3f);
            float py = ctx.H(foot) + 0.2f;
            var pool = new MeshBuilder();
            for (int k = 0; k < 16; k++)
            {
                float a0 = k * Mathf.PI / 8f, a1 = (k + 1) * Mathf.PI / 8f;
                pool.Tri(V(foot, py), V(foot + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * 4.5f, py), V(foot + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * 4.5f, py), Vector2.zero, Vector3.up);
            }
            var pgo = ctx.MeshObject("Falls pool", parent, pool, ctx.mats.water, null, false);
            pgo.layer = 4;
            var stones = new MeshBuilder();
            var rnd = new System.Random(4422);
            for (int k = 0; k < 11; k++)
            {
                float a = k / 11f * Mathf.PI * 2f + (float)rnd.NextDouble() * 0.3f, r = 4.6f + (float)rnd.NextDouble() * 1.4f;
                Vector2 q = foot + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                float s = 0.5f + (float)rnd.NextDouble() * 0.7f;
                stones.Sphere(V(q, ctx.H(q) + s * 0.2f), new Vector3(s * 1.3f, s * 0.8f, s), 7, 4, Atlas.MaterialUV(Atlas.MatDarkStone));
            }
            ctx.MeshObject("Falls boulders", parent, stones, ctx.mats.atlas, stones, true);
            ctx.Zone("Sariyatal Falls", V(c, ctx.H(c)), 40f);
            var cam = new GameObject("ViewShot Sariyatal Falls");   // PreviewCapture: from the pool, looking up the falls
            cam.transform.SetParent(parent, false);
            Vector3 eye = V(foot + down * 9f + side * 3f, py + 1.8f);
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(V(c + down * (len * 0.4f), ctx.H(c + down * (len * 0.4f)) + 2f) - eye));
            Debug.Log($"[Nainital] Sariyatal Falls: {len} m of falling water, drop {ctx.H(c) - ctx.H(c + down * len):F1} m");
        }
    }
}
