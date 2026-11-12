using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    public static partial class WorldBuilder
    {
        /// <summary>
        /// Sharp corners off the roads (user, 2026-11-12: "kuch roads pe ajeeb sa turn hai, V shaped ya ekdam se; real
        /// roads aisi nahi hain, mostly max U shaped"). OpenStreetMap draws a bend as a few straight pieces meeting at a
        /// point; a real hill road sweeps round. Every corner of more than 18 degrees is replaced by a curve (a cubic
        /// Bezier from a point on the way in to a point on the way out, its handles toward the corner), so a bend is round
        /// and a hairpin is a U. The ends of a road and any point another road shares (a junction) stay exactly where
        /// they are, so the network still joins up; Mall Road's decks keep their surveyed lines (the Mall builder fits its
        /// walls and railings to them). Runs first: everything after reads the rounded roads.
        /// </summary>
        static void SmoothRoadCorners(WorldCtx ctx)
        {
            // points shared by two or more roads (junctions), on a 0.5 m grid
            var shared = new Dictionary<long, int>();
            long Key(float x, float z) => ((long)Mathf.RoundToInt(x * 2f) << 32) ^ (uint)Mathf.RoundToInt(z * 2f);
            foreach (var r in ctx.world.roads)
                for (int i = 0; i + 1 < r.pts.Length; i += 2)
                {
                    long k = Key(r.pts[i], r.pts[i + 1]);
                    shared[k] = shared.TryGetValue(k, out var c) ? c + 1 : 1;
                }

            int corners = 0, roads = 0;
            foreach (var r in ctx.world.roads)
            {
                if (IsMallDeck(r.name) || r.name == MallUpper || r.name == MallLower) continue;
                if (r.kind == "steps") continue;
                var p = Geo.ToPoints(r.pts);
                if (p.Length < 3) continue;
                var outPts = new List<Vector2> { p[0] };
                bool any = false;
                for (int i = 1; i < p.Length - 1; i++)
                {
                    Vector2 a = p[i - 1], b = p[i], c = p[i + 1];
                    Vector2 inD = b - a, outD = c - b;
                    float lin = inD.magnitude, lout = outD.magnitude;
                    float turn = lin > 0.05f && lout > 0.05f ? Vector2.Angle(inD, outD) : 0f;
                    bool junction = shared.TryGetValue(Key(b.x, b.y), out var cnt) && cnt > 1;
                    if (turn < 18f || junction) { outPts.Add(b); continue; }
                    // how far back along each leg the curve starts: a sweeping radius for a road of this kind, never past
                    // the middle of a leg (the next corner needs the other half)
                    bool major = r.kind == "primary" || r.kind == "secondary" || r.kind == "trunk" || r.kind == "tertiary" || r.kind == "trunk_link";
                    float radius = major ? 16f : r.kind == "path" || r.kind == "footway" || r.kind == "pedestrian" || r.kind == "track" ? 5f : 10f;
                    float half = turn * 0.5f * Mathf.Deg2Rad;
                    float t = radius * Mathf.Tan(Mathf.Min(half, 1.2f));
                    t = Mathf.Min(t, lin * 0.48f, lout * 0.48f, 22f);
                    if (t < 0.6f) { outPts.Add(b); continue; }
                    Vector2 p0 = b - inD / lin * t, p3 = b + outD / lout * t;
                    float k = turn > 120f ? 1.3f : turn > 85f ? 0.95f : 0.62f;   // hairpins: a fuller U (the tip swings a little past the corner)
                    Vector2 c1 = p0 + (b - p0) * k, c2 = p3 + (b - p3) * k;
                    int seg = Mathf.Clamp(Mathf.CeilToInt(turn / 10f), 3, 18);
                    for (int s = 0; s <= seg; s++)
                    {
                        float u = s / (float)seg, v = 1f - u;
                        outPts.Add(v * v * v * p0 + 3f * v * v * u * c1 + 3f * v * u * u * c2 + u * u * u * p3);
                    }
                    corners++;
                    any = true;
                }
                outPts.Add(p[p.Length - 1]);
                if (!any) continue;
                var f = new float[outPts.Count * 2];
                for (int i = 0; i < outPts.Count; i++) { f[i * 2] = outPts[i].x; f[i * 2 + 1] = outPts[i].y; }
                r.pts = f;
                roads++;
            }
            Debug.Log($"[Nainital] road corners rounded: {corners} corners on {roads} roads");
        }
    }
}
