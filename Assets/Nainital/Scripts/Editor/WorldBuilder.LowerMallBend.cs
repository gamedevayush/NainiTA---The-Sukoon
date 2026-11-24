using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// The turn from the Tallital road into the Lower Mall at the "Mall Road Nainital" board (owner, 24 Nov 2026: "lower mall
    /// road ki turn sharp hai...isko smooth kro..lake ki taraf thoda extend krdo with boundary"). Mall Road (7.5 m) meets
    /// the Lower Mall (6 m) at the fork with a 21 degree kink, and the lake-side edge steps in there. Here the inside of the
    /// bend gets extra asphalt towards the lake, up to 2.6 m at the fork, easing out over 24 m either way, so the lake-side
    /// edge runs as one curve. The data's roads (and so traffic) are unchanged; the boundary on the new edge is Ambedkar
    /// Park's road-side grill (WorldBuilder.AmbedkarPark.cs).
    /// </summary>
    public static partial class WorldBuilder
    {
        const float BendExtra = 2.6f, BendReach = 24f;

        class BendChain
        {
            public List<Vector2> pts = new List<Vector2>();
            public List<float> s = new List<float>();   // arc length, 0 at the fork, negative on Mall Road
            public List<float> half = new List<float>();
            public float lakeSign = 1f;                  // which normal points to the lake
        }

        static BendChain bendChain;

        static BendChain LowerMallBendChain(WorldCtx ctx)
        {
            if (bendChain != null) return bendChain;
            RoadData mall = null, lower = null;
            foreach (var r in ctx.world.roads)
                if (r.name == MallLower && lower == null) lower = r;
            if (lower == null) return null;
            Vector2 j = new Vector2(lower.pts[0], lower.pts[1]);
            foreach (var r in ctx.world.roads)   // the Mall Road that ends at the fork
                if (r.name == "Mall Road" && r.pts.Length >= 4 && Vector2.Distance(new Vector2(r.pts[r.pts.Length - 2], r.pts[r.pts.Length - 1]), j) < 1f) mall = r;
            if (mall == null) return null;
            var mp = Geo.ToPoints(mall.pts);
            if (Vector2.Distance(mp[mp.Length - 1], j) > 1f) return null;
            var lp = Geo.ToPoints(lower.pts);
            var c = new BendChain();
            float acc = 0f;
            var back = new List<(Vector2, float)>();
            for (int i = mp.Length - 1; i >= 0; i--)
            {
                if (i < mp.Length - 1) acc += Vector2.Distance(mp[i], mp[i + 1]);
                back.Add((mp[i], -acc));
                if (acc > BendReach + 12f) break;
            }
            for (int i = back.Count - 1; i >= 1; i--) { c.pts.Add(back[i].Item1); c.s.Add(back[i].Item2); c.half.Add(mall.width * 0.5f); }
            acc = 0f;
            for (int i = 0; i < lp.Length; i++)
            {
                if (i > 0) acc += Vector2.Distance(lp[i - 1], lp[i]);
                c.pts.Add(lp[i]); c.s.Add(acc); c.half.Add(lower.width * 0.5f);
                if (acc > BendReach + 12f) break;
            }
            Vector2 d = (lp[1] - mp[mp.Length - 2]).normalized, n = new Vector2(-d.y, d.x);
            c.lakeSign = ctx.InLake(j + n * 25f) ? 1f : -1f;
            bendChain = c;
            return c;
        }

        static float BendExtraAt(float s)
        {
            float u = Mathf.Abs(s) / BendReach;
            if (u >= 1f) return 0f;
            float k = 1f - u * u;
            return BendExtra * k * k;
        }

        /// <summary>Nearest chain point: its arc length, the lake-side offset of q from the centre line, and the half width.</summary>
        static bool BendLocal(BendChain c, Vector2 q, out float s, out float lateral, out float half, out Vector2 centre, out Vector2 lakeN)
        {
            s = 0f; lateral = 0f; half = 3f; centre = q; lakeN = Vector2.zero;
            float best = float.MaxValue;
            for (int i = 0; i + 1 < c.pts.Count; i++)
            {
                Vector2 a = c.pts[i], b = c.pts[i + 1], ab = b - a;
                float len = ab.magnitude;
                if (len < 0.01f) continue;
                float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / (len * len));
                Vector2 p = a + ab * t;
                float dd = (q - p).sqrMagnitude;
                if (dd >= best) continue;
                best = dd;
                Vector2 dir = ab / len;
                Vector2 n = new Vector2(-dir.y, dir.x) * c.lakeSign;
                s = Mathf.Lerp(c.s[i], c.s[i + 1], t);
                half = Mathf.Lerp(c.half[i], c.half[i + 1], t);
                lateral = Vector2.Dot(q - p, n);
                centre = p;
                lakeN = n;
            }
            return best < float.MaxValue;
        }

        /// <summary>From a shore point, how far inland (along o) the bend's widened asphalt begins (9 m if it is not near).</summary>
        static float LowerMallBendDistance(WorldCtx ctx, Vector2 p, Vector2 o)
        {
            var c = LowerMallBendChain(ctx);
            if (c == null) return 9f;
            for (float d = 0.5f; d < 22f; d += 0.25f)
            {
                Vector2 q = p + o * d;
                if (!BendLocal(c, q, out float s, out float lat, out float half, out _, out _)) break;
                if (lat <= half + BendExtraAt(s)) return d;
            }
            return 9f;
        }

        /// <summary>Paint the extra asphalt on the lake side of the bend (called from BuildTallital).</summary>
        static void PaintLowerMallBend(WorldCtx ctx)
        {
            bendChain = null;   // a fresh build
            var c = LowerMallBendChain(ctx);
            if (c == null) { Debug.LogWarning("[Nainital] Lower Mall bend: Mall Road / Lower Mall not found"); return; }
            var mb = new MeshBuilder();
            Vector3 prevIn = default, prevOut = default;
            bool have = false;
            float total = 0f;
            for (float s = -BendReach; s <= BendReach; s += 1f)
            {
                // the chain point at arc length s
                int i = 0;
                while (i + 2 < c.s.Count && c.s[i + 1] < s) i++;
                float t = Mathf.InverseLerp(c.s[i], c.s[i + 1], s);
                Vector2 p = Vector2.Lerp(c.pts[i], c.pts[i + 1], t);
                Vector2 dir = (c.pts[i + 1] - c.pts[i]).normalized;
                Vector2 n = new Vector2(-dir.y, dir.x) * c.lakeSign;
                // at the fork the two roads' normals differ: average them for a seam-free edge
                if (Mathf.Abs(s) < 4f && i > 0)
                {
                    Vector2 d0 = (c.pts[i] - c.pts[i - 1]).normalized;
                    n = (n + new Vector2(-d0.y, d0.x) * c.lakeSign).normalized;
                }
                float half = Mathf.Lerp(c.half[i], c.half[i + 1], t);
                float ex = BendExtraAt(s);
                Vector2 pin = p + n * (half - 0.4f), pout = p + n * (half + ex);
                Vector3 vi = V(pin, ctx.H(pin) + 0.07f), vo = V(pout, ctx.H(pout) + 0.07f);
                if (have) mb.Quad(prevIn, vi, vo, prevOut, new Rect(0f, 0f, 1f, 1f), Vector3.up);
                prevIn = vi; prevOut = vo; have = true;
                total += ex;
            }
            PaintRoadSurface(mb, ChAsphalt);
            Debug.Log($"[Nainital] Lower Mall bend: lake side widened up to {BendExtra:F1} m over {BendReach * 2f:F0} m (lake sign {c.lakeSign})");
        }
    }
}
