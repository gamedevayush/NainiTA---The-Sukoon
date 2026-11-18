using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// The shortest way along the drivable roads (RoadNetwork) between two places, for vehicles that have to get
    /// somewhere instead of wandering like traffic: the police going after the player, the 108 ambulance going to an
    /// accident (user, 2026-11-18: "police aayegi phir ambulance"). Every road point is a node; it joins the points
    /// before and after it on its road, and a road's end joins the point of the road it meets (RoadNetwork.Links).
    /// Emergency vehicles may use a one-way road either way. A* with the straight-line distance as the estimate.
    /// </summary>
    public static class RoadRoute
    {
        static RoadNetwork net;
        static Vector3[] pos;        // every road point
        static int[] roadStart;      // node of each road's first point
        static int[] roadOf;         // road of each node
        static List<int>[] links;    // extra neighbours (junctions)
        static Dictionary<long, List<int>> grid;
        const float Cell = 25f;

        // A* scratch (reused; one search at a time on the main thread)
        static float[] g;
        static int[] came, seenAt, closedAt;
        static int stamp;
        static readonly List<(float f, int n)> heap = new List<(float, int)>();

        public static RoadNetwork Net => net;
        public static int Count => pos != null ? pos.Length : 0;
        public static Vector3 Pos(int node) => pos[node];

        /// <summary>Make sure the graph is built for this network (once).</summary>
        public static bool Ready(RoadNetwork n)
        {
            if (!n || n.roads == null || n.roads.Length == 0) return false;
            if (net == n && pos != null) return true;
            Build(n);
            return true;
        }

        /// <summary>The network the town's traffic uses (TrafficManager's), found once.</summary>
        public static bool ReadyAny()
        {
            if (net && pos != null) return true;
            var tm = Object.FindAnyObjectByType<TrafficManager>();
            return tm && Ready(tm.network);
        }

        static long Key(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;

        static void Build(RoadNetwork n)
        {
            net = n;
            int total = 0;
            roadStart = new int[n.roads.Length];
            for (int r = 0; r < n.roads.Length; r++) { roadStart[r] = total; total += n.roads[r].pts.Length; }
            pos = new Vector3[total];
            roadOf = new int[total];
            links = new List<int>[total];
            grid = new Dictionary<long, List<int>>();
            for (int r = 0; r < n.roads.Length; r++)
            {
                var pts = n.roads[r].pts;
                for (int i = 0; i < pts.Length; i++)
                {
                    int k = roadStart[r] + i;
                    pos[k] = pts[i];
                    roadOf[k] = r;
                    long key = Key(Mathf.FloorToInt(pts[i].x / Cell), Mathf.FloorToInt(pts[i].z / Cell));
                    if (!grid.TryGetValue(key, out var list)) grid[key] = list = new List<int>();
                    list.Add(k);
                }
            }
            for (int r = 0; r < n.roads.Length; r++)
            {
                int len = n.roads[r].pts.Length;
                for (int e = 0; e < 2; e++)
                {
                    var l = n.Links(r, e);
                    if (l == null) continue;
                    int a = roadStart[r] + (e == 0 ? 0 : len - 1);
                    foreach (var (o, idx) in l)
                    {
                        int b = roadStart[o] + Mathf.Clamp(idx, 0, n.roads[o].pts.Length - 1);
                        Join(a, b);
                    }
                }
            }
            g = new float[total];
            came = new int[total];
            seenAt = new int[total];
            closedAt = new int[total];
            stamp = 0;
        }

        static void Join(int a, int b)
        {
            if (a == b) return;
            (links[a] ??= new List<int>(2)).Add(b);
            (links[b] ??= new List<int>(2)).Add(a);
        }

        /// <summary>The road point nearest p (within maxDist on the ground plane), or -1.</summary>
        public static int Nearest(Vector3 p, float maxDist = 150f)
        {
            if (pos == null) return -1;
            int best = -1;
            float bd = maxDist * maxDist;
            int cx = Mathf.FloorToInt(p.x / Cell), cz = Mathf.FloorToInt(p.z / Cell);
            int rings = Mathf.CeilToInt(maxDist / Cell);
            for (int ring = 0; ring <= rings; ring++)
            {
                for (int dx = -ring; dx <= ring; dx++)
                    for (int dz = -ring; dz <= ring; dz++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != ring) continue;
                        if (!grid.TryGetValue(Key(cx + dx, cz + dz), out var list)) continue;
                        foreach (int k in list)
                        {
                            float ddx = pos[k].x - p.x, ddz = pos[k].z - p.z;
                            float d = ddx * ddx + ddz * ddz + (pos[k].y - p.y) * (pos[k].y - p.y) * 0.25f;   // a road far above / below counts less
                            if (d < bd) { bd = d; best = k; }
                        }
                    }
                if (best >= 0 && (ring + 0.5f) * Cell > Mathf.Sqrt(bd) + Cell) break;   // nothing nearer can be further out
            }
            return best;
        }

        static void Push(float f, int n)
        {
            heap.Add((f, n));
            int i = heap.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (heap[p].f <= heap[i].f) break;
                (heap[p], heap[i]) = (heap[i], heap[p]);
                i = p;
            }
        }

        static int Pop()
        {
            int top = heap[0].n;
            int last = heap.Count - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, m = i;
                if (l < heap.Count && heap[l].f < heap[m].f) m = l;
                if (r < heap.Count && heap[r].f < heap[m].f) m = r;
                if (m == i) break;
                (heap[m], heap[i]) = (heap[i], heap[m]);
                i = m;
            }
            return top;
        }

        static float H(int a, int b) { float dx = pos[a].x - pos[b].x, dz = pos[a].z - pos[b].z; return Mathf.Sqrt(dx * dx + dz * dz); }

        /// <summary>Road points from the one nearest `from` to the one nearest `to` (inclusive) into path. False if the two are
        /// not joined (or the search gave up after maxExpand points).</summary>
        public static bool Find(Vector3 from, Vector3 to, List<Vector3> path, int maxExpand = 9000)
        {
            path.Clear();
            int s = Nearest(from), t = Nearest(to, 400f);
            if (s < 0 || t < 0) return false;
            return Find(s, t, path, maxExpand);
        }

        public static bool Find(int s, int t, List<Vector3> path, int maxExpand = 9000)
        {
            path.Clear();
            if (pos == null || s < 0 || t < 0) return false;
            if (s == t) { path.Add(pos[s]); return true; }
            stamp++;
            if (stamp == int.MaxValue) { System.Array.Clear(seenAt, 0, seenAt.Length); System.Array.Clear(closedAt, 0, closedAt.Length); stamp = 1; }
            heap.Clear();
            g[s] = 0f;
            came[s] = -1;
            seenAt[s] = stamp;
            Push(H(s, t), s);
            int expanded = 0;
            bool found = false;
            while (heap.Count > 0)
            {
                int c = Pop();
                if (closedAt[c] == stamp) continue;
                closedAt[c] = stamp;
                if (c == t) { found = true; break; }
                if (++expanded > maxExpand) break;
                int r = roadOf[c];
                int first = roadStart[r], last = first + net.roads[r].pts.Length - 1;
                if (c > first) Relax(c, c - 1, t);
                if (c < last) Relax(c, c + 1, t);
                var l = links[c];
                if (l != null) for (int i = 0; i < l.Count; i++) Relax(c, l[i], t);
            }
            if (!found) return false;
            for (int k = t; k >= 0; k = came[k]) path.Add(pos[k]);
            path.Reverse();
            return true;
        }

        static void Relax(int c, int nb, int t)
        {
            if (closedAt[nb] == stamp) return;
            float ng = g[c] + Vector3.Distance(pos[c], pos[nb]);
            if (seenAt[nb] == stamp && ng >= g[nb]) return;
            seenAt[nb] = stamp;
            g[nb] = ng;
            came[nb] = c;
            Push(ng + H(nb, t), nb);
        }

        /// <summary>A road point between minD and maxD from c, out of the camera's view (where a vehicle can appear
        /// without popping in), or -1.</summary>
        public static int SpawnNode(Vector3 c, float minD, float maxD, Camera cam, int tries = 40)
        {
            if (pos == null) return -1;
            for (int i = 0; i < tries; i++)
            {
                float a = Random.value * Mathf.PI * 2f, d = Random.Range(minD, maxD);
                int k = Nearest(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * d, 60f);
                if (k < 0) continue;
                float dd = Vector3.Distance(pos[k], c);
                if (dd < minD * 0.8f || dd > maxD * 1.2f) continue;
                if (cam)
                {
                    Vector3 vp = cam.WorldToViewportPoint(pos[k]);
                    if (vp.z > 0f && vp.z < 160f && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f) continue;
                }
                return k;
            }
            return -1;
        }

        /// <summary>The direction of the road at a node (towards the next point of its road).</summary>
        public static Vector3 Heading(int k)
        {
            int r = roadOf[k];
            int first = roadStart[r], last = first + net.roads[r].pts.Length - 1;
            if (first == last) return Vector3.forward;
            Vector3 d = k < last ? pos[k + 1] - pos[k] : pos[k] - pos[k - 1];
            d.y = 0f;
            return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward;
        }

        /// <summary>The road's width at a node.</summary>
        public static float Width(int k) => net.roads[roadOf[k]].width;
    }
}
