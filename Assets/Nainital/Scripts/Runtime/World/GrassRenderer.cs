using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nainital
{
    /// <summary>
    /// GPU-instanced grass clumps around each camera, placed only where the terrain splat is grass /
    /// forest floor (not roads, dirt or rock), thinning out with distance. Works in Editor and game.
    /// </summary>
    [ExecuteAlways]
    public class GrassRenderer : MonoBehaviour
    {
        public Terrain terrain;
        public Material material;
        public float radius = 55f;
        public float cell = 2.6f;
        public int perCell = 8;

        // roads / paths (world XZ polylines) where no grass may grow; filled by the world builder.
        // The terrain splat is too coarse (~4 m texels) to keep grass off 5-7 m wide roads by itself.
        [HideInInspector] public Vector2[] noGrassPts;
        [HideInInspector] public int[] noGrassStarts;       // start index of each polyline in noGrassPts
        [HideInInspector] public float[] noGrassHalfWidth;  // per polyline

        const float MaskCell = 2f;
        System.Collections.BitArray noGrass;
        int maskW, maskH;
        Vector3 maskOrigin;

        Mesh[] clumps;
        byte[] grassiness;
        int ar;
        class Cache { public Vector3 center = new Vector3(1e9f, 0, 0); public Matrix4x4[][] m; public int[] n; }
        readonly Dictionary<int, Cache> caches = new Dictionary<int, Cache>();
        const int Batch = 1023;

        void OnEnable()
        {
            if (Application.isMobilePlatform) radius = Mathf.Min(radius, 32f);
            Prepare();
            RenderPipelineManager.beginCameraRendering += Draw;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Draw;
            caches.Clear();
        }

        static Mesh Clump(int variant)
        {
            var mb = new MeshBuilder();
            var rnd = new System.Random(variant * 977 + 3);
            int blades = 7;
            for (int b = 0; b < blades; b++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float r = (float)rnd.NextDouble() * 0.14f;
                float h = 0.12f + (float)rnd.NextDouble() * 0.15f;
                float lean = 0.04f + (float)rnd.NextDouble() * 0.09f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Vector3 side = new Vector3(-dir.z, 0, dir.x) * 0.022f;
                Vector3 basePos = dir * r;
                Vector3 tip = basePos + dir * lean + Vector3.up * h;
                Vector2 uv = Atlas.ExtraUV(rnd.NextDouble() < 0.25 ? Px.GrassDry : Px.GrassBlade);
                mb.Tri(basePos - side, basePos + side, tip, uv, dir + Vector3.up);
                mb.Tri(basePos - side, basePos + side, tip, uv, -dir + Vector3.up);
            }
            var m = mb.ToMesh("GrassClump" + variant);
            m.hideFlags = HideFlags.DontSave;
            return m;
        }

        void Prepare()
        {
            if (!terrain) terrain = Terrain.activeTerrain;
            if (!terrain || !material) return;
            clumps = new[] { Clump(0), Clump(1), Clump(2) };
            var td = terrain.terrainData;
            ar = td.alphamapResolution;
            grassiness = new byte[ar * ar];
            const int Band = 256;   // read the 4096 alphamap in bands: all of it as floats at once is 268 MB
            for (int y0 = 0; y0 < ar; y0 += Band)
            {
                int rows = Mathf.Min(Band, ar - y0);
                var a = td.GetAlphamaps(0, y0, ar, rows);
                int layers = a.GetLength(2);
                for (int yy = 0; yy < rows; yy++)
                    for (int x = 0; x < ar; x++)
                    {
                        float g = a[yy, x, 0] + (layers > 1 ? a[yy, x, 1] * 0.3f : 0f);   // tufts in the open, few in the leaf litter under trees
                        float bad = (layers > 2 ? a[yy, x, 2] : 0f) + (layers > 3 ? a[yy, x, 3] * 1.6f : 0f);
                        grassiness[(y0 + yy) * ar + x] = (byte)(Mathf.Clamp01(g - bad * 0.8f) * 255f);
                    }
            }
            BuildRoadMask(td);
            caches.Clear();
        }

        void BuildRoadMask(TerrainData td)
        {
            noGrass = null;
            if (noGrassPts == null || noGrassStarts == null || noGrassHalfWidth == null || noGrassPts.Length < 2) return;
            maskOrigin = terrain.transform.position;
            maskW = Mathf.CeilToInt(td.size.x / MaskCell) + 1;
            maskH = Mathf.CeilToInt(td.size.z / MaskCell) + 1;
            noGrass = new System.Collections.BitArray(maskW * maskH);
            for (int l = 0; l < noGrassStarts.Length; l++)
            {
                int s0 = noGrassStarts[l], s1 = l + 1 < noGrassStarts.Length ? noGrassStarts[l + 1] : noGrassPts.Length;
                float hw = (l < noGrassHalfWidth.Length ? noGrassHalfWidth[l] : 2f) + 0.9f;   // + verge
                for (int i = s0; i + 1 < s1; i++)
                {
                    Vector2 a = noGrassPts[i], b = noGrassPts[i + 1], ab = b - a;
                    float len2 = Mathf.Max(ab.sqrMagnitude, 1e-4f);
                    int x0 = Mathf.FloorToInt((Mathf.Min(a.x, b.x) - hw - maskOrigin.x) / MaskCell), x1 = Mathf.CeilToInt((Mathf.Max(a.x, b.x) + hw - maskOrigin.x) / MaskCell);
                    int z0 = Mathf.FloorToInt((Mathf.Min(a.y, b.y) - hw - maskOrigin.z) / MaskCell), z1 = Mathf.CeilToInt((Mathf.Max(a.y, b.y) + hw - maskOrigin.z) / MaskCell);
                    x0 = Mathf.Max(0, x0); z0 = Mathf.Max(0, z0); x1 = Mathf.Min(maskW - 1, x1); z1 = Mathf.Min(maskH - 1, z1);
                    for (int z = z0; z <= z1; z++)
                        for (int x = x0; x <= x1; x++)
                        {
                            Vector2 p = new Vector2(maskOrigin.x + (x + 0.5f) * MaskCell, maskOrigin.z + (z + 0.5f) * MaskCell);
                            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                            if ((p - (a + ab * t)).sqrMagnitude <= hw * hw) noGrass[z * maskW + x] = true;
                        }
                }
            }
        }

        bool OnRoad(float x, float z)
        {
            if (noGrass == null) return false;
            int ix = (int)((x - maskOrigin.x) / MaskCell), iz = (int)((z - maskOrigin.z) / MaskCell);
            return ix >= 0 && iz >= 0 && ix < maskW && iz < maskH && noGrass[iz * maskW + ix];
        }

        static float Hash(int x, int z, int k)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093 ^ z * 19349663 ^ k * 83492791);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (h & 0xFFFF) / 65535f;
            }
        }

        Cache Build(Camera cam)
        {
            int id = cam.GetInstanceID();
            if (!caches.TryGetValue(id, out var c))
            {
                c = new Cache { m = new Matrix4x4[3][], n = new int[3] };
                for (int i = 0; i < 3; i++) c.m[i] = new Matrix4x4[4096];
                caches[id] = c;
            }
            Vector3 cp = cam.transform.position;
            if ((cp - c.center).sqrMagnitude < cell * cell) return c;
            c.center = cp;
            for (int i = 0; i < 3; i++) c.n[i] = 0;
            var td = terrain.terrainData;
            Vector3 tp = terrain.transform.position, ts = td.size;
            float groundY = terrain.SampleHeight(cp) + tp.y;
            if (cp.y - groundY > radius * 1.5f) return c;     // high up: no grass needed
            int r = Mathf.CeilToInt(radius / cell);
            int cx = Mathf.FloorToInt(cp.x / cell), cz = Mathf.FloorToInt(cp.z / cell);
            for (int gz = cz - r; gz <= cz + r; gz++)
                for (int gx = cx - r; gx <= cx + r; gx++)
                    for (int k = 0; k < perCell; k++)
                    {
                        float px = (gx + Hash(gx, gz, k * 2)) * cell, pz = (gz + Hash(gx, gz, k * 2 + 1)) * cell;
                        float dx = px - cp.x, dz = pz - cp.z;
                        float d = Mathf.Sqrt(dx * dx + dz * dz);
                        if (d > radius) continue;
                        if (Hash(gx, gz, k + 40) > 1f - (d / radius) * 0.75f) continue;   // thinner far away
                        float u = (px - tp.x) / ts.x, v = (pz - tp.z) / ts.z;
                        if (u < 0 || u > 1 || v < 0 || v > 1) continue;
                        int ix = Mathf.Clamp((int)(u * ar), 0, ar - 1), iz = Mathf.Clamp((int)(v * ar), 0, ar - 1);
                        if (Hash(gx, gz, k + 90) * 255f > grassiness[iz * ar + ix]) continue;
                        if (OnRoad(px, pz)) continue;
                        Vector3 n = td.GetInterpolatedNormal(u, v);
                        if (n.y < 0.85f) continue;   // none on banks steeper than ~32 deg (bare soil and stone there)
                        float y = td.GetInterpolatedHeight(u, v) + tp.y - 0.03f;
                        int variant = (int)(Hash(gx, gz, k + 7) * 2.999f);
                        float s = 0.7f + Hash(gx, gz, k + 13) * 0.5f;
                        var m = Matrix4x4.TRS(new Vector3(px, y, pz), Quaternion.Euler(0, Hash(gx, gz, k + 21) * 360f, 0), new Vector3(s, s * (0.8f + Hash(gx, gz, k + 5) * 0.5f), s));
                        if (c.n[variant] >= c.m[variant].Length) System.Array.Resize(ref c.m[variant], c.m[variant].Length * 2);
                        c.m[variant][c.n[variant]++] = m;
                    }
            return c;
        }

        void Draw(ScriptableRenderContext ctx, Camera cam)
        {
            if (clumps == null || grassiness == null || !terrain || cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection) return;
            var c = Build(cam);
            if (!material.enableInstancing) material.enableInstancing = true;   // without it every draw throws and the frame stays black
            var rp = new RenderParams(material) { camera = cam, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true, worldBounds = new Bounds(c.center, Vector3.one * radius * 3f) };
            for (int v = 0; v < 3; v++)
                for (int s = 0; s < c.n[v]; s += Batch)
                    Graphics.RenderMeshInstanced(rp, clumps[v], 0, c.m[v], Mathf.Min(Batch, c.n[v] - s), s);
        }
    }
}
