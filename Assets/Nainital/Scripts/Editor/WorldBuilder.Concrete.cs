using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>The roads round the lake (Mall Road and the other lake-edge roads) are cement concrete slabs with white
    /// edge lines, not asphalt (user's Street View check at the Upper/Lower Mall split, 2026-11-11).</summary>
    public static partial class WorldBuilder
    {
        static WorldCtx concreteFor;
        static Material concreteMat;

        /// <summary>Light grey concrete; one slab joint across each tile (a tile is one road width long).</summary>
        static Material ConcreteMaterial(WorldCtx ctx)
        {
            if (concreteFor == ctx && concreteMat) return concreteMat;
            const int S = 512;
            var rnd = new System.Random(4242);
            var px = new Color32[S * S];
            // a photo of worn concrete underneath (CC0, Poly Haven), so the slabs look poured, not printed
            Color[] photo = null;
            int pw = 0, phh = 0;
            string pp = "Assets/Nainital/Data/PBR/concrete_floor_worn_001_albedo.png";
            if (System.IO.File.Exists(pp))
            {
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                t.LoadImage(System.IO.File.ReadAllBytes(pp));
                photo = t.GetPixels();
                pw = t.width;
                phh = t.height;
                Object.DestroyImmediate(t);
            }
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float g = photo != null
                        ? 0.06f + 0.9f * photo[((y * 2) % phh) * pw + ((x * 2) % pw)].grayscale + (Mathf.PerlinNoise(x * 0.02f, y * 0.02f) - 0.5f) * 0.03f
                        : 0.6f + (Mathf.PerlinNoise(x * 0.02f, y * 0.02f) - 0.5f) * 0.06f
                                   + (Mathf.PerlinNoise(x * 0.11f + 30f, y * 0.11f) - 0.5f) * 0.035f
                                   + ((float)rnd.NextDouble() - 0.5f) * 0.045f;
                    float wear = Mathf.Exp(-Mathf.Pow((Mathf.Abs(u - 0.5f) - 0.25f) / 0.07f, 2f));   // tyre bands
                    g *= 1f - wear * 0.06f;
                    g *= 1f - Mathf.Clamp01(Mathf.PerlinNoise(x * 0.008f + 7f, y * 0.008f) - 0.62f) * 0.5f;   // old stains
                    if (y < 3 || y > S - 3) g *= 0.55f;                         // transverse slab joint
                    if (Mathf.Abs(x - S / 2) < 1.5f) g *= 0.75f;               // longitudinal joint
                    px[y * S + x] = new Color(g * 1.01f, g, g * 0.97f, 1f);
                }
            var tex = SaveTexture(px, S, S, Gen + "/T_RoadConcrete.png", false, false);
            string path = Gen + "/M_RoadConcrete.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.08f);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            concreteFor = ctx;
            concreteMat = m;
            return m;
        }

        /// <summary>A CC0 photo texture (albedo + normal) from Assets/Nainital/Data/PBR as a URP Lit material.</summary>
        public static Material PhotoMaterial(string texName, string matName, Color tint, float smooth)
        {
            string dir = "Assets/Nainital/Data/PBR/";
            var alb = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + texName + "_albedo.png");
            if (!alb) return null;
            var nrmPath = dir + texName + "_normal.png";
            var imp = AssetImporter.GetAtPath(nrmPath) as TextureImporter;
            if (imp && imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; imp.SaveAndReimport(); }
            var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(nrmPath);
            string path = Gen + "/" + matName + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", alb);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_SmoothnessTextureChannel", 1f);
            m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            if (nrm) { m.SetTexture("_BumpMap", nrm); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
            EditorUtility.SetDirty(m);
            return m;
        }

        static readonly HashSet<string> NotRoads = new HashSet<string> { "footway", "steps", "path", "track", "bridleway" };

        /// <summary>Mall Road, and any road that runs mostly along the lake edge.</summary>
        static bool IsLakeRoad(WorldCtx ctx, RoadData r)
        {
            if (NotRoads.Contains(r.kind)) return false;
            if (!string.IsNullOrEmpty(r.name) && r.name.IndexOf("Mall Road", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            var line = Geo.ToPoints(r.pts);
            if (line.Length < 2) return false;
            var pts = Resample(line, 10f, false);
            int near = 0;
            foreach (var p in pts) if (ctx.LakeDistance(p) < 40f) near++;
            return near >= pts.Count * 0.6f;
        }

        static WorldCtx junctionFor;
        static HashSet<long> junctionCells;

        /// <summary>Within ~9 m of where roads meet: no painted lines there, they would criss-cross the junction.</summary>
        static bool NearJunction(WorldCtx ctx, Vector2 p)
        {
            if (junctionFor != ctx)
            {
                junctionFor = ctx;
                junctionCells = new HashSet<long>();
                foreach (var r in ctx.world.roads)
                {
                    if (NotRoads.Contains(r.kind)) continue;
                    var line = Geo.ToPoints(r.pts);
                    if (line.Length < 2) continue;
                    foreach (var e in new[] { line[0], line[line.Length - 1] })
                    {
                        int cx = Mathf.FloorToInt(e.x / 3f), cz = Mathf.FloorToInt(e.y / 3f);
                        for (int dz = -3; dz <= 3; dz++)
                            for (int dx = -3; dx <= 3; dx++)
                                if (dx * dx + dz * dz <= 10) junctionCells.Add(((long)(cx + dx) << 32) ^ (uint)(cz + dz));
                    }
                }
            }
            return junctionCells.Contains(((long)Mathf.FloorToInt(p.x / 3f) << 32) ^ (uint)Mathf.FloorToInt(p.y / 3f));
        }

        /// <summary>White edge lines (and a dashed centre line on wider roads) on one road segment.</summary>
        static void PaintStripes(MeshBuilder paint, Vector3 pl, Vector3 pr, Vector3 r, Vector3 l, float width, float run)
        {
            Vector2 white = Atlas.SolidUV(Pal.ShirtWhite);
            float e = 0.3f / width, w = 0.12f / width;
            Vector3 up = Vector3.up * 0.02f;
            void Band(float f0, float f1)
            {
                paint.Quad(Vector3.Lerp(pl, pr, f0) + up, Vector3.Lerp(pl, pr, f1) + up, Vector3.Lerp(l, r, f1) + up, Vector3.Lerp(l, r, f0) + up, white, Vector3.up);
            }
            Band(e, e + w);
            Band(1f - e - w, 1f - e);
            if (width >= 5.5f && Mathf.Repeat(run, 6f) < 3f) Band(0.5f - w * 0.5f, 0.5f + w * 0.5f);
        }
    }
}
