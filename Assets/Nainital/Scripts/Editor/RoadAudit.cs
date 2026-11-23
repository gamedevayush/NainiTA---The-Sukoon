using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Lists every built object that stands on a carriageway (user, 2026-11-15: "church road pe aa raha hai ... baaki bhi
    /// bahut dikkatein"). For each renderer under Landmarks, Mallital, Tallital, Flats and the G.O.A.N. civic buildings, the
    /// road centre lines that pass through its footprint (bounds shrunk by 1.5 m) are counted; anything with a road through
    /// it is logged with its position, rotation and the road. Batch: "call RoadAudit.Run".
    /// </summary>
    public static class RoadAudit
    {
        /// <summary>
        /// Every place where something built stands ON a drivable road (owner, 23 Nov 2026: "house on road?"): the baked
        /// RoadNetwork's roads (the hand-traced ones too) sampled every 2 m, a 0.8 x 1.8 m box 0.5 m over the road tested
        /// against every collider but the ground, people, vehicles and triggers. Logged as "[Audit] road block" with the
        /// position, the road and the object's path; runs of hits on the same object are one line. Batch: "call
        /// RoadAudit.Colliders".
        /// </summary>
        public static void Colliders()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != WorldBuilder.ScenePath)
                EditorSceneManager.OpenScene(WorldBuilder.ScenePath);
            Physics.SyncTransforms();
            RoadNetwork net = null;
            foreach (var g in AssetDatabase.FindAssets("t:RoadNetwork")) { net = AssetDatabase.LoadAssetAtPath<RoadNetwork>(AssetDatabase.GUIDToAssetPath(g)); if (net) break; }
            if (!net) { Debug.Log("[Audit] no RoadNetwork"); return; }
            int mask = ~((1 << 2) | (1 << 4) | (1 << 8) | (1 << 9) | (1 << VehicleFactory.VehicleLayer));
            var buf = new Collider[16];
            var seen = new HashSet<(Collider, int, int)>();   // per object and 10 m cell (a houses chunk holds many)
            int lines = 0;
            foreach (var r in net.roads)
            {
                if (r?.pts == null) continue;
                for (int i = 0; i + 1 < r.pts.Length; i++)
                {
                    Vector3 a = r.pts[i], d = r.pts[i + 1] - a;
                    int n = Mathf.Max(1, Mathf.CeilToInt(new Vector2(d.x, d.z).magnitude / 2f));
                    var rot = Quaternion.LookRotation(new Vector3(d.x, 0f, d.z).sqrMagnitude > 1e-4f ? new Vector3(d.x, 0f, d.z) : Vector3.forward);
                    for (int k = 0; k <= n; k++)
                    {
                        Vector3 q = a + d * (k / (float)n);
                        int c = Physics.OverlapBoxNonAlloc(q + Vector3.up * 1.4f, new Vector3(0.4f, 0.9f, 0.4f), buf, rot, mask, QueryTriggerInteraction.Ignore);
                        for (int j = 0; j < c; j++)
                        {
                            var col = buf[j];
                            var key = (col, Mathf.FloorToInt(q.x / 10f), Mathf.FloorToInt(q.z / 10f));
                            if (col is TerrainCollider || seen.Contains(key)) continue;
                            seen.Add(key);
                            Debug.Log($"[Audit] road block at ({q.x:F0}, {q.y:F0}, {q.z:F0}) on {(string.IsNullOrEmpty(r.name) ? "road" : r.name)} (w {r.width:F1}): {PathOf(col.transform)}");
                            lines++;
                        }
                    }
                }
            }
            Debug.Log($"[Audit] road blocks: {lines} objects on {net.roads.Length} roads");
        }

        /// <summary>Every renderer whose bounds come within r of (x, z) (Bridge/arg.txt "x,z,r"): what is built there.</summary>
        public static void WhatIsAt()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != WorldBuilder.ScenePath)
                EditorSceneManager.OpenScene(WorldBuilder.ScenePath);
            string arg = "";
            try { arg = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../Bridge/arg.txt")).Trim(); } catch { }
            var n = arg.Split(',');
            if (n.Length < 3) { Debug.Log("[Audit] WhatIsAt needs x,z,r"); return; }
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            float x = float.Parse(n[0], inv), z = float.Parse(n[1], inv), r = float.Parse(n[2], inv);
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var b = mr.bounds;
                float dx = Mathf.Max(0f, Mathf.Abs(x - b.center.x) - b.extents.x), dz = Mathf.Max(0f, Mathf.Abs(z - b.center.z) - b.extents.z);
                if (dx * dx + dz * dz > r * r) continue;
                if (b.size.x > 300f || b.size.z > 300f) continue;
                Debug.Log($"[Audit] at ({x:F0},{z:F0}): {PathOf(mr.transform)} bounds {b.center:F0} size {b.size:F0} mat {(mr.sharedMaterial ? mr.sharedMaterial.name : "-")}");
            }
        }

        public static void Run()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != WorldBuilder.ScenePath)
                EditorSceneManager.OpenScene(WorldBuilder.ScenePath);
            var json = System.IO.File.ReadAllText("Assets/Nainital/Data/world.json");
            var world = JsonUtility.FromJson<WorldJson>(json);
            var roads = new List<(string name, float half, Vector2[] line)>();
            foreach (var r in world.roads)
            {
                if (r.kind == "path" || r.kind == "footway" || r.kind == "steps" || r.kind == "track" || r.kind == "bridleway" || r.kind == "pedestrian") continue;
                roads.Add((string.IsNullOrEmpty(r.name) ? r.kind : r.name, r.width * 0.5f, Geo.ToPoints(r.pts)));
            }
            string[] roots = { "Landmarks", "Mallital", "Tallital", "The Flats", "G.O.A.N.", "Capitol", "Boat House" };
            var hits = new List<string>();
            int checkedN = 0;
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                string path = PathOf(mr.transform);
                if (!roots.Any(r => path.Contains(r))) continue;
                if (path.Contains("Road") || path.Contains("road") || path.Contains("Lamp") || path.Contains("Sign") || path.Contains("board")) continue;
                var b = mr.bounds;
                if (b.size.x < 3f && b.size.z < 3f) continue;   // posts, signs, small props
                checkedN++;
                float sx = Mathf.Max(0f, b.extents.x - 1.5f), sz = Mathf.Max(0f, b.extents.z - 1.5f);
                Vector2 c = new Vector2(b.center.x, b.center.z);
                foreach (var (name, half, line) in roads)
                {
                    bool through = false;
                    for (int i = 0; i + 1 < line.Length && !through; i++)
                    {
                        // sample the segment every metre: inside the shrunk footprint?
                        Vector2 a = line[i], d = line[i + 1] - a;
                        int n = Mathf.Max(1, Mathf.CeilToInt(d.magnitude));
                        for (int k = 0; k <= n; k++)
                        {
                            Vector2 q = a + d * (k / (float)n);
                            if (Mathf.Abs(q.x - c.x) < sx - half * 0.5f && Mathf.Abs(q.y - c.y) < sz - half * 0.5f) { through = true; break; }
                        }
                    }
                    if (through)
                    {
                        hits.Add($"{path}  at ({c.x:F0}, {c.y:F0}) size {b.size.x:F0}x{b.size.z:F0} yaw {mr.transform.eulerAngles.y:F0}  <- {name}");
                        break;
                    }
                }
            }
            Debug.Log($"[Audit] {checkedN} objects checked, {hits.Count} with a road through them:\n" + string.Join("\n", hits));
        }

        static string PathOf(Transform t)
        {
            string s = t.name;
            for (var p = t.parent; p; p = p.parent) s = p.name + "/" + s;
            return s;
        }
    }
}
