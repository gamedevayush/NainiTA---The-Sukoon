using System.Collections.Generic;
using System.Linq;
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
