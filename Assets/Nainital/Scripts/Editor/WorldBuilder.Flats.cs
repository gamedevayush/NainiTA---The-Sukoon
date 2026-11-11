using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>The Flats / DSA ground as 2025 videos show it: a big level ground of tan grit with white pitch lines and
    /// goals, stepped stands painted red, teal and white under tarps along the side away from the lake, a rock-climbing tower
    /// in a corner, pull-up bars, and a red iron railing between brick pillars all round with a blue gate.</summary>
    public static partial class WorldBuilder
    {
        static float PolyArea(Vector2[] p)
        {
            float a = 0f;
            for (int i = 0; i < p.Length; i++) { var q = p[(i + 1) % p.Length]; a += p[i].x * q.y - q.x * p[i].y; }
            return Mathf.Abs(a) * 0.5f;
        }

        static void BuildFlats(WorldCtx ctx)
        {
            Vector2[] poly = null;
            foreach (var a in ctx.world.areas)
            {
                if (a.kind != "stadium") continue;
                var p = Geo.ToPoints(a.pts);
                if (p.Length >= 3 && (poly == null || PolyArea(p) > PolyArea(poly))) poly = p;
            }
            if (poly == null) return;
            var parent = new GameObject("The Flats (DSA ground)").transform;
            parent.SetParent(ctx.worldRoot, false);

            // the ground's frame: the smallest box round it; u along the long side
            Vector2 cen = Vector2.zero;
            foreach (var p in poly) cen += p;
            cen /= poly.Length;
            float best = float.MaxValue, hu = 0f, hv = 0f, ang = 0f;
            Vector2 mid = cen;
            for (int d = 0; d < 180; d++)
            {
                Vector2 uu = new Vector2(Mathf.Cos(d * Mathf.Deg2Rad), Mathf.Sin(d * Mathf.Deg2Rad)), vv = new Vector2(-uu.y, uu.x);
                float u0 = 1e9f, u1 = -1e9f, v0 = 1e9f, v1 = -1e9f;
                foreach (var p in poly) { float pu = Vector2.Dot(p - cen, uu), pv = Vector2.Dot(p - cen, vv); u0 = Mathf.Min(u0, pu); u1 = Mathf.Max(u1, pu); v0 = Mathf.Min(v0, pv); v1 = Mathf.Max(v1, pv); }
                if ((u1 - u0) * (v1 - v0) < best && u1 - u0 >= v1 - v0)
                { best = (u1 - u0) * (v1 - v0); hu = (u1 - u0) * 0.5f; hv = (v1 - v0) * 0.5f; ang = d; mid = cen + uu * ((u0 + u1) * 0.5f) + vv * ((v0 + v1) * 0.5f); }
            }
            Vector2 U = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)), Vv = new Vector2(-U.y, U.x);
            Vector2 F(float u, float v) => mid + U * u + Vv * v;
            // the stands go along the long side away from the lake
            float standSide = Vector2.Distance(F(0f, hv), ctx.lakeCenter) > Vector2.Distance(F(0f, -hv), ctx.lakeCenter) ? 1f : -1f;

            // ---- the ground: pale grey grit, a 2 m grid over the polygon
            var grit = PhotoMaterial("concrete_floor_worn_001", "M_FlatsGrit", new Color(1.45f, 1.3f, 1.05f), 0.05f) ?? ctx.mats.paver;
            var gm = new MeshBuilder();
            const float cell = 2f;
            for (float u = -hu; u < hu; u += cell)
                for (float v = -hv; v < hv; v += cell)
                {
                    Vector2 a = F(u, v), b = F(u + cell, v), c = F(u + cell, v + cell), d = F(u, v + cell);
                    if (!Geo.PointInPolygon(poly, F(u + cell * 0.5f, v + cell * 0.5f))) continue;
                    gm.Quad(V(a, ctx.H(a) + 0.04f), V(b, ctx.H(b) + 0.04f), V(c, ctx.H(c) + 0.04f), V(d, ctx.H(d) + 0.04f), new Rect(a.x / 5f, a.y / 5f, cell / 5f, cell / 5f), Vector3.up);
                }
            ctx.MeshObject("Flats ground", parent, gm, grit, null, false);

            // ---- pitch lines and goals, inside the ground with room for the stands
            var paint = new MeshBuilder();
            Vector2 white = Atlas.SolidUV(Pal.ShirtWhite);
            float L = Mathf.Min(hu * 2f - 24f, 96f) * 0.5f, Wd = Mathf.Min(hv * 2f - 30f, 60f) * 0.5f;
            float cv = -standSide * 3f;   // the pitch sits a little off the stands
            void Line(Vector2 a, Vector2 b, float w = 0.12f)
            {
                Vector2 dir = (b - a).normalized, n = new Vector2(-dir.y, dir.x) * (w * 0.5f);
                float y = Mathf.Max(ctx.H(a), ctx.H(b)) + 0.07f;
                paint.Quad(V(a - n, y), V(b - n, y), V(b + n, y), V(a + n, y), white, Vector3.up);
            }
            Vector2 P(float u, float v) => F(u, cv + v);
            if (L > 20f && Wd > 12f)
            {
                Line(P(-L, -Wd), P(L, -Wd)); Line(P(L, -Wd), P(L, Wd)); Line(P(L, Wd), P(-L, Wd)); Line(P(-L, Wd), P(-L, -Wd));
                Line(P(0f, -Wd), P(0f, Wd));
                for (int k = 0; k < 32; k++)
                {
                    float a0 = k * Mathf.PI / 16f, a1 = (k + 1) * Mathf.PI / 16f;
                    Line(P(Mathf.Cos(a0) * 9.15f, Mathf.Sin(a0) * 9.15f), P(Mathf.Cos(a1) * 9.15f, Mathf.Sin(a1) * 9.15f));
                }
                float box = Mathf.Min(16.5f, L * 0.3f), boxW = Mathf.Min(20f, Wd * 0.75f), small = Mathf.Min(5.5f, box * 0.4f), smallW = Mathf.Min(9.2f, boxW * 0.5f);
                var goalMb = new MeshBuilder();
                foreach (float e in new[] { -1f, 1f })
                {
                    Line(P(e * L, -boxW), P(e * (L - box), -boxW)); Line(P(e * (L - box), -boxW), P(e * (L - box), boxW)); Line(P(e * (L - box), boxW), P(e * L, boxW));
                    Line(P(e * L, -smallW), P(e * (L - small), -smallW)); Line(P(e * (L - small), -smallW), P(e * (L - small), smallW)); Line(P(e * (L - small), smallW), P(e * L, smallW));
                    // the goal: white posts and bar, a frame at the back
                    Vector2 g0 = P(e * L, -3.66f), g1 = P(e * L, 3.66f);
                    float gy = ctx.H(P(e * L, 0f));
                    foreach (var g in new[] { g0, g1 }) goalMb.Cylinder(V(g, gy), 0.07f, 0.07f, 2.44f, 8, white, true);
                    goalMb.Limb(V(g0, gy + 2.44f), V(g1, gy + 2.44f), 0.07f, 0.07f, 8, white);
                    Vector2 b0 = P(e * (L + 1.8f), -3.66f), b1 = P(e * (L + 1.8f), 3.66f);
                    goalMb.Limb(V(b0, gy), V(b1, gy), 0.03f, 0.03f, 6, white);
                    goalMb.Limb(V(g0, gy + 2.44f), V(b0, gy), 0.03f, 0.03f, 6, white);
                    goalMb.Limb(V(g1, gy + 2.44f), V(b1, gy), 0.03f, 0.03f, 6, white);
                }
                ctx.MeshObject("Flats goals", parent, goalMb, ctx.mats.atlas, null, true);
            }
            ctx.MeshObject("Flats lines", parent, paint, ctx.mats.atlas, null, false);

            // ---- the stands: six tiers in red, teal and white bands, a white back wall, tarps over the middle
            var mb = new MeshBuilder();
            var col = new MeshBuilder();
            Vector2 red = Atlas.SolidUV(Pal.TempleRed), teal = Atlas.SolidUV(Pal.BoatGreen), whiteWall = Atlas.WallSolid(0);
            Vector2[] bands = { red, teal, white, red, teal, white };
            float sLen = hu * 1.1f, front = standSide * (hv - 9.5f);
            var srot = Quaternion.LookRotation(Flat(-Vv * standSide), Vector3.up);
            float gyS = ctx.H(F(0f, front));
            for (int k = 0; k < 6; k++)
            {
                float vMid = front + standSide * (k + 0.5f) * 0.85f, h = (k + 1) * 0.36f;
                Vector3 cc = V(F(0f, vMid), gyS) + Vector3.up * (h * 0.5f);
                var size = new Vector3(sLen, h, 0.86f);
                mb.Box(cc, size, srot, bands[k]);
                col.Box(cc, size, srot, Vector2.zero);
            }
            float back = front + standSide * 5.4f;
            Vector3 wc = V(F(0f, back), gyS) + Vector3.up * 1.6f;
            mb.Box(wc, new Vector3(sLen + 0.4f, 3.2f, 0.3f), srot, whiteWall);
            col.Box(wc, new Vector3(sLen + 0.4f, 3.2f, 0.3f), srot, Vector2.zero);
            int[] tarps = { Pal.BoatBlue, Pal.BoatYellow, Pal.BoatBlue, Pal.BoatYellow };
            for (int t = 0; t < 4; t++)
            {
                float u = (t - 1.5f) * (sLen * 0.22f);
                Vector2 a0 = F(u - sLen * 0.1f, front + standSide * 0.3f), a1 = F(u + sLen * 0.1f, front + standSide * 0.3f);
                Vector2 b0 = F(u - sLen * 0.1f, back), b1 = F(u + sLen * 0.1f, back);
                float ty = gyS + 4.6f;
                foreach (var q in new[] { a0, a1 }) mb.Cylinder(V(q, gyS), 0.06f, 0.06f, 3.9f, 6, Atlas.SolidUV(Pal.IronBlack), true);
                Vector2 tp = Atlas.SolidUV(tarps[t]);
                mb.QuadDouble(V(a0, ty - 0.7f), V(a1, ty - 0.7f), V(b1, ty), V(b0, ty), new Rect(tp.x, tp.y, 0f, 0f), Vector3.up);
            }

            // the stands' front rail: orange-red tube
            for (int k = 0; k < 2; k++)
            {
                Vector2 r0 = F(-sLen * 0.5f, front - standSide * 0.2f), r1 = F(sLen * 0.5f, front - standSide * 0.2f);
                float ry = gyS + 0.55f + k * 0.45f;
                mb.Limb(V(r0, ry), V(r1, ry), 0.05f, 0.05f, 8, Atlas.SolidUV(Pal.SareeOrange));
            }
            for (float u = -sLen * 0.5f; u <= sLen * 0.5f + 0.1f; u += 2.5f)
                mb.Cylinder(V(F(u, front - standSide * 0.2f), gyS), 0.04f, 0.04f, 1f, 6, Atlas.SolidUV(Pal.SareeOrange), true);

            // ---- the rock-climbing tower on the lake side (white, a red-orange chevron at the top), and pull-up bars
            Vector2 tw = F(-hu * 0.3f, -standSide * (hv - 9f));
            float ty0 = ctx.H(tw);
            var trot = Quaternion.LookRotation(Flat(Vv * standSide), Vector3.up);   // its climbing face looks at the stands
            Vector3 tc = V(tw, ty0);
            mb.Box(tc + Vector3.up * 7f, new Vector3(4.6f, 14f, 3f), trot, whiteWall);
            col.Box(tc + Vector3.up * 7f, new Vector3(4.6f, 14f, 3f), trot, Vector2.zero);
            Vector3 tf = trot * Vector3.forward, tr = trot * Vector3.right;
            mb.Box(tc + Vector3.up * 7f + tf * 1.52f + tr * 1.6f, new Vector3(1.2f, 14f, 0.04f), trot, Atlas.SolidUV(Pal.SareeOrange));   // painted bands up the face
            mb.Box(tc + Vector3.up * 7f + tf * 1.52f - tr * 1.6f, new Vector3(0.8f, 14f, 0.04f), trot, red);
            var rnd = new System.Random(306);
            int[] holds = { Pal.JacketRed, Pal.BoatYellow, Pal.JacketBlue, Pal.JacketGreen, Pal.SareeOrange };
            for (int k = 0; k < 70; k++)
                mb.Box(tc + Vector3.up * (0.6f + (float)rnd.NextDouble() * 12.8f) + tr * ((float)rnd.NextDouble() * 4f - 2f) + tf * 1.56f,
                       new Vector3(0.16f, 0.12f, 0.1f), trot, Atlas.SolidUV(holds[rnd.Next(holds.Length)]));
            mb.Box(tc + Vector3.up * 14.15f, new Vector3(5f, 0.3f, 3.4f), trot, red);
            for (int k = 0; k < 8; k++)   // the chevron painted near the top, on every face
            {
                float w = 4.6f * (1f - k / 8f), yy = 12.6f - k * 0.42f;
                foreach (var fv in new[] { tf, -tf })
                    mb.Box(tc + Vector3.up * yy + fv * 1.53f, new Vector3(w, 0.42f, 0.03f), trot, k % 2 == 0 ? red : Atlas.SolidUV(Pal.SareeOrange));
            }
            Vector2 bar = F(-hu * 0.7f, -standSide * (hv - 9f));
            float by = ctx.H(bar);
            for (int k = 0; k < 3; k++)
            {
                Vector2 q0 = bar + U * (k * 2.2f), q1 = q0 + U * 1.6f;
                foreach (var q in new[] { q0, q1 }) mb.Cylinder(V(q, by), 0.04f, 0.04f, 2.3f + k * 0.2f, 6, Atlas.SolidUV(Pal.IronBlack), true);
                mb.Limb(V(q0, by + 2.3f + k * 0.2f), V(q1, by + 2.3f + k * 0.2f), 0.025f, 0.025f, 6, Atlas.SolidUV(Pal.IronBlack));
            }

            // ---- the railing round it: brick pillars, red iron bars, a blue gate towards the bazaar
            var gateAt = ctx.Poi("Bada Bazar") ?? ctx.Poi("Naina Devi Temple") ?? mid;
            int gateEdge = 0;
            float gateD = float.MaxValue;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 m2 = (poly[i] + poly[(i + 1) % poly.Length]) * 0.5f;
                if (Vector2.Distance(m2, gateAt) < gateD && Vector2.Distance(poly[i], poly[(i + 1) % poly.Length]) > 8f) { gateD = Vector2.Distance(m2, gateAt); gateEdge = i; }
            }
            Vector2 brick = Atlas.SolidUV(Pal.BoatRed), iron = Atlas.SolidUV(Pal.TempleRed), blue = Atlas.SolidUV(Pal.BoatBlue), cap = Atlas.MaterialUV(Atlas.MatConcrete);
            int pillars = 0;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                float len = Vector2.Distance(a, b);
                int n = Mathf.Max(1, Mathf.RoundToInt(len / 3f));
                Vector2 dir = (b - a) / len;
                var rrot = Quaternion.LookRotation(Flat(dir), Vector3.up);
                for (int k = 0; k <= n; k++)
                {
                    Vector2 q = Vector2.Lerp(a, b, k / (float)n);
                    bool gate = i == gateEdge && Mathf.Abs(k / (float)n - 0.5f) * len < 2.4f;
                    if (Mathf.Abs(Vector2.Dot(q - mid, Vv) - (hv - 4f) * standSide) < 3f && standSide * Vector2.Dot(q - mid, Vv) > 0f && Mathf.Abs(Vector2.Dot(q - mid, U)) < sLen * 0.5f) continue;   // behind the stands
                    float y = ctx.H(q);
                    if (!gate)
                    {
                        mb.Box(V(q, y) + Vector3.up * 0.65f, new Vector3(0.42f, 1.3f, 0.42f), rrot, brick);
                        mb.Box(V(q, y) + Vector3.up * 1.34f, new Vector3(0.5f, 0.08f, 0.5f), rrot, cap);
                        col.Box(V(q, y) + Vector3.up * 0.65f, new Vector3(0.42f, 1.3f, 0.42f), rrot, Vector2.zero);
                        pillars++;
                    }
                    if (k == n) continue;
                    Vector2 q2 = Vector2.Lerp(a, b, (k + 1) / (float)n);
                    bool gate2 = i == gateEdge && Mathf.Abs((k + 0.5f) / n - 0.5f) * len < 2.4f;
                    float seg = Vector2.Distance(q, q2), y2 = ctx.H(q2);
                    Vector3 m3 = V((q + q2) * 0.5f, (y + y2) * 0.5f);
                    if (gate2)
                    {   // the blue gate, swung half open
                        mb.Box(m3 + Vector3.up * 0.9f + Flat(Vector2.Perpendicular(dir)) * 0.6f, new Vector3(0.05f, 1.6f, seg * 0.6f), rrot * Quaternion.Euler(0f, 60f, 0f), blue);
                        continue;
                    }
                    mb.Box(m3 + Vector3.up * 0.3f, new Vector3(0.05f, 0.05f, seg), rrot, iron);
                    mb.Box(m3 + Vector3.up * 1.1f, new Vector3(0.05f, 0.05f, seg), rrot, iron);
                    for (float s = -seg * 0.5f + 0.25f; s < seg * 0.5f - 0.2f; s += 0.18f)
                        mb.Box(m3 + Vector3.up * 0.7f + Flat(dir) * s, new Vector3(0.03f, 0.8f, 0.03f), rrot, iron);
                    col.Box(m3 + Vector3.up * 0.65f, new Vector3(0.1f, 1.3f, seg), rrot, Vector2.zero);
                }
            }
            ctx.MeshObject("Flats stands, tower and railing", parent, mb, ctx.mats.atlas, col);

            // camera for the preview: from the gate side, looking across the ground at the stands
            var shot = new GameObject("FlatsShot");
            shot.transform.SetParent(parent, false);
            Vector2 eye = F(-hu * 0.45f, -standSide * (hv - 14f));
            Vector3 e3 = V(eye, ctx.H(eye) + 1.7f);
            shot.transform.SetPositionAndRotation(e3, Quaternion.LookRotation(V(F(hu * 0.2f, standSide * hv * 0.6f), ctx.H(mid) + 3f) - e3));
            Debug.Log($"[Nainital] The Flats: ground {hu * 2f:F0} x {hv * 2f:F0} m, stands {sLen:F0} m, {pillars} railing pillars");
        }
    }
}
