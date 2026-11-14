using UnityEngine;

namespace Nainital.EditorTools
{
    public static partial class WorldBuilder
    {
        /// <summary>
        /// The walk from the Mallital end of Mall Road to Naina Devi (user, 2026-11-14: "is road pe koi bada ghar nahi hai,
        /// max 2 manzil ke hain, aur vo bhi Capitol ke aage"). The data had two rows of 3-4 storey houses 8-9 m either side
        /// of it; the real walk is open lakefront. Houses within 18 m of it are left out, except in front of the Capitol,
        /// where they stay at most two storeys (and no G.O.A.N. model, which may be taller).
        /// </summary>
        static Vector2[] mallitalWalk;
        static WorldCtx mallitalWalkFor;
        const float WalkClear = 18f, WalkLow = 42f, CapitolKeep = 45f;

        static Vector2[] MallitalWalk(WorldCtx ctx)
        {
            if (mallitalWalkFor == ctx) return mallitalWalk;
            mallitalWalkFor = ctx;
            mallitalWalk = null;
            var temple = ctx.Poi("Naina Devi Temple") ?? ctx.Poi("Naina Devi Gate");
            // (two roads are named "Mall Road", one of them at Tallital: the Upper Mall's line is the sure one)
            var upper = RoadPoints(ctx, MallUpper);
            if (!temple.HasValue || upper == null) return null;
            float best = float.MaxValue;
            foreach (var r in ctx.world.roads)
            {
                if (r.kind != "pedestrian" && r.kind != "footway") continue;
                var l = Geo.ToPoints(r.pts);
                if (l.Length < 2) continue;
                // one end at the Mall, the other near the temple
                float s0 = Geo.DistanceToPolyline(upper, l[0], false) + Vector2.Distance(l[l.Length - 1], temple.Value);
                float s1 = Geo.DistanceToPolyline(upper, l[l.Length - 1], false) + Vector2.Distance(l[0], temple.Value);
                if (s1 < s0) { System.Array.Reverse(l); s0 = s1; }   // walk[0] is the Mall end
                if (s0 < best) { best = s0; mallitalWalk = l; }
            }
            if (best > 150f) mallitalWalk = null;
            return mallitalWalk;
        }

        /// <summary>
        /// Where the Jama Masjid stands: across the road from the Flats at the tiraha the user pointed out (2026-11-14, the
        /// junction at -150, 20 on the map: "ye real me ek tiraha hai", with Street View of the mosque beside it), facing
        /// the ground. The nearest spot to the Flats that is clear of every road for a 30 x 12 m building.
        /// </summary>
        internal static bool MasjidSite(WorldCtx ctx, out Vector2 at, out Vector2 faceTo)
        {
            // OpenStreetMap's footprint (user, 2026-11-14: "teen taraf road hai"): a long block running north-north-east,
            // between the Flats road on the east, the tiraha on the south and the Kaladhungi road on the west; its front
            // looks east across the road to the Flats
            at = new Vector2(-152.5f, 51.5f);
            Vector2 axis = new Vector2(10f, 47f).normalized, east = new Vector2(axis.y, -axis.x);
            faceTo = at + east * 60f;
            return true;
        }

        static bool MasjidSiteSearch(WorldCtx ctx, out Vector2 at, out Vector2 faceTo)
        {
            at = default;
            faceTo = default;
            var poly = FlatsPoly(ctx);
            if (poly == null) return false;
            Vector2 cen = Vector2.zero;
            foreach (var p in poly) cen += p;
            cen /= poly.Length;
            faceTo = cen;
            var tiraha = new Vector2(-150f, 20f);
            var clear = Clearance(ctx);
            float best = float.MaxValue;
            for (float r = 14f; r <= 60f; r += 2f)
                for (int a = 0; a < 360; a += 10)
                {
                    Vector2 p = tiraha + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * r;
                    if (ctx.InLake(p) || Geo.PointInPolygon(poly, p) || Geo.DistanceToPolyline(poly, p, true) < 12f) continue;
                    Vector2 f = (cen - p).normalized, right = new Vector2(f.y, -f.x);
                    if (clear.Blocked(p, 9f) || clear.Blocked(p + right * 11f, 4f) || clear.Blocked(p - right * 11f, 4f)) continue;
                    float d = Vector2.Distance(p, cen) + Vector2.Distance(p, tiraha) * 0.5f;
                    if (d < best) { best = d; at = p; }
                }
            return best < float.MaxValue;
        }

        /// <summary>False: no house here. lowOnly: the house stays, at most two storeys and procedural.</summary>
        static bool WalkHouse(WorldCtx ctx, Vector2 c, ref int levels, out bool lowOnly)
        {
            lowOnly = false;
            // nothing stands between the Flats (DSA ground) and the lake / Mall Road but what is built by hand (Capitol,
            // Video Games, bandstand, ghat): user, 2026-11-14 ("DSA aur Mall Road ke beech koi building nahi")
            var flats = FlatsPoly(ctx);
            if (flats != null && !Geo.PointInPolygon(flats, c))
            {
                float dF = Geo.DistanceToPolyline(flats, c, true), dL = ctx.LakeDistance(c);
                if (dF < 50f && dL < 70f && dF + dL < 80f) return false;
            }
            var walk = MallitalWalk(ctx);
            if (walk == null) return true;
            float dw = Geo.DistanceToPolyline(walk, c, false);
            if (dw > WalkLow) return true;
            // houses behind the mouth of the walk stand on Mall Road itself (its row of hotels): those stay as they are
            if (Vector2.Dot(c - walk[0], (walk[1] - walk[0]).normalized) < 3f) return true;
            var cap = CapitolBox(ctx);
            if (dw > WalkClear || cap.HasValue && Vector2.Distance(cap.Value.c, c) < CapitolKeep)
            {
                levels = Mathf.Min(levels, 2);   // nothing tall anywhere near the walk
                lowOnly = true;
                return true;
            }
            return false;
        }
    }
}
