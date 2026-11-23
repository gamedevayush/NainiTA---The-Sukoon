using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// The Kaladhungi road from DSA ground (the Flats) to Khurpatal as Google Street View shows it (owner, 23 Nov 2026:
    /// "ek ek photo ko dekhke socho ki kya kam ya jyada krna hai... sbko ditto bnaado... building kam jyada ya texture wgera
    /// sab"). 117 Street View panoramas, one every 100 m, were set beside game shots from the same spots
    /// (Shots/kd_compare/index.html). Per panorama and side, Data/streetview_kaladhungi.json (written by
    /// Tools/streetview/kaladhungi_survey.py, which documents every token) says how thick the bushes and the trees by the
    /// road are and which trees, whether a crash barrier or concrete blocks stand on the drop, and which houses, sheds and
    /// stalls stand by the road. The game had a bare earth cut and an open grass edge almost everywhere: the map's trees were
    /// kept 7-13 m off every road and there were no bushes at all.
    /// Runs on the final ground (after the roads are carved), and adds its bushes (TreeField type 7) and trees to the forest.
    /// </summary>
    public static partial class WorldBuilder
    {
        const string SvDataPath = "Assets/Nainital/Data/streetview_kaladhungi.json";

        [Serializable] class SvPoint { public int i; public float m; public string L, R; }
        [Serializable] class SvItem { public int i; public string side, kind, note; public float along, back, w, d, len; public int lv, wall, roof, tarp; public bool flat, shop; }
        [Serializable] class SvRestyle { public float x, z, r; public int wall, roof; }
        [Serializable] class SvData { public float[] line, hw, metres; public SvPoint[] points; public SvItem[] items; public SvRestyle[] restyle; }

        /// <summary>One side of one survey point, parsed.</summary>
        struct SvSide
        {
            public bool town, built, open, rail, blocks, none;
            public int bush, trees;
            public char kind;
        }

        static SvSide ParseSide(string s)
        {
            var r = new SvSide { kind = 'o' };
            if (string.IsNullOrWhiteSpace(s)) { r.built = true; return r; }
            foreach (var tok in s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                switch (tok)
                {
                    case "town": r.town = true; break;
                    case "open": r.open = true; break;
                    case "rail": r.rail = true; break;
                    case "blocks": r.blocks = true; break;
                    case "none": r.none = true; break;
                    case "b1": r.bush = 1; break;
                    case "b2": r.bush = 2; break;
                    case "t1": r.trees = 1; break;
                    case "t2": r.trees = 2; break;
                    case "o": case "d": case "p": case "m": r.kind = tok[0]; break;
                }
            return r;
        }

        /// <summary>The surveyed route: its centre line (as the map had it), metres along it, and a 20 m grid of segments.</summary>
        sealed class SvRoute
        {
            public readonly SvData data;
            public readonly Vector2[] p;
            public readonly float[] m, hw;
            readonly SvSide[] left, right;
            readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();
            const float Cell = 20f;
            static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

            public SvRoute(SvData d)
            {
                data = d;
                p = Geo.ToPoints(d.line);
                m = d.metres;
                hw = d.hw;
                left = new SvSide[d.points.Length];
                right = new SvSide[d.points.Length];
                for (int k = 0; k < d.points.Length; k++) { left[k] = ParseSide(d.points[k].L); right[k] = ParseSide(d.points[k].R); }
                for (int i = 0; i + 1 < p.Length; i++)
                {
                    Vector2 lo = Vector2.Min(p[i], p[i + 1]) - Vector2.one * 30f, hi = Vector2.Max(p[i], p[i + 1]) + Vector2.one * 30f;
                    for (int x = Mathf.FloorToInt(lo.x / Cell); x <= Mathf.FloorToInt(hi.x / Cell); x++)
                        for (int z = Mathf.FloorToInt(lo.y / Cell); z <= Mathf.FloorToInt(hi.y / Cell); z++)
                        {
                            long k = Key(x, z);
                            if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<int>();
                            l.Add(i);
                        }
                }
            }

            /// <summary>The nearest point of the line to q (within ~30 m): metres along it, its direction of travel, the distance
            /// and whether q is on the left of the travel.</summary>
            public bool Locate(Vector2 q, out float metres, out Vector2 tan, out float dist, out bool isLeft, out float half)
            {
                metres = 0f; tan = Vector2.up; dist = float.MaxValue; isLeft = false; half = 3f;
                if (!grid.TryGetValue(Key(Mathf.FloorToInt(q.x / Cell), Mathf.FloorToInt(q.y / Cell)), out var l)) return false;
                int best = -1; float bt = 0f;
                foreach (int i in l)
                {
                    Vector2 a = p[i], ab = p[i + 1] - a;
                    float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                    float dd = (q - (a + ab * t)).sqrMagnitude;
                    if (dd < dist) { dist = dd; best = i; bt = t; }
                }
                if (best < 0) return false;
                dist = Mathf.Sqrt(dist);
                Vector2 seg = p[best + 1] - p[best];
                tan = seg.normalized;
                metres = Mathf.Lerp(m[best], m[best + 1], bt);
                half = Mathf.Lerp(hw[best], hw[best + 1], bt);
                Vector2 leftN = new Vector2(-tan.y, tan.x);
                isLeft = Vector2.Dot(q - (p[best] + seg * bt), leftN) > 0f;
                return true;
            }

            /// <summary>The point at a distance along the line, its direction and half width.</summary>
            public Vector2 At(float metres, out Vector2 tan, out float half)
            {
                int i = Array.BinarySearch(m, metres);
                if (i < 0) i = ~i - 1;
                i = Mathf.Clamp(i, 0, p.Length - 2);
                float t = Mathf.Clamp01((metres - m[i]) / Mathf.Max(m[i + 1] - m[i], 1e-4f));
                tan = (p[i + 1] - p[i]).normalized;
                half = Mathf.Lerp(hw[i], hw[i + 1], t);
                return Vector2.Lerp(p[i], p[i + 1], t);
            }

            public int PointIndex(float metres)
            {
                int best = 0;
                for (int k = 1; k < data.points.Length; k++)
                    if (Mathf.Abs(data.points[k].m - metres) < Mathf.Abs(data.points[best].m - metres)) best = k;
                return best;
            }

            public SvSide Side(float metres, bool isLeft) { int k = PointIndex(metres); return isLeft ? left[k] : right[k]; }
        }

        static SvRoute svRoute;
        static WorldCtx svRouteFor;

        static SvRoute StreetViewRoute(WorldCtx ctx)
        {
            if (svRouteFor == ctx) return svRoute;
            svRouteFor = ctx;
            svRoute = null;
            if (!File.Exists(SvDataPath)) { Debug.LogWarning("[Nainital] Street View road: " + SvDataPath + " missing (Tools/streetview/kaladhungi_survey.py)"); return null; }
            var d = JsonUtility.FromJson<SvData>(File.ReadAllText(SvDataPath));
            if (d?.line == null || d.points == null || d.line.Length < 4) return null;
            svRoute = new SvRoute(d);
            return svRoute;
        }

        /// <summary>BuildRoadWalls asks: may the whitewashed parapet stand here? Not where the photos show a crash barrier,
        /// concrete blocks, or nothing (this step builds the barrier itself).</summary>
        static bool SvParapetAllowed(WorldCtx ctx, Vector2 edge)
        {
            var r = StreetViewRoute(ctx);
            if (r == null || !r.Locate(edge, out float metres, out _, out float dist, out bool isLeft, out float half)) return true;
            if (dist > half + 4f) return true;
            var s = r.Side(metres, isLeft);
            return !(s.rail || s.blocks || s.none);
        }

        /// <summary>BuildBuildings asks: does a map building here wear the colours of the photos?</summary>
        static void SvRestyleBuilding(WorldCtx ctx, Vector2 c, ref int wall, ref int roof)
        {
            var r = StreetViewRoute(ctx);
            if (r?.data.restyle == null) return;
            foreach (var s in r.data.restyle)
                if ((c - new Vector2(s.x, s.z)).sqrMagnitude < s.r * s.r) { wall = s.wall; roof = s.roof; return; }
        }

        // ------------------------------------------------------------------ the step
        static void BuildStreetViewRoad(WorldCtx ctx)
        {
            var route = StreetViewRoute(ctx);
            if (route == null) return;
            var parent = new GameObject("Kaladhungi road as on Street View").transform;
            parent.SetParent(ctx.worldRoot, false);
            var rng = new System.Random(2311);
            var made = new List<(Vector2 c, float r)>();
            int items = SvItems(ctx, route, parent, rng, made);
            Physics.SyncTransforms();
            SvBarriers(ctx, route, parent, out float railM, out int blocks);
            Physics.SyncTransforms();
            int painted = SvPaintGround(ctx, route);
            SvGreen(ctx, route, rng, made, out int bushes, out int trees, out int cleared);
            Debug.Log($"[Nainital] Street View road (Kaladhungi): {items} roadside buildings / stalls / walls, {railM:F0} m of crash barrier, " +
                      $"{blocks} concrete blocks, {bushes} bushes, {trees} trees by the road, {cleared} trees cleared (open ground, new buildings), " +
                      $"{painted} ground texels painted green");
        }

        /// <summary>
        /// The verges and hill cuts green, as in every photo (monsoon grass, fern and moss), where the game painted bare
        /// earth and rock: grass and forest floor within 14 m of the road edge, fading out by 22 m; some rock stays on the
        /// steepest faces. The game's grass then grows there too (GrassRenderer reads these weights).
        /// </summary>
        static int SvPaintGround(WorldCtx ctx, SvRoute route)
        {
            var td = ctx.terrain.terrainData;
            if (td.alphamapLayers < 4) return 0;
            int ar = td.alphamapResolution;
            Vector3 size = td.size, tp = ctx.terrain.transform.position;
            Vector2 lo = Vector2.one * float.MaxValue, hi = -lo;
            foreach (var q in route.p) { lo = Vector2.Min(lo, q); hi = Vector2.Max(hi, q); }
            lo -= Vector2.one * 30f; hi += Vector2.one * 30f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((lo.x - tp.x) / size.x * (ar - 1)), 0, ar - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((hi.x - tp.x) / size.x * (ar - 1)), 0, ar - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((lo.y - tp.z) / size.z * (ar - 1)), 0, ar - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((hi.y - tp.z) / size.z * (ar - 1)), 0, ar - 1);
            var roads = Clearance(ctx);
            int painted = 0, w = x1 - x0 + 1;
            const int Band = 128;
            for (int zb = z0; zb <= z1; zb += Band)
            {
                int h = Mathf.Min(Band, z1 - zb + 1);
                var al = td.GetAlphamaps(x0, zb, w, h);
                int n = al.GetLength(2);
                bool any = false;
                for (int z = 0; z < h; z++)
                    for (int x = 0; x < w; x++)
                    {
                        var p = new Vector2(tp.x + (x0 + x) / (float)(ar - 1) * size.x, tp.z + (zb + z) / (float)(ar - 1) * size.z);
                        if (!route.Locate(p, out float metres, out _, out float d, out bool isLeft, out float half)) continue;
                        float e = d - half;
                        if (e < 0.2f || e > 22f) continue;
                        var sd = route.Side(metres, isLeft);
                        if (sd.town || sd.built || (sd.bush == 0 && sd.trees == 0 && !sd.open)) continue;
                        if (roads.Blocked(p, 0.3f) || ctx.InKeepClear(p) || ctx.InLake(p)) continue;
                        float gx = ctx.H(p + new Vector2(1.5f, 0f)) - ctx.H(p - new Vector2(1.5f, 0f));
                        float gz = ctx.H(p + new Vector2(0f, 1.5f)) - ctx.H(p - new Vector2(0f, 1.5f));
                        float slope = Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz) / 3f) * Mathf.Rad2Deg;
                        Vector4 t = sd.open ? new Vector4(0.85f, 0.15f, 0f, 0f)
                                  : slope < 35f ? new Vector4(0.72f, 0.28f, 0f, 0f)
                                  : slope < 55f ? new Vector4(0.55f, 0.33f, 0.12f, 0f)
                                  : new Vector4(0.38f, 0.3f, 0.32f, 0f);
                        float f = e < 14f ? 1f : Mathf.InverseLerp(22f, 14f, e);
                        for (int k = 0; k < n; k++) al[z, x, k] *= 1f - f;
                        al[z, x, 0] += f * t.x; al[z, x, 1] += f * t.y; al[z, x, 2] += f * t.z; al[z, x, 3] += f * t.w;
                        any = true;
                        painted++;
                    }
                if (any) td.SetAlphamaps(x0, zb, al);
            }
            return painted;
        }

        /// <summary>The map's roads this route runs on, sampled every metre with the side normals; per sample the survey's
        /// metres, side and the road edge.</summary>
        static IEnumerable<(Vector2 q, Vector2 n, Vector2 tan, float half, float metres, bool isLeft, int road)> SvSamples(WorldCtx ctx, SvRoute route, float step)
        {
            for (int ri = 0; ri < ctx.world.roads.Length; ri++)
            {
                var r = ctx.world.roads[ri];
                if (!VehicleKinds.Contains(r.kind)) continue;
                var line = Geo.ToPoints(r.pts);
                if (line.Length < 2) continue;
                // on the route? most of its 10 m samples within 4 m of the surveyed line
                var coarse = Resample(line, 10f, false);
                int near = 0;
                foreach (var q in coarse) if (route.Locate(q, out _, out _, out float d0, out _, out _) && d0 < 4f) near++;
                if (near < 3 || near < coarse.Count * 0.5f) continue;
                var path = Resample(line, step, false);
                float half = r.width * 0.5f;
                for (int i = 0; i < path.Count; i++)
                {
                    Vector2 q = path[i];
                    if (!route.Locate(q, out float metres, out var rtan, out float d, out _, out _) || d > 5f) continue;
                    Vector2 tan = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Vector2 n = new Vector2(-tan.y, tan.x) * side;
                        bool isLeft = Vector2.Dot(n, new Vector2(-rtan.y, rtan.x)) > 0f;
                        yield return (q, n, tan, half, metres, isLeft, ri);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ crash barriers and concrete blocks
        static void SvBarriers(WorldCtx ctx, SvRoute route, Transform parent, out float railM, out int blocks)
        {
            var mb = new MallChunks(ctx, parent, "Crash barrier", ctx.mats.atlas, true, 0.04f);
            Vector2 beam = Atlas.ExtraUV(Px.Chrome), post = Atlas.ExtraUV(Px.Rim), white = Atlas.WallSolid(0), conc = Atlas.MaterialUV(Atlas.MatConcrete);
            var roads = Clearance(ctx);
            railM = 0f; blocks = 0;
            // per road and side: consecutive samples on a drop where the survey says rail / blocks
            var runs = new Dictionary<(int road, float side), List<(Vector2 e, Vector2 tan, Vector2 n, float y, bool rail)>>();
            foreach (var s in SvSamples(ctx, route, 1f))
            {
                var sd = route.Side(s.metres, s.isLeft);
                if (!(sd.rail || sd.blocks) || sd.town || sd.built) continue;
                Vector2 e = s.q + s.n * (s.half + 0.45f);
                // only along a drop (the road's bank can be level for a few metres before it falls away)
                float h0 = ctx.H(s.q);
                if (ctx.H(s.q + s.n * (s.half + 3f)) - h0 > -0.4f && ctx.H(s.q + s.n * (s.half + 6f)) - h0 > -0.5f) continue;
                if (roads.Blocked(e, 0.1f) || NearJunction(ctx, e) || ctx.InKeepClear(e, 0.5f) || ctx.InLake(e)) continue;
                var key = (s.road, Mathf.Sign(Vector2.Dot(s.n, new Vector2(-s.tan.y, s.tan.x))));
                if (!runs.TryGetValue(key, out var list)) runs[key] = list = new List<(Vector2, Vector2, Vector2, float, bool)>();
                list.Add((e, s.tan, s.n, ctx.H(e), sd.rail));
            }
            foreach (var kv in runs)
            {
                var list = kv.Value;
                float sinceBlock = 99f;
                for (int i = 0; i < list.Count; i++)
                {
                    var a = list[i];
                    var c = mb.At(a.e);
                    if (a.rail)
                    {
                        if (i % 2 == 0) c[0].Box(new Vector3(a.e.x, a.y + 0.38f, a.e.y), new Vector3(0.1f, 0.8f, 0.12f), Quaternion.LookRotation(Flat(a.tan)), post);
                        if (i + 1 < list.Count && list[i + 1].rail && (list[i + 1].e - a.e).sqrMagnitude < 2.5f * 2.5f)
                        {
                            var b = list[i + 1];
                            Vector2 ea = a.e - a.n * 0.08f, eb = b.e - b.n * 0.08f;   // the beam on the road side of the posts
                            Vector3 toward = Flat(-a.n);
                            c[0].Quad(V(ea, a.y + 0.42f), V(eb, b.y + 0.42f), V(eb, b.y + 0.76f), V(ea, a.y + 0.76f), beam, toward);
                            c[0].Quad(V(eb, b.y + 0.42f), V(ea, a.y + 0.42f), V(ea, a.y + 0.76f), V(eb, b.y + 0.76f), post, -toward);
                            c[0].Quad(V(ea, a.y + 0.59f) + toward * 0.04f, V(eb, b.y + 0.59f) + toward * 0.04f, V(eb, b.y + 0.62f) + toward * 0.04f, V(ea, a.y + 0.62f) + toward * 0.04f, post, toward);
                            BlockBothWays(c[1], V(ea, a.y - 0.2f), V(ea, a.y + 0.95f), V(eb, b.y + 0.95f), V(eb, b.y - 0.2f), toward);
                            railM += Vector2.Distance(a.e, b.e);
                        }
                    }
                    else
                    {
                        sinceBlock += i > 0 ? Vector2.Distance(list[i - 1].e, a.e) : 99f;
                        if (sinceBlock < 2.8f) continue;
                        sinceBlock = 0f;
                        var rot = Quaternion.LookRotation(Flat(a.tan));
                        var p3 = new Vector3(a.e.x, a.y + 0.22f, a.e.y);
                        var size = new Vector3(0.45f, 0.6f, 1.15f);
                        c[0].Box(p3, size, rot, blocks % 3 == 2 ? conc : white);
                        c[1].Box(p3, size, rot, Vector2.zero);
                        blocks++;
                    }
                }
            }
            mb.Flush();
        }

        // ------------------------------------------------------------------ bushes, trees, open ground
        static void SvGreen(WorldCtx ctx, SvRoute route, System.Random rng, List<(Vector2 c, float r)> made,
                            out int bushes, out int trees, out int cleared)
        {
            bushes = trees = cleared = 0;
            var field = AssetDatabase.LoadAssetAtPath<TreeField>(Gen + "/TreeField.asset");
            if (!field || field.lod0 == null || field.lod0.Length < 8) { Debug.LogWarning("[Nainital] Street View road: no TreeField with the bush type"); return; }
            var data = new List<float>(field.data);

            // 1. open ground and the new buildings' footprints: the tall trees go (one in eight stays on the open ground)
            var keep = new List<float>(data.Count);
            for (int i = 0; i + 5 < data.Count; i += 6)
            {
                var p = new Vector2(data[i], data[i + 2]);
                int type = (int)data[i + 3];
                bool drop = false;
                if (type <= 4)
                {
                    foreach (var mk in made) if ((p - mk.c).sqrMagnitude < mk.r * mk.r) { drop = true; break; }
                    if (!drop && route.Locate(p, out float metres, out _, out float d, out bool isLeft, out float half) && d < half + 150f)
                    {
                        // open ground: everything within 70 m, and the flat meadow out to 150 m (the hills keep their forest)
                        var sd = route.Side(metres, isLeft);
                        if (sd.open)
                        {
                            bool flat = Mathf.Abs(ctx.H(p + new Vector2(4f, 0f)) - ctx.H(p - new Vector2(4f, 0f))) < 2.1f
                                     && Mathf.Abs(ctx.H(p + new Vector2(0f, 4f)) - ctx.H(p - new Vector2(0f, 4f))) < 2.1f;   // under ~15 degrees
                            if ((d < half + 70f || flat) && rng.NextDouble() < 0.95) drop = true;
                        }
                    }
                }
                if (drop) { cleared++; continue; }
                for (int k = 0; k < 6; k++) keep.Add(data[i + k]);
            }
            data = keep;

            // trees already standing: a 3 m grid
            var taken = new HashSet<long>();
            long Cell3(Vector2 p) => ((long)Mathf.FloorToInt(p.x / 3f) << 32) ^ (uint)Mathf.FloorToInt(p.y / 3f);
            for (int i = 0; i + 5 < data.Count; i += 6) if (data[i + 3] <= 4) taken.Add(Cell3(new Vector2(data[i], data[i + 2])));

            var roads = Clearance(ctx);
            int mask = ~((1 << 8) | (1 << 9) | (1 << 2) | (1 << 10) | (1 << 4));
            bool Standing(Vector2 q, float y, float r)
            {
                foreach (var col in Physics.OverlapSphere(new Vector3(q.x, y + r + 0.3f, q.y), r, mask, QueryTriggerInteraction.Ignore))
                    if (col is not TerrainCollider) return true;
                return false;
            }
            bool InMade(Vector2 q) { foreach (var mk in made) if ((q - mk.c).sqrMagnitude < mk.r * mk.r) return true; return false; }

            foreach (var s in SvSamples(ctx, route, 1f))
            {
                var sd = route.Side(s.metres, s.isLeft);
                if (sd.town || sd.built) continue;
                Vector2 edge = s.q + s.n * s.half;
                float rise = ctx.H(edge + s.n * 3f) - ctx.H(edge);
                bool up = rise > 0.6f, down = rise < -0.6f;
                // bushes: a deep band on a hill cut, a narrow one on the drop
                if (sd.bush > 0)
                {
                    float rate = sd.bush == 2 ? (up ? 1.7f : 1.2f) : 0.45f;
                    float far = (up ? 8.5f : down ? 4.5f : 6f) * (sd.bush == 2 ? 1f : 0.65f);
                    int n = (int)rate + (rng.NextDouble() < rate - (int)rate ? 1 : 0);
                    for (int k = 0; k < n; k++)
                    {
                        // more of them low on the cut, where the road sees them
                        float off = Mathf.Lerp(up ? 0.4f : 0.8f, far, Mathf.Pow((float)rng.NextDouble(), 1.4f));
                        Vector2 q = edge + s.n * off + s.tan * (float)(rng.NextDouble() - 0.5);
                        if (roads.Blocked(q, 0.7f) || ctx.InKeepClear(q, 0.5f) || ctx.InLake(q) || InMade(q)) continue;
                        float y = ctx.H(q);
                        if (Standing(q, y, 0.6f)) continue;
                        float sc = sd.bush == 2 ? 0.7f + (float)rng.NextDouble() * 0.7f : 0.55f + (float)rng.NextDouble() * 0.5f;
                        data.Add(q.x); data.Add(y - 0.15f * sc); data.Add(q.y);
                        data.Add(7); data.Add(sc); data.Add((float)rng.NextDouble() * 360f);
                        bushes++;
                    }
                }
                // trees close by the road
                if (sd.trees > 0)
                {
                    float rate = sd.trees == 2 ? 0.16f : 0.07f;
                    if (rng.NextDouble() < rate)
                    {
                        float off = 2.5f + Mathf.Pow((float)rng.NextDouble(), 1.4f) * 11f;
                        Vector2 q = edge + s.n * off;
                        long cell = Cell3(q);
                        if (!taken.Contains(cell) && !roads.Blocked(q, 1.8f) && !ctx.InKeepClear(q, 1f) && !ctx.InLake(q) && !InMade(q))
                        {
                            float y = ctx.H(q);
                            if (!Standing(q, y + 1f, 1.2f))
                            {
                                double r = rng.NextDouble();
                                int type = sd.kind == 'd' ? (r < 0.8 ? 0 : 2) : sd.kind == 'p' ? (r < 0.85 ? 1 : 2) : sd.kind == 'm' ? (r < 0.5 ? 0 : 2) : (r < 0.8 ? 2 : 0);
                                if (type == 2 && rng.NextDouble() < 0.12) type = 4;   // a buransh among the oaks
                                data.Add(q.x); data.Add(y - 0.2f); data.Add(q.y);
                                data.Add(type); data.Add(0.75f + (float)rng.NextDouble() * 0.4f); data.Add((float)rng.NextDouble() * 360f);
                                taken.Add(cell);
                                trees++;
                            }
                        }
                    }
                }
            }
            field.data = data.ToArray();
            EditorUtility.SetDirty(field);
            var tr = UnityEngine.Object.FindAnyObjectByType<TreeRenderer>();
            if (tr) { tr.enabled = false; tr.enabled = true; tr.gameObject.name = "Forest (" + field.Count + " trees and bushes, GPU instanced)"; }
        }

        // ------------------------------------------------------------------ roadside buildings, sheds, stalls, walls
        static int SvItems(WorldCtx ctx, SvRoute route, Transform parent, System.Random rng, List<(Vector2 c, float r)> made)
        {
            if (route.data.items == null) return 0;
            var mb = new MeshBuilder();
            var col = new MeshBuilder();
            var mossMat = MossyStoneMaterial();
            var moss = new MallChunks(ctx, parent, "Roadside terrace walls", mossMat ? mossMat : ctx.mats.atlas, true, 0.05f);
            var roads = Clearance(ctx);
            int mask = ~((1 << 8) | (1 << 9) | (1 << 2) | (1 << 10) | (1 << 4));
            int[] tarps = { Pal.BoatBlue, Pal.BoatRed, Pal.BoatYellow, Pal.JacketGreen };
            int count = 0;
            foreach (var it in route.data.items)
            {
                if (it.i < 0 || it.i >= route.data.points.Length) continue;
                float metres = route.data.points[it.i].m + it.along;
                Vector2 c0 = route.At(metres, out var tan, out float half);
                Vector2 n = new Vector2(-tan.y, tan.x) * (it.side == "L" ? 1f : -1f);
                float back = it.back;
                // never on a carriageway (a hairpin's other leg, a rounded corner): step back until clear
                Vector2 Front() => c0 + n * (half + back);
                for (int k = 0; k < 8 && (roads.Blocked(Front() + tan * (it.w * 0.5f), 0.3f) || roads.Blocked(Front() - tan * (it.w * 0.5f), 0.3f)); k++) back += 1f;
                Vector2 fc = Front();
                Vector2 F = -n;
                float yaw = Mathf.Atan2(F.x, F.y) * Mathf.Rad2Deg;
                string tag = $"#{it.i:000}{it.side} {it.kind} {it.note}";
                int madeBefore = made.Count;
                switch (it.kind)
                {
                    case "house":
                    {
                        Vector2 c = fc + n * (it.d * 0.5f);
                        var q = Quaternion.Euler(0f, yaw, 0f);
                        float gy = ctx.H(c);
                        {   // still across a road after stepping back (another lane, a hairpin's leg): leave it out (23 Nov)
                            Vector3 r3 = q * Vector3.right * (it.w * 0.42f), f3 = q * Vector3.forward * (it.d * 0.42f);
                            Vector2 rr = new Vector2(r3.x, r3.z), ff = new Vector2(f3.x, f3.z);
                            if (roads.Blocked(c, 0.3f) || roads.Blocked(c + rr + ff, 0f) || roads.Blocked(c + rr - ff, 0f) || roads.Blocked(c - rr + ff, 0f) || roads.Blocked(c - rr - ff, 0f))
                            { Debug.Log($"[Nainital] Street View road: {tag} skipped, on a road"); continue; }
                        }
                        bool clash = false;
                        foreach (var cl in Physics.OverlapBox(new Vector3(c.x, gy + 3f, c.y), new Vector3(it.w * 0.5f - 0.4f, 2f, it.d * 0.5f - 0.4f), q, mask, QueryTriggerInteraction.Ignore))
                        {
                            if (cl is TerrainCollider) continue;
                            string cn = cl.name;   // a lamp post, a pole, a sign or a road wall in the way does not stop a house
                            if (cn.Contains("Light") || cn.Contains("Lamp") || cn.Contains("Pole") || cn.Contains("Wire") || cn.Contains("Sign") || cn.Contains("Road walls") || cn.Contains("Info:")) continue;
                            clash = true; Debug.Log($"[Nainital] Street View road: {tag} skipped, '{cn}' stands there"); break;
                        }
                        if (clash) continue;
                        Vector3 R3 = q * Vector3.right * (it.w * 0.5f), F3 = q * Vector3.forward * (it.d * 0.5f);
                        Vector2 R = new Vector2(R3.x, R3.z), Fv = new Vector2(F3.x, F3.z);
                        float gMin = float.MaxValue;
                        for (int gu = -2; gu <= 2; gu++)
                            for (int gv = -2; gv <= 2; gv++) gMin = Mathf.Min(gMin, ctx.H(c + R * (gu * 0.5f) + Fv * (gv * 0.5f)));
                        // the floor at the road's level: a house on the hill side is cut into it, as they are
                        float gFront = Mathf.Max(ctx.H(c + Fv - R), Mathf.Max(ctx.H(c + Fv + R), ctx.H(c + Fv)));
                        var st = new BuildingKit.Style { wall = Mathf.Clamp(it.wall, 0, Atlas.WallColors - 1), roof = Mathf.Clamp(it.roof, 0, Atlas.RoofColors - 1), flatRoof = it.flat, shop = it.shop, litChance = 0.45f };
                        BuildingKit.House(mb, col, c, it.w, it.d, yaw, Mathf.Max(1, it.lv), gMin, gFront + 0.3f, st, rng);
                        made.Add((c, Mathf.Sqrt(it.w * it.w + it.d * it.d) * 0.5f + 0.5f));
                        break;
                    }
                    case "stall":
                    case "shop":
                    {
                        Vector2 c = fc + n * 1.6f;
                        if (roads.Blocked(c, 0.6f)) { Debug.Log($"[Nainital] Street View road: {tag} skipped, on a road"); continue; }
                        mb.M = Matrix4x4.TRS(new Vector3(c.x, ctx.H(c), c.y), Quaternion.identity, Vector3.one);
                        if (it.kind == "stall") LandmarkBuilder.TarpStall(mb, Vector3.zero, yaw, tarps[Mathf.Abs(it.tarp) % tarps.Length], rng);
                        else LandmarkBuilder.HillShop(mb, Vector3.zero, yaw, Mathf.Max(2.5f, it.w), rng);
                        mb.M = Matrix4x4.identity;
                        col.Box(new Vector3(c.x, ctx.H(c) + 1.2f, c.y), new Vector3(3f, 2.4f, 2.4f), Quaternion.Euler(0f, yaw, 0f), Vector2.zero);
                        made.Add((c, 2.4f));
                        break;
                    }
                    case "chairs":
                    {
                        Vector2 white = Atlas.SolidUV(Pal.ShirtWhite);
                        for (int k = 0; k < 5; k++)
                        {
                            Vector2 c = fc + n * (0.8f + (float)rng.NextDouble() * 2f) + tan * (k * 1.1f - 2.2f);
                            float y = ctx.H(c);
                            var rot = Quaternion.Euler(0f, yaw + (float)(rng.NextDouble() - 0.5) * 60f, 0f);
                            mb.Box(new Vector3(c.x, y + 0.44f, c.y), new Vector3(0.46f, 0.05f, 0.44f), rot, white);
                            mb.Box(new Vector3(c.x, y + 0.7f, c.y) + rot * Vector3.back * 0.21f, new Vector3(0.46f, 0.5f, 0.04f), rot, white);
                            foreach (float lx in new[] { -0.2f, 0.2f })
                                foreach (float lz in new[] { -0.18f, 0.18f })
                                    mb.Box(new Vector3(c.x, y + 0.21f, c.y) + rot * new Vector3(lx, 0f, lz), new Vector3(0.04f, 0.42f, 0.04f), rot, white);
                        }
                        made.Add((fc + n * 1.8f, 3.5f));
                        break;
                    }
                    case "kiosk":
                    {
                        Vector2 c = fc + n * 1.2f;
                        float y = ctx.H(c);
                        var rot = Quaternion.Euler(0f, yaw, 0f);
                        mb.Box(new Vector3(c.x, y + 1.25f, c.y), new Vector3(3.2f, 2.5f, 2.1f), rot, Atlas.SolidUV(Pal.SareeOrange));
                        mb.Box(new Vector3(c.x, y + 2.6f, c.y), new Vector3(3.6f, 0.15f, 2.6f), rot, Atlas.SolidUV(Pal.JacketRed));
                        mb.Box(new Vector3(c.x, y + 3.0f, c.y) + rot * Vector3.forward * 1.0f, new Vector3(3.2f, 0.6f, 0.06f), rot, Atlas.SolidUV(Pal.BoatYellow));
                        mb.Box(new Vector3(c.x, y + 1.2f, c.y) + rot * Vector3.forward * 1.06f, new Vector3(2.2f, 1f, 0.02f), rot, Atlas.SolidUV(Pal.IronBlack));   // the hatch
                        col.Box(new Vector3(c.x, y + 1.25f, c.y), new Vector3(3.2f, 2.5f, 2.1f), rot, Vector2.zero);
                        made.Add((c, 2.4f));
                        break;
                    }
                    case "shed":
                    case "thatch":
                    {
                        float w = Mathf.Max(3f, it.w), d = Mathf.Max(2.5f, it.d);
                        Vector2 c = fc + n * (d * 0.5f);
                        float y = ctx.H(fc);
                        var rot = Quaternion.Euler(0f, yaw, 0f);
                        Vector2 wood = Atlas.SolidUV(Pal.WoodDark);
                        int posts = Mathf.Max(2, Mathf.RoundToInt(w / 2.5f)) + 1;
                        float eave = it.kind == "thatch" ? 2.3f : 2.6f;
                        for (int k = 0; k < posts; k++)
                            foreach (float zz in new[] { -0.5f, 0.5f })
                            {
                                Vector3 pc = rot * new Vector3((k / (float)(posts - 1) - 0.5f) * (w - 0.3f), 0f, zz * (d - 0.3f));
                                float gy = ctx.H(c + new Vector2(pc.x, pc.z));
                                float top = y + eave;
                                mb.Box(new Vector3(c.x + pc.x, (gy - 0.3f + top) * 0.5f, c.y + pc.z), new Vector3(0.14f, top - gy + 0.3f, 0.14f), rot, wood);
                            }
                        // a back wall of planks or tin, and a counter in front
                        Vector3 bw = rot * new Vector3(0f, 0f, -d * 0.5f + 0.1f);
                        mb.Box(new Vector3(c.x + bw.x, y + eave * 0.5f, c.y + bw.z), new Vector3(w, eave, 0.08f), rot, it.kind == "thatch" ? Atlas.SolidUV(Pal.WoodLight) : Atlas.Roof(Mathf.Clamp(it.roof, 0, Atlas.RoofColors - 1)).center);
                        var saved = mb.M;
                        mb.M = Matrix4x4.TRS(new Vector3(c.x, y + eave, c.y), rot * (w >= d ? Quaternion.identity : Quaternion.Euler(0, 90, 0)), Vector3.one);
                        Rect roofUV = it.kind == "thatch" ? new Rect(Atlas.SolidUV(Pal.PantsKhaki), Vector2.zero) : Atlas.Roof(Mathf.Clamp(it.roof, 0, Atlas.RoofColors - 1));
                        mb.GableRoof(Vector3.zero, Mathf.Max(w, d), Mathf.Min(w, d), Mathf.Min(w, d) * (it.kind == "thatch" ? 0.42f : 0.22f), roofUV, wood, it.kind == "thatch" ? 0.6f : 0.35f);
                        mb.M = saved;
                        col.Box(new Vector3(c.x + bw.x, y + eave * 0.5f, c.y + bw.z), new Vector3(w, eave, 0.2f), rot, Vector2.zero);
                        made.Add((c, Mathf.Sqrt(w * w + d * d) * 0.5f + 0.5f));
                        break;
                    }
                    case "arch":
                    {
                        Vector2 c = fc + n * 0.5f;
                        float y = ctx.H(c);
                        var rot = Quaternion.Euler(0f, yaw, 0f);
                        Vector3 right = rot * Vector3.right;
                        Vector2 white = Atlas.SolidUV(Pal.TempleWhite);
                        const float span = 2.6f, ht = 3.0f;
                        foreach (float sx in new[] { -1f, 1f })
                        {
                            Vector3 bc = new Vector3(c.x, y, c.y) + right * (sx * span * 0.5f);
                            foreach (float a in new[] { -0.15f, 0.15f })
                                foreach (float b in new[] { -0.15f, 0.15f })
                                    mb.Box(bc + right * a + rot * Vector3.forward * b + Vector3.up * ht * 0.5f, new Vector3(0.05f, ht, 0.05f), rot, white);
                            for (float hy = 0.3f; hy < ht; hy += 0.45f)
                                mb.Box(bc + Vector3.up * hy, new Vector3(0.34f, 0.04f, 0.34f), rot, white);
                        }
                        Vector3 mid = new Vector3(c.x, y + ht, c.y);
                        for (int k = 0; k < 10; k++)
                        {
                            float a0 = Mathf.PI * k / 10f, a1 = Mathf.PI * (k + 1) / 10f;
                            Vector3 p0 = mid + right * (Mathf.Cos(a0) * span * 0.5f) + Vector3.up * (Mathf.Sin(a0) * 0.9f);
                            Vector3 p1 = mid + right * (Mathf.Cos(a1) * span * 0.5f) + Vector3.up * (Mathf.Sin(a1) * 0.9f);
                            mb.Limb(p0, p1, 0.09f, 0.09f, 5, white);
                        }
                        made.Add((c, 2f));
                        break;
                    }
                    case "terrace":
                    {
                        // two tiers of mossy rubble wall along the road, whitewashed pillars in the lower one (forest department)
                        float len = Mathf.Max(6f, it.len);
                        Vector2 white = Atlas.WallSolid(0);
                        foreach (float tierBack in new[] { it.back, it.back + 2.2f })
                        {
                            float u = 0f;
                            Vector2 prev = Vector2.zero; float prevY = 0f; bool has = false;
                            for (float s = -len * 0.5f; s <= len * 0.5f + 0.01f; s += 2f)
                            {
                                Vector2 cl = route.At(metres + s, out var t2, out float h2);
                                Vector2 n2 = new Vector2(-t2.y, t2.x) * (it.side == "L" ? 1f : -1f);
                                Vector2 e = cl + n2 * (h2 + tierBack);
                                if (roads.Blocked(e, 0.5f)) { has = false; continue; }
                                float ey = ctx.H(e);
                                if (has)
                                {
                                    var ch = moss.At(e);
                                    float seg = Vector2.Distance(prev, e);
                                    WallFace(ch[0], prev, e, prevY - 0.4f, ey - 0.4f, prevY + 1.1f, ey + 1.1f, u, u + seg, Flat(-n2), 2.5f);
                                    ch[0].Quad(V(prev, prevY + 1.1f), V(e, ey + 1.1f), V(e + n2 * 0.4f, ey + 1.1f), V(prev + n2 * 0.4f, prevY + 1.1f), Atlas.MaterialUV(Atlas.MatDarkStone), Vector3.up);
                                    BlockBothWays(ch[1], V(prev, prevY - 0.4f), V(prev, prevY + 1.2f), V(e, ey + 1.2f), V(e, ey - 0.4f), Flat(-n2));
                                    u += seg;
                                    if (tierBack == it.back && Mathf.Repeat(s + len * 0.5f, 8f) < 2f)
                                        mb.Box(new Vector3(e.x, ey + 0.55f, e.y) - Flat(n2) * 0.05f, new Vector3(0.7f, 1.2f, 0.5f), Quaternion.LookRotation(Flat(t2)), white);
                                }
                                prev = e; prevY = ey; has = true;
                            }
                        }
                        Vector2 bc0 = route.At(metres, out var bt, out float bh);
                        Vector2 bn = new Vector2(-bt.y, bt.x) * (it.side == "L" ? 1f : -1f);
                        Vector2 board = bc0 + bn * (bh + it.back + 2.2f);
                        float by = ctx.H(board);
                        mb.Box(new Vector3(board.x, by + 1.35f, board.y) - Flat(bn) * 0.25f, new Vector3(1.4f, 0.55f, 0.06f), Quaternion.LookRotation(Flat(-bn)), Atlas.SolidUV(Pal.JacketGreen));   // the board
                        break;
                    }
                    default:
                        Debug.LogWarning($"[Nainital] Street View road: unknown item kind '{it.kind}' ({tag})");
                        continue;
                }
                if (made.Count > madeBefore) ctx.keepClear.Add(made[made.Count - 1]);
                count++;
            }
            moss.Flush();
            ctx.MeshObject("Roadside houses, sheds and stalls", parent, mb, ctx.mats.atlas, col, true);
            return count;
        }
    }
}
