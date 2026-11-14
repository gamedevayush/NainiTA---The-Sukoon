using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    public static partial class WorldBuilder
    {
        /// <summary>
        /// Keeps poles and lamps off the carriageway (user, 2026-11-14, bug report from the iPhone: "some roads are having
        /// poles in mid.. check all"). Each placer stood its pole beside its own road only, so at a crossing or where two
        /// roads run close the pole landed on the other road. This answers "is this spot on any road or junction?" for every
        /// road of the network (after the corners are rounded), from a 16 m grid of road pieces.
        /// </summary>
        sealed class RoadClearance
        {
            const float Cell = 16f;
            readonly List<(Vector2 a, Vector2 b, float half)> segs = new List<(Vector2, Vector2, float)>();
            readonly List<(Vector2 c, float r)> nodes = new List<(Vector2, float)>();
            readonly Dictionary<long, List<int>> segGrid = new Dictionary<long, List<int>>();
            readonly Dictionary<long, List<int>> nodeGrid = new Dictionary<long, List<int>>();

            static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

            static void Insert(Dictionary<long, List<int>> grid, Vector2 min, Vector2 max, int idx)
            {
                int x0 = Mathf.FloorToInt(min.x / Cell), x1 = Mathf.FloorToInt(max.x / Cell);
                int z0 = Mathf.FloorToInt(min.y / Cell), z1 = Mathf.FloorToInt(max.y / Cell);
                for (int x = x0; x <= x1; x++)
                    for (int z = z0; z <= z1; z++)
                    {
                        long k = Key(x, z);
                        if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<int>();
                        l.Add(idx);
                    }
            }

            public RoadClearance(WorldCtx ctx)
            {
                foreach (var r in ctx.world.roads)
                {
                    if (NotRoads.Contains(r.kind)) continue;
                    var line = Geo.ToPoints(r.pts);
                    if (line.Length < 2) continue;
                    // Lower Mall's paving runs on to the wall below Upper Mall, wider than its OSM width on the hill side
                    float half = Mathf.Max(1.2f, r.width * 0.5f) + (r.name == MallLower ? 3.5f : r.name == MallUpper ? 1.6f : 0f);
                    for (int i = 0; i + 1 < line.Length; i++)
                    {
                        Vector2 a = line[i], b = line[i + 1];
                        segs.Add((a, b, half));
                        Vector2 pad = Vector2.one * (half + 1.5f);
                        Insert(segGrid, Vector2.Min(a, b) - pad, Vector2.Max(a, b) + pad, segs.Count - 1);
                    }
                    // the ends are where junctions are: their curved corners reach past both roads' edges
                    foreach (var e in new[] { line[0], line[line.Length - 1] })
                    {
                        float rad = half + 2.5f;
                        nodes.Add((e, rad));
                        Vector2 pad = Vector2.one * (rad + 1.5f);
                        Insert(nodeGrid, e - pad, e + pad, nodes.Count - 1);
                    }
                }
            }

            /// <summary>True when p is on a road (within its half width plus pad) or inside a junction's corner sweep.</summary>
            public bool Blocked(Vector2 p, float pad)
            {
                long k = Key(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.y / Cell));
                if (segGrid.TryGetValue(k, out var sl))
                    foreach (int i in sl)
                    {
                        var (a, b, half) = segs[i];
                        Vector2 ab = b - a;
                        float u = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                        if ((p - (a + ab * u)).sqrMagnitude < (half + pad) * (half + pad)) return true;
                    }
                if (nodeGrid.TryGetValue(k, out var nl))
                    foreach (int i in nl)
                        if ((p - nodes[i].c).sqrMagnitude < (nodes[i].r + pad) * (nodes[i].r + pad)) return true;
                return false;
            }
        }

        static RoadClearance clearance;
        static WorldCtx clearanceFor;

        static RoadClearance Clearance(WorldCtx ctx)
        {
            if (clearanceFor != ctx) { clearance = new RoadClearance(ctx); clearanceFor = ctx; }
            return clearance;
        }

        /// <summary>Where a pole beside road point p may stand: offset to the side given, or the other side when that one is
        /// on a road. False when both sides are taken (then no pole here).</summary>
        static bool PoleSpot(WorldCtx ctx, Vector2 p, Vector2 side, float offset, out Vector2 spot, out Vector2 usedSide)
        {
            var c = Clearance(ctx);
            spot = p + side * offset;
            usedSide = side;
            if (!c.Blocked(spot, 0.4f)) return true;
            spot = p - side * offset;
            usedSide = -side;
            return !c.Blocked(spot, 0.4f);
        }
    }
}
