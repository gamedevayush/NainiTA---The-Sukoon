using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    public static partial class WorldBuilder
    {
        /// <summary>Trees for the Mall (the Lower Mall bank, the strip between the two roads): x, z, type, scale, and in
        /// bankTreeY the foot's height (NaN: on the ground). BuildTrees adds them.</summary>
        static readonly List<Vector4> bankTrees = new List<Vector4>();
        static readonly List<float> bankTreeY = new List<float>();

        /// <summary>
        /// The bank between Lower Mall's parapet and the lake, as it is in Street View (2022 panos along Mall Rd): a
        /// narrow strip of ground just below the road behind the railing, falling gently, then more steeply, to the stone
        /// edge of the water, under big broadleaf trees (banj oak, the odd deodar) over grass and leaf litter. The ground is
        /// shaped from the deck heights (L.y) and the real shore line; the ends (boat stands, jetties) blend into the
        /// ground that was there. Called at the end of BuildMallRoad, after the parapet is built.
        /// (user, 2026-11-12: the strip had been put under water by mistake; it should be ground with trees.)
        /// </summary>
        static void LowerMallBank(WorldCtx ctx, MallLine U, MallLine L, int[] low, List<int> stairs)
        {
            bankTrees.Clear();
            bankTreeY.Clear();
            bankKeep.Clear();
            JoggersPark.Spots.Clear();
            const float off = LowerHalf + 0.42f + 0.22f;   // the parapet's outer face
            int n = L.Count;
            var along = new float[n];
            for (int i = 1; i < n; i++) along[i] = along[i - 1] + Vector2.Distance(L.p[i - 1], L.p[i]);
            float total = along[n - 1];

            // width of the bank (parapet face to shore line), smoothed, and how much of the new shape to use there
            var raw = new float[n];
            var wt = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 a = L.p[i] + L.side[i] * off;
                float ld = ctx.InLake(a) ? 0f : ctx.LakeDistance(a);
                raw[i] = ld;
                float ends = Mathf.Clamp01((Mathf.Min(along[i], total - along[i]) - 40f) / 15f);
                float far = Mathf.Clamp01((34f - ld) / 4f);
                float jetty = 1f;
                foreach (var j in ctx.jettyRoots) jetty = Mathf.Min(jetty, Mathf.Clamp01(((a - j).magnitude - 6f) / 8f));
                wt[i] = ld <= 0.5f ? 0f : Mathf.Max(Mathf.Min(ends, Mathf.Min(far, jetty)), JoggersPark.Weight(a));   // the park is always shaped
            }
            var w = new float[n];
            for (int i = 0; i < n; i++)
            {
                float s = 0f; int c = 0;
                for (int k = Mathf.Max(0, i - 5); k <= Mathf.Min(n - 1, i + 5); k++) { s += raw[k]; c++; }
                w[i] = s / c;
            }

            // nearest centre-line point by 4 m cells
            var cells = new Dictionary<long, List<int>>();
            long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
            for (int i = 0; i < n; i++)
            {
                long k = Key(Mathf.FloorToInt(L.p[i].x / 4f), Mathf.FloorToInt(L.p[i].y / 4f));
                if (!cells.TryGetValue(k, out var l)) cells[k] = l = new List<int>();
                l.Add(i);
            }
            int Nearest(Vector2 p)
            {
                int cx0 = Mathf.FloorToInt(p.x / 4f), cz0 = Mathf.FloorToInt(p.y / 4f), best = -1;
                float bd = float.MaxValue;
                for (int r = 0; r <= 10 && best < 0; r++)   // grow the ring until a point is found (the bank is at most ~35 m wide)
                    for (int dz = -r; dz <= r; dz++)
                        for (int dx = -r; dx <= r; dx++)
                        {
                            if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                            if (!cells.TryGetValue(Key(cx0 + dx, cz0 + dz), out var l)) continue;
                            foreach (int i in l) { float d = (L.p[i] - p).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
                        }
                if (best >= 0)   // the ring that found it may miss a nearer point one ring further out
                {
                    int r2 = Mathf.CeilToInt(Mathf.Sqrt(bd) / 4f) + 1;
                    for (int dz = -r2; dz <= r2; dz++)
                        for (int dx = -r2; dx <= r2; dx++)
                            if (cells.TryGetValue(Key(cx0 + dx, cz0 + dz), out var l))
                                foreach (int i in l) { float d = (L.p[i] - p).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
                }
                return best;
            }

            float bottom = ctx.lakeY + 1.3f;   // the foot of the bank: a stone wall about this high stands in the water below it
            float Target(int i, float s, Vector2 p)   // s: metres out from the parapet face
            {
                // the ground starts below the road behind the parapet (the trees grow from below the railing), falls
                // evenly to a level ledge just behind the stone wall at the water
                float top = Mathf.Max(L.y[i] - 1.8f, bottom + 0.2f);
                float ledge = Mathf.Min(2.6f, w[i] * 0.4f);
                float t = Mathf.Clamp01(s / Mathf.Max(w[i] - ledge, 0.8f));
                float f = Mathf.SmoothStep(0f, 1f, t) * 0.6f + t * 0.4f;
                float bump = (Mathf.PerlinNoise(p.x * 0.21f, p.y * 0.21f) - 0.5f) * 0.3f * Mathf.Sin(t * Mathf.PI);
                float y = Mathf.Lerp(top, bottom, f) + bump;
                float pk = JoggersPark.Weight(p);
                return pk > 0f ? Mathf.Lerp(y, ParkTarget(i, s, p), pk) : y;
            }

            // City Joggers Park (owner, 22 Nov 2026: "na vo flat hai...up down terrain hai"): open ground from just under the
            // road, rolling up and down as it falls to the ledge at the water; at the two openings in the parapet it comes up
            // to the road so you walk in
            float ParkTarget(int i, float s, Vector2 p)
            {
                // a level strip of earth along the parapet first, a hand under the road: the samples under the parapet stay
                // near the road (its edge must not sag), and with the bank starting lower the 1.46 m heightmap made a
                // saw-tooth of road-high lumps out from the wall (22 Nov, first build of the park)
                const float strip = 1.8f;
                float ledge = Mathf.Min(2.6f, w[i] * 0.3f);
                float t = Mathf.Clamp01((s - strip) / Mathf.Max(w[i] - ledge - strip, 0.8f));
                float open = JoggersPark.OpeningK(p);
                float top = Mathf.Lerp(L.y[i] - 0.15f, L.y[i] - 0.06f, open);
                float f = t * 0.55f + Mathf.SmoothStep(0f, 1f, t) * 0.45f;
                float roll = (Mathf.PerlinNoise(p.x * 0.11f + 3.7f, p.y * 0.11f + 1.9f) - 0.5f) * 1.3f
                           + (Mathf.PerlinNoise(p.x * 0.27f + 9.1f, p.y * 0.27f + 4.4f) - 0.5f) * 0.45f;
                float env = Mathf.Sin(t * Mathf.PI) * (1f - open * Mathf.Clamp01(1f - t * 3f));   // the road edge, the ledge and the way in stay put
                return Mathf.Lerp(top, bottom, f) + roll * env;
            }

            // shape the ground
            var tr = ctx.terrain;
            var td = tr.terrainData;
            int res = td.heightmapResolution;
            var h = td.GetHeights(0, 0, res, res);
            Vector3 tp = tr.transform.position, sz = td.size;
            float hx = (res - 1) / sz.x, hz = (res - 1) / sz.z;
            Vector2 mn = new Vector2(float.MaxValue, float.MaxValue), mx = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < n; i++)
            {
                if (wt[i] <= 0f) continue;
                Vector2 o = L.p[i] + L.side[i] * (off + w[i] + 36f);   // and the lake bed in front of it
                mn = Vector2.Min(mn, Vector2.Min(L.p[i], o));
                mx = Vector2.Max(mx, Vector2.Max(L.p[i], o));
            }
            int changed = 0, sunk = 0;
            if (mn.x < mx.x)
            {
                int x0 = Mathf.Max(0, Mathf.FloorToInt((mn.x - tp.x) * hx)), x1 = Mathf.Min(res - 1, Mathf.CeilToInt((mx.x - tp.x) * hx));
                int z0 = Mathf.Max(0, Mathf.FloorToInt((mn.y - tp.z) * hz)), z1 = Mathf.Min(res - 1, Mathf.CeilToInt((mx.y - tp.z) * hz));
                for (int zi = z0; zi <= z1; zi++)
                    for (int xi = x0; xi <= x1; xi++)
                    {
                        var p = new Vector2(tp.x + xi / hx, tp.z + zi / hz);
                        int i = Nearest(p);
                        if (i < 0 || wt[i] <= 0f) continue;
                        float s = Vector2.Dot(p - L.p[i], L.side[i]) - off;
                        if (Geo.PointInPolygon(ctx.lake, p))
                        {
                            // past the real shore line the coarse DEM still had a shelf of land at road height, standing
                            // out of the water as a dark cliff: it goes under the lake, falling away from the bank
                            if (s < w[i] - 1f || s > w[i] + 35f) continue;
                            float bed = ctx.lakeY - 1.2f - Mathf.Min(3f, Mathf.Max(0f, s - w[i]) * 0.3f);
                            float now = tp.y + h[zi, xi] * sz.y;
                            if (now > bed) { h[zi, xi] = Mathf.Clamp01((Mathf.Lerp(now, bed, wt[i]) - tp.y) / sz.y); sunk++; }
                            continue;
                        }
                        if (s < -1.6f || s > w[i] + 1.5f) continue;   // the lake side of the parapet, out to the shore
                        if (s < 0.15f)
                        {
                            // under the parapet and the edge of the deck (both cover it): down to the bank's top, so the
                            // ground meets the parapet's outer face cleanly instead of in a saw-tooth (in the park's
                            // openings there is no parapet: up or down to the way in)
                            float under = Target(i, 0f, p), here = tp.y + h[zi, xi] * sz.y;
                            bool opening = JoggersPark.OpeningK(p) > 0.5f;
                            if (here > under || opening) h[zi, xi] = Mathf.Clamp01((Mathf.Lerp(here, under, wt[i]) - tp.y) / sz.y);
                            if (opening && s > -0.6f) bankKeep.Add(zi * res + xi);
                            continue;
                        }
                        float y = Target(i, Mathf.Min(s, w[i]), p);
                        if (ctx.LakeDistance(p) < 1.0f) y = ctx.lakeY - 0.6f;   // behind the stone wall at the water, so no ground pokes out in front of it
                        float cur = tp.y + h[zi, xi] * sz.y;
                        h[zi, xi] = Mathf.Clamp01((Mathf.Lerp(cur, y, wt[i]) - tp.y) / sz.y);
                        if (wt[i] > 0.5f) bankKeep.Add(zi * res + xi);   // the parapet holds this ground: the road's banks leave it (RoadGround)
                        changed++;
                    }

                // City Joggers Park: a piece of the open gym every ~7 m along, each on the flattest place across the bank there
                // (nearer the road and nearer the water by turns, off the ways in), the ground under it made level; the
                // equipment itself is built on them after the last ground step (LandmarkBuilder.BuildJoggersPark)
                float HAt(Vector2 q)
                {
                    float fx = (q.x - tp.x) * hx, fz = (q.y - tp.z) * hz;
                    int x0i = Mathf.Clamp(Mathf.FloorToInt(fx), 0, res - 2), z0i = Mathf.Clamp(Mathf.FloorToInt(fz), 0, res - 2);
                    float ux = Mathf.Clamp01(fx - x0i), uz = Mathf.Clamp01(fz - z0i);
                    float r0 = Mathf.Lerp(h[z0i, x0i], h[z0i, x0i + 1], ux), r1 = Mathf.Lerp(h[z0i + 1, x0i], h[z0i + 1, x0i + 1], ux);
                    return tp.y + Mathf.Lerp(r0, r1, uz) * sz.y;
                }
                float[] spotS = { 3f, 14.5f, 21.5f, 28.5f, 35.5f, 41.5f, 52.5f, 59.5f, 66.5f };
                for (int k = 0; k < spotS.Length; k++)
                {
                    int i = Nearest(JoggersPark.World(spotS[k], 10f));
                    if (i < 0 || wt[i] < 0.9f) continue;
                    float lo = 3.2f, hi = w[i] - 4.2f;
                    if (hi < lo) { Debug.LogWarning($"[Nainital] Joggers Park: no room at s {spotS[k]} (bank {w[i]:F1} m)"); continue; }
                    float best = float.MaxValue;
                    Vector2 bp = default;
                    for (float sOut = lo; sOut <= hi + 0.01f; sOut += 0.75f)
                    {
                        Vector2 q = L.p[i] + L.side[i] * (off + sOut);
                        float slope = 0f;
                        for (int a = 0; a < 3; a++)
                        {
                            var o = new Vector2(Mathf.Cos(a * Mathf.PI / 3f), Mathf.Sin(a * Mathf.PI / 3f)) * 1.4f;
                            slope = Mathf.Max(slope, Mathf.Abs(HAt(q + o) - HAt(q - o)));
                        }
                        float row = (sOut - lo) / Mathf.Max(hi - lo, 1f);
                        float score = slope + (k % 2 == 0 ? row : 1f - row) * 0.45f;
                        if (score < best) { best = score; bp = q; }
                    }
                    JoggersPark.Spots.Add((bp, L.tan[i], k % 3));
                }
                foreach (var sp in JoggersPark.Spots)
                {
                    float y = HAt(sp.p);
                    const float R0 = 1.5f, R1 = 3.3f;
                    int sx0 = Mathf.Max(0, Mathf.FloorToInt((sp.p.x - R1 - tp.x) * hx)), sx1 = Mathf.Min(res - 1, Mathf.CeilToInt((sp.p.x + R1 - tp.x) * hx));
                    int sz0 = Mathf.Max(0, Mathf.FloorToInt((sp.p.y - R1 - tp.z) * hz)), sz1 = Mathf.Min(res - 1, Mathf.CeilToInt((sp.p.y + R1 - tp.z) * hz));
                    for (int zi = sz0; zi <= sz1; zi++)
                        for (int xi = sx0; xi <= sx1; xi++)
                        {
                            float dd = Vector2.Distance(new Vector2(tp.x + xi / hx, tp.z + zi / hz), sp.p);
                            if (dd > R1) continue;
                            float kk = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(R0, R1, dd));
                            float cur = tp.y + h[zi, xi] * sz.y;
                            h[zi, xi] = Mathf.Clamp01((Mathf.Lerp(cur, y, kk) - tp.y) / sz.y);
                            bankKeep.Add(zi * res + xi);
                        }
                }
                Debug.Log($"[Nainital] Joggers Park: {JoggersPark.Spots.Count} level spots for the open gym");
                td.SetHeights(0, 0, h);
                UnityEditor.EditorUtility.SetDirty(td);
            }

            // the ground cover: grass and leaf litter under the trees, a little bare earth (layers: grass, forest floor, rock, earth)
            int painted = 0;
            if (mn.x < mx.x)
            {
                int ar = td.alphamapResolution;
                float ax = ar / sz.x, az = ar / sz.z;
                int a0 = Mathf.Max(0, Mathf.FloorToInt((mn.x - tp.x) * ax)), a1 = Mathf.Min(ar - 1, Mathf.CeilToInt((mx.x - tp.x) * ax));
                int b0 = Mathf.Max(0, Mathf.FloorToInt((mn.y - tp.z) * az)), b1 = Mathf.Min(ar - 1, Mathf.CeilToInt((mx.y - tp.z) * az));
                var am = td.GetAlphamaps(a0, b0, a1 - a0 + 1, b1 - b0 + 1);
                for (int zi = 0; zi <= b1 - b0; zi++)
                    for (int xi = 0; xi <= a1 - a0; xi++)
                    {
                        var p = new Vector2(tp.x + (a0 + xi + 0.5f) / ax, tp.z + (b0 + zi + 0.5f) / az);
                        if (Geo.PointInPolygon(ctx.lake, p)) continue;
                        int i = Nearest(p);
                        if (i < 0 || wt[i] <= 0f) continue;
                        float sOut = Vector2.Dot(p - L.p[i], L.side[i]) - off;
                        if (sOut < -0.1f || sOut > w[i] + 1.5f) continue;
                        float nz = Mathf.PerlinNoise(p.x * 0.13f + 7.1f, p.y * 0.13f + 3.3f);
                        float g = 0.35f + nz * 0.45f, f = 0.85f - g, e = 0.15f;
                        float k = wt[i];
                        float[] want = { g, f, 0f, e };
                        float pk = JoggersPark.Weight(p);
                        if (pk > 0f)   // the park: trodden earth and leaf litter more than grass (satellite: brown under the trees), bare round the equipment
                        {
                            float bare = JoggersPark.NearSpot(p, 1.9f) ? 0.9f : 0.32f + (1f - nz) * 0.3f;
                            want = new[] { Mathf.Lerp(g, (1f - bare) * 0.6f, pk), Mathf.Lerp(f, (1f - bare) * 0.4f, pk), 0f, Mathf.Lerp(e, bare, pk) };
                        }
                        for (int l = 0; l < 4 && l < am.GetLength(2); l++) am[zi, xi, l] = Mathf.Lerp(am[zi, xi, l], want[l], k);
                        painted++;
                    }
                td.SetAlphamaps(a0, b0, am);
            }

            // trees, set at random as they grow: in clumps and gaps (a slow noise along the road sets how thick they
            // stand), each kept clear of the others by its own size; young, grown and old trees mixed
            var rnd = new System.Random(23);
            float R() => (float)rnd.NextDouble();
            var placed = new List<Vector2>();
            bool Free(Vector2 p, float r) { foreach (var q in placed) if ((q - p).sqrMagnitude < r * r) return false; return true; }
            // the user's Street View route (19 Nov 2026): along the whole Mall the trees are old broadleaves, tall and close, their
            // crowns meeting over the road; few young ones, a deodar now and then
            float Size() { float r = R(); return r < 0.12f ? 0.7f + R() * 0.25f : r < 0.65f ? 1.0f + R() * 0.35f : 1.35f + R() * 0.35f; }
            int Kind() { float r = R(); return r < 0.82f ? 2 : r < 0.93f ? 0 : r < 0.98f ? 3 : 1; }   // banj oak, deodar, cypress, chir
            void Add(Vector2 p, float y, int type, float scale) { bankTrees.Add(new Vector4(p.x, p.y, type, scale)); bankTreeY.Add(y); placed.Add(p); }
            float Thick(float d, float seed) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 0.72f, Mathf.PerlinNoise(d * 0.028f + seed, seed * 0.37f)));

            // 1. the bank below the Lower Mall railing
            int bank = 0;
            for (int attempt = 0; attempt < 3600; attempt++)
            {
                int i = rnd.Next(n);
                if (wt[i] < 0.9f || w[i] < 2.2f) continue;
                float dens = Thick(along[i], 11.3f);
                if (R() > 0.3f + 0.7f * dens) continue;
                float s = 1.0f + Mathf.Pow(R(), 1.3f) * Mathf.Max(0.2f, w[i] - 1.9f);   // more of them up near the railing
                Vector2 p = L.p[i] + L.side[i] * (off + s) + L.tan[i] * (R() - 0.5f);
                if (ctx.InLake(p)) continue;
                if (JoggersPark.NearSpot(p, 3.2f) || JoggersPark.OpeningK(p) > 0.2f) continue;   // the open gym's pieces and the ways in
                float sc = Size();
                if (s < 2.4f && sc < 1.1f) sc = 1.1f + R() * 0.4f;   // by the railing only grown trees: their crowns stand clear of the road
                if (!Free(p, (2.4f + sc * 2.4f) * Mathf.Lerp(1.35f, 0.8f, dens) + R() * 1.2f)) continue;
                Add(p, float.NaN, Kind(), sc);
                bank++;
            }

            // 2. old trees growing out of the strip between Upper and Lower Mall (Street View: a big one every so often)
            int strip = 0;
            float uAlong = 0f, nextU = 8f + R() * 20f;
            for (int i = 0; i < U.Count; i++)
            {
                if (i > 0) uAlong += Vector2.Distance(U.p[i - 1], U.p[i]);
                if (uAlong < nextU) continue;
                if (Vector2.Distance(U.p[i], L.p[low[i]]) < MallHalf * 2f + 1.5f) continue;   // no strip here
                bool nearStair = false;
                foreach (int si in stairs) if (Mathf.Abs(si - i) <= 3) nearStair = true;
                if (nearStair) continue;
                Vector2 p = U.p[i] - U.side[i] * (MallHalf + 0.75f) + U.tan[i] * (R() - 0.5f) * 1.5f;
                bool nearLamp = false;
                foreach (var lh in ctx.lampHeads) if (new Vector2(lh.x - p.x, lh.z - p.y).sqrMagnitude < 3.5f * 3.5f) { nearLamp = true; break; }
                if (nearLamp || !Free(p, 5f)) continue;
                Add(p, U.y[i] + 0.14f - 0.1f, R() < 0.88f ? 2 : 0, 1.15f + R() * 0.5f);
                strip++;
                nextU = uAlong + 7f + R() * 7f;   // a row of old trees every 7-14 m (Street View 2022, Tallital to the middle)
            }

            // 3. where the two roads draw apart, trees on the ground between Lower Mall's hill edge and the wall below Upper
            int between = 0;
            for (int attempt = 0; attempt < 1500; attempt++)
            {
                int i = rnd.Next(n);
                if (L.hillW == null) break;
                float gap = Geo.DistanceToPolyline(U.arr, L.p[i], false) - (MallHalf + MallStrip) - L.hillW[i];
                if (gap < 3.2f) continue;
                float dens = Thick(along[i], 5.9f);
                if (R() > 0.15f + 0.85f * dens) continue;
                Vector2 p = L.p[i] - L.side[i] * (L.hillW[i] + 1.2f + R() * (gap - 2.4f));
                float sc = Size();
                if (!Free(p, (2.4f + sc * 2.2f) * Mathf.Lerp(2.4f, 1f, dens) + R() * 1.5f)) continue;
                Add(p, float.NaN, Kind(), sc);
                between++;
            }
            int trees = bank + strip + between;
            Debug.Log($"[Nainital] Lower Mall bank: {changed} ground samples shaped, {sunk} lake-bed samples sunk, {painted} texels painted; trees: {bank} on the bank, {strip} on the strip between the roads, {between} on the ground between them");
        }
    }
}
