using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    public static partial class WorldBuilder
    {
        /// <summary>Every built road slab (left and right edge points), collected by BuildRoads for CarveUnderRoads.</summary>
        static readonly List<(Vector3[] L, Vector3[] R)> roadStrips = new List<(Vector3[], Vector3[])>();

        /// <summary>
        /// The ground under every road sinks to 25 cm below the slab (user, 2026-11-12: on the iPhone the terrain showed
        /// through the roads; the RoadCheck found hill roads standing under 5 cm over the ground). The slab and its kerb
        /// faces still meet the ground at the edges; only the inside (12 cm in from each edge) is lowered.
        /// </summary>
        static void CarveUnderRoads(WorldCtx ctx)
        {
            var t = ctx.terrain;
            if (!t || roadStrips.Count == 0) return;
            var td = t.terrainData;
            int res = td.heightmapResolution;
            var h = td.GetHeights(0, 0, res, res);   // [z, x], 0..1 of size.y
            Vector3 tp = t.transform.position, sz = td.size;
            float sx = (res - 1) / sz.x, sz2 = (res - 1) / sz.z;
            int lowered = 0;

            void Tri(Vector3 a, Vector3 b, Vector3 c)
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
                        if (w1 < 0f || w2 < 0f || w3 < 0f) continue;
                        float y = w1 * a.y + w2 * b.y + w3 * c.y - 0.25f;
                        float n = Mathf.Clamp01((y - tp.y) / sz.y);
                        if (n < h[zi, xi]) { h[zi, xi] = n; lowered++; }
                    }
            }

            foreach (var (L, R) in roadStrips)
                for (int i = 0; i + 1 < L.Length; i++)
                {
                    Vector3 Inset(Vector3 p, Vector3 q)   // 12 cm in from the edge towards the other one
                    {
                        Vector3 d = q - p; d.y = 0f;
                        float m = d.magnitude;
                        return m > 0.3f ? p + d / m * 0.12f : p;
                    }
                    Vector3 l0 = Inset(L[i], R[i]), r0 = Inset(R[i], L[i]), l1 = Inset(L[i + 1], R[i + 1]), r1 = Inset(R[i + 1], L[i + 1]);
                    Tri(l0, r0, r1);
                    Tri(l0, r1, l1);
                }
            td.SetHeights(0, 0, h);
            UnityEditor.EditorUtility.SetDirty(td);
            Debug.Log($"[Nainital] ground under the roads: {lowered} height samples lowered under {roadStrips.Count} roads");
            roadStrips.Clear();
        }
    }
}
