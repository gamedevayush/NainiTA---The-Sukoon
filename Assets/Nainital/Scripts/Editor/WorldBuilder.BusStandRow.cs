using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// The long yellow building on the Bhowali road from the Tallital junction towards the bus stand (owner, 24-25 Nov 2026:
    /// "till bus stand road tk yellow building hai ek" / "there is no house sticked with road from rest gate to bustand").
    /// The Esri satellite shows one long teal-tin-roofed building along the road's north-east side, ~50 m from just past the
    /// junction; the data had a row of small separate houses there that did not stand. Here: one building in sections that
    /// follow the road and step with the ground, its front on the pavement, light-yellow walls, teal tin roof, shops below.
    /// </summary>
    public static partial class WorldBuilder
    {
        const float RowStart = 13f, RowLength = 50f, RowSection = 12.5f, RowDepth = 9f, RowPavement = 1.2f;

        static List<(Vector2 c, float w, float yaw)> BusRoadRow(WorldCtx ctx)
        {
            var row = new List<(Vector2, float, float)>();
            RoadData road = null;
            foreach (var r in ctx.world.roads)
                if (r.name == "Nainital - Bhawali Road" && r.pts.Length >= 4 && Vector2.Distance(new Vector2(r.pts[0], r.pts[1]), new Vector2(832.5f, -1132.2f)) < 3f)
                    road = r;
            if (road == null) return row;
            var p = Geo.ToPoints(road.pts);
            Vector2 At(float s)
            {
                for (int i = 0; i + 1 < p.Length; i++)
                {
                    float l = Vector2.Distance(p[i], p[i + 1]);
                    if (s <= l) return Vector2.Lerp(p[i], p[i + 1], s / l);
                    s -= l;
                }
                return p[p.Length - 1];
            }
            float half = road.width * 0.5f;
            for (float s = RowStart; s + RowSection <= RowStart + RowLength + 0.01f; s += RowSection)
            {
                Vector2 a = At(s), b = At(s + RowSection), dir = (b - a).normalized;
                Vector2 n = new Vector2(-dir.y, dir.x);
                if (n.x + n.y < 0f) n = -n;   // the north-east side (the bus station is across the road)
                Vector2 c = (a + b) * 0.5f + n * (half + RowPavement + RowDepth * 0.5f);
                float yaw = Mathf.Atan2(-n.x, -n.y) * Mathf.Rad2Deg;   // the front faces the road
                row.Add((c, Vector2.Distance(a, b) + 0.25f, yaw));
            }
            return row;
        }

        /// <summary>PlanTallital: no data house where the row stands.</summary>
        static void PlanBusRoadRow(WorldCtx ctx)
        {
            foreach (var (c, w, _) in BusRoadRow(ctx)) ctx.keepClear.Add((c, Mathf.Max(w, RowDepth) * 0.5f + 0.5f));
        }

        /// <summary>BuildTallital: the row itself.</summary>
        static void BuildBusRoadRow(WorldCtx ctx, Transform parent)
        {
            var row = BusRoadRow(ctx);
            if (row.Count == 0) { Debug.LogWarning("[Nainital] bus road row: the Bhowali road was not found"); return; }
            var mb = new MeshBuilder();
            var col = new MeshBuilder();
            var rng = new System.Random(2511);   // its own, so nothing built after it changes
            var clear = Clearance(ctx);
            int built = 0;
            foreach (var (c, w, yaw) in row)
            {
                var q = Quaternion.Euler(0f, yaw, 0f);
                Vector3 R3 = q * Vector3.right * (w * 0.5f), F3 = q * Vector3.forward * (RowDepth * 0.5f);
                Vector2 R = new Vector2(R3.x, R3.z), F = new Vector2(F3.x, F3.z);
                if (clear.Blocked(c + F * 0.9f, 0f) || clear.Blocked(c + F * 0.9f + R * 0.9f, 0f) || clear.Blocked(c + F * 0.9f - R * 0.9f, 0f)) continue;
                float gMin = float.MaxValue, gMax = float.MinValue;
                for (int gu = -2; gu <= 2; gu++)
                    for (int gv = -2; gv <= 2; gv++)
                    {
                        float g = ctx.H(c + R * (gu * 0.5f) + F * (gv * 0.5f));
                        gMin = Mathf.Min(gMin, g);
                        gMax = Mathf.Max(gMax, g);
                    }
                var st = new BuildingKit.Style { wall = 2, roof = 3, shop = true, litChance = 0.5f, flatRoof = false };   // light yellow, teal tin
                BuildingKit.House(mb, col, c, w, RowDepth, yaw, 2, gMin, gMax, st, rng);
                built++;
            }
            ctx.MeshObject("Bhowali road yellow building", parent, mb, ctx.mats.atlas, col);
            Debug.Log($"[Nainital] bus road row: {built} of {row.Count} sections, {RowLength:F0} m along the Bhowali road from the junction");
        }

        /// <summary>The strip between the Haldwani highway and the Bhowali road near p: its middle, the way across it
        /// (highway to Bhowali road) and the clear width between the two carriageways' edges.</summary>
        static bool BusStrip(WorldCtx ctx, Vector2 p, out Vector2 mid, out Vector2 across, out float clear)
        {
            mid = p; across = Vector2.right; clear = 0f;
            float hwW = 8f, bwW = 8f;
            foreach (var r in ctx.world.roads)
            {
                if (r.name == "Nainital - Haldwani Highway") hwW = r.width;
                if (r.name == "Nainital - Bhawali Road") bwW = r.width;
            }
            Vector2 q = p;
            for (int it = 0; it < 3; it++)
            {
                if (!NearestRoadPoint(ctx, q, 45f, r => r.name == "Nainital - Haldwani Highway", out var hw, out _)) return false;
                if (!NearestRoadPoint(ctx, q, 45f, r => r.name == "Nainital - Bhawali Road", out var bw, out _)) return false;
                q = (hw + bw) * 0.5f;
                across = (bw - hw).normalized;
                clear = Vector2.Distance(hw, bw) - (hwW + bwW) * 0.5f;
            }
            mid = q;
            return clear > 10f;
        }
    }
}
