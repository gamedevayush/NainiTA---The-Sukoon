using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    public static partial class WorldBuilder
    {
        /// <summary>
        /// Roads level across and smooth along (user, 2026-11-12, with screenshots: a road by the Flats leaned sideways up
        /// the hill like a ramp, and every road went up and down with each bump of the ground). Before any road is built,
        /// the ground along every road is set to the road's own profile: the centre-line height averaged over ±16 m, the
        /// same right across the carriageway, easing back into the natural ground over 2.5 m beyond each edge. Where two
        /// roads' corridors overlap, the nearer road decides. Mall Road's decks keep their own levels (Mall builder).
        /// Everything built afterwards reads these heights (ctx.H samples the terrain), so roads, junctions, kerbs and
        /// houses all agree.
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
            const float Ease = 2.5f;
            int roads = 0;
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
                // the road's own ends meet other roads: keep them at the ground there, easing in over 12 m
                for (int i = 0; i < n; i++)
                {
                    float e = Mathf.Min(i, n - 1 - i) * 2f;
                    if (e < 12f) prof[i] = Mathf.Lerp(raw[i], prof[i], Mathf.SmoothStep(0f, 1f, e / 12f));
                }
                float half = Mathf.Max(1.2f, r.width * 0.5f) + 0.4f, reach = half + Ease;
                for (int i = 0; i + 1 < n; i++)
                {
                    Vector2 p0 = pts[i], p1 = pts[i + 1], d = p1 - p0;
                    float len2 = d.sqrMagnitude;
                    if (len2 < 1e-4f) continue;
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
                            float w = dist <= half ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (dist - half) / Ease);
                            int idx = zi * res + xi;
                            if (w <= bestW[idx]) continue;
                            bestW[idx] = w;
                            bestH[idx] = Mathf.Lerp(prof[i], prof[i + 1], u);
                        }
                }
                roads++;
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
            Debug.Log($"[Nainital] roads levelled: {roads} roads, {changed} ground samples set to their road's profile");
        }
    }
}
