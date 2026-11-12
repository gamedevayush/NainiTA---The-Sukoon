using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    public static partial class WorldBuilder
    {
        /// <summary>Lamp posts round the popular places that have none yet (temples, viewpoints, gardens, the bazaars),
        /// so every place people go to at night is lit (user, 2026-11-11).</summary>
        static void BuildLandmarkLamps(WorldCtx ctx)
        {
            var parent = new GameObject("Landmark Lights").transform;
            parent.SetParent(ctx.worldRoot, false);
            var mb = new MeshBuilder();
            var col = new MeshBuilder();
            var existing = new List<Vector3>(ctx.lampHeads);
            int made = 0;
            Physics.SyncTransforms();
            foreach (var kv in ctx.landmarkSites)
            {
                Vector3 c = kv.Value;
                if (ctx.InLake(new Vector2(c.x, c.z))) continue;
                bool lit = false;
                foreach (var e in existing) if ((new Vector2(e.x - c.x, e.z - c.z)).sqrMagnitude < 22f * 22f) { lit = true; break; }
                if (lit) continue;
                for (int k = 0; k < 4; k++)
                {
                    float a = k * Mathf.PI * 0.5f + 0.4f;
                    Vector2 q = new Vector2(c.x + Mathf.Cos(a) * 11f, c.z + Mathf.Sin(a) * 11f);
                    if (ctx.InLake(q)) continue;
                    float y = ctx.H(q);
                    if (Physics.Raycast(new Vector3(q.x, y + 25f, q.y), Vector3.down, out var hit, 40f, ~((1 << 8) | (1 << 9) | (1 << 2) | (1 << 10)), QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider is not TerrainCollider && hit.point.y > y + 2.5f) continue;   // a roof: no lamp post there
                        y = hit.point.y;
                    }
                    var basePos = new Vector3(q.x, y, q.y);
                    var rot = Quaternion.LookRotation(new Vector3(c.x - q.x, 0f, c.z - q.y).normalized);
                    using (PropKit.At(mb, basePos, rot))
                    {
                        Vector3 bulb = PropKit.RoadLamp(mb);
                        ctx.lampHeads.Add(basePos + rot * bulb);
                    }
                    col.Box(basePos + Vector3.up * 2.8f, new Vector3(0.25f, 5.6f, 0.25f), Vector2.zero);
                    made++;
                }
            }
            ctx.MeshObject("Landmark lamps", parent, mb, ctx.mats.atlas, col);
            Debug.Log($"[Nainital] landmark lamps: {made}");
        }

        static Material lampGlowMat;

        /// <summary>Sodium or white LED for each lamp (about a third LED, as in the town now), the same every build.</summary>
        static Color[] LampColors(List<Vector3> heads)
        {
            var c = new Color[heads.Count];
            for (int i = 0; i < heads.Count; i++)
            {
                uint h = (uint)(Mathf.RoundToInt(heads[i].x * 7f) * 73856093 ^ Mathf.RoundToInt(heads[i].z * 7f) * 19349663);
                c[i] = (h % 100) < 35 ? new Color(0.92f, 0.96f, 1f) : new Color(1f, 0.70f, 0.38f);
            }
            return c;
        }

        /// <summary>A light just outside every shop front at its sign board: warm or cool white, on all night.</summary>
        static void ShopLights(WorldCtx ctx, out Vector3[] pos, out Color[] col)
        {
            var p = new List<Vector3>();
            var c = new List<Color>();
            foreach (var (front, n, width) in ctx.shopFronts)
            {
                Vector3 nf = new Vector3(n.x, 0f, n.z).normalized;
                p.Add(front + nf * 0.7f - Vector3.up * 0.35f);
                uint h = (uint)(Mathf.RoundToInt(front.x * 5f) * 83492791 ^ Mathf.RoundToInt(front.z * 5f) * 2654435761u);
                c.Add((h % 100) < 45 ? new Color(0.9f, 0.95f, 1f) : new Color(1f, 0.86f, 0.62f));
            }
            pos = p.ToArray();
            col = c.ToArray();
        }

        /// <summary>A halo round every lamp bulb and every shop light (Shaders/LampGlow), in meshes of 250 m cells; the light
        /// on the ground comes from the real pooled lights (LampManager), which follow every rise and fall of a road.</summary>
        static void BuildLampGlow(WorldCtx ctx, Transform parent, Color[] lampCol, Vector3[] shopPos, Color[] shopCol)
        {
            var sh = Shader.Find("Nainital/LampGlow");
            if (!sh) { Debug.LogWarning("[Nainital] LampGlow shader missing"); return; }
            string mp = Gen + "/M_LampGlow.mat";
            lampGlowMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (!lampGlowMat) { lampGlowMat = new Material(sh) { name = "M_LampGlow" }; UnityEditor.AssetDatabase.CreateAsset(lampGlowMat, mp); }
            lampGlowMat.shader = sh;

            var cells = new Dictionary<Vector2Int, (List<Vector3> v, List<Color> c, List<Vector2> uv, List<Vector2> uv2, List<int> t)>();
            void Halo(Vector3 at, Color col, float size)
            {
                var key = new Vector2Int(Mathf.FloorToInt(at.x / 250f), Mathf.FloorToInt(at.z / 250f));
                if (!cells.TryGetValue(key, out var m)) { m = (new List<Vector3>(), new List<Color>(), new List<Vector2>(), new List<Vector2>(), new List<int>()); cells[key] = m; }
                int b = m.v.Count;
                for (int k = 0; k < 4; k++) { m.v.Add(at); m.c.Add(col); m.uv2.Add(new Vector2(size, 0f)); }
                m.uv.Add(new Vector2(0, 0)); m.uv.Add(new Vector2(1, 0)); m.uv.Add(new Vector2(1, 1)); m.uv.Add(new Vector2(0, 1));
                m.t.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }
            for (int i = 0; i < ctx.lampHeads.Count; i++)
            {
                Color c = i < lampCol.Length ? lampCol[i] : new Color(1f, 0.7f, 0.38f);
                Halo(ctx.lampHeads[i], new Color(c.r * 3f, c.g * 2.8f, c.b * 2.6f, 1f), 0.9f);
            }
            for (int i = 0; i < shopPos.Length; i++)
            {
                Color c = shopCol[i];
                Halo(shopPos[i] + Vector3.up * 0.25f, new Color(c.r * 1.8f, c.g * 1.8f, c.b * 1.8f, 1f), 0.45f);
            }
            var root = new GameObject("Lamp glow (night)").transform;
            root.SetParent(parent, false);
            int idx = 0;
            foreach (var kv in cells)
            {
                var mesh = new Mesh { name = "LampGlow_" + kv.Key.x + "_" + kv.Key.y, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(kv.Value.v);
                mesh.SetColors(kv.Value.c);
                mesh.SetUVs(0, kv.Value.uv);
                mesh.SetUVs(1, kv.Value.uv2);
                mesh.SetTriangles(kv.Value.t, 0);
                mesh.RecalculateBounds();
                var bnd = mesh.bounds;
                bnd.Expand(4f);   // the billboards reach past the points they are built on
                mesh.bounds = bnd;
                string path = Gen + "/" + mesh.name + ".asset";
                if (System.IO.File.Exists(path)) UnityEditor.AssetDatabase.DeleteAsset(path);
                UnityEditor.AssetDatabase.CreateAsset(mesh, path);
                var go = new GameObject(mesh.name);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = lampGlowMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                idx++;
            }
            Debug.Log($"[Nainital] lamp glow: {ctx.lampHeads.Count} lamp halos, {shopPos.Length} shop lights, {idx} meshes");
        }
    }
}
