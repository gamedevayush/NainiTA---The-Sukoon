using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>Hill-town buildings: multi-storey facades that step down the slope, tin gable or flat roofs.</summary>
    public static class BuildingKit
    {
        public const float FloorH = 3f;

        public struct Style
        {
            public int wall, roof;
            public bool flatRoof, shop;
            public float litChance;
        }

        static int PickStyle(System.Random rng, bool front, bool isGround, bool belowStreet, bool shop, bool isDoorBay, float litChance)
        {
            bool lit = rng.NextDouble() < litChance;
            if (isGround && front)
            {
                if (shop) return lit || rng.NextDouble() < 0.5 ? Atlas.StyleShopLit : Atlas.StyleShopDark;
                if (isDoorBay) return Atlas.StyleDoor;
            }
            double r = rng.NextDouble();
            if (!front && r < 0.35) return Atlas.StylePlain;
            if (front && !isGround && !belowStreet && r < 0.2) return lit ? Atlas.StyleBalconyLit : Atlas.StyleBalconyDark;
            if (r < 0.62) return lit ? Atlas.StyleWinALit : Atlas.StyleWinADark;
            if (r < 0.9) return lit ? Atlas.StyleWinBLit : Atlas.StyleWinBDark;
            return Atlas.StylePlain;
        }

        /// <summary>One wall from a to b (XZ), rows of 3 m floors from yTop down to yBase.</summary>
        public static void FacadeWall(MeshBuilder mb, Vector2 a, Vector2 b, float yBase, float yTop, float streetY,
                                      Vector3 outward, Style st, System.Random rng, bool front)
        {
            float len = Vector2.Distance(a, b);
            if (len < 0.2f) return;
            int bays = Mathf.Max(1, Mathf.RoundToInt(len / 3.2f));
            float top = yTop;
            int guard = 0;
            while (top > yBase + 0.05f && guard++ < 40)
            {
                float bottom = top - FloorH;
                bool partial = bottom < yBase - 0.01f;
                if (partial) bottom = yBase;
                bool isGround = Mathf.Abs(bottom - streetY) < 0.6f;
                bool below = bottom < streetY - 0.6f;
                for (int i = 0; i < bays; i++)
                {
                    Vector2 p0 = Vector2.Lerp(a, b, i / (float)bays), p1 = Vector2.Lerp(a, b, (i + 1) / (float)bays);
                    var bl = new Vector3(p0.x, bottom, p0.y);
                    var br = new Vector3(p1.x, bottom, p1.y);
                    var tr = new Vector3(p1.x, top, p1.y);
                    var tl = new Vector3(p0.x, top, p0.y);
                    Rect uv = partial ? Atlas.Material(Atlas.MatStone)
                                      : Atlas.Facade(st.wall, PickStyle(rng, front, isGround, below, st.shop, i == bays / 2, st.litChance));
                    mb.Quad(bl, br, tr, tl, uv, outward);
                }
                // chhajja: concrete sunshade slab along the front at every floor
                if (front && !partial && !below && top < yTop - 0.1f)
                {
                    Vector2 mid = (a + b) * 0.5f;
                    var center = new Vector3(mid.x, top - 0.05f, mid.y) + outward * 0.3f;
                    mb.Box(center, new Vector3(len, 0.12f, 0.6f), Quaternion.LookRotation(outward), Atlas.WallSolid(st.wall));
                }
                top = bottom;
            }
        }

        /// <summary>Called for each shop front: (position above the shutter, outward normal, frontage width).</summary>
        public static System.Action<Vector3, Vector3, float> ShopFront;

        /// <summary>Ground height at a point (set by the world builder), so a house can meet the real slope.</summary>
        public static System.Func<Vector2, float> Ground;

        /// <summary>A textured strip of an atlas material tile, about 2 m of material per tile.</summary>
        static Rect TileStrip(int mat, float wM, float hM, float v0M = 0f)
        {
            Rect r = Atlas.Material(mat);
            float fw = Mathf.Clamp01(wM / 2f), fh = Mathf.Clamp01(hM / 2f), f0 = fh > 0.98f ? 0f : Mathf.Repeat(v0M / 2f, 1f - fh);
            return new Rect(r.x, r.y + r.height * f0, r.width * fw, r.height * fh);
        }

        /// <summary>Where a wall meets the ground (user, 2026-11-11: "house ek dam se ghaas pe na aaye"). As in the town:
        /// a stone or rendered plinth standing a hand's width proud of the wall, up to ~0.45 m above the highest ground
        /// along it (taller on the downhill side), and at its foot a concrete apron (plinth protection) wherever the ground
        /// is level enough, its edge stepping down to the ground. The terrain round it is baked to grit and earth.</summary>
        static void WallFoot(MeshBuilder mb, Vector2 a, Vector2 b, Vector3 outward, float yBase, float streetY, int plinthMat, bool apron)
        {
            if (Ground == null) return;
            float len = Vector2.Distance(a, b);
            if (len < 0.5f) return;
            Vector2 o = new Vector2(outward.x, outward.z).normalized, dir = (b - a) / len;
            int n = Mathf.Max(1, Mathf.CeilToInt(len / 1.6f));
            float gTop = float.MinValue;
            for (int k = 0; k <= n; k++) gTop = Mathf.Max(gTop, Ground(Vector2.Lerp(a, b, k / (float)n) + o * 0.4f));
            float top = Mathf.Min(gTop + 0.45f, streetY + 0.7f);
            if (top <= yBase + 0.1f) return;
            const float Proud = 0.08f;
            Vector2 ea = a - dir * Proud, eb = b + dir * Proud;   // meet the next wall's plinth at the corner
            Vector2 conc = Atlas.MaterialUV(Atlas.MatConcrete);
            for (int k = 0; k < n; k++)
            {
                Vector2 p0 = Vector2.Lerp(ea, eb, k / (float)n) + o * Proud, p1 = Vector2.Lerp(ea, eb, (k + 1) / (float)n) + o * Proud;
                float seg = Vector2.Distance(p0, p1);
                mb.Quad(new Vector3(p0.x, yBase, p0.y), new Vector3(p1.x, yBase, p1.y), new Vector3(p1.x, top, p1.y), new Vector3(p0.x, top, p0.y),
                        TileStrip(plinthMat, seg, top - yBase, k * 0.7f), outward);
                Vector2 q0 = p0 - o * Proud, q1 = p1 - o * Proud;   // the ledge back to the wall
                mb.Quad(new Vector3(p0.x, top, p0.y), new Vector3(p1.x, top, p1.y), new Vector3(q1.x, top, q1.y), new Vector3(q0.x, top, q0.y),
                        new Rect(conc, Vector2.zero), Vector3.up);
            }
            if (!apron) return;
            const float W = 0.75f;
            for (int k = 0; k < n; k++)
            {
                Vector2 p0 = Vector2.Lerp(ea, eb, k / (float)n) + o * Proud, p1 = Vector2.Lerp(ea, eb, (k + 1) / (float)n) + o * Proud;
                if (k == n - 1) p1 += dir * W;   // round the corner, once
                float gi0 = Ground(p0), gi1 = Ground(p1), go0 = Ground(p0 + o * W), go1 = Ground(p1 + o * W);
                if (Mathf.Abs(gi0 - go0) > 0.45f || Mathf.Abs(gi1 - go1) > 0.45f || Mathf.Max(gi0, gi1) > top - 0.05f) continue;   // steep: the plinth is enough
                float y0 = Mathf.Max(gi0, go0) + 0.06f, y1 = Mathf.Max(gi1, go1) + 0.06f;
                Vector2 r0 = p0 + o * W, r1 = p1 + o * W;
                float seg = Vector2.Distance(p0, p1);
                mb.Quad(new Vector3(r0.x, y0 - 0.02f, r0.y), new Vector3(r1.x, y1 - 0.02f, r1.y), new Vector3(p1.x, y1, p1.y), new Vector3(p0.x, y0, p0.y),
                        TileStrip(Atlas.MatConcrete, seg, W, k * 0.9f), Vector3.up);
                float d0 = Mathf.Min(go0, gi0) - 0.08f, d1 = Mathf.Min(go1, gi1) - 0.08f;   // its edge, down into the ground
                mb.Quad(new Vector3(r0.x, d0, r0.y), new Vector3(r1.x, d1, r1.y), new Vector3(r1.x, y1 - 0.02f, r1.y), new Vector3(r0.x, y0 - 0.02f, r0.y),
                        TileStrip(Atlas.MatConcrete, seg, 0.3f, k * 0.5f), outward);
            }
        }

        static int PlinthMaterial(System.Random rng)
        {
            double r = rng.NextDouble();
            return r < 0.55 ? Atlas.MatStone : r < 0.85 ? Atlas.MatConcrete : Atlas.MatDarkStone;
        }

        /// <summary>Rectangular house. Front (local +Z) faces downhill / the street.</summary>
        public static void House(MeshBuilder mb, MeshBuilder col, Vector2 c, float w, float d, float yaw, int levels,
                                 float groundMin, float groundMax, Style st, System.Random rng)
        {
            float streetY = groundMax - 0.25f;
            float yBase = groundMin - 0.6f;
            float yTop = streetY + levels * FloorH;
            Quaternion q = Quaternion.Euler(0, yaw, 0);
            Vector3 R3 = q * Vector3.right, F3 = q * Vector3.forward;
            Vector2 R = new Vector2(R3.x, R3.z) * (w * 0.5f), F = new Vector2(F3.x, F3.z) * (d * 0.5f);
            Vector2 fl = c - R + F, fr = c + R + F, br = c + R - F, bl = c - R - F;

            FacadeWall(mb, fl, fr, yBase, yTop, streetY, F3, st, rng, true);
            if (st.shop && ShopFront != null)
            {
                Vector2 fm = (fl + fr) * 0.5f;
                ShopFront(new Vector3(fm.x, streetY + 2.75f, fm.y) + F3 * 0.62f, F3, w);
            }
            if (st.shop && levels >= 2) FrontDetail(mb, fl, fr, F3, Quaternion.LookRotation(F3), w, levels, streetY, rng);
            FacadeWall(mb, fr, br, yBase, yTop, streetY, R3, st, rng, false);
            FacadeWall(mb, br, bl, yBase, yTop, streetY, -F3, st, rng, false);
            FacadeWall(mb, bl, fl, yBase, yTop, streetY, -R3, st, rng, false);
            int plinth = PlinthMaterial(rng);
            WallFoot(mb, fl, fr, F3, yBase, streetY, plinth, !st.shop);
            WallFoot(mb, fr, br, R3, yBase, streetY, plinth, !st.shop);
            WallFoot(mb, br, bl, -F3, yBase, streetY, plinth, !st.shop);
            WallFoot(mb, bl, fl, -R3, yBase, streetY, plinth, !st.shop);

            var saved = mb.M;
            Vector3 topCenter = new Vector3(c.x, yTop, c.y);
            if (!st.flatRoof)
            {
                bool alongX = w >= d;
                mb.M = saved * Matrix4x4.TRS(topCenter, q * (alongX ? Quaternion.identity : Quaternion.Euler(0, 90, 0)), Vector3.one);
                float L = alongX ? w : d, D = alongX ? d : w;
                mb.GableRoof(Vector3.zero, L, D, D * 0.27f, Atlas.Roof(st.roof), Atlas.WallSolid(st.wall), 0.45f);
            }
            else
            {
                mb.M = saved * Matrix4x4.TRS(topCenter, q, Vector3.one);
                Vector2 conc = Atlas.MaterialUV(Atlas.MatConcrete), wallUV = Atlas.WallSolid(st.wall);
                mb.Quad(new Vector3(-w / 2, 0.02f, -d / 2), new Vector3(w / 2, 0.02f, -d / 2), new Vector3(w / 2, 0.02f, d / 2), new Vector3(-w / 2, 0.02f, d / 2), conc, Vector3.up);
                float ph = 0.75f, t = 0.15f;
                mb.Box(new Vector3(0, ph / 2, d / 2 - t / 2), new Vector3(w, ph, t), wallUV);
                mb.Box(new Vector3(0, ph / 2, -d / 2 + t / 2), new Vector3(w, ph, t), wallUV);
                mb.Box(new Vector3(w / 2 - t / 2, ph / 2, 0), new Vector3(t, ph, d - 2 * t), wallUV);
                mb.Box(new Vector3(-w / 2 + t / 2, ph / 2, 0), new Vector3(t, ph, d - 2 * t), wallUV);
                if (rng.NextDouble() < 0.3 && w > 5f)   // clothesline with drying clothes
                {
                    float lz = (float)(rng.NextDouble() - 0.5) * (d - 2f);
                    Vector3 p0 = new Vector3(-w / 2 + 0.8f, 0, lz), p1 = new Vector3(w / 2 - 0.8f, 0, lz);
                    mb.Limb(p0, p0 + Vector3.up * 1.7f, 0.03f, 0.03f, 4, Atlas.SolidUV(Pal.IronBlack));
                    mb.Limb(p1, p1 + Vector3.up * 1.7f, 0.03f, 0.03f, 4, Atlas.SolidUV(Pal.IronBlack));
                    mb.Limb(p0 + Vector3.up * 1.65f, p1 + Vector3.up * 1.65f, 0.01f, 0.01f, 3, Atlas.SolidUV(Pal.IronBlack), false);
                    int[] cloth = { Pal.SareeOrange, Pal.JacketBlue, Pal.ShirtWhite, Pal.SareePink, Pal.JacketYellow, Pal.Jeans };
                    int pieces = 3 + rng.Next(4);
                    for (int k = 0; k < pieces; k++)
                    {
                        float x = Mathf.Lerp(p0.x + 0.4f, p1.x - 0.4f, (k + 0.5f) / pieces);
                        float cw = 0.35f + (float)rng.NextDouble() * 0.4f, ch = 0.4f + (float)rng.NextDouble() * 0.5f;
                        var uv = new Rect(Atlas.SolidUV(cloth[rng.Next(cloth.Length)]), Vector2.zero);
                        mb.QuadDouble(new Vector3(x - cw / 2, 1.65f - ch, lz), new Vector3(x + cw / 2, 1.65f - ch, lz),
                                      new Vector3(x + cw / 2, 1.65f, lz), new Vector3(x - cw / 2, 1.65f, lz), uv, Vector3.forward);
                    }
                }
                if (rng.NextDouble() < 0.7)   // black Sintex water tank: a very Nainital rooftop detail
                {
                    float tx = (float)(rng.NextDouble() - 0.5) * (w - 2f), tz = (float)(rng.NextDouble() - 0.5) * (d - 2f);
                    mb.Cylinder(new Vector3(tx, 0, tz), 0.55f, 0.55f, 1.15f, 8, Atlas.SolidUV(Pal.WaterTank), true);
                }
                if (rng.NextDouble() < 0.35)   // dish TV antenna
                {
                    float ax = (float)(rng.NextDouble() - 0.5) * (w - 1.5f), az = (float)(rng.NextDouble() - 0.5) * (d - 1.5f);
                    mb.Limb(new Vector3(ax, 0, az), new Vector3(ax, 0.9f, az), 0.03f, 0.03f, 4, Atlas.SolidUV(Pal.StoneGrey));
                    mb.Cylinder(new Vector3(ax, 0.75f, az + 0.05f), 0.32f, 0.05f, 0.18f, 8, Atlas.SolidUV(Pal.ShirtWhite), true, true, 1f);
                }
                if (rng.NextDouble() < 0.12)   // bougainvillea / potted plants on the terrace edge
                    mb.Sphere(new Vector3(w * 0.35f, 0.9f, d * 0.5f - 0.3f), new Vector3(0.9f, 0.6f, 0.4f), 6, 3, Atlas.ExtraUV(Px.Bougainvillea));
                if (rng.NextDouble() < 0.45 && w > 6f && d > 6f)
                {
                    mb.Box(new Vector3(-w / 2 + 1.6f, 1.2f, -d / 2 + 1.6f), new Vector3(2.4f, 2.4f, 2.4f), wallUV);
                    mb.Box(new Vector3(-w / 2 + 1.6f, 2.45f, -d / 2 + 1.6f), new Vector3(2.8f, 0.12f, 2.8f), conc);
                }
            }
            mb.M = saved;

            // simple collision box
            col.Box(new Vector3(c.x, (yBase + yTop) * 0.5f, c.y), new Vector3(w, yTop - yBase, d), q, Vector2.zero);
            if (!st.flatRoof) { }
        }

        /// <summary>What a bazaar facade carries in the photos: projecting balconies with railings (white, wood or green
        /// iron), split-AC boxes, now and then a water pipe and washing on a line.</summary>
        static void FrontDetail(MeshBuilder mb, Vector2 fl, Vector2 fr, Vector3 F3, Quaternion rot, float w, int levels, float streetY, System.Random rng)
        {
            Vector2 fm = (fl + fr) * 0.5f;
            Vector3 along = new Vector3(fr.x - fl.x, 0f, fr.y - fl.y).normalized;
            Vector2 conc = Atlas.MaterialUV(Atlas.MatConcrete), white = Atlas.SolidUV(Pal.ShirtWhite), ac = Atlas.SolidUV(Pal.ShirtWhite), pipe = Atlas.SolidUV(Pal.StoneGrey);
            Vector2[] rails = { Atlas.SolidUV(Pal.ShirtWhite), Atlas.MaterialUV(Atlas.MatWood), Atlas.SolidUV(Pal.IronGreen), Atlas.SolidUV(Pal.IronBlack) };
            int[] cloth = { Pal.SareeOrange, Pal.JacketBlue, Pal.ShirtWhite, Pal.SareePink, Pal.JacketYellow, Pal.Jeans };
            for (int k = 1; k < levels; k++)
            {
                if (rng.NextDouble() > 0.5) continue;
                float y = streetY + k * FloorH;
                float bw = w * (0.5f + (float)rng.NextDouble() * 0.42f), depth = 0.9f + (float)rng.NextDouble() * 0.35f;
                float shift = ((float)rng.NextDouble() - 0.5f) * (w - bw);
                Vector3 slab = new Vector3(fm.x, y, fm.y) + F3 * (depth * 0.5f + 0.05f) + along * shift;
                mb.Box(slab, new Vector3(bw, 0.14f, depth), rot, conc);
                Vector2 rail = rails[rng.Next(rails.Length)];
                Vector3 edge = slab + F3 * (depth * 0.5f - 0.03f);
                mb.Box(edge + Vector3.up * 1.0f, new Vector3(bw, 0.06f, 0.07f), rot, rail);
                mb.Box(edge + Vector3.up * 0.15f, new Vector3(bw, 0.05f, 0.05f), rot, rail);
                int posts = Mathf.Max(3, Mathf.RoundToInt(bw / 0.3f));
                for (int p = 0; p < posts; p++)
                    mb.Box(edge + along * ((p / (float)(posts - 1) - 0.5f) * bw) + Vector3.up * 0.55f, new Vector3(0.035f, 0.85f, 0.035f), rot, rail);
                foreach (float s in new[] { -1f, 1f })
                    mb.Box(slab + along * (s * bw * 0.5f) + Vector3.up * 0.55f, new Vector3(0.04f, 0.85f, depth), rot, rail);
                if (rng.NextDouble() < 0.3)   // washing on a line along the balcony
                {
                    int pieces = 2 + rng.Next(4);
                    for (int i = 0; i < pieces; i++)
                    {
                        float x = ((i + 0.5f) / pieces - 0.5f) * bw * 0.85f, cw = 0.18f + (float)rng.NextDouble() * 0.15f, ch = 0.35f + (float)rng.NextDouble() * 0.35f;
                        Vector3 top = edge + along * x + Vector3.up * 1.0f;
                        mb.QuadDouble(top - along * cw - Vector3.up * ch, top + along * cw - Vector3.up * ch, top + along * cw, top - along * cw,
                                      new Rect(Atlas.SolidUV(cloth[rng.Next(cloth.Length)]), Vector2.zero), F3);
                    }
                }
            }
            int units = rng.Next(0, 3);   // split-AC boxes hung on the front
            for (int i = 0; i < units; i++)
            {
                int k = 1 + rng.Next(levels);
                float x = ((float)rng.NextDouble() - 0.5f) * (w - 1.2f);
                Vector3 c = new Vector3(fm.x, streetY + (k - 1) * FloorH + 2.1f, fm.y) + along * x + F3 * 0.2f;
                mb.Box(c, new Vector3(0.82f, 0.55f, 0.3f), rot, ac);
                mb.Box(c + F3 * 0.16f, new Vector3(0.5f, 0.4f, 0.02f), rot, pipe);
            }
            if (rng.NextDouble() < 0.4)   // a drain pipe down one corner
            {
                Vector3 top = new Vector3(fl.x, streetY + levels * FloorH, fl.y) + along * 0.2f + F3 * 0.08f;
                mb.Limb(top, new Vector3(top.x, streetY, top.z), 0.05f, 0.05f, 5, pipe, false);
            }
        }

        /// <summary>OSM footprint building (any polygon), flat roof with parapet.</summary>
        public static void Footprint(MeshBuilder mb, MeshBuilder col, Vector2[] poly, int levels, float groundMin, float groundMax,
                                     Style st, System.Random rng)
        {
            if (poly.Length < 3) return;
            if (Geo.SignedArea(poly) < 0) System.Array.Reverse(poly);   // CCW
            float streetY = groundMax - 0.25f, yBase = groundMin - 0.6f, yTop = streetY + levels * FloorH;
            int plinthMat = PlinthMaterial(rng);
            Vector2 center = Vector2.zero;
            foreach (var p in poly) center += p;
            center /= poly.Length;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                Vector2 e = b - a;
                Vector3 outward = new Vector3(e.y, 0, -e.x).normalized;
                FacadeWall(mb, a, b, yBase, yTop, streetY, outward, st, rng, i == 0);
                WallFoot(mb, a, b, outward, yBase, streetY, plinthMat, !st.shop);
                // parapet
                mb.Quad(new Vector3(a.x, yTop, a.y), new Vector3(b.x, yTop, b.y), new Vector3(b.x, yTop + 0.8f, b.y), new Vector3(a.x, yTop + 0.8f, a.y),
                        Atlas.WallSolid(st.wall), outward);
                mb.Quad(new Vector3(a.x, yTop, a.y), new Vector3(b.x, yTop, b.y), new Vector3(b.x, yTop + 0.8f, b.y), new Vector3(a.x, yTop + 0.8f, a.y),
                        Atlas.WallSolid(st.wall), -outward);
                col.Quad(new Vector3(a.x, yBase, a.y), new Vector3(b.x, yBase, b.y), new Vector3(b.x, yTop, b.y), new Vector3(a.x, yTop, a.y), Vector2.zero, outward);
            }
            int[] tris = Geo.Triangulate(poly);
            Vector2 conc = Atlas.MaterialUV(Atlas.MatConcrete);
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                Vector2 p0 = poly[tris[t]], p1 = poly[tris[t + 1]], p2 = poly[tris[t + 2]];
                mb.Tri(new Vector3(p0.x, yTop + 0.02f, p0.y), new Vector3(p1.x, yTop + 0.02f, p1.y), new Vector3(p2.x, yTop + 0.02f, p2.y), conc, Vector3.up);
                col.Tri(new Vector3(p0.x, yTop, p0.y), new Vector3(p1.x, yTop, p1.y), new Vector3(p2.x, yTop, p2.y), Vector2.zero, Vector3.up);
            }
            if (rng.NextDouble() < 0.6)
                mb.Cylinder(new Vector3(center.x, yTop, center.y), 0.6f, 0.6f, 1.2f, 8, Atlas.SolidUV(Pal.WaterTank), true);
        }
    }
}
