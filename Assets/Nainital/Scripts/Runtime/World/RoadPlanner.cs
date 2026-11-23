using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Routes over the road network, in the game (it was the editor's TrafficLab.Plan; the menu's bus ride needs it too:
    /// BusTour). Dijkstra over (road, way, entry point), moving only the way a one-way road allows, from a road's end onto
    /// the roads RoadChoice.Onward offers there, or off its middle onto a branch (RoadNetwork.Branches).
    /// </summary>
    public static class RoadPlanner
    {
        /// <summary>Metres along a road's points from one to another.</summary>
        public static float Run(Vector3[] q, int from, int to)
        {
            float s = 0f;
            int st = to >= from ? 1 : -1;
            for (int k = from; k != to; k += st) s += Vector3.Distance(q[k], q[k + st]);
            return s;
        }

        /// <summary>The roads to drive (road, way) from (sr, si) to (gr, gi); startDir: the way it must set off (0 = either:
        /// a place on the way is not a place to turn round). Null: no way there.</summary>
        public static List<(int r, int d)> Plan(RoadNetwork net, int sr, int si, int gr, int gi, int startDir = 0)
        {
            var keys = PlanEntries(net, sr, si, gr, gi, startDir);
            if (keys == null) return null;
            var steps = new List<(int r, int d)>(keys.Count);
            foreach (var k in keys) steps.Add((k.r, k.d));
            return steps;
        }

        /// <summary>As Plan, with the point each road is entered at (the first: si) - BusService draws a route's line from
        /// them (24 Nov 2026).</summary>
        public static List<(int r, int d, int i)> PlanEntries(RoadNetwork net, int sr, int si, int gr, int gi, int startDir = 0)
        {
            var dist = new Dictionary<(int r, int d, int i), float>();
            var from = new Dictionary<(int r, int d, int i), (int r, int d, int i)>();
            var open = new List<((int r, int d, int i) k, float c)>();
            foreach (int d in new[] { 1, -1 })
            {
                int ow = net.roads[sr].oneWay;
                if (ow != 0 && d != ow) continue;
                if (startDir != 0 && d != startDir) continue;
                var k = (sr, d, si);
                dist[k] = 0f;
                open.Add((k, 0f));
            }
            (int r, int d, int i) goalKey = (-1, 0, 0);
            int guard = 0;
            var off = new List<(int r, int i, int d, int at)>();
            while (open.Count > 0 && guard++ < 20000)
            {
                int bi = 0;
                for (int k = 1; k < open.Count; k++) if (open[k].c < open[bi].c) bi = k;
                var (cur, c) = open[bi];
                open.RemoveAt(bi);
                if (dist.TryGetValue(cur, out var known) && known < c - 1e-3f) continue;
                var q = net.roads[cur.r].pts;
                if (cur.r == gr && (gi - cur.i) * cur.d >= 0) { goalKey = cur; break; }
                int end = cur.d > 0 ? q.Length - 1 : 0;
                net.Branches(cur.r, cur.i, cur.d, off);
                foreach (var b in off)
                {
                    if ((b.at - cur.i) * cur.d < 0) continue;
                    var nk = (b.r, b.d, b.i);
                    float nc = c + Run(q, cur.i, b.at) + 2f;
                    if (dist.TryGetValue(nk, out var old) && old <= nc) continue;
                    dist[nk] = nc;
                    from[nk] = cur;
                    open.Add((nk, nc));
                }
                float c2 = c + Run(q, cur.i, end);
                foreach (var o in RoadChoice.Onward(net, cur.r, end, true))
                {
                    var nk = (o.r, o.d, o.i);
                    float nc = c2 + 2f;
                    if (dist.TryGetValue(nk, out var old) && old <= nc) continue;
                    dist[nk] = nc;
                    from[nk] = cur;
                    open.Add((nk, nc));
                }
            }
            if (goalKey.r < 0) return null;
            var keys = new List<(int r, int d, int i)>();
            var at = goalKey;
            keys.Add(at);
            while (from.TryGetValue(at, out var prev)) { at = prev; keys.Add(at); }
            keys.Reverse();
            return keys;
        }
    }
}
