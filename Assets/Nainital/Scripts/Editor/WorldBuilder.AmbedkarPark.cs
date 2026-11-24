using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Tallital's lakeside on the road up to Mall Road's fork (owner, 24 Nov 2026, from the editor with his Street View:
    /// "green grill honi chahiye lake ke border pe ... boats terrain me ghusi hui hain.. actual me wahan boat hongi hi nhi..
    /// vo aage hain ..or uske just aage bhimrao ambedkar park"). The Esri satellite shows the moored boats at (836, -1075),
    /// on the shore just short of the fork, and a small walled-in green with trees between the road and the water from
    /// there up to the fork: Dr. Bhimrao Ambedkar Park. Street View: green iron grill along the road and the water, pink-red
    /// gate pillars, a paved floor.
    /// Here: the boat stand moves to the boats (BuildJettyAt), a green grill runs along the water from the chowk's terrace to
    /// the fork, and the park fills the strip between the road and the lake from the boats to the fork, with the Lower Mall
    /// bend's lake-side boundary as its road-side grill (WorldBuilder.LowerMallBend.cs widens that bend).
    /// </summary>
    public static partial class WorldBuilder
    {
        internal static readonly Vector2 TallitalBoatsAt = new Vector2(836f, -1075f);
        static readonly Vector2 MallForkLakeEnd = new Vector2(852f, -1051f);   // the park's north end, at the fork

        /// <summary>The nearest point on the lake outline and the way inland from it.</summary>
        internal static bool ShorePoint(WorldCtx ctx, Vector2 near, out Vector2 p, out Vector2 inland)
        {
            p = near;
            inland = Vector2.up;
            var L = ctx.lake;
            if (L == null || L.Length < 3) return false;
            float best = float.MaxValue;
            for (int i = 0; i < L.Length; i++)
            {
                Vector2 a = L[i], b = L[(i + 1) % L.Length], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(near - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                Vector2 q = a + ab * t;
                float d = (q - near).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                p = q;
                Vector2 n = new Vector2(ab.y, -ab.x).normalized;
                inland = ctx.InLake(q + n * 2f) ? -n : n;
            }
            return true;
        }

        /// <summary>Shore samples every ~2 m from a to b (both projected onto the lake outline).</summary>
        static List<(Vector2 p, Vector2 o)> ShoreRun(WorldCtx ctx, Vector2 a, Vector2 b)
        {
            var run = new List<(Vector2, Vector2)>();
            int n = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(a, b) / 2f));
            for (int i = 0; i <= n; i++)
                if (ShorePoint(ctx, Vector2.Lerp(a, b, i / (float)n), out var p, out var o)) run.Add((p, o));
            return run;
        }

        /// <summary>Called from BuildTallital after the terrace: the grill on the water and Ambedkar Park.</summary>
        static void BuildAmbedkarPark(WorldCtx ctx, Transform parent, TallitalPlan t)
        {
            var root = new GameObject("Tallital lakeside (grill, Ambedkar Park)").transform;
            root.SetParent(parent, false);

            // ---- the green grill on the water, from the end of the chowk's terrace to the boats
            int n = t.shore.Count;
            Vector2 terraceEnd = t.shore[((t.c0 + t.dir * t.kEnd) % n + n) % n];
            ShorePoint(ctx, TallitalBoatsAt, out var jetty, out var jIn);
            Vector2 jAlong = new Vector2(jIn.y, -jIn.x);
            if (Vector2.Dot(jAlong, MallForkLakeEnd - jetty) < 0f) jAlong = -jAlong;   // along the shore, towards the fork
            var rail = new MeshBuilder();
            var railCol = new MeshBuilder();
            void Grill(List<(Vector2 p, Vector2 o)> run, float inset, bool skipJetty)
            {
                var pts = new List<Vector3>();
                foreach (var (p, o) in run)
                {
                    Vector2 q = p + o * inset;
                    if (skipJetty && NearJetty(ctx, q, 4.5f)) { if (pts.Count > 1) PropKit.Railing(rail, railCol, pts); pts.Clear(); continue; }
                    pts.Add(V(q, ctx.H(q) + 0.07f));
                }
                if (pts.Count > 1) PropKit.Railing(rail, railCol, pts);
            }
            Grill(ShoreRun(ctx, terraceEnd, jetty + jAlong * 6f), 0.9f, true);

            // ---- the park: between the water and the bend's lake-side edge, from the boats to the fork
            Vector2 a = jetty + jAlong * 6.5f, b = MallForkLakeEnd;
            var run = ShoreRun(ctx, a, b);
            var inner = new List<Vector2>();   // at the water (inside the grill)
            var outer = new List<Vector2>();   // at the road (inside its grill)
            foreach (var (p, o) in run)
            {
                Vector2 pin = p + o * 0.9f;
                float w = LowerMallBendDistance(ctx, p, o) - 1.0f;   // up to a metre short of the bend's edge
                if (w < 3f) w = 3f;
                inner.Add(pin);
                outer.Add(p + o * w);
            }
            if (inner.Count < 3) { Debug.LogWarning("[Nainital] Ambedkar Park: no shore here"); return; }

            // floor: red pavers into the ground, as the chowk's
            var floor = new MeshBuilder();
            float v = 0f;
            for (int i = 1; i < inner.Count; i++)
            {
                float seg = Vector2.Distance(inner[i - 1], inner[i]);
                floor.Quad(V(inner[i - 1], ctx.H(inner[i - 1]) + 0.07f), V(inner[i], ctx.H(inner[i]) + 0.07f),
                           V(outer[i], ctx.H(outer[i]) + 0.07f), V(outer[i - 1], ctx.H(outer[i - 1]) + 0.07f),
                           new Rect(0f, v / 2.5f, seg / 2.5f, 1f), Vector3.up);
                v += seg;
            }
            PaintRoadSurface(floor, ChRedPaver);
            var mid = new List<Vector2>();
            for (int i = 0; i < inner.Count; i++) mid.Add((inner[i] + outer[i]) * 0.5f);
            noGrassExtra.Add((mid.ToArray(), 5f));
            ctx.keepClear.Add(((inner[inner.Count / 2] + outer[outer.Count / 2]) * 0.5f, 6f));

            // grill: the water side, the two short ends, and the road side broken for the gate
            Grill(run, 0.9f, false);
            void Line(List<Vector2> ps)
            {
                var pts = new List<Vector3>();
                foreach (var p in ps) pts.Add(V(p, ctx.H(p) + 0.07f));
                if (pts.Count > 1) PropKit.Railing(rail, railCol, pts);
            }
            Line(new List<Vector2> { inner[0], outer[0] });
            Line(new List<Vector2> { inner[inner.Count - 1], outer[outer.Count - 1] });
            int gate = outer.Count / 2;
            const float gateHalf = 1.6f;
            Vector2 gp = outer[gate];
            Vector2 gt = (outer[Mathf.Min(gate + 1, outer.Count - 1)] - outer[Mathf.Max(gate - 1, 0)]).normalized;
            var left = new List<Vector2>();
            var right = new List<Vector2>();
            for (int i = 0; i < outer.Count; i++)
            {
                float s = Vector2.Dot(outer[i] - gp, gt);
                if (s < -gateHalf) left.Add(outer[i]);
                else if (s > gateHalf) right.Add(outer[i]);
            }
            left.Add(gp - gt * gateHalf);
            right.Insert(0, gp + gt * gateHalf);
            Line(left);
            Line(right);
            ctx.MeshObject("Tallital lake grill", root, rail, ctx.mats.atlas, railCol, false);

            // the gate: two pink-red pillars with white caps (Street View), the park's name on a board between them
            var mb = new MeshBuilder();
            var col = new MeshBuilder();
            Vector2 gIn = -new Vector2(gt.y, -gt.x);
            if (Vector2.Dot(gIn, inner[gate] - gp) < 0f) gIn = -gIn;   // from the gate into the park
            var gq = Quaternion.LookRotation(Flat(-gIn));                 // the gate faces the road
            Vector2 red = Atlas.SolidUV(Pal.TempleRed), white = Atlas.SolidUV(Pal.TempleWhite), cream = Atlas.WallSolid(1);
            float gy = ctx.H(gp);
            foreach (float sx in new[] { -1f, 1f })
            {
                Vector2 pp = gp + gt * (sx * (gateHalf + 0.25f));
                Vector3 pc = V(pp, ctx.H(pp));
                mb.Box(pc + Vector3.up * 1.1f, new Vector3(0.5f, 2.2f, 0.5f), gq, red);
                mb.Box(pc + Vector3.up * 2.28f, new Vector3(0.62f, 0.16f, 0.62f), gq, white);
                mb.Box(pc + Vector3.up * 0.12f, new Vector3(0.6f, 0.24f, 0.6f), gq, white);
                col.Box(pc + Vector3.up * 1.1f, new Vector3(0.5f, 2.2f, 0.5f), gq, Vector2.zero);
            }
            Vector3 boardC = V(gp, gy + 2.75f);
            mb.Box(boardC, new Vector3(gateHalf * 2f + 0.9f, 0.55f, 0.08f), gq, cream);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Label(root, font, "DR. BHIMRAO AMBEDKAR PARK", boardC + Flat(-gIn) * 0.06f, Flat(-gIn), 0.26f, new Color(0.12f, 0.2f, 0.5f));

            // Babasaheb on a pedestal in the middle, facing the road: blue coat, the book of the Constitution in his left
            // arm, the right arm raised and pointing ahead
            Vector2 sc = (inner[gate] + outer[gate]) * 0.5f + gIn * 0.4f;
            float sy = ctx.H(sc);
            var sq = Quaternion.LookRotation(Flat(-gIn));
            Vector3 s0 = V(sc, sy);
            Rect stone = Atlas.Material(Atlas.MatStone);
            TexBox(mb, s0 + Vector3.up * 0.25f, new Vector3(2.2f, 0.5f, 2.2f), sq, stone);
            mb.Box(s0 + Vector3.up * 1.35f, new Vector3(1.2f, 1.7f, 1.2f), sq, cream);
            mb.Box(s0 + Vector3.up * 2.28f, new Vector3(1.45f, 0.16f, 1.45f), sq, Atlas.MaterialUV(Atlas.MatConcrete));
            col.Box(s0 + Vector3.up * 1.2f, new Vector3(2.2f, 2.4f, 2.2f), sq, Vector2.zero);
            Vector2 coat = Atlas.SolidUV(Pal.JacketBlue), trousers = Atlas.SolidUV(Pal.JacketBlue), shoe = Atlas.SolidUV(Pal.IronBlack),
                    skin = Atlas.SolidUV(Pal.SkinDark), hair = Atlas.SolidUV(Pal.HairBlack), book = Atlas.SolidUV(Pal.JacketRed), tie = Atlas.SolidUV(Pal.JacketRed);
            using (PropKit.At(mb, s0 + Vector3.up * 2.36f, sq, 1.1f))
            {
                mb.Box(new Vector3(0.1f, 0.03f, 0.05f), new Vector3(0.12f, 0.06f, 0.26f), shoe);
                mb.Box(new Vector3(-0.1f, 0.03f, 0.05f), new Vector3(0.12f, 0.06f, 0.26f), shoe);
                mb.Limb(new Vector3(0.1f, 0.05f, 0f), new Vector3(0.1f, 0.85f, 0f), 0.07f, 0.08f, 6, trousers);
                mb.Limb(new Vector3(-0.1f, 0.05f, 0f), new Vector3(-0.1f, 0.85f, 0f), 0.07f, 0.08f, 6, trousers);
                mb.Cylinder(new Vector3(0f, 0.8f, 0f), 0.2f, 0.2f, 0.25f, 8, coat, true);       // coat skirt
                mb.Cylinder(new Vector3(0f, 1.15f, 0f), 0.2f, 0.18f, 0.42f, 8, coat, true);     // chest
                mb.Box(new Vector3(0f, 1.22f, 0.17f), new Vector3(0.05f, 0.3f, 0.02f), tie);
                mb.Box(new Vector3(0f, 1.36f, 0.16f), new Vector3(0.12f, 0.06f, 0.03f), Atlas.SolidUV(Pal.ShirtWhite));
                mb.Limb(new Vector3(-0.2f, 1.5f, 0f), new Vector3(0.2f, 1.5f, 0f), 0.07f, 0.07f, 6, coat);   // shoulders
                mb.Limb(new Vector3(0f, 1.52f, 0f), new Vector3(0f, 1.62f, 0.01f), 0.05f, 0.05f, 5, skin);    // neck
                mb.Sphere(new Vector3(0f, 1.73f, 0.02f), new Vector3(0.11f, 0.13f, 0.12f), 8, 6, skin);      // head
                mb.Sphere(new Vector3(0f, 1.79f, -0.01f), new Vector3(0.115f, 0.09f, 0.12f), 8, 5, hair);    // hair, combed back
                // right arm raised forward, the index finger pointing ahead
                mb.Limb(new Vector3(0.21f, 1.47f, 0f), new Vector3(0.3f, 1.62f, 0.28f), 0.05f, 0.045f, 5, coat);
                mb.Limb(new Vector3(0.3f, 1.62f, 0.28f), new Vector3(0.34f, 1.8f, 0.52f), 0.042f, 0.035f, 5, coat);
                mb.Sphere(new Vector3(0.345f, 1.83f, 0.57f), Vector3.one * 0.045f, 6, 4, skin);
                mb.Limb(new Vector3(0.35f, 1.85f, 0.6f), new Vector3(0.36f, 1.88f, 0.68f), 0.012f, 0.01f, 4, skin);
                // left arm down at the side, the book held against the chest
                mb.Limb(new Vector3(-0.21f, 1.47f, 0f), new Vector3(-0.25f, 1.15f, 0.06f), 0.05f, 0.045f, 5, coat);
                mb.Limb(new Vector3(-0.25f, 1.15f, 0.06f), new Vector3(-0.1f, 1.1f, 0.2f), 0.042f, 0.035f, 5, coat);
                mb.Box(new Vector3(-0.12f, 1.2f, 0.2f), new Vector3(0.2f, 0.27f, 0.06f), Quaternion.Euler(0f, 0f, 12f), book);
            }

            // benches along the water, facing it, either side of the statue; two lamps by the gate
            var rnd = new System.Random(1891);
            foreach (int i in new[] { Mathf.Max(1, gate - 4), Mathf.Min(inner.Count - 2, gate + 4) })
            {
                if (i == gate) continue;
                Vector2 bp = Vector2.Lerp(inner[i], outer[i], 0.3f);
                var bq = Quaternion.LookRotation(Flat(-gIn));
                bq = Quaternion.LookRotation(Flat(inner[i] - outer[i]));   // facing the lake
                Vector3 at = V(bp, ctx.H(bp) + 0.07f);
                using (PropKit.At(mb, at, bq)) PropKit.Bench(mb);
                col.Box(at + Vector3.up * 0.4f, new Vector3(1.6f, 0.8f, 0.6f), bq, Vector2.zero);
            }
            foreach (float sx in new[] { -1f, 1f })
            {
                Vector2 lp = gp + gt * (sx * (gateHalf + 2.2f)) + gIn * 0.6f;
                Vector3 lamp = V(lp, ctx.H(lp));
                var lq = Quaternion.LookRotation(Flat(gt));
                Vector3 bulb;
                using (PropKit.At(mb, lamp, lq)) bulb = MallTwinLamp(mb, rnd);
                ctx.lampHeads.Add(lamp + lq * bulb);
            }
            ctx.MeshObject("Ambedkar Park", root, mb, ctx.mats.atlas, col);
            ctx.Zone("Dr. Bhimrao Ambedkar Park", V(sc, sy), 14f);
            ctx.InfoSign("Dr. Bhimrao Ambedkar Park", "Jheel ke kinare chhota sa park - Babasaheb ki murti, benches, aur saamne naav ka adda.",
                         V(gp + gt * (gateHalf + 3.4f) - gIn * 0.8f, ctx.H(gp + gt * (gateHalf + 3.4f) - gIn * 0.8f)), Quaternion.LookRotation(Flat(-gIn)), root);
            Debug.Log($"[Nainital] Ambedkar Park: {inner.Count} shore samples, {Vector2.Distance(inner[0], inner[inner.Count - 1]):F0} m along the water, " +
                      $"gate at ({gp.x:F1}, {gp.y:F1}); boat stand at ({jetty.x:F1}, {jetty.y:F1})");
        }
    }
}
