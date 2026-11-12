using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Finds where the terrain comes up through the roads (user, 2026-11-12: "terrain kahin kahin road pe aa rahi hai").
    /// Every road of world.json is walked every 3 m, at its centre and near both edges; a ray from above must meet the
    /// road before the ground. Where it meets the terrain first, or the terrain is within 5 cm of the road surface
    /// (close enough that a phone's coarser terrain mesh pokes through), the spot is listed. Writes Bridge/road_check.csv
    /// and logs the worst places.
    /// </summary>
    public static class RoadCheck
    {
        static readonly HashSet<string> Skip = new HashSet<string> { "path", "footway", "steps", "bridleway", "track", "cycleway", "corridor" };

        public static void Run()
        {
            var world = JsonUtility.FromJson<WorldJson>(File.ReadAllText(WorldBuilder.Root + "/Data/world.json"));
            var terrain = Terrain.activeTerrain;
            if (!terrain) { Debug.Log("[Nainital] road check: no terrain"); return; }
            Physics.SyncTransforms();
            int mask = ~((1 << 8) | (1 << 9) | (1 << 10) | (1 << 2) | (1 << 4));
            int samples = 0, through = 0, close = 0;
            var bad = new List<(Vector3 p, string road, float diff, bool hitTerrain)>();
            foreach (var r in world.roads)
            {
                if (Skip.Contains(r.kind)) continue;
                var line = Geo.ToPoints(r.pts);
                if (line.Length < 2) continue;
                var pts = WorldBuilder.Resample(line, 3f, false);
                for (int i = 0; i < pts.Count; i++)
                {
                    Vector2 tan = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                    Vector2 n = new Vector2(-tan.y, tan.x);
                    float hw = Mathf.Max(0.6f, r.width * 0.5f - 0.35f);
                    foreach (float off in new[] { -hw, 0f, hw })
                    {
                        Vector2 q = pts[i] + n * off;
                        float tY = terrain.SampleHeight(new Vector3(q.x, 0f, q.y)) + terrain.transform.position.y;
                        if (!Physics.Raycast(new Vector3(q.x, tY + 30f, q.y), Vector3.down, out var hit, 60f, mask, QueryTriggerInteraction.Ignore)) continue;
                        samples++;
                        bool hitTerrain = hit.collider is TerrainCollider;
                        if (hitTerrain)
                        {
                            // the road may simply be missing here (a gap) or buried under the ground
                            through++;
                            bad.Add((hit.point, r.name ?? r.kind, 0f, true));
                            continue;
                        }
                        if (hit.point.y > tY + 3f) continue;   // a bridge, a deck or a roof over the road: not a road surface check
                        float diff = hit.point.y - tY;          // how far the road stands over the terrain
                        if (diff < 0.05f) { close++; bad.Add((hit.point, r.name ?? r.kind, diff, false)); }
                    }
                }
            }
            string csv = Path.GetFullPath(Path.Combine(Application.dataPath, "../Bridge/road_check.csv"));
            using (var w = new StreamWriter(csv))
            {
                w.WriteLine("x,y,z,road,road_over_terrain_m,terrain_hit");
                foreach (var b in bad) w.WriteLine($"{b.p.x:F1},{b.p.y:F2},{b.p.z:F1},\"{b.road}\",{b.diff:F3},{(b.hitTerrain ? 1 : 0)}");
            }
            // group the bad spots into 40 m cells and log the worst
            var cells = new Dictionary<Vector2Int, int>();
            var where = new Dictionary<Vector2Int, (Vector3 p, string road)>();
            foreach (var b in bad)
            {
                var k = new Vector2Int(Mathf.FloorToInt(b.p.x / 40f), Mathf.FloorToInt(b.p.z / 40f));
                cells.TryGetValue(k, out int c);
                cells[k] = c + 1;
                where[k] = (b.p, b.road);
            }
            var list = new List<KeyValuePair<Vector2Int, int>>(cells);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            Debug.Log($"[Nainital] road check: {samples} samples, terrain over the road at {through}, road within 5 cm of the terrain at {close}, in {cells.Count} places -> {csv}");
            for (int i = 0; i < Mathf.Min(15, list.Count); i++)
                Debug.Log($"[Nainital]   {list[i].Value} bad samples near {where[list[i].Key].p:F0} ({where[list[i].Key].road})");
        }
    }
}
