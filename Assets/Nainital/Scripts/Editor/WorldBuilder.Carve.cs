using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    public static partial class WorldBuilder
    {
        /// <summary>Every road strip (left and right edge points, the lift its surface was given above the ground, and
        /// whether the road is painted into the ground), collected by BuildRoads and the Mall Road.</summary>
        static readonly List<(Vector3[] L, Vector3[] R, float lift, bool painted)> roadStrips = new List<(Vector3[], Vector3[], float, bool)>();

        /// <summary>Heightmap samples (z * res + x) of the bank behind Lower Mall's parapet, shaped by LowerMallBank: the
        /// parapet holds that ground, so the banks beside a painted road leave it. (22 Nov 2026: the final pass raised the
        /// 3 m behind the parapet 25-60 % of the way back to the road - dark mounds against the wall, the open gym's frames
        /// 0.7-1.5 m in the ground, the trees by the railing sunk.)</summary>
        static readonly HashSet<int> bankKeep = new HashSet<int>();

        /// <summary>
        /// A road painted into the ground (WorldBuilder.RoadMask.cs) has no slab: the ground under it is set to the road's
        /// smoothed surface, up or down, so the terrain collider is the road and the road has no bumps from the ground
        /// (user, 2026-11-16: "saari roads terrain me bake kro", "jama masjid road smooth kro"). Where two roads overlap
        /// (a junction) the heights are averaged, so they meet without a step, and the two rings of samples beside a road
        /// follow it part of the way (0.6, 0.25), so its edge is a short bank, not a ledge.
        /// Run right after the roads (everything placed later reads the road surface) and again at the end (a later step
        /// that levelled a site over a road is undone there: the road wins).
        /// </summary>
        static void SetGroundToPaintedRoads(WorldCtx ctx) => RoadGround(ctx, false);

        /// <summary>How far the ground lies under a road that is still a slab (the Upper Mall deck).</summary>
        const float SlabClearance = 0.4f;

        /// <summary>The last step: the painted roads once more (see SetGroundToPaintedRoads), and under a road that is still
        /// a slab (the Mall deck) the ground sinks to 25 cm below it, as before (2026-11-12: on the iPhone the terrain showed
        /// through the roads).</summary>
        static void CarveUnderRoads(WorldCtx ctx)
        {
            RoadGround(ctx, true);
            roadStrips.Clear();
            bankKeep.Clear();
            DeckEndRamps(ctx);
        }

        /// <summary>Each end of a road that is still a slab (the Upper Mall deck): its two corners and a point a little way in.</summary>
        static readonly List<(Vector3 L, Vector3 R, Vector3 inward)> deckEnds = new List<(Vector3, Vector3, Vector3)>();

        /// <summary>
        /// Where the Upper Mall deck begins, the painted ground of the junction rises to the deck's top over 7 m, and lies just
        /// under it for the deck's first 1.5 m (user, 2026-11-17, at the tourist help centre: "ye road alag hi height pe
        /// different mesh dikhri hai"): the deck stood 10-17 cm over the junction with its edge showing across the road.
        /// </summary>
        static void DeckEndRamps(WorldCtx ctx)
        {
            var t = ctx.terrain;
            if (!t || deckEnds.Count == 0) return;
            var td = t.terrainData;
            int res = td.heightmapResolution;
            Vector3 tp = t.transform.position, sz = td.size;
            float sx = (res - 1) / sz.x, sz2 = (res - 1) / sz.z;
            const float Ramp = 7f, Under = 1.5f;
            int changed = 0;
            foreach (var (L, R, inward) in deckEnds)
            {
                Vector2 l = new Vector2(L.x, L.z), r = new Vector2(R.x, R.z), m = (l + r) * 0.5f;
                Vector2 outD = m - new Vector2(inward.x, inward.z);
                if (outD.sqrMagnitude < 1e-4f) continue;
                outD.Normalize();
                Vector2 across = r - l;
                float halfW = across.magnitude * 0.5f;
                across.Normalize();
                float top = (L.y + R.y) * 0.5f;
                float reach = Ramp + halfW + 1.5f;
                int x0 = Mathf.Clamp(Mathf.FloorToInt((m.x - reach - tp.x) * sx), 0, res - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((m.x + reach - tp.x) * sx), 0, res - 1);
                int z0 = Mathf.Clamp(Mathf.FloorToInt((m.y - reach - tp.z) * sz2), 0, res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((m.y + reach - tp.z) * sz2), 0, res - 1);
                int w = x1 - x0 + 1, hgt = z1 - z0 + 1;
                var h = td.GetHeights(x0, z0, w, hgt);
                for (int zi = 0; zi < hgt; zi++)
                    for (int xi = 0; xi < w; xi++)
                    {
                        var q = new Vector2(tp.x + (x0 + xi) / sx, tp.z + (z0 + zi) / sz2) - m;
                        float a = Vector2.Dot(q, outD), s = Mathf.Abs(Vector2.Dot(q, across));
                        if (a < -Under || a > Ramp || s > halfW + 1.5f) continue;
                        if (a < 0f && s > halfW) continue;   // beside the deck (its footway, the divider): leave it
                        float k = a <= 0f ? 1f : Mathf.SmoothStep(1f, 0f, a / Ramp);
                        k *= Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((s - halfW) / 1.5f));
                        float cur = tp.y + h[zi, xi] * sz.y;
                        float want = top - 0.03f;
                        if (a < 0f && cur < want - 0.6f) continue;   // under the deck where the ground falls away: leave it
                        float y = Mathf.Lerp(cur, want, k);
                        if (Mathf.Abs(y - cur) < 0.005f) continue;
                        h[zi, xi] = Mathf.Clamp01((y - tp.y) / sz.y);
                        changed++;
                    }
                td.SetHeights(x0, z0, h);
            }
            Debug.Log($"[Nainital] deck ends: {deckEnds.Count} ramps, {changed} samples");
            deckEnds.Clear();
        }

        static void RoadGround(WorldCtx ctx, bool slabs)
        {
            var t = ctx.terrain;
            if (!t || roadStrips.Count == 0) return;
            var td = t.terrainData;
            int res = td.heightmapResolution;
            var h = td.GetHeights(0, 0, res, res);   // [z, x], 0..1 of size.y
            Vector3 tp = t.transform.position, sz = td.size;
            float sx = (res - 1) / sz.x, sz2 = (res - 1) / sz.z;
            var sum = new float[res * res];
            var cnt = new ushort[res * res];
            int bx0 = res, bz0 = res, bx1 = -1, bz1 = -1, lowered = 0;

            void Tri(Vector3 a, Vector3 b, Vector3 c, float lift, bool exact)
            {
                int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - tp.x) * sx));
                int x1 = Mathf.Min(res - 1, Mathf.CeilToInt((Mathf.Max(a.x, Mathf.Max(b.x, c.x)) - tp.x) * sx));
                int z0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.z, Mathf.Min(b.z, c.z)) - tp.z) * sz2));
                int z1 = Mathf.Min(res - 1, Mathf.CeilToInt((Mathf.Max(a.z, Mathf.Max(b.z, c.z)) - tp.z) * sz2));
                float den = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(den) < 1e-6f) return;
                for (int zi = z0; zi <= z1; zi++)
                    for (int xi = x0; xi <= x1; xi++)
                    {
                        float px = tp.x + xi / sx, pz = tp.z + zi / sz2;
                        float w1 = ((b.z - c.z) * (px - c.x) + (c.x - b.x) * (pz - c.z)) / den;
                        float w2 = ((c.z - a.z) * (px - c.x) + (a.x - c.x) * (pz - c.z)) / den;
                        float w3 = 1f - w1 - w2;
                        if (w1 < -1e-4f || w2 < -1e-4f || w3 < -1e-4f) continue;
                        float surf = w1 * a.y + w2 * b.y + w3 * c.y;
                        if (exact)
                        {
                            int k = zi * res + xi;
                            if (cnt[k] == ushort.MaxValue) continue;
                            sum[k] += (surf - lift - tp.y) / sz.y;
                            cnt[k]++;
                            if (xi < bx0) bx0 = xi; if (xi > bx1) bx1 = xi;
                            if (zi < bz0) bz0 = zi; if (zi > bz1) bz1 = zi;
                        }
                        else if (slabs)
                        {
                            float n = Mathf.Clamp01((surf - SlabClearance - tp.y) / sz.y);
                            if (n < h[zi, xi]) { h[zi, xi] = n; lowered++; }
                        }
                    }
            }

            foreach (var (L, R, lift, painted) in roadStrips)
            {
                if (!painted && !slabs) continue;
                for (int i = 0; i + 1 < L.Length; i++)
                {
                    Vector3 Inset(Vector3 p, Vector3 q)   // slabs: 12 cm in from the edge towards the other one
                    {
                        if (painted) return p;
                        Vector3 d = q - p; d.y = 0f;
                        float m = d.magnitude;
                        return m > 0.3f ? p + d / m * 0.12f : p;
                    }
                    Vector3 l0 = Inset(L[i], R[i]), r0 = Inset(R[i], L[i]), l1 = Inset(L[i + 1], R[i + 1]), r1 = Inset(R[i + 1], L[i + 1]);
                    Tri(l0, r0, r1, lift, painted);
                    Tri(l0, r1, l1, lift, painted);
                }
            }

            int set = 0, banked = 0;
            if (bx1 >= 0)
            {
                // the road samples: the average of the roads over them; the change kept for the banks beside them
                var delta = new float[res * res];
                var ring = new byte[res * res];   // 1 road, 2 first ring, 3 second ring
                for (int zi = bz0; zi <= bz1; zi++)
                    for (int xi = bx0; xi <= bx1; xi++)
                    {
                        int k = zi * res + xi;
                        if (cnt[k] == 0) continue;
                        float n = Mathf.Clamp01(sum[k] / cnt[k]);
                        delta[k] = n - h[zi, xi];
                        h[zi, xi] = n;
                        ring[k] = 1;
                        set++;
                    }
                for (int pass = 0; pass < 2; pass++)
                {
                    byte from = (byte)(pass + 1), to = (byte)(pass + 2);
                    float f = pass == 0 ? 0.6f : 0.25f;
                    int gx0 = Mathf.Max(1, bx0 - 2), gx1 = Mathf.Min(res - 2, bx1 + 2), gz0 = Mathf.Max(1, bz0 - 2), gz1 = Mathf.Min(res - 2, bz1 + 2);
                    var add = new List<(int k, float d)>();
                    for (int zi = gz0; zi <= gz1; zi++)
                        for (int xi = gx0; xi <= gx1; xi++)
                        {
                            int k = zi * res + xi;
                            if (ring[k] != 0 || bankKeep.Contains(k)) continue;
                            float s = 0f; int c = 0;
                            for (int dz = -1; dz <= 1; dz++)
                                for (int dx = -1; dx <= 1; dx++)
                                {
                                    int q = k + dz * res + dx;
                                    if (ring[q] == from) { s += pass == 0 ? delta[q] : delta[q] / 0.6f; c++; }
                                }
                            if (c > 0) add.Add((k, s / c));
                        }
                    foreach (var (k, d) in add)
                    {
                        int zi = k / res, xi = k % res;
                        h[zi, xi] = Mathf.Clamp01(h[zi, xi] + d * f);
                        delta[k] = d * f;
                        ring[k] = to;
                        banked++;
                    }
                }
            }
            td.SetHeights(0, 0, h);
            UnityEditor.EditorUtility.SetDirty(td);
            Debug.Log($"[Nainital] ground under the roads{(slabs ? " (final)" : "")}: {set} samples set to the painted roads' surface, {banked} banked beside them, {lowered} lowered under slabs, {roadStrips.Count} strips");
        }
    }
}
