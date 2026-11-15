using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    public static partial class WorldBuilder
    {
        /// <summary>Every road strip (left and right edge points, the lift its surface was given above the ground, and
        /// whether the road is painted into the ground), collected by BuildRoads and the Mall Road.</summary>
        static readonly List<(Vector3[] L, Vector3[] R, float lift, bool painted)> roadStrips = new List<(Vector3[], Vector3[], float, bool)>();

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

        /// <summary>The last step: the painted roads once more (see SetGroundToPaintedRoads), and under a road that is still
        /// a slab (the Mall deck) the ground sinks to 25 cm below it, as before (2026-11-12: on the iPhone the terrain showed
        /// through the roads).</summary>
        static void CarveUnderRoads(WorldCtx ctx)
        {
            RoadGround(ctx, true);
            roadStrips.Clear();
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
                            float n = Mathf.Clamp01((surf - 0.25f - tp.y) / sz.y);
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
                            if (ring[k] != 0) continue;
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
