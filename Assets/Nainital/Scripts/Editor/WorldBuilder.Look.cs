using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>The grime texture for Nainital/AtlasLit (user, 2026-11-11: "sirf color mat do... halka with texture"): R for
    /// walls - soft blotches, stains running down, fine grit; G for tops and ground - dust patches and grit. 0.8 is clean.
    /// Tileable, so it repeats over the town without seams.</summary>
    public static partial class WorldBuilder
    {
        static Texture2D GrimeTexture()
        {
            const int S = 512;
            var px = new Color32[S * S];
            float Tile(float x, float y, float f, float seed)
            {
                // a noise that wraps at S: blend the four shifted copies
                float fx = x / S, fy = y / S;
                float a = Mathf.PerlinNoise(x * f + seed, y * f + seed), b = Mathf.PerlinNoise((x - S) * f + seed, y * f + seed);
                float c = Mathf.PerlinNoise(x * f + seed, (y - S) * f + seed), d = Mathf.PerlinNoise((x - S) * f + seed, (y - S) * f + seed);
                return a * (1 - fx) * (1 - fy) + b * fx * (1 - fy) + c * (1 - fx) * fy + d * fx * fy;
            }
            var rnd = new System.Random(77);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float blot = Tile(x, y, 0.012f, 11.3f) * 0.6f + Tile(x, y, 0.035f, 47.1f) * 0.4f;
                    float streak = Tile(x * 6f, y * 0.25f, 0.02f, 3.7f);                         // tall thin runs (the shader stretches y too)
                    float grit = (float)rnd.NextDouble();
                    float wall = 0.85f - Mathf.Max(0f, blot - 0.42f) * 0.4f - Mathf.Max(0f, streak - 0.6f) * 0.3f + (grit - 0.5f) * 0.04f;
                    float dust = Tile(x, y, 0.018f, 91.2f);
                    float top = 0.86f - Mathf.Max(0f, dust - 0.4f) * 0.4f - Mathf.Max(0f, blot - 0.55f) * 0.2f + (grit - 0.5f) * 0.09f;
                    px[y * S + x] = new Color(Mathf.Clamp01(wall), Mathf.Clamp01(top), 0.8f, 1f);
                }
            return SaveTexture(px, S, S, Gen + "/T_Grime.png", false, false);
        }

        static void BuildSurfaceLook(WorldCtx ctx)
        {
            var go = new GameObject("Surface look (weathering)");
            go.transform.SetParent(ctx.worldRoot, false);
            var look = go.AddComponent<SurfaceLook>();
            look.grime = GrimeTexture();
            look.Apply();
            Debug.Log("[Nainital] surface look: grime " + (look.grime ? "on" : "missing") + ", atlas shader " + (ctx.mats.atlas ? ctx.mats.atlas.shader.name : "-"));
        }
    }
}
