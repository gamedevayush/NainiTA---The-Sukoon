using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Khurpatal Lake View Point and the Mansa Devi Mandir above it (user, 2026-11-19: Street View of the pull-off,
    /// photos of the cypress clearing, a sketch - "green in plain, red in rocky stairs or just kinda rock trek, whole place
    /// is full of trees"; then "mansa devi mandir just above it on road", and on a screenshot of the first build: "khurpatal
    /// view point is not red, its at green" - at the hairpin's tip, not on its west arm). On the outer side of the tip of
    /// the Kaladhungi road hairpin between Lover's Point and Sariyatal: a gravel pull-off round the tip where taxis stop,
    /// snack stalls under blue tarps, concrete bench blocks along the edge. From it a dirt path runs straight out along a
    /// spur and bends towards Khurpatal (level-ish), then rough rock steps go down to a bare earth clearing among
    /// Himalayan cypresses - one of them the many-trunked tree of the photos - looking down the valley to Khurpatal. On
    /// the knoll inside the hairpin, just above, the little white Mansa Devi temple. The slope falls ~35 degrees, so the
    /// spur is shaped from the levelled road: the walk's level along its crest, the flanks falling into the hillside.
    /// </summary>
    public static partial class LandmarkBuilder
    {
        static readonly Vector2 KlRoadPoint = new Vector2(-1830f, -1143f);   // the hairpin's tip (it points south)
        static readonly Vector2 KlLake = new Vector2(-2591f, -1995f);        // Khurpatal: the clearing looks its way
        // the walk as sketched (dx, dz from its head, drop below the pull-off; steps from node 4 on, red), turned to face the lake
        static readonly float[] KlWalk =
        {
            0f, 0f, 0f,
            -4.0f, -1.1f, -0.3f,
            -8.3f, -2.1f, -0.9f,
            -13.1f, -3.7f, -1.7f,
            -16.8f, -4.3f, -2.5f,
            -20.5f, -3.9f, -3.8f,
            -23.7f, -3.4f, -5.0f,
            -26.4f, -3.7f, -6.3f,
            -29.3f, -3.4f, -7.4f,
        };
        const int KlFirstStep = 4;
        const float KlVerge = 5f, KlClearR = 5.5f;

        sealed class KlFrame
        {
            public Vector2[] road; public float hw, s0, valley, along;
            public readonly List<Vector2> walk = new List<Vector2>();
            public readonly List<float> drop = new List<float>();
            public Vector2 clear;
            /// <summary>The point `d` metres from the road's centre line (+ = the lake side) at distance s along it.</summary>
            public Vector2 At(float s, float d) { var p = PointAt(road, s, out var dir); return p + new Vector2(dir.y, -dir.x) * (d * valley); }
            public float RoadY(WorldCtx ctx, float s) => (ctx.H(At(s - 3f, 0f)) + ctx.H(At(s, 0f)) * 2f + ctx.H(At(s + 3f, 0f))) * 0.25f;
            public float Rel(float s) => (s - s0) * along;
            public float S(float rel) => s0 + rel * along;
            public static float Verge(float rel) => rel < -15f || rel > 13f ? 0f : rel < -12f ? Mathf.Lerp(0.5f, KlVerge, (rel + 15f) / 3f)
                                                    : rel > 10f ? Mathf.Lerp(KlVerge, 0.5f, (rel - 10f) / 3f) : KlVerge;
        }

        static KlFrame KlMake(WorldCtx ctx)
        {
            var f = new KlFrame();
            f.road = RoadLine(ctx, "Kaldhungi-Nainital Road", KlRoadPoint, out f.hw);
            if (f.road == null) { Debug.LogWarning("[Nainital] Khurpatal Lake View Point: Kaladhungi road not found"); return null; }
            Project(f.road, KlRoadPoint, out float sGuess, out _, out _, out _);
            // the tip: the southernmost point of the loop near the guess (the corners were rounded since the map)
            f.s0 = sGuess;
            float minZ = float.MaxValue;
            for (float s = sGuess - 30f; s <= sGuess + 30f; s += 0.5f)
            {
                var p = PointAt(f.road, s, out _);
                if (p.y < minZ) { minZ = p.y; f.s0 = s; }
            }
            Project(f.road, PointAt(f.road, f.s0, out _) + new Vector2(0f, -20f), out _, out float side, out _, out _);
            f.valley = side >= 0f ? 1f : -1f;   // the outer side of the tip
            // "along" = + uphill along the road (the temple stands a little that way)
            f.along = 1f;
            if (f.RoadY(ctx, f.s0 - 12f) > f.RoadY(ctx, f.s0 + 12f)) f.along = -1f;
            Vector2 start = f.At(f.s0, f.hw + KlVerge - 0.3f);
            // straight out from the tip, bending over the first ~12 m towards Khurpatal (the owner's green line)
            Vector2 outN = (start - PointAt(f.road, f.s0, out _)).normalized;
            float h0 = Mathf.Atan2(outN.x, outN.y), h1 = Mathf.Atan2(KlLake.x - start.x, KlLake.y - start.y);
            h1 = h0 + Mathf.DeltaAngle(h0 * Mathf.Rad2Deg, h1 * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            Vector2 at = start;
            float run = 0f;
            for (int i = 0; i + 2 < KlWalk.Length; i += 3)
            {
                if (i > 0)
                {
                    float seg = Vector2.Distance(new Vector2(KlWalk[i - 3], KlWalk[i - 2]), new Vector2(KlWalk[i], KlWalk[i + 1]));
                    float hd = Mathf.Lerp(h0, h1, Mathf.SmoothStep(0f, 1f, (run + seg * 0.5f) / 12f));
                    at += new Vector2(Mathf.Sin(hd), Mathf.Cos(hd)) * seg;
                    run += seg;
                }
                f.walk.Add(at);
                f.drop.Add(KlWalk[i + 2]);
            }
            Vector2 last = f.walk[f.walk.Count - 1], dir = (last - f.walk[f.walk.Count - 2]).normalized;
            f.clear = last + dir * 2.6f;
            return f;
        }

        static float KlLevel(KlFrame f, float y0, float sw)
        {
            float run = 0f;
            for (int i = 0; i + 1 < f.walk.Count; i++)
            {
                float len = Vector2.Distance(f.walk[i], f.walk[i + 1]);
                if (run + len >= sw || i + 2 == f.walk.Count) return y0 + Mathf.Lerp(f.drop[i], f.drop[i + 1], Mathf.Clamp01((sw - run) / Mathf.Max(len, 1e-3f)));
                run += len;
            }
            return y0;
        }

        static int SegIndex(KlFrame f, float sw)
        {
            float run = 0f;
            for (int i = 0; i + 1 < f.walk.Count; i++)
            {
                run += Vector2.Distance(f.walk[i], f.walk[i + 1]);
                if (sw <= run) return i;
            }
            return f.walk.Count - 2;
        }

        /// <summary>Where the temple stands: on the knoll inside the hairpin, facing the tip and the walk's head.</summary>
        static Vector2 KlTemple(KlFrame f, out float s) { s = f.S(0f); return f.At(s, -(f.hw + 3.0f)); }

        /// <summary>Circles for the planners: the pull-off's road edge (no parapet), the walk, the clearing, the temple.</summary>
        internal static IEnumerable<(Vector2 c, float r)> KhurpatalLakeSpots(WorldCtx ctx)
        {
            var f = KlMake(ctx);
            if (f == null) yield break;
            for (float rel = -14f; rel <= 12f; rel += 2f) yield return (f.At(f.S(rel), f.hw + 0.6f), 1.6f);
            foreach (var p in f.walk) yield return (p, 1.6f);
            yield return (f.clear, KlClearR - 1f);
            yield return (KlTemple(f, out _), 3.5f);
        }

        static void BuildKhurpatalLakeView(WorldCtx ctx, Transform parent)
        {
            var f = KlMake(ctx);
            if (f == null) return;
            var rng = new System.Random(2310);
            float y0 = f.RoadY(ctx, f.s0) + 0.03f;
            float total = 0f;
            for (int i = 0; i + 1 < f.walk.Count; i++) total += Vector2.Distance(f.walk[i], f.walk[i + 1]);
            float yEnd = y0 + f.drop[f.drop.Count - 1] - 0.15f;
            Vector2 templeC = KlTemple(f, out float templeS);
            float templeY = f.RoadY(ctx, templeS) + 0.25f;

            // 1. the ground: the pull-off level with the road, the walk's crest at its own level (cut or filled), the spur's
            // flanks and the pull-off's bank falling at ~35 degrees until they meet the hillside; a pad cut for the temple
            Vector2 bmin = Vector2.Min(Vector2.Min(f.walk[0], f.clear), templeC) - new Vector2(45f, 45f);
            Vector2 bmax = Vector2.Max(Vector2.Max(f.walk[0], f.clear), templeC) + new Vector2(45f, 45f);
            Vector2 tAlong = (f.At(templeS + 1f, 0f) - f.At(templeS - 1f, 0f)).normalized, tIn = new Vector2(-tAlong.y, tAlong.x);
            ShapeTerrain(ctx, bmin, bmax, (p, y) =>
            {
                Project(f.road, p, out float s, out float side, out _, out _);
                float d = side * f.valley, rel = f.Rel(s);
                if (Mathf.Abs(d) < f.hw + 0.3f) return y;   // any part of the carriageway is the road's own (LevelRoads)
                Vector2 tq = p - templeC;
                if (Mathf.Abs(Vector2.Dot(tq, tAlong)) < 3.4f && Mathf.Abs(Vector2.Dot(tq, tIn)) < 2.6f) return Mathf.Min(y, templeY - 0.05f);
                float best = y;
                if (d > 0f)
                {
                    float v = KlFrame.Verge(rel), tv = f.RoadY(ctx, s) - 0.02f;
                    float taper = Mathf.Max(0f, Mathf.Max(-12f - rel, rel - 10f));
                    if (v > 0.6f && d <= f.hw + v) return tv;
                    if (rel > -20f && rel < 18f) best = Mathf.Max(best, tv - 0.75f * (Mathf.Max(0f, d - f.hw - Mathf.Max(v, 0.5f)) + taper * 1.5f));
                }
                Project(f.walk, p, out float sw, out float lat, out _, out _);
                float prof = KlLevel(f, y0, sw);
                bool onSteps = sw > 0f && SegIndex(f, sw) >= KlFirstStep;
                float rc = Vector2.Distance(p, f.clear);
                if (rc < KlClearR) return yEnd - 0.04f;
                if (sw > 0.05f && sw < total - 0.05f && Mathf.Abs(lat) < 1.4f) return onSteps ? prof - 0.25f : prof - 0.04f;
                // past the walk's end the crest does not run on into the valley (it made a causeway, 19 Nov)
                float off = sw >= total - 0.05f ? Vector2.Distance(p, f.walk[f.walk.Count - 1]) : Mathf.Abs(lat);
                float ridge = sw <= 0.05f ? float.MinValue : prof - 0.15f - 0.7f * Mathf.Max(0f, off - 1.4f);
                float round = yEnd - 0.04f - 0.7f * Mathf.Max(0f, rc - KlClearR);
                return Mathf.Max(best, Mathf.Max(ridge, round));
            });
            PaintTerrain(ctx, bmin, bmax, (p, slope) =>
            {
                Project(f.walk, p, out _, out _, out _, out float dw);
                float rc = Vector2.Distance(p, f.clear);
                if (rc < KlClearR + 0.5f || dw < 1.6f) return new Vector4(0.05f, 0.2f, 0.15f, 0.6f);   // bare trodden earth
                Project(f.road, p, out float s, out float side, out _, out _);
                float d = side * f.valley, rel = f.Rel(s);
                if (d > f.hw && d < f.hw + KlFrame.Verge(rel) + 0.5f) return new Vector4(0f, 0.05f, 0.45f, 0.5f);   // gravel
                if (slope > 42f && (dw < 22f || rc < 22f)) return new Vector4(0.02f, 0.45f, 0.53f, 0f);   // the spur's steep flanks
                return null;
            });

            var sm = new SiteMeshes();
            var gravel = sm.Photo("concrete_floor_worn_001", new Color(0.98f, 0.97f, 0.94f));
            var stone = sm.Photo("old_stone_wall", new Color(0.82f, 0.8f, 0.76f));
            var rock = sm.Photo(GreyRock(), new Color(0.78f, 0.78f, 0.76f), 0.1f);
            Vector3 VergeP(float rel, float d) { float s = f.S(rel); return V3(f.At(s, d), f.RoadY(ctx, s) + 0.03f); }
            float FaceRoad(Vector2 p) { Project(f.road, p, out float s, out _, out _, out _); Vector2 dd = PointAt(f.road, s, out _) - p; return Mathf.Atan2(dd.x, dd.y) * Mathf.Rad2Deg; }

            // 2. the pull-off: gravel from the road edge, concrete bench blocks along its edge, stalls under blue tarps
            var inner = new List<Vector3>(); var outer = new List<Vector3>();
            for (float rel = -15f; rel <= 13.01f; rel += 1f)
            {
                float s = f.S(rel), yy = f.RoadY(ctx, s) + 0.03f;
                inner.Add(V3(f.At(s, f.hw + 0.02f), yy));
                outer.Add(V3(f.At(s, f.hw + Mathf.Max(KlFrame.Verge(rel), 0.5f)), yy));
            }
            Band(ctx, gravel, stone, inner, outer, false, true);
            for (float rel = -11f; rel <= 9.5f; rel += 2.3f)
            {
                if (Mathf.Abs(rel) < 1.8f || rel > 3f && rel < 9.6f) continue;   // the walk's opening; the stalls
                Vector3 bp = VergeP(rel, f.hw + KlVerge - 0.45f);
                sm.atlas.Box(bp + Vector3.up * 0.22f, new Vector3(0.48f, 0.45f, 1.7f), Quaternion.Euler(0f, FaceRoad(XZ(bp)), 0f), S(Pal.Concrete));
            }
            foreach (float rel in new[] { 4.6f, 8.3f })
            {
                Vector3 sp = VergeP(rel, f.hw + KlVerge - 1.7f);
                TarpStall(sm.atlas, sp, FaceRoad(XZ(sp)), rel < 6f ? Pal.BoatBlue : Pal.JacketBlue, rng);
            }
            {
                // the big blue tarp stretched over the seats by the walk's head, tied to poles at the edge and on the bank
                Vector3 a0 = VergeP(0.9f, f.hw + KlVerge - 2.6f), a1 = VergeP(6.4f, f.hw + KlVerge - 2.6f);
                Vector3 b0 = VergeP(0.9f, f.hw + KlVerge + 1.6f), b1 = VergeP(6.4f, f.hw + KlVerge + 1.6f);
                var poles = new[] { a0, a1, b0, b1 };
                float[] hts = { 2.9f, 2.9f, 2.3f, 2.3f };
                for (int k = 0; k < 4; k++)
                {
                    Vector3 g = poles[k]; g.y = Mathf.Min(ctx.H(XZ(g)), g.y) - 0.2f;
                    sm.atlas.Limb(g, poles[k] + Vector3.up * hts[k], 0.045f, 0.04f, 5, S(Pal.WoodLight));
                }
                sm.loose.QuadDouble(b0 + Vector3.up * 2.3f, b1 + Vector3.up * 2.3f, a1 + Vector3.up * 2.9f, a0 + Vector3.up * 2.9f, new Rect(S(Pal.BoatBlue), Vector2.zero), Vector3.up);
                foreach (float rel in new[] { 2.2f, 4.6f }) Bench(sm.atlas, VergeP(rel, f.hw + KlVerge - 0.9f), FaceRoad(XZ(VergeP(rel, f.hw + KlVerge - 0.9f))) + 180f, Pal.WoodLight, Pal.WoodDark, 1.5f, false);
            }
            {
                Vector2 sDir = (f.walk[1] - f.walk[0]).normalized, sR = new Vector2(sDir.y, -sDir.x);
                Vector2 bp = f.walk[0] + sR * 1.6f + sDir * 0.6f;
                Board(sm, V3(bp, ctx.H(bp) + 1.75f), FaceRoad(bp), 1.6f, 0.75f, Pal.BoatGreen, new[] { "KHURPATAL LAKE", "VIEW POINT" },
                      new[] { 0.19f, 0.19f }, Pal.ShirtWhite, Pal.IronGreen, ctx.H(bp));
            }

            // 3. the walk: a dirt path along the crest (the ground itself), then rough rock steps down to the clearing
            var steps = new List<WalkNode>();
            for (int i = KlFirstStep; i < f.walk.Count; i++) steps.Add(new WalkNode(f.walk[i], y0 + f.drop[i], i + 1 < f.walk.Count));
            Walkway(ctx, sm, steps, 1.5f, rock, rock, 0.2f);
            for (int i = 0; i + 1 < f.walk.Count; i++)
            {
                Vector2 d = (f.walk[i + 1] - f.walk[i]).normalized, r = new Vector2(d.y, -d.x);
                int n = i >= KlFirstStep ? 2 : 1;
                for (int k = 0; k < n; k++)
                {
                    float t = (k + 0.5f) / n, sx = (i + k) % 2 == 0 ? 1f : -1f;
                    Vector2 p = Vector2.Lerp(f.walk[i], f.walk[i + 1], t) + r * sx * (1.3f + (float)rng.NextDouble() * 0.6f);
                    float rr = i >= KlFirstStep ? 0.5f + (float)rng.NextDouble() * 0.4f : 0.3f + (float)rng.NextDouble() * 0.25f;
                    RockLump(rock, V3(p, ctx.H(p) - rr * 0.25f), new Vector3(rr * 1.2f, rr * 0.75f, rr), rng.Next(1000));
                }
            }

            // 4. the clearing: bare earth on the spur's end, rocks at its lip, the many-trunked cypress, a seat rock
            Vector2 endDir = (f.walk[f.walk.Count - 1] - f.walk[f.walk.Count - 2]).normalized, endR = new Vector2(endDir.y, -endDir.x);
            for (int k = 0; k < 7; k++)
            {
                float a = Mathf.Atan2(endDir.x, endDir.y) + (k - 3) * 0.42f;
                Vector2 bp = f.clear + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * (KlClearR + 0.4f + (float)rng.NextDouble() * 0.8f);
                float rr = 0.5f + (float)rng.NextDouble() * 0.6f;
                RockLump(rock, V3(bp, ctx.H(bp) - rr * 0.3f), new Vector3(rr * 1.3f, rr * 0.7f, rr), rng.Next(1000));
            }
            Vector2 seat = f.clear - endR * 2.6f + endDir * 0.5f;
            RockLump(rock, V3(seat, yEnd - 0.2f), new Vector3(0.9f, 0.5f, 0.6f), 77);
            CandelabraCypress(sm.atlas, sm.loose, V3(f.clear + endR * 3.4f + endDir * 1.6f, yEnd - 0.1f), Mathf.Atan2(endDir.x, endDir.y) * Mathf.Rad2Deg + 90f, rng);

            // 5. the trees: Himalayan cypress, oak and deodar all along the spur and round the clearing ("whole place is
            // full of trees"); the crest is too gentle for the hillside fill, so they are added here
            var extra = new List<float>();
            void Tree(Vector2 p, float type, float scale) { extra.Add(p.x); extra.Add(p.y); extra.Add(type); extra.Add(scale); }
            for (int i = 0; i + 1 < f.walk.Count; i++)
            {
                Vector2 d = (f.walk[i + 1] - f.walk[i]).normalized, r = new Vector2(d.y, -d.x);
                float len = Vector2.Distance(f.walk[i], f.walk[i + 1]);
                for (float t = 0.3f; t < len; t += 2.6f)
                    foreach (float sx in new[] { 1f, -1f })
                    {
                        if (rng.NextDouble() < 0.25) continue;
                        Vector2 p = f.walk[i] + d * t + r * sx * (3.3f + (float)rng.NextDouble() * 3.5f);
                        double c = rng.NextDouble();
                        Tree(p, c < 0.55 ? 3f : c < 0.8 ? 2f : 0f, 0.85f + (float)rng.NextDouble() * 0.4f);
                    }
            }
            float view = Mathf.Atan2(endDir.x, endDir.y);
            for (int k = 0; k < 14; k++)
            {
                float a = view + Mathf.PI * 2f * k / 14f;
                if (Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, view * Mathf.Rad2Deg)) < 28f) continue;   // the lake stays in view
                Vector2 p = f.clear + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * (KlClearR + 1.2f + (float)rng.NextDouble() * 3f);
                Tree(p, rng.NextDouble() < 0.7 ? 3f : 2f, 0.95f + (float)rng.NextDouble() * 0.35f);
            }
            var trees = new float[ctx.world.trees.Length + extra.Count];
            ctx.world.trees.CopyTo(trees, 0);
            extra.CopyTo(trees, ctx.world.trees.Length);
            ctx.world.trees = trees;

            // 6. the Mansa Devi Mandir across the road
            float templeYaw = FaceRoad(templeC);
            MansaDeviMandir(ctx, sm, templeC, templeY, templeYaw, rng);

            sm.Emit(ctx, "Khurpatal Lake View Point", parent);
            var site = new Site { origin = V3(f.clear - endDir * 2.5f + endR * 3f, yEnd), rot = Quaternion.Euler(0f, view * Mathf.Rad2Deg + 180f, 0f), depth = 1f };
            Sign(ctx, "Khurpatal Lake View Point", site, Vector3.zero, parent);
            var tq0 = Quaternion.Euler(0f, templeYaw, 0f);
            var tSite = new Site { origin = V3(templeC, templeY) + tq0 * new Vector3(3.3f, 0f, 1.4f), rot = tq0, depth = 1f };
            Sign(ctx, "Mansa Devi Mandir", tSite, Vector3.zero, parent);
            ctx.Zone("Khurpatal Lake View Point", V3(f.clear, yEnd), 40f);
            ctx.Zone("Mansa Devi Mandir", V3(templeC, templeY), 14f);
            Vector3 stand = V3(f.clear - endDir * 1.5f, yEnd + 0.1f);
            ctx.landmarkSites["Khurpatal Lake View Point"] = stand;
            ctx.landmarkSites["Mansa Devi Mandir"] = V3(templeC, templeY) + tq0 * new Vector3(0f, 0.1f, 3.2f);
            var cam = new GameObject("ViewShot Khurpatal Lake View Point");
            cam.transform.SetParent(parent, false);
            cam.transform.SetPositionAndRotation(V3(f.clear + endDir * 3.5f, yEnd + 1.7f), Quaternion.Euler(14f, view * Mathf.Rad2Deg, 0f));   // at the lip, looking down the valley
            Debug.Log($"[Nainital] Khurpatal Lake View Point: pull-off {y0:F1} at ({f.walk[0].x:F1},{f.walk[0].y:F1}), clearing {yEnd:F1} at ({f.clear.x:F1},{f.clear.y:F1}), " +
                      $"walk {total:F0} m, {extra.Count / 4} trees; Mansa Devi at ({templeC.x:F1},{templeY:F1},{templeC.y:F1}) facing {templeYaw:F0}");
        }

        /// <summary>
        /// The roadside Mansa Devi temple (the owner's photo): a small white shrine with a white pyramid shikhara painted
        /// with a sun, a white parapet lettered in red, a red collapsible gate and an arched red window grille, marigold
        /// garlands hanging all along the front, a pink-and-red skirting, a red flag and a lamp. Faces the road (+z).
        /// </summary>
        static void MansaDeviMandir(WorldCtx ctx, SiteMeshes sm, Vector2 c, float floor, float yaw, System.Random rng)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 o = V3(c, floor);
            var plaster = sm.Photo("white_rough_plaster", new Color(1f, 1f, 0.98f));
            const float W = 4.6f, D = 3.2f, H = 2.7f;
            // plinth down to the ground, a step at the front
            float gMin = float.MaxValue;
            foreach (float sx in new[] { -0.5f, 0.5f })
                foreach (float sz in new[] { -0.5f, 0.5f }) gMin = Mathf.Min(gMin, ctx.H(XZ(o + q * new Vector3(W * sx, 0f, D * sz))));
            Slab(sm.Photo("concrete_floor_worn_001", new Color(0.9f, 0.9f, 0.88f)), plaster, XZ(o + q * new Vector3(0f, 0f, -D * 0.5f - 0.1f)), XZ(o + q * new Vector3(0f, 0f, D * 0.5f + 0.5f)), W + 0.3f, floor, floor, Mathf.Min(gMin, floor) - 0.5f);
            TBox(plaster, o + q * new Vector3(0f, H * 0.5f, -0.1f), new Vector3(W, H, D - 0.2f), q, 1.2f);
            // skirting: pink and red bands round the foot
            sm.atlas.Box(o + q * new Vector3(0f, 0.12f, D * 0.5f - 0.18f), new Vector3(W + 0.04f, 0.24f, 0.04f), q, S(Pal.SareePink));
            for (float x = -W * 0.5f + 0.15f; x < W * 0.5f; x += 0.3f)
                sm.atlas.Box(o + q * new Vector3(x, 0.12f, D * 0.5f - 0.15f), new Vector3(0.12f, 0.24f, 0.02f), q, S(Pal.TempleRed));
            // the parapet over the front, white, lettered in red
            sm.atlas.Box(o + q * new Vector3(0f, H + 0.25f, D * 0.5f - 0.25f), new Vector3(W + 0.2f, 0.5f, 0.25f), q, S(Pal.TempleWhite));
            sm.atlas.Box(o + q * new Vector3(0f, H + 0.02f, D * 0.5f - 0.12f), new Vector3(W + 0.25f, 0.06f, 0.3f), q, S(Pal.TempleRed));
            Vector3 fwd = q * Vector3.forward;
            Glyph3D.Write(sm.loose, "JAI MATA DI", Glyph3D.Face.Desi, o + q * new Vector3(0.6f, H + 0.25f, D * 0.5f - 0.1f), fwd, 0.22f, 0.02f, 2.6f, S(Pal.TempleRed), S(Pal.StoneGrey));
            Glyph3D.Write(sm.loose, "MANSA DEVI", Glyph3D.Face.Desi, o + q * new Vector3(-1.55f, H + 0.25f, D * 0.5f - 0.1f), fwd, 0.18f, 0.02f, 1.3f, S(Pal.TempleRed), S(Pal.StoneGrey));
            // the shikhara: a white pyramid, a painted sun, a finial
            sm.atlas.Pyramid(o + q * new Vector3(-0.3f, H, -0.3f), 2.3f, 2.3f, 2.1f, S(Pal.TempleWhite));
            sm.loose.Cylinder(o + q * new Vector3(-0.3f, H + 0.95f, 0.76f), 0.28f, 0.28f, 0.02f, 12, S(Pal.SareeOrange), true);
            sm.atlas.Sphere(o + q * new Vector3(-0.3f, H + 2.15f, -0.3f), Vector3.one * 0.1f, 6, 3, S(Pal.Gold));
            // the red collapsible gate (bars over a dark inside) and the arched window grille on the left
            Vector3 gate = o + q * new Vector3(0.45f, 1.1f, D * 0.5f - 0.07f);
            sm.atlas.Box(gate, new Vector3(1.6f, 2.2f, 0.03f), q, S(Pal.IronBlack));
            for (float x = -0.75f; x <= 0.76f; x += 0.1f) sm.atlas.Box(gate + q * new Vector3(x, 0f, 0.04f), new Vector3(0.03f, 2.2f, 0.03f), q, S(Pal.TempleRed));
            foreach (float y in new[] { -0.9f, 0f, 0.9f }) sm.atlas.Box(gate + q * new Vector3(0f, y, 0.06f), new Vector3(1.6f, 0.05f, 0.03f), q, S(Pal.TempleRed));
            Vector3 win = o + q * new Vector3(-1.55f, 1.25f, D * 0.5f - 0.07f);
            sm.atlas.Box(win, new Vector3(1.0f, 1.1f, 0.03f), q, S(Pal.IronBlack));
            for (float x = -0.45f; x <= 0.46f; x += 0.15f) sm.atlas.Box(win + q * new Vector3(x, 0f, 0.04f), new Vector3(0.03f, 1.1f, 0.03f), q, S(Pal.TempleRed));
            for (int k = 0; k <= 8; k++)
            {
                float a = k / 8f * Mathf.PI;
                sm.atlas.Box(win + q * new Vector3(Mathf.Cos(a) * 0.5f, 0.55f + Mathf.Sin(a) * 0.3f, 0.04f), new Vector3(0.12f, 0.04f, 0.03f), q * Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f), S(Pal.TempleRed));
            }
            // marigold garlands: swags under the parapet and strands hanging down the front
            int[] mari = { Pal.SareeOrange, Pal.BoatYellow, Pal.JacketRed, Pal.SareeOrange, Pal.ShirtWhite };
            for (int sg = 0; sg < 6; sg++)
            {
                float x0 = -W * 0.5f + sg * (W / 6f), x1 = x0 + W / 6f;
                for (int k = 0; k <= 6; k++)
                {
                    float t = k / 6f;
                    Vector3 p = o + q * new Vector3(Mathf.Lerp(x0, x1, t), H - 0.08f - 0.22f * 4f * t * (1f - t), D * 0.5f + 0.02f);
                    sm.loose.Sphere(p, Vector3.one * 0.055f, 5, 3, S(mari[(k + sg) % mari.Length]));
                }
            }
            for (int st = 0; st < 9; st++)
            {
                float x = -W * 0.5f + 0.2f + st * ((W - 0.4f) / 8f);
                if (Mathf.Abs(x - 0.45f) < 0.6f && st % 2 == 1) continue;   // keep the gate's middle clear
                int beads = 9 + rng.Next(4);
                for (int k = 0; k < beads; k++)
                    sm.loose.Sphere(o + q * new Vector3(x, H - 0.12f - k * 0.13f, D * 0.5f + 0.03f), Vector3.one * 0.05f, 5, 3, S(mari[(k + st) % mari.Length]));
            }
            // the red flag on its pole at the right corner, a lamp on the left
            Flag(sm.atlas, o + q * new Vector3(W * 0.5f - 0.1f, H, -0.5f), 2.4f, Pal.TempleRed);
            Vector3 lamp = o + q * new Vector3(W * 0.5f + 0.5f, 0f, D * 0.5f + 0.3f);
            sm.atlas.Cylinder(lamp, 0.05f, 0.04f, 3.2f, 6, S(Pal.StoneGrey), true);
            sm.loose.Sphere(lamp + Vector3.up * 3.3f, Vector3.one * 0.17f, 8, 5, S(Pal.ShirtWhite));
        }

        /// <summary>A smooth lumpy blob in one atlas colour (tree crowns, bushes): smooth normals, unlike Sphere().</summary>
        static void Blob(MeshBuilder mb, Vector3 c, Vector3 radii, int seed, Vector2 uv, float lumpy = 0.3f)
        {
            const int seg = 10, rings = 7;
            var P = new Vector3[rings + 1, seg + 1];
            var want = new Vector3[rings + 1, seg + 1];
            var sAlong = new float[rings + 1];
            for (int i = 0; i <= rings; i++)
            {
                float lat = Mathf.Lerp(-1f, 1f, i / (float)rings) * Mathf.PI * 0.5f;
                for (int j = 0; j <= seg; j++)
                {
                    float lon = j / (float)seg * Mathf.PI * 2f;
                    Vector3 d = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
                    float n = 1f + lumpy * (Mathf.PerlinNoise(seed * 0.31f + d.x * 2.1f + 7f, d.z * 2.1f + d.y * 1.7f + 3f) - 0.5f);
                    P[i, j] = c + Vector3.Scale(d * n, radii);
                    want[i, j] = d;
                }
            }
            int v0 = mb.V.Count;
            Grid(mb, P, want, sAlong, 1f);
            for (int k = v0; k < mb.UV.Count; k++) mb.UV[k] = uv;
        }

        /// <summary>
        /// The many-trunked Himalayan cypress of the Khurpatal photos: five trunks swing out from one root in a U, then go
        /// straight up, each carrying a narrow crown of soft lumps in two greens. Trunks collide; crowns do not.
        /// </summary>
        static void CandelabraCypress(MeshBuilder trunkMb, MeshBuilder leafMb, Vector3 b, float yaw, System.Random rng)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            Vector2 bark = S(Pal.Trunk);
            Vector2[] greens = { S(Pal.Cypress), S(Pal.Pine), S(Pal.DeodarLight), S(Pal.Oak) };
            trunkMb.Sphere(b + Vector3.up * 0.12f, new Vector3(0.55f, 0.3f, 0.45f), 8, 4, bark);
            const int n = 5;
            for (int k = 0; k < n; k++)
            {
                float a = (k - (n - 1) * 0.5f) * 0.5f;
                Vector3 outDir = q * new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a) * 0.3f).normalized;
                float spread = 0.35f + Mathf.Abs(k - 2) * 0.45f;
                float h = 8.5f + (float)rng.NextDouble() * 3.5f;
                Vector3 p0 = b + Vector3.up * 0.25f, p1 = b + outDir * (spread * 0.75f) + Vector3.up * 0.15f, p2 = b + outDir * spread + Vector3.up * 1.0f;
                Vector3 p3 = b + outDir * (spread * 1.12f) + Vector3.up * (h * 0.55f), p4 = b + outDir * (spread * 1.2f) + Vector3.up * h;
                trunkMb.Limb(p0, p1, 0.19f, 0.17f, 7, bark);
                trunkMb.Limb(p1, p2, 0.17f, 0.15f, 7, bark);
                trunkMb.Limb(p2, p3, 0.15f, 0.1f, 7, bark);
                trunkMb.Limb(p3, p4, 0.1f, 0.04f, 6, bark);
                for (int j = 0; j < 9; j++)
                {
                    float t = j / 8f;
                    Vector3 c = Vector3.Lerp(p3 - Vector3.up * (h * 0.14f), p4 - Vector3.up * 0.2f, t);
                    c += new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * 0.45f + outDir * 0.1f;
                    float r = Mathf.Lerp(0.95f, 0.35f, t) * (0.8f + (float)rng.NextDouble() * 0.35f);
                    Blob(leafMb, c, new Vector3(r, r * 1.25f, r), rng.Next(1000), greens[rng.Next(greens.Length)], 0.45f);
                }
            }
        }
    }
}
