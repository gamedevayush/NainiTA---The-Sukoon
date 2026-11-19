using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Numbers for the Mall roads (user, 2026-11-19: "terrain ubad khabad near toll plaza ... kisi ghar me na ghuse ...
    /// poori mall road"): every 0.5 m along each road named *Mall*, a ray straight down on the centre line and 0.5 m in
    /// from each edge. Reports bumps (the height's second difference over 1 m), steps between neighbouring samples, the
    /// road tipping sideways, and anything other than the ground standing on the carriageway (the collider hit first).
    /// Batch: call MallProbe.Run  -> [Probe] lines in the log and Logs/mall_probe.txt
    /// </summary>
    public static class MallProbe
    {
        public static void Run()
        {
            if (Terrain.activeTerrains.Length == 0) EditorSceneManager.OpenScene("Assets/Nainital/Scenes/Nainital.unity");
            Physics.SyncTransforms();
            var w = JsonUtility.FromJson<WorldJson>(File.ReadAllText(Path.Combine(Application.dataPath, "Nainital/Data/world.json")));
            var outLines = new List<string>();
            var issues = new List<(float sev, string line)>();
            var blockers = new Dictionary<string, int>();
            foreach (var r in w.roads.Where(r => (r.name ?? "").Contains("Mall")))
            {
                var pts = new List<Vector2>();
                for (int i = 0; i + 1 < r.pts.Length; i += 2) pts.Add(new Vector2(r.pts[i], r.pts[i + 1]));
                // resample at 0.5 m
                var s = new List<(Vector2 p, Vector2 n, float d)>();
                float along = 0f;
                for (int i = 0; i + 1 < pts.Count; i++)
                {
                    Vector2 a = pts[i], b = pts[i + 1], dir = (b - a);
                    float len = dir.magnitude;
                    if (len < 1e-3f) continue;
                    dir /= len;
                    Vector2 n = new Vector2(-dir.y, dir.x);
                    for (float t = 0f; t < len; t += 0.5f) s.Add((a + dir * t, n, along + t));
                    along += len;
                }
                float half = Mathf.Max(0.5f, r.width * 0.5f - 0.5f);
                var hc = new float[s.Count];
                for (int i = 0; i < s.Count; i++)
                {
                    var (p, n, d) = s[i];
                    hc[i] = Ground(p, out string hitC);
                    float hl = Ground(p + n * half, out string hitL), hr = Ground(p - n * half, out string hitR);
                    foreach (var h in new[] { hitC, hitL, hitR })
                        if (h != null) { blockers.TryGetValue(h, out int k); blockers[h] = k + 1; if (k < 2) issues.Add((5f, $"{r.name} @{d:F0} m ({p.x:F1}, {p.y:F1}): {h} on the carriageway")); }
                    float tilt = Mathf.Abs(hl - hr) / (2f * half);
                    if (tilt > 0.12f) issues.Add((tilt * 10f, $"{r.name} @{d:F0} m ({p.x:F1}, {p.y:F1}): sideways slope {tilt * 100f:F0}% (left {hl:F2}, right {hr:F2})"));
                }
                for (int i = 1; i < s.Count; i++)
                {
                    float step = Mathf.Abs(hc[i] - hc[i - 1]);
                    if (step > 0.22f) issues.Add((step * 8f, $"{r.name} @{s[i].d:F0} m ({s[i].p.x:F1}, {s[i].p.y:F1}): step {step:F2} m in 0.5 m"));
                }
                for (int i = 2; i + 2 < s.Count; i += 1)
                {
                    float d2 = hc[i + 2] - 2f * hc[i] + hc[i - 2];   // over 1 m each side
                    if (Mathf.Abs(d2) > 0.12f) issues.Add((Mathf.Abs(d2) * 10f, $"{r.name} @{s[i].d:F0} m ({s[i].p.x:F1}, {s[i].p.y:F1}): bump {d2:+0.00;-0.00} m"));
                }
                outLines.Add($"[Probe] {r.name}: {along:F0} m, {s.Count} samples, height {hc.Min():F1}..{hc.Max():F1}");
            }
            // close-together issues collapse to the worst one per 4 m
            var kept = new List<(float sev, string line)>();
            foreach (var it in issues.OrderByDescending(i => i.sev))
            {
                string key = it.line.Substring(0, it.line.IndexOf(')') + 1);
                if (kept.Count < 60 && !kept.Any(k => Near(k.line, it.line, 4f))) kept.Add(it);
            }
            outLines.Add($"[Probe] issues {issues.Count} (worst {kept.Count} listed), things hit on the carriageway: " +
                         string.Join(", ", blockers.OrderByDescending(b => b.Value).Take(15).Select(b => b.Key + " x" + b.Value)));
            outLines.AddRange(kept.Select(k => "[Probe]   " + k.line));
            foreach (var l in outLines) Debug.Log(l);
            File.WriteAllLines(Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/mall_probe.txt")), outLines);
        }

        /// <summary>Cross-sections at the worst spots of the last Run (Logs/mall_probe.txt): across the road from -5 to +5 m,
        /// the terrain height and what a ray from above hits first (name, height). Batch: call MallProbe.Sections</summary>
        public static void Sections()
        {
            if (Terrain.activeTerrains.Length == 0) EditorSceneManager.OpenScene("Assets/Nainital/Scenes/Nainital.unity");
            Physics.SyncTransforms();
            var w = JsonUtility.FromJson<WorldJson>(File.ReadAllText(Path.Combine(Application.dataPath, "Nainital/Data/world.json")));
            var spots = new (string road, float x, float z)[]
            {
                ("Upper Mall Road", 661.8f, -370.9f), ("Upper Mall Road", 708.3f, -468.3f), ("Upper Mall Road", 687.4f, -424.8f),
                ("Upper Mall Road", 874.7f, -985.2f), ("Upper Mall Road", 866.9f, -964.7f), ("Upper Mall Road", 854.0f, -929.1f),
                ("Lower Mall Road", 406.2f, -91.3f), ("Lower Mall Road", 496.5f, -197.5f), ("Lower Mall Road", 731.6f, -550.2f),
            };
            var lines = new List<string>();
            foreach (var (road, x, z) in spots)
            {
                var r = w.roads.First(q => q.name == road);
                Vector2 p = new Vector2(x, z), best = Vector2.zero, dir = Vector2.right;
                float bd = 1e9f;
                for (int i = 0; i + 3 < r.pts.Length; i += 2)
                {
                    Vector2 a = new Vector2(r.pts[i], r.pts[i + 1]), b = new Vector2(r.pts[i + 2], r.pts[i + 3]);
                    Vector2 ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                    Vector2 c = a + ab * t; float d = (p - c).magnitude;
                    if (d < bd) { bd = d; best = c; dir = ab.normalized; }
                }
                Vector2 n = new Vector2(-dir.y, dir.x);
                lines.Add($"[Probe] {road} at ({x:F1}, {z:F1}), width {r.width}, across (n = {n.x:F2}, {n.y:F2}):");
                for (float o = -5f; o <= 5.01f; o += 1f)
                {
                    Vector2 q = best + n * o;
                    float terr = float.NaN;
                    foreach (var t in Terrain.activeTerrains)
                    {
                        var tp = t.GetPosition(); var sz = t.terrainData.size;
                        if (q.x >= tp.x && q.x <= tp.x + sz.x && q.y >= tp.z && q.y <= tp.z + sz.z) { terr = t.SampleHeight(new Vector3(q.x, 0f, q.y)) + tp.y; break; }
                    }
                    var hits = Physics.RaycastAll(new Vector3(q.x, terr + 40f, q.y), Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance).Take(3)
                        .Select(h => $"{h.collider.name}@{h.point.y:F2}");
                    lines.Add($"[Probe]   {o,5:+0;-0;0} m  terrain {terr:F2}  hits {string.Join(" | ", hits)}");
                }
                // along the road: centre-line terrain every 1 m, 8 m each way
                var along = new List<string>();
                for (float s = -8f; s <= 8.01f; s += 1f)
                {
                    Vector2 q = best + dir * s;
                    foreach (var t in Terrain.activeTerrains)
                    {
                        var tp = t.GetPosition(); var sz = t.terrainData.size;
                        if (q.x >= tp.x && q.x <= tp.x + sz.x && q.y >= tp.z && q.y <= tp.z + sz.z) { along.Add((t.SampleHeight(new Vector3(q.x, 0f, q.y)) + tp.y).ToString("F2")); break; }
                    }
                }
                lines.Add("[Probe]   along -8..+8 m: " + string.Join(" ", along));
            }
            foreach (var l in lines) Debug.Log(l);
            File.WriteAllLines(Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/mall_sections.txt")), lines);
        }

        static bool Near(string a, string b, float m)
        {
            Vector2 P(string s) { int i = s.IndexOf('(') + 1, j = s.IndexOf(')'); var f = s.Substring(i, j - i).Split(','); return new Vector2(float.Parse(f[0]), float.Parse(f[1])); }
            try { return (P(a) - P(b)).magnitude < m; } catch { return false; }
        }

        /// <summary>The height under (x, z): the first thing a ray from 60 m up hits. blocker = its name when that is not
        /// the ground (terrain, a road deck, a kerb or a pavement) and stands more than 1.2 m over the terrain.</summary>
        static float Ground(Vector2 p, out string blocker)
        {
            blocker = null;
            float terr = float.NegativeInfinity;
            foreach (var t in Terrain.activeTerrains)
            {
                var tp = t.GetPosition(); var sz = t.terrainData.size;
                if (p.x >= tp.x && p.x <= tp.x + sz.x && p.y >= tp.z && p.y <= tp.z + sz.z) { terr = t.SampleHeight(new Vector3(p.x, 0f, p.y)) + tp.y; break; }
            }
            var hits = Physics.RaycastAll(new Vector3(p.x, (float.IsInfinity(terr) ? 3000f : terr) + 60f, p.y), Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0) return terr;
            var top = hits.OrderBy(h => h.distance).First();
            string n = top.collider.name;
            bool ground = top.collider is TerrainCollider || n.Contains("Road") || n.Contains("Deck") || n.Contains("Kerb") || n.Contains("Pave") || n.Contains("Ground") || n.Contains("Bridge");
            if (!ground && !float.IsInfinity(terr) && top.point.y > terr + 1.2f) blocker = n;
            return ground || float.IsInfinity(terr) ? top.point.y : terr;
        }
    }
}
