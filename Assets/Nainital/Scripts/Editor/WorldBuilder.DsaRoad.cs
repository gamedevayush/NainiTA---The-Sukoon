using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// DSA road (the walk from Mall Road to Naina Devi along the Flats) on its two levels, from the bandstand to the Mall
    /// (owner, 23 Nov 2026, a marked Google Maps image: "DSA road is in two levels, here green one is main road connecting
    /// naina devi mandir with mall road..yellow is its down and parallel with lake..green and yellow connected together with
    /// stair..orange are parks...pink is for boat house which is also down mesh..black dots are all trees..so overall if we
    /// enter DSA road lake will be visible from bandstand part..otherwise tress will be there").
    /// From the map's lines (world.json): the upper road (pedestrian, 5.5 m) (24,29) -> (39,51) -> (73,102) -> (87,115); the
    /// lower path along the lake (40,19) -> (50,36) -> (58,51) -> (66,56) -> (89,55) and up beside the Boat House to the Mall
    /// (89,55) -> (86,86) -> (87,115); the stairs between them (39,51) - (53,42); the small park between the levels.
    /// The Capitol's stretch further south is WorldBuilder.CapitolGround.cs (the same idea: the walk high, the lake side low).
    ///  1. ShapeDsaGround (before the roads): the lake side of the upper road comes down to the lower path's level - the
    ///     path, the small park, the Boat House lawn - the ground under the wall low too; the path up beside the Boat House a
    ///     straight grade.
    ///  2. PlanDsaTrees: rows of deodar and oak along both sides of the upper road (planted by BuildTrees), none on the
    ///     stretch by the bandstand, where the lake is seen.
    ///  3. BuildDsaRoad (on the final ground): the stone retaining wall along the upper road's lake edge with its coping, the
    ///     green railing on it (violet LED along its top rail, warm lights at the wall's foot), a flat ledge on the high side
    ///     (no trench), the stairs down to the lower path with side walls, the small park's fence.
    /// </summary>
    public static partial class WorldBuilder
    {
        static readonly Vector2[] DsaUpper = { new Vector2(24f, 29f), new Vector2(39f, 51f), new Vector2(73f, 102f), new Vector2(87f, 115f) };
        static readonly Vector2[] DsaLower = { new Vector2(40f, 19f), new Vector2(50f, 36f), new Vector2(53f, 42f), new Vector2(58f, 51f), new Vector2(66f, 56f), new Vector2(75f, 55f), new Vector2(89f, 55f) };
        static readonly Vector2[] DsaRamp = { new Vector2(89f, 55f), new Vector2(86f, 86f), new Vector2(86f, 102f), new Vector2(87f, 115f) };
        static readonly Vector2 DsaStairTop = new Vector2(39f, 51f), DsaStairFoot = new Vector2(53f, 42f);
        static readonly Vector2[] DsaSmallPark = { new Vector2(28f, 29f), new Vector2(38f, 23f), new Vector2(41f, 25f), new Vector2(51f, 40f), new Vector2(43f, 45f), new Vector2(38f, 44f) };
        const float DsaHalf = 2.75f, DsaWallT = 1.5f;
        /// <summary>Along the upper road (metres from (24,29)) where the wall starts and where the lake-side trees start (the
        /// open stretch by the bandstand before it).</summary>
        const float DsaWallFrom = 4f, DsaTreesFrom = 30f;

        static List<Vector2> dsaUp;
        static float[] dsaUpY;
        static float dsaLowY = float.NaN;
        internal static readonly List<Vector4> dsaTrees = new List<Vector4>();   // x, z, type, scale (BuildTrees)

        /// <summary>Along / side of q against the upper road: side &gt; 0 toward the lake, along from its start; i: the nearest
        /// resampled point.</summary>
        static void DsaUV(Vector2 q, out float along, out float side, out int i)
        {
            along = 0f; side = float.MaxValue; i = 0;
            float best = float.MaxValue, acc = 0f;
            for (int k = 0; k + 1 < dsaUp.Count; k++)
            {
                Vector2 a = dsaUp[k], b = dsaUp[k + 1], ab = b - a;
                float len = ab.magnitude;
                float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(1e-6f, len * len));
                Vector2 p = a + ab * t;
                float d = (q - p).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    along = acc + t * len;
                    Vector2 n = new Vector2(ab.y, -ab.x) / Mathf.Max(1e-6f, len);   // the right of the way (24,29) -> (87,115): south-east, the lake
                    side = Vector2.Dot(q - p, n);
                    i = t < 0.5f ? k : k + 1;
                }
                acc += len;
            }
        }

        static float DsaUpLevel(int i) => dsaUpY != null && dsaUpY.Length > 0 ? dsaUpY[Mathf.Clamp(i, 0, dsaUpY.Length - 1)] : float.NaN;

        // ------------------------------------------------------------------ 1. the ground
        static void ShapeDsaGround(WorldCtx ctx)
        {
            dsaTrees.Clear();
            dsaUp = Resample(DsaUpper, 1f, false);
            var raw = new float[dsaUp.Count];
            for (int i = 0; i < raw.Length; i++) raw[i] = ctx.H(dsaUp[i]);
            dsaUpY = new float[raw.Length];
            for (int i = 0; i < raw.Length; i++)
            {
                float s = 0f; int n = 0;
                for (int j = Mathf.Max(0, i - 4); j <= Mathf.Min(raw.Length - 1, i + 4); j++) { s += raw[j]; n++; }
                dsaUpY[i] = s / n;
            }
            // the lower level: the Capitol's lakeside level (the path at the bandstand), never under 1.4 m over the water
            dsaLowY = !float.IsNaN(capLowY) ? capLowY : ctx.H(DsaLower[0]);
            dsaLowY = Mathf.Max(dsaLowY, ctx.lakeY + 1.4f);
            float mallY = ctx.H(DsaRamp[DsaRamp.Length - 1]);
            var ramp = Resample(DsaRamp, 1f, false);
            float rampLen = 0f;
            for (int i = 1; i < ramp.Count; i++) rampLen += Vector2.Distance(ramp[i - 1], ramp[i]);

            var td = ctx.terrain.terrainData;
            int res = td.heightmapResolution;
            Vector3 tp = ctx.terrain.transform.position, sz = td.size;
            float cx = (res - 1) / sz.x, cz = (res - 1) / sz.z;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((10f - tp.x) * cx), 0, res - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((100f - tp.x) * cx), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((15f - tp.z) * cz), 0, res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((118f - tp.z) * cz), 0, res - 1);
            int w = x1 - x0 + 1, h = z1 - z0 + 1;
            var hs = td.GetHeights(x0, z0, w, h);
            float upLen = 0f;
            for (int i = 1; i < dsaUp.Count; i++) upLen += Vector2.Distance(dsaUp[i - 1], dsaUp[i]);
            int lowN = 0, rampN = 0, wallN = 0;
            for (int zi = 0; zi < h; zi++)
                for (int xi = 0; xi < w; xi++)
                {
                    var q = new Vector2(tp.x + (x0 + xi) / cx, tp.z + (z0 + zi) / cz);
                    if (ctx.InLake(q)) continue;
                    DsaUV(q, out float along, out float side, out int ui);
                    if (along < DsaWallFrom - 2f || along > upLen - 1f) continue;   // beyond its two ends: the Capitol's ground, the Mall
                    if (side <= DsaHalf + 0.05f) continue;                          // the road itself and the Flats side keep theirs
                    float cur = tp.y + hs[zi, xi] * sz.y, want = cur;
                    float upY = DsaUpLevel(ui);
                    // the path up beside the Boat House: a straight grade from the lower path to the Mall
                    float dr = Geo.DistanceToPolyline(ramp.ToArray(), q, false);
                    if (dr < 3.5f)
                    {
                        int best = 0; float bd = float.MaxValue; float acc = 0f, at = 0f;
                        for (int k = 0; k < ramp.Count; k++) { if (k > 0) acc += Vector2.Distance(ramp[k - 1], ramp[k]); float d = (ramp[k] - q).sqrMagnitude; if (d < bd) { bd = d; best = k; at = acc; } }
                        want = Mathf.Lerp(dsaLowY, mallY, Mathf.Clamp01(at / Mathf.Max(1f, rampLen)));
                        rampN++;
                    }
                    else if (side <= DsaHalf + DsaWallT + 0.1f)
                    { want = Mathf.Min(cur, dsaLowY - 0.1f); wallN++; }                // under the retaining wall: low, so the slope is inside it
                    else if (q.x < 92f && side < 70f)
                    {
                        // the lake side: the lower path, the small park and the Boat House lawn on one low level (the lawn and
                        // the park a hand higher), easing into the ground beyond 60 m from the road
                        bool lawn = Geo.PointInPolygon(DsaSmallPark, q) || along > 55f;
                        float lv = dsaLowY + (lawn ? 0.25f : 0f);
                        float k = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((side - 55f) / 12f));
                        want = Mathf.Lerp(cur, lv, k);
                        lowN++;
                    }
                    if (upY - want < 0.6f && side > DsaHalf + DsaWallT + 0.1f && dr >= 3.5f) want = Mathf.Min(want, upY - 0.6f);
                    if (Mathf.Abs(want - cur) > 0.01f) hs[zi, xi] = Mathf.Clamp01((want - tp.y) / sz.y);
                }
            td.SetHeights(x0, z0, hs);
            PlanDsaTrees(ctx, upLen);
            Debug.Log($"[Nainital] DSA road two levels: upper {dsaUpY[0]:F1}..{dsaUpY[dsaUpY.Length - 1]:F1}, lower {dsaLowY:F1}, {lowN} samples low, {wallN} under the wall, {rampN} on the Boat House path; {dsaTrees.Count} trees planned");
        }

        /// <summary>After the roads: the ground under the wall and 3.5 m out from it back at the low level, and kept there (the
        /// road's banks raised it into lumps at the wall's foot: the final RoadGround leaves bankKeep alone).</summary>
        static void KeepDsaWallFootLow(WorldCtx ctx)
        {
            if (dsaUp == null || float.IsNaN(dsaLowY)) return;
            var td = ctx.terrain.terrainData;
            int res = td.heightmapResolution;
            Vector3 tp = ctx.terrain.transform.position, sz = td.size;
            float cx = (res - 1) / sz.x, cz = (res - 1) / sz.z;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((10f - tp.x) * cx), 0, res - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((100f - tp.x) * cx), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((15f - tp.z) * cz), 0, res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((118f - tp.z) * cz), 0, res - 1);
            int w = x1 - x0 + 1, h = z1 - z0 + 1;
            var hs = td.GetHeights(x0, z0, w, h);
            float upLen = 0f;
            for (int i = 1; i < dsaUp.Count; i++) upLen += Vector2.Distance(dsaUp[i - 1], dsaUp[i]);
            var ramp = DsaRamp;
            int kept = 0;
            for (int zi = 0; zi < h; zi++)
                for (int xi = 0; xi < w; xi++)
                {
                    var q = new Vector2(tp.x + (x0 + xi) / cx, tp.z + (z0 + zi) / cz);
                    if (ctx.InLake(q)) continue;
                    DsaUV(q, out float along, out float side, out _);
                    if (along < DsaWallFrom || along > upLen - 1f) continue;
                    if (side <= DsaHalf + 0.2f || side > DsaHalf + DsaWallT + 3.5f) continue;
                    if (Geo.DistanceToPolyline(ramp, q, false) < 4f || Vector2.Distance(q, DsaStairTop) < 4f) continue;
                    float cur = tp.y + hs[zi, xi] * sz.y;
                    float want = side <= DsaHalf + DsaWallT + 0.1f ? Mathf.Min(cur, dsaLowY - 0.1f) : Mathf.Min(cur, dsaLowY + 0.05f);
                    hs[zi, xi] = Mathf.Clamp01((want - tp.y) / sz.y);
                    bankKeep.Add((z0 + zi) * res + (x0 + xi));
                    kept++;
                }
            td.SetHeights(x0, z0, hs);
            Debug.Log($"[Nainital] DSA road: {kept} samples at the wall's foot kept low");
        }

        // ------------------------------------------------------------------ 2. the trees
        static void PlanDsaTrees(WorldCtx ctx, float upLen)
        {
            var rnd = new System.Random(2323);
            float acc = 0f;
            for (int i = 1; i < dsaUp.Count; i++)
            {
                float prev = acc;
                acc += Vector2.Distance(dsaUp[i - 1], dsaUp[i]);
                Vector2 tan = (dsaUp[i] - dsaUp[i - 1]).normalized, n = new Vector2(tan.y, -tan.x);   // toward the lake
                // every ~4.5 m on the Flats side, two rows on the lake side; the lake side open by the bandstand
                if (Mathf.FloorToInt(acc / 4.5f) == Mathf.FloorToInt(prev / 4.5f)) continue;
                if (Vector2.Distance(dsaUp[i], DsaStairTop) < 5f) continue;
                void Plant(Vector2 p, float s)
                {
                    if (ctx.InLake(p) || ctx.LakeDistance(p) < 3f || Clearance(ctx).Blocked(p, 0.6f)) return;
                    int type = rnd.NextDouble() < 0.55 ? 0 : 2;   // deodar and banj oak
                    dsaTrees.Add(new Vector4(p.x, p.y, type, s * (0.85f + (float)rnd.NextDouble() * 0.35f)));
                    ctx.keepClear.Add((p, 1.2f));
                }
                Plant(dsaUp[i] - n * (DsaHalf + 2.0f + (float)rnd.NextDouble() * 0.8f) + tan * (float)(rnd.NextDouble() - 0.5), 1f);
                if (acc < DsaTreesFrom) continue;
                Plant(dsaUp[i] + n * (DsaHalf + DsaWallT + 2.2f + (float)rnd.NextDouble()), 1f);
                Plant(dsaUp[i] + n * (DsaHalf + DsaWallT + 6.5f + (float)rnd.NextDouble() * 1.5f) + tan * 2f, 1.1f);
            }
        }

        // ------------------------------------------------------------------ 3. the wall, railing, stairs, fence
        static void BuildDsaRoad(WorldCtx ctx)
        {
            if (dsaUp == null || dsaUp.Count < 2 || float.IsNaN(dsaLowY)) return;
            var parent = new GameObject("DSA road (two levels)").transform;
            parent.SetParent(ctx.worldRoot, false);
            var mossMat = MossyStoneMaterial();
            var stoneMb = new MeshBuilder();
            var mb = new MeshBuilder();
            var col = new MeshBuilder();
            Vector2 cap = Atlas.MaterialUV(Atlas.MatConcrete), iron = Atlas.SolidUV(Pal.IronGreen), stone = Atlas.MaterialUV(Atlas.MatStone);
            float acc = 0f;
            int pieces = 0;
            Vector2 stairDir = (DsaStairFoot - DsaStairTop).normalized;
            for (int i = 0; i + 1 < dsaUp.Count; i++)
            {
                Vector2 a = dsaUp[i], b = dsaUp[i + 1];
                float len = Vector2.Distance(a, b);
                float mid = acc + len * 0.5f;
                acc += len;
                if (mid < DsaWallFrom) continue;
                Vector2 tan = (b - a).normalized, n = new Vector2(tan.y, -tan.x);
                Vector2 m2 = (a + b) * 0.5f;
                if (Vector2.Distance(m2, DsaStairTop) < 1.8f) continue;   // the stairs go down here
                if (Geo.DistanceToPolyline(DsaRamp, m2 + n * (DsaHalf + 0.75f), false) < 3f) continue;   // the Boat House path meets the road here
                float top = Mathf.Lerp(DsaUpLevel(i), DsaUpLevel(i + 1), 0.5f);
                Vector2 wc = m2 + n * (DsaHalf + DsaWallT * 0.5f);
                float bottom = Mathf.Min(ctx.H(m2 + n * (DsaHalf + DsaWallT + 0.8f)), dsaLowY) - 0.4f;
                if (top - bottom < 0.5f) continue;
                // the wall and its coping follow the road's slope (flat pieces read as steps)
                float ta0 = DsaUpLevel(i), tb0 = DsaUpLevel(i + 1);
                var rot = Quaternion.LookRotation(new Vector3(b.x - a.x, tb0 - ta0, b.y - a.y));
                // the wall (the stone face on its lake side), the coping, the ledge on the high side
                TexBox(stoneMb, new Vector3(wc.x, (top + bottom) * 0.5f, wc.y), new Vector3(DsaWallT, top - bottom, len + 0.02f), rot, new Rect(mid / 2.5f, bottom / 2.5f, len / 2.5f, (top - bottom) / 2.5f));
                col.Box(new Vector3(wc.x, (top + bottom) * 0.5f, wc.y), new Vector3(DsaWallT, top - bottom, len), rot, Vector2.zero);
                Vector2 e2 = m2 + n * (DsaHalf + DsaWallT - 0.2f);
                mb.Box(new Vector3(e2.x, top + 0.15f, e2.y), new Vector3(0.4f, 0.3f, len + 0.04f), rot, cap);
                {   // the flat strip on the high side (no trench at the wall), sloping with the road, not stepped
                    Vector2 la = a + n * (DsaHalf + 0.75f), lb = b + n * (DsaHalf + 0.75f), hw2 = n * 0.75f;
                    float ta = DsaUpLevel(i) + 0.03f, tb = DsaUpLevel(i + 1) + 0.03f;
                    mb.Quad(V(la - hw2, ta), V(lb - hw2, tb), V(lb + hw2, tb), V(la + hw2, ta), cap, Vector3.up);
                    Vector3 la3 = V(la, ta - 0.5f), lb3 = V(lb, tb - 0.5f);
                    col.Box((la3 + lb3) * 0.5f, new Vector3(1.5f, 1.0f, Vector3.Distance(la3, lb3) + 0.05f), Quaternion.LookRotation(lb3 - la3), Vector2.zero);
                }
                // the green railing on its outer edge, the violet LED on its top rail, a warm foot light every 6 m
                Vector3 p0 = new Vector3(e2.x - tan.x * len * 0.5f, ta0 + 0.3f, e2.y - tan.y * len * 0.5f), p1 = new Vector3(e2.x + tan.x * len * 0.5f, tb0 + 0.3f, e2.y + tan.y * len * 0.5f);
                mb.Limb(p0, p0 + Vector3.up * 0.95f, 0.03f, 0.03f, 4, iron, false);
                mb.Limb(p0 + Vector3.up * 0.95f, p1 + Vector3.up * 0.95f, 0.028f, 0.028f, 4, iron, false);
                mb.Limb(p0 + Vector3.up * 0.5f, p1 + Vector3.up * 0.5f, 0.02f, 0.02f, 4, iron, false);
                col.Box(new Vector3(e2.x, top + 0.8f, e2.y), new Vector3(0.1f, 1.0f, len), rot, Vector2.zero);
                Led(p0 + Vector3.up * 0.99f, p1 + Vector3.up * 0.99f, LedViolet, 0.1f, 0.05f, 2.6f);
                if (pieces % 6 == 0)
                {
                    Vector2 f = m2 + n * (DsaHalf + DsaWallT + 0.35f);
                    Vector3 fl = new Vector3(f.x, ctx.H(f) + 0.25f, f.y);
                    Bulb(fl, LedWarm, 0.12f, 2.6f);
                    Accent(fl + Vector3.up * 0.2f, new Color(1f, 0.75f, 0.45f), 4.5f, 4f);
                }
                pieces++;
            }
            // the stairs from the upper road down to the lower path, between two side walls
            {
                DsaUV(DsaStairTop, out _, out _, out int ti);
                float topY = DsaUpLevel(ti), footY = ctx.H(DsaStairFoot);
                Vector2 n = stairDir;
                Vector2 start = DsaStairTop + n * (DsaHalf + 0.2f);
                float drop = topY - footY;
                if (drop > 0.4f)
                {
                    int steps = Mathf.Clamp(Mathf.RoundToInt(drop / 0.17f), 3, 40);
                    float rise = drop / steps, run = 0.3f;
                    var rot = Quaternion.LookRotation(new Vector3(n.x, 0f, n.y));
                    for (int k = 0; k < steps; k++)
                    {
                        float tread = topY - rise * (k + 1), foot = footY - 0.6f;
                        Vector2 q = start + n * ((k + 0.5f) * run);
                        mb.Box(new Vector3(q.x, (tread + foot) * 0.5f, q.y), new Vector3(2.4f, tread - foot, run), rot, cap);
                        col.Box(new Vector3(q.x, (tread + foot) * 0.5f, q.y), new Vector3(2.4f, tread - foot, run), rot, Vector2.zero);
                    }
                    Vector3 right = rot * Vector3.right;
                    float len = steps * run;
                    Vector2 mid = start + n * (len * 0.5f);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var c3 = new Vector3(mid.x, (topY + 0.6f + footY - 1f) * 0.5f, mid.y) + right * s * 1.35f;
                        var size = new Vector3(0.3f, topY + 0.6f - (footY - 1f), len + 0.2f);
                        TexBox(stoneMb, c3, size, rot, new Rect(0f, 0f, size.z / 2.5f, size.y / 2.5f));
                        col.Box(c3, size, rot, Vector2.zero);
                    }
                    Debug.Log($"[Nainital] DSA road stairs: {steps} steps, {drop:F2} m");
                }
            }
            // the small park between the levels: a low iron fence round it
            var park = Resample(DsaSmallPark, 2f, true);
            for (int i = 0; i < park.Count; i++)
            {
                Vector2 a = park[i], b = park[(i + 1) % park.Count];
                if (Clearance(ctx).Blocked((a + b) * 0.5f, 0.2f)) continue;
                Vector3 a3 = V(a, ctx.H(a)), b3 = V(b, ctx.H(b));
                mb.Limb(a3, a3 + Vector3.up * 0.9f, 0.025f, 0.025f, 4, iron, false);
                mb.Limb(a3 + Vector3.up * 0.85f, b3 + Vector3.up * 0.85f, 0.02f, 0.02f, 4, iron, false);
                mb.Limb(a3 + Vector3.up * 0.4f, b3 + Vector3.up * 0.4f, 0.02f, 0.02f, 4, iron, false);
            }
            // the stone wall's mesh: the photo material (double-sided), boxes UV'd in metres
            ctx.MeshObject("DSA road retaining wall", parent, stoneMb, mossMat ? mossMat : ctx.mats.atlas, null, true);
            ctx.MeshObject("DSA road railing, ledge and stairs", parent, mb, ctx.mats.atlas, col, true);
            Debug.Log($"[Nainital] DSA road: wall and railing in {pieces} pieces, lower level {dsaLowY:F1}");
        }
    }
}
