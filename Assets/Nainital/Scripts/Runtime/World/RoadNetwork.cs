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
        }

        public Path[] roads;   // drivable
        public Path[] walks;   // sidewalks, footpaths, promenade

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
