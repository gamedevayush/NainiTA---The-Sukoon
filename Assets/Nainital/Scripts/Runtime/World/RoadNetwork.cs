using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>Baked road + footpath polylines (from OSM) used by AI traffic and pedestrians.</summary>
    public class RoadNetwork : ScriptableObject
    {
        [Serializable]
        public class Path
        {
            public string name;
            public float width;
            public Vector3[] pts;
            public int oneWay;   // 0 both ways, +1 only along pts, -1 only against
            /// <summary>Metres to the driver's left to add to the lane at each point, driving along pts (Fwd) / against
            /// (Back): round what stands in that lane (WorldBuilder.ClearLanes). Empty: none.</summary>
            public float[] shiftFwd, shiftBack;

            public float Shift(int dir, int i)
            {
                var s = dir > 0 ? shiftFwd : shiftBack;
                return s != null && i >= 0 && i < s.Length ? s[i] : 0f;
            }
        }

        public Path[] roads;   // drivable
        public Path[] walks;   // sidewalks, footpaths, promenade

        /// <summary>A road's end meeting another road, as the map has it (WorldBuilder.RoadJoins).</summary>
        [Serializable]
        public struct Meeting { public int road, end, other, idx; }
        public Meeting[] meetings;

        /// <summary>The roads that start from the middle of `road` still ahead of point `idx`, driving `dir`: the road,
        /// the point they are entered at, the way along them, and the point of `road` to turn off at. (At its two ends:
        /// Links.)</summary>
        public void Branches(int road, int idx, int dir, List<(int r, int i, int d, int at)> into)
        {
            into.Clear();
            if (meetings == null) return;
            int last = roads[road].pts.Length - 1;
            foreach (var m in meetings)
            {
                if (m.other != road || m.idx <= 1 || m.idx >= last - 1) continue;
                if ((m.idx - idx) * dir < -6) continue;   // well behind (a few points: it may be just past it)
                int d = m.end == 0 ? 1 : -1, ow = roads[m.road].oneWay;
                if (ow != 0 && ow != d) continue;
                into.Add((m.road, m.end == 0 ? 0 : roads[m.road].pts.Length - 1, d, m.idx));
            }
        }

        /// <summary>A thing standing in the middle of a junction that traffic drives round, keeping it on the driver's
        /// right (the Tallital police island).</summary>
        [Serializable]
        public struct Island { public Vector3 centre; public float radius; }
        public Island[] islands;

        const float IslandRoom = 0.5f;

        public bool IslandNear(Vector3 p, float beyond, out Island found)
        {
            found = default;
            if (islands == null) return false;
            foreach (var s in islands)
            {
                float dx = p.x - s.centre.x, dz = p.z - s.centre.z, r = s.radius + beyond;
                if (dx * dx + dz * dz < r * r) { found = s; return true; }
            }
            return false;
        }

        public bool NearIsland(Vector3 p, float beyond)
        {
            if (islands == null) return false;
            foreach (var s in islands)
            {
                float dx = p.x - s.centre.x, dz = p.z - s.centre.z, r = s.radius + beyond;
                if (dx * dx + dz * dz < r * r) return true;
            }
            return false;
        }

        /// <summary>A lane point inside an island the vehicle is already at: it counts as passed (nobody can reach it).</summary>
        public bool InIsland(Vector3 point, Vector3 pos, float halfWidth)
        {
            if (islands == null) return false;
            foreach (var s in islands)
            {
                float r = s.radius + halfWidth + IslandRoom;
                Vector2 c = new Vector2(s.centre.x, s.centre.z);
                if ((new Vector2(point.x, point.z) - c).magnitude < r && (new Vector2(pos.x, pos.z) - c).magnitude < r + 4f) return true;
            }
            return false;
        }

        /// <summary>Where to steer for to reach `aim` without crossing an island: round the side that way already passes
        /// it on (a left turn and going straight on keep it on the right; a right turn at the T takes the near side -
        /// "always clockwise" was 260 degrees round through two 7.4 m pinches, and it flipped sides mid-turn).</summary>
        public Vector3 RoundIslands(Vector3 pos, Vector3 aim, float halfWidth)
        {
            if (islands == null) return aim;
            foreach (var s in islands)
            {
                float r = s.radius + halfWidth + IslandRoom;
                Vector3 rel = pos - s.centre, seg = aim - pos;
                rel.y = seg.y = 0f;
                float d = rel.magnitude, len = seg.magnitude;
                if (d < 0.01f || len < 0.01f || d > r + 25f) continue;
                Vector3 way = seg / len;
                if (Vector3.Dot(way, rel / d) > 0.5f) continue;   // driving away from it
                float t = Mathf.Clamp(Vector3.Dot(-rel, way), 0f, Mathf.Max(len, 10f));
                if (d >= r && (rel + way * t).magnitude >= r) continue;
                Vector3 to;
                float side = Vector3.Dot(Vector3.Cross(Vector3.up, way), -rel) >= 0f ? 1f : -1f;   // +1: the island on the right
                if (d < r + 0.5f) to = s.centre + Quaternion.AngleAxis(35f * side, Vector3.up) * (rel / d) * (r + 0.4f);
                else
                {
                    Vector3 inward = -rel / d, left = Vector3.Cross(inward, Vector3.up);
                    float a = Mathf.Acos(r / d);
                    to = s.centre + r * (-inward * Mathf.Cos(a) + left * (side * Mathf.Sin(a)));
                }
                to.y = aim.y;
                return to;
            }
            return aim;
        }

        // ---- runtime helpers
        [NonSerialized] List<(int road, int idx)>[] endLinks;

        /// <summary>Roads that connect near the given end (0 = start, 1 = end) of a road.</summary>
        public List<(int road, int idx)> Links(int road, int end)
        {
            if (endLinks == null) BuildLinks();
            return endLinks[road * 2 + end];
        }

        void BuildLinks()
        {
            if (meetings != null && meetings.Length > 0)   // the map's own junctions (nearness joined roads across walls, and missed some)
            {
                endLinks = new List<(int, int)>[roads.Length * 2];
                for (int k = 0; k < endLinks.Length; k++) endLinks[k] = new List<(int, int)>();
                foreach (var m in meetings) endLinks[m.road * 2 + m.end].Add((m.other, m.idx));
                return;
            }
            // a network baked before 22 Nov 2026: whatever lies within 12 m of an end
            endLinks = new List<(int, int)>[roads.Length * 2];
            for (int r = 0; r < roads.Length; r++)
                for (int e = 0; e < 2; e++)
                {
                    var list = new List<(int, int)>();
                    var pts = roads[r].pts;
                    Vector3 p = e == 0 ? pts[0] : pts[pts.Length - 1];
                    for (int o = 0; o < roads.Length; o++)
                    {
                        if (o == r) continue;
                        var q = roads[o].pts;
                        int best = -1;
                        float bd = 144f;
                        for (int i = 0; i < q.Length; i++)
                        {
                            float d = (q[i] - p).sqrMagnitude;
                            if (d < bd) { bd = d; best = i; }
                        }
                        if (best >= 0) list.Add((o, best));
                    }
                    endLinks[r * 2 + e] = list;
                }
        }
    }
}
