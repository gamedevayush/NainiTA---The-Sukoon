using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>Road junctions as one surface with curved corners. Each road is its own ribbon, so where roads met the
    /// ribbons simply overlapped with hard corners; real junctions sweep from one kerb to the next. Here every place
    /// where roads meet gets a patch: the arms' edges joined by Bezier fillets, laid just above the ribbons.</summary>
    public static partial class WorldBuilder
    {
        sealed class Arm
        {
            public Vector2 dir;   // away from the junction
            public float width;
            public RoadData road;
        }

        static void BuildJunctions(WorldCtx ctx)
        {
            var parent = new GameObject("Road junctions").transform;
            parent.SetParent(ctx.worldRoot, false);
            var roads = new List<(RoadData r, Vector2[] line)>();
            foreach (var r in ctx.world.roads)
            {
                if (NotRoads.Contains(r.kind)) continue;
                var line = Geo.ToPoints(r.pts);
                if (line.Length >= 2) roads.Add((r, line));
            }

            // 1. candidate nodes: every road end, merged when close
            var nodes = new List<Vector2>();
            foreach (var (r, line) in roads)
                foreach (var e in new[] { line[0], line[line.Length - 1] })
                {
                    bool merged = false;
                    for (int i = 0; i < nodes.Count; i++)
                        if ((nodes[i] - e).sqrMagnitude < 36f) { nodes[i] = (nodes[i] + e) * 0.5f; merged = true; break; }
                    if (!merged) nodes.Add(e);
                }

            var meshes = new Dictionary<Material, MeshBuilder>();
            int made = 0;
            foreach (var J in nodes)
            {
                // 2. the arms leaving this node: ends of roads here, and roads passing through (two arms each)
                var arms = new List<Arm>();
                foreach (var (r, line) in roads)
                {
                    float d = ClosestOnLine(line, J, out int seg, out float t, out Vector2 q);
                    if (d > r.width * 0.5f + 3f) continue;
                    float len = LineLength(line), at = Distance(line, seg, t);
                    if (at > 4f) arms.Add(new Arm { dir = (PointAt(line, Mathf.Max(0f, at - 10f)) - q).normalized, width = r.width, road = r });
                    if (len - at > 4f) arms.Add(new Arm { dir = (PointAt(line, Mathf.Min(len, at + 10f)) - q).normalized, width = r.width, road = r });
                }
                if (arms.Count < 2) continue;
                // two arms that just continue each other in a straight line need no patch
                if (arms.Count == 2 && Vector2.Dot(arms[0].dir, arms[1].dir) < -0.94f) continue;
                arms.Sort((a, b) => Mathf.Atan2(a.dir.y, a.dir.x).CompareTo(Mathf.Atan2(b.dir.y, b.dir.x)));

                float wMax = 0f, minAng = 180f;
                foreach (var a in arms) wMax = Mathf.Max(wMax, a.width);
                for (int k = 0; k < arms.Count; k++)
                    minAng = Mathf.Min(minAng, Vector2.Angle(arms[k].dir, arms[(k + 1) % arms.Count].dir));
                float D = Mathf.Clamp(wMax * 0.5f / Mathf.Tan(Mathf.Max(minAng, 28f) * 0.5f * Mathf.Deg2Rad) + 1.2f, wMax * 0.7f, 13f);

                // 3. rim: for each arm its right then left edge at distance D, then a curved fillet to the next arm
                var rim = new List<Vector2>();
                for (int k = 0; k < arms.Count; k++)
                {
                    var a = arms[k];
                    var b = arms[(k + 1) % arms.Count];
                    Vector2 na = new Vector2(-a.dir.y, a.dir.x), nb = new Vector2(-b.dir.y, b.dir.x);
                    Vector2 ra = J + a.dir * D - na * a.width * 0.5f, la = J + a.dir * D + na * a.width * 0.5f;
                    Vector2 rb = J + b.dir * D - nb * b.width * 0.5f;
                    rim.Add(ra);
                    rim.Add(la);
                    // control point: where a's left kerb line meets b's right kerb line (if sensible)
                    Vector2 ctrl;
                    float den = a.dir.x * b.dir.y - a.dir.y * b.dir.x;
                    if (Mathf.Abs(den) > 0.15f)
                    {
                        Vector2 w0 = rb - la;
                        float s = (w0.x * b.dir.y - w0.y * b.dir.x) / den;
                        ctrl = la + a.dir * s;
                        if ((ctrl - J).magnitude > D * 1.6f || s > 0f) ctrl = J + (na * a.width * 0.5f - nb * b.width * 0.5f) * 0.5f;
                    }
                    else ctrl = J + (la + rb - 2f * J) * 0.25f;
                    for (int i = 1; i < 8; i++)
                    {
                        float u = i / 8f;
                        rim.Add((1 - u) * (1 - u) * la + 2f * (1 - u) * u * ctrl + u * u * rb);
                    }
                }

                // 4. material of the main road here
                Material mat = ctx.mats.asphalt;
                bool lake = false, allWalk = true;
                foreach (var a in arms)
                {
                    if (IsLakeRoad(ctx, a.road)) lake = true;
                    if (a.road.kind != "pedestrian" && a.road.kind != "living_street") allWalk = false;
                }
                if (lake) mat = ConcreteMaterial(ctx);
                else if (allWalk) mat = PhotoMaterial("cobblestone_floor_08", "M_MallCobblePhoto", new Color(0.92f, 0.92f, 0.9f), 0.45f) ?? ctx.mats.paver;
                if (!meshes.TryGetValue(mat, out var mb)) meshes[mat] = mb = new MeshBuilder();

                // 5. fan from the centre in three rings, each vertex on the ground just above the ribbons
                float Y(Vector2 p) => (ctx.InLake(p) ? ctx.lakeY + 0.3f : ctx.H(p)) + RoadLift + 0.045f;   // just over the road slabs it joins
                for (int i = 0; i < rim.Count; i++)
                {
                    Vector2 p0 = rim[i], p1 = rim[(i + 1) % rim.Count];
                    Vector2 prev0 = J, prev1 = J;
                    for (int ring = 1; ring <= 3; ring++)
                    {
                        float f = ring / 3f;
                        Vector2 c0 = Vector2.Lerp(J, p0, f), c1 = Vector2.Lerp(J, p1, f);
                        Vector2 U(Vector2 p) => p / 6f;   // planar, so the texture runs straight across the whole junction
                        if (ring == 1) mb.Tri(V(J, Y(J)), V(c1, Y(c1)), V(c0, Y(c0)), U(J), U(c1), U(c0), Vector3.up);
                        else
                        {
                            mb.Tri(V(prev0, Y(prev0)), V(c0, Y(c0)), V(c1, Y(c1)), U(prev0), U(c0), U(c1), Vector3.up);
                            mb.Tri(V(prev0, Y(prev0)), V(c1, Y(c1)), V(prev1, Y(prev1)), U(prev0), U(c1), U(prev1), Vector3.up);
                        }
                        prev0 = c0;
                        prev1 = c1;
                    }
                }
                noGrassExtra.Add((new[] { J - Vector2.right * D, J + Vector2.right * D }, D));
                made++;
            }
            foreach (var kv in meshes)
            {
                for (int i = 0; i < kv.Value.N.Count; i++) kv.Value.N[i] = Vector3.up;   // one smooth surface, no fan-shaped facets
                ctx.MeshObject("Junctions " + kv.Key.name, parent, kv.Value, kv.Key, null, false, true);
            }
            Debug.Log($"[Nainital] junctions: {made} with curved corners");
        }

        // ---- polyline helpers
        static float LineLength(Vector2[] l) { float s = 0f; for (int i = 0; i + 1 < l.Length; i++) s += Vector2.Distance(l[i], l[i + 1]); return s; }

        static float Distance(Vector2[] l, int seg, float t)
        {
            float s = 0f;
            for (int i = 0; i < seg; i++) s += Vector2.Distance(l[i], l[i + 1]);
            return s + Vector2.Distance(l[seg], l[seg + 1]) * t;
        }

        static Vector2 PointAt(Vector2[] l, float dist)
        {
            for (int i = 0; i + 1 < l.Length; i++)
            {
                float d = Vector2.Distance(l[i], l[i + 1]);
                if (dist <= d) return Vector2.Lerp(l[i], l[i + 1], d > 1e-4f ? dist / d : 0f);
                dist -= d;
            }
            return l[l.Length - 1];
        }

        static float ClosestOnLine(Vector2[] l, Vector2 p, out int seg, out float t, out Vector2 q)
        {
            float best = float.MaxValue;
            seg = 0; t = 0f; q = l[0];
            for (int i = 0; i + 1 < l.Length; i++)
            {
                Vector2 a = l[i], ab = l[i + 1] - a;
                float u = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                Vector2 c = a + ab * u;
                float d = (p - c).sqrMagnitude;
                if (d < best) { best = d; seg = i; t = u; q = c; }
            }
            return Mathf.Sqrt(best);
        }
    }
}
