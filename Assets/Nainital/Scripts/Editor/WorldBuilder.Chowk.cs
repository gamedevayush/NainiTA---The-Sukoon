using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// The chowk at Naina Devi's gate, where DSA road ends (owner, 23 Nov 2026, with three Street View 360s: "ye poora flat
    /// hai..even aage do road jaari hai usme pehle waali bhi isilevel pe hai..uske upar waali upar hai...beech me stair...
    /// road ki bhari hui hai"). One flat paved square from the Tibetan market's stalls to the temple's gate, grey pavers with
    /// red bands round its edge and round cobble medallions; where the ground beyond its edge stands higher or lower, a stone
    /// wall; where a road or path leaves it for a higher level, stairs up it; shop rows filling its free edges.
    /// The outline is the paved area on the satellite photo (Tools/satcheck/capcrop.py -80 -140 70).
    ///  ShapeChowkGround (before the roads): the square at the gate's level, easing into the ground over 5 m outside.
    ///  BuildChowk (with Mallital, before the road mask is finished): the paving painted into the ground, the edge walls and
    ///  stairs, the shop rows.
    /// </summary>
    public static partial class WorldBuilder
    {
        internal static readonly Vector2[] Chowk =
        {
            new Vector2(-69f, -106f), new Vector2(-64f, -117f), new Vector2(-66f, -130f), new Vector2(-70f, -143f), new Vector2(-78f, -152f),
            new Vector2(-90f, -158f), new Vector2(-99f, -150f), new Vector2(-98f, -136f), new Vector2(-90f, -122f), new Vector2(-80f, -110f),
        };
        static readonly Vector2 ChowkGate = new Vector2(-77f, -143f);
        static float chowkY = float.NaN;
        const float ChowkEase = 5f;

        static void ShapeChowkGround(WorldCtx ctx)
        {
            chowkY = ctx.H(ChowkGate);
            var td = ctx.terrain.terrainData;
            int res = td.heightmapResolution;
            Vector3 tp = ctx.terrain.transform.position, sz = td.size;
            float cx = (res - 1) / sz.x, cz = (res - 1) / sz.z;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((-108f - tp.x) * cx), 0, res - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((-55f - tp.x) * cx), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((-167f - tp.z) * cz), 0, res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((-97f - tp.z) * cz), 0, res - 1);
            int w = x1 - x0 + 1, h = z1 - z0 + 1;
            var hs = td.GetHeights(x0, z0, w, h);
            int inside = 0, eased = 0;
            for (int zi = 0; zi < h; zi++)
                for (int xi = 0; xi < w; xi++)
                {
                    var q = new Vector2(tp.x + (x0 + xi) / cx, tp.z + (z0 + zi) / cz);
                    if (ctx.InLake(q)) continue;
                    float cur = tp.y + hs[zi, xi] * sz.y, want;
                    if (Geo.PointInPolygon(Chowk, q)) { want = chowkY; inside++; }
                    else
                    {
                        float d = Geo.DistanceToPolyline(Chowk, q, true);
                        if (d > ChowkEase) continue;
                        // just outside: a wall stands on the edge (BuildChowk); the ground eases back to its own level beyond it
                        want = Mathf.Lerp(chowkY, cur, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 1.2f) / (ChowkEase - 1.2f))));
                        eased++;
                    }
                    hs[zi, xi] = Mathf.Clamp01((want - tp.y) / sz.y);
                }
            td.SetHeights(x0, z0, hs);
            Debug.Log($"[Nainital] Naina Devi chowk: flat at {chowkY:F2} ({inside} samples, {eased} eased round it)");
        }

        static void BuildChowk(WorldCtx ctx)
        {
            if (float.IsNaN(chowkY)) return;
            var parent = new GameObject("Naina Devi chowk").transform;
            parent.SetParent(ctx.worldRoot, false);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 1. the paving: grey pavers over the square, red bands 1.2 m inside its edge, round red medallions
            var grey = new MeshBuilder();
            var red = new MeshBuilder();
            Vector2 c = Vector2.zero;
            foreach (var p in Chowk) c += p;
            c /= Chowk.Length;
            for (int i = 0; i < Chowk.Length; i++)
            {
                Vector2 a = Chowk[i], b = Chowk[(i + 1) % Chowk.Length];
                grey.Tri(V(c, chowkY), V(b, chowkY), V(a, chowkY), Vector2.zero, Vector2.zero, Vector2.zero, Vector3.up);
                Vector2 ai = a + (c - a).normalized * 1.4f, bi = b + (c - b).normalized * 1.4f;
                red.Quad(V(a, chowkY), V(b, chowkY), V(bi, chowkY), V(ai, chowkY), Vector2.zero, Vector3.up);
            }
            foreach (var m in new[] { c, Vector2.Lerp(c, ChowkGate, 0.6f), Vector2.Lerp(c, new Vector2(-84f, -118f), 0.6f) })
            {
                const int seg = 20;
                for (int k = 0; k < seg; k++)
                {
                    float a0 = k * Mathf.PI * 2f / seg, a1 = (k + 1) * Mathf.PI * 2f / seg;
                    Vector2 o0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)), o1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
                    red.Quad(V(m + o0 * 2.6f, chowkY), V(m + o1 * 2.6f, chowkY), V(m + o1 * 2.0f, chowkY), V(m + o0 * 2.0f, chowkY), Vector2.zero, Vector3.up);
                    if (k % 2 == 0) red.Tri(V(m, chowkY), V(m + o1 * 1.2f, chowkY), V(m + o0 * 1.2f, chowkY), Vector2.zero, Vector2.zero, Vector2.zero, Vector3.up);
                }
            }
            PaintRoadSurface(grey, ChGreyPaver);
            PaintRoadSurfaceOver(red, ChRedPaver);

            // 2. the edge: a stone wall where the ground beyond is 0.7 m higher or lower; stairs where a path climbs out
            var mossMat = MossyStoneMaterial();
            var stone = new MeshBuilder();
            var mb = new MeshBuilder();
            var col = new MeshBuilder();
            Vector2 cap = Atlas.MaterialUV(Atlas.MatConcrete), iron = Atlas.SolidUV(Pal.IronGreen);
            var edge = Resample(Chowk, 1.5f, true);
            int wallN = 0, stairsN = 0;
            var stairsAt = new List<Vector2>();
            // the paths that leave the square: where one crosses the edge with a step in level, stairs (built below)
            foreach (var r in ctx.world.roads)
            {
                if (r.kind != "footway" && r.kind != "pedestrian" && r.kind != "steps" && r.kind != "path") continue;
                var line = Geo.ToPoints(r.pts);
                for (int i = 0; i + 1 < line.Length; i++)
                {
                    bool aIn = Geo.PointInPolygon(Chowk, line[i]), bIn = Geo.PointInPolygon(Chowk, line[i + 1]);
                    if (aIn == bIn) continue;
                    Vector2 inP = aIn ? line[i] : line[i + 1], outP = aIn ? line[i + 1] : line[i];
                    // the crossing point
                    Vector2 x = inP;
                    for (int k = 0; k < 20; k++) { Vector2 m = (inP + x) * 0.5f; x = Vector2.Lerp(inP, outP, (k + 1) / 20f); if (!Geo.PointInPolygon(Chowk, x)) break; }
                    stairsAt.Add(x);
                }
            }
            for (int i = 0; i < edge.Count; i++)
            {
                Vector2 a = edge[i], b = edge[(i + 1) % edge.Count];
                Vector2 m = (a + b) * 0.5f, t = (b - a).normalized, outN = new Vector2(t.y, -t.x);
                if (Geo.PointInPolygon(Chowk, m + outN * 0.5f)) outN = -outN;
                if (Clearance(ctx).Blocked(m + outN * 1.0f, 0.2f)) continue;   // a road leaves here: open
                bool nearStairs = false;
                foreach (var s in stairsAt) if ((s - m).sqrMagnitude < 2.5f * 2.5f) { nearStairs = true; break; }
                if (nearStairs) continue;
                float outY = ctx.H(m + outN * 3f);
                float diff = outY - chowkY;
                if (Mathf.Abs(diff) < 0.7f) continue;
                float len = Vector2.Distance(a, b) + 0.05f;
                var rot = Quaternion.LookRotation(new Vector3(t.x, 0f, t.y));
                Vector2 wc = m + outN * 0.4f;
                float top = Mathf.Max(chowkY, outY) + (diff > 0f ? 0.1f : 0.6f), bot = Mathf.Min(chowkY, outY) - 0.4f;
                TexBox(stone, new Vector3(wc.x, (top + bot) * 0.5f, wc.y), new Vector3(0.8f, top - bot, len), rot, new Rect(0f, bot / 2.5f, len / 2.5f, (top - bot) / 2.5f));
                col.Box(new Vector3(wc.x, (top + bot) * 0.5f, wc.y), new Vector3(0.8f, top - bot, len), rot, Vector2.zero);
                mb.Box(new Vector3(wc.x, top + 0.05f, wc.y), new Vector3(0.9f, 0.1f, len + 0.04f), rot, cap);
                if (diff < 0f)   // a drop beyond: a railing on the wall
                {
                    Vector3 p0 = new Vector3(a.x + outN.x * 0.4f, top + 0.1f, a.y + outN.y * 0.4f), p1 = new Vector3(b.x + outN.x * 0.4f, top + 0.1f, b.y + outN.y * 0.4f);
                    mb.Limb(p0, p0 + Vector3.up * 0.9f, 0.03f, 0.03f, 4, iron, false);
                    mb.Limb(p0 + Vector3.up * 0.9f, p1 + Vector3.up * 0.9f, 0.025f, 0.025f, 4, iron, false);
                }
                wallN++;
            }
            // stairs at each path that climbs (or drops) out of the square: steps from its edge outward to the ground there
            foreach (var s in stairsAt)
            {
                Vector2 inward = (c - s).normalized, outward = -inward;
                float far = ctx.H(s + outward * 4.5f), drop = far - chowkY;
                if (Mathf.Abs(drop) < 0.5f) continue;
                int steps = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(drop) / 0.17f), 3, 30);
                float rise = drop / steps, run = 0.32f;
                var rot = Quaternion.LookRotation(new Vector3(outward.x, 0f, outward.y));
                float lo = Mathf.Min(chowkY, far) - 0.5f;
                for (int k = 0; k < steps; k++)
                {
                    float tread = chowkY + rise * (k + 1);
                    Vector2 q = s + outward * ((k + 0.5f) * run);
                    float y0 = Mathf.Min(tread, lo), y1 = Mathf.Max(tread, lo);
                    mb.Box(new Vector3(q.x, (y0 + y1) * 0.5f, q.y), new Vector3(3.0f, y1 - y0, run), rot, cap);
                    col.Box(new Vector3(q.x, (y0 + y1) * 0.5f, q.y), new Vector3(3.0f, y1 - y0, run), rot, Vector2.zero);
                }
                Vector3 right = rot * Vector3.right;
                float runLen = steps * run;
                Vector2 mid = s + outward * (runLen * 0.5f);
                float hi = Mathf.Max(chowkY, far) + 0.5f;
                foreach (float sd in new[] { -1f, 1f })
                {
                    var c3 = new Vector3(mid.x, (hi + lo) * 0.5f, mid.y) + right * sd * 1.65f;
                    TexBox(stone, c3, new Vector3(0.3f, hi - lo, runLen + 0.2f), rot, new Rect(0f, 0f, runLen / 2.5f, (hi - lo) / 2.5f));
                    col.Box(c3, new Vector3(0.3f, hi - lo, runLen + 0.2f), rot, Vector2.zero);
                }
                stairsN++;
            }

            // 3. shops round its free edges, a few metres out, facing in: boards, a light each
            var shops = new MeshBuilder();
            var rng = new System.Random(4242);
            string[] names = { "MOMOS & CHOWMEIN", "WOOLLEN HOUSE", "PRASAD BHANDAR", "GIFT CORNER", "CHAI KASH CAFE", "KUMAONI HANDICRAFTS", "BAL MITHAI", "PHOTO STUDIO" };
            var signs = new GameObject("Chowk shop boards").transform;
            signs.SetParent(parent, false);
            int made = 0;
            Physics.SyncTransforms();
            var spots = Resample(Chowk, 5f, true);
            for (int i = 0; i < spots.Count; i++)
            {
                Vector2 a = spots[i], b = spots[(i + 1) % spots.Count];
                Vector2 m = (a + b) * 0.5f, t = (b - a).normalized, outN = new Vector2(t.y, -t.x);
                if (Geo.PointInPolygon(Chowk, m + outN * 0.5f)) outN = -outN;
                Vector2 q = m + outN * 3.2f;
                if (ctx.InLake(q) || Clearance(ctx).Blocked(q, 2.2f) || SolidAt(ctx, q) || ctx.InKeepClear(q, 2f)) continue;
                if (Mathf.Abs(ctx.H(q) - chowkY) > 1.2f) continue;   // the ground there is another level: the wall stands
                float yaw = Mathf.Atan2(-outN.x, -outN.y) * Mathf.Rad2Deg;
                shops.M = Matrix4x4.TRS(new Vector3(q.x, chowkY, q.y), Quaternion.identity, Vector3.one);
                LandmarkBuilder.HillShop(shops, Vector3.zero, yaw, 4.2f, rng);
                shops.M = Matrix4x4.identity;
                Vector3 face = new Vector3(-outN.x, 0f, -outN.y);
                Vector3 board = new Vector3(q.x, chowkY + 2.75f, q.y) + face * 2.3f;
                Label(signs, font, names[made % names.Length], board, face, 0.24f, new Color(1f, 0.95f, 0.75f));
                Vector3 al = Vector3.Cross(Vector3.up, face).normalized;
                Led(board + Vector3.up * 0.32f - al * 1.8f, board + Vector3.up * 0.32f + al * 1.8f, made % 2 == 0 ? LedWarm : LedWhite, 0.1f, 0.045f, 2.4f);
                Accent(board + face * 0.8f - Vector3.up * 0.6f, new Color(1f, 0.88f, 0.65f), 7f, 8f);
                col.Box(new Vector3(q.x, chowkY + 1.4f, q.y), new Vector3(3.6f, 2.8f, 3.6f), Quaternion.Euler(0f, yaw, 0f), Vector2.zero);
                ctx.keepClear.Add((q, 2.5f));
                made++;
            }
            ctx.MeshObject("Chowk walls", parent, stone, mossMat ? mossMat : ctx.mats.atlas, null, true);
            ctx.MeshObject("Chowk stairs and rails", parent, mb, ctx.mats.atlas, col, true);
            ctx.MeshObject("Chowk shops", parent, shops, ctx.mats.atlas, null, true, true);
            Debug.Log($"[Nainital] Naina Devi chowk: paved at {chowkY:F2}, {wallN} wall pieces, {stairsN} stairs, {made} shops");
        }
    }
}
