using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    public static partial class WorldBuilder
    {
        /// <summary>
        /// The Flats beside its roads (user, 2026-11-14, Street View screenshots of the tiraha by the Jama Masjid): the
        /// ground lies almost level with the road, behind a low parapet people sit on. The DEM left the ground up to 3 m
        /// below the levelled road, a rock cliff with the railing at its foot. Here the ground inside the Flats comes up
        /// to 0.8 m below the road at the parapet and falls away gently (1 in 5) to its own level; the strip between the
        /// road and the parapet is level with the road. BuildFlats then builds a parapet there instead of the railing.
        /// </summary>
        // ---- the spectators' steps (user, 2026-11-14, photo: "stadium ke side mein audience baithne ke liye, till Jama
        // Masjid"): along the Flats road from the tiraha north to the mosque, five broad steps go down from the road to the
        // ground, pink risers with a teal band, a pink balustrade with lattice panels at the road, an orange rail at the foot.
        // The road there stands this high over the ground.
        public const float StandsRise = 2.6f, StandsStep = 0.52f, StandsTread = 0.95f;
        const int StandsTiers = 5;
        static readonly Vector2[] StandsRoad = { new Vector2(-148f, 21f), new Vector2(-146f, 23f), new Vector2(-143f, 33f), new Vector2(-139f, 41f),
                                                 new Vector2(-135f, 60f), new Vector2(-137f, 68f), new Vector2(-140f, 74f) };
        const float StandsRoadHalf = 3.5f;

        /// <summary>How far p lies from the road edge into the steps (negative: on the road side), and whether it is
        /// alongside the stretch at all.</summary>
        static bool InStands(Vector2 p, out float into)
        {
            into = 0f;
            float best = float.MaxValue, bestU = 0f;
            int bestSeg = -1, last = StandsRoad.Length - 2;
            for (int i = 0; i <= last; i++)
            {
                Vector2 a = StandsRoad[i], ab = StandsRoad[i + 1] - a;
                float u = Vector2.Dot(p - a, ab) / ab.sqrMagnitude;
                Vector2 q = a + ab * Mathf.Clamp01(u), n = new Vector2(ab.y, -ab.x).normalized;   // the Flats lie east: +x side
                if (n.x < 0f) n = -n;
                // the true distance to the segment, signed by the side it lies on. (Projecting p - q on the normal, as
                // before, came out near zero for a point past a short segment's end, so a strip beside the steps counted
                // as road: LevelFlatsEdge raised it to the road and the cut skipped it, and the steps sat in blocky pits.)
                float dist = (p - q).magnitude;
                float d = Vector2.Dot(p - q, n) < 0f ? -dist : dist;
                if (dist < Mathf.Abs(best)) { best = d; bestSeg = i; bestU = u; }
            }
            // only the two ends of the whole line cut it off, and only where that end is the nearest part of the line; at
            // a bend in between, a point beyond both segments' ends still belongs to the steps (it left a rock fin on the
            // outside of the bend). (Cutting off the first segment alone let the second one's start vertex claim the
            // ground south of the steps: it was raised by LevelFlatsEdge and partly cut, a ridge at z 9, 15 Nov.)
            if (bestSeg < 0 || (bestSeg == 0 && bestU < -0.02f) || (bestSeg == last && bestU > 1.02f)) return false;
            into = best - StandsRoadHalf;
            return true;
        }

        internal static bool InStandsBand(Vector2 p) => InStands(p, out float into) && into > -StandsRoadHalf - 0.5f && into < StandsTiers * StandsTread + 1.5f;

        internal static float FlatsGroundY(WorldCtx ctx)
        {
            var poly = FlatsPoly(ctx);
            if (poly == null) return 0f;
            Vector2 mn = poly[0], mx = poly[0];
            foreach (var p in poly) { mn = Vector2.Min(mn, p); mx = Vector2.Max(mx, p); }
            var inner = new List<float>();
            for (float x = mn.x; x <= mx.x; x += 4f)
                for (float z = mn.y; z <= mx.y; z += 4f)
                {
                    var q = new Vector2(x, z);
                    if (Geo.PointInPolygon(poly, q) && Geo.DistanceToPolyline(poly, q, true) > 20f) inner.Add(ctx.H(q));
                }
            if (inner.Count == 0) return 0f;
            inner.Sort();
            return inner[inner.Count / 2];
        }

        /// <summary>Builds the steps and cuts the ground under them down to the Flats (runs after the roads).</summary>
        static void BuildStands(WorldCtx ctx, Transform parent, float groundY)
        {
            var mb = new MeshBuilder();
            var col = new MeshBuilder();
            var lat = new MeshBuilder();
            Vector2 pink = Atlas.SolidUV(Pal.SareePink), teal = Atlas.SolidUV(Pal.BoatGreen), conc = Atlas.MaterialUV(Atlas.MatConcrete),
                    orange = Atlas.SolidUV(Pal.SareeOrange);
            // lower the ground under the steps to the Flats
            var td = ctx.terrain.terrainData;
            int res = td.heightmapResolution;
            Vector3 tp = ctx.terrain.transform.position, sz = td.size;
            float cx = (res - 1) / sz.x, cz = (res - 1) / sz.z;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((-160f - tp.x) * cx), 0, res - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((-100f - tp.x) * cx), 0, res - 1);
            // the box reaches everything InStands can claim (26 m out from the first segment goes down to z 3): a box edge
            // inside the cut left a raised row standing (15 Nov)
            int z0 = Mathf.Clamp(Mathf.FloorToInt((-10f - tp.z) * cz), 0, res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((100f - tp.z) * cz), 0, res - 1);
            // (user, 2026-11-14 screenshot: the steps lay in a ditch, "kya kya bana diya"): the ground is cut to the Flats'
            // level under the steps and a 3 m apron at their foot, then rises back to what LevelFlatsEdge left over 14 m,
            // a gentle slope with no cliff. Nothing is raised.
            float band = (StandsTiers + 1) * StandsTread;
            var hs = td.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
            for (int zi = 0; zi <= z1 - z0; zi++)
                for (int xi = 0; xi <= x1 - x0; xi++)
                {
                    var q = new Vector2(tp.x + (x0 + xi) / cx, tp.z + (z0 + zi) / cz);
                    if (!InStands(q, out float into) || into < 0.1f || into > band + 17f) continue;
                    float cur = tp.y + hs[zi, xi] * sz.y;
                    float want = groundY - 0.1f;
                    if (into > band + 3f) want = Mathf.Lerp(want, cur, Mathf.SmoothStep(0f, 1f, (into - band - 3f) / 14f));
                    if (cur > want) hs[zi, xi] = Mathf.Clamp01((want - tp.y) / sz.y);
                }
            // where the steps end, the cut met the ground LevelFlatsEdge raised beside the road in a step of up to 16 cm
            // that poked through the grit in a line of teeth (15 Nov). Inside the Flats and outside the cut, the ground
            // now eases down to the cut: no sample more than 10 cm above a neighbour (about 1 in 15). Only lowered.
            {
                int nx = x1 - x0 + 1, nz = z1 - z0 + 1;
                var free = new bool[nz, nx];
                var flats = FlatsPoly(ctx);
                for (int zi = 0; zi < nz; zi++)
                    for (int xi = 0; xi < nx; xi++)
                    {
                        var q = new Vector2(tp.x + (x0 + xi) / cx, tp.z + (z0 + zi) / cz);
                        free[zi, xi] = flats != null && Geo.PointInPolygon(flats, q) && !(InStands(q, out float into) && into >= 0.1f);
                    }
                float stepH = 0.1f / sz.y;
                for (int iter = 0; iter < 40; iter++)
                {
                    bool changed = false;
                    for (int zi = 1; zi < nz - 1; zi++)
                        for (int xi = 1; xi < nx - 1; xi++)
                        {
                            if (!free[zi, xi]) continue;
                            float lim = Mathf.Min(Mathf.Min(hs[zi - 1, xi], hs[zi + 1, xi]), Mathf.Min(hs[zi, xi - 1], hs[zi, xi + 1])) + stepH;
                            if (hs[zi, xi] > lim) { hs[zi, xi] = lim; changed = true; }
                        }
                    if (!changed) break;
                }
            }
            td.SetHeights(x0, z0, hs);

            // one continuous set of steps: a smooth centre line with averaged normals and a smoothed road level, every tier a
            // ribbon of shared corners (no gaps or jumps at the bends)
            var line = Resample(StandsRoad, 1.2f, false);
            int m = line.Count;
            var nrm = new Vector2[m];
            var roadY = new float[m];
            for (int i = 0; i < m; i++)
            {
                Vector2 t = (line[Mathf.Min(m - 1, i + 1)] - line[Mathf.Max(0, i - 1)]).normalized;
                Vector2 n = new Vector2(t.y, -t.x);
                if (n.x < 0f) n = -n;   // the Flats lie east
                nrm[i] = n;
                roadY[i] = ctx.H(line[i] + n * (StandsRoadHalf - 0.6f)) + RoadLift;
            }
            var smooth = new float[m];
            for (int i = 0; i < m; i++)
            {
                float s = 0f; int c = 0;
                for (int j = Mathf.Max(0, i - 3); j <= Mathf.Min(m - 1, i + 3); j++) { s += roadY[j]; c++; }
                smooth[i] = s / c;
            }
            Vector3 P(int i, float off, float y) { Vector2 q = line[i] + nrm[i] * off; return new Vector3(q.x, y, q.y); }
            float Top(int i, int k) { float step = Mathf.Max(0.2f, (smooth[i] - groundY) / (StandsTiers + 1)); return smooth[i] - k * step; }
            for (int i = 0; i + 1 < m; i++)
            {
                Vector3 up = Vector3.up;
                Vector3 outward = new Vector3(nrm[i].x, 0f, nrm[i].y);
                for (int k = 0; k <= StandsTiers; k++)
                {
                    float o0 = StandsRoadHalf + k * StandsTread, o1 = o0 + StandsTread;
                    float ta = Top(i, k), tb = Top(i + 1, k);
                    float na = k < StandsTiers ? Top(i, k + 1) : groundY - 0.15f, nb = k < StandsTiers ? Top(i + 1, k + 1) : groundY - 0.15f;
                    // the tread
                    mb.Quad(P(i, o0, ta), P(i + 1, o0, tb), P(i + 1, o1, tb), P(i, o1, ta), conc, up);
                    col.Quad(P(i, o0, ta), P(i + 1, o0, tb), P(i + 1, o1, tb), P(i, o1, ta), Vector2.zero, up);
                    // the riser below its outer edge: a teal band at the top, pink below
                    float ba = Mathf.Max(na, ta - 0.12f), bb = Mathf.Max(nb, tb - 0.12f);
                    mb.Quad(P(i, o1, ba), P(i + 1, o1, bb), P(i + 1, o1, tb), P(i, o1, ta), teal, outward);
                    mb.Quad(P(i, o1, na), P(i + 1, o1, nb), P(i + 1, o1, bb), P(i, o1, ba), pink, outward);
                    col.Quad(P(i, o1, na), P(i + 1, o1, nb), P(i + 1, o1, tb), P(i, o1, ta), Vector2.zero, outward);
                }
                // the road-side kerb face and the top of the balustrade wall foot
                float ra = smooth[i], rb = smooth[i + 1];
                mb.Quad(P(i, StandsRoadHalf, ra + 0.12f), P(i + 1, StandsRoadHalf, rb + 0.12f), P(i + 1, StandsRoadHalf + 0.3f, rb + 0.12f), P(i, StandsRoadHalf + 0.3f, ra + 0.12f), teal, up);
                // the balustrade rails (pink cap, teal foot), continuous
                mb.Quad(P(i, StandsRoadHalf + 0.08f, ra + 1.0f), P(i + 1, StandsRoadHalf + 0.08f, rb + 1.0f), P(i + 1, StandsRoadHalf + 0.28f, rb + 1.0f), P(i, StandsRoadHalf + 0.28f, ra + 1.0f), pink, up);
                mb.Quad(P(i, StandsRoadHalf + 0.08f, ra + 0.88f), P(i + 1, StandsRoadHalf + 0.08f, rb + 0.88f), P(i + 1, StandsRoadHalf + 0.08f, rb + 1.0f), P(i, StandsRoadHalf + 0.08f, ra + 1.0f), pink, -outward);
                mb.Quad(P(i, StandsRoadHalf + 0.28f, ra + 0.88f), P(i + 1, StandsRoadHalf + 0.28f, rb + 0.88f), P(i + 1, StandsRoadHalf + 0.28f, rb + 1.0f), P(i, StandsRoadHalf + 0.28f, ra + 1.0f), pink, outward);
                // the lattice panel between the rails (see-through pink jaali)
                Panel(lat, P(i, StandsRoadHalf + 0.18f, ra + 0.12f), P(i + 1, StandsRoadHalf + 0.18f, rb + 0.12f), 0.76f, -outward);
                col.Quad(P(i, StandsRoadHalf + 0.18f, ra), P(i + 1, StandsRoadHalf + 0.18f, rb), P(i + 1, StandsRoadHalf + 0.18f, rb + 1.05f), P(i, StandsRoadHalf + 0.18f, ra + 1.05f), Vector2.zero, -outward);
                if (i % 2 == 0)   // slim pink posts every 2.4 m
                {
                    Vector3 pc = P(i, StandsRoadHalf + 0.18f, ra + 0.5f);
                    mb.Box(pc, new Vector3(0.16f, 1.0f, 0.16f), Quaternion.LookRotation(outward), pink);
                }
                // the orange rail at the foot, on slim posts
                float fo = StandsRoadHalf + band + 0.3f;
                foreach (float y in new[] { 0.5f, 0.95f })
                    mb.Limb(P(i, fo, groundY + y), P(i + 1, fo, groundY + y), 0.03f, 0.03f, 4, orange, false);
                if (i % 2 == 0) mb.Limb(P(i, fo, groundY - 0.1f), P(i, fo, groundY + 1f), 0.035f, 0.035f, 4, orange, false);
            }
            ctx.MeshObject("Flats spectators' steps", parent, mb, ctx.mats.atlas, col);
            ctx.MeshObject("Flats steps lattice", parent, lat, IronMaterial("StandsLattice", FiligreePanel(), new Color(0.86f, 0.36f, 0.56f)), null, false);
        }

        static Vector2[] flatsPoly;
        static readonly List<(Vector2 p, float half)> flatsRoads = new List<(Vector2, float)>();

        static Vector2[] FlatsPoly(WorldCtx ctx)
        {
            Vector2[] poly = null;
            foreach (var a in ctx.world.areas)
            {
                if (a.kind != "stadium") continue;
                var p = Geo.ToPoints(a.pts);
                if (p.Length >= 3 && (poly == null || PolyArea(p) > PolyArea(poly))) poly = p;
            }
            return poly;
        }

        /// <summary>The nearest carriageway to q: its centre-line point and how far q is from its edge (negative: on it).</summary>
        static bool FlatsRoadNear(Vector2 q, float within, out Vector2 rp, out float edgeDist)
        {
            rp = q;
            edgeDist = float.MaxValue;
            foreach (var (p, half) in flatsRoads)
            {
                float d = Vector2.Distance(p, q) - half;
                if (d < edgeDist) { edgeDist = d; rp = p; }
            }
            return edgeDist <= within;
        }

        static bool FlatsByRoad(Vector2 q) => flatsPoly != null && FlatsRoadNear(q, 8f, out _, out _);
        static bool ByStands(Vector2 q) => InStands(q, out float into) && into > -6f && into < StandsTiers * StandsTread + 9f;

        static void LevelFlatsEdge(WorldCtx ctx)
        {
            flatsPoly = FlatsPoly(ctx);
            flatsRoads.Clear();
            var poly = flatsPoly;
            if (poly == null) return;
            Vector2 mn = poly[0], mx = poly[0];
            foreach (var p in poly) { mn = Vector2.Min(mn, p); mx = Vector2.Max(mx, p); }
            foreach (var r in ctx.world.roads)
            {
                if (NotRoads.Contains(r.kind)) continue;
                var line = Geo.ToPoints(r.pts);
                if (line.Length < 2) continue;
                foreach (var p in Resample(line, 2f, false))
                    if (p.x > mn.x - 30f && p.y > mn.y - 30f && p.x < mx.x + 30f && p.y < mx.y + 30f)
                        flatsRoads.Add((p, Mathf.Max(1.2f, r.width * 0.5f)));
            }
            if (flatsRoads.Count == 0) return;
            {   // what the data gives: the ground's own level, and each corner against its road
                var inner = new List<float>();
                for (float x = mn.x; x <= mx.x; x += 4f)
                    for (float z = mn.y; z <= mx.y; z += 4f)
                    {
                        var q = new Vector2(x, z);
                        if (Geo.PointInPolygon(poly, q) && Geo.DistanceToPolyline(poly, q, true) > 20f) inner.Add(ctx.H(q));
                    }
                inner.Sort();
                var sb = new System.Text.StringBuilder();
                if (inner.Count > 0) sb.Append($"inside min {inner[0]:F1} median {inner[inner.Count / 2]:F1} max {inner[inner.Count - 1]:F1}, lake {ctx.lakeY:F1}; corners: ");
                foreach (var p in poly)
                {
                    FlatsRoadNear(p, 999f, out var rp0, out float de0);
                    sb.Append($"({p.x:F0},{p.y:F0}) g{ctx.H(p):F1} road{ctx.H(rp0):F1}@{de0:F0}m  ");
                }
                Debug.Log("[Nainital] Flats levels: " + sb);
            }

            var td = ctx.terrain.terrainData;
            int res = td.heightmapResolution;
            Vector3 tp = ctx.terrain.transform.position, sz = td.size;
            float cx = (res - 1) / sz.x, cz = (res - 1) / sz.z;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((mn.x - 12f - tp.x) * cx), 0, res - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((mx.x + 12f - tp.x) * cx), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((mn.y - 12f - tp.z) * cz), 0, res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((mx.y + 12f - tp.z) * cz), 0, res - 1);
            int w = x1 - x0 + 1, hh = z1 - z0 + 1;
            if (w < 2 || hh < 2) return;
            var hs = td.GetHeights(x0, z0, w, hh);
            int raised = 0;
            float most = 0f;
            for (int z = 0; z < hh; z++)
                for (int x = 0; x < w; x++)
                {
                    var q = new Vector2(tp.x + (x0 + x) / cx, tp.z + (z0 + z) / cz);
                    bool inside = Geo.PointInPolygon(poly, q);
                    float dp = Geo.DistanceToPolyline(poly, q, true);
                    if (inside ? dp > 18f : dp > 6f) continue;
                    if (InStandsBand(q)) continue;   // the steps stand here: the ground goes down, not up
                    if (!FlatsRoadNear(q, 30f, out var rp, out float de) || de <= 0.3f) continue;
                    if (de - (inside ? dp : 0f) > 8f) continue;   // the road does not run along this edge
                    float roadY = ctx.H(rp);
                    float floor = inside ? roadY - 0.8f - Mathf.Max(0f, dp - 0.5f) / 5f : roadY - 0.1f;
                    float cur = tp.y + hs[z, x] * sz.y;
                    if (cur >= floor) continue;
                    most = Mathf.Max(most, floor - cur);
                    hs[z, x] = Mathf.Clamp01((floor - tp.y) / sz.y);
                    raised++;
                }
            td.SetHeights(x0, z0, hs);
            UnityEditor.EditorUtility.SetDirty(td);
            Debug.Log($"[Nainital] Flats edge: {raised} ground samples raised beside the roads (most {most:F1} m)");
        }

        /// <summary>One piece of the Flats' roadside parapet: a low tan wall with a dull red coping, as high as the road
        /// side needs and down to the ground inside.</summary>
        static void FlatsParapet(WorldCtx ctx, MeshBuilder mb, MeshBuilder col, Vector2 a, Vector2 b)
        {
            Vector2 mid = (a + b) * 0.5f, dir = (b - a).normalized, n = new Vector2(-dir.y, dir.x);
            if (Geo.PointInPolygon(flatsPoly, mid + n * 0.7f)) n = -n;   // n points out, to the road
            float yOut = Mathf.Max(ctx.H(mid + n * 1.2f), ctx.H(mid)), yIn = Mathf.Min(ctx.H(mid - n * 1.6f), ctx.H(mid));
            float top = yOut + 0.6f, bot = yIn - 0.3f, seg = Vector2.Distance(a, b) + 0.05f;
            var rot = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y), Vector3.up);
            var c = new Vector3(mid.x, (top + bot) * 0.5f, mid.y);
            mb.Box(c, new Vector3(0.45f, top - bot, seg), rot, Atlas.MaterialUV(Atlas.MatConcrete));
            mb.Box(new Vector3(mid.x, top + 0.05f, mid.y), new Vector3(0.6f, 0.1f, seg), rot, Atlas.SolidUV(Pal.TempleRed));
            col.Box(c, new Vector3(0.45f, top - bot, seg), rot, Vector2.zero);
        }
    }
}
