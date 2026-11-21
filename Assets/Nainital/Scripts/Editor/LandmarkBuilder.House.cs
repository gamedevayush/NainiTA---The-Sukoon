using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// The player's house on the Zoo Road (owner, 21 Nov 2026, FREEROAM #6: "one house on the zoo road with a really good
    /// interior - pahadi wooden style, fireplace, lake-view balcony, drawing room, bedroom, kitchen; sleep and save there;
    /// everything bought in the shops goes to the house"). A Kumaoni house on the valley side of the road, about a third
    /// of the way from Tallital to the zoo: a dry-stone plinth down the slope, timber walls over a stone base, carved dark
    /// window frames, a red tin gable roof with a stone chimney, and a timber balcony on the lake side. Inside, walkable at
    /// the player's scale: the drawing room (sofa set, rug, low table, shelves, a stone FIREPLACE with a real fire), the
    /// kitchen (counter, stove, gas cylinder, utensils) and the bedroom (a double bed - E to sleep and save - and the almari:
    /// E for everything bought and the outfits). Runtime parts: Runtime/World/House.cs.
    /// PlanHouse (before the Buildings step) keeps the footprint clear, so the one house of the data standing there is
    /// replaced by this one (the count of houses stays the same).
    /// </summary>
    public static partial class LandmarkBuilder
    {
        const float HsW = 12f, HsD = 9f, HsWall = 3.0f, HsBalc = 2.6f;
        static bool hsPlanned;
        static Vector2 hsO, hsAlong, hsAway;   // the front wall's middle at the road, along the road, away from it (towards the valley)

        internal static void PlanHouse(WorldCtx ctx)
        {
            hsPlanned = false;
            RoadData zoo = null;
            foreach (var r in ctx.world.roads) if (r.name == "Zoo Road Nainital") { zoo = r; break; }
            if (zoo == null) { Debug.LogWarning("[Nainital] house: no Zoo Road"); return; }
            var line = Geo.ToPoints(zoo.pts);
            float total = 0f;
            for (int i = 1; i < line.Length; i++) total += Vector2.Distance(line[i - 1], line[i]);
            // the first free stretch from a third of the way along, on the valley (lake) side
            foreach (float frac in new[] { 0.30f, 0.26f, 0.34f, 0.22f, 0.38f, 0.42f, 0.18f, 0.46f, 0.5f })
            {
                Vector2 p = AlongLine(line, total * frac, out Vector2 tan);
                Vector2 n = new Vector2(-tan.y, tan.x);
                if (ctx.H(p + n * 12f) > ctx.H(p - n * 12f)) n = -n;   // downhill = the view
                Vector2 o = p + n * (zoo.width * 0.5f + 1.2f);
                if (HouseBlockedByRoads(ctx, o, tan, n, zoo)) continue;
                hsO = o; hsAlong = tan; hsAway = n;
                hsPlanned = true;
                ctx.keepClear.Add((o + n * (HsD * 0.5f + 0.6f), Mathf.Max(HsW, HsD) * 0.5f + 1.5f));
                Debug.Log($"[Nainital] house planned on Zoo Road at {frac:P0}: front ({o.x:F1}, {o.y:F1}), the balcony facing ({n.x:F2}, {n.y:F2})");
                return;
            }
            Debug.LogWarning("[Nainital] house: no free stretch of Zoo Road");
        }

        static Vector2 AlongLine(Vector2[] line, float dist, out Vector2 tan)
        {
            for (int i = 1; i < line.Length; i++)
            {
                float seg = Vector2.Distance(line[i - 1], line[i]);
                if (dist <= seg || i == line.Length - 1)
                {
                    tan = (line[i] - line[i - 1]).normalized;
                    return Vector2.Lerp(line[i - 1], line[i], Mathf.Clamp01(dist / Mathf.Max(seg, 0.01f)));
                }
                dist -= seg;
            }
            tan = Vector2.right;
            return line[0];
        }

        static bool HouseBlockedByRoads(WorldCtx ctx, Vector2 o, Vector2 along, Vector2 away, RoadData own)
        {
            for (float x = -HsW * 0.5f - 0.5f; x <= HsW * 0.5f + 0.5f; x += 1.5f)
                for (float z = 0.3f; z <= HsD + HsBalc + 0.5f; z += 1.5f)
                {
                    Vector2 p = o + along * x + away * z;
                    if (ctx.InLake(p)) return true;
                    foreach (var r in ctx.world.roads)
                    {
                        if (r == own || r.kind == "track") continue;
                        var l = Geo.ToPoints(r.pts);
                        if (l.Length < 2) continue;
                        if (Geo.DistanceToPolyline(l, p, false) < r.width * 0.5f + 0.5f) return true;
                    }
                }
            return false;
        }

        internal static void BuildHouse(WorldCtx ctx, Transform parent)
        {
            if (!hsPlanned) return;
            Vector2 o = hsO, along = hsAlong, away = hsAway;
            if (along.x * away.y - along.y * away.x < 0f) along = -along;   // along = the frame's right (Cross(up, away)), so the layout is not mirrored
            float road = ctx.H(o - away * 1.0f);
            float y0 = road + 0.25f;   // the floor: one step up from the road
            Vector3 A = new Vector3(along.x, 0f, along.y), Z = new Vector3(away.x, 0f, away.y);
            var q = Quaternion.LookRotation(Z);
            Vector3 P(float x, float y, float z) => new Vector3(o.x, y0, o.y) + A * x + Vector3.up * y + Z * z;

            // the ground: never into the rooms (cut below the floor), the yard by the road level with it
            float lowest = float.MaxValue;
            for (float x = -HsW * 0.5f; x <= HsW * 0.5f; x += 1f)
                for (float z = 0f; z <= HsD + HsBalc; z += 1f)
                    lowest = Mathf.Min(lowest, ctx.H(o + along * x + away * z));
            Vector2 c2 = o + away * ((HsD + HsBalc) * 0.5f);
            float R = Mathf.Sqrt(HsW * HsW + (HsD + HsBalc) * (HsD + HsBalc)) * 0.5f + 5f;
            ShapeTerrain(ctx, c2 - Vector2.one * R, c2 + Vector2.one * R, (p, y) =>
            {
                Vector2 d = p - o;
                float lx = Mathf.Abs(Vector2.Dot(d, along)), lz = Vector2.Dot(d, away);
                if (lz < -0.5f) return y;                                                       // the road
                bool under = lx <= HsW * 0.5f + 0.3f && lz <= HsD + HsBalc + 0.3f;
                if (under) return Mathf.Min(y, y0 - 0.35f);                                     // under the house: below the floor
                if (lz < 1.5f && lx <= HsW * 0.5f + 3f) return Mathf.Lerp(road, y, Mathf.Clamp01((lx - HsW * 0.5f) / 3f));   // the yard by the road
                return y;
            });

            var sm = new SiteMeshes();
            var col = sm.col;
            int stone = Pal.StoneGrey, timber = Pal.WoodDark, panel = Pal.WoodLight, plaster = Pal.TempleWhite;
            void Box(Vector3 c, Vector3 s, int pal, bool collide = false) { sm.atlas.Box(c, s, q, S(pal)); if (collide) col.Box(c, s, q, Vector2.zero); }
            void Detail(Vector3 c, Vector3 s, int pal) => sm.loose.Box(c, s, q, S(pal));

            // ---- the stone plinth down the slope and the floor
            float baseBottom = Mathf.Min(lowest, y0) - 0.6f - y0;
            Box(P(0f, (baseBottom - 0.15f) * 0.5f, HsD * 0.5f), new Vector3(HsW, -baseBottom - 0.15f, HsD), stone, true);
            Box(P(0f, -0.075f, HsD * 0.5f), new Vector3(HsW, 0.15f, HsD), panel, true);                           // the floor boards
            Box(P(-3f, -0.12f, -0.5f), new Vector3(1.8f, 0.25f, 1.0f), stone, true);                              // the step

            // ---- walls with their openings: stone to 1 m, timber above; the inside faced with light boards
            void Wall(Vector3 a, Vector3 b, float thick, List<(float c, float w, float bot, float top)> holes, bool outer)
            {
                Vector3 dir = (b - a); float len = dir.magnitude; dir /= len;
                var wq = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up), Vector3.up);
                holes = holes ?? new List<(float, float, float, float)>();
                holes.Sort((h1, h2) => h1.c.CompareTo(h2.c));
                float s = 0f;
                void Piece(float s0, float s1, float yb, float yt)
                {
                    if (s1 - s0 < 0.01f || yt - yb < 0.01f) return;
                    Vector3 mid = a + dir * ((s0 + s1) * 0.5f);
                    float h = yt - yb;
                    if (outer)
                    {
                        float stoneTop = Mathf.Min(yt, 1.0f);
                        if (stoneTop > yb) { sm.atlas.Box(mid + Vector3.up * ((yb + stoneTop) * 0.5f), new Vector3(s1 - s0, stoneTop - yb, thick), wq, S(stone)); }
                        if (yt > 1.0f) { float b0 = Mathf.Max(yb, 1.0f); sm.atlas.Box(mid + Vector3.up * ((b0 + yt) * 0.5f), new Vector3(s1 - s0, yt - b0, thick), wq, S(timber)); }
                        sm.atlas.Box(mid + Vector3.up * ((yb + yt) * 0.5f) + wq * new Vector3(0f, 0f, thick * 0.5f + 0.01f), new Vector3(s1 - s0, h, 0.02f), wq, S(panel));   // inside boards
                    }
                    else sm.atlas.Box(mid + Vector3.up * ((yb + yt) * 0.5f), new Vector3(s1 - s0, h, thick), wq, S(plaster));
                    col.Box(mid + Vector3.up * ((yb + yt) * 0.5f), new Vector3(s1 - s0, h, thick), wq, Vector2.zero);
                }
                foreach (var hle in holes)
                {
                    float h0 = hle.c - hle.w * 0.5f, h1 = hle.c + hle.w * 0.5f;
                    Piece(s, h0, 0f, HsWall);
                    Piece(h0, h1, 0f, hle.bot);
                    Piece(h0, h1, hle.top, HsWall);
                    // the carved frame round the opening
                    Vector3 fc = a + dir * hle.c;
                    sm.loose.Box(fc + Vector3.up * (hle.top + 0.06f), new Vector3(hle.w + 0.3f, 0.12f, thick + 0.08f), wq, S(timber));
                    if (hle.bot > 0.05f) sm.loose.Box(fc + Vector3.up * (hle.bot - 0.05f), new Vector3(hle.w + 0.3f, 0.1f, thick + 0.12f), wq, S(timber));
                    foreach (float side in new[] { -1f, 1f })
                        sm.loose.Box(fc + dir * (side * (hle.w * 0.5f + 0.06f)) + Vector3.up * ((hle.bot + hle.top) * 0.5f), new Vector3(0.12f, hle.top - hle.bot + 0.1f, thick + 0.06f), wq, S(timber));
                    if (hle.bot > 0.05f)   // a window: glass and the cross bars
                    {
                        sm.loose.Box(fc + Vector3.up * ((hle.bot + hle.top) * 0.5f), new Vector3(hle.w, hle.top - hle.bot, 0.03f), wq, S(Pal.IronBlack));
                        sm.loose.Box(fc + Vector3.up * ((hle.bot + hle.top) * 0.5f), new Vector3(0.05f, hle.top - hle.bot, 0.06f), wq, S(timber));
                        sm.loose.Box(fc + Vector3.up * ((hle.bot + hle.top) * 0.5f), new Vector3(hle.w, 0.05f, 0.06f), wq, S(timber));
                    }
                    s = h1;
                }
                Piece(s, len, 0f, HsWall);
            }
            float hw = HsW * 0.5f, t = 0.28f;
            // front (road side): the door into the drawing room, a kitchen window
            Wall(P(-hw, 0f, 0f), P(hw, 0f, 0f), t, new List<(float, float, float, float)> { (3f, 1.1f, 0f, 2.15f), (9.6f, 1.4f, 0.95f, 2.15f) }, true);
            // back (the valley): the balcony door, the drawing room's and the bedroom's windows
            Wall(P(hw, 0f, HsD), P(-hw, 0f, HsD), t, new List<(float, float, float, float)> { (2.2f, 2.0f, 0.95f, 2.35f), (9f, 1.6f, 0f, 2.2f), (5.4f, 1.6f, 0.9f, 2.3f) }, true);
            // left: the fireplace's wall (a window beside it); right: kitchen and bedroom windows
            Wall(P(-hw, 0f, HsD), P(-hw, 0f, 0f), t, new List<(float, float, float, float)> { (2.3f, 1.2f, 1.0f, 2.2f) }, true);
            Wall(P(hw, 0f, 0f), P(hw, 0f, HsD), t, new List<(float, float, float, float)> { (2.2f, 1.2f, 1.0f, 2.2f), (6.8f, 1.3f, 1.0f, 2.2f) }, true);
            // inside: the drawing room | kitchen + bedroom partition (two doorways), kitchen | bedroom
            Wall(P(1.5f, 0f, 0f), P(1.5f, 0f, HsD), 0.12f, new List<(float, float, float, float)> { (2.3f, 1.0f, 0f, 2.1f), (6.6f, 1.0f, 0f, 2.1f) }, false);
            Wall(P(1.5f, 0f, 4.3f), P(hw, 0f, 4.3f), 0.12f, null, false);
            Box(P(0f, HsWall + 0.04f, HsD * 0.5f), new Vector3(HsW, 0.08f, HsD), panel);                           // the ceiling
            // beams under the ceiling
            for (float bz = 1.2f; bz < HsD; bz += 1.6f) Detail(P(0f, HsWall - 0.08f, bz), new Vector3(HsW - 0.3f, 0.14f, 0.14f), timber);

            // ---- the roof: red tin, a gable along the road, with the eaves out over the walls; the gables boarded
            float rise = 2.2f, half = HsD * 0.5f + 0.7f, slope = Mathf.Sqrt(half * half + rise * rise);
            float pitch = Mathf.Atan2(rise, half) * Mathf.Rad2Deg;
            foreach (float side in new[] { -1f, 1f })
            {
                var rq = q * Quaternion.Euler(side * pitch, 0f, 0f);
                Vector3 c = P(0f, HsWall + rise * 0.5f + 0.1f, HsD * 0.5f + side * (half * 0.5f));   // side +1: the valley half, sloping down away from the ridge
                sm.atlas.Box(c, new Vector3(HsW + 1.2f, 0.07f, slope), rq, S(Pal.BoatRed));
                for (float rx = -hw; rx <= hw; rx += 0.55f)   // the tin's ribs
                    sm.loose.Box(c + q * new Vector3(rx, 0.05f, 0f), new Vector3(0.04f, 0.03f, slope), rq, S(Pal.TempleRed));
            }
            sm.loose.Box(P(0f, HsWall + rise + 0.12f, HsD * 0.5f), new Vector3(HsW + 1.3f, 0.12f, 0.2f), q, S(Pal.TempleRed));   // the ridge
            foreach (float gx in new[] { -hw, hw })
                for (int i = 0; i < 6; i++)   // the boarded gable, stepped
                {
                    float f = (i + 0.5f) / 6f, hgt = rise * (1f - f);
                    float zz = HsD * 0.5f;
                    sm.atlas.Box(P(gx, HsWall + rise * f * 0.5f + 0.05f, zz), new Vector3(0.12f, rise * f + 0.02f, (HsD) * (1f - f) + 0.1f), q, S(timber));
                }
            // the chimney over the fireplace
            Box(P(-hw - 0.05f, HsWall + rise * 0.5f + 0.9f, 3.0f), new Vector3(0.8f, rise + 1.8f, 0.8f), stone, true);
            Detail(P(-hw - 0.05f, HsWall + rise + 1.85f, 3.0f), new Vector3(0.95f, 0.12f, 0.95f), stone);

            // ---- the balcony on the valley side
            float bz0 = HsD, bz1 = HsD + HsBalc;
            Box(P(0f, -0.08f, (bz0 + bz1) * 0.5f), new Vector3(HsW, 0.16f, HsBalc), panel, true);
            foreach (float px in new[] { -hw + 0.2f, -2f, 2f, hw - 0.2f })
            {
                float down = -baseBottom;
                Box(P(px, -down * 0.5f - 0.1f, bz1 - 0.2f), new Vector3(0.22f, down, 0.22f), timber, true);   // the posts down the slope
            }
            for (float rx = -hw + 0.1f; rx <= hw - 0.05f; rx += 0.18f)   // the balusters
                Detail(P(rx, 0.5f, bz1 - 0.05f), new Vector3(0.04f, 0.9f, 0.04f), panel);
            foreach (float side in new[] { -1f, 1f })
                for (float rz = bz0 + 0.1f; rz <= bz1; rz += 0.18f)
                    Detail(P(side * (hw - 0.05f), 0.5f, rz), new Vector3(0.04f, 0.9f, 0.04f), panel);
            Box(P(0f, 1.0f, bz1 - 0.05f), new Vector3(HsW, 0.08f, 0.12f), timber, true);                               // the rail
            Box(P(-hw + 0.05f, 1.0f, (bz0 + bz1) * 0.5f), new Vector3(0.12f, 0.08f, HsBalc), timber, true);
            Box(P(hw - 0.05f, 1.0f, (bz0 + bz1) * 0.5f), new Vector3(0.12f, 0.08f, HsBalc), timber, true);
            col.Box(P(0f, 0.6f, bz1 - 0.05f), new Vector3(HsW, 1.2f, 0.12f), q, Vector2.zero);
            Chair(sm, P(-4.2f, 0f, bz0 + 1.3f), q * Quaternion.Euler(0f, 160f, 0f));
            Chair(sm, P(-2.4f, 0f, bz0 + 1.3f), q * Quaternion.Euler(0f, 200f, 0f));
            sm.atlas.Cylinder(P(-3.3f, 0f, bz0 + 1.7f), 0.05f, 0.05f, 0.6f, 6, S(timber), true);
            sm.atlas.Cylinder(P(-3.3f, 0.6f, bz0 + 1.7f), 0.35f, 0.35f, 0.04f, 12, S(panel), true);

            // ---- the drawing room: rug, sofa set round a low table, shelves, the fireplace on the left wall
            Detail(P(-2.6f, 0.012f, 4.4f), new Vector3(3.2f, 0.02f, 2.4f), Pal.TempleRed);
            Detail(P(-2.6f, 0.02f, 4.4f), new Vector3(2.8f, 0.02f, 2.0f), Pal.Saffron);
            Sofa(sm, col, P(-0.6f, 0f, 4.4f), q * Quaternion.Euler(0f, -90f, 0f), 2.2f, Pal.JacketGreen);              // facing the fireplace
            Sofa(sm, col, P(-2.6f, 0f, 6.4f), q * Quaternion.Euler(0f, 180f, 0f), 1.2f, Pal.JacketGreen);
            Sofa(sm, col, P(-2.6f, 0f, 2.3f), q, 1.2f, Pal.JacketGreen);
            Box(P(-2.6f, 0.22f, 4.4f), new Vector3(1.1f, 0.06f, 0.7f), timber, true);
            foreach (var lx in new[] { -0.5f, 0.5f }) foreach (var lz in new[] { -0.3f, 0.3f }) Detail(P(-2.6f + lx, 0.1f, 4.4f + lz), new Vector3(0.06f, 0.2f, 0.06f), timber);
            Detail(P(-2.75f, 0.28f, 4.35f), new Vector3(0.2f, 0.06f, 0.14f), Pal.SareeOrange);                           // a book on it
            // the shelves on the front wall
            for (int sh = 0; sh < 3; sh++)
            {
                Detail(P(-4.6f, 0.9f + sh * 0.5f, 0.32f), new Vector3(1.8f, 0.05f, 0.3f), timber);
                for (int b = 0; b < 7; b++) Detail(P(-5.35f + b * 0.24f, 1.05f + sh * 0.5f, 0.34f), new Vector3(0.12f, 0.26f, 0.22f), new[] { Pal.BoatRed, Pal.BoatBlue, Pal.BoatYellow, Pal.JacketGreen, Pal.SareePink }[(b + sh) % 5]);
            }
            // the fireplace: a stone breast on the left wall, the hearth, the mantel with a clock and photos
            Vector3 fp = P(-hw + 0.5f, 0f, 3.0f);
            Box(fp + Vector3.up * 0.75f, new Vector3(1.0f, 1.5f, 1.8f), stone, true);
            Detail(fp + Vector3.up * 0.45f + A * 0.51f, new Vector3(0.04f, 0.8f, 1.0f), Pal.IronBlack);                      // the opening (soot)
            Detail(fp + Vector3.up * 0.04f + A * 0.8f, new Vector3(0.6f, 0.08f, 1.9f), stone);                              // the hearth
            Detail(fp + Vector3.up * 1.55f + A * 0.1f, new Vector3(1.3f, 0.1f, 2.1f), timber);                              // the mantel
            Detail(fp + Vector3.up * 1.72f + A * 0.1f, new Vector3(0.12f, 0.24f, 0.2f), Pal.Gold);
            Detail(fp + Vector3.up * 1.75f + A * 0.1f + Z * 0.6f, new Vector3(0.04f, 0.28f, 0.22f), Pal.ShirtWhite);
            Detail(fp + Vector3.up * 1.75f + A * 0.1f - Z * 0.6f, new Vector3(0.04f, 0.28f, 0.22f), Pal.ShirtWhite);
            Box(fp + Vector3.up * 2.3f, new Vector3(0.8f, 1.4f, 1.1f), stone);                                              // the breast up to the ceiling
            for (int lg = 0; lg < 3; lg++) Detail(fp + Vector3.up * 0.18f + A * 0.3f + Z * (-0.25f + lg * 0.25f), new Vector3(0.6f, 0.12f, 0.12f), Pal.WoodDark);   // logs

            // ---- the kitchen: a counter along the right wall, the stove, a gas cylinder, utensils, a small table
            Box(P(hw - 0.45f, 0.45f, 2.2f), new Vector3(0.7f, 0.9f, 3.6f), panel, true);
            Detail(P(hw - 0.45f, 0.92f, 2.2f), new Vector3(0.74f, 0.05f, 3.7f), Pal.StoneGrey);
            Detail(P(hw - 0.45f, 0.97f, 1.4f), new Vector3(0.5f, 0.05f, 0.7f), Pal.IronBlack);                              // the stove
            foreach (float bzz in new[] { 1.22f, 1.58f }) sm.loose.Cylinder(P(hw - 0.45f, 1.0f, bzz), 0.09f, 0.09f, 0.02f, 10, Atlas.ExtraUV(Px.Chrome), true);
            sm.loose.Cylinder(P(hw - 0.45f, 1.02f, 1.22f), 0.13f, 0.11f, 0.16f, 12, S(Pal.IronBlack), true);                // a pressure cooker
            sm.atlas.Cylinder(P(hw - 0.4f, 0f, 0.6f), 0.16f, 0.16f, 0.6f, 12, S(Pal.BoatRed), true);                        // the gas cylinder
            for (int u = 0; u < 5; u++) sm.loose.Cylinder(P(hw - 0.6f, 0.97f, 2.6f + u * 0.22f), 0.07f, 0.06f, 0.12f + 0.04f * (u % 2), 8, S(u % 2 == 0 ? Pal.Gold : Pal.ShirtWhite), true);
            Detail(P(hw - 0.15f, 1.6f, 2.2f), new Vector3(0.3f, 0.05f, 2.6f), timber);                                     // the shelf over it
            for (int j = 0; j < 8; j++) sm.loose.Cylinder(P(hw - 0.15f, 1.63f, 1.1f + j * 0.3f), 0.06f, 0.06f, 0.18f, 8, S(new[] { Pal.Saffron, Pal.JacketYellow, Pal.BoatRed, Pal.Grass }[j % 4]), true);
            Box(P(3.4f, 0.38f, 2.0f), new Vector3(0.9f, 0.05f, 0.9f), timber, true);
            foreach (var lx in new[] { -0.38f, 0.38f }) foreach (var lz in new[] { -0.38f, 0.38f }) Detail(P(3.4f + lx, 0.18f, 2.0f + lz), new Vector3(0.05f, 0.36f, 0.05f), timber);

            // ---- the bedroom: the double bed (sleep), a side table with a lamp, the almari (storage, clothes)
            Vector3 bed = P(4.2f, 0f, 7.6f);
            Box(bed + Vector3.up * 0.22f, new Vector3(1.7f, 0.44f, 2.1f), timber, true);
            Detail(bed + Vector3.up * 0.5f, new Vector3(1.6f, 0.14f, 2.0f), Pal.ShirtWhite);
            Detail(bed + Vector3.up * 0.6f - Z * 0.25f, new Vector3(1.62f, 0.08f, 1.4f), Pal.SareePink);                    // the quilt
            Detail(bed + Vector3.up * 0.62f + Z * 0.75f + A * 0.4f, new Vector3(0.6f, 0.12f, 0.35f), Pal.ShirtWhite);
            Detail(bed + Vector3.up * 0.62f + Z * 0.75f - A * 0.4f, new Vector3(0.6f, 0.12f, 0.35f), Pal.ShirtWhite);
            Detail(bed + Vector3.up * 0.75f + Z * 1.02f, new Vector3(1.75f, 0.9f, 0.08f), timber);                         // the headboard
            Box(P(5.4f, 0.3f, 8.5f), new Vector3(0.45f, 0.6f, 0.45f), timber);
            sm.loose.Cylinder(P(5.4f, 0.6f, 8.5f), 0.06f, 0.08f, 0.3f, 8, S(Pal.Gold), true);
            sm.loose.Cylinder(P(5.4f, 0.9f, 8.5f), 0.16f, 0.1f, 0.18f, 10, S(Pal.LampBulb), true);
            Vector3 almari = P(2.2f, 0f, 8.55f);
            Box(almari + Vector3.up * 1.0f, new Vector3(1.2f, 2.0f, 0.6f), timber, true);
            Detail(almari + Vector3.up * 1.0f - Z * 0.31f, new Vector3(0.02f, 1.9f, 0.02f), Pal.IronBlack);
            foreach (float hx in new[] { -0.08f, 0.08f }) Detail(almari + Vector3.up * 1.0f - Z * 0.32f + A * hx, new Vector3(0.03f, 0.18f, 0.03f), Pal.Gold);
            Detail(P(4.2f, 0.012f, 6.0f), new Vector3(2.0f, 0.02f, 1.2f), Pal.BoatBlue);                                    // a rug by the bed

            string title = "Apna Ghar";
            sm.Emit(ctx, title, parent);

            // ---- the runtime parts: the fire and the lamps, the bed, the almari (Runtime/World/House.cs)
            var root = new GameObject(title);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(P(0f, 0f, HsD * 0.5f), q);
            var house = root.AddComponent<House>();
            house.fireAt = fp + Vector3.up * 0.3f + A * 0.35f;
            var lamps = new List<Light>();
            foreach (var lp in new[] { P(-2.6f, HsWall - 0.4f, 4.4f), P(4.0f, HsWall - 0.4f, 6.8f), P(3.6f, HsWall - 0.4f, 2.0f) })
            {
                var lg = new GameObject("Lamp").AddComponent<Light>();
                lg.transform.SetParent(root.transform, true);
                lg.transform.position = lp;
                lg.type = LightType.Point;
                lg.range = 7f;
                lg.intensity = 2.2f;
                lg.color = new Color(1f, 0.82f, 0.6f);
                lg.shadows = LightShadows.None;
                lg.enabled = false;
                lamps.Add(lg);
                sm.loose.Sphere(lp + Vector3.up * 0.05f, new Vector3(0.12f, 0.12f, 0.12f), 8, 4, S(Pal.LampBulb));
            }
            house.lamps = lamps.ToArray();
            var bedGo = new GameObject("Bed");
            bedGo.transform.SetParent(root.transform, true);
            bedGo.transform.SetPositionAndRotation(bed + Vector3.up * 0.3f, q);
            bedGo.AddComponent<HouseBed>().radius = 1.8f;
            var alGo = new GameObject("Almari");
            alGo.transform.SetParent(root.transform, true);
            alGo.transform.SetPositionAndRotation(almari + Vector3.up * 0.9f - Z * 0.5f, q);
            alGo.AddComponent<HouseStorage>().radius = 1.6f;
            ctx.Zone(title, P(0f, 0f, HsD * 0.5f), 16f);
            ctx.landmarkSites[title] = P(0f, 0f, HsD * 0.5f);
            Debug.Log($"[Nainital] house built on Zoo Road: floor {y0:F1}, plinth down to {y0 + baseBottom:F1}");
        }

        static void Chair(SiteMeshes sm, Vector3 at, Quaternion cq)
        {
            sm.atlas.Box(at + Vector3.up * 0.45f, new Vector3(0.48f, 0.06f, 0.48f), cq, S(Pal.WoodLight));
            sm.atlas.Box(at + Vector3.up * 0.75f + cq * new Vector3(0f, 0f, -0.22f), new Vector3(0.48f, 0.55f, 0.05f), cq, S(Pal.WoodLight));
            foreach (var lx in new[] { -0.2f, 0.2f }) foreach (var lz in new[] { -0.2f, 0.2f })
                sm.atlas.Box(at + cq * new Vector3(lx, 0.22f, lz), new Vector3(0.04f, 0.44f, 0.04f), cq, S(Pal.WoodDark));
        }

        /// <summary>A sofa facing its +z: seat, back, arms, cushions; len = its length.</summary>
        static void Sofa(SiteMeshes sm, MeshBuilder col, Vector3 at, Quaternion sq, float len, int fabric)
        {
            sm.atlas.Box(at + sq * new Vector3(0f, 0.22f, 0f), new Vector3(len, 0.44f, 0.85f), sq, S(Pal.WoodDark));
            sm.atlas.Box(at + sq * new Vector3(0f, 0.5f, 0.05f), new Vector3(len - 0.1f, 0.14f, 0.75f), sq, S(fabric));
            sm.atlas.Box(at + sq * new Vector3(0f, 0.75f, -0.36f), new Vector3(len, 0.6f, 0.16f), sq, S(fabric));
            foreach (float s in new[] { -1f, 1f }) sm.atlas.Box(at + sq * new Vector3(s * (len * 0.5f - 0.06f), 0.62f, 0f), new Vector3(0.14f, 0.3f, 0.85f), sq, S(fabric));
            for (float c = -len * 0.5f + 0.4f; c < len * 0.5f - 0.2f; c += 0.6f)
                sm.loose.Box(at + sq * new Vector3(c, 0.78f, -0.22f), new Vector3(0.4f, 0.36f, 0.12f), sq, S(Pal.Saffron));
            col.Box(at + sq * new Vector3(0f, 0.4f, 0f), new Vector3(len, 0.8f, 0.85f), sq, Vector2.zero);
        }
    }
}
