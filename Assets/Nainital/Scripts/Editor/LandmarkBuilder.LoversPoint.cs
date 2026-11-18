using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Lover's Point and the walk down to Suicide Point (user, 2026-11-19, Street View photos + a sketch on the
    /// satellite: "orange means stairs to down, green is plain part, baaki buildings satellite view mein"). On the outer
    /// side of the Kaladhungi road hairpin: a wide verge with the Ghora Chaalak Seva Samiti hut ("WELCOMES YOU LOVERS
    /// POINT"), a green shade net along the drop, benches on a low wall and the blue horse-ride boards; across the road a
    /// stone-walled terrace with tarp shelters for the horses. From the verge's south corner a walk runs west along the
    /// tin-roofed tea sheds - level, steps down, level, steps, a landing - and a last flight down to the Suicide Point
    /// ledge over the valley. Positions: the satellite's buildings shifted onto the game road (the mapped road runs
    /// ~12 m east of the one in the imagery). The DEM there is a 33 degree slope, so the ground under the verge, the walk
    /// and the sheds is shaped into the shelf the real place stands on (ShapeLoversPointGround, with the landmark sites).
    /// </summary>
    public static partial class LandmarkBuilder
    {
        static readonly Vector2 LpApex = new Vector2(-1411f, -377f);
        // the walk: x, z, drop below the verge (green = level, orange = steps down)
        static readonly float[] LpWalk =
        {
            -1418.5f, -386.5f, 0f,
            -1421.5f, -388.3f, 0f,
            -1428.5f, -390.0f, -3.4f,
            -1436.0f, -389.6f, -3.7f,
            -1439.5f, -389.8f, -5.0f,
            -1447.0f, -389.6f, -5.3f,
            -1452.0f, -390.2f, -5.4f,
            -1460.0f, -396.2f, -11.0f,
            -1464.5f, -399.2f, -11.2f,
        };

        sealed class LpFrame
        {
            public Vector2[] road; public float hw, sApex, west, north;
            public readonly List<Vector2> walk = new List<Vector2>();
            public readonly List<float> drop = new List<float>();

            /// <summary>The point `d` metres from the road's centre line (+ = the valley side, west) at distance s along it.</summary>
            public Vector2 At(float s, float d)
            {
                var p = PointAt(road, s, out var dir);
                return p + new Vector2(dir.y, -dir.x) * (d * west);
            }
            public float S(float rel) => sApex + rel * north;
            public float RoadY(WorldCtx ctx, float s) => (ctx.H(At(s - 3f, 0f)) + ctx.H(At(s, 0f)) * 2f + ctx.H(At(s + 3f, 0f))) * 0.25f;
            /// <summary>How far the verge reaches past the road edge, by distance from the hairpin (+ = north).</summary>
            public static float Verge(float rel)
            {
                if (rel < -15f || rel > 26f) return 0f;
                if (rel < -10f) return Mathf.Lerp(1f, 7f, (rel + 15f) / 5f);
                if (rel < 5f) return 7f;
                if (rel < 9f) return Mathf.Lerp(7f, 3.6f, (rel - 5f) / 4f);
                if (rel < 22f) return 3.6f;
                return Mathf.Lerp(3.6f, 0.8f, (rel - 22f) / 4f);
            }
            public float WalkLevel(float y0, float sw)
            {
                float run = 0f;
                for (int i = 0; i + 1 < walk.Count; i++)
                {
                    float len = Vector2.Distance(walk[i], walk[i + 1]);
                    if (run + len >= sw || i + 2 == walk.Count) return y0 + Mathf.Lerp(drop[i], drop[i + 1], Mathf.Clamp01((sw - run) / Mathf.Max(len, 1e-3f)));
                    run += len;
                }
                return y0;
            }
            public float WalkLen { get { float l = 0f; for (int i = 0; i + 1 < walk.Count; i++) l += Vector2.Distance(walk[i], walk[i + 1]); return l; } }
        }

        static Vector2 PointAt(IList<Vector2> line, float s, out Vector2 dir)
        {
            float run = 0f;
            dir = Vector2.up;
            for (int i = 0; i + 1 < line.Count; i++)
            {
                Vector2 ab = line[i + 1] - line[i];
                float len = ab.magnitude;
                if (len < 1e-4f) continue;
                dir = ab / len;
                if (s <= run + len || i + 2 == line.Count) return line[i] + dir * Mathf.Clamp(s - run, 0f, len);
                run += len;
            }
            return line.Count > 0 ? line[line.Count - 1] : Vector2.zero;
        }

        static LpFrame LpMake(WorldCtx ctx)
        {
            var f = new LpFrame();
            f.road = RoadLine(ctx, "Kaldhungi-Nainital Road", LpApex, out f.hw);
            if (f.road == null) { Debug.LogWarning("[Nainital] Lover's Point: Kaladhungi road not found"); return null; }
            Project(f.road, LpApex, out f.sApex, out _, out _, out _);
            Project(f.road, LpApex + new Vector2(-25f, 0f), out _, out float side, out _, out _);
            f.west = side >= 0f ? 1f : -1f;
            f.north = Mathf.Sign(PointAt(f.road, f.sApex + 8f, out _).y - PointAt(f.road, f.sApex - 8f, out _).y);
            if (f.north == 0f) f.north = 1f;
            for (int i = 0; i + 2 < LpWalk.Length; i += 3) { f.walk.Add(new Vector2(LpWalk[i], LpWalk[i + 1])); f.drop.Add(LpWalk[i + 2]); }
            return f;
        }

        /// <summary>The shelf the place stands on: (target height, weight) for a ground point, from the verge, the walk with
        /// the sheds south of it, and the horse terrace across the road. kind: 0 none, 1 verge, 2 walk shelf, 3 terrace.</summary>
        static (float T, float w, int kind) LpGround(WorldCtx ctx, LpFrame f, float y0, Vector2 p, float y)
        {
            float bestW = 0f, bestT = y; int kind = 0;
            void Offer(float T, float w, int k)
            {
                if (w > bestW + 1e-3f || (Mathf.Abs(w - bestW) <= 1e-3f && T > bestT && w > 0f)) { bestW = w; bestT = T; kind = k; }
            }
            Project(f.road, p, out float s, out float side, out _, out _);
            float d = side * f.west, rel = (s - f.sApex) * f.north;
            if (Mathf.Abs(d) < f.hw + 0.3f) return (y, 0f, 0);   // the carriageway is the road's own (LevelRoads)
            float roadY = f.RoadY(ctx, s);
            // the blend widens with the height it makes up (a 25 m fill over 9 m stood as a wall): about 50 degrees at most
            float Reach(float T, float min) => Mathf.Max(min, Mathf.Abs(T - y) * 0.85f);
            if (d > 0f)
            {
                float out1 = Mathf.Max(0f, d - (f.hw + Mathf.Max(LpFrame.Verge(rel), 0.5f)));
                float outRel = Mathf.Max(0f, Mathf.Max(-15f - rel, rel - 26f));
                float b = Reach(roadY, 8f);
                Offer(roadY - 0.02f, 1f - Mathf.SmoothStep(0f, 1f, Mathf.Max(out1 / b, outRel / Mathf.Max(6f, b * 0.75f))), 1);
            }
            else
            {
                float ad = -d, top = roadY + 0.9f;
                float endW = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Max(0f, Mathf.Max(4f - rel, rel - 26f)) / 3f);
                if (ad < f.hw + 2.2f) Offer(roadY - 0.02f, endW, 3);
                else if (ad < f.hw + 8.0f) Offer(top - 0.05f, endW, 3);
                else
                {
                    float back = Mathf.Max(y, top + 2.7f);
                    Offer(back, endW * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Max(0f, ad - (f.hw + 11f)) / 3f)), 3);
                }
            }
            Project(f.walk, p, out float sw, out float sideW, out int seg, out float dw);
            float total = f.WalkLen;
            float outEnd = sw >= total - 0.01f ? Mathf.Max(0f, Vector2.Distance(p, f.walk[f.walk.Count - 1]) - 4.5f) : 0f;
            float outStart = sw <= 0.01f ? Mathf.Max(0f, Vector2.Distance(p, f.walk[0]) - 2f) : 0f;
            float outS = Mathf.Max(Mathf.Max(0f, sideW - 3.2f), Mathf.Max(-24f - sideW, Mathf.Max(outEnd, outStart)));
            float Ts = f.WalkLevel(y0, sw) - 0.3f - 0.14f * Mathf.Max(0f, -sideW - 1.2f);
            if (sw >= total - 0.01f) Ts = f.WalkLevel(y0, total) - 0.3f;
            Offer(Ts, 1f - Mathf.SmoothStep(0f, 1f, outS / Reach(Ts, 9f)), 2);
            return (bestT, bestW, kind);
        }

        static void LpBounds(out Vector2 min, out Vector2 max) { min = new Vector2(-1530f, -460f); max = new Vector2(-1370f, -320f); }

        /// <summary>Shapes the shelf from the levelled road (called first thing in BuildLoversPoint: the roads are level by
        /// then, the trees, walls and lamps not yet placed). The data houses here are kept off by LoversPointSpots.</summary>
        static void ShapeLoversPointGround(WorldCtx ctx, LpFrame f, float y0)
        {
            LpBounds(out var min, out var max);
            ShapeTerrain(ctx, min, max, (p, y) =>
            {
                var g = LpGround(ctx, f, y0, p, y);
                return g.w > 0f ? Mathf.Lerp(y, g.T, g.w) : y;
            });
            PaintTerrain(ctx, min, max, (p, slope) =>
            {
                var g = LpGround(ctx, f, y0, p, ctx.H(p));
                if (g.w > 0.6f && slope < 30f) return new Vector4(0.05f, 0.1f, 0.25f, 0.6f);   // trodden earth and gravel
                if (g.w > 0.02f && slope > 40f) return new Vector4(0.02f, 0.12f, 0.86f, 0f);   // the cut and filled faces: rock
                return null;
            });
        }

        /// <summary>Circles for the planners: no house or tree on the verge, the terrace, the walk or the sheds.</summary>
        internal static IEnumerable<(Vector2 c, float r)> LoversPointSpots(WorldCtx ctx)
        {
            var f = LpMake(ctx);
            if (f == null) yield break;
            for (float rel = -14f; rel <= 25f; rel += 2f)
            {
                float v = LpFrame.Verge(rel);
                yield return (f.At(f.S(rel), f.hw + 0.6f), 1.6f);                       // no parapet on the verge's road edge
                if (v > 2f) yield return (f.At(f.S(rel), f.hw + v * 0.6f), v * 0.5f);
                if (rel >= 4f) yield return (f.At(f.S(rel), -(f.hw + 5f)), 3.2f);
            }
            float total = f.WalkLen;
            for (float sw = 0f; sw <= total; sw += 2.5f)
            {
                var p = PointAt(f.walk, sw, out var dir);
                yield return (p, 2.6f);
                yield return (p + new Vector2(dir.y, -dir.x) * -8f, 6f);    // the sheds, south of the walk
                yield return (p + new Vector2(dir.y, -dir.x) * -17f, 6f);
            }
            yield return (f.walk[f.walk.Count - 1], 5f);
            // the building data's houses here (one stands across the walk); the sheds below take their places
            yield return (new Vector2(-1456.2f, -385.5f), 3f);
            yield return (new Vector2(-1460.3f, -408.4f), 3f);
            yield return (new Vector2(-1466.2f, -397.1f), 3f);
        }

        /// <summary>A paved band between two edges (same number of points) with stone faces down to the ground on the
        /// sides asked for and at both ends.</summary>
        static void Band(WorldCtx ctx, MeshBuilder top, MeshBuilder side, IList<Vector3> a, IList<Vector3> b, bool faceA, bool faceB, float tile = 1.5f)
        {
            int n = a.Count;
            if (n < 2) return;
            Vector3 Bot(Vector3 p) => new Vector3(p.x, Mathf.Min(ctx.H(XZ(p)), p.y) - 0.45f, p.z);
            for (int i = 0; i + 1 < n; i++)
            {
                top.Tri(a[i], a[i + 1], b[i + 1], WUV(a[i], tile), WUV(a[i + 1], tile), WUV(b[i + 1], tile), Vector3.up);
                top.Tri(a[i], b[i + 1], b[i], WUV(a[i], tile), WUV(b[i + 1], tile), WUV(b[i], tile), Vector3.up);
                if (faceA) { Vector3 o = a[i] - b[i]; o.y = 0f; Face(side, a[i], a[i + 1], Bot(a[i + 1]), Bot(a[i]), o, tile); }
                if (faceB) { Vector3 o = b[i] - a[i]; o.y = 0f; Face(side, b[i], b[i + 1], Bot(b[i + 1]), Bot(b[i]), o, tile); }
            }
            Vector3 e0 = a[0] - a[1]; e0.y = 0f;
            Vector3 e1 = a[n - 1] - a[n - 2]; e1.y = 0f;
            Face(side, a[0], b[0], Bot(b[0]), Bot(a[0]), e0, tile);
            Face(side, a[n - 1], b[n - 1], Bot(b[n - 1]), Bot(a[n - 1]), e1, tile);
        }

        static void BuildLoversPoint(WorldCtx ctx, Transform parent)
        {
            var f = LpMake(ctx);
            if (f == null) return;
            var rng = new System.Random(1404);
            var sm = new SiteMeshes();
            var gravel = sm.Photo("concrete_floor_worn_001", new Color(0.98f, 0.97f, 0.94f));   // grey worn tarmac and gravel, as the road
            var stone = sm.Photo("old_stone_wall", new Color(0.82f, 0.8f, 0.76f));
            var flags = sm.Photo("cobblestone_floor_08", new Color(0.78f, 0.77f, 0.74f), 0.25f);
            var plaster = new Color(0.95f, 0.95f, 0.93f);
            var tinRoof = new Color(0.78f, 0.84f, 0.9f);
            Project(f.road, f.walk[0], out float s0, out _, out _, out _);
            float y0 = f.RoadY(ctx, s0) + 0.03f;
            ShapeLoversPointGround(ctx, f, y0);
            float Rel(Vector2 p) { Project(f.road, p, out float s, out _, out _, out _); return (s - f.sApex) * f.north; }
            float FaceRoad(Vector2 p) { Project(f.road, p, out float s, out _, out _, out _); Vector2 d = PointAt(f.road, s, out _) - p; return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg; }

            // the ground once more, now the road has its final level: nothing pokes through the verge, terrace or walk
            float total = f.WalkLen;
            LpBounds(out var bmin, out var bmax);
            ShapeTerrain(ctx, bmin, bmax, (p, y) =>
            {
                Project(f.road, p, out float s, out float side, out _, out _);
                float d = side * f.west, rel = (s - f.sApex) * f.north, ry = f.RoadY(ctx, s);
                if (d > f.hw + 0.1f && d < f.hw + LpFrame.Verge(rel) + 0.6f) y = Mathf.Min(y, ry - 0.04f);
                if (-d > f.hw + 2.0f && -d < f.hw + 8.2f && rel > 3.5f && rel < 26.5f) y = Mathf.Min(y, ry + 0.84f);
                Project(f.walk, p, out float sw, out _, out _, out float dw);
                if (dw < 1.9f) y = Mathf.Min(y, f.WalkLevel(y0, sw) - (sw > 0.5f ? 0.12f : 0.05f));
                return y;
            });

            // 1. the verge: worn tarmac and gravel from the road edge to the drop
            var inner = new List<Vector3>(); var outer = new List<Vector3>();
            for (float rel = -15f; rel <= 26.01f; rel += 1f)
            {
                float s = f.S(rel), v = Mathf.Max(LpFrame.Verge(rel), 0.6f), yy = f.RoadY(ctx, s) + 0.03f;
                inner.Add(V3(f.At(s, f.hw + 0.02f), yy));
                outer.Add(V3(f.At(s, f.hw + v), yy));
            }
            Band(ctx, gravel, stone, inner, outer, false, true);
            Vector3 VergeP(float rel, float d) { float s = f.S(rel); return V3(f.At(s, d), f.RoadY(ctx, s) + 0.03f); }

            // the path opening in the verge's edge
            float relWalk = Rel(f.walk[0]);
            // 2. the Ghora Chaalak Seva Samiti hut, its board, bollards, sacks of feed, the flag
            Vector2 hutC = f.At(f.S(1f), f.hw + 8.7f);
            float hutYaw = FaceRoad(hutC);
            var hq = Quaternion.Euler(0f, hutYaw, 0f);
            float hy = Hut(ctx, sm, hutC, hutYaw, 3.4f, 3.0f, 2.5f, "white_rough_plaster", plaster, new Color(0.62f, 0.42f, 0.3f), Pal.BoatBlue, true, 0.8f, VergeP(1f, f.hw + 8.7f).y);
            Vector3 hut0 = V3(hutC, hy);
            sm.atlas.Box(hut0 + hq * new Vector3(1.72f, 1.1f, 0f), new Vector3(0.04f, 2.2f, 2.9f), hq, S(Pal.BoatBlue));       // the blue dado on the side
            sm.loose.Box(hut0 + hq * new Vector3(-0.6f, 1.7f, 1.53f), new Vector3(0.7f, 0.5f, 0.02f), hq, S(Pal.BoatGreen));  // notices on the front
            sm.loose.Box(hut0 + hq * new Vector3(-1.2f, 1.2f, 1.53f), new Vector3(0.35f, 0.45f, 0.02f), hq, S(Pal.ShirtWhite));
            Flag(sm.atlas, hut0 + hq * new Vector3(-1.5f, 2.45f, -1.3f), 2.4f, Pal.JacketBlue);
            Vector3 board = VergeP(-2.6f, f.hw + 4.6f);
            Board(sm, board + Vector3.up * 2.25f, hutYaw, 2.8f, 1.5f, Pal.JacketBlue,
                  new[] { "GHORA CHAALAK SEVA SAMITI", "WELCOMES YOU", "LOVERS POINT", "HORSE STAND NAINITAL" }, new[] { 0.15f, 0.34f, 0.24f, 0.09f },
                  Pal.ShirtWhite, Pal.BoatBlue, board.y, Glyph3D.Face.Block, new[] { Pal.ShirtWhite, Pal.BoatYellow, Pal.ShirtWhite, Pal.ShirtWhite });
            var bollards = new List<Vector3>();
            for (float rel = -1.8f; rel <= 3.21f; rel += 1.25f) bollards.Add(VergeP(rel, f.hw + 6.3f));
            PipeRail(sm, bollards, Pal.BoatBlue, 0.75f, 0.035f);
            int[] sackCol = { Pal.BoatYellow, Pal.ShirtWhite, Pal.PantsKhaki, Pal.BoatYellow };
            for (int k = 0; k < 7; k++)
            {
                Vector3 sp = VergeP(-0.4f + (k % 4) * 0.55f, f.hw + 6.75f + (k / 4) * 0.3f) + Vector3.up * (0.15f + (k / 4) * 0.28f);
                sm.atlas.Box(sp, new Vector3(0.5f, 0.3f, 0.75f), hq * Quaternion.Euler(0f, (float)rng.NextDouble() * 30f - 15f, 0f), S(sackCol[k % sackCol.Length]));
            }

            // 3. the green shade net along the drop, from the hut to the walk's opening
            var netPts = new List<Vector3>();
            for (float rel = -1.2f; rel >= relWalk + 1.6f; rel -= 1.9f) netPts.Add(VergeP(rel, f.hw + LpFrame.Verge(rel) - 0.15f));
            for (int i = 0; i < netPts.Count; i++)
            {
                sm.atlas.Limb(netPts[i] + Vector3.down * 0.3f, netPts[i] + Vector3.up * 2.2f, 0.045f, 0.04f, 5, S(Pal.WoodLight));
                if (i == 0) continue;
                Vector3 a = netPts[i - 1], b = netPts[i], sideN = Vector3.Cross(Vector3.up, b - a).normalized;
                sm.loose.QuadDouble(a + Vector3.up * 0.1f, b + Vector3.up * 0.1f, b + Vector3.up * 2.05f, a + Vector3.up * 2.05f, new Rect(S(Pal.BoatGreen), Vector2.zero), sideN);
                sm.loose.Limb(a + Vector3.up * 1.0f, b + Vector3.up * 1.0f, 0.03f, 0.03f, 4, S(Pal.WoodLight), false);
                sm.col.Quad(a, b, b + Vector3.up * 2.1f, a + Vector3.up * 2.1f, Vector2.zero, sideN);
                sm.col.Quad(b, a, a + Vector3.up * 2.1f, b + Vector3.up * 2.1f, Vector2.zero, -sideN);
            }
            // blue pipe railing along the rest of the drop: south of the opening, and the north strip
            var railS = new List<Vector3>();
            for (float rel = relWalk - 1.6f; rel >= -14f; rel -= 2f) railS.Add(VergeP(rel, f.hw + LpFrame.Verge(rel) - 0.15f));
            PipeRail(sm, railS, Pal.BoatBlue);
            var railN = new List<Vector3>();
            for (float rel = 3.5f; rel <= 24.01f; rel += 2f) railN.Add(VergeP(rel, f.hw + LpFrame.Verge(rel) - 0.15f));
            PipeRail(sm, railN, Pal.BoatBlue);

            // 4. benches on a low stone wall along the north strip, and the blue horse-ride boards
            for (float rel = 5.5f; rel < 21f; rel += 3.2f)
            {
                Vector3 a = VergeP(rel, f.hw + 2.9f), b = VergeP(rel + 2.6f, f.hw + LpFrame.Verge(rel + 2.6f) - 0.7f);
                Slab(sm.Photo("concrete_floor_worn_001", new Color(0.85f, 0.85f, 0.82f)), stone, XZ(a), XZ(b), 0.5f, a.y + 0.45f, a.y + 0.45f, a.y - 0.3f);
            }
            foreach (float rel in new[] { 7.2f, 13.6f, 19.2f })
            {
                Vector3 bp = VergeP(rel, f.hw + 1.9f);
                bool green = rel < 10f || rel > 18f;
                Bench(sm.atlas, bp, FaceRoad(XZ(bp)), green ? Pal.BoatGreen : Pal.JacketRed, green ? Pal.BoatYellow : Pal.Concrete, 1.8f);
            }
            Vector3 hb = VergeP(10.4f, f.hw + 3.3f);
            Board(sm, hb + Vector3.up * 2.0f, FaceRoad(XZ(hb)), 3.4f, 1.3f, Pal.JacketBlue,
                  new[] { "FOREST AREA HORSE RIDE", "6 POINT PACKAGE TOUR  RS 760", "NO PARKING" }, new[] { 0.2f, 0.17f, 0.13f },
                  Pal.ShirtWhite, Pal.BoatBlue, hb.y, Glyph3D.Face.Block, new[] { Pal.ShirtWhite, Pal.ShirtWhite, Pal.BoatYellow });
            Vector3 hb2 = VergeP(16.8f, f.hw + 3.3f);
            Board(sm, hb2 + Vector3.up * 2.0f, FaceRoad(XZ(hb2)), 3.4f, 1.35f, Pal.JacketBlue,
                  new[] { "GHORA CHAALAK SEVA SAMITI", "HORSE RIDE RS 760", "1 VIEW POINT  2 TIGER TOP  3 LANDS END", "4 FOREST POINT  5 TIFFIN TOP  6 DOROTHY SEAT" },
                  new[] { 0.16f, 0.16f, 0.1f, 0.1f }, Pal.ShirtWhite, Pal.BoatBlue, hb2.y);

            // 5. across the road: the horse terrace behind its stone wall, tarp shelters against the back wall
            float tRel0 = 4f, tRel1 = 26f;
            var tf = new List<Vector3>(); var tb = new List<Vector3>(); var wt = new List<Vector3>(); var wb = new List<Vector3>();
            for (float rel = tRel0; rel <= tRel1 + 0.01f; rel += 1f)
            {
                float s = f.S(rel), top = f.RoadY(ctx, s) + 0.9f;
                tf.Add(V3(f.At(s, -(f.hw + 2.2f)), top));
                tb.Add(V3(f.At(s, -(f.hw + 7.4f)), top));
                wt.Add(V3(f.At(s, -(f.hw + 7.4f)), top + 2.8f));
                wb.Add(V3(f.At(s, -(f.hw + 8.0f)), top + 2.8f));
            }
            Band(ctx, flags, stone, tf, tb, true, false);
            Band(ctx, stone, stone, wt, wb, true, false);
            // the steps up from the road at the south end
            float stS = f.S(5.6f);
            var steps = new List<WalkNode>
            {
                new WalkNode(f.At(stS, -(f.hw + 0.25f)), f.RoadY(ctx, stS) + 0.02f, true),
                new WalkNode(f.At(stS, -(f.hw + 2.25f)), f.RoadY(ctx, stS) + 0.9f, false),
            };
            Walkway(ctx, sm, steps, 1.6f, flags, stone);
            var railT = new List<Vector3>();
            for (float rel = 7f; rel <= tRel1 - 0.5f; rel += 2f) { float s = f.S(rel); railT.Add(V3(f.At(s, -(f.hw + 2.35f)), f.RoadY(ctx, s) + 0.9f)); }
            PipeRail(sm, railT, Pal.BoatBlue);
            int[] tarps = { Pal.StoneGrey, Pal.BoatBlue, Pal.Concrete, Pal.BoatBlue };
            for (int k = 0; k < 4; k++)
            {
                float r0 = 9f + k * 4.2f, r1 = r0 + 4.0f;
                float s0t = f.S(r0), s1t = f.S(r1);
                float top0 = f.RoadY(ctx, s0t) + 0.9f, top1 = f.RoadY(ctx, s1t) + 0.9f;
                Vector3 b0 = V3(f.At(s0t, -(f.hw + 7.35f)), top0 + 2.55f), b1 = V3(f.At(s1t, -(f.hw + 7.35f)), top1 + 2.55f);
                Vector3 p0 = V3(f.At(s0t, -(f.hw + 4.6f)), top0), p1 = V3(f.At(s1t, -(f.hw + 4.6f)), top1);
                foreach (var pp in new[] { p0, p1 }) sm.atlas.Limb(pp, pp + Vector3.up * 2.0f, 0.05f, 0.045f, 5, S(Pal.WoodDark));
                Vector3 f0 = Vector3.LerpUnclamped(b0, p0 + Vector3.up * 2.0f, 1.18f), f1 = Vector3.LerpUnclamped(b1, p1 + Vector3.up * 2.0f, 1.18f);
                sm.loose.QuadDouble(b0, b1, f1, f0, new Rect(S(tarps[k]), Vector2.zero), Vector3.up);
                sm.loose.Limb(p0 + Vector3.up * 1.1f, p1 + Vector3.up * 1.1f, 0.03f, 0.03f, 4, S(Pal.WoodLight), false);   // the tethering rail
                Vector3 drum = Vector3.Lerp(b0, b1, 0.3f); drum.y = top0;
                drum += (p0 - V3(XZ(b0), p0.y)) * 0.15f;
                sm.atlas.Cylinder(drum, 0.3f, 0.3f, 0.85f, 10, S(k % 2 == 0 ? Pal.BoatBlue : Pal.JacketBlue), true);
                Vector3 hay = Vector3.Lerp(b0, b1, 0.7f); hay.y = top0 + 0.25f;
                hay += (p0 - V3(XZ(b0), p0.y)) * 0.2f;
                sm.atlas.Box(hay, new Vector3(0.9f, 0.5f, 0.6f), Quaternion.LookRotation(b1 - b0), S(Pal.PantsKhaki));
            }
            foreach (float rel in new[] { 6.6f, 7.9f })
            {
                float s = f.S(rel);
                Vector3 bp = V3(f.At(s, -(f.hw + 3.3f)), f.RoadY(ctx, s) + 0.9f);
                Bench(sm.atlas, bp, FaceRoad(XZ(bp)), rel < 7f ? Pal.BoatGreen : Pal.JacketRed, Pal.Concrete, 1.4f);
            }
            {
                float s = f.S(3.8f);
                Vector3 gp = V3(f.At(s, -(f.hw + 1.0f)), f.RoadY(ctx, s));
                Board(sm, gp + Vector3.up * 1.75f, FaceRoad(XZ(gp)) + 180f, 0.9f, 1.1f, Pal.BoatGreen, new[] { "VIEW POINT", "TIFFIN TOP", "LANDS END", "NAINITAL" },
                      new[] { 0.11f, 0.11f, 0.11f, 0.08f }, Pal.ShirtWhite, Pal.IronGreen, gp.y);
                s = f.S(12.5f);
                Vector3 pp = V3(f.At(s, -(f.hw + 2.6f)), f.RoadY(ctx, s) + 0.9f);
                Board(sm, pp + Vector3.up * 1.6f, FaceRoad(XZ(pp)), 1.3f, 1.6f, Pal.ShirtWhite, new[] { "GREEN AREA", "HORSE RIDE", "6 POINT", "FULL PACKAGE" },
                      new[] { 0.13f, 0.15f, 0.15f, 0.12f }, Pal.JacketBlue, Pal.BoatBlue, pp.y, Glyph3D.Face.Block, new[] { Pal.JacketGreen, Pal.JacketBlue, Pal.JacketRed, Pal.JacketBlue });
                s = f.S(21f);
                Vector3 np = V3(f.At(s, -(f.hw + 1.1f)), f.RoadY(ctx, s));
                Board(sm, np + Vector3.up * 1.5f, FaceRoad(XZ(np)) + 25f, 1.4f, 0.55f, Pal.ShirtWhite, new[] { "NO PARKING" }, new[] { 0.2f }, Pal.JacketRed, Pal.IronBlack, np.y);
            }

            // 6. the walk to Suicide Point: level stretches and flights of steps, a blue railing on the drop side
            var nodes = new List<WalkNode>();
            for (int i = 0; i < f.walk.Count; i++)
                nodes.Add(new WalkNode(f.walk[i], y0 + f.drop[i], i + 1 < f.walk.Count && f.drop[i] - f.drop[i + 1] > 0.5f));
            Walkway(ctx, sm, nodes, 1.8f, flags, stone, 0.18f);
            PipeRail(sm, WalkSide(nodes, 1.0f, 2f, 1, nodes.Count - 2), Pal.BoatBlue);   // up to the foot of the last flight:
            PipeRail(sm, WalkSide(nodes, -1.0f, 2f, 6, nodes.Count - 2), Pal.BoatBlue);  // the ledge has its own railing
            // a lamp post at the landing
            Vector3 lampP = V3(f.walk[6], y0 + f.drop[6]) + V3(new Vector2(f.walk[6].y - f.walk[5].y, f.walk[5].x - f.walk[6].x).normalized * -1.3f, 0f);
            sm.atlas.Cylinder(lampP, 0.07f, 0.05f, 5.2f, 6, S(Pal.IronGreen), true);
            sm.atlas.Limb(lampP + Vector3.up * 5.1f, lampP + Vector3.up * 5.0f + Vector3.right * 0.6f, 0.03f, 0.03f, 4, S(Pal.IronGreen), false);
            sm.loose.Sphere(lampP + Vector3.up * 4.85f + Vector3.right * 0.6f, new Vector3(0.12f, 0.08f, 0.12f), 6, 3, S(Pal.LampBulb));

            // 7. the tin-roofed sheds and the tea stall south of the walk, stepping down the shelf
            float WalkS(int node) { float l = 0f; for (int i = 0; i < node; i++) l += Vector2.Distance(f.walk[i], f.walk[i + 1]); return l; }
            void Shed(float swA, float swB, float sideNear, float depth, float h, string sign, Color roof, bool blackTarp)
            {
                float mid = (swA + swB) * 0.5f;
                var p = PointAt(f.walk, mid, out var dir);
                Vector2 right = new Vector2(dir.y, -dir.x);
                Vector2 c = p + right * (sideNear - depth * 0.5f);
                float yaw = Mathf.Atan2(right.x, right.y) * Mathf.Rad2Deg;   // front to the walk
                float floor = Mathf.Min(f.WalkLevel(y0, mid) - 0.25f, ctx.H(c) + 0.6f);
                float hy2 = Hut(ctx, sm, c, yaw, swB - swA, depth, h, "white_rough_plaster", plaster, roof, Pal.BoatBlue, true, 0.5f, floor, LightTin());
                var q = Quaternion.Euler(0f, yaw, 0f);
                Vector3 o = V3(c, hy2);
                if (blackTarp)
                    sm.loose.QuadDouble(o + q * new Vector3(-(swB - swA) * 0.45f, 0.3f, depth * 0.5f + 0.06f), o + q * new Vector3((swB - swA) * 0.1f, 0.2f, depth * 0.5f + 0.06f),
                                        o + q * new Vector3((swB - swA) * 0.1f, h - 0.1f, depth * 0.5f + 0.1f), o + q * new Vector3(-(swB - swA) * 0.45f, h - 0.1f, depth * 0.5f + 0.1f),
                                        new Rect(S(Pal.IronBlack), Vector2.zero), q * Vector3.forward);
                // a blue tarp patch weighed down on the roof (the roof falls from h at the front eave to h - 0.35 at the back)
                float RoofY(float z) => Mathf.Lerp(h - 0.37f, h + 0.06f, (z + depth * 0.5f + 0.25f) / (depth + 0.75f)) + 0.05f;
                float px = (float)rng.NextDouble() * 2f - 1f;
                sm.loose.QuadDouble(o + q * new Vector3(px - 1.4f, RoofY(-0.8f), -0.8f), o + q * new Vector3(px + 1.4f, RoofY(-0.8f), -0.8f),
                                    o + q * new Vector3(px + 1.4f, RoofY(1.4f), 1.4f), o + q * new Vector3(px - 1.4f, RoofY(1.4f), 1.4f),
                                    new Rect(S(Pal.BoatBlue), Vector2.zero), Vector3.up);
                if (sign != null)   // the painted name over the door, under the eave
                    Board(sm, o + q * new Vector3(0f, h - 0.4f, depth * 0.5f + 0.08f), yaw, Mathf.Min(3.6f, swB - swA - 0.4f), 0.45f, Pal.BoatYellow, new[] { sign },
                          new[] { 0.19f }, Pal.JacketRed, Pal.IronBlack, o.y + h);
            }
            float sL2 = WalkS(2), sL3 = WalkS(3), sL4 = WalkS(4), sL5 = WalkS(5), sL6 = WalkS(6);
            Shed(sL2 + 0.6f, sL3 + 0.4f, -1.5f, 7f, 2.7f, null, tinRoof, true);
            Shed(sL4 + 0.3f, sL5 + 0.6f, -1.5f, 7f, 2.7f, "MAGGI  TEA  OMLET", tinRoof, false);
            Shed(sL3 - 1f, sL5, -10.5f, 10f, 2.9f, null, new Color(0.86f, 0.88f, 0.9f), false);
            Shed(sL5 + 1.4f, sL6 + 1.8f, -1.8f, 6f, 2.6f, "SUBHAN KHAN TEA STALL", tinRoof, false);
            Shed(sL5 + 2f, sL6 + 6f, -9f, 11f, 2.8f, null, new Color(0.8f, 0.86f, 0.92f), false);

            // 8. Suicide Point: a round ledge at the end, railed on the drop side, boulders at its foot
            Vector2 endP = f.walk[f.walk.Count - 1], endDir = (endP - f.walk[f.walk.Count - 2]).normalized;
            float ey = y0 + f.drop[f.drop.Count - 1];
            Vector2 ledge = endP + endDir * 2.2f;
            const int seg = 14;
            var railE = new List<Vector3>();
            for (int k = 0; k < seg; k++)
            {
                float a0 = k * Mathf.PI * 2f / seg, a1 = (k + 1) * Mathf.PI * 2f / seg;
                Vector2 p0 = ledge + new Vector2(Mathf.Sin(a0), Mathf.Cos(a0)) * 3.2f, p1 = ledge + new Vector2(Mathf.Sin(a1), Mathf.Cos(a1)) * 3.2f;
                Slab(flags, stone, ledge, (p0 + p1) * 0.5f, Vector2.Distance(p0, p1) * 1.02f, ey, ey, Mathf.Min(GroundMin(ctx, ledge, (p0 + p1) * 0.5f, 1f), ey) - 0.6f);
            }
            for (int k = -5; k <= 5; k++)
            {
                float a = Mathf.Atan2(endDir.x, endDir.y) + k * 0.3f;
                railE.Add(V3(ledge + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 3.0f, ey));
            }
            PipeRail(sm, railE, Pal.BoatBlue);
            var rock = sm.Photo(GreyRock(), new Color(0.78f, 0.78f, 0.76f), 0.1f);
            for (int k = 0; k < 6; k++)
            {
                float a = Mathf.Atan2(endDir.x, endDir.y) + (k - 2.5f) * 0.55f;
                Vector2 bp = ledge + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * (3.6f + (float)rng.NextDouble());
                float rr = 0.8f + (float)rng.NextDouble() * 0.9f;
                RockLump(rock, V3(bp, Mathf.Min(ctx.H(bp), ey - 0.4f) - 0.2f), new Vector3(rr * 1.2f, rr * 0.8f, rr), rng.Next(1000));
            }
            Vector2 spb = ledge - endDir * 1.0f + new Vector2(endDir.y, -endDir.x) * 2.4f;
            Board(sm, V3(spb, ey + 1.6f), Mathf.Atan2(-endDir.x, -endDir.y) * Mathf.Rad2Deg, 1.8f, 0.6f, Pal.JacketBlue, new[] { "SUICIDE POINT", "STAY BEHIND THE RAILING" },
                  new[] { 0.22f, 0.09f }, Pal.ShirtWhite, Pal.BoatBlue, ey);
            Bench(sm.atlas, V3(ledge - endDir * 1.4f - new Vector2(endDir.y, -endDir.x) * 1.6f, ey), Mathf.Atan2(endDir.x, endDir.y) * Mathf.Rad2Deg, Pal.BoatGreen, Pal.BoatYellow, 1.6f);

            sm.Emit(ctx, "Lover's Point", parent);
            // info boards, zones, teleport points and the preview camera
            Vector3 info = VergeP(4.2f, f.hw + 5.2f);
            var site = new Site { origin = info, rot = Quaternion.Euler(0f, FaceRoad(XZ(info)), 0f), depth = 1f };
            Sign(ctx, "Lover's Point", site, Vector3.zero, parent);
            var sSite = new Site { origin = V3(ledge - endDir * 2.6f + new Vector2(endDir.y, -endDir.x) * -2.2f, ey), rot = Quaternion.Euler(0f, Mathf.Atan2(-endDir.x, -endDir.y) * Mathf.Rad2Deg, 0f), depth = 1f };
            Sign(ctx, "Suicide Point", sSite, Vector3.zero, parent);
            Vector3 front = VergeP(1f, f.hw + 4.5f);
            ctx.Zone("Lover's Point", front, 45f);
            ctx.Zone("Suicide Point", V3(ledge, ey), 14f);
            ctx.landmarkSites["Lover's Point"] = front + Vector3.up * 0.1f;
            ctx.landmarkSites["Suicide Point"] = V3(endP, ey + 0.1f);
            var cam = new GameObject("ViewShot Lover's Point");
            cam.transform.SetParent(parent, false);
            Vector3 cp = VergeP(-6f, -(f.hw - 1.5f));
            cam.transform.SetPositionAndRotation(cp + Vector3.up * 1.7f, Quaternion.LookRotation(V3(hutC, hy + 1.2f) - (cp + Vector3.up * 1.7f)));
            Debug.Log($"[Nainital] Lover's Point: verge level {y0:F1}, Suicide Point {ey:F1} ({ey - y0:F1} m), walk {total:F0} m");
        }
    }
}
