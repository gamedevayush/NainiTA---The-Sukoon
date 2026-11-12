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
                wt[i] = ld <= 0.5f ? 0f : Mathf.Min(ends, Mathf.Min(far, jetty));
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
                return Mathf.Lerp(top, bottom, f) + bump;
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
                            // ground meets the parapet's outer face cleanly instead of in a saw-tooth
                            float under = Target(i, 0f, p), here = tp.y + h[zi, xi] * sz.y;
                            if (here > under) h[zi, xi] = Mathf.Clamp01((Mathf.Lerp(here, under, wt[i]) - tp.y) / sz.y);
                            continue;
                        }
                        float y = Target(i, Mathf.Min(s, w[i]), p);
                        if (ctx.LakeDistance(p) < 1.0f) y = ctx.lakeY - 0.6f;   // behind the stone wall at the water, so no ground pokes out in front of it
                        float cur = tp.y + h[zi, xi] * sz.y;
                        h[zi, xi] = Mathf.Clamp01((Mathf.Lerp(cur, y, wt[i]) - tp.y) / sz.y);
                        changed++;
                    }
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
            float Size() { float r = R(); return r < 0.22f ? 0.55f + R() * 0.25f : r < 0.8f ? 0.85f + R() * 0.4f : 1.3f + R() * 0.3f; }
            int Kind() { float r = R(); return r < 0.66f ? 2 : r < 0.86f ? 0 : r < 0.94f ? 3 : 1; }   // banj oak, deodar, cypress, chir
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
                float sc = Size();
                if (s < 2.4f && sc < 1.1f) sc = 1.1f + R() * 0.4f;   // by the railing only grown trees: their crowns stand clear of the road
                if (!Free(p, (2.4f + sc * 2.4f) * Mathf.Lerp(1.9f, 0.9f, dens) + R() * 1.5f)) continue;
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
                Add(p, U.y[i] + 0.14f - 0.1f, R() < 0.8f ? 2 : 0, 1.0f + R() * 0.5f);
                strip++;
                nextU = uAlong + (R() < 0.3f ? 5f + R() * 4f : 14f + R() * 50f);   // now and then two together, else long gaps
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
