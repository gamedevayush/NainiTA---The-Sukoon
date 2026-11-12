using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    public static partial class WorldBuilder
    {
        /// <summary>2 m cells that are lake water although outside the OSM shore line (the strip by Lower Mall).</summary>
        static readonly HashSet<long> extraWater = new HashSet<long>();
        static long WaterKey(Vector2 p) => ((long)Mathf.FloorToInt(p.x / 2f) << 32) ^ (uint)Mathf.FloorToInt(p.y / 2f);
        internal static bool InExtraWater(Vector2 p) => extraWater.Contains(WaterKey(p));

        /// <summary>
        /// The lake comes right up to Lower Mall's parapet (user, 2026-11-12: the strip of grass and trees between the
        /// Lower Mall railing and the water should not be there). From the outer face of the parapet to a few metres past
        /// the OSM shore line the ground goes under water: the terrain sinks 1.5 m below the lake, and the quads are
        /// added to the lake's water surface. The first and last 40 m (the Mallital and Tallital boat stands) keep their
        /// banks. Called by BuildLake before the water mesh and the embankment are made.
        /// </summary>
        static List<Vector2[]> LowerMallWater(WorldCtx ctx)
        {
            extraWater.Clear();
            var quads = new List<Vector2[]>();
            var L = MallPath(ctx, MallLower, MallUpper, 1f);
            if (L == null) return quads;
            float along = 0f, total = 0f;
            for (int i = 1; i < L.Count; i++) total += Vector2.Distance(L.p[i - 1], L.p[i]);
            const float off = LowerHalf + 0.42f + 0.22f;   // the parapet's outer face
            Vector2 prevA = default, prevO = default;
            bool havePrev = false;
            for (int i = 0; i < L.Count; i += 2)
            {
                if (i > 0) for (int k = Mathf.Max(1, i - 1); k <= i; k++) along += Vector2.Distance(L.p[k - 1], L.p[k]);
                Vector2 a = L.p[i] + L.side[i] * off;
                bool ok = along > 40f && total - along > 40f && !ctx.InLake(a) && ctx.LakeDistance(a) < 34f;
                if (!ok) { havePrev = false; continue; }
                Vector2 o = a + L.side[i] * (ctx.LakeDistance(a) + 6f);
                if (havePrev) quads.Add(new[] { prevA, a, o, prevO });
                prevA = a; prevO = o; havePrev = true;
            }
            if (quads.Count == 0) return quads;

            // mark the cells, and sink the ground there
            var t = ctx.terrain;
            var td = t.terrainData;
            int res = td.heightmapResolution;
            var h = td.GetHeights(0, 0, res, res);
            Vector3 tp = t.transform.position, sz = td.size;
            float cx = (res - 1) / sz.x, cz = (res - 1) / sz.z;
            float floor = Mathf.Clamp01((ctx.lakeY - 1.5f - tp.y) / sz.y);
            foreach (var q in quads)
            {
                float minX = Mathf.Min(Mathf.Min(q[0].x, q[1].x), Mathf.Min(q[2].x, q[3].x)), maxX = Mathf.Max(Mathf.Max(q[0].x, q[1].x), Mathf.Max(q[2].x, q[3].x));
                float minZ = Mathf.Min(Mathf.Min(q[0].y, q[1].y), Mathf.Min(q[2].y, q[3].y)), maxZ = Mathf.Max(Mathf.Max(q[0].y, q[1].y), Mathf.Max(q[2].y, q[3].y));
                for (float x = minX; x <= maxX; x += 1f)
                    for (float z = minZ; z <= maxZ; z += 1f)
                        if (Geo.PointInPolygon(q, new Vector2(x, z))) extraWater.Add(WaterKey(new Vector2(x, z)));
                int x0 = Mathf.Max(0, Mathf.FloorToInt((minX - tp.x) * cx)), x1 = Mathf.Min(res - 1, Mathf.CeilToInt((maxX - tp.x) * cx));
                int z0 = Mathf.Max(0, Mathf.FloorToInt((minZ - tp.z) * cz)), z1 = Mathf.Min(res - 1, Mathf.CeilToInt((maxZ - tp.z) * cz));
                for (int zi = z0; zi <= z1; zi++)
                    for (int xi = x0; xi <= x1; xi++)
                    {
                        var p = new Vector2(tp.x + xi / cx, tp.z + zi / cz);
                        if (Geo.PointInPolygon(q, p) && h[zi, xi] > floor) h[zi, xi] = floor;
                    }
            }
            // and every ground sample within 2.5 m of the water cells, so no ridge of the old bank sticks out between pieces
            foreach (var q in quads)
            {
                float minX = Mathf.Min(Mathf.Min(q[0].x, q[1].x), Mathf.Min(q[2].x, q[3].x)) - 3f, maxX = Mathf.Max(Mathf.Max(q[0].x, q[1].x), Mathf.Max(q[2].x, q[3].x)) + 3f;
                float minZ = Mathf.Min(Mathf.Min(q[0].y, q[1].y), Mathf.Min(q[2].y, q[3].y)) - 3f, maxZ = Mathf.Max(Mathf.Max(q[0].y, q[1].y), Mathf.Max(q[2].y, q[3].y)) + 3f;
                int x0 = Mathf.Max(0, Mathf.FloorToInt((minX - tp.x) * cx)), x1 = Mathf.Min(res - 1, Mathf.CeilToInt((maxX - tp.x) * cx));
                int z0 = Mathf.Max(0, Mathf.FloorToInt((minZ - tp.z) * cz)), z1 = Mathf.Min(res - 1, Mathf.CeilToInt((maxZ - tp.z) * cz));
                for (int zi = z0; zi <= z1; zi++)
                    for (int xi = x0; xi <= x1; xi++)
                    {
                        if (h[zi, xi] <= floor) continue;
                        var p = new Vector2(tp.x + xi / cx, tp.z + zi / cz);
                        bool wet = false;
                        for (int k = 0; k < 9 && !wet; k++) wet = InExtraWater(p + new Vector2((k % 3 - 1) * 2.5f, (k / 3 - 1) * 2.5f));
                        if (wet && Geo.DistanceToPolyline(L.arr, p, false) < off - 0.05f) wet = false;   // never on the road side of the parapet
                        if (wet) h[zi, xi] = floor;
                    }
            }
            td.SetHeights(0, 0, h);
            UnityEditor.EditorUtility.SetDirty(td);
            Debug.Log($"[Nainital] Lower Mall: the lake reaches the parapet along {quads.Count} pieces ({extraWater.Count} water cells)");
            return quads;
        }
    }
}
