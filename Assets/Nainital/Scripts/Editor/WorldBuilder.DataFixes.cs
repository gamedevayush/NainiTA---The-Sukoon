using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Corrections to world.json applied when the world is built, for things the map data is missing or has wrong.
    /// Each one names its source. Applied right after world.json is read, before anything else uses it.
    /// </summary>
    public static partial class WorldBuilder
    {
        static void FixWorldData(WorldJson world)
        {
            var roads = new List<RoadData>(world.roads);
            // Colvin Road (user, 2026-11-14: "gate ke saamne se bhi ek road upar jaati hai, you missed it"). Not in our
            // OpenStreetMap extract; traced from the user's Google satellite screenshot, scaled by the Boat House Club's
            // footprint (about 4.7 px a metre): it leaves the Mall at its Mallital end, opposite the Shaktipeeth gate, and
            // climbs north-east past the hotels above the Boat House Club.
            if (!roads.Exists(r => r.name == "Colvin Road"))
                roads.Add(new RoadData
                {
                    name = "Colvin Road", kind = "tertiary", width = 5f,
                    pts = new[] { 90f, 118f, 104f, 124f, 122f, 130f, 145f, 134f, 173f, 140f, 196f, 151f, 214f, 166f },
                });
            // The footway that climbed from the walk at the Capitol's front corner straight down its front as a ramp (user,
            // 2026-11-14: "stairs red line direction nahi"): it now starts at the foot of the stairs that come down along the
            // retaining wall (WorldBuilder.CapitolGround.cs) and runs on at the low level.
            foreach (var r in roads)
            {
                if (r.kind != "footway" || r.pts == null || r.pts.Length < 4) continue;
                if (Mathf.Abs(r.pts[0] - (-5f)) < 1.5f && Mathf.Abs(r.pts[1] - (-15f)) < 1.5f && Mathf.Abs(r.pts[2] - 28f) < 1.5f)
                {
                    var foot = CapStairsFoot;
                    r.pts[0] = foot.x; r.pts[1] = foot.y;
                }
            }
            world.roads = roads.ToArray();
            // The bandstand (user, iPhone bug 2026-11-16: "Bandstand will be in front of where T point meeting and not in
            // right park"): process_data.py had it approximate, inside the park by the Flats; it stands at the water's edge
            // straight ahead of the T where the path down from the Flats meets the lakeside path (WorldBuilder.Mallital.cs)
            if (world.pois != null)
                foreach (var p in world.pois)
                    if (p.name == "Bandstand") { p.x = BandstandSpot.x; p.z = BandstandSpot.y; }
            FixJoggersParkShore(world);
        }

        /// <summary>
        /// The shore at City Joggers Park (owner, 2026-11-22, the open gym's ground): the lake outline went straight from
        /// (785, -696) to (747, -611) to (714, -507), 4-9 m inside the real edge along the park, so the bank there was a
        /// 2-8 m ledge and a 5 m drop. The middle vertex is replaced by the edge read off the satellite (JoggersPark.Shore).
        /// </summary>
        static void FixJoggersParkShore(WorldJson world)
        {
            var pts = world.lake?.pts;
            if (pts == null || pts.Length < 6) return;
            int n = pts.Length / 2, hit = -1;
            for (int i = 0; i < n; i++)
                if (Mathf.Abs(pts[i * 2] - 747.1f) < 0.6f && Mathf.Abs(pts[i * 2 + 1] - (-610.67f)) < 0.6f) { hit = i; break; }
            if (hit < 0) { Debug.LogWarning("[Nainital] Joggers Park shore: the lake vertex at (747.1, -610.7) is not there - data changed? (left as it is)"); return; }
            // the new points must run the same way round as the outline: from the vertex before (south) to the one after
            Vector2 prev = new Vector2(pts[((hit - 1 + n) % n) * 2], pts[((hit - 1 + n) % n) * 2 + 1]);
            Vector2 next = new Vector2(pts[((hit + 1) % n) * 2], pts[((hit + 1) % n) * 2 + 1]);
            bool northward = JoggersPark.Track(next).x > JoggersPark.Track(prev).x;
            var list = new List<float>(pts.Length + JoggersPark.Shore.Length * 2);
            for (int i = 0; i < n; i++)
            {
                if (i != hit) { list.Add(pts[i * 2]); list.Add(pts[i * 2 + 1]); continue; }
                for (int k = 0; k < JoggersPark.Shore.Length; k++)
                {
                    var sd = JoggersPark.Shore[northward ? k : JoggersPark.Shore.Length - 1 - k];
                    var w = JoggersPark.World(sd.x, sd.y);
                    list.Add(w.x); list.Add(w.y);
                }
            }
            world.lake.pts = list.ToArray();
            Debug.Log($"[Nainital] Joggers Park shore: lake vertex {hit} -> {JoggersPark.Shore.Length} points along the park");
        }

        /// <summary>The T where the path from the Flats (24, 29) meets the lakeside path, and the way it comes in.</summary>
        static readonly Vector2 BandstandT = new Vector2(40f, 19f), BandstandDir = new Vector2(16f, -10f).normalized;
        /// <summary>The bandstand: 6.3 m on from the T, its 4.4 m plinth reaching out over the water's edge.</summary>
        internal static Vector2 BandstandSpot => BandstandT + BandstandDir * 6.3f;
    }
}
