using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    public static partial class WorldBuilder
    {
        sealed class LevelRoad
        {
            public RoadData r;
            public Vector2[] line;
            public List<Vector2> pts;
            public float[] raw, prof;
            public float half, reach;   // reach < 0: a Mall deck (its height is fixed, its ground is not written here)
            public float[] ease;        // how far beyond the edge the ground eases back, per point (wider where the road was moved a lot)
        }

        /// <summary>
        /// Roads level across and smooth along (user, 2026-11-12, with screenshots: a road by the Flats leaned sideways up
        /// the hill like a ramp, and every road went up and down with each bump of the ground). Before any road is built,
        /// the ground along every road is set to the road's own profile: the centre-line height averaged over ±40 m, the
        /// same right across the carriageway, easing back into the natural ground over 2.5 m beyond each edge. Where two
        /// roads' corridors overlap, the nearer road decides. Mall Road's decks keep their own levels (Mall builder).
        /// Everything built afterwards reads these heights (ctx.H samples the terrain), so roads, junctions, kerbs and
        /// houses all agree.
        ///
        /// Junctions (user, 2026-11-14, iPhone bug report at the Flats: "ye real me ek tiraha hai"): a road passing through
        /// a junction kept its smoothed height there while a road ending at it sat on the raw ground, so on the slope they
        /// met up to 1.7 m apart and the slabs tilted over each other; the road forking off down into the Flats ran 2 m
        /// under the road beside it. Now wherever two carriageways touch or overlap in plan, the narrower road takes the
        /// wider one's height for as long as they overlap, then eases back into its own profile.
        /// </summary>
        static void LevelRoads(WorldCtx ctx)
        {
            var t = ctx.terrain;
            var td = t.terrainData;
            int res = td.heightmapResolution;
            var h = td.GetHeights(0, 0, res, res);
            Vector3 tp = t.transform.position, sz = td.size;
            float cx = (res - 1) / sz.x, cz = (res - 1) / sz.z;
            var bestW = new float[res * res];
            var bestH = new float[res * res];
            var bestD = new float[res * res];   // distance to the deciding road's centre line: on a tie the nearer road decides
            for (int k = 0; k < bestD.Length; k++) bestD[k] = float.MaxValue;
            const float Ease = 2.5f, Pin = 12f;

            // 1. every levelled road's smoothed profile
            var list = new List<LevelRoad>();
            foreach (var r in ctx.world.roads)
            {
                if (IsMinor(r.kind) || r.kind == "service" && r.width < 3f) continue;
                if (r.name == MallUpper || r.name == MallLower || IsMallDeck(r.name)) continue;
                var line = Geo.ToPoints(r.pts);
                if (line.Length < 2) continue;
                var pts = Resample(line, 2f, false);
                if (pts.Count < 2) continue;
                int n = pts.Count;
                var raw = new float[n];
                for (int i = 0; i < n; i++) raw[i] = ctx.InLake(pts[i]) ? ctx.lakeY + 0.3f : ctx.H(pts[i]);
                // two passes of a ±10-sample (±20 m) moving average (a smooth bell, about ±40 m in all), shorter at the
                // ends: no hump or dip a car would feel (user, 2026-11-12: roads still went up and down in places)
                var prof = raw;
                for (int pass = 0; pass < 2; pass++)
                {
                    var next = new float[n];
                    for (int i = 0; i < n; i++)
                    {
                        int a = Mathf.Max(0, i - 10), b = Mathf.Min(n - 1, i + 10);
                        float s = 0f;
                        for (int k = a; k <= b; k++) s += prof[k];
                        next[i] = s / (b - a + 1);
                    }
                    prof = next;
                }
                var lr = new LevelRoad { r = r, line = line, pts = pts, raw = raw, prof = prof };
                lr.half = Mathf.Max(1.2f, r.width * 0.5f) + 0.4f;
                lr.reach = lr.half + Ease;
                list.Add(lr);
            }

            // 2. wherever two carriageways touch or overlap in plan (a junction, a fork, two roads side by side) they must be
            // at one height, or one slab hangs over the other. The wider road keeps its profile; the narrower one takes the
            // wider road's height for as long as they overlap, then eases back into its own (12 m, longer for a big step).
            // The Mall decks keep the ground they stand on, so they come first and are not written back.
            foreach (var r in ctx.world.roads)
            {
                if (!IsMallDeck(r.name)) continue;
                var line = Geo.ToPoints(r.pts);
                if (line.Length < 2) continue;
                var pts = Resample(line, 2f, false);
                var raw = new float[pts.Count];
                for (int i = 0; i < pts.Count; i++) raw[i] = ctx.InLake(pts[i]) ? ctx.lakeY + 0.3f : ctx.H(pts[i]);
                list.Add(new LevelRoad { r = r, line = line, pts = pts, raw = raw, prof = raw, half = r.width * 0.5f + 0.4f, reach = -1f });
            }
            var order = new List<int>();
            for (int i = 0; i < list.Count; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                bool ma = list[a].reach < 0f, mb = list[b].reach < 0f;
                if (ma != mb) return ma ? -1 : 1;
                int c = list[b].r.width.CompareTo(list[a].r.width);
                return c != 0 ? c : a.CompareTo(b);
            });
            const float GCell = 8f;
            var grid = new Dictionary<long, List<(int road, int i)>>();
            long GKey(int x, int z) => ((long)x << 32) ^ (uint)z;
            var rank = new int[list.Count];
            for (int o = 0; o < order.Count; o++) rank[order[o]] = o;

            // The Flats (user, 2026-11-14, Street View screenshots of the tiraha by the Jama Masjid): the roads along the
            // ground are almost level with it, behind a low parapet. The DEM smears the hill out to the ground's edge and had
            // these roads 11 to 14 m above it. Any road within 12 m of the Flats takes the ground's level plus 0.9 m (a road
            // into the ground lies on it), and climbs back to its own profile beyond.
            var flats = FlatsPoly(ctx);
            float flatsY = 0f;
            if (flats != null)
            {
                Vector2 fmn = flats[0], fmx = flats[0];
                foreach (var p in flats) { fmn = Vector2.Min(fmn, p); fmx = Vector2.Max(fmx, p); }
                var inner = new List<float>();
                for (float x = fmn.x; x <= fmx.x; x += 4f)
                    for (float z = fmn.y; z <= fmx.y; z += 4f)
                    {
                        var q = new Vector2(x, z);
                        if (Geo.PointInPolygon(flats, q) && Geo.DistanceToPolyline(flats, q, true) > 20f) inner.Add(ctx.H(q));
                    }
                if (inner.Count == 0) flats = null;
                else { inner.Sort(); flatsY = inner[inner.Count / 2]; }
            }

            int roads = 0, pinned = 0, atFlats = 0;
            foreach (int ri in order)
            {
                var lr = list[ri];
                var pts = lr.pts;
                int n = pts.Count;
                if (lr.reach >= 0f)
                {
                    var own = lr.prof;
                    var target = new float[n];
                    var tied = new bool[n];
                    for (int i = 0; i < n; i++)
                    {
                        int gx = Mathf.FloorToInt(pts[i].x / GCell), gz = Mathf.FloorToInt(pts[i].y / GCell);
                        int bestRank = int.MaxValue;
                        float nearD = float.MaxValue;
                        for (int dx = -2; dx <= 2; dx++)
                            for (int dz = -2; dz <= 2; dz++)
                            {
                                if (!grid.TryGetValue(GKey(gx + dx, gz + dz), out var cellList)) continue;
                                foreach (var (orr, oi) in cellList)
                                {
                                    var ot = list[orr];
                                    float d = Vector2.Distance(pts[i], ot.pts[oi]);
                                    if (d > lr.half + ot.half + 0.6f) continue;
                                    if (rank[orr] < bestRank || rank[orr] == bestRank && d < nearD)
                                    {
                                        bestRank = rank[orr];
                                        nearD = d;
                                        target[i] = ot.prof[oi];
                                        tied[i] = true;
                                    }
                                }
                            }
                        if (flats != null)
                        {
                            float fd = Geo.DistanceToPolyline(flats, pts[i], true);
                            bool fin = Geo.PointInPolygon(flats, pts[i]);
                            if (fin || fd < 12f)
                            {
                                target[i] = flatsY + (fin && fd > 6f ? 0.12f : 0.9f);
                                // along the spectators' steps the road stands at their top (user's photo, 2026-11-14)
                                if (InStands(pts[i], out float into) && into < 0.5f && into > -StandsRoadHalf * 2f - 1f) target[i] = flatsY + StandsRise;
                                tied[i] = true;
                                atFlats++;
                            }
                        }
                    }
                    var prof = new float[n];
                    for (int i = 0; i < n; i++)
                    {
                        if (tied[i]) { prof[i] = target[i]; pinned++; continue; }
                        int l = i - 1, rr = i + 1;
                        while (l >= 0 && !tied[l]) l--;
                        while (rr < n && !tied[rr]) rr++;
                        float add = 0f, wl = 0f, wr = 0f, ol = 0f, or2 = 0f;
                        if (l >= 0) { ol = target[l] - own[l]; wl = 1f - Mathf.SmoothStep(0f, 1f, (i - l) * 2f / Mathf.Max(Pin, Mathf.Abs(ol) * 8f)); }
                        if (rr < n) { or2 = target[rr] - own[rr]; wr = 1f - Mathf.SmoothStep(0f, 1f, (rr - i) * 2f / Mathf.Max(Pin, Mathf.Abs(or2) * 8f)); }
                        float ws = wl + wr;
                        if (ws > 1f) { wl /= ws; wr /= ws; }
                        add = ol * wl + or2 * wr;
                        prof[i] = own[i] + add;
                    }
                    // a road moved far from the DEM (the Flats) gets a long bank beside it, not a cliff: 1.5 m out per metre
                    lr.ease = new float[n];
                    for (int i = 0; i < n; i++) lr.ease[i] = Mathf.Max(Ease, Mathf.Abs(prof[i] - own[i]) * 1.5f);
                    lr.prof = prof;
                }
                for (int i = 0; i < n; i++)
                {
                    long k = GKey(Mathf.FloorToInt(pts[i].x / GCell), Mathf.FloorToInt(pts[i].y / GCell));
                    if (!grid.TryGetValue(k, out var cl)) grid[k] = cl = new List<(int, int)>();
                    cl.Add((ri, i));
                }
                if (lr.reach < 0f) continue;   // a Mall deck: its ground is shaped by the Mall builder

                // 3. lay the road's profile on the ground
                {
                    var prof = lr.prof;
                    LayProfile(lr, prof);
                    roads++;
                }
            }

            void LayProfile(LevelRoad lr, float[] prof)
            {
                var pts = lr.pts;
                int n = pts.Count;
                float half = lr.half;
                for (int i = 0; i + 1 < n; i++)
                {
                    Vector2 p0 = pts[i], p1 = pts[i + 1], d = p1 - p0;
                    float len2 = d.sqrMagnitude;
                    if (len2 < 1e-4f) continue;
                    float e0 = lr.ease[i], e1 = lr.ease[i + 1], reach = half + Mathf.Max(e0, e1);
                    int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(p0.x, p1.x) - reach - tp.x) * cx));
                    int x1 = Mathf.Min(res - 1, Mathf.CeilToInt((Mathf.Max(p0.x, p1.x) + reach - tp.x) * cx));
                    int z0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(p0.y, p1.y) - reach - tp.z) * cz));
                    int z1 = Mathf.Min(res - 1, Mathf.CeilToInt((Mathf.Max(p0.y, p1.y) + reach - tp.z) * cz));
                    for (int zi = z0; zi <= z1; zi++)
                        for (int xi = x0; xi <= x1; xi++)
                        {
                            Vector2 q = new Vector2(tp.x + xi / cx, tp.z + zi / cz);
                            float u = Mathf.Clamp01(Vector2.Dot(q - p0, d) / len2);
                            float dist = Vector2.Distance(q, p0 + d * u);
                            if (dist > reach) continue;
                            float ease = Mathf.Lerp(e0, e1, u);
                            if (dist > half + ease) continue;
                            float w = dist <= half ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (dist - half) / ease);
                            int idx = zi * res + xi;
                            // inside two carriageways both weigh 1: the nearer road's centre line decides (the first road
                            // in the list used to win, so a road beside another took its ground; the Flats' service road)
                            if (w < bestW[idx] || w == bestW[idx] && dist >= bestD[idx]) continue;
                            bestW[idx] = w;
                            bestD[idx] = dist;
                            bestH[idx] = Mathf.Lerp(prof[i], prof[i + 1], u);
                        }
                }
            }
            int changed = 0;
            for (int zi = 0; zi < res; zi++)
                for (int xi = 0; xi < res; xi++)
                {
                    int idx = zi * res + xi;
                    float w = bestW[idx];
                    if (w <= 0f) continue;
                    float target = Mathf.Clamp01((bestH[idx] - tp.y) / sz.y);
                    h[zi, xi] = Mathf.Lerp(h[zi, xi], target, w);
                    changed++;
                }
            td.SetHeights(0, 0, h);
            UnityEditor.EditorUtility.SetDirty(td);
            Debug.Log($"[Nainital] roads levelled: {roads} roads, {changed} ground samples set to their road's profile, {pinned} road points tied to the wider road they touch, {atFlats} at the Flats' level ({flatsY:F1} m)");
        }
    }
}
