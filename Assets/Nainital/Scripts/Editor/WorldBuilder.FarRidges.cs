using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nainital.EditorTools
{
    /// <summary>The land beyond the 4 km map: ridge after ridge fading into haze, and the Kumaon snow range (Trishul,
    /// Nanda Devi, Panchachuli) on the north-east horizon, from real terrain (Tools/make_far_ridges.py). Without it the
    /// viewpoints look out at an empty sky.</summary>
    public static partial class WorldBuilder
    {
        [System.Serializable] class FarBand { public float d0, d1; public float[] lo, hi, elev, dist; }
        [System.Serializable] class FarData { public float azStep, eyeLo, eyeHi, snowLine; public FarBand[] bands; }

        static void BuildFarRidges(WorldCtx ctx)
        {
            const string path = "Assets/Nainital/Data/far_ridges.json";
            if (!File.Exists(path)) { Debug.LogWarning("[Nainital] no " + path + " (run Tools/make_far_ridges.py)"); return; }
            var data = JsonUtility.FromJson<FarData>(File.ReadAllText(path));
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            const float bottom = -25f * Mathf.Deg2Rad;
            var rockNear = new Color(0.12f, 0.18f, 0.14f);
            var rockFar = new Color(0.2f, 0.25f, 0.33f);
            var rockRange = new Color(0.32f, 0.38f, 0.5f);     // the blue base of the snow range
            var snowLit = new Color(1f, 1f, 1f);
            var snowShade = new Color(0.62f, 0.7f, 0.86f);
            int snowCols = 0;

            // farthest band first: one draw, painted back to front
            for (int b = data.bands.Length - 1; b >= 0; b--)
            {
                var B = data.bands[b];
                if (B.d1 <= 4000f) continue;   // just past the map's edge: from off-centre viewpoints it stands up as walls
                int n = B.lo.Length, start = verts.Count;
                bool range = B.d0 >= 60000f;
                float ex = range ? 2.2f : 1f;   // the snow range enlarged a little, so it reads as it does to the eye
                for (int i = 0; i <= n; i++)
                {
                    int k = i % n;
                    float az = i * data.azStep * Mathf.Deg2Rad;
                    float lo = Mathf.Max(B.lo[k] > 0f ? B.lo[k] * ex : B.lo[k], -25f) * Mathf.Deg2Rad;
                    float hi = Mathf.Max(B.hi[k] > 0f ? B.hi[k] * ex : B.hi[k], -25f) * Mathf.Deg2Rad;
                    float dist = Mathf.Max(B.dist[k], 1f);
                    float snowDepth = B.elev[k] > data.snowLine ? Mathf.Atan((B.elev[k] - data.snowLine) / dist) * ex : 0f;
                    if (snowDepth > 0f && b == data.bands.Length - 1) snowCols++;
                    float haze = range ? 0.3f : Mathf.Clamp(1f - Mathf.Exp(-dist / 60000f), 0.04f, 0.6f);   // the atmosphere pass adds the rest
                    var rock = range ? rockRange : Color.Lerp(rockNear, rockFar, Mathf.InverseLerp(4000f, 40000f, dist));
                    // one flank of each peak in sun, the other in blue shade, from which way the skyline climbs
                    float climb = B.lo[(k + 1) % n] - B.lo[(k + n - 1) % n];
                    var snow = Color.Lerp(snowShade, snowLit, Mathf.Clamp01(0.55f - climb * 12f));
                    // bottom (valley mist), rock top, snow start, top
                    verts.Add(new Vector3(az, bottom, bottom)); cols.Add(rock); uvs.Add(new Vector2(Mathf.Min(haze + 0.3f, 0.85f), 0));
                    verts.Add(new Vector3(az, lo - snowDepth, hi - snowDepth)); cols.Add(rock); uvs.Add(new Vector2(haze, 0));
                    verts.Add(new Vector3(az, lo - snowDepth, hi - snowDepth)); cols.Add(snow); uvs.Add(new Vector2(0.14f, 0));
                    verts.Add(new Vector3(az, lo, hi)); cols.Add(snowDepth > 0f ? snow : rock); uvs.Add(new Vector2(snowDepth > 0f ? 0.12f : haze, 0));
                }
                for (int i = 0; i < n; i++)
                {
                    int a = start + i * 4, c = a + 4;
                    tris.AddRange(new[] { a, a + 1, c + 1, a, c + 1, c });             // rock
                    tris.AddRange(new[] { a + 2, a + 3, c + 3, a + 2, c + 3, c + 2 }); // snow cap (flat where there is none)
                }
            }

            var mesh = new Mesh { name = "FarRidges", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 200000f);   // it follows the camera: never cull it
            string meshPath = Gen + "/FarRidges.asset", matPath = Gen + "/M_FarRidges.mat";
            if (File.Exists(meshPath)) AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);
            var sh = Shader.Find("Nainital/FarRidges");
            if (!sh) { Debug.LogWarning("[Nainital] FarRidges shader missing"); return; }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!mat) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, matPath); }
            mat.shader = sh;
            mat.renderQueue = -1;   // the shader's: after the skybox
            float hMin = ctx.world.meta.hMin;
            mat.SetFloat("_EyeLo", data.eyeLo - hMin);
            mat.SetFloat("_EyeHi", data.eyeHi - hMin);
            mat.SetFloat("_Radius", 0.94f);
            EditorUtility.SetDirty(mat);

            var go = new GameObject("Far ridges (beyond the map)");
            go.transform.SetParent(ctx.worldRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            Debug.Log($"[Nainital] far ridges: {data.bands.Length} bands, {verts.Count} vertices, snow on {snowCols} columns of the far band");
        }
    }
}
