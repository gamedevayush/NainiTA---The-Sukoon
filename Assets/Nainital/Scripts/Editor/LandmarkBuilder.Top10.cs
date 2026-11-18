using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Nainital's top places from photos (owner, 2026-11-19: "saare points photorealistic chahiye, top 10 nainital places me
    /// jo bhi aaye, uske photos uthaao, par saare banao"). References from Wikimedia Commons (scratchpad refs/): Raj Bhavan
    /// - the Governor's House, a Victorian Gothic house of dark grey rubble stone with cream dressings, crenellated towers
    /// with corner turrets, green roofs, an arched porch, lawns, a gravel drive and palms - rebuilt on its real footprint;
    /// the zoo's gate (a concrete portal with the zoo's name boards, green iron gates, a ticket window) with a bear pit and
    /// a deer paddock behind; and a photo pass on Naina Peak, Tiffin Top and Land's End: flagstone ground, grey rock
    /// outcrops and timber railings instead of the flat-coloured boulders and fences.
    /// </summary>
    public static partial class LandmarkBuilder
    {
        static float peakLift;   // how far PeakDressing raised its ring above the site (Land's End), for the preview camera

        static string DarkStone() => Recoloured("old_stone_wall", "stone_dark", 0.04f, 0.78f, new Color(0.94f, 0.97f, 1.03f), 0.12f);

        /// <summary>Outward unit normals of a polygon's edges (tested against the inside, so the winding does not matter).</summary>
        static Vector2[] EdgeNormals(Vector2[] poly)
        {
            var n = new Vector2[poly.Length];
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length], d = (b - a).normalized, r = new Vector2(d.y, -d.x);
                n[i] = Geo.PointInPolygon(poly, (a + b) * 0.5f + r * 0.3f) ? -r : r;
            }
            return n;
        }

        /// <summary>The polygon moved inwards by `d` (mitred, the mitre capped at 2 d).</summary>
        static Vector2[] Inset(Vector2[] poly, Vector2[] n, float d)
        {
            var o = new Vector2[poly.Length];
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 n0 = n[(i - 1 + poly.Length) % poly.Length], n1 = n[i], m = (n0 + n1).normalized;
                float c = Mathf.Max(0.5f, Vector2.Dot(m, n1));
                o[i] = poly[i] - m * (d / c);
            }
            return o;
        }

        /// <summary>A Gothic window: dark glass, a cream surround and a pointed head, on a wall facing `n`.</summary>
        static void GothicWindow(MeshBuilder mb, Vector3 c, Vector2 n, float w, float h)
        {
            var q = Quaternion.LookRotation(new Vector3(n.x, 0f, n.y));
            Vector3 o = new Vector3(n.x, 0f, n.y);
            mb.Box(c + o * 0.04f, new Vector3(w, h, 0.06f), q, S(Pal.JacketBlack));
            mb.Box(c + o * 0.08f + Vector3.up * (h * 0.5f + 0.06f), new Vector3(w + 0.3f, 0.14f, 0.12f), q, S(Pal.PantsKhaki));
            mb.Box(c + o * 0.08f - Vector3.up * (h * 0.5f + 0.08f), new Vector3(w + 0.4f, 0.16f, 0.18f), q, S(Pal.PantsKhaki));
            foreach (float s in new[] { -1f, 1f })
            {
                mb.Box(c + o * 0.08f + q * new Vector3(s * (w * 0.5f + 0.08f), 0f, 0f), new Vector3(0.14f, h, 0.12f), q, S(Pal.PantsKhaki));
                mb.Box(c + o * 0.08f + q * new Vector3(s * w * 0.27f, h * 0.5f + 0.28f, 0f), new Vector3(w * 0.62f, 0.13f, 0.12f), q * Quaternion.Euler(0f, 0f, -s * 32f), S(Pal.PantsKhaki));
            }
            mb.Box(c + o * 0.07f, new Vector3(0.05f, h, 0.04f), q, S(Pal.ShirtWhite));   // the mullion
        }

        static void Merlons(MeshBuilder stone, Vector3 a, Vector3 b, Vector2 n, float every = 2.2f)
        {
            float len = Vector3.Distance(a, b);
            int k = Mathf.Max(1, Mathf.FloorToInt(len / every));
            var q = Quaternion.LookRotation(new Vector3(n.x, 0f, n.y));
            for (int i = 0; i < k; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, (i + 0.5f) / k);
                TBox(stone, p + Vector3.up * 0.4f, new Vector3(len / k * 0.5f, 0.8f, 0.55f), q, 1.2f);
            }
        }

        static void BuildRajBhavan(WorldCtx ctx, Transform parent)
        {
            Vector2[] poly = null;
            foreach (var b in ctx.world.osmBuildings) if (b.name == "Raj Bhavan") poly = Geo.ToPoints(b.pts);
            var poi = ctx.Poi("Raj Bhavan");
            if (poly == null || !poi.HasValue) return;
            var rng = new System.Random(1900);
            var sm = new SiteMeshes();
            var stone = sm.Photo(DarkStone(), new Color(0.9f, 0.9f, 0.9f), 0.15f);
            var roof = sm.Photo(LightTin(), new Color(0.5f, 0.72f, 0.58f), 0.3f);
            var gravel = sm.Photo("rock_ground_02", new Color(0.9f, 0.88f, 0.84f));
            var n = EdgeNormals(poly);
            float y0 = ctx.H(poi.Value) + 0.2f;
            const float H = 10.5f;
            Vector2 cen = Vector2.zero; foreach (var p in poly) cen += p; cen /= poly.Length;

            // 1. the walls: rubble stone from the ground to the parapet, cream string courses, Gothic windows in two rows,
            // battlements along the top
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                float len = Vector2.Distance(a, b);
                Vector3 at = V3(a, y0 + H), bt = V3(b, y0 + H);
                Vector3 ab = V3(a, Mathf.Min(ctx.H(a), y0) - 0.6f), bb = V3(b, Mathf.Min(ctx.H(b), y0) - 0.6f);
                Vector3 o3 = new Vector3(n[i].x, 0f, n[i].y);
                Face(stone, at, bt, bb, ab, o3, 2.2f);
                var q = Quaternion.LookRotation(o3);
                foreach (float yb in new[] { 4.0f, 7.7f })
                    sm.atlas.Box(V3((a + b) * 0.5f, y0 + yb) + o3 * 0.07f, new Vector3(len + 0.1f, 0.22f, 0.14f), Quaternion.LookRotation(V3(b - a, 0f)), S(Pal.PantsKhaki));
                Merlons(stone, at + o3 * 0.0f, bt, n[i]);
                if (len < 3f) continue;
                int wins = Mathf.Max(1, Mathf.FloorToInt((len - 1.6f) / 3.3f));
                for (int k = 0; k < wins; k++)
                {
                    Vector2 p = Vector2.Lerp(a, b, (k + 0.5f) / wins);
                    GothicWindow(sm.atlas, V3(p, y0 + 2.0f), n[i], 1.1f, 2.0f);
                    GothicWindow(sm.atlas, V3(p, y0 + 5.9f), n[i], 1.1f, 2.0f);
                    if (len > 9f && k % 2 == 1) GothicWindow(sm.atlas, V3(p, y0 + 9.0f), n[i], 0.7f, 1.0f);
                }
            }
            // 2. the roofs: green, rising from inside the battlements to a flat ridge
            var inner = Inset(poly, n, 0.6f);
            var top = Inset(poly, n, 3.4f);
            for (int i = 0; i < poly.Length; i++)
            {
                int j = (i + 1) % poly.Length;
                Vector3 a0 = V3(inner[i], y0 + H), b0 = V3(inner[j], y0 + H), a1 = V3(top[i], y0 + H + 2.6f), b1 = V3(top[j], y0 + H + 2.6f);
                TQuad(roof, a0, b0, b1, a1, 1.0f, V3(n[i], 0f) + Vector3.up);
            }
            var tris = Geo.Triangulate(top);
            for (int i = 0; i + 2 < tris.Length; i += 3)
                roof.Tri(V3(top[tris[i]], y0 + H + 2.6f), V3(top[tris[i + 1]], y0 + H + 2.6f), V3(top[tris[i + 2]], y0 + H + 2.6f),
                         WUV(V3(top[tris[i]], 0f), 1f), WUV(V3(top[tris[i + 1]], 0f), 1f), WUV(V3(top[tris[i + 2]], 0f), 1f), Vector3.up);

            // 3. the towers at the four outermost corners, with corner turrets and battlements; the flag on the tallest
            var corners = new List<int>();
            foreach (var dir in new[] { new Vector2(1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(-1f, -1f) })
            {
                int best = 0; float bd = float.MinValue;
                for (int i = 0; i < poly.Length; i++) { float d = Vector2.Dot(poly[i], dir); if (d > bd) { bd = d; best = i; } }
                if (!corners.Contains(best)) corners.Add(best);
            }
            for (int t = 0; t < corners.Count; t++)
            {
                int i = corners[t];
                Vector2 c = poly[i] - (n[i] + n[(i - 1 + poly.Length) % poly.Length]).normalized * 2.2f;
                float th = t == 0 ? 18.5f : 16f, w = 6.6f;
                var q = Quaternion.LookRotation(V3(n[i], 0f));
                float g = Mathf.Min(ctx.H(c), y0) - 0.6f;
                TBox(stone, new Vector3(c.x, (g + y0 + th) * 0.5f, c.y), new Vector3(w, y0 + th - g, w), q, 2.2f);
                foreach (var f in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                {
                    Vector3 fn = q * f;
                    Vector2 fn2 = new Vector2(fn.x, fn.z);
                    Vector3 fc = new Vector3(c.x, 0f, c.y) + fn * (w * 0.5f);
                    for (int lvl = 0; lvl < 3; lvl++) GothicWindow(sm.atlas, fc + Vector3.up * (y0 + 2.2f + lvl * 4.6f), fn2, 1.0f, 1.9f);
                    Vector3 side = Vector3.Cross(Vector3.up, fn) * (w * 0.5f);
                    Merlons(stone, fc - side + Vector3.up * (y0 + th), fc + side + Vector3.up * (y0 + th), fn2, 1.8f);
                    sm.atlas.Box(fc + fn * 0.07f + Vector3.up * (y0 + th - 1.2f), new Vector3(w + 0.1f, 0.25f, 0.14f), q * Quaternion.LookRotation(f), S(Pal.PantsKhaki));
                }
                foreach (float sx in new[] { -1f, 1f })
                    foreach (float sz in new[] { -1f, 1f })
                    {
                        Vector3 tp = new Vector3(c.x, y0 + th - 3f, c.y) + q * new Vector3(sx * w * 0.5f, 0f, sz * w * 0.5f);
                        sm.atlas.Cylinder(tp, 0.65f, 0.65f, 4.2f, 10, S(Pal.StoneGrey), true);
                        for (int m = 0; m < 6; m++)
                        {
                            float a = m * 60f * Mathf.Deg2Rad;
                            sm.atlas.Box(tp + new Vector3(Mathf.Cos(a) * 0.55f, 4.45f, Mathf.Sin(a) * 0.55f), new Vector3(0.32f, 0.5f, 0.32f), S(Pal.StoneGrey));
                        }
                    }
                if (t == 0)
                {
                    Vector3 fp = new Vector3(c.x, y0 + th, c.y);
                    sm.atlas.Cylinder(fp, 0.06f, 0.05f, 6f, 6, S(Pal.ShirtWhite), true);
                    int[] tri = { Pal.SareeOrange, Pal.ShirtWhite, Pal.BoatGreen };
                    for (int k = 0; k < 3; k++)
                        sm.loose.QuadDouble(fp + new Vector3(0.05f, 5.8f - (k + 1) * 0.4f, 0f), fp + new Vector3(1.85f, 5.8f - (k + 1) * 0.4f, 0f), fp + new Vector3(1.85f, 5.8f - k * 0.4f, 0f), fp + new Vector3(0.05f, 5.8f - k * 0.4f, 0f), new Rect(S(tri[k]), Vector2.zero), Vector3.forward);
                }
            }

            // 4. the porch on the side that faces the town: three pointed arches, battlements and a red railing on top
            int front = 0; float fb = float.MinValue;
            Vector2 toTown = (ctx.lakeCenter - cen).normalized;
            for (int i = 0; i < poly.Length; i++)
            {
                float len = Vector2.Distance(poly[i], poly[(i + 1) % poly.Length]);
                if (len < 12f) continue;
                float sc = Vector2.Dot(n[i], toTown) + len * 0.01f;
                if (sc > fb) { fb = sc; front = i; }
            }
            Vector2 fa = poly[front], fbp = poly[(front + 1) % poly.Length], fm = (fa + fbp) * 0.5f, fd = (fbp - fa).normalized, fo = n[front];
            var pq = Quaternion.LookRotation(V3(fo, 0f));
            Vector3 porch = V3(fm + fo * 3.2f, y0);
            Slab(gravel, stone, fm - fd * 5f + fo * 0.1f, fm + fd * 5f + fo * 0.1f, 6.6f, y0, y0, Mathf.Min(ctx.H(fm + fo * 6f), y0) - 0.5f);
            for (int k = 0; k < 4; k++)
            {
                Vector3 pier = porch + pq * new Vector3(-4.2f + k * 2.8f, 0f, 2.6f);
                TBox(stone, pier + Vector3.up * 2.4f, new Vector3(0.9f, 4.8f, 0.9f), pq, 1.2f);
                if (k < 3)
                    for (int s = 0; s <= 8; s++)
                    {
                        float a = s / 8f * Mathf.PI;
                        Vector3 ap = porch + pq * new Vector3(-2.8f + k * 2.8f + Mathf.Cos(a) * 0.95f, 3.4f + Mathf.Sin(a) * 1.1f, 3.06f);
                        sm.atlas.Box(ap, new Vector3(0.3f, 0.3f, 0.2f), pq, S(Pal.PantsKhaki));
                    }
            }
            TBox(stone, porch + pq * new Vector3(0f, 5.2f, 0.6f), new Vector3(9.4f, 0.8f, 5.2f), pq, 1.6f);
            for (int k = 0; k < 13; k++) sm.atlas.Box(porch + pq * new Vector3(-4.5f + k * 0.75f, 6.0f, 3.1f), new Vector3(0.06f, 0.8f, 0.06f), pq, S(Pal.TempleRed));
            sm.atlas.Box(porch + pq * new Vector3(0f, 6.4f, 3.1f), new Vector3(9.4f, 0.08f, 0.08f), pq, S(Pal.TempleRed));
            sm.atlas.Box(porch + pq * new Vector3(0f, 2.4f, -0.2f), new Vector3(2.0f, 3.2f, 0.1f), pq, S(Pal.WoodDark));   // the door

            // 5. the grounds: the gravel drive round to the porch, lawns, hydrangea beds, palms
            PaintTerrain(ctx, cen - new Vector2(80f, 80f), cen + new Vector2(80f, 80f), (p, slope) =>
            {
                if (Geo.PointInPolygon(poly, p) || slope > 22f) return null;
                float dPorch = Vector2.Distance(p, fm + fo * 12f);
                if (dPorch < 13f && dPorch > 7f) return new Vector4(0.05f, 0f, 0.15f, 0.8f);   // the drive's loop
                if (Geo.DistanceToPolyline(poly, p, true) < 28f) return new Vector4(1f, 0.1f, 0f, 0f);   // the lawns
                return null;
            });
            for (int k = 0; k < 7; k++)
            {
                Vector2 bp = fm + fd * (-9f + k * 3f) + fo * 1.6f;
                if (Mathf.Abs(-9f + k * 3f) < 5.5f) continue;
                Blob(sm.loose, V3(bp, ctx.H(bp) + 0.25f), new Vector3(0.9f, 0.55f, 0.8f), rng.Next(1000), S(Pal.Oak), 0.4f);
                for (int f = 0; f < 5; f++)
                    Blob(sm.loose, V3(bp + new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 1.2f, ctx.H(bp) + 0.65f), Vector3.one * 0.22f, rng.Next(1000), S(f % 2 == 0 ? Pal.SareePink : Pal.JacketBlue), 0.3f);
            }
            foreach (float s in new[] { -12f, 12f, 16f })
            {
                Vector2 pp = fm + fd * s + fo * 8f;
                Vector3 b = V3(pp, ctx.H(pp) - 0.1f), tp = b + new Vector3(0.3f, 6.5f, 0.2f);
                sm.atlas.Limb(b, tp, 0.22f, 0.16f, 7, S(Pal.Trunk));
                for (int f = 0; f < 9; f++)
                {
                    float a = f / 9f * Mathf.PI * 2f;
                    Vector3 tip = tp + new Vector3(Mathf.Cos(a) * 2.4f, -1.1f, Mathf.Sin(a) * 2.4f), mid = Vector3.Lerp(tp, tip, 0.5f) + Vector3.up * 0.5f;
                    Vector3 side = Vector3.Cross(tip - tp, Vector3.up).normalized * 0.35f;
                    sm.loose.Tri(tp, mid + side, mid - side, S(Pal.DeodarLight), Vector3.up);
                    sm.loose.Tri(mid + side, tip, mid - side, S(Pal.Oak), Vector3.up);
                    sm.loose.Tri(tp, mid - side, mid + side, S(Pal.DeodarLight), Vector3.down);
                    sm.loose.Tri(mid + side, mid - side, tip, S(Pal.Oak), Vector3.down);
                }
            }

            sm.Emit(ctx, "Raj Bhavan", parent);
            var site = new Site { origin = V3(fm + fo * 15f + fd * 6f, ctx.H(fm + fo * 15f + fd * 6f)), rot = Quaternion.LookRotation(V3(-fo, 0f)), depth = 1f };
            Sign(ctx, "Raj Bhavan", site, Vector3.zero, parent);
            ctx.Zone("Raj Bhavan", V3(cen, y0), 120f);
            ctx.landmarkSites["Raj Bhavan"] = V3(fm + fo * 14f, ctx.H(fm + fo * 14f) + 0.1f);
            var cam = new GameObject("ViewShot Raj Bhavan");
            cam.transform.SetParent(parent, false);
            Vector3 eye = V3(fm + fo * 30f + fd * 10f, ctx.H(fm + fo * 30f + fd * 10f) + 1.7f);
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(V3(fm, y0 + 6f) - eye));
            Debug.Log($"[Nainital] Raj Bhavan: {poly.Length} sides, floor {y0:F1}, porch at ({fm.x:F1},{fm.y:F1}) facing ({fo.x:F2},{fo.y:F2}), eye ({eye.x:F1},{eye.y:F1},{eye.z:F1})");
        }

        /// <summary>The zoo's gate (the owner's photo set): a concrete portal with the zoo's name in raised letters on two
        /// boards, green iron gates, a ticket window; behind it a bear pit with a stone den and a deer paddock in chain-link.</summary>
        static void ZooPhoto(WorldCtx ctx, Transform parent, Site s, System.Random rng)
        {
            var sm = new SiteMeshes();
            var conc = sm.Photo("concrete_wall_008", new Color(0.92f, 0.93f, 0.9f));
            var stone = sm.Photo(DarkStone(), new Color(0.95f, 0.95f, 0.95f));
            var pave = sm.Photo("concrete_floor_worn_001", new Color(0.85f, 0.85f, 0.83f));
            Vector3 L(float x, float y, float z) => s.origin + s.rot * new Vector3(x, y, z);
            float yaw = s.rot.eulerAngles.y;
            Vector2 L2(float x, float z) => XZ(L(x, 0f, z));
            Slab(pave, conc, L2(0f, -4f), L2(0f, 4f), 10f, s.origin.y, s.origin.y, s.origin.y - Mathf.Min(s.depth, 3f));
            foreach (float x in new[] { -3.6f, 3.6f }) TBox(conc, L(x, 2.75f, 0f), new Vector3(1.3f, 5.5f, 1.3f), s.rot, 1.4f);
            TBox(conc, L(0f, 5.4f, 0f), new Vector3(8.6f, 1.6f, 1.2f), s.rot, 1.4f);
            Board(sm, L(0f, 5.75f, 0.64f), yaw, 6.8f, 0.6f, Pal.TempleWhite, new[] { "BHARAT RATNA PT. GOVIND BALLABH PANT" }, new[] { 0.26f }, Pal.IronGreen, Pal.TempleWhite, s.origin.y + 6f);
            Board(sm, L(0f, 4.95f, 0.64f), yaw, 7.6f, 0.6f, Pal.TempleWhite, new[] { "HIGH ALTITUDE ZOO, NAINITAL" }, new[] { 0.3f }, Pal.IronGreen, Pal.TempleWhite, s.origin.y + 5f);
            // the two green gate leaves, a little open
            foreach (float sx in new[] { -1f, 1f })
            {
                var q = s.rot * Quaternion.Euler(0f, sx * 18f, 0f);
                Vector3 hinge = L(sx * 2.9f, 0f, 0.1f);
                Vector3 Lp(float x, float y) => hinge + q * new Vector3(-sx * x, y, 0f);
                foreach (float yy in new[] { 0.15f, 1.3f, 2.5f }) sm.atlas.Box(Lp(1.45f, yy), new Vector3(2.9f, 0.08f, 0.08f), q, S(Pal.IronGreen));
                for (float x = 0.1f; x <= 2.81f; x += 0.24f) sm.atlas.Box(Lp(x, 1.3f), new Vector3(0.04f, 2.5f, 0.04f), q, S(Pal.IronGreen));
                sm.atlas.Box(Lp(1.45f, 1.9f), new Vector3(1.2f, 0.6f, 0.05f), q, S(Pal.BoatYellow));   // the painted bird panel
            }
            // side walls with grilles, the ticket window on the right
            foreach (float sx in new[] { -1f, 1f })
            {
                Slab(conc, conc, L2(sx * 4.3f, 0f), L2(sx * 9f, 0f), 0.4f, s.origin.y + 1.0f, s.origin.y + 1.0f, s.origin.y - 1.5f);
                for (float x = 4.6f; x <= 8.8f; x += 0.3f) sm.atlas.Box(L(sx * x, 1.6f, 0f), new Vector3(0.04f, 1.2f, 0.04f), s.rot, S(Pal.IronGreen));
                sm.atlas.Box(L(sx * 6.7f, 2.2f, 0f), new Vector3(4.6f, 0.06f, 0.06f), s.rot, S(Pal.IronGreen));
            }
            float ty = Hut(ctx, sm, L2(7.5f, -3.2f), yaw, 3f, 2.4f, 2.6f, "concrete_wall_008", new Color(0.86f, 0.9f, 0.86f), new Color(0.5f, 0.7f, 0.55f), Pal.IronGreen, true, 0.5f, s.origin.y);
            Board(sm, L(7.5f, ty - s.origin.y + 2.3f, -1.95f), yaw, 1.9f, 0.4f, Pal.BoatGreen, new[] { "TICKET COUNTER" }, new[] { 0.15f }, Pal.ShirtWhite, Pal.IronGreen, ty + 2.4f);

            // inside: a bear pit (stone walls, a rock den) and a deer paddock in chain-link, each with its board
            void Pen(Vector2 c, float w, float d, bool pit, string name)
            {
                Vector2 x2 = XZ(s.rot * Vector3.right) * (w * 0.5f), z2 = XZ(s.rot * Vector3.forward) * (d * 0.5f);
                var cs = new[] { c - x2 - z2, c + x2 - z2, c + x2 + z2, c - x2 + z2 };
                for (int k = 0; k < 4; k++)
                {
                    Vector2 a = cs[k], b = cs[(k + 1) % 4];
                    if (pit) Slab(stone, stone, a, b, 0.5f, Mathf.Max(ctx.H(a), ctx.H(b)) + 1.4f, Mathf.Max(ctx.H(a), ctx.H(b)) + 1.4f, Mathf.Min(ctx.H(a), ctx.H(b)) - 0.5f, 1.2f);
                    var posts = new List<Vector3>();
                    int m = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 2.5f));
                    for (int i = 0; i <= m; i++) { Vector2 p = Vector2.Lerp(a, b, i / (float)m); posts.Add(V3(p, ctx.H(p) + (pit ? 1.4f : 0f))); }
                    PipeRail(sm, posts, Pal.StoneGrey, pit ? 1.6f : 2.2f, 0.03f);
                    for (int i = 0; i + 1 < posts.Count; i++)   // the chain-link: fine uprights
                        for (int j = 1; j < 10; j++)
                        {
                            Vector3 p = Vector3.Lerp(posts[i], posts[i + 1], j / 10f);
                            sm.loose.Box(p + Vector3.up * (pit ? 0.8f : 1.1f), new Vector3(0.012f, pit ? 1.55f : 2.15f, 0.012f), S(Pal.StoneGrey));
                        }
                }
                if (pit) RockLump(stone, V3(c - z2 * 0.6f, ctx.H(c - z2 * 0.6f) + 0.2f), new Vector3(2.4f, 1.8f, 1.8f), rng.Next(1000));
                for (int k = 0; k < 3; k++)
                {
                    Vector2 bp = c + x2 * ((float)rng.NextDouble() - 0.5f) + z2 * ((float)rng.NextDouble() - 0.3f);
                    RockLump(sm.Photo(GreyRock(), new Color(0.75f, 0.75f, 0.73f)), V3(bp, ctx.H(bp) - 0.1f), new Vector3(0.6f, 0.4f, 0.5f), rng.Next(1000));
                }
                Vector2 sp = c + z2 * 1.25f;
                Board(sm, V3(sp, ctx.H(sp) + 1.5f), yaw, 2.2f, 0.5f, Pal.BoatGreen, new[] { name }, new[] { 0.17f }, Pal.ShirtWhite, Pal.IronGreen, ctx.H(sp));
            }
            Pen(L2(-8f, -14f), 10f, 8f, true, "HIMALAYAN BLACK BEAR");
            Pen(L2(8f, -15f), 12f, 9f, false, "BARKING DEER");
            sm.Emit(ctx, "Nainital Zoo gate", parent);
        }

        /// <summary>The photo pass on a hilltop viewpoint: a flagstone ground, grey rock outcrops round it, a timber railing
        /// along the drop (the site's +z), a bench or two. kind: 0 Naina Peak, 1 Tiffin Top, 2 Land's End.</summary>
        static void PeakDressing(WorldCtx ctx, Transform parent, Site s, int kind, System.Random rng)
        {
            var sm = new SiteMeshes();
            var flags = sm.Photo("cobblestone_floor_08", new Color(0.8f, 0.79f, 0.76f), 0.2f);
            var stone = sm.Photo("old_stone_wall", new Color(0.8f, 0.78f, 0.75f));
            var rock = sm.Photo(GreyRock(), new Color(0.75f, 0.76f, 0.73f), 0.12f);
            var wood = sm.Photo("brown_planks_04", new Color(0.75f, 0.68f, 0.6f));
            // Land's End is a ledge at its road's edge (the road runs behind it): its ring sits forward, over the drop
            float fwd = kind == 2 ? 2.6f : 0f;
            Vector3 L(float x, float y, float z) => s.origin + s.rot * new Vector3(x, y, z + fwd);
            Vector2 c = XZ(L(0f, 0f, 0f));
            float R = kind == 1 ? 6.5f : kind == 2 ? 5.4f : 6f, yTop = s.origin.y + 0.02f;
            if (kind == 2)
            {
                // the lookout's level is its road's, and here the ground beside the road stands higher: the ring sits on it
                // (the first pass buried the ring and the preview camera under the hillside)
                float gMax = float.MinValue;
                for (int k = 0; k < 12; k++)
                {
                    float a = k * Mathf.PI / 6f;
                    foreach (float rr in new[] { 0f, R * 0.5f, R })
                        gMax = Mathf.Max(gMax, ctx.H(XZ(s.origin + s.rot * new Vector3(Mathf.Sin(a) * rr, 0f, Mathf.Cos(a) * rr + fwd))));
                }
                peakLift = 0f;   // (the ledge is on the valley side now; raising the ring to the ground made a stone drum)
            }
            else peakLift = 0f;
            const int seg = 16;
            for (int k = 0; k < seg; k++)
            {
                float a0 = k * Mathf.PI * 2f / seg, a1 = (k + 1) * Mathf.PI * 2f / seg;
                Vector2 p0 = XZ(L(Mathf.Sin(a0) * R, 0f, Mathf.Cos(a0) * R)), p1 = XZ(L(Mathf.Sin(a1) * R, 0f, Mathf.Cos(a1) * R));
                Vector2 m = (p0 + p1) * 0.5f;
                Slab(flags, stone, c, m, Vector2.Distance(p0, p1) * 1.02f, yTop, yTop, Mathf.Min(ctx.H(m), yTop) - 0.6f);
            }
            // grey outcrops round the back and the sides, larger where the ground falls
            for (int k = 0; k < 10; k++)
            {
                float a = Mathf.PI * (0.35f + 1.3f * k / 9f) + (float)rng.NextDouble() * 0.2f;
                float r = R + 0.8f + (float)rng.NextDouble() * 2.2f;
                Vector2 p = XZ(L(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r));
                float rr = 0.7f + (float)rng.NextDouble() * (kind == 0 ? 1.4f : 0.9f);
                if (WorldBuilder.NearestMotorRoad(ctx, p, 7f + rr, out _, out _)) continue;   // never on a road (the first pass put them on Land's End's)
                RockLump(rock, V3(p, Mathf.Min(ctx.H(p), yTop) - rr * 0.25f), new Vector3(rr * 1.3f, rr * 0.8f, rr), rng.Next(1000));
            }
            if (kind == 0) RockLump(rock, L(-1.5f, -0.4f, -R - 1.2f), new Vector3(3.2f, 2.2f, 2.4f), 31);   // the summit rock
            // the timber railing along the front arc
            var posts = new List<Vector3>();
            for (int k = -5; k <= 5; k++)
            {
                float a = k * 0.2f;
                posts.Add(L(Mathf.Sin(a) * (R - 0.3f), peakLift, Mathf.Cos(a) * (R - 0.3f)));
            }
            for (int i = 0; i < posts.Count; i++)
            {
                TBox(wood, posts[i] + Vector3.up * 0.5f, new Vector3(0.14f, 1.2f, 0.14f), Quaternion.Euler(0f, s.rot.eulerAngles.y, 0f), 0.8f);
                if (i == 0) continue;
                Vector3 a = posts[i - 1], b = posts[i], d = b - a;
                var q = Quaternion.LookRotation(d);
                foreach (float h in new[] { 0.55f, 1.0f }) TBox(wood, (a + b) * 0.5f + Vector3.up * h, new Vector3(0.08f, 0.12f, d.magnitude + 0.1f), q, 0.8f);
                Vector3 side = Vector3.Cross(Vector3.up, d).normalized;
                sm.col.Quad(a, b, b + Vector3.up * 1.1f, a + Vector3.up * 1.1f, Vector2.zero, side);
                sm.col.Quad(b, a, a + Vector3.up * 1.1f, b + Vector3.up * 1.1f, Vector2.zero, -side);
            }
            Bench(sm.atlas, L(-2.2f, peakLift, R - 1.8f), s.rot.eulerAngles.y + 180f, Pal.WoodLight, Pal.WoodDark, 1.8f);
            if (kind != 1) Bench(sm.atlas, L(2.4f, peakLift, R - 1.9f), s.rot.eulerAngles.y + 180f, Pal.WoodLight, Pal.WoodDark, 1.8f);
            if (kind == 1)
            {
                // Dorothy's Seat in dressed stone, its plaque
                TBox(stone, L(0f, 0.3f, 3.2f), new Vector3(3.2f, 0.6f, 1.0f), s.rot, 1.2f);
                TBox(stone, L(0f, 0.95f, 2.72f), new Vector3(3.4f, 1.3f, 0.32f), s.rot, 1.2f);
                sm.atlas.Box(L(0f, 1.05f, 2.9f), new Vector3(1.0f, 0.55f, 0.05f), s.rot, S(Pal.Gold));
            }
            sm.Emit(ctx, (kind == 0 ? "Naina Peak" : kind == 1 ? "Tiffin Top" : "Land's End") + " (photo pass)", parent);
        }
    }
}
