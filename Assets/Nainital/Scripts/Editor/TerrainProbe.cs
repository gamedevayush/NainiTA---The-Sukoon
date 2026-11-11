using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>Diagnostics for the terrain's texture weights (batch mode: -executeMethod
    /// Nainital.EditorTools.TerrainProbe.Run / .TestSplat / .ReadTest).</summary>
    public static class TerrainProbe
    {
        const string TestPath = "Assets/Nainital/Generated/_ProbeTerrain.asset";

        static string Means(TerrainData td)
        {
            int ar = td.alphamapResolution, n = td.alphamapLayers, cnt = 0;
            var sum = new double[n];
            for (int y = 0; y < ar; y += 64)
            {
                var row = td.GetAlphamaps(0, y, ar, 1);
                for (int x = 0; x < ar; x += 16) { for (int k = 0; k < n; k++) sum[k] += row[0, x, k]; cnt++; }
            }
            string m = "";
            for (int k = 0; k < n; k++) m += (sum[k] / Mathf.Max(1, cnt)).ToString("F3") + " ";
            return m.Trim();
        }

        /// <summary>What the built terrain's weights really are.</summary>
        public static void Run()
        {
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Nainital/Generated/NainitalTerrain.asset");
            if (!td) { Debug.Log("[Probe] no terrain data"); return; }
            Debug.Log($"[Probe] size {td.size}, heightmap {td.heightmapResolution}, alphamap {td.alphamapResolution}, layers {td.alphamapLayers}, alpha textures {td.alphamapTextureCount}");
            Debug.Log($"[Probe] built terrain mean weights: {Means(td)}");
        }

        static void Fill(TerrainData td, Color32[] px, int sw, int sh, int band)
        {
            int ar = td.alphamapResolution;
            for (int y0 = 0; y0 < ar; y0 += band)
            {
                int rows = Mathf.Min(band, ar - y0);
                var a = new float[rows, ar, 4];
                for (int yy = 0; yy < rows; yy++)
                    for (int x = 0; x < ar; x++)
                    {
                        var c = px[((y0 + yy) * sh / ar) * sw + x * sw / ar];
                        float s = Mathf.Max(1f, c.r + c.g + c.b + c.a);
                        a[yy, x, 0] = c.r / s; a[yy, x, 1] = c.g / s; a[yy, x, 2] = c.b / s; a[yy, x, 3] = c.a / s;
                    }
                td.SetAlphamaps(0, y0, a);
            }
        }

        /// <summary>Which way of writing the weights survives: bands on a loose TerrainData, or on one that is an asset.</summary>
        public static void TestSplat()
        {
            var layers = new TerrainLayer[4];
            string[] names = { "TL_Grass", "TL_ForestFloor", "TL_Rock", "TL_Dirt" };
            for (int i = 0; i < 4; i++) layers[i] = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Nainital/Generated/Materials/" + names[i] + ".terrainlayer");
            var splat = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            splat.LoadImage(File.ReadAllBytes("Assets/Nainital/Data/splat.png"));
            var px = splat.GetPixels32();
            int sw = splat.width, sh = splat.height;
            Debug.Log($"[Probe] splat.png {sw} x {sh}");

            // A: loose TerrainData, bands of 256 (what the last build did)
            var a = new TerrainData { heightmapResolution = 513 };
            a.size = new Vector3(6000f, 1400f, 6000f);
            a.terrainLayers = layers;
            a.alphamapResolution = 4096;
            Fill(a, px, sw, sh, 256);
            Debug.Log($"[Probe] A loose, bands: {Means(a)}");

            // A2: loose, all rows in one call at 2048 (what worked before)
            var a2 = new TerrainData { heightmapResolution = 513 };
            a2.size = new Vector3(6000f, 1400f, 6000f);
            a2.terrainLayers = layers;
            a2.alphamapResolution = 2048;
            Fill(a2, px, sw, sh, 2048);
            Debug.Log($"[Probe] A2 loose, one call at 2048: {Means(a2)}");

            // B: asset first, then layers, resolution, bands
            if (File.Exists(TestPath)) AssetDatabase.DeleteAsset(TestPath);
            var b = new TerrainData { heightmapResolution = 513 };
            b.size = new Vector3(6000f, 1400f, 6000f);
            AssetDatabase.CreateAsset(b, TestPath);
            b.terrainLayers = layers;
            b.alphamapResolution = 4096;
            Fill(b, px, sw, sh, 256);
            Debug.Log($"[Probe] B asset first, bands: {Means(b)}");
            EditorUtility.SetDirty(b);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Probe] B after SaveAssets: {Means(b)}, file {new FileInfo(TestPath).Length / 1048576} MB");
            Object.DestroyImmediate(splat);
        }

        /// <summary>Second run: is test B still there after a restart?</summary>
        public static void ReadTest()
        {
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(TestPath);
            Debug.Log(td ? $"[Probe] B reloaded: {Means(td)} (alphamap {td.alphamapResolution}, layers {td.alphamapLayers})" : "[Probe] no test asset");
            if (td) AssetDatabase.DeleteAsset(TestPath);
        }
    }
}
