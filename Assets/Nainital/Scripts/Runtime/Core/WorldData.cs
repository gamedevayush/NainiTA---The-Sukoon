using System;
using UnityEngine;

namespace Nainital
{
    // JSON written by Tools/process_data.py (JsonUtility friendly: flat float arrays x0,z0,x1,z1,...)
    [Serializable] public class WorldJson
    {
        public WorldMeta meta;
        public NamedPoly lake;
        public NamedPoly[] ponds;
        public RoadData[] roads;
        public OsmBuilding[] osmBuildings;
        public float[] houses;   // stride 9: x, z, w, d, yaw, levels, wall, roof, roofType
        public float[] trees;    // stride 4: x, z, type, scale
        public PoiData[] pois;
        public AreaData[] areas;
    }

    [Serializable] public class WorldMeta
    {
        public float sizeM;
        public int res;
        public float hMin, hMax, lakeLevel;
        public float mapCx, mapCz;   // the map square's centre in local metres (0, 0 = CENTER_LAT/LON)
        public double centerLat, centerLon;
        public string attribution;
    }

    [Serializable] public class NamedPoly { public string name; public float[] pts; public float level; }   // level: a small lake's water (m), 0 = none
    [Serializable] public class RoadData { public string name, kind; public float width; public float[] pts; }
    [Serializable] public class OsmBuilding { public string name, kind; public int levels; public float[] pts; }
    [Serializable] public class PoiData { public string name, kind, sub; public float x, z; }
    [Serializable] public class AreaData { public string kind, name; public float[] pts; }

    public static class Geo
    {
        public static Vector2[] ToPoints(float[] flat)
        {
            var p = new Vector2[flat.Length / 2];
            for (int i = 0; i < p.Length; i++) p[i] = new Vector2(flat[2 * i], flat[2 * i + 1]);
            return p;
        }

        public static bool PointInPolygon(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }
            return inside;
        }

        public static float SignedArea(Vector2[] poly)
        {
            float a = 0;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                a += poly[j].x * poly[i].y - poly[i].x * poly[j].y;
            return a * 0.5f;
        }

        public static Vector2 ClosestOnSegment(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return a + ab * t;
        }

        public static float DistanceToPolyline(Vector2[] line, Vector2 p, bool closed)
        {
            float best = float.MaxValue;
            int n = closed ? line.Length : line.Length - 1;
            for (int i = 0; i < n; i++)
            {
                var c = ClosestOnSegment(line[i], line[(i + 1) % line.Length], p);
                best = Mathf.Min(best, (c - p).sqrMagnitude);
            }
            return Mathf.Sqrt(best);
        }

        /// <summary>Ear-clipping triangulation of a simple polygon (CCW or CW). Returns index triples.</summary>
        public static int[] Triangulate(Vector2[] poly)
        {
            int n = poly.Length;
            var result = new System.Collections.Generic.List<int>(n * 3);
            if (n < 3) return result.ToArray();
            var idx = new System.Collections.Generic.List<int>(n);
            bool ccw = SignedArea(poly) > 0;
            for (int i = 0; i < n; i++) idx.Add(ccw ? i : n - 1 - i);
            int guard = 0;
            while (idx.Count > 3 && guard++ < 10000)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int i0 = idx[(i + idx.Count - 1) % idx.Count], i1 = idx[i], i2 = idx[(i + 1) % idx.Count];
                    Vector2 a = poly[i0], b = poly[i1], c = poly[i2];
                    float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                    if (cross <= 1e-7f) continue; // reflex
                    bool any = false;
                    for (int k = 0; k < idx.Count && !any; k++)
                    {
                        int ik = idx[k];
                        if (ik == i0 || ik == i1 || ik == i2) continue;
                        if (InTri(poly[ik], a, b, c)) any = true;
                    }
                    if (any) continue;
                    result.Add(i0); result.Add(i1); result.Add(i2);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break; // degenerate polygon: give up gracefully
            }
            if (idx.Count == 3) { result.Add(idx[0]); result.Add(idx[1]); result.Add(idx[2]); }
            return result.ToArray();
        }

        static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }
    }
}
