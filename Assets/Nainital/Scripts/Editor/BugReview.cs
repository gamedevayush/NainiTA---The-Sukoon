using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// "bug dekho" (user, 2026-11-14): replays every report in BugReports on the current scene. For each note.txt it renders
    /// the reporter's own camera ("shot spec") and a top-down view of the spot into Shots/bug_*, lists what stands there
    /// (colliders within 14 m, terrain vs surface heights on a grid), then runs the RoadCheck over all roads.
    /// Batch: Unity -batchmode -quit -projectPath . -executeMethod Nainital.EditorTools.BugReview.Batch
    /// </summary>
    public static class BugReview
    {
        public static void Batch()
        {
            try { Run(); }
            catch (System.Exception e) { Debug.LogException(e); UnityEditor.EditorApplication.Exit(1); }
        }

        /// <summary>Renders the two Naina Devi gates from their GateShot markers into Shots/gate_*.
        /// Batch: -executeMethod Nainital.EditorTools.BugReview.GatesBatch</summary>
        public static void GatesBatch()
        {
            try
            {
                var inv = CultureInfo.InvariantCulture;
                EditorSceneManager.OpenScene(WorldBuilder.ScenePath);
                var shots = new List<string>();
                string V3(Vector3 v) => v.x.ToString("F2", inv) + "," + v.y.ToString("F2", inv) + "," + v.z.ToString("F2", inv);
                // every marker left by the builders: GateShot* and WorshipShot* (the four places of worship)
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    if (!t.name.StartsWith("GateShot") && !t.name.StartsWith("WorshipShot")) continue;
                    string name = t.name.Replace(' ', '_');
                    Vector3 p = t.position, f = t.forward, r = t.right;
                    shots.Add($"gate_{name};{V3(p)};{V3(p + f * 10f)};11");
                    shots.Add($"gate_{name}_side;{V3(p + r * 14f + f * 4f + Vector3.up * 4f)};{V3(p + f * 22f + Vector3.up * 3f)};15");
                    Debug.Log($"[Nainital] gate review: {t.name} at {V3(p)}");
                }
                // the Shaktipeeth gate prefab, wherever it has been placed
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    if (!t.name.StartsWith("Shaktipeeth") || t.parent == null || t.parent.name != WorldBuilder.PlacedRootName) continue;
                    Vector3 p = t.position, f = t.forward, r = t.right;
                    shots.Add($"gate_Shaktipeeth;{V3(p + f * 17f + r * 3.5f + Vector3.up * 1.7f)};{V3(p + Vector3.up * 4.6f)};11");
                    Debug.Log($"[Nainital] gate review: Shaktipeeth gate at {V3(p)}");
                }
                // the walk from Mall Road to the temple, and a few places changed from the user's video
                void Ground(string name, float x, float z, float lx, float lz, float hour) =>
                    shots.Add($"gate_{name};{x.ToString(inv)},1.7,{z.ToString(inv)};{lx.ToString(inv)},1.5,{lz.ToString(inv)};{hour.ToString(inv)};g");
                Ground("walk_from_mall", 80f, 104f, 40f, 40f, 11f);
                Ground("walk_by_capitol", 20f, 0f, -40f, -90f, 11f);
                foreach (var marker in new[] { "MallShotLane", "MallShotA" })
                {
                    var mk = GameObject.Find(marker);
                    if (mk) shots.Add($"gate_{marker};{V3(mk.transform.position)};{V3(mk.transform.position + mk.transform.forward * 30f)};11");
                }
                foreach (var name in new[] { "Tallital traffic post", "Mallital boat ghat boats" })
                {
                    var go = GameObject.Find(name);
                    var rend = go ? go.GetComponent<Renderer>() : null;
                    if (!rend) continue;
                    Vector3 c = rend.bounds.center;
                    shots.Add($"gate_{name.Replace(' ', '_')};{V3(c + new Vector3(11f, 3.5f, 11f))};{V3(c)};11");
                }
                PreviewCapture.Only = new[] { "-" };
                PreviewCapture.Custom = string.Join("|", shots);
                PreviewCapture.Capture();
                PreviewCapture.Only = null;
                PreviewCapture.Custom = null;
            }
            catch (System.Exception e) { Debug.LogException(e); UnityEditor.EditorApplication.Exit(1); }
        }

        /// <summary>Only the report folders whose name starts with this (e.g. "2026-11-16"); null = all.
        /// Batch: set env NT_BUGS to the prefix.</summary>
        public static string OnlyPrefix;

        public static void Run()
        {
            string only = OnlyPrefix ?? System.Environment.GetEnvironmentVariable("NT_BUGS");
            var inv = CultureInfo.InvariantCulture;
            string root = Path.GetFullPath("BugReports");
            if (!Directory.Exists(root)) { Debug.Log("[Nainital] bug review: no BugReports folder"); return; }
            EditorSceneManager.OpenScene(WorldBuilder.ScenePath);
            Physics.SyncTransforms();
            var terrain = Terrain.activeTerrain;
            var shots = new List<string>();
            var report = new StringBuilder();
            foreach (var dir in Directory.GetDirectories(root))
            {
                string note = Path.Combine(dir, "note.txt");
                if (!File.Exists(note)) continue;
                if (!string.IsNullOrEmpty(only) && !Path.GetFileName(dir).StartsWith(only)) continue;
                string id = "bug_" + Path.GetFileName(dir).Replace("-", "").Replace("_", "");
                string spec = null, player = null, first = null;
                foreach (var line in File.ReadAllLines(note))
                {
                    if (first == null && line.StartsWith("BUG:")) first = line;
                    if (line.StartsWith("shot spec: ")) spec = line.Substring(11);
                    if (line.StartsWith("player: ")) player = line.Substring(8).Split(' ')[0];
                }
                report.AppendLine("== " + Path.GetFileName(dir) + "  " + first);
                if (spec != null)
                {
                    var f = spec.Split(';');   // bug;pos;look;hour
                    if (f.Length >= 4) shots.Add($"{id}_view;{f[1]};{f[2]};{f[3]}");
                    if (f.Length >= 4) shots.Add($"{id}_viewday;{f[1]};{f[2]};12");   // the same view at noon, to see the shapes
                    if (f.Length >= 4)
                    {   // and at eye height over today's ground (a fix may have moved the ground under the old camera)
                        var a = f[1].Split(','); var b = f[2].Split(',');
                        shots.Add($"{id}_eye;{a[0]},1.7,{a[2]};{b[0]},0.8,{b[2]};12;g");
                    }
                }
                if (player == null) continue;
                var c = player.Split(',');
                var p = new Vector3(float.Parse(c[0], inv), float.Parse(c[1], inv), float.Parse(c[2], inv));
                shots.Add($"{id}_top;{p.x.ToString(inv)},{(p.y + 38f).ToString(inv)},{(p.z - 6f).ToString(inv)};{p.x.ToString(inv)},{p.y.ToString(inv)},{(p.z + 0.5f).ToString(inv)};12");
                shots.Add($"{id}_low;{(p.x + 9f).ToString(inv)},{(p.y + 3f).ToString(inv)},{(p.z - 9f).ToString(inv)};{p.x.ToString(inv)},{p.y.ToString(inv)},{p.z.ToString(inv)};12");

                var names = new SortedDictionary<string, float>();
                foreach (var col in Physics.OverlapSphere(p, 14f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (col is TerrainCollider) continue;
                    float d = Vector3.Distance(col.ClosestPoint(p), p);
                    string n = col.transform.parent ? col.transform.parent.name + "/" + col.name : col.name;
                    if (!names.TryGetValue(n, out var old) || d < old) names[n] = d;
                }
                foreach (var kv in names) report.AppendLine($"   {kv.Value,5:F1} m  {kv.Key}");
                // every road within 15 m: along it (within 30 m of the spot) the terrain and the first surface a ray meets
                var world = JsonUtility.FromJson<WorldJson>(File.ReadAllText(WorldBuilder.Root + "/Data/world.json"));
                int ri = 0;
                foreach (var r in world.roads)
                {
                    ri++;
                    var line = Geo.ToPoints(r.pts);
                    if (line.Length < 2 || Geo.DistanceToPolyline(line, new Vector2(p.x, p.z), false) > 15f) continue;
                    report.AppendLine($"   road #{ri} {r.kind} '{r.name}' w{r.width:F1} ends ({line[0].x:F0},{line[0].y:F0}) ({line[line.Length - 1].x:F0},{line[line.Length - 1].y:F0})");
                    var row = new StringBuilder("      ");
                    foreach (var q2 in WorldBuilder.Resample(line, 3f, false))
                    {
                        if ((q2 - new Vector2(p.x, p.z)).magnitude > 30f) continue;
                        var q = new Vector3(q2.x, 0, q2.y);
                        float tY = terrain ? terrain.SampleHeight(q) + terrain.transform.position.y : 0f;
                        string s = Physics.Raycast(new Vector3(q.x, tY + 20f, q.z), Vector3.down, out var hh, 40f, ~((1 << 8) | (1 << 9) | (1 << 10) | (1 << 2) | (1 << 4)), QueryTriggerInteraction.Ignore)
                            ? (hh.collider is TerrainCollider ? "T" : $"{hh.point.y - tY:+0.0;-0.0}") : "-";
                        row.Append($"[{tY:F1} {s}] ");
                    }
                    report.AppendLine(row.ToString());
                }
                // a 7 x 7 grid, 2 m apart: what a ray from above meets first, and how high the terrain is there
                report.AppendLine("   grid (dx,dz: surface - terrain, first hit):");
                for (int gz = 3; gz >= -3; gz--)
                {
                    var row = new StringBuilder("   ");
                    for (int gx = -3; gx <= 3; gx++)
                    {
                        var q = p + new Vector3(gx * 2f, 0, gz * 2f);
                        float tY = terrain ? terrain.SampleHeight(q) + terrain.transform.position.y : 0f;
                        if (Physics.Raycast(new Vector3(q.x, tY + 20f, q.z), Vector3.down, out var hit, 40f, ~((1 << 8) | (1 << 9) | (1 << 10) | (1 << 2) | (1 << 4)), QueryTriggerInteraction.Ignore))
                            row.Append(hit.collider is TerrainCollider ? "  TERR " : $"{hit.point.y - tY,6:+0.00;-0.00} ");
                        else row.Append("   -   ");
                    }
                    report.AppendLine(row.ToString());
                }
            }
            string outFile = Path.GetFullPath("Shots/bug_review.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(outFile));
            File.WriteAllText(outFile, report.ToString());
            Debug.Log("[Nainital] bug review notes -> " + outFile + "\n" + report);

            RoadCheck.Run();

            PreviewCapture.Only = new[] { "-" };
            PreviewCapture.Custom = string.Join("|", shots);
            PreviewCapture.Capture();
            PreviewCapture.Only = null;
            PreviewCapture.Custom = null;
        }
    }
}
