using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// The ground round the Capitol and the Mallital lake bank.
    /// 2026-11-14 (user's screenshots): "stairs red line direction nahi, balki green direction hongi; upar ki upar aur neeche
    /// ki neeche, beech mein stairs": the walk keeps its level, the Capitol's front lies lower, a stone retaining wall stands
    /// between them along the walk and the stairs run down along that wall. The bank between the lakeside path and the water is
    /// level with the path.
    /// 2026-11-16 (user's screenshots and the satellite photo): "grill ke beech me stairs aare hain"; "green boundary waala
    /// saara uncha hai, baaki saara neeche, vo park upar waali road ke level se match krrha hai"; "capitol ke upar poora grey
    /// pattern tiles, niche poora red". So:
    ///  - the children's park (the OSM park polygon) is a terrace at the walk's level, held by stone retaining walls where the
    ///    ground beside it is lower (the Capitol's lane, the lakeside path), with its fence on top;
    ///  - the Capitol, the Video Games hall and the lane in front of them lie at the low level, paved in red; the walk and the
    ///    strip beside the park are grey pavers;
    ///  - the retaining wall runs along the walk's edge (the Capitol now stands clear of the walk, WorldBuilder.Mallital.cs),
    ///    and the stairs run down along it in the lane's corner, against the walk wall and the park's wall: outside the park
    ///    fence, with the walk railing stopping at their landing.
    /// Positions are in "walk coordinates": 'along' the walk to Naina Devi (north-east) from its corner at the Capitol, and
    /// 'side' from its centre line toward the lake (south-east).
    /// </summary>
    public static partial class WorldBuilder
    {
        static readonly Vector2 CapWalkAt = new Vector2(-5f, -15f), CapWalkDir = new Vector2(0.55f, 0.835f).normalized;
        static Vector2 CapLakeSide => new Vector2(CapWalkDir.y, -CapWalkDir.x);   // south-east
        const float CapWalkHalf = 2.75f;                       // the walk is 5.5 m wide (OSM)
        // the retaining walls are 1.5 m thick with low ground under them: the terrain has a sample every 1.46 m, so the slope
        // between the high and the low level is always inside a wall, never a ramp in the lane at its foot (16 Nov, first try)
        const float CapWallIn = CapWalkHalf, CapWallT = 1.5f;
        static float CapWallOut => CapWallIn + CapWallT;
        const float CapStairW = 2.4f, CapTread = 0.3f, CapLandLen = 1.0f;
        static Vector2 CapW(float along, float side) => CapWalkAt + CapWalkDir * along + CapLakeSide * side;
        static void CapUV(Vector2 q, out float along, out float side)
        {
            Vector2 rel = q - CapWalkAt;
            along = Vector2.Dot(rel, CapWalkDir);
            side = Vector2.Dot(rel, CapLakeSide);
        }
        /// <summary>The foot of the stairs (DataFixes.cs starts the lane's footway there). Nominal: the walk 2.3 m over the lane, 14 steps.</summary>
        internal static Vector2 CapStairsFoot => CapW(3.45f - CapLandLen - 14 * CapTread - 0.4f, CapWallOut + CapStairW * 0.5f);

        /// <summary>The lakeside path is kept at the Capitol's low level from here (beside the Capitol) to the T at the bandstand.</summary>
        static readonly Vector2 BankLowFrom = new Vector2(28f, -45f), BankLowTo = new Vector2(40f, 19f);

        // the lakeside path in front of the Capitol, from the Capitol corner to the boat stand
        static readonly Vector2[] MallitalBankPath =
        {
            new Vector2(9f, -73f), new Vector2(23f, -56f), new Vector2(28f, -45f), new Vector2(28f, -37f), new Vector2(28f, -27f),
            new Vector2(28f, -17f), new Vector2(35f, 0f), new Vector2(40f, 19f), new Vector2(50f, 36f),
        };

        static float capLowY = float.NaN, capHighY = float.NaN;
        static float[] capWalkY;              // the walk's level every metre of 'along', from CapWalkFrom
        const int CapWalkFrom = -70;

        static float CapWalkLevel(float along)
        {
            if (capWalkY == null) return capHighY;
            float t = Mathf.Clamp(along - CapWalkFrom, 0f, capWalkY.Length - 1);
            int i = Mathf.FloorToInt(t), j = Mathf.Min(i + 1, capWalkY.Length - 1);
            return Mathf.Lerp(capWalkY[i], capWalkY[j], t - i);
        }

        sealed class CapGeo
        {
            public float a0, a1, s0, s1;      // the Capitol in walk coordinates (a1: its front, toward the park)
            public float parkA;               // the park's edge toward the Capitol's lane
            public Vector2[] park;            // the children's park (raised)
            public float hallA0, hallA1, hallS0, hallS1;   // the Video Games hall
            public float landA1;              // the stairs' landing, its north-east end
            public float cornerS;             // 'side' of the park's corner at the lane's end
            public bool[] walled;             // per park edge: the ground outside is lower, a retaining wall holds the terrace
        }

        static CapGeo capGeo;
        static WorldCtx capGeoFor;

        /// <summary>The children's park by the Capitol: the OSM park polygon round its POI.</summary>
        static Vector2[] CapitolPark(WorldCtx ctx)
        {
            var poi = ctx.Poi("Children's Park (Capitol)");
            if (!poi.HasValue) return null;
            Vector2[] best = null;
            float bd = 25f;
            foreach (var ar in ctx.world.areas)
            {
                if (ar.kind != "park") continue;
                var poly = Geo.ToPoints(ar.pts);
                if (poly.Length < 3) continue;
                Vector2 c = Vector2.zero;
                foreach (var q in poly) c += q;
                c /= poly.Length;
                float d = Vector2.Distance(c, poi.Value);
                if (d < bd) { bd = d; best = poly; }
            }
            return best;
        }

        static CapGeo CapitolGeo(WorldCtx ctx)
        {
            if (capGeoFor == ctx && capGeo != null) return capGeo;
            var cap = CapitolBox(ctx);
            if (!cap.HasValue) return null;
            var (c, f, W, D) = cap.Value;
            Vector2 r = new Vector2(f.y, -f.x);
            var g = new CapGeo { a0 = 1e9f, a1 = -1e9f, s0 = 1e9f, s1 = -1e9f, parkA = 1e9f };
            foreach (var (u, v) in BoxProbe)
            {
                CapUV(c + r * (u * W) + f * (v * D), out float a, out float s);
                g.a0 = Mathf.Min(g.a0, a); g.a1 = Mathf.Max(g.a1, a); g.s0 = Mathf.Min(g.s0, s); g.s1 = Mathf.Max(g.s1, s);
            }
            g.park = CapitolPark(ctx);
            if (g.park != null)
                foreach (var p in g.park)
                {
                    CapUV(p, out float a, out float s);
                    if (s < 35f && a > g.a1) g.parkA = Mathf.Min(g.parkA, a);
                }
            if (g.parkA > 1e8f) g.parkA = g.a1 + 8f;
            g.landA1 = g.parkA - 0.05f;
            g.cornerS = CapWallOut;
            if (g.park != null)
            {
                g.cornerS = 1e9f;
                foreach (var p in g.park) { CapUV(p, out float a, out float s); if (Mathf.Abs(a - g.parkA) < 0.5f) g.cornerS = Mathf.Min(g.cornerS, s); }
                if (g.cornerS > 1e8f) g.cornerS = CapWallOut;
            }
            // the Video Games / Zoom Land hall (user, 2026-11-16: "almost end tk hai, side se hlki si gali"): from the Capitol's
            // lake side to a narrow lane short of the lakeside path, as deep as the Capitol
            g.hallA0 = g.a0 + 1.5f;
            g.hallA1 = g.a1 - 1.0f;
            g.hallS0 = g.s1 + 0.8f;
            g.hallS1 = g.hallS0 + 8f;
            for (float hs = g.hallS0 + 8f; hs < g.hallS0 + 26f; hs += 0.25f)
            {
                bool ok = true;
                for (float a = g.hallA0; a <= g.hallA1 + 0.01f && ok; a += 1f)
                {
                    Vector2 q = CapW(a, hs);
                    if (ctx.InLake(q) || ctx.LakeDistance(q) < 4f || Geo.DistanceToPolyline(MallitalBankPath, q, false) < 3.2f) ok = false;
                }
                if (!ok) break;
                g.hallS1 = hs;
            }
            capGeo = g;
            capGeoFor = ctx;
            return g;
        }

        /// <summary>The low ground: the Capitol, the hall and the lane in front, from the walk wall to the lakeside path.
        /// 'ease' is 0 on the flat and rises to 1 behind the Capitol, where it meets the ground there.</summary>
        static bool CapLowZone(WorldCtx ctx, CapGeo g, Vector2 q, float along, float side, out float ease)
        {
            ease = 0f;
            if (side <= CapWallIn + 0.05f || side > g.hallS1 + 14f) return false;   // under the walk wall: low too
            // at the lane's end the corner wall (1.5 m, toward the park) stands on low ground as well
            if (along > g.parkA + (side < g.cornerS + 0.2f ? CapWallT : 0.1f) || along < g.a0 - 6f) return false;
            if (g.park != null && Geo.PointInPolygon(g.park, q)) return false;
            if (ctx.InLake(q) || ctx.LakeDistance(q) < 2.5f || Geo.DistanceToPolyline(MallitalBankPath, q, false) < 1.5f) return false;
            ease = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((g.a0 - 1f - along) / 5f));
            return true;
        }

        /// <summary>Inside the park: 0 = no, 1 = the terrace, 2 = under a retaining wall (within 1.5 m of a walled edge: low,
        /// like the ground outside, so the slope is hidden in the wall).</summary>
        static int CapParkZone(CapGeo g, Vector2 q)
        {
            if (g.park == null || !Geo.PointInPolygon(g.park, q)) return 0;
            if (g.walled != null)
                for (int i = 0; i < g.park.Length; i++)
                {
                    if (!g.walled[i]) continue;
                    Vector2 a = g.park[i], b = g.park[(i + 1) % g.park.Length], ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                    if ((q - (a + ab * t)).sqrMagnitude < CapWallT * CapWallT) return 2;
                }
            return 1;
        }

        /// <summary>The park edge nearest q (index, the point on it, and its outward normal).</summary>
        static int CapParkEdge(CapGeo g, Vector2 q, out Vector2 on, out Vector2 outN)
        {
            int best = 0; float bd = float.MaxValue; on = q; outN = Vector2.zero;
            for (int i = 0; i < g.park.Length; i++)
            {
                Vector2 a = g.park[i], b = g.park[(i + 1) % g.park.Length], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                Vector2 p = a + ab * t;
                float d = (q - p).sqrMagnitude;
                if (d < bd) { bd = d; best = i; on = p; }
            }
            Vector2 e = (g.park[(best + 1) % g.park.Length] - g.park[best]).normalized;
            outN = new Vector2(e.y, -e.x);
            if (Geo.PointInPolygon(g.park, on + outN * 0.3f)) outN = -outN;
            return best;
        }

        static void ShapeCapitolGround(WorldCtx ctx)
        {
            var td = ctx.terrain.terrainData;
            int res = td.heightmapResolution;
            Vector3 tp = ctx.terrain.transform.position, sz = td.size;
            float cx = (res - 1) / sz.x, cz = (res - 1) / sz.z;
            capGeo = null;
            var g = CapitolGeo(ctx);
            if (g == null) return;

            // the walk's level along its length, smoothed over +-3 m
            var raw = new float[141];
            for (int i = 0; i < raw.Length; i++) raw[i] = ctx.H(CapW(CapWalkFrom + i, 0f));
            capWalkY = new float[raw.Length];
            for (int i = 0; i < raw.Length; i++)
            {
                float s = 0f; int n = 0;
                for (int j = Mathf.Max(0, i - 3); j <= Mathf.Min(raw.Length - 1, i + 3); j++) { s += raw[j]; n++; }
                capWalkY[i] = s / n;
            }
            capHighY = CapWalkLevel(g.parkA);
            capLowY = Mathf.Min(ctx.H(CapW(g.a1 + 3f, g.s0 + 8f)), ctx.H(CapW(g.a1 + 3f, g.s1)), capHighY - 1.2f);

            // the path along the lake: its level from the ground under it, smoothed
            var path = Resample(MallitalBankPath, 2f, false);
            var pathY = new float[path.Count];
            for (int i = 0; i < path.Count; i++) pathY[i] = ctx.H(path[i]);
            var sm = new float[path.Count];
            for (int i = 0; i < path.Count; i++)
            {
                float s = 0f; int c = 0;
                for (int j = Mathf.Max(0, i - 4); j <= Mathf.Min(path.Count - 1, i + 4); j++) { s += pathY[j]; c++; }
                sm[i] = Mathf.Max(s / c, ctx.lakeY + 1.4f);
            }
            // "baaki saara neeche" (16 Nov): the lane, the Capitol and the hall at the lakeside path's own level, one low
            // level from the walk wall to the water (at 1.2 m under the walk the lane rose in a hump to meet the path)
            {
                Vector2 laneEnd = CapW((g.hallA0 + g.hallA1) * 0.5f, g.hallS1 + 3f);
                int best = 0; float bd = float.MaxValue;
                for (int i = 0; i < path.Count; i++) { float d = (path[i] - laneEnd).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
                capLowY = Mathf.Min(capLowY, sm[best] + 0.1f);
            }

            // the lake side of the park, from the Capitol to the bandstand at the T where the path from the Flats comes down
            // (user, iPhone bug 2026-11-16: "terrain near park in lake side should be lower level like capitol.. till bandstand
            // side"): the lakeside path rose in a 1.2 m hump between them; now one even grade from the Capitol's low level to
            // the T, and the ground from the park's wall to the water at the path's level (the park stays a walled terrace)
            int lowA = 0, lowB = 0;
            {
                float da = float.MaxValue, db = float.MaxValue;
                for (int i = 0; i < path.Count; i++)
                {
                    float a = (path[i] - BankLowFrom).sqrMagnitude, b = (path[i] - BankLowTo).sqrMagnitude;
                    if (a < da) { da = a; lowA = i; }
                    if (b < db) { db = b; lowB = i; }
                }
                float yA = Mathf.Min(sm[lowA], capLowY), yB = Mathf.Min(sm[lowB], ctx.H(BankLowTo));
                for (int i = lowA; i <= lowB; i++) sm[i] = Mathf.Lerp(yA, yB, (i - lowA) / (float)Mathf.Max(1, lowB - lowA));
            }
            var lowPath = path.GetRange(lowA, lowB - lowA + 1).ToArray();
            int lakeSide = 0;

            // which edges of the park need a retaining wall: the ground beyond them is (or will be) lower than the terrace
            if (g.park != null)
            {
                g.walled = new bool[g.park.Length];
                for (int i = 0; i < g.park.Length; i++)
                {
                    Vector2 a = g.park[i], b = g.park[(i + 1) % g.park.Length];
                    if (Vector2.Distance(a, b) < 0.3f) continue;
                    Vector2 m = (a + b) * 0.5f, e = (b - a).normalized, outN = new Vector2(e.y, -e.x);
                    if (Geo.PointInPolygon(g.park, m + outN * 0.3f)) outN = -outN;
                    Vector2 o = m + outN * 2.4f;
                    CapUV(m, out float am, out _);
                    CapUV(o, out float ao, out float so);
                    // the strip between the walk and the park is set to the walk's level below (3.): no wall there, or the low
                    // ground under it left a hole at the walk's edge (user, 2026-11-17: "hole in terrain near capitol")
                    bool vergeOut = so > CapWalkHalf + 0.05f && so < 7f && ao > g.a0 - 6f && ao < 50f;
                    g.walled[i] = !vergeOut && (CapLowZone(ctx, g, o, ao, so, out _) || ctx.H(o) < CapWalkLevel(am) - 0.3f);
                }
            }

            int x0 = Mathf.Clamp(Mathf.FloorToInt((-50f - tp.x) * cx), 0, res - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((70f - tp.x) * cx), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((-100f - tp.z) * cz), 0, res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((50f - tp.z) * cz), 0, res - 1);
            int w = x1 - x0 + 1, h = z1 - z0 + 1;
            var hs = td.GetHeights(x0, z0, w, h);
            int low = 0, park = 0, verge = 0, bank = 0, under = 0;
            var underWall = new List<(int zi, int xi)>();
            for (int zi = 0; zi < h; zi++)
                for (int xi = 0; xi < w; xi++)
                {
                    var q = new Vector2(tp.x + (x0 + xi) / cx, tp.z + (z0 + zi) / cz);
                    float cur = tp.y + hs[zi, xi] * sz.y;
                    float want = cur;
                    CapUV(q, out float along, out float side);
                    int pz = CapParkZone(g, q);
                    if (pz == 2) { underWall.Add((zi, xi)); continue; }   // after the ground outside is done (below)
                    if (pz == 1) { want = CapWalkLevel(along); park++; }   // 1. the park: at the walk's level
                    else if (CapLowZone(ctx, g, q, along, side, out float ease)) { want = Mathf.Lerp(capLowY, cur, ease); low++; }   // 2. the Capitol's level
                    else if (side > CapWalkHalf + 0.05f && side < 7f && along > g.a0 - 6f && along < 50f && !ctx.InLake(q))
                    { want = CapWalkLevel(along); verge++; }   // 3. the strip between the walk and the park
                    else if (pz == 0 && side >= 7f && !ctx.InLake(q) && lowPath.Length > 1)
                    {
                        // 5. beside the lakeside path from the Capitol to the bandstand T: the path's even level (see above),
                        //    out to 7 m on either side, easing back to the ground over the next 3 m
                        float dp = Geo.DistanceToPolyline(lowPath, q, false);
                        if (dp < 10f)
                        {
                            int best = lowA; float bd = float.MaxValue;
                            for (int i = lowA; i <= lowB; i++) { float d = (path[i] - q).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
                            float k = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((dp - 7f) / 3f));
                            want = Mathf.Lerp(cur, sm[best] - 0.02f, k);
                            lakeSide++;
                        }
                    }

                    // 4. the bank between the lakeside path and the water: level with the path to the shore
                    if (!ctx.InLake(q))
                    {
                        float dl = ctx.LakeDistance(q);
                        if (dl < 9f)
                        {
                            int best = -1; float bd = float.MaxValue;
                            for (int i = 0; i < path.Count; i++) { float d = (path[i] - q).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
                            if (best >= 0 && bd < 14f * 14f && dl <= ctx.LakeDistance(path[best]) + 0.5f)   // between the path and the shore
                            {
                                float y = sm[best] - 0.05f;
                                if (want < y) { want = y; bank++; }
                            }
                        }
                    }
                    if (Mathf.Abs(want - cur) > 0.01f) hs[zi, xi] = Mathf.Clamp01((want - tp.y) / sz.y);
                }
            td.SetHeights(x0, z0, hs);
            // under the park's retaining walls: the level of the ground just outside, so the terrain is flat under the wall
            // out to the lane or the path and climbs to the terrace only inside it
            foreach (var (zi, xi) in underWall)
            {
                var q = new Vector2(tp.x + (x0 + xi) / cx, tp.z + (z0 + zi) / cz);
                CapParkEdge(g, q, out var on, out var outN);
                float y = Mathf.Min(ctx.H(on + outN * 1.6f), ctx.H(on + outN * 2.2f));
                CapUV(q, out float along, out _);
                y = Mathf.Min(y, CapWalkLevel(along) - 0.3f);
                hs[zi, xi] = Mathf.Clamp01((y - tp.y) / sz.y);
                under++;
            }
            td.SetHeights(x0, z0, hs);
            Debug.Log($"[Nainital] Capitol ground: walk {capHighY:F1}, front {capLowY:F1} ({low} samples low, park raised at {park}, under its walls {under}, walk strip {verge}); " +
                      $"Capitol along {g.a0:F1}..{g.a1:F1} side {g.s0:F1}..{g.s1:F1}; hall side {g.hallS0:F1}..{g.hallS1:F1}; park edge at {g.parkA:F1}; lake bank raised at {bank} samples; " +
                      $"lake side of the park levelled at {lakeSide} samples ({sm[lowA]:F2} to {sm[lowB]:F2})");
        }

        const float CapLedgeW = 1.5f;

        /// <summary>
        /// A flat stone strip along the high side of a retaining wall (user, iPhone bug 2026-11-16: "player stucked in terrain
        /// near capitol stairs.. make it proper flat"). The ground is low under the wall, and with a terrain sample every 1.46 m
        /// its slope down began up to 1.5 m before the wall: a trench 0.8 m deep between the walk and the wall that the player
        /// fell into and could not climb out of (the wall's collider on one side). The strip lies 3 cm over the high level, over
        /// that last 1.5 m, with a collider, so the walk runs flat to the wall's edge.
        /// </summary>
        static void InnerLedge(MeshBuilder mb, MeshBuilder col, Vector2 c, Quaternion rot, float len, float top, Vector2 uv)
        {
            const float depth = 1.0f;   // down past the bottom of the trench
            mb.Box(V(c, top + 0.03f - depth * 0.5f), new Vector3(CapLedgeW, depth, len + 0.02f), rot, uv);
            col.Box(V(c, top + 0.03f - depth * 0.5f), new Vector3(CapLedgeW, depth, len), rot, Vector2.zero);
        }

        /// <summary>The retaining wall along the walk, the stairs down in the lane's corner, the park's retaining walls, and the
        /// red and grey paving painted into the ground (WorldBuilder.RoadMask.cs).</summary>
        static void CapitolWallAndStairs(WorldCtx ctx, MeshBuilder mb, MeshBuilder col)
        {
            var g = CapitolGeo(ctx);
            if (g == null || float.IsNaN(capLowY)) return;
            Vector2 stone = Atlas.MaterialUV(Atlas.MatStone), cap = Atlas.MaterialUV(Atlas.MatConcrete), iron = Atlas.SolidUV(Pal.IronGreen);
            var wrot = Quaternion.LookRotation(Flat(CapWalkDir));
            float wallMid = (CapWallIn + CapWallOut) * 0.5f;
            float landA0 = g.landA1 - CapLandLen;
            float landY = CapWalkLevel(g.landA1 - CapLandLen * 0.5f);
            float rise = landY - capLowY;
            int steps = Mathf.Max(3, Mathf.CeilToInt(rise / 0.17f));
            float sr = rise / steps;

            // 1. the walk wall, 1.5 m thick, in 1.5 m pieces that follow the walk's level: its top a stone ledge flush with the
            //    walk, the coping and the railing on its outer edge, except over the landing
            float wa0 = g.a0 - 5f;
            float edgeS = CapWallOut - 0.18f;
            for (float a = wa0; a < g.landA1 - 0.01f; a += 1.5f)
            {
                float a1 = Mathf.Min(a + 1.5f, g.landA1), am = (a + a1) * 0.5f, len = a1 - a;
                float top = CapWalkLevel(am), bottom = capLowY - 0.5f;
                bool overLanding = am > landA0;
                Vector2 m2 = CapW(am, wallMid);
                mb.Box(V(m2, (top - 0.04f + bottom) * 0.5f), new Vector3(CapWallT, top - 0.04f - bottom, len + 0.02f), wrot, stone);
                mb.Box(V(m2, top - 0.02f), new Vector3(CapWallT, 0.05f, len + 0.02f), wrot, cap);   // flagstones on top
                col.Box(V(m2, (top + bottom) * 0.5f), new Vector3(CapWallT, top - bottom, len), wrot, Vector2.zero);
                InnerLedge(mb, col, CapW(am, CapWallIn - CapLedgeW * 0.5f), wrot, len, top, cap);
                if (overLanding) continue;   // open: you step from the walk over the ledge onto the landing
                Vector2 e2 = CapW(am, edgeS);
                mb.Box(V(e2, top + 0.15f), new Vector3(0.4f, 0.3f, len + 0.04f), wrot, cap);   // the coping people sit on
                Vector2 p0 = CapW(a, edgeS), p1 = CapW(a1, edgeS);
                mb.Limb(V(p0, CapWalkLevel(a) + 0.3f), V(p0, CapWalkLevel(a) + 1.2f), 0.03f, 0.03f, 4, iron, false);
                mb.Limb(V(p0, CapWalkLevel(a) + 1.2f), V(p1, CapWalkLevel(a1) + 1.2f), 0.025f, 0.025f, 4, iron, false);
                mb.Limb(V(p0, CapWalkLevel(a) + 0.75f), V(p1, CapWalkLevel(a1) + 0.75f), 0.02f, 0.02f, 4, iron, false);
                col.Box(V(e2, top + 0.75f), new Vector3(0.1f, 0.9f, len), wrot, Vector2.zero);
            }

            // 2. the stairs: a landing at the walk's level against the park's wall, then the flight down along the walk wall
            //    toward the Capitol (south-west), 2.4 m wide; the open side toward the lane has a rail
            float sm1 = CapWallOut + CapStairW, smid = CapWallOut + CapStairW * 0.5f;
            Vector2 lc = CapW(g.landA1 - CapLandLen * 0.5f, smid);
            float lh = landY - (capLowY - 0.3f);
            mb.Box(V(lc, capLowY - 0.3f + lh * 0.5f), new Vector3(CapStairW, lh, CapLandLen), wrot, stone);
            col.Box(V(lc, capLowY - 0.3f + lh * 0.5f), new Vector3(CapStairW, lh, CapLandLen), wrot, Vector2.zero);
            for (int k = 0; k < steps; k++)
            {
                float sy = landY - (k + 1) * sr;
                float am = landA0 - (k + 0.5f) * CapTread;
                float sh = sy - (capLowY - 0.3f);
                Vector2 sp = CapW(am, smid);
                mb.Box(V(sp, capLowY - 0.3f + sh * 0.5f), new Vector3(CapStairW, sh, CapTread + 0.02f), wrot, stone);
                col.Box(V(sp, capLowY - 0.3f + sh * 0.5f), new Vector3(CapStairW, sh, CapTread + 0.02f), wrot, Vector2.zero);
            }
            float footA = landA0 - steps * CapTread;
            for (int k = 0; k <= steps; k += 2)
            {
                float a = landA0 - k * CapTread, y = landY - k * sr;
                Vector2 p = CapW(a, sm1 - 0.08f);
                mb.Limb(V(p, y), V(p, y + 0.95f), 0.03f, 0.03f, 4, iron, false);
            }
            mb.Limb(V(CapW(g.landA1, sm1 - 0.08f), landY + 0.95f), V(CapW(landA0, sm1 - 0.08f), landY + 0.95f), 0.03f, 0.03f, 4, iron, false);
            mb.Limb(V(CapW(landA0, sm1 - 0.08f), landY + 0.95f), V(CapW(footA, sm1 - 0.08f), capLowY + 0.95f), 0.03f, 0.03f, 4, iron, false);
            mb.Limb(V(CapW(g.landA1, sm1 - 0.08f), landY), V(CapW(g.landA1, sm1 - 0.08f), landY + 0.95f), 0.03f, 0.03f, 4, iron, false);

            // 3. the park's retaining walls, wherever the ground beside the terrace is lower; first the short piece that closes
            //    the lane's corner between the walk wall and the park
            // (1.55 m thick, into the terrace: its top is a stone border at the park's level with the fence on the outer edge)
            int wallPieces = 0;
            void WallPiece(Vector2 a, Vector2 b, Vector2 outN, float top)
            {
                float len = Vector2.Distance(a, b);
                if (len < 0.05f) return;
                Vector2 m = (a + b) * 0.5f;
                float lowY = Mathf.Min(ctx.H(m + outN * 1.8f), ctx.H(m + outN * 2.6f));
                if (top - lowY < 0.3f) return;
                var r = Quaternion.LookRotation(Flat((b - a) / len));
                Vector2 c = m - outN * (CapWallT * 0.5f - 0.05f);
                mb.Box(V(c, (top + 0.04f + lowY - 0.5f) * 0.5f), new Vector3(CapWallT + 0.05f, top + 0.04f - (lowY - 0.5f), len + 0.02f), r, stone);
                mb.Box(V(c, top + 0.06f), new Vector3(CapWallT + 0.05f, 0.05f, len + 0.02f), r, cap);
                col.Box(V(c, (top + 0.08f + lowY) * 0.5f), new Vector3(CapWallT, top + 0.08f - lowY, len), r, Vector2.zero);
                InnerLedge(mb, col, m - outN * (CapWallT + CapLedgeW * 0.5f - 0.05f), r, len, top + 0.05f, cap);
                wallPieces++;
            }
            if (g.park != null)
            {
                // the corner piece: across the lane's end from the walk wall to the park, 1.5 m deep toward the park
                float top = CapWalkLevel(g.parkA + 0.75f);
                Vector2 cm = CapW(g.parkA + CapWallT * 0.5f - 0.05f, (CapWallIn + g.cornerS + 0.2f) * 0.5f);
                float cw = g.cornerS + 0.2f - CapWallIn;
                if (cw > 0.3f)
                {
                    var crot = Quaternion.LookRotation(Flat(CapLakeSide));
                    mb.Box(V(cm, (top - 0.04f + capLowY - 0.5f) * 0.5f), new Vector3(CapWallT + 0.1f, top - 0.04f - (capLowY - 0.5f), cw), crot, stone);
                    mb.Box(V(cm, top - 0.02f), new Vector3(CapWallT + 0.1f, 0.05f, cw), crot, cap);
                    col.Box(V(cm, (top + capLowY) * 0.5f), new Vector3(CapWallT, top - capLowY, cw), crot, Vector2.zero);
                    // its far side (toward Naina Devi) is the walk-level strip: flat over the slope down under the wall
                    InnerLedge(mb, col, CapW(g.parkA + CapWallT + CapLedgeW * 0.5f - 0.05f, (CapWallIn + g.cornerS + 0.2f) * 0.5f), crot, cw, top, cap);
                }
                for (int i = 0; i < g.park.Length; i++)
                {
                    if (g.walled != null && !g.walled[i]) continue;
                    Vector2 a = g.park[i], b = g.park[(i + 1) % g.park.Length];
                    float len = Vector2.Distance(a, b);
                    if (len < 0.3f) continue;
                    Vector2 dir = (b - a) / len, outN = new Vector2(dir.y, -dir.x);
                    if (Geo.PointInPolygon(g.park, (a + b) * 0.5f + outN * 0.5f)) outN = -outN;
                    for (float s = 0f; s < len - 0.01f; s += 1f)
                    {
                        Vector2 p0 = a + dir * s, p1 = a + dir * Mathf.Min(s + 1f, len);
                        CapUV((p0 + p1) * 0.5f, out float am, out _);
                        WallPiece(p0, p1, outN, CapWalkLevel(am));
                    }
                }
            }

            // 4. paving into the ground: red over all the low ground (the lane, round the Capitol and the hall), grey on the
            //    strip between the walk and the park (the walk itself is grey: BuildRoads)
            var red = new MeshBuilder();
            var grey = new MeshBuilder();
            const float cell = 1.2f;
            for (float a = g.a0 - 6f; a < 52f; a += cell)
                for (float s = 1.5f; s < g.hallS1 + 14f; s += cell)
                {
                    float ac = a + cell * 0.5f, sc = s + cell * 0.5f;
                    Vector2 q = CapW(ac, sc);
                    MeshBuilder tgt = null;
                    if (CapLowZone(ctx, g, q, ac, sc, out float ease)) { if (ease < 0.6f) tgt = red; }
                    else if (CapParkZone(g, q) == 0 && sc > CapWalkHalf - 0.3f && sc < 7f && ac > g.parkA - 0.5f && !ctx.InLake(q)) tgt = grey;
                    if (tgt == null) continue;
                    Vector2 q00 = CapW(a, s), q10 = CapW(a + cell, s), q11 = CapW(a + cell, s + cell), q01 = CapW(a, s + cell);
                    tgt.Quad(V(q00, 0f), V(q01, 0f), V(q11, 0f), V(q10, 0f), new Rect(0, 0, 1, 1), Vector3.up);
                }
            PaintRoadSurface(red, ChRedPaver);
            PaintRoadSurface(grey, ChGreyPaver);
            Debug.Log($"[Nainital] Capitol wall and stairs: rise {rise:F2} m, {steps} steps, landing at along {g.landA1:F1}; park walls {wallPieces} m");
        }
    }
}
