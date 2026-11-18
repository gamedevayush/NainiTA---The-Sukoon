using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Shared pieces for the sites laid out by hand from the user's photos and the satellite (Eco Cave Garden, Lover's
    /// Point; 19 Nov 2026): meshes in world space grouped by photo material (uvs in metres, so the scans keep their real
    /// size), walkways of paving and stone steps that wall down to the ground, pipe railings, painted boards with solid
    /// 3D letters, tin-roofed huts, and terrain shaping / painting for a site.
    /// </summary>
    public static partial class LandmarkBuilder
    {
        /// <summary>One site's meshes: a builder per photo material (+ tint), painted atlas pieces that collide, small
        /// atlas dressing that does not, and invisible collision (railings).</summary>
        sealed class SiteMeshes
        {
            readonly Dictionary<string, MeshBuilder> photo = new Dictionary<string, MeshBuilder>();
            readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
            public readonly MeshBuilder atlas = new MeshBuilder(), loose = new MeshBuilder(), col = new MeshBuilder();

            public MeshBuilder Photo(string tex, Color tint, float smooth = 0.08f)
            {
                string key = tex + "_" + ColorUtility.ToHtmlStringRGB(tint);
                if (!photo.TryGetValue(key, out var mb))
                {
                    photo[key] = mb = new MeshBuilder();
                    mats[key] = WorldBuilder.PhotoMaterial(tex, "M_Site_" + key, tint, smooth);
                    if (!mats[key]) Debug.LogWarning("[Nainital] site material missing: " + tex);
                }
                return mb;
            }

            public Transform Emit(WorldCtx ctx, string name, Transform parent)
            {
                var root = new GameObject(name).transform;
                root.SetParent(parent, false);
                foreach (var kv in photo)
                    if (kv.Value.VertexCount > 0)
                        ctx.MeshObject(name + " " + kv.Key, root, kv.Value, mats[kv.Key] ? mats[kv.Key] : ctx.mats.atlas, null, true, true);
                if (atlas.VertexCount > 0) ctx.MeshObject(name + " painted", root, atlas, ctx.mats.atlas, null, true, true);
                if (loose.VertexCount > 0) ctx.MeshObject(name + " details", root, loose, ctx.mats.atlas, null, true, false);
                if (col.VertexCount > 0) ctx.MeshObject(name + " railings", root, null, null, col);
                return root;
            }
        }

        /// <summary>A recoloured copy of a scan, written once beside the others as dst (its normal map is the source's):
        /// grey = lift + gain x luminance, times a tint, with `keep` of the original colour left in.</summary>
        static string Recoloured(string src, string dst, float lift, float gain, Color tint, float keep)
        {
            const string dir = "Assets/Nainital/Data/PBR/";
            string alb = dir + dst + "_albedo.png", nrm = dir + dst + "_normal.png";
            if (!System.IO.File.Exists(alb))
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(System.IO.File.ReadAllBytes(dir + src + "_albedo.png"));
                var px = tex.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                {
                    var c = px[i];
                    float l = (0.3f * c.r + 0.59f * c.g + 0.11f * c.b) / 255f;
                    float g = Mathf.Clamp01(lift + l * gain);
                    float r = Mathf.Lerp(g * tint.r, c.r / 255f, keep), gg = Mathf.Lerp(g * tint.g, c.g / 255f, keep), b = Mathf.Lerp(g * tint.b, c.b / 255f, keep);
                    px[i] = new Color32((byte)(Mathf.Clamp01(r) * 255f), (byte)(Mathf.Clamp01(gg) * 255f), (byte)(Mathf.Clamp01(b) * 255f), c.a);
                }
                tex.SetPixels32(px);
                System.IO.File.WriteAllBytes(alb, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                if (System.IO.File.Exists(dir + src + "_normal.png")) System.IO.File.Copy(dir + src + "_normal.png", nrm, true);
                AssetDatabase.ImportAsset(alb);
                if (System.IO.File.Exists(nrm)) AssetDatabase.ImportAsset(nrm);
            }
            return dst;
        }

        /// <summary>Grey rock from rock_face (every rock scan on disk is warm brown; Kumaon's boulders and the Eco Cave rock
        /// are grey).</summary>
        static string GreyRock() => Recoloured("rock_face", "rock_grey", 0.16f, 1.3f, new Color(0.97f, 0.99f, 1.02f), 0.12f);

        /// <summary>Weathered light tin from corrugated_iron_02 (the scan is dark and rusty; the tea sheds' roofs read
        /// white-grey on the satellite).</summary>
        static string LightTin() => Recoloured("corrugated_iron_02", "tin_light", 0.34f, 1.05f, new Color(0.94f, 0.97f, 1.02f), 0.08f);

        static Vector3 V3(Vector2 p, float y) => new Vector3(p.x, y, p.y);
        static Vector2 XZ(Vector3 p) => new Vector2(p.x, p.z);

        /// <summary>A quad whose uvs run in metres / tile from uv0.</summary>
        static void TQuad(MeshBuilder mb, Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl, float tile, Vector3 outward, Vector2 uv0 = default)
        {
            float w = Vector3.Distance(bl, br) / tile, h = Vector3.Distance(bl, tl) / tile;
            Vector2 a = uv0, b = uv0 + new Vector2(w, 0f), c = uv0 + new Vector2(w, h), d = uv0 + new Vector2(0f, h);
            mb.Tri(bl, br, tr, a, b, c, outward);
            mb.Tri(bl, tr, tl, a, c, d, outward);
        }

        static void TBox(MeshBuilder mb, Vector3 c, Vector3 size, Quaternion rot, float tile)
        {
            Vector3 h = size * 0.5f;
            Vector3 X = rot * Vector3.right * h.x, Y = rot * Vector3.up * h.y, Z = rot * Vector3.forward * h.z;
            TQuad(mb, c + Z - X - Y, c + Z + X - Y, c + Z + X + Y, c + Z - X + Y, tile, Z);
            TQuad(mb, c - Z + X - Y, c - Z - X - Y, c - Z - X + Y, c - Z + X + Y, tile, -Z);
            TQuad(mb, c + X + Z - Y, c + X - Z - Y, c + X - Z + Y, c + X + Z + Y, tile, X);
            TQuad(mb, c - X - Z - Y, c - X + Z - Y, c - X + Z + Y, c - X - Z + Y, tile, -X);
            TQuad(mb, c + Y - X - Z, c + Y + X - Z, c + Y + X + Z, c + Y - X + Z, tile, Y);
            TQuad(mb, c - Y - X + Z, c - Y + X + Z, c - Y + X - Z, c - Y - X - Z, tile, -Y);
        }

        static Vector2 WUV(Vector3 p, float tile) => new Vector2(p.x, p.z) / tile;

        /// <summary>A wall face from its top corners t0, t1 down to b1, b0; u along the wall in world metres, v from the
        /// world height, so stone courses run on from one piece to the next.</summary>
        static void Face(MeshBuilder mb, Vector3 t0, Vector3 t1, Vector3 b1, Vector3 b0, Vector3 outward, float tile)
        {
            Vector2 dir = XZ(t1 - t0);
            if (dir.sqrMagnitude < 1e-8f) return;
            dir.Normalize();
            float u0 = Vector2.Dot(XZ(t0), dir) / tile, u1 = Vector2.Dot(XZ(t1), dir) / tile;
            mb.Tri(t0, t1, b1, new Vector2(u0, t0.y / tile), new Vector2(u1, t1.y / tile), new Vector2(u1, b1.y / tile), outward);
            mb.Tri(t0, b1, b0, new Vector2(u0, t0.y / tile), new Vector2(u1, b1.y / tile), new Vector2(u0, b0.y / tile), outward);
        }

        /// <summary>A solid slab between plan points a and b: the top from ya to yb (paving), four sides down to `bottom`
        /// (stone). Steps, landings, plinths and walls are all made of these.</summary>
        static void Slab(MeshBuilder top, MeshBuilder side, Vector2 a, Vector2 b, float width, float ya, float yb, float bottom, float tile = 1.5f)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 1e-3f) return;
            Vector2 f = d / len, r = new Vector2(f.y, -f.x) * (width * 0.5f);
            Vector3 aL = V3(a - r, ya), aR = V3(a + r, ya), bL = V3(b - r, yb), bR = V3(b + r, yb);
            Vector3 aLb = V3(a - r, bottom), aRb = V3(a + r, bottom), bLb = V3(b - r, bottom), bRb = V3(b + r, bottom);
            Vector3 fwd = new Vector3(f.x, 0f, f.y), right = new Vector3(r.x, 0f, r.y).normalized;
            if (top != null)
            {
                top.Tri(aL, aR, bR, WUV(aL, tile), WUV(aR, tile), WUV(bR, tile), Vector3.up);
                top.Tri(aL, bR, bL, WUV(aL, tile), WUV(bR, tile), WUV(bL, tile), Vector3.up);
            }
            Face(side, aR, bR, bRb, aRb, right, tile);
            Face(side, bL, aL, aLb, bLb, -right, tile);
            Face(side, aL, aR, aRb, aLb, -fwd, tile);
            Face(side, bR, bL, bLb, bRb, fwd, tile);
        }

        /// <summary>The lowest ground under a strip from a to b.</summary>
        static float GroundMin(WorldCtx ctx, Vector2 a, Vector2 b, float width)
        {
            Vector2 d = b - a;
            Vector2 r = d.sqrMagnitude > 1e-6f ? new Vector2(d.y, -d.x).normalized * (width * 0.5f) : Vector2.zero;
            float g = float.MaxValue;
            for (int i = 0; i <= 2; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i * 0.5f);
                g = Mathf.Min(g, Mathf.Min(ctx.H(p - r), Mathf.Min(ctx.H(p), ctx.H(p + r))));
            }
            return g;
        }

        /// <summary>A node of a walkway: where, the walking level there, and whether the stretch to the next node is steps.</summary>
        struct WalkNode
        {
            public Vector2 p; public float y; public bool steps;
            public WalkNode(Vector2 p, float y, bool steps) { this.p = p; this.y = y; this.steps = steps; }
        }

        /// <summary>
        /// A walkway: level or gently ramped stretches as paved slabs, stretches marked as steps as stone treads of about
        /// `riser` each. Every piece is walled down to below the lowest ground under it, so on a slope the downhill side
        /// shows a stone face, never a gap. A pad at each bend fills the mitre.
        /// </summary>
        static void Walkway(WorldCtx ctx, SiteMeshes sm, IList<WalkNode> n, float width, MeshBuilder top, MeshBuilder side, float riser = 0.17f)
        {
            for (int i = 0; i + 1 < n.Count; i++)
            {
                var A = n[i];
                var B = n[i + 1];
                float dy = B.y - A.y;
                if (!A.steps || Mathf.Abs(dy) < riser * 0.6f)
                {
                    float g = GroundMin(ctx, A.p, B.p, width);
                    Slab(top, side, A.p, B.p, width, A.y, B.y, Mathf.Min(g, Mathf.Min(A.y, B.y)) - 0.45f);
                    continue;
                }
                int risers = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(dy) / riser));
                int treads = Mathf.Max(1, risers - 1);
                for (int k = 1; k <= treads; k++)
                {
                    Vector2 p0 = Vector2.Lerp(A.p, B.p, (k - 1) / (float)treads), p1 = Vector2.Lerp(A.p, B.p, k / (float)treads);
                    float y = risers == 1 ? A.y : A.y + dy * k / risers;
                    float g = GroundMin(ctx, p0, p1, width);
                    Slab(top, side, p0, p1, width, y, y, Mathf.Min(g, y) - 0.45f);
                }
            }
            for (int i = 1; i + 1 < n.Count; i++)   // the bends
            {
                Vector2 d0 = (n[i].p - n[i - 1].p).normalized, d1 = (n[i + 1].p - n[i].p).normalized;
                if (Vector2.Dot(d0, d1) > 0.995f) continue;
                Vector2 dir = (d0 + d1).sqrMagnitude > 1e-4f ? (d0 + d1).normalized : d1;
                Vector2 a = n[i].p - dir * width * 0.5f, b = n[i].p + dir * width * 0.5f;
                float y = n[i].y - 0.012f;
                Slab(top, side, a, b, width, y, y, Mathf.Min(GroundMin(ctx, a, b, width), y) - 0.45f);
            }
        }

        /// <summary>Points along a walkway's side at `offset` (+ right of the walking direction), at the walking level
        /// (a handrail runs parallel to the pitch of the steps), at most `step` apart.</summary>
        static List<Vector3> WalkSide(IList<WalkNode> n, float offset, float step = 2f, int from = 0, int to = int.MaxValue)
        {
            var pts = new List<Vector3>();
            to = Mathf.Min(to, n.Count - 1);
            for (int i = from; i < to; i++)
            {
                Vector2 d = n[i + 1].p - n[i].p;
                float len = d.magnitude;
                if (len < 1e-3f) continue;
                Vector2 r = new Vector2(d.y, -d.x) / len * offset;
                int k = Mathf.Max(1, Mathf.CeilToInt(len / step));
                for (int j = 0; j < k; j++)
                {
                    float t = j / (float)k;
                    pts.Add(V3(Vector2.Lerp(n[i].p, n[i + 1].p, t) + r, Mathf.Lerp(n[i].y, n[i + 1].y, t)));
                }
                if (i == to - 1) pts.Add(V3(n[i + 1].p + r, n[i + 1].y));
            }
            return pts;
        }

        /// <summary>Pipe railing on posts through base points (walking level): a top pipe at `h`, a middle one at h/2.
        /// Gaps longer than 4 m are left open. Collision in sm.col.</summary>
        static void PipeRail(SiteMeshes sm, IList<Vector3> pts, int pal, float h = 1.0f, float r = 0.03f)
        {
            Vector2 c = S(pal);
            for (int i = 0; i < pts.Count; i++)
            {
                sm.atlas.Cylinder(pts[i] + Vector3.down * 0.2f, r * 1.3f, r * 1.2f, h + 0.25f, 6, c, true);
                if (i == 0) continue;
                Vector3 a = pts[i - 1], b = pts[i];
                if ((b - a).sqrMagnitude > 16f) continue;
                sm.atlas.Limb(a + Vector3.up * h, b + Vector3.up * h, r, r, 6, c, false);
                sm.atlas.Limb(a + Vector3.up * h * 0.5f, b + Vector3.up * h * 0.5f, r * 0.8f, r * 0.8f, 5, c, false);
                Vector3 side = Vector3.Cross(Vector3.up, b - a).normalized;
                sm.col.Quad(a, b, b + Vector3.up * (h + 0.1f), a + Vector3.up * (h + 0.1f), Vector2.zero, side);
                sm.col.Quad(b, a, a + Vector3.up * (h + 0.1f), b + Vector3.up * (h + 0.1f), Vector2.zero, -side);
            }
        }

        /// <summary>Posts with chains hanging between them (the green chain railing on the Eco Cave steps).</summary>
        static void ChainRail(SiteMeshes sm, IList<Vector3> pts, int postPal, int chainPal, float h = 0.85f)
        {
            Vector2 post = S(postPal), chain = S(chainPal);
            for (int i = 0; i < pts.Count; i++)
            {
                sm.atlas.Box(pts[i] + Vector3.up * (h * 0.5f - 0.1f), new Vector3(0.1f, h + 0.2f, 0.1f), post);
                sm.atlas.Sphere(pts[i] + Vector3.up * (h + 0.02f), Vector3.one * 0.07f, 6, 3, post);
                if (i == 0) continue;
                Vector3 a = pts[i - 1] + Vector3.up * (h - 0.06f), b = pts[i] + Vector3.up * (h - 0.06f);
                if ((b - a).sqrMagnitude > 16f) continue;
                Vector3 last = a;
                for (int k = 1; k <= 6; k++)
                {
                    float t = k / 6f;
                    Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * (0.16f * 4f * t * (1f - t));
                    sm.loose.Limb(last, p, 0.012f, 0.012f, 4, chain, false);
                    last = p;
                }
                Vector3 side = Vector3.Cross(Vector3.up, b - a).normalized;
                Vector3 a0 = pts[i - 1], b0 = pts[i];
                sm.col.Quad(a0, b0, b0 + Vector3.up * (h + 0.1f), a0 + Vector3.up * (h + 0.1f), Vector2.zero, side);
                sm.col.Quad(b0, a0, a0 + Vector3.up * (h + 0.1f), b0 + Vector3.up * (h + 0.1f), Vector2.zero, -side);
            }
        }

        /// <summary>
        /// A painted board on two posts: `center` is the board's middle, it faces `yaw`; the lines are solid 3D letters
        /// (Glyph3D) sized by `sizes` (cap height) and shrunk to fit the board. groundY = where the posts stand.
        /// </summary>
        static void Board(SiteMeshes sm, Vector3 center, float yaw, float w, float h, int boardPal, string[] lines, float[] sizes,
                          int textPal, int postPal, float groundY, Glyph3D.Face face = Glyph3D.Face.Block, int[] linePals = null)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 outward = q * Vector3.forward, right = q * Vector3.right;
            sm.atlas.Box(center, new Vector3(w, h, 0.06f), q, S(boardPal));
            sm.atlas.Box(center - outward * 0.005f, new Vector3(w + 0.08f, h + 0.08f, 0.05f), q, S(Pal.ShirtWhite));   // the painted border
            float bottom = center.y - h * 0.5f;
            if (bottom - groundY > 0.05f)
                foreach (float x in new[] { -w * 0.5f + 0.12f, w * 0.5f - 0.12f })
                {
                    Vector3 p = center + right * x - outward * 0.06f;
                    sm.atlas.Box(new Vector3(p.x, (groundY + center.y + h * 0.4f) * 0.5f - 0.15f, p.z), new Vector3(0.08f, center.y + h * 0.4f - groundY + 0.3f, 0.08f), q, S(postPal));
                }
            float total = 0f;
            foreach (float s in sizes) total += s * 1.45f;
            float y = center.y + total * 0.5f;
            for (int i = 0; i < lines.Length; i++)
            {
                float s = sizes[Mathf.Min(i, sizes.Length - 1)];
                y -= s * 0.725f;
                int pal = linePals != null && i < linePals.Length ? linePals[i] : textPal;
                Glyph3D.Write(sm.loose, lines[i], face, new Vector3(center.x, y, center.z) + outward * 0.035f, outward, s, 0.015f, w * 0.9f,
                              S(pal), S(Pal.StoneGrey));
                y -= s * 0.725f;
            }
        }

        /// <summary>A bench: a slab seat on two legs (painted concrete in Kumaon's viewpoints, green and yellow).</summary>
        static void Bench(MeshBuilder mb, Vector3 p, float yaw, int seatPal, int legPal, float len = 1.8f, bool back = true)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            mb.Box(p + q * new Vector3(0f, 0.43f, 0f), new Vector3(len, 0.07f, 0.42f), q, S(seatPal));
            foreach (float x in new[] { -len * 0.5f + 0.18f, len * 0.5f - 0.18f })
                mb.Box(p + q * new Vector3(x, 0.2f, 0f), new Vector3(0.1f, 0.42f, 0.38f), q, S(legPal));
            if (back) mb.Box(p + q * new Vector3(0f, 0.75f, -0.2f), new Vector3(len, 0.32f, 0.06f), q * Quaternion.Euler(-12f, 0f, 0f), S(seatPal));
        }

        /// <summary>
        /// A small hut: plastered walls (photo), a single-pitch corrugated tin roof falling to the back with an overhang,
        /// a door and a window on the front (+z of yaw), standing on a stone plinth down to the ground. Returns the floor level.
        /// </summary>
        static float Hut(WorldCtx ctx, SiteMeshes sm, Vector2 c, float yaw, float w, float d, float h, string wallTex, Color wallTint,
                         Color roofTint, int doorPal, bool window = true, float overhang = 0.6f, float? floorY = null, string roofTex = null)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 X = q * Vector3.right, Z = q * Vector3.forward;
            Vector2 x2 = XZ(X), z2 = XZ(Z);
            float gMax = float.MinValue, gMin = float.MaxValue;
            foreach (float sx in new[] { -0.5f, 0f, 0.5f })
                foreach (float sz in new[] { -0.5f, 0f, 0.5f })
                {
                    float g = ctx.H(c + x2 * (w * sx) + z2 * (d * sz));
                    gMax = Mathf.Max(gMax, g); gMin = Mathf.Min(gMin, g);
                }
            float y0 = floorY ?? gMax + 0.15f;
            var stone = sm.Photo("old_stone_wall", new Color(0.78f, 0.76f, 0.72f));
            Slab(sm.Photo("concrete_floor_worn_001", new Color(0.8f, 0.8f, 0.78f)), stone, c - z2 * (d * 0.5f + 0.15f), c + z2 * (d * 0.5f + 0.15f), w + 0.3f, y0, y0, Mathf.Min(gMin, y0) - 0.5f);
            var wall = sm.Photo(wallTex, wallTint);
            Vector3 o = V3(c, y0);
            Vector3 fl = o - X * w * 0.5f + Z * d * 0.5f, fr = o + X * w * 0.5f + Z * d * 0.5f, br = o + X * w * 0.5f - Z * d * 0.5f, bl = o - X * w * 0.5f - Z * d * 0.5f;
            float hb = h - 0.35f;   // the back wall is lower: the roof falls to the back
            Vector3 up = Vector3.up;
            TQuad(wall, fl, fr, fr + up * h, fl + up * h, 1.2f, Z);
            TQuad(wall, br, bl, bl + up * hb, br + up * hb, 1.2f, -Z);
            wall.Tri(fr, br, br + up * hb, Vector2.zero, new Vector2(d / 1.2f, 0f), new Vector2(d / 1.2f, hb / 1.2f), X);
            wall.Tri(fr, br + up * hb, fr + up * h, Vector2.zero, new Vector2(d / 1.2f, hb / 1.2f), new Vector2(0f, h / 1.2f), X);
            wall.Tri(bl, fl, fl + up * h, Vector2.zero, new Vector2(d / 1.2f, 0f), new Vector2(d / 1.2f, h / 1.2f), -X);
            wall.Tri(bl, fl + up * h, bl + up * hb, Vector2.zero, new Vector2(d / 1.2f, h / 1.2f), new Vector2(0f, hb / 1.2f), -X);
            // the tin roof: a thin sloped slab with an overhang all round, more at the front
            var roof = sm.Photo(roofTex ?? "corrugated_iron_02", roofTint, 0.25f);
            Vector3 rf = o + Z * (d * 0.5f + overhang) + up * (h + 0.06f), rb = o - Z * (d * 0.5f + 0.25f) + up * (hb - 0.02f);
            Vector3 rx = X * (w * 0.5f + 0.25f);
            TQuad(roof, rb - rx, rb + rx, rf + rx, rf - rx, 1.0f, up + Z * 0.1f);
            TQuad(roof, rf - rx + up * -0.05f, rf + rx + up * -0.05f, rb + rx + up * -0.05f, rb - rx + up * -0.05f, 1.0f, -up);
            // door and window: dark openings with painted frames
            Vector3 door = o + Z * (d * 0.5f + 0.02f) + X * (w * 0.22f);
            sm.atlas.Box(door + up * 1.0f, new Vector3(0.95f, 2.0f, 0.04f), q, S(Pal.IronBlack));
            sm.atlas.Box(door + up * 1.0f - Z * 0.005f, new Vector3(1.1f, 2.1f, 0.03f), q, S(doorPal));
            if (window && w > 2.2f)
            {
                Vector3 win = o + Z * (d * 0.5f + 0.02f) - X * (w * 0.24f) + up * 1.35f;
                sm.atlas.Box(win, new Vector3(0.95f, 0.8f, 0.04f), q, S(Pal.IronBlack));
                sm.atlas.Box(win - Z * 0.005f, new Vector3(1.08f, 0.92f, 0.03f), q, S(doorPal));
            }
            return y0;
        }

        /// <summary>Reshapes the terrain inside the box min..max: f(point, current height) gives the new height.</summary>
        static void ShapeTerrain(WorldCtx ctx, Vector2 min, Vector2 max, System.Func<Vector2, float, float> f)
        {
            var td = ctx.terrain.terrainData;
            int res = td.heightmapResolution;
            Vector3 size = td.size, tp = ctx.terrain.transform.position;
            float cx = size.x / (res - 1), cz = size.z / (res - 1);
            int x0 = Mathf.Clamp(Mathf.FloorToInt((min.x - tp.x) / cx), 0, res - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((max.x - tp.x) / cx), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((min.y - tp.z) / cz), 0, res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((max.y - tp.z) / cz), 0, res - 1);
            int w = x1 - x0 + 1, h = z1 - z0 + 1;
            if (w < 2 || h < 2) return;
            var hs = td.GetHeights(x0, z0, w, h);
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector2(tp.x + (x0 + x) * cx, tp.z + (z0 + z) * cz);
                    float y = tp.y + hs[z, x] * size.y;
                    hs[z, x] = Mathf.Clamp01((f(p, y) - tp.y) / size.y);
                }
            td.SetHeights(x0, z0, hs);
        }

        /// <summary>Repaints the ground in the box: f(point, slope in degrees) gives the layer weights (grass, forest floor,
        /// rock, earth) or null to keep what is there.</summary>
        static void PaintTerrain(WorldCtx ctx, Vector2 min, Vector2 max, System.Func<Vector2, float, Vector4?> f)
        {
            var td = ctx.terrain.terrainData;
            if (td.alphamapLayers < 4) return;
            int ar = td.alphamapResolution;
            Vector3 size = td.size, tp = ctx.terrain.transform.position;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((min.x - tp.x) / size.x * (ar - 1)), 0, ar - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((max.x - tp.x) / size.x * (ar - 1)), 0, ar - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((min.y - tp.z) / size.z * (ar - 1)), 0, ar - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((max.y - tp.z) / size.z * (ar - 1)), 0, ar - 1);
            int w = x1 - x0 + 1, h = z1 - z0 + 1;
            if (w < 1 || h < 1) return;
            var al = td.GetAlphamaps(x0, z0, w, h);
            int n = al.GetLength(2);
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector2(tp.x + (x0 + x) / (float)(ar - 1) * size.x, tp.z + (z0 + z) / (float)(ar - 1) * size.z);
                    float gx = ctx.H(p + new Vector2(1.5f, 0f)) - ctx.H(p - new Vector2(1.5f, 0f));
                    float gz = ctx.H(p + new Vector2(0f, 1.5f)) - ctx.H(p - new Vector2(0f, 1.5f));
                    float slope = Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz) / 3f) * Mathf.Rad2Deg;
                    var wts = f(p, slope);
                    if (!wts.HasValue) continue;
                    var v = wts.Value;
                    float s = Mathf.Max(1e-4f, v.x + v.y + v.z + v.w);
                    for (int k = 0; k < n; k++) al[z, x, k] = 0f;
                    al[z, x, 0] = v.x / s; al[z, x, 1] = v.y / s; al[z, x, 2] = v.z / s; al[z, x, 3] = v.w / s;
                }
            td.SetAlphamaps(x0, z0, al);
        }

        /// <summary>Closest point on a polyline: the distance along it (s), the signed side offset (+ = right of the
        /// direction of travel) and the segment index.</summary>
        static void Project(IList<Vector2> line, Vector2 p, out float s, out float side, out int seg, out float dist)
        {
            s = 0f; side = 0f; seg = 0; dist = float.MaxValue;
            float run = 0f;
            for (int i = 0; i + 1 < line.Count; i++)
            {
                Vector2 a = line[i], ab = line[i + 1] - a;
                float len = ab.magnitude;
                if (len < 1e-4f) continue;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / (len * len));
                Vector2 q = a + ab * t;
                float d = Vector2.Distance(p, q);
                if (d < dist)
                {
                    dist = d; seg = i; s = run + t * len;
                    Vector2 r = new Vector2(ab.y, -ab.x) / len;
                    side = Vector2.Dot(p - q, r);
                }
                run += len;
            }
        }

        /// <summary>The polyline of the named road nearest to p (after the corners were rounded).</summary>
        static Vector2[] RoadLine(WorldCtx ctx, string name, Vector2 p, out float halfWidth)
        {
            Vector2[] best = null;
            float bd = float.MaxValue;
            halfWidth = 3.75f;
            foreach (var r in ctx.world.roads)
            {
                if (r.name != name) continue;
                var line = Geo.ToPoints(r.pts);
                Project(line, p, out _, out _, out _, out float d);
                if (d < bd) { bd = d; best = line; halfWidth = r.width * 0.5f; }
            }
            return best;
        }

        /// <summary>A stand of bamboo: thin canes leaning out, leaf sprays on the upper half.</summary>
        static void Bamboo(MeshBuilder mb, Vector3 c, float radius, int canes, System.Random rng)
        {
            Vector2 cane = S(Pal.OakLight), caneOld = S(Pal.PantsKhaki), leaf = S(Pal.DeodarLight), leafDark = S(Pal.Oak);
            for (int i = 0; i < canes; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = radius * Mathf.Sqrt((float)rng.NextDouble());
                Vector3 b = c + new Vector3(Mathf.Cos(a) * r, -0.1f, Mathf.Sin(a) * r);
                float hgt = 6f + (float)rng.NextDouble() * 4f;
                Vector3 lean = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (0.15f + r / radius * 0.25f) + new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * 0.12f;
                Vector3 mid = b + (Vector3.up + lean * 0.5f) * hgt * 0.5f, top = b + (Vector3.up + lean * 1.4f).normalized * hgt;
                Vector2 col = rng.NextDouble() < 0.25 ? caneOld : cane;
                mb.Limb(b, mid, 0.05f, 0.042f, 5, col, false);
                mb.Limb(mid, top, 0.042f, 0.02f, 5, col, false);
                for (int k = 0; k < 7; k++)
                {
                    float t = 0.45f + k * 0.08f;
                    Vector3 p = Vector3.Lerp(b, top, t) + (k > 3 ? (top - mid) * 0.1f : Vector3.zero);
                    float la = (float)rng.NextDouble() * Mathf.PI * 2f;
                    Vector3 out1 = new Vector3(Mathf.Cos(la), -0.35f, Mathf.Sin(la)) * (0.9f + (float)rng.NextDouble() * 0.5f);
                    Vector3 w = Vector3.Cross(out1, Vector3.up).normalized * 0.2f;
                    mb.Tri(p - w, p + w, p + out1, k % 2 == 0 ? leaf : leafDark, Vector3.up);
                    mb.Tri(p + w, p - w, p + out1, k % 2 == 0 ? leaf : leafDark, Vector3.down);
                }
            }
        }
    }
}
