using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Sariyatal and the Woodland Waterfall below it (user, 2026-11-19: Street View of the park and the lakeside road, an
    /// aerial photo of the lake, the Google map; then the falls - "green is stairs, white is waterfall, blue is water
    /// flow; when stairs meet water there is a way to go there and bathe; it does not go up to the road; the entry gate is
    /// down", with the ticket counter, the log arch, the stairs and the pool).
    /// Sariyatal: the long lake in the Kaladhungi road's loop. At its north end, inside the loop and below the road, the
    /// park: a lawn with tables, chairs and umbrellas, a green-roofed cafe and a Pepsi shed, a slide and swings, flags, two
    /// fenced settling basins, a stepped weir into the lake, a crash barrier along the road above. Along the west shore a
    /// walk with a blue railing and, between it and the road, snack stalls under tarps and plastic chairs; zorb balls
    /// and paddle boats on the water; a small dam at the south tip. The DEM puts the west road up to 12 m under the
    /// water in the south (a narrow valley SRTM cannot see): the shore and the road are raised to the lake before the
    /// roads are levelled (ShapeSariyatalGround).
    /// Woodland Waterfall: the stream leaves the lake's south end in a culvert under the stalls, comes out of a rock face
    /// as a cascade, runs down a wooded ravine over two falls into concrete bathing pools and on to the lower road.
    /// From the gate on the lower road (ticket counter Rs 50, a log arch) stone steps with green railings climb beside
    /// the stream to the lower pool, on to the upper pool, and round a loop through the cypresses back down.
    /// </summary>
    public static partial class LandmarkBuilder
    {
        internal const float SyCutZ = -1168f;   // the water ends here; north of it is the park
        static readonly Vector2 SyPark = new Vector2(-2318f, -1148f);

        static float SyWater(WorldCtx ctx)
        {
            foreach (var p in ctx.world.ponds) if (p.name == "Sariyatal") return p.level - ctx.world.meta.hMin;
            return 583.8f;
        }

        static Vector2[] SyPond(WorldCtx ctx)
        {
            foreach (var p in ctx.world.ponds) if (p.name == "Sariyatal") return Geo.ToPoints(p.pts);
            return null;
        }

        /// <summary>The part of a polygon with z at or below `cut` (Sutherland-Hodgman against one line).</summary>
        internal static Vector2[] ClipSouth(Vector2[] poly, float cut)
        {
            var o = new List<Vector2>();
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                bool ina = a.y <= cut, inb = b.y <= cut;
                if (ina) o.Add(a);
                if (ina != inb) o.Add(Vector2.Lerp(a, b, (cut - a.y) / (b.y - a.y)));
            }
            return o.ToArray();
        }

        /// <summary>The west shore of the water south of the park, north to south, pushed `off` metres out of the water.</summary>
        static List<Vector2> SyWestShore(WorldCtx ctx, float off)
        {
            var poly = ClipSouth(SyPond(ctx), SyCutZ);
            // the west edge: from the cut's west end round to the south tip, the points west of the lake's middle line
            var pts = new List<Vector2>();
            float midX = 0f;
            foreach (var p in poly) midX += p.x;
            midX /= poly.Length;
            foreach (var p in poly)   // the west side, stopping short of the narrow south tip (its two sides would zigzag)
                if (p.y > -1292f && (p.x < midX - 4f || p.y > SyCutZ - 0.5f && p.x < midX)) pts.Add(p);
            pts.Sort((a, b) => b.y.CompareTo(a.y));
            var o = new List<Vector2>();
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 t = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                Vector2 n = new Vector2(t.y, -t.x);   // heading south, the right hand is west: out of the water
                if (n.x > 0f) n = -n;
                o.Add(pts[i] + n * off);
            }
            return o;
        }

        /// <summary>Planner circles: the park, the west shore strip and its stalls, the falls' ravine.</summary>
        internal static IEnumerable<(Vector2 c, float r)> SariyatalSpots(WorldCtx ctx)
        {
            if (SyPond(ctx) == null) yield break;
            for (float x = -2350f; x <= -2286f; x += 8f)
                for (float z = -1164f; z <= -1132f; z += 8f) yield return (new Vector2(x, z), 5f);
            foreach (var p in SyWestShore(ctx, 6f)) yield return (p, 6f);
            yield return (new Vector2(-2342.5f, -1212.1f), 3f);   // a data hut on the shore strip
            foreach (var p in WlStream) yield return (p, 3.5f);
            yield return (new Vector2(-2356.3f, -1326.2f), 3f);   // the data hut where the stream comes out
        }

        /// <summary>Before the roads are levelled: the shore and the west road at the lake's level (the DEM sank them
        /// up to 12 m under the water), and the park's lawn level just above the water inside the road's loop.</summary>
        internal static void ShapeSariyatalGround(WorldCtx ctx)
        {
            var pond = SyPond(ctx);
            if (pond == null) return;
            float water = SyWater(ctx);
            var road = RoadLine(ctx, "Kaldhungi-Nainital Road", new Vector2(-2368f, -1250f), out float hw);
            if (road == null) return;
            var water2 = ClipSouth(pond, SyCutZ);
            ShapeTerrain(ctx, new Vector2(-2400f, -1345f), new Vector2(-2262f, -1112f), (p, y) =>
            {
                Project(road, p, out _, out _, out _, out float dRoad);
                bool inWater = Geo.PointInPolygon(water2, p);
                // the park: north of the cut, inside the loop, clear of the carriageway
                if (p.y > SyCutZ && p.y < -1124f && p.x > -2357f && p.x < -2281f)
                {
                    float edge = dRoad - (hw + 1.2f);   // a grass bank up to the road
                    float park = water + 1.3f;
                    if (edge <= 0f) return y;
                    return edge < 5f ? Mathf.Lerp(Mathf.Max(park, y), park, edge / 5f) : park;
                }
                if (inWater || p.y > -1175f || p.y < -1312f) return y;   // not into the falls' ravine below the south tip
                // west of the water: shore and road raised to the lake (only raised), easing out over 10 m
                float dShore = Geo.DistanceToPolyline(water2, p, true);
                bool west = p.x < -2330f;
                if (!west) return y;
                float t = water + 0.6f + Mathf.Min(1.0f, dShore * 0.25f);
                float zFade = Mathf.Clamp01(Mathf.Min(-1175f - p.y, p.y + 1312f) / 10f);
                float reach = Mathf.Max(0f, dRoad - (hw + 6f));
                float w = zFade * (1f - Mathf.SmoothStep(0f, 1f, reach / 10f));
                return Mathf.Lerp(y, Mathf.Max(y, t), w);
            });
        }

        static void BuildSariyatal(WorldCtx ctx, Transform parent)
        {
            var pond = SyPond(ctx);
            if (pond == null) return;
            var rng = new System.Random(4021);
            float water = SyWater(ctx), park = water + 1.3f;
            var road = RoadLine(ctx, "Kaldhungi-Nainital Road", new Vector2(-2368f, -1250f), out float hw);
            var sm = new SiteMeshes();
            var concrete = sm.Photo("concrete_floor_worn_001", new Color(0.88f, 0.88f, 0.86f));
            var stone = sm.Photo("old_stone_wall", new Color(0.82f, 0.8f, 0.76f));
            var paver = sm.Photo("cobblestone_floor_08", new Color(0.86f, 0.84f, 0.8f), 0.3f);
            float FaceTo(Vector2 a, Vector2 b) { Vector2 d = b - a; return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg; }
            float Gnd(Vector2 p) => ctx.H(p);

            // the park's lawn: grass on the levelled ground, a gravel walk round it
            PaintTerrain(ctx, new Vector2(-2358f, -1170f), new Vector2(-2280f, -1124f), (p, slope) =>
            {
                if (p.y < SyCutZ || slope > 25f) return null;
                bool walk = Mathf.Abs(p.y - (SyCutZ + 2.5f)) < 1.2f || Mathf.Abs(p.x + 2306f) < 1.1f;
                return walk ? new Vector4(0.1f, 0.05f, 0.25f, 0.6f) : new Vector4(0.9f, 0.1f, 0f, 0.05f);
            });

            // 1. the stepped weir along the cut: three concrete steps down into the lake, white water on each
            var pond2 = ClipSouth(pond, SyCutZ);
            float wx0 = float.MaxValue, wx1 = float.MinValue;
            foreach (var p in pond2) if (Mathf.Abs(p.y - SyCutZ) < 0.01f) { wx0 = Mathf.Min(wx0, p.x); wx1 = Mathf.Max(wx1, p.x); }
            if (wx1 > wx0)
                for (int k = 0; k < 3; k++)
                {
                    float z0 = SyCutZ + 0.2f - k * 0.55f, top = park - 0.45f * (k + 1);
                    Slab(concrete, concrete, new Vector2(wx0 - 1f, z0), new Vector2(wx1 + 1f, z0), 0.6f, top, top, water - 1.5f, 1.2f);
                    sm.loose.Quad(new Vector3(wx0, top, z0 - 0.31f), new Vector3(wx1, top, z0 - 0.31f), new Vector3(wx1, top - 0.43f, z0 - 0.31f), new Vector3(wx0, top - 0.43f, z0 - 0.31f),
                                  new Rect(S(Pal.ShirtWhite), Vector2.zero), Vector3.back);
                }

            // 2. the settling basins in the park's west corner: concrete tanks, brown water, a gravel heap, a blue mesh fence
            var basinWater = new MeshBuilder();
            foreach (var (bc, bw, bd) in new[] { (new Vector2(-2343f, -1153f), 9f, 7f), (new Vector2(-2343f, -1141.5f), 9f, 6f) })
            {
                float top = park + 0.25f, floorY = park - 1.2f;
                Vector2 x2 = new Vector2(bw * 0.5f, 0f), z2 = new Vector2(0f, bd * 0.5f);
                foreach (var (a, b) in new[] { (bc - x2 - z2, bc + x2 - z2), (bc + x2 - z2, bc + x2 + z2), (bc + x2 + z2, bc - x2 + z2), (bc - x2 + z2, bc - x2 - z2) })
                    Slab(concrete, concrete, a, b, 0.35f, top, top, floorY - 0.4f, 1.2f);
                basinWater.Quad(new Vector3(bc.x - bw * 0.5f, park - 0.55f, bc.y - bd * 0.5f), new Vector3(bc.x + bw * 0.5f, park - 0.55f, bc.y - bd * 0.5f),
                                new Vector3(bc.x + bw * 0.5f, park - 0.55f, bc.y + bd * 0.5f), new Vector3(bc.x - bw * 0.5f, park - 0.55f, bc.y + bd * 0.5f), Vector2.zero, Vector3.up);
                var fence = new List<Vector3>();
                foreach (var c in new[] { bc - x2 - z2 - new Vector2(0.5f, 0.5f), bc + x2 - z2 + new Vector2(0.5f, -0.5f), bc + x2 + z2 + new Vector2(0.5f, 0.5f), bc - x2 + z2 + new Vector2(-0.5f, 0.5f), bc - x2 - z2 - new Vector2(0.5f, 0.5f) })
                    fence.Add(V3(c, top));
                var fencePts = new List<Vector3>();
                for (int i = 0; i + 1 < fence.Count; i++)
                {
                    int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(fence[i], fence[i + 1]) / 2f));
                    for (int k = 0; k < n; k++) fencePts.Add(Vector3.Lerp(fence[i], fence[i + 1], k / (float)n));
                }
                fencePts.Add(fence[fence.Count - 1]);
                PipeRail(sm, fencePts, Pal.BoatBlue, 1.15f, 0.025f);
                for (int i = 0; i + 1 < fencePts.Count; i++)   // the mesh: thin uprights between the posts
                    for (int k = 1; k < 8; k++)
                    {
                        Vector3 m = Vector3.Lerp(fencePts[i], fencePts[i + 1], k / 8f);
                        sm.loose.Box(m + Vector3.up * 0.58f, new Vector3(0.015f, 1.1f, 0.015f), S(Pal.BoatBlue));
                    }
            }
            Blob(sm.atlas, V3(new Vector2(-2349.5f, -1162f), park - 0.3f), new Vector3(2.6f, 1.1f, 2.2f), 11, S(Pal.StoneGrey), 0.25f);   // the gravel heap
            ctx.MeshObject("Sariyatal basin water", parent, basinWater, ctx.mats.water, null, false);

            // 3. the cafe and the Pepsi shed by the road on the east side, flags and sacks along the road
            Vector2 cafe = new Vector2(-2296f, -1140f);
            float cafeYaw = FaceTo(cafe, SyPark + new Vector2(-6f, -6f));
            float cy = Hut(ctx, sm, cafe, cafeYaw, 6f, 4f, 2.8f, "plastered_wall_04", new Color(0.32f, 0.36f, 0.3f), new Color(0.45f, 0.72f, 0.5f), Pal.WoodDark, true, 1.6f, park + 0.15f);
            var cq = Quaternion.Euler(0f, cafeYaw, 0f);
            Board(sm, V3(cafe, cy + 2.55f) + cq * new Vector3(-0.8f, 0f, 2.06f), cafeYaw, 3.2f, 0.5f, Pal.JacketBlue, new[] { "SARIYATAL POINT CAFE" }, new[] { 0.2f }, Pal.ShirtWhite, Pal.IronBlack, cy + 2.6f);
            Vector2 pepsi = cafe + new Vector2(Mathf.Cos(cafeYaw * Mathf.Deg2Rad), -Mathf.Sin(cafeYaw * Mathf.Deg2Rad)) * 5.2f;
            sm.atlas.Box(V3(pepsi, park + 1.2f), new Vector3(3.2f, 2.4f, 2f), Quaternion.Euler(0f, cafeYaw, 0f), S(Pal.JacketBlue));
            Glyph3D.Write(sm.loose, "PEPSI", Glyph3D.Face.Block, V3(pepsi, park + 1.4f) + cq * new Vector3(0f, 0f, 1.03f), cq * Vector3.forward, 0.45f, 0.02f, 2.8f, S(Pal.ShirtWhite), S(Pal.StoneGrey));
            int[] flagCols = { Pal.BoatYellow, Pal.JacketBlue, Pal.JacketRed, Pal.BoatGreen, Pal.SareePink, Pal.BoatYellow };
            for (int k = 0; k < 6; k++)
            {
                Vector2 fp = new Vector2(-2300f + k * 3.2f, -1131f + k * 0.3f);
                Flag(sm.atlas, V3(fp, Gnd(fp)), 2.4f, flagCols[k]);
            }
            for (int k = 0; k < 6; k++) sm.atlas.Box(V3(cafe + new Vector2(-4.2f + (k % 3) * 0.55f, 3.2f), park + 0.18f + (k / 3) * 0.3f), new Vector3(0.5f, 0.32f, 0.75f), S(k % 2 == 0 ? Pal.BoatYellow : Pal.PantsKhaki));

            // 4. the lawn: tables with plastic chairs, umbrellas, a slide and climbing frame, swings, a seesaw, solar lamps
            int[] umb = { Pal.BoatGreen, Pal.SareePink, Pal.JacketBlue };
            for (int k = 0; k < 9; k++)
            {
                Vector2 tp = SyPark + new Vector2(-8f + (k % 3) * 6.5f + (float)rng.NextDouble(), -7f + (k / 3) * 6.5f + (float)rng.NextDouble());
                Vector3 t3 = V3(tp, park);
                sm.atlas.Box(t3 + Vector3.up * 0.72f, new Vector3(0.85f, 0.05f, 0.85f), S(Pal.IronBlack));
                sm.atlas.Box(t3 + Vector3.up * 0.36f, new Vector3(0.08f, 0.72f, 0.08f), S(Pal.IronBlack));
                for (int c = 0; c < 4; c++)
                {
                    var q = Quaternion.Euler(0f, c * 90f + 45f * (k % 2), 0f);
                    Vector3 chair = t3 + q * new Vector3(0f, 0f, 0.8f);
                    sm.atlas.Box(chair + Vector3.up * 0.43f, new Vector3(0.45f, 0.05f, 0.45f), q, S(Pal.JacketBlack));
                    sm.atlas.Box(chair + q * new Vector3(0f, 0.68f, 0.22f), new Vector3(0.45f, 0.5f, 0.05f), q, S(Pal.JacketBlack));
                    foreach (float x in new[] { -0.19f, 0.19f })
                        foreach (float z in new[] { -0.19f, 0.19f }) sm.loose.Box(chair + q * new Vector3(x, 0.21f, z), new Vector3(0.04f, 0.42f, 0.04f), q, S(Pal.JacketBlack));
                }
                if (k % 3 == 1)
                {
                    sm.loose.Cylinder(t3 + Vector3.up * 0.74f, 0.025f, 0.025f, 1.6f, 4, S(Pal.ShirtWhite), true);
                    sm.loose.Cylinder(t3 + Vector3.up * 2.1f, 1.3f, 0.05f, 0.45f, 8, S(umb[k / 3]), true);
                }
            }
            {
                // the playground by the road bend on the west side: a slide on a climbing frame, blue and yellow
                Vector3 pg = V3(new Vector2(-2330f, -1133f), park);
                foreach (float x in new[] { -1f, 1f })
                    foreach (float z in new[] { -1f, 1f }) sm.atlas.Box(pg + new Vector3(x, 1.2f, z), new Vector3(0.12f, 2.4f, 0.12f), S(Pal.BoatYellow));
                sm.atlas.Box(pg + Vector3.up * 1.5f, new Vector3(2.1f, 0.1f, 2.1f), S(Pal.BoatBlue));
                sm.atlas.Pyramid(pg + Vector3.up * 2.4f, 2.3f, 2.3f, 0.8f, S(Pal.JacketRed));
                sm.atlas.Box(pg + new Vector3(2.4f, 0.8f, 0f), new Vector3(3.2f, 0.06f, 0.7f), Quaternion.Euler(0f, 0f, -28f), S(Pal.BoatYellow));
                for (int k = 0; k < 5; k++) sm.atlas.Box(pg + new Vector3(-1.05f, 0.3f + k * 0.3f, 0f), new Vector3(0.05f, 0.05f, 1.9f), S(Pal.BoatBlue));
                Vector3 sw = V3(SyPark + new Vector2(-12f, 6f), park);
                foreach (float x in new[] { -1.8f, 1.8f })
                {
                    sm.atlas.Limb(sw + new Vector3(x, 0f, -0.7f), sw + new Vector3(x, 2.4f, 0f), 0.05f, 0.05f, 5, S(Pal.JacketRed));
                    sm.atlas.Limb(sw + new Vector3(x, 0f, 0.7f), sw + new Vector3(x, 2.4f, 0f), 0.05f, 0.05f, 5, S(Pal.JacketRed));
                }
                sm.atlas.Limb(sw + new Vector3(-1.8f, 2.4f, 0f), sw + new Vector3(1.8f, 2.4f, 0f), 0.05f, 0.05f, 5, S(Pal.JacketRed));
                foreach (float x in new[] { -0.8f, 0.8f })
                {
                    sm.loose.Limb(sw + new Vector3(x - 0.2f, 2.4f, 0f), sw + new Vector3(x - 0.2f, 0.5f, 0f), 0.01f, 0.01f, 3, S(Pal.IronBlack), false);
                    sm.loose.Limb(sw + new Vector3(x + 0.2f, 2.4f, 0f), sw + new Vector3(x + 0.2f, 0.5f, 0f), 0.01f, 0.01f, 3, S(Pal.IronBlack), false);
                    sm.atlas.Box(sw + new Vector3(x, 0.5f, 0f), new Vector3(0.5f, 0.05f, 0.25f), S(Pal.BoatYellow));
                }
                Vector3 ss = V3(SyPark + new Vector2(10f, 7f), park);
                sm.atlas.Box(ss + Vector3.up * 0.25f, new Vector3(0.2f, 0.5f, 0.3f), S(Pal.IronBlack));
                sm.atlas.Box(ss + Vector3.up * 0.55f, new Vector3(3.2f, 0.08f, 0.3f), Quaternion.Euler(0f, 0f, 10f), S(Pal.JacketRed));
            }
            foreach (var lp in new[] { new Vector2(-2328f, -1158f), new Vector2(-2306f, -1158f), new Vector2(-2318f, -1136f) })
            {
                sm.atlas.Cylinder(V3(lp, park), 0.06f, 0.05f, 5f, 6, S(Pal.StoneGrey), true);
                sm.atlas.Box(V3(lp, park + 5.05f), new Vector3(0.7f, 0.04f, 0.45f), Quaternion.Euler(-20f, 0f, 0f), S(Pal.JacketBlue));   // the solar panel
                sm.loose.Box(V3(lp, park + 4.6f) + new Vector3(0.35f, 0f, 0f), new Vector3(0.3f, 0.1f, 0.15f), S(Pal.LampBulb));
            }

            // 5. the crash barrier along the road above the park
            if (road != null)
            {
                Project(road, SyPark, out float sPark, out float parkSide, out _, out _);
                var barrier = new List<Vector3>();
                for (float s = sPark - 70f; s <= sPark + 70f; s += 2f)
                {
                    var p = PointAt(road, s, out var dir);
                    Vector2 q = p + new Vector2(dir.y, -dir.x) * Mathf.Sign(parkSide) * (hw + 0.7f);
                    if (q.y < SyCutZ - 6f || q.x < -2362f || q.x > -2276f) { if (barrier.Count > 1) WBeam(sm, barrier); barrier.Clear(); continue; }
                    barrier.Add(V3(q, ctx.H(p)));
                }
                if (barrier.Count > 1) WBeam(sm, barrier);
            }

            // 6. the west shore: a paved walk with a blue railing on the water side, stalls between it and the road
            var shore = SyWestShore(ctx, 1.4f);
            var walk = new List<WalkNode>();
            foreach (var p in shore) walk.Add(new WalkNode(p, water + 0.9f, false));
            Walkway(ctx, sm, walk, 1.8f, paver, stone);
            PipeRail(sm, WalkSide(walk, -0.85f * (Vector2.Dot(shore[shore.Count - 1] - shore[0], Vector2.down) > 0 ? 1f : -1f), 2f), Pal.BoatBlue);
            if (road != null)
            {
                int[] tarps = { Pal.SareePink, Pal.JacketBlue, Pal.BoatBlue, Pal.SareeOrange };
                int k = 0;
                for (float z = -1192f; z >= -1290f; z -= 14f, k++)
                {
                    Vector2 guess = new Vector2(-2350f, z);
                    Project(road, guess, out float s, out float side, out _, out _);
                    var rp = PointAt(road, s, out var dir);
                    Vector2 n = new Vector2(dir.y, -dir.x);
                    if (n.x < 0f) n = -n;   // towards the lake
                    Vector2 at = rp + n * (hw + 3.2f);
                    float yaw = FaceTo(at, rp), g = Mathf.Max(Gnd(at), water + 1.0f);
                    if (k == 1)
                    {
                        // Laxmi Fast Food Centre: a counter under a purple tarp with its painted board
                        TarpStall(sm.atlas, V3(at, g), yaw, Pal.SareePink, rng);
                        var q = Quaternion.Euler(0f, yaw, 0f);
                        Board(sm, V3(at, g) + q * new Vector3(0f, 0.75f, 1.75f), yaw, 2.8f, 0.9f, Pal.JacketBlue, new[] { "LAXMI", "FAST FOOD CENTRE" }, new[] { 0.26f, 0.16f },
                              Pal.BoatYellow, Pal.WoodDark, g, Glyph3D.Face.Desi);
                    }
                    else TarpStall(sm.atlas, V3(at, g), yaw, tarps[k % tarps.Length], rng);
                    // plastic chairs and a table on the shore side of the stall
                    Vector3 tc = V3(at + n * 3.4f + new Vector2(-n.y, n.x) * 1.5f, g);
                    sm.atlas.Box(tc + Vector3.up * 0.7f, new Vector3(0.8f, 0.05f, 0.8f), S(Pal.IronBlack));
                    sm.atlas.Box(tc + Vector3.up * 0.35f, new Vector3(0.08f, 0.7f, 0.08f), S(Pal.IronBlack));
                    for (int c = 0; c < 3; c++)
                    {
                        var q = Quaternion.Euler(0f, c * 120f + k * 20f, 0f);
                        Vector3 ch = tc + q * new Vector3(0f, 0f, 0.8f);
                        sm.atlas.Box(ch + Vector3.up * 0.43f, new Vector3(0.45f, 0.05f, 0.45f), q, S(Pal.StoneGrey));
                        sm.atlas.Box(ch + q * new Vector3(0f, 0.68f, 0.22f), new Vector3(0.45f, 0.5f, 0.05f), q, S(Pal.StoneGrey));
                    }
                }
            }

            // 7. on the water: zorb balls and paddle boats by the west shore, the red inflatable; the dam at the south tip
            var water3 = ClipSouth(pond, SyCutZ);
            var afloatA = SyWestShore(ctx, -5f);
            var afloatB = SyWestShore(ctx, -8f);
            for (int k = 0; k < 7; k++)
            {
                var row = k % 2 == 0 ? afloatA : afloatB;
                Vector2 p = Vector2.Lerp(row[Mathf.Clamp(1 + k / 2, 0, row.Count - 1)], row[Mathf.Clamp(2 + k / 2, 0, row.Count - 1)], 0.5f);
                if (!Geo.PointInPolygon(water3, p)) continue;
                if (k < 3)
                {
                    Vector3 c = V3(p, water + 0.9f);
                    sm.loose.Limb(c + new Vector3(-1.2f, 0f, 0f), c + new Vector3(1.2f, 0f, 0f), 1.0f, 1.0f, 12, S(k == 1 ? Pal.JacketBlue : Pal.ShirtWhite));
                }
                else
                {
                    var q = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    Vector3 c = V3(p, water);
                    sm.loose.Box(c + Vector3.up * 0.2f, new Vector3(1.5f, 0.4f, 2.4f), q, S(k % 2 == 0 ? Pal.BoatYellow : Pal.JacketRed));
                    sm.loose.Box(c + q * new Vector3(0f, 1.0f, -0.2f), new Vector3(1.5f, 0.06f, 1.3f), q, S(Pal.ShirtWhite));
                    foreach (float x in new[] { -0.7f, 0.7f }) sm.loose.Box(c + q * new Vector3(x, 0.7f, -0.2f), new Vector3(0.04f, 0.6f, 0.04f), q, S(Pal.ShirtWhite));
                }
            }
            Vector2 tip = new Vector2(-2353.5f, -1309.5f);
            Slab(concrete, stone, tip + new Vector2(-5f, 3.5f), tip + new Vector2(5f, -3.5f), 1.2f, water + 0.5f, water + 0.5f, Gnd(tip) - 1f, 1.2f);

            sm.Emit(ctx, "Sariyatal", parent);
            var site = new Site { origin = V3(new Vector2(-2310f, -1160f), park), rot = Quaternion.Euler(0f, 200f, 0f), depth = 1f };
            Sign(ctx, "Sariyatal", site, Vector3.zero, parent);
            ctx.Zone("Sariyatal", V3(new Vector2(-2320f, -1220f), water), 120f);
            ctx.landmarkSites["Sariyatal"] = V3(SyPark, park + 0.1f);
            var cam = new GameObject("ViewShot Sariyatal");
            cam.transform.SetParent(parent, false);
            Vector2 eye = new Vector2(-2300f, -1124f);
            cam.transform.SetPositionAndRotation(V3(eye, ctx.H(eye) + 1.7f), Quaternion.LookRotation(V3(new Vector2(-2325f, -1190f), water) - V3(eye, ctx.H(eye) + 1.7f)));
            Debug.Log($"[Nainital] Sariyatal: water {water:F1}, park {park:F1}, shore walk {shore.Count} points");
        }

        /// <summary>A W-beam crash barrier on posts, its red reflectors (the road above Sariyatal's park).</summary>
        static void WBeam(SiteMeshes sm, List<Vector3> pts)
        {
            for (int i = 0; i < pts.Count; i++)
            {
                sm.atlas.Box(pts[i] + Vector3.up * 0.38f, new Vector3(0.12f, 0.9f, 0.12f), S(Pal.StoneGrey));
                if (i == 0) continue;
                Vector3 a = pts[i - 1] + Vector3.up * 0.62f, b = pts[i] + Vector3.up * 0.62f, d = b - a;
                var q = Quaternion.LookRotation(d);
                sm.atlas.Box((a + b) * 0.5f, new Vector3(0.06f, 0.32f, d.magnitude + 0.05f), q, S(Pal.Concrete));
                if (i % 3 == 0) sm.loose.Box(b + Vector3.up * 0.05f, new Vector3(0.08f, 0.1f, 0.03f), q, S(Pal.JacketRed));
                Vector3 side = Vector3.Cross(Vector3.up, d).normalized;
                sm.col.Quad(pts[i - 1], pts[i], pts[i] + Vector3.up * 0.9f, pts[i - 1] + Vector3.up * 0.9f, Vector2.zero, side);
                sm.col.Quad(pts[i], pts[i - 1], pts[i - 1] + Vector3.up * 0.9f, pts[i] + Vector3.up * 0.9f, Vector2.zero, -side);
            }
        }

        // ------------------------------------------------------------------ Woodland Waterfall
        // the stream (from the owner's sketch, north-up, 5.3 px/m, anchored on the falls' map point): where it comes out of
        // the rock below the stalls, the "free waterfall" cascade, the upper fall and pool, the lower fall and pool, the road
        static readonly Vector2[] WlStream =
        {
            new Vector2(-2367.0f, -1325.0f), new Vector2(-2365.5f, -1328.5f), new Vector2(-2364.5f, -1331.5f), new Vector2(-2360.0f, -1335.5f),
            new Vector2(-2357.0f, -1338.2f), new Vector2(-2350.5f, -1344.6f), new Vector2(-2347.2f, -1348.8f), new Vector2(-2343.5f, -1356.0f),
            new Vector2(-2342.0f, -1362.0f), new Vector2(-2341.0f, -1367.5f), new Vector2(-2337.0f, -1372.0f), new Vector2(-2331.0f, -1378.0f),
        };
        const int WlUpperPool = 4, WlLowerPool = 6;

        static void BuildWoodlandFalls(WorldCtx ctx, Transform parent)
        {
            var gatePoi = ctx.Poi("Sariyatal Falls");
            if (!gatePoi.HasValue) return;
            var rng = new System.Random(5150);
            var road = RoadLine(ctx, "Nainital Bypass", gatePoi.Value, out float hw);
            if (road == null) return;
            var sm = new SiteMeshes();
            var stone = sm.Photo("old_stone_wall", new Color(0.8f, 0.78f, 0.74f));
            var treads = sm.Photo("concrete_floor_worn_001", new Color(0.78f, 0.78f, 0.76f));
            var concrete = sm.Photo("concrete_floor_worn_001", new Color(0.86f, 0.86f, 0.84f));
            var rock = sm.Photo(GreyRock(), new Color(0.62f, 0.64f, 0.6f), 0.15f);

            // 1. the stream's levels: the ravine's own floor (smoothed), never rising; above each pool the lip is lifted on a
            // rock ledge so the fall drops ~3.5 m (the first build set them from the top down and the stream floated 19 m up)
            var lv = new float[WlStream.Length];
            for (int i = 0; i < lv.Length; i++)
            {
                Vector2 p = WlStream[i];
                lv[i] = (ctx.H(p) * 2f + ctx.H(p + new Vector2(1.2f, 0f)) + ctx.H(p - new Vector2(1.2f, 0f)) + ctx.H(p + new Vector2(0f, 1.2f)) + ctx.H(p - new Vector2(0f, 1.2f))) / 6f + 0.15f;
            }
            for (int i = 1; i < lv.Length; i++) lv[i] = Mathf.Min(lv[i], lv[i - 1] - 0.05f);
            foreach (int pi in new[] { WlUpperPool, WlLowerPool }) lv[pi - 1] = Mathf.Max(lv[pi - 1], lv[pi] + 3.5f);
            var ground = new float[lv.Length];
            for (int i = 0; i < lv.Length; i++) ground[i] = ctx.H(WlStream[i]);
            // keep the run's end level with the culvert under the road
            Project(road, WlStream[WlStream.Length - 1], out float sEnd, out _, out _, out _);
            float roadEnd = ctx.H(PointAt(road, sEnd, out _));
            lv[lv.Length - 1] = Mathf.Min(lv[lv.Length - 1], roadEnd - 0.6f);
            for (int i = lv.Length - 2; i >= 0; i--) lv[i] = Mathf.Max(lv[i], lv[i + 1] + 0.05f);

            // 2. the stairs: the gate on the road, up beside the stream to the lower pool, on to the upper pool, round a loop
            Project(road, gatePoi.Value, out float sGate, out float gSide, out _, out _);
            var gRoad = PointAt(road, sGate, out var gDir);
            Vector2 gN = new Vector2(gDir.y, -gDir.x);
            if (Vector2.Dot(gN, WlStream[6] - gRoad) < 0f) gN = -gN;   // towards the falls
            Vector2 gate = gRoad + gN * (hw + 0.6f);
            Vector2 PoolSide(int i, float off)
            {
                Vector2 t = (WlStream[i + 1] - WlStream[i - 1]).normalized, r = new Vector2(t.y, -t.x);
                if (r.x > 0f) r = -r;   // the west bank
                return WlStream[i] + r * off;
            }
            var mainLine = new List<Vector2> { gate, gate + gN * 3f, new Vector2(-2355.5f, -1388f), new Vector2(-2352.5f, -1380.5f), new Vector2(-2348.8f, -1373.5f),
                                               new Vector2(-2346.2f, -1366f), new Vector2(-2346.4f, -1358.5f), PoolSide(WlLowerPool, 3.4f) };
            var upperLine = new List<Vector2> { PoolSide(WlLowerPool, 3.4f), new Vector2(-2353.2f, -1347.5f), PoolSide(WlUpperPool, 3.4f) };
            var loopLine = new List<Vector2> { PoolSide(WlUpperPool, 3.4f), new Vector2(-2362.5f, -1343.5f), new Vector2(-2368f, -1350.5f), new Vector2(-2373f, -1358.5f),
                                               new Vector2(-2372.5f, -1366.5f), new Vector2(-2366.5f, -1369f), new Vector2(-2359.5f, -1363f), new Vector2(-2351.5f, -1361f), new Vector2(-2346.6f, -1360.5f) };
            float gateY = ctx.H(gRoad) + 0.05f;
            var ends = new Dictionary<Vector2Int, float>();
            List<WalkNode> Stairs(List<Vector2> line, float? startY)
            {
                var pts = new List<Vector2>();
                for (int i = 0; i + 1 < line.Count; i++)
                {
                    int n = Mathf.Max(1, Mathf.RoundToInt(Vector2.Distance(line[i], line[i + 1]) / 2.2f));
                    for (int k = 0; k < n; k++) pts.Add(Vector2.Lerp(line[i], line[i + 1], k / (float)n));
                }
                pts.Add(line[line.Count - 1]);
                var ys = new float[pts.Count];
                for (int i = 0; i < pts.Count; i++) ys[i] = (ctx.H(pts[i]) * 2f + ctx.H(pts[i] + new Vector2(0.8f, 0f)) + ctx.H(pts[i] - new Vector2(0.8f, 0f))) / 4f + 0.12f;
                var k0 = Vector2Int.RoundToInt(pts[0] * 2f); var k1 = Vector2Int.RoundToInt(pts[pts.Count - 1] * 2f);
                if (startY.HasValue) ys[0] = startY.Value; else if (ends.TryGetValue(k0, out float a)) ys[0] = a;
                if (ends.TryGetValue(k1, out float b)) ys[ys.Length - 1] = b;
                ends[k0] = ys[0]; ends[k1] = ys[ys.Length - 1];
                var nodes = new List<WalkNode>();
                for (int i = 0; i < pts.Count; i++)
                    nodes.Add(new WalkNode(pts[i], ys[i], i + 1 < pts.Count && Mathf.Abs(ys[i + 1] - ys[i]) / Mathf.Max(0.1f, Vector2.Distance(pts[i], pts[i + 1])) > 0.1f));
                return nodes;
            }
            // the pool landings are at the pools' rim level
            ends[Vector2Int.RoundToInt(PoolSide(WlLowerPool, 3.4f) * 2f)] = lv[WlLowerPool] + 0.5f;
            ends[Vector2Int.RoundToInt(PoolSide(WlUpperPool, 3.4f) * 2f)] = lv[WlUpperPool] + 0.5f;
            var walks = new List<List<WalkNode>> { Stairs(mainLine, gateY) };
            walks.Add(Stairs(upperLine, null));
            walks.Add(Stairs(loopLine, null));

            // 3. the ground: a bed for the stream, pools and stairs cut in (only lowered), the gate's landing
            Vector2 bmin = new Vector2(-2385f, -1405f), bmax = new Vector2(-2320f, -1318f);
            var lines = new List<Vector2[]>();
            foreach (var wk in walks) { var l = new Vector2[wk.Count]; for (int i = 0; i < wk.Count; i++) l[i] = wk[i].p; lines.Add(l); }
            ShapeTerrain(ctx, bmin, bmax, (p, y) =>
            {
                Project(road, p, out _, out _, out _, out float dRoad);
                if (dRoad < hw + 0.3f) return y;
                float ny = y;
                Project(WlStream, p, out float s, out _, out int seg, out float d);
                if (d < 1.6f)
                {
                    float segLen = Vector2.Distance(WlStream[seg], WlStream[seg + 1]);
                    float run = 0f; for (int i = 0; i < seg; i++) run += Vector2.Distance(WlStream[i], WlStream[i + 1]);
                    float t = Mathf.Clamp01((s - run) / Mathf.Max(segLen, 1e-3f));
                    ny = Mathf.Min(ny, Mathf.Lerp(lv[seg], lv[seg + 1], t) - 0.35f);
                }
                foreach (int pi in new[] { WlUpperPool, WlLowerPool })
                    if (Vector2.Distance(p, WlStream[pi]) < 3.3f) ny = Mathf.Min(ny, lv[pi] - 0.4f);
                for (int k = 0; k < walks.Count; k++)
                {
                    Project(lines[k], p, out _, out _, out int sg, out float dw);
                    if (dw > 1.6f) continue;
                    var A = walks[k][sg]; var B = walks[k][sg + 1];
                    ny = Mathf.Min(ny, Mathf.Min(A.y, B.y) - 0.12f);
                }
                return ny;
            });

            // 4. the stairs themselves, green railings both sides, red flower pots on the posts
            for (int k = 0; k < walks.Count; k++)
            {
                Walkway(ctx, sm, walks[k], 1.6f, treads, stone, 0.18f);
                foreach (float off in new[] { 0.9f, -0.9f })
                {
                    var rail = WalkSide(walks[k], off, 2f, k == 0 ? 2 : 0);
                    var keep = new List<Vector3>();
                    foreach (var q in rail)
                    {
                        bool nearPool = Vector2.Distance(XZ(q), WlStream[WlUpperPool]) < 3.8f || Vector2.Distance(XZ(q), WlStream[WlLowerPool]) < 3.8f;
                        bool nearEnd = false;
                        for (int o = 0; o < walks.Count; o++)
                            if (o != k) { Project(lines[o], XZ(q), out _, out _, out _, out float dd); if (dd < 1.4f) nearEnd = true; }
                        if (nearPool || nearEnd) { if (keep.Count > 1) PipeRail(sm, keep, Pal.IronGreen, 0.95f); keep.Clear(); continue; }
                        keep.Add(q);
                    }
                    if (keep.Count > 1) PipeRail(sm, keep, Pal.IronGreen, 0.95f);
                    for (int i = 2; i < rail.Count; i += 3)
                        sm.loose.Cylinder(rail[i] + Vector3.up * 0.75f + Vector3.right * 0.12f, 0.1f, 0.13f, 0.18f, 8, S(Pal.TempleRed), true);
                }
            }

            // 5. the water: runs as ribbons, the falls as white sheets over rock, two concrete bathing pools
            var runMb = new MeshBuilder();
            var fallMb = new MeshBuilder();
            for (int i = 0; i + 1 < WlStream.Length; i++)
            {
                Vector2 a = WlStream[i], b = WlStream[i + 1], d = (b - a).normalized, r = new Vector2(d.y, -d.x);
                float len = Vector2.Distance(a, b), drop = lv[i] - lv[i + 1];
                float wa = i == 0 ? 1.0f : 1.6f, wb = 1.6f;
                if (drop / len > 0.5f)
                {
                    // a fall: the sheet a hand's breadth off the rock, rock lumps either side and at the lip
                    Vector3 la = V3(a + r * wa * 0.5f, lv[i] + 0.12f), ra = V3(a - r * wa * 0.5f, lv[i] + 0.12f);
                    Vector3 lb = V3(b + r * wb * 0.5f, lv[i + 1] + 0.1f), rb = V3(b - r * wb * 0.5f, lv[i + 1] + 0.1f);
                    fallMb.Tri(lb, rb, ra, new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, drop / 2f), V3(d, 0f) + Vector3.up * 0.3f);
                    fallMb.Tri(lb, ra, la, new Vector2(1f, 0f), new Vector2(0f, drop / 2f), new Vector2(1f, drop / 2f), V3(d, 0f) + Vector3.up * 0.3f);
                    foreach (float sx in new[] { 1f, -1f })
                        for (int k = 0; k < 2; k++)
                        {
                            Vector2 p = Vector2.Lerp(a, b, 0.3f + k * 0.4f) + r * sx * (1.4f + (float)rng.NextDouble() * 0.5f);
                            float rr = 0.8f + (float)rng.NextDouble() * 0.6f;
                            RockLump(rock, V3(p, Mathf.Lerp(lv[i], lv[i + 1], 0.3f + k * 0.4f) - 0.3f), new Vector3(rr, rr * 1.2f, rr), rng.Next(1000));
                        }
                    RockLump(rock, V3(a - d * 1.2f, lv[i] - 0.6f), new Vector3(2.0f, 1.0f, 1.4f), rng.Next(1000));
                    float under = lv[i] - ground[i];   // a lip lifted over the ground stands on a ledge of rock
                    if (under > 0.4f) RockLump(rock, V3(a - d * 0.4f, lv[i] - under * 0.55f - 0.2f), new Vector3(2.3f, under * 0.6f + 0.5f, 1.9f), rng.Next(1000));
                    sm.loose.Sphere(V3(b + d * 0.3f, lv[i + 1] + 0.15f), new Vector3(0.9f, 0.18f, 0.7f), 8, 3, S(Pal.ShirtWhite));   // the foam
                }
                else
                {
                    float gap = Mathf.Lerp(lv[i], lv[i + 1], 0.5f) - ctx.H((a + b) * 0.5f);
                    if (gap > 0.4f) RockLump(rock, V3((a + b) * 0.5f, Mathf.Lerp(lv[i], lv[i + 1], 0.5f) - gap * 0.5f - 0.3f), new Vector3(1.5f, gap * 0.6f + 0.4f, len * 0.6f + 0.5f), rng.Next(1000));
                    runMb.Tri(V3(a + r * wa * 0.5f, lv[i] + 0.04f), V3(b + r * wb * 0.5f, lv[i + 1] + 0.04f), V3(b - r * wb * 0.5f, lv[i + 1] + 0.04f), Vector2.zero, Vector3.up);
                    runMb.Tri(V3(a + r * wa * 0.5f, lv[i] + 0.04f), V3(b - r * wb * 0.5f, lv[i + 1] + 0.04f), V3(a - r * wa * 0.5f, lv[i] + 0.04f), Vector2.zero, Vector3.up);
                    for (int k = 0; k < 2; k++)
                    {
                        Vector2 p = Vector2.Lerp(a, b, (float)rng.NextDouble()) + r * (k == 0 ? 1f : -1f) * (1.0f + (float)rng.NextDouble() * 0.4f);
                        float rr = 0.35f + (float)rng.NextDouble() * 0.3f;
                        RockLump(rock, V3(p, Mathf.Lerp(lv[i], lv[i + 1], 0.5f) - rr * 0.3f), new Vector3(rr * 1.2f, rr * 0.7f, rr), rng.Next(1000));
                    }
                }
            }
            // where it comes out: a rock face over a pipe mouth below the stalls
            RockLump(rock, V3(WlStream[0] - (WlStream[1] - WlStream[0]).normalized * 1.4f, lv[0] - 0.5f), new Vector3(2.6f, 2.2f, 1.8f), 9);
            var pools = new MeshBuilder();
            foreach (int pi in new[] { WlUpperPool, WlLowerPool })
            {
                Vector2 c = WlStream[pi], t = (WlStream[pi + 1] - WlStream[pi - 1]).normalized, r = new Vector2(t.y, -t.x);
                float rim = lv[pi] + 0.5f, wl = lv[pi] + 0.3f, L = 2.4f, Wd = 1.8f;
                Vector2 c0 = c - t * L - r * Wd, c1 = c + t * L - r * Wd, c2 = c + t * L + r * Wd, c3 = c - t * L + r * Wd;
                // the walls, open where the stream comes in and goes out
                foreach (var (a, b) in new[] { (c0, c1), (c2, c3) }) Slab(concrete, concrete, a, b, 0.35f, rim, rim, lv[pi] - 0.6f, 1.2f);
                foreach (var (a, b) in new[] { (c1, c2), (c3, c0) })
                {
                    Vector2 m = (a + b) * 0.5f;
                    Slab(concrete, concrete, a, Vector2.Lerp(a, m, 0.6f), 0.35f, rim, rim, lv[pi] - 0.6f, 1.2f);
                    Slab(concrete, concrete, Vector2.Lerp(b, m, 0.6f), b, 0.35f, rim, rim, lv[pi] - 0.6f, 1.2f);
                }
                pools.Quad(V3(c0, wl), V3(c1, wl), V3(c2, wl), V3(c3, wl), Vector2.zero, Vector3.up);
                // the wire-mesh fence on the far (east) side
                Vector2 far = Vector2.Dot(r, Vector2.right) > 0f ? r : -r;
                var fence = new List<Vector3>();
                for (int k = -2; k <= 2; k++) fence.Add(V3(c + t * (k * 1.3f) + far * (Wd + 0.6f), rim));
                PipeRail(sm, fence, Pal.StoneGrey, 1.6f, 0.025f);
                for (int i = 0; i + 1 < fence.Count; i++)
                    for (int k = 1; k < 7; k++) sm.loose.Box(Vector3.Lerp(fence[i], fence[i + 1], k / 7f) + Vector3.up * 0.8f, new Vector3(0.012f, 1.55f, 0.012f), S(Pal.StoneGrey));
            }
            ctx.MeshObject("Woodland Waterfall falls", parent, fallMb, WorldBuilder.WaterfallMaterial(), null, false);
            ctx.MeshObject("Woodland Waterfall stream", parent, runMb, ctx.mats.water, null, false);
            ctx.MeshObject("Woodland Waterfall pools", parent, pools, PoolWater(), null, false);

            // 6. the gate: a stone wall along the road, the green ticket counter, the log arch over the first steps
            float gYaw = Mathf.Atan2(-gN.x, -gN.y) * Mathf.Rad2Deg;   // facing the road
            var gq = Quaternion.Euler(0f, gYaw, 0f);
            Vector2 along = new Vector2(gDir.x, gDir.y);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 a = gRoad + gN * (hw + 0.5f) + along * side * 1.3f, b = gRoad + gN * (hw + 0.5f) + along * side * 12f;
                Slab(stone, stone, a, b, 0.5f, ctx.H(a) + 1.3f, ctx.H(b) + 1.3f, Mathf.Min(GroundMin(ctx, a, b, 0.5f), ctx.H(a)) - 0.4f, 1.2f);
            }
            Vector2 tc = mainLine[2] - along * 3.2f;
            float ty = Hut(ctx, sm, tc, gYaw, 3f, 2.4f, 2.5f, "plastered_wall_04", new Color(0.42f, 0.62f, 0.45f), new Color(0.45f, 0.65f, 0.5f), Pal.IronGreen, true, 0.5f);
            Board(sm, V3(tc, ty + 1.75f) + gq * new Vector3(0.6f, 0f, 1.27f), gYaw, 1.3f, 0.8f, Pal.BoatGreen, new[] { "TICKET", "COUNTER", "RS 50" }, new[] { 0.14f, 0.14f, 0.16f },
                  Pal.ShirtWhite, Pal.IronGreen, ty + 1.7f);
            {
                var w0 = walks[0];
                Vector2 ap = w0[Mathf.Min(2, w0.Count - 1)].p;
                float ay = w0[Mathf.Min(2, w0.Count - 1)].y;
                Vector2 ad = (w0[Mathf.Min(3, w0.Count - 1)].p - w0[Mathf.Min(1, w0.Count - 1)].p).normalized, ar = new Vector2(ad.y, -ad.x);
                Vector2 log = S(Pal.WoodDark), log2 = S(Pal.ShoeBrown);
                foreach (float sx in new[] { -1.3f, 1.3f }) sm.atlas.Limb(V3(ap + ar * sx, ay - 0.3f), V3(ap + ar * sx, ay + 2.7f), 0.13f, 0.11f, 7, log);
                Vector3 prev = default;
                for (int k = 0; k <= 10; k++)
                {
                    float a = k / 10f * Mathf.PI;
                    Vector3 p = V3(ap + ar * (Mathf.Cos(a) * 1.5f), ay + 2.6f + Mathf.Sin(a) * 0.7f);
                    if (k > 0) sm.atlas.Limb(prev, p, 0.11f, 0.11f, 6, k % 2 == 0 ? log : log2, false);
                    prev = p;
                }
                for (int k = 0; k < 7; k++)   // the stubs of sawn branches on top
                {
                    float a = (k + 0.5f) / 7f * Mathf.PI;
                    Vector3 p = V3(ap + ar * (Mathf.Cos(a) * 1.5f), ay + 2.6f + Mathf.Sin(a) * 0.7f);
                    sm.atlas.Limb(p, p + new Vector3(Mathf.Cos(a) * 0.4f, 0.35f, 0f), 0.07f, 0.05f, 5, log2);
                }
                float yawIn = Mathf.Atan2(-ad.x, -ad.y) * Mathf.Rad2Deg;
                Board(sm, V3(ap, ay + 2.55f), yawIn, 2.4f, 0.42f, Pal.WoodDark, new[] { "WOODLAND WATERFALL" }, new[] { 0.2f }, Pal.PantsKhaki, Pal.WoodDark, ay + 2.6f);
                Bench(sm.atlas, V3(ap + ar * 2.6f + ad * 1.2f, ay), yawIn + 90f, Pal.JacketRed, Pal.IronBlack, 1.6f);
                Vector3 lamp = V3(ap - ar * 2.2f, ay);
                sm.atlas.Cylinder(lamp, 0.06f, 0.05f, 4.8f, 6, S(Pal.StoneGrey), true);
                sm.atlas.Box(lamp + Vector3.up * 4.85f, new Vector3(0.7f, 0.04f, 0.45f), Quaternion.Euler(-20f, yawIn, 0f), S(Pal.JacketBlue));
            }
            Vector2 np = gate + along * 3.2f;
            Board(sm, V3(np, ctx.H(np) + 1.1f), gYaw, 1.1f, 0.5f, Pal.ShirtWhite, new[] { "NO PARKING" }, new[] { 0.14f }, Pal.JacketRed, Pal.IronBlack, ctx.H(np));
            // the two restaurants across the road keep their boards on poles at the road side
            foreach (var (off, name, col) in new[] { (9f, "UTTARANCHAL RESTAURANT", Pal.JacketBlue), (20f, "SHUBHAM RESTAURANT", Pal.IronBlack) })
            {
                Vector2 rp = PointAt(road, sGate + off, out var rd);
                Vector2 rn = new Vector2(rd.y, -rd.x);
                if (Vector2.Dot(rn, gN) > 0f) rn = -rn;
                Vector2 bp = rp + rn * (hw + 1.2f);
                Board(sm, V3(bp, ctx.H(bp) + 2.6f), Mathf.Atan2(-rn.x, -rn.y) * Mathf.Rad2Deg, 3.6f, 0.7f, col, new[] { name }, new[] { 0.24f }, Pal.ShirtWhite, Pal.IronBlack, ctx.H(bp));
            }

            // 7. the trees: cypress and pine thick over the ravine, kept off the steps, the stream and the pools
            var extra = new List<float>();
            for (float x = -2384f; x <= -2324f; x += 4.5f)
                for (float z = -1396f; z <= -1322f; z += 4.5f)
                {
                    if (rng.NextDouble() < 0.3) continue;
                    var p = new Vector2(x + (float)(rng.NextDouble() - 0.5) * 3.5f, z + (float)(rng.NextDouble() - 0.5) * 3.5f);
                    Project(road, p, out _, out _, out _, out float dr);
                    if (dr < hw + 4f) continue;
                    Project(WlStream, p, out _, out _, out _, out float ds);
                    if (ds < 3f) continue;
                    bool bad = false;
                    foreach (var l in lines) { Project(l, p, out _, out _, out _, out float dw); if (dw < 2.6f) { bad = true; break; } }
                    if (bad || Vector2.Distance(p, WlStream[WlUpperPool]) < 5f || Vector2.Distance(p, WlStream[WlLowerPool]) < 5f) continue;
                    double c = rng.NextDouble();
                    extra.Add(p.x); extra.Add(p.y); extra.Add(c < 0.5 ? 3f : c < 0.8 ? 1f : 2f); extra.Add(0.9f + (float)rng.NextDouble() * 0.4f);
                }
            var trees = new float[ctx.world.trees.Length + extra.Count];
            ctx.world.trees.CopyTo(trees, 0);
            extra.CopyTo(trees, ctx.world.trees.Length);
            ctx.world.trees = trees;

            sm.Emit(ctx, "Woodland Waterfall", parent);
            var site = new Site { origin = V3(PoolSide(WlLowerPool, 4.6f), lv[WlLowerPool] + 0.5f), rot = Quaternion.Euler(0f, gYaw + 180f, 0f), depth = 1f };
            Sign(ctx, "Woodland Waterfall", site, Vector3.zero, parent);
            ctx.Zone("Woodland Waterfall", V3(WlStream[WlLowerPool], lv[WlLowerPool]), 45f);
            ctx.landmarkSites["Woodland Waterfall"] = V3(gate + gN * 1.5f, gateY + 0.1f);
            var cam = new GameObject("ViewShot Sariyatal Falls");   // PreviewCapture: from the lower pool's side, up at the fall
            cam.transform.SetParent(parent, false);
            Vector2 eye = PoolSide(WlLowerPool, 3.4f) + (WlStream[WlLowerPool + 1] - WlStream[WlLowerPool]).normalized * 3f;
            cam.transform.SetPositionAndRotation(V3(eye, lv[WlLowerPool] + 2.2f), Quaternion.LookRotation(V3(WlStream[WlLowerPool - 1], lv[WlLowerPool - 1] + 1f) - V3(eye, lv[WlLowerPool] + 2.2f)));
            Debug.Log($"[Nainital] Woodland Waterfall: gate {gateY:F1} at ({gate.x:F1},{gate.y:F1}), stream {lv[0]:F1} -> {lv[lv.Length - 1]:F1}, upper pool {lv[WlUpperPool]:F1}, lower pool {lv[WlLowerPool]:F1}, {extra.Count / 4} trees");
        }
    }
}
