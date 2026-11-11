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

        /// <summary>A halo round every lamp head and a pool of light on the ground under it (Shaders/LampGlow), in meshes
        /// of 250 m cells.</summary>
        static void BuildLampGlow(WorldCtx ctx, Transform parent)
        {
            var sh = Shader.Find("Nainital/LampGlow");
            if (!sh) { Debug.LogWarning("[Nainital] LampGlow shader missing"); return; }
            string mp = Gen + "/M_LampGlow.mat";
            lampGlowMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (!lampGlowMat) { lampGlowMat = new Material(sh) { name = "M_LampGlow" }; UnityEditor.AssetDatabase.CreateAsset(lampGlowMat, mp); }
            lampGlowMat.shader = sh;

            var cells = new Dictionary<Vector2Int, (List<Vector3> v, List<Color> c, List<Vector2> uv, List<Vector2> uv2, List<int> t)>();
            Physics.SyncTransforms();
            int mask = ~((1 << 8) | (1 << 9) | (1 << 2) | (1 << 10) | (1 << 4));
            Color warm = new Color(1f, 0.74f, 0.45f);
            int pools = 0;
            foreach (var head in ctx.lampHeads)
            {
                var key = new Vector2Int(Mathf.FloorToInt(head.x / 250f), Mathf.FloorToInt(head.z / 250f));
                if (!cells.TryGetValue(key, out var m)) { m = (new List<Vector3>(), new List<Color>(), new List<Vector2>(), new List<Vector2>(), new List<int>()); cells[key] = m; }
                // halo: four corners at the bulb, spread on screen by the shader
                int b = m.v.Count;
                for (int k = 0; k < 4; k++)
                {
                    m.v.Add(head);
                    m.c.Add(new Color(warm.r * 2.2f, warm.g * 2.0f, warm.b * 1.8f, 1f));
                    m.uv2.Add(new Vector2(0.75f, 0f));
                }
                m.uv.Add(new Vector2(0, 0)); m.uv.Add(new Vector2(1, 0)); m.uv.Add(new Vector2(1, 1)); m.uv.Add(new Vector2(0, 1));
                m.t.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
                // pool: a square lying on whatever is under the bulb (road deck, paving, terrain)
                if (!Physics.Raycast(head + Vector3.down * 0.4f, Vector3.down, out var hit, 12f, mask, QueryTriggerInteraction.Ignore)) continue;
                Vector3 n = hit.normal;
                if (n.y < 0.6f) continue;
                Vector3 t1 = Vector3.Cross(n, Vector3.forward).normalized;
                if (t1.sqrMagnitude < 0.01f) t1 = Vector3.right;
                Vector3 t2 = Vector3.Cross(t1, n);
                float r = Mathf.Clamp((head.y - hit.point.y) * 1.15f, 3f, 7f);
                Vector3 c0 = hit.point + n * 0.07f;
                b = m.v.Count;
                m.v.Add(c0 - t1 * r - t2 * r); m.v.Add(c0 + t1 * r - t2 * r); m.v.Add(c0 + t1 * r + t2 * r); m.v.Add(c0 - t1 * r + t2 * r);
                for (int k = 0; k < 4; k++) { m.c.Add(new Color(warm.r, warm.g, warm.b, 0.55f)); m.uv2.Add(Vector2.zero); }
                m.uv.Add(new Vector2(0, 0)); m.uv.Add(new Vector2(1, 0)); m.uv.Add(new Vector2(1, 1)); m.uv.Add(new Vector2(0, 1));
                m.t.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
                pools++;
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
            Debug.Log($"[Nainital] lamp glow: {ctx.lampHeads.Count} halos, {pools} pools of light, {idx} meshes");
        }
    }
}
