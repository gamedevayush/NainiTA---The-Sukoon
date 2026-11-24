using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// The Army Rest &amp; Recoup Centre on the hill side of the road from Tallital chowk to Mall Road (the user's Street View
    /// route, 19 Nov 2026, panoramas 5-12, Aug 2022): a long whitewashed boundary wall with a grey plinth along the road,
    /// and its gate - two white pillars with small domed caps and red bands, a red board "ARMY REST &amp; RECOUP CENTRE
    /// NAINITAL" between them, black iron gate leaves. Inside, low green-roofed barracks on open ground (the generated
    /// houses there were lowered to one storey by Tools/fit_mall_streetview.py's notes; see README).
    /// </summary>
    public static partial class LandmarkBuilder
    {
        // the car's track past the compound (route.csv 5..12, local metres); the wall runs on the hill (east) side
        static readonly Vector2[] ArmyTrack =
        {
            new Vector2(842.9f, -1104.1f), new Vector2(846.0f, -1094.2f), new Vector2(849.5f, -1084.3f), new Vector2(853.1f, -1073.5f),
            new Vector2(858.1f, -1063.9f), new Vector2(863.2f, -1055.3f), new Vector2(869.3f, -1045.1f), new Vector2(874.2f, -1037.1f),
        };
        const int ArmyGateAt = 2;   // panorama 7: the gate is right beside it

        internal static void BuildArmyRest(WorldCtx ctx, Transform parent)
        {
            var sm = new SiteMeshes();
            var plaster = sm.Photo("white_rough_plaster", new Color(1.3f, 1.3f, 1.27f));   // fresh whitewash (the scan alone reads grey)
            var plinth = sm.Photo("concrete_wall_008", new Color(0.62f, 0.63f, 0.62f));

            // the wall's line: the track pushed out to the road's hill-side edge (clear of every carriageway)
            var line = new List<Vector2>();
            for (int i = 0; i < ArmyTrack.Length; i++)
            {
                Vector2 a = ArmyTrack[Mathf.Max(i - 1, 0)], b = ArmyTrack[Mathf.Min(i + 1, ArmyTrack.Length - 1)];
                Vector2 t = (b - a).normalized, east = new Vector2(t.y, -t.x);
                if (east.x < 0f) east = -east;
                Vector2 p = ArmyTrack[i] + east * 6.5f;
                ClearOfRoads(ctx, ref p, 1.2f, "Army Rest wall");
                line.Add(p);
            }
            const float wallH = 2.3f, wallT = 0.3f, gateW = 5.2f;
            // the gate stands where the compound's own road comes through the wall (owner, 24 Nov 2026: "army rest centre
            // ki road gate se align nhi hai" - it stood 4 m up the wall from the service road into the compound)
            int gi = ArmyGateAt;
            {
                Vector2 want = line[ArmyGateAt];
                float best = 25f;
                int bestSeg = -1;
                Vector2 bestX = want;
                foreach (var r in ctx.world.roads)
                {
                    if (r.pts == null || r.pts.Length < 4 || (r.name ?? "").Contains("Mall")) continue;
                    for (int k = 0; k + 3 < r.pts.Length; k += 2)
                    {
                        Vector2 a0 = new Vector2(r.pts[k], r.pts[k + 1]), a1 = new Vector2(r.pts[k + 2], r.pts[k + 3]);
                        for (int s = 0; s + 1 < line.Count; s++)
                            if (SegCross(a0, a1, line[s], line[s + 1], out var x) && (x - want).magnitude < best)
                            { best = (x - want).magnitude; bestSeg = s; bestX = x; }
                    }
                }
                if (bestSeg >= 0)
                {
                    // keep a whole wall span either side of the opening
                    float room = gateW * 0.5f + 1.2f;
                    if ((bestX - line[bestSeg]).magnitude < room) line.RemoveAt(bestSeg--);
                    if (bestSeg + 1 < line.Count && (line[bestSeg + 1] - bestX).magnitude < room) line.RemoveAt(bestSeg + 1);
                    line.Insert(bestSeg + 1, bestX);
                    gi = bestSeg + 1;
                }
                else Debug.LogWarning("[Nainital] Army Rest: no compound road through the wall; the gate stays at the track's point");
            }
            gi = Mathf.Clamp(gi, 1, line.Count - 2);
            Vector2 g = line[gi];
            Vector2 gt = (line[gi + 1] - line[gi - 1]).normalized;
            Vector2 gn = new Vector2(gt.y, -gt.x);   // from the wall towards the road
            if (Vector2.Dot(gn, ArmyTrack[Mathf.Min(ArmyGateAt, ArmyTrack.Length - 1)] - g) < 0f) gn = -gn;

            // the wall, broken for the gate
            for (int i = 0; i + 1 < line.Count; i++)
            {
                Vector2 a = line[i], b = line[i + 1];
                if (i == gi - 1) b = g - gt * (gateW * 0.5f + 0.6f);
                if (i == gi) a = g + gt * (gateW * 0.5f + 0.6f);
                float ya = ctx.H(a), yb = ctx.H(b), lo = Mathf.Min(ya, yb) - 0.8f;
                Slab(plinth, plinth, a, b, wallT + 0.08f, ya + 0.45f, yb + 0.45f, lo, 1.2f);
                Slab(plaster, plaster, a, b, wallT, ya + wallH, yb + wallH, ya + 0.45f, 1.6f);
            }

            // the gate: two pillars with domed caps and red bands, the red board between them, black iron leaves
            float gy = ctx.H(g);
            float yaw = Mathf.Atan2(gn.x, gn.y) * Mathf.Rad2Deg;
            var q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 G(float x, float y, float z) => new Vector3(g.x, gy, g.y) + q * new Vector3(x, y, z);
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * (gateW * 0.5f + 0.55f);
                TBox(plaster, G(x, 1.9f, 0f), new Vector3(1.1f, 3.8f, 1.1f), q, 1.4f);
                sm.atlas.Box(G(x, 3.25f, 0f), new Vector3(1.16f, 0.22f, 1.16f), q, S(Pal.TempleRed));
                sm.atlas.Box(G(x, 3.95f, 0f), new Vector3(1.3f, 0.2f, 1.3f), q, S(Pal.TempleWhite));
                sm.atlas.Sphere(G(x, 4.35f, 0f), new Vector3(0.8f, 0.6f, 0.8f), 8, 4, S(Pal.TempleWhite));
                sm.atlas.Cylinder(G(x, 4.6f, 0f), 0.05f, 0.03f, 0.45f, 5, S(Pal.TempleRed), true);
                sm.atlas.Box(G(x, 0.25f, 0f), new Vector3(1.25f, 0.5f, 1.25f), q, S(Pal.TempleRed));
            }
            // the arch board spans the pillars on a white beam
            TBox(plaster, G(0f, 3.95f, 0f), new Vector3(gateW + 0.2f, 0.35f, 0.5f), q, 1.4f);
            Board(sm, G(0f, 4.45f, 0.3f), yaw, gateW - 0.2f, 0.62f, Pal.TempleRed, new[] { "ARMY REST & RECOUP CENTRE NAINITAL" }, new[] { 0.2f },
                  Pal.TempleWhite, Pal.TempleWhite, gy + 4.1f);
            // the two black iron leaves, shut
            foreach (float sx in new[] { -1f, 1f })
            {
                float x0 = sx * 0.02f, x1 = sx * gateW * 0.5f;
                for (float yy = 0.15f; yy < 2.2f; yy += 0.95f)
                    sm.atlas.Box(G((x0 + x1) * 0.5f, yy, 0f), new Vector3(Mathf.Abs(x1 - x0), 0.07f, 0.07f), q, S(Pal.IronBlack));
                for (float x = 0.12f; x < gateW * 0.5f; x += 0.16f)
                    sm.atlas.Box(G(sx * x, 1.1f, 0f), new Vector3(0.035f, 2.0f, 0.035f), q, S(Pal.IronBlack));
                sm.atlas.Box(G(sx * gateW * 0.25f, 1.3f, 0.05f), new Vector3(0.7f, 0.7f, 0.04f), q, S(Pal.TempleRed));   // the round crest
            }
            // collision across the leaves
            Vector3 l0 = G(-gateW * 0.5f, 0f, 0f), l1 = G(gateW * 0.5f, 0f, 0f);
            Vector3 side = new Vector3(gn.x, 0f, gn.y);
            sm.col.Quad(l0, l1, l1 + Vector3.up * 2.4f, l0 + Vector3.up * 2.4f, Vector2.zero, side);
            sm.col.Quad(l1, l0, l0 + Vector3.up * 2.4f, l1 + Vector3.up * 2.4f, Vector2.zero, -side);

            sm.Emit(ctx, "Army Rest & Recoup Centre", parent);
            Debug.Log($"[Nainital] Army Rest & Recoup Centre: wall {line.Count - 1} spans, gate at ({g.x:F1}, {g.y:F1})");
        }

        /// <summary>Where segment a0-a1 crosses segment b0-b1.</summary>
        static bool SegCross(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1, out Vector2 x)
        {
            x = default;
            Vector2 r = a1 - a0, s = b1 - b0;
            float den = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(den) < 1e-6f) return false;
            Vector2 d = b0 - a0;
            float t = (d.x * s.y - d.y * s.x) / den, u = (d.x * r.y - d.y * r.x) / den;
            if (t < 0f || t > 1f || u < 0f || u > 1f) return false;
            x = a0 + r * t;
            return true;
        }
    }
}
