using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Trees that read as trees: near ones are a bark-textured trunk and branches plus dozens of alpha-cut leaf cards
    /// (broad leaves for banj oak, needles for chir, deodar and cypress) lit with crown-shaped normals; far ones are
    /// three crossed cards carrying a painted picture of the same tree (impostor). Types: 0 deodar, 1 chir pine,
    /// 2 banj oak, 3 cypress (as in TreeField).
    /// </summary>
    public static class TreeKit
    {
        const string Dir = WorldBuilder.Gen;
        public static readonly float[] Height = { 16.5f, 14f, 9.6f, 12.5f };
        static Color32[] leafPx;
        const int LW = 512, LH = 256;   // leaf atlas: left half broad leaves, right half needles

        // ------------------------------------------------------------------ textures
        static Color32[] LeafPixels()
        {
            if (leafPx != null) return leafPx;
            var px = new Color[LW * LH];
            var bg = new Color(0.2f, 0.3f, 0.14f, 0f);
            for (int i = 0; i < px.Length; i++) px[i] = bg;
            var rnd = new System.Random(1717);
            // broad leaves, in a loose round cluster
            for (int i = 0; i < 230; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, rr = Mathf.Sqrt((float)rnd.NextDouble()) * 112f;
                float cx = 128f + Mathf.Cos(a) * rr, cy = 128f + Mathf.Sin(a) * rr;
                float len = 11f + (float)rnd.NextDouble() * 9f, wid = 4.5f + (float)rnd.NextDouble() * 3f, ang = (float)rnd.NextDouble() * Mathf.PI;
                float shade = 0.75f + (float)rnd.NextDouble() * 0.45f - rr / 112f * 0.18f;
                var col = Color.Lerp(new Color(0.16f, 0.28f, 0.1f), new Color(0.38f, 0.52f, 0.2f), (float)rnd.NextDouble()) * shade;
                Ellipse(px, 0, cx, cy, len, wid, ang, col);
            }
            // needles: sprays of thin strokes along short twigs
            for (int tw = 0; tw < 26; tw++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, rr = Mathf.Sqrt((float)rnd.NextDouble()) * 92f;
                Vector2 c = new Vector2(384f + Mathf.Cos(a) * rr, 128f + Mathf.Sin(a) * rr);
                Vector2 dir = new Vector2(Mathf.Cos((float)rnd.NextDouble() * 6.28f), Mathf.Sin((float)rnd.NextDouble() * 6.28f));
                float shade = 0.75f + (float)rnd.NextDouble() * 0.4f;
                for (int k = 0; k < 26; k++)
                {
                    float t = (float)rnd.NextDouble() * 34f - 17f;
                    Vector2 b = c + dir * t;
                    float na = Mathf.Atan2(dir.y, dir.x) + ((k % 2 == 0) ? 1f : -1f) * (0.5f + (float)rnd.NextDouble() * 0.5f);
                    Vector2 e = b + new Vector2(Mathf.Cos(na), Mathf.Sin(na)) * (12f + (float)rnd.NextDouble() * 12f);
                    var col = Color.Lerp(new Color(0.12f, 0.22f, 0.14f), new Color(0.24f, 0.36f, 0.2f), (float)rnd.NextDouble()) * shade;
                    Stroke(px, b, e, 1.3f, col);
                }
            }
            leafPx = new Color32[px.Length];
            for (int i = 0; i < px.Length; i++) leafPx[i] = px[i];
            return leafPx;
        }

        static void Ellipse(Color[] px, int x0, float cx, float cy, float len, float wid, float ang, Color col)
        {
            float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
            int r = Mathf.CeilToInt(len) + 1;
            for (int y = (int)cy - r; y <= (int)cy + r; y++)
                for (int x = (int)cx - r; x <= (int)cx + r; x++)
                {
                    if (x < x0 || x >= x0 + LW / 2 || y < 0 || y >= LH) continue;
                    float dx = x - cx, dy = y - cy, u = (dx * ca + dy * sa) / len, v = (-dx * sa + dy * ca) / wid;
                    float d = u * u + v * v;
                    if (d > 1f) continue;
                    float edge = Mathf.Clamp01((1f - d) * 3f), vein = Mathf.Abs(v) < 0.12f ? 0.85f : 1f;
                    var c = col * vein * (0.8f + 0.2f * (1f - d));
                    c.a = 1f;
                    px[y * LW + x] = Color.Lerp(px[y * LW + x].a > 0.5f ? px[y * LW + x] : c, c, edge);
                }
        }

        static void Stroke(Color[] px, Vector2 a, Vector2 b, float w, Color col)
        {
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - w - 1), x1 = Mathf.CeilToInt(Mathf.Max(a.x, b.x) + w + 1);
            int y0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - w - 1), y1 = Mathf.CeilToInt(Mathf.Max(a.y, b.y) + w + 1);
            Vector2 ab = b - a;
            float l2 = Mathf.Max(ab.sqrMagnitude, 1e-3f);
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(LH - 1, y1); y++)
                for (int x = Mathf.Max(LW / 2, x0); x <= Mathf.Min(LW - 1, x1); x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
                    if (Vector2.Distance(p, a + ab * t) > w) continue;
                    var c = col;
                    c.a = 1f;
                    px[y * LW + x] = c;
                }
        }

        static Texture2D Save(Color32[] px, int w, int h, string name)
        {
            string path = Dir + "/" + name + ".png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = true;
            imp.mipMapsPreserveCoverage = true;
            imp.alphaTestReferenceValue = 0.5f;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>A painted picture of each tree for the far cards: trunk, then the crown stamped from the leaf atlas.</summary>
        static Texture2D ImpostorTexture()
        {
            const int CW = 256, CH = 512, W = CW * 4, TH = 256, AH = CH + TH;   // side views below, top views above them
            var leaf = LeafPixels();
            var px = new Color32[W * AH];
            var bg = new Color32(42, 66, 34, 0);
            for (int i = 0; i < px.Length; i++) px[i] = bg;
            var rnd = new System.Random(2323);
            for (int type = 0; type < 4; type++)
            {
                float H = Height[type], mpp = H / CH;   // metres per pixel
                int ox = type * CW;
                // trunk
                float trunkTop = type == 1 ? 11f : type == 2 ? 5f : type == 0 ? 15f : 2.5f, tr = type == 3 ? 0.25f : 0.36f;
                for (int y = 0; y < CH; y++)
                {
                    float m = y * mpp;
                    if (m > trunkTop) break;
                    float half = Mathf.Lerp(tr, tr * 0.45f, m / trunkTop) / mpp;
                    for (int x = -Mathf.CeilToInt(half); x <= Mathf.CeilToInt(half); x++)
                    {
                        float s = 0.55f + 0.25f * (x / Mathf.Max(half, 1f));   // lit from one side
                        px[y * W + ox + CW / 2 + x] = new Color(0.3f * s, 0.24f * s, 0.18f * s, 1f);
                    }
                }
                // crown: stamps of leaves inside the type's silhouette, darker low and on the shadow side
                bool needles = type != 2;
                int stamps = type == 2 ? 230 : type == 3 ? 170 : 260;
                for (int i = 0; i < stamps; i++)
                {
                    Vector2 c = Inside(type, rnd);   // metres: x across, y up
                    float size = (type == 3 ? 1.0f : 1.7f) + (float)rnd.NextDouble() * 0.9f;
                    float light = 0.6f + 0.35f * Mathf.Clamp01((c.y - 2f) / H * 1.4f) + 0.15f * Mathf.Clamp(c.x / 3f, -1f, 1f);
                    int sx = ox + CW / 2 + Mathf.RoundToInt(c.x / mpp), sy = Mathf.RoundToInt(c.y / mpp), half = Mathf.RoundToInt(size / mpp * 0.5f);
                    for (int y = -half; y < half; y++)
                        for (int x = -half; x < half; x++)
                        {
                            int X = sx + x, Y = sy + y;
                            if (X < ox || X >= ox + CW || Y < 0 || Y >= CH) continue;
                            int lu = (needles ? LW / 2 : 0) + Mathf.Clamp((int)((x + half) / (2f * half) * (LW / 2)), 0, LW / 2 - 1);
                            int lv = Mathf.Clamp((int)((y + half) / (2f * half) * LH), 0, LH - 1);
                            var l = leaf[lv * LW + lu];
                            if (l.a < 128) continue;
                            px[Y * W + X] = new Color32((byte)Mathf.Clamp(l.r * light, 0, 255), (byte)Mathf.Clamp(l.g * light, 0, 255), (byte)Mathf.Clamp(l.b * light, 0, 255), 255);
                        }
                }
            }
            // top views (satellite 2026-11-11: from above the hills are closed canopy): leaves stamped inside the crown's
            // circle, brighter towards the sunny middle
            for (int type = 0; type < 4; type++)
            {
                float R = CrownRadius[type], mpp = 2f * R / TH;
                int ox = type * CW + (CW - TH) / 2;
                bool needles = type != 2;
                for (int i = 0; i < (type == 3 ? 160 : 340); i++)
                {
                    float a = (float)rnd.NextDouble() * Mathf.PI * 2f, rr = Mathf.Sqrt((float)rnd.NextDouble()) * R * 0.92f;
                    Vector2 c = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                    float size = (type == 3 ? 0.9f : 1.6f) + (float)rnd.NextDouble() * 0.9f;
                    float light = 0.55f + 0.45f * (1f - rr / R) + 0.12f * Mathf.Clamp(c.x - c.y, -1f, 1f) / R;
                    int sx = ox + TH / 2 + Mathf.RoundToInt(c.x / mpp), sy = CH + TH / 2 + Mathf.RoundToInt(c.y / mpp), half = Mathf.RoundToInt(size / mpp * 0.5f);
                    for (int y = -half; y < half; y++)
                        for (int x = -half; x < half; x++)
                        {
                            int X = sx + x, Y = sy + y;
                            if (X < ox || X >= ox + TH || Y < CH || Y >= AH) continue;
                            int lu = (needles ? LW / 2 : 0) + Mathf.Clamp((int)((x + half) / (2f * half) * (LW / 2)), 0, LW / 2 - 1);
                            int lv = Mathf.Clamp((int)((y + half) / (2f * half) * LH), 0, LH - 1);
                            var l = leaf[lv * LW + lu];
                            if (l.a < 128) continue;
                            px[Y * W + X] = new Color32((byte)Mathf.Clamp(l.r * light, 0, 255), (byte)Mathf.Clamp(l.g * light, 0, 255), (byte)Mathf.Clamp(l.b * light, 0, 255), 255);
                        }
                }
            }
            return Save(px, W, AH, "T_TreeImpostors");
        }

        /// <summary>Crown radius (m) and the height of its widest part, per type, for the level card.</summary>
        static readonly float[] CrownRadius = { 3.6f, 3.0f, 3.4f, 1.4f };
        static readonly float[] CrownLevel = { 0.5f, 0.82f, 0.68f, 0.55f };

        /// <summary>A random point in the crown of a tree type (metres, x across, y up).</summary>
        static Vector2 Inside(int type, System.Random r)
        {
            float u = (float)r.NextDouble() * 2f - 1f, v = (float)r.NextDouble();
            switch (type)
            {
                case 0:   // deodar: a cone that droops in tiers
                {
                    float y = 2.2f + v * 13.6f, rad = 3.5f * (1f - (y - 2.2f) / 14.5f) * (0.75f + 0.25f * Mathf.Abs(Mathf.Sin(y * 1.6f)));
                    return new Vector2(u * rad, y);
                }
                case 1:   // chir: an open umbrella on a tall bare trunk
                {
                    float y = 9.6f + v * 3.6f, rad = 2.9f * Mathf.Sqrt(1f - Mathf.Pow((y - 11.4f) / 1.9f, 2f) * 0.8f);
                    return new Vector2(u * rad, y);
                }
                case 2:   // banj oak: a broad dome
                {
                    float a = (float)r.NextDouble() * Mathf.PI * 2f, rr = Mathf.Pow((float)r.NextDouble(), 0.5f);
                    return new Vector2(Mathf.Cos(a) * rr * 3.3f, 6.6f + Mathf.Sin(a) * rr * 2.6f);
                }
                default:  // cypress: a narrow flame
                {
                    float y = 1.4f + v * 10.6f, rad = 1.25f * Mathf.Sin(Mathf.Clamp01((y - 1.2f) / 10.8f) * Mathf.PI) + 0.15f;
                    return new Vector2(u * rad, y);
                }
            }
        }

        // ------------------------------------------------------------------ materials
        static Material Cutout(string name, Texture2D tex, float smooth)
        {
            string path = Dir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.SetFloat("_Cull", (float)CullMode.Off);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_SpecularHighlights", 0f);
            m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            m.renderQueue = (int)RenderQueue.AlphaTest;
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material LeafMaterial() => Cutout("M_TreeLeaves", Save(LeafPixels(), LW, LH, "T_TreeLeaves"), 0.12f);
        public static Material ImpostorMaterial() => Cutout("M_TreeImpostors", ImpostorTexture(), 0.05f);

        public static Material BarkMaterial(Material fallback)
        {
            var m = WorldBuilder.PhotoMaterial("bark_brown_02", "M_TreeBark", new Color(0.82f, 0.78f, 0.74f), 0.12f);
            if (m) m.enableInstancing = true;
            return m ? m : fallback;
        }

        // ------------------------------------------------------------------ meshes
        sealed class Build
        {
            public readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> bark = new List<int>(), leaves = new List<int>();

            /// <summary>A tapered cylinder with real bark UVs (u around, v up, ~1 tile per 2 m).</summary>
            public void Limb(Vector3 a, Vector3 b, float ra, float rb, int seg = 7)
            {
                Vector3 up = (b - a).normalized, side = Vector3.Cross(up, Mathf.Abs(up.y) > 0.9f ? Vector3.right : Vector3.up).normalized, fwd = Vector3.Cross(side, up);
                float len = Vector3.Distance(a, b);
                int i0 = v.Count;
                for (int s = 0; s <= seg; s++)
                {
                    float t = s / (float)seg * Mathf.PI * 2f;
                    Vector3 dir = side * Mathf.Cos(t) + fwd * Mathf.Sin(t);
                    v.Add(a + dir * ra); n.Add(dir); uv.Add(new Vector2(s / (float)seg * 1.5f, 0f));
                    v.Add(b + dir * rb); n.Add(dir); uv.Add(new Vector2(s / (float)seg * 1.5f, len / 2f));
                }
                for (int s = 0; s < seg; s++)
                {
                    int q = i0 + s * 2;
                    bark.AddRange(new[] { q, q + 1, q + 3, q, q + 3, q + 2 });
                }
            }

            /// <summary>One leaf card: a square facing roughly away from the crown centre, normals bent outwards.</summary>
            public void Card(Vector3 p, float size, Quaternion rot, Vector3 crown, bool needles)
            {
                Vector3 r = rot * Vector3.right * size * 0.5f, u = rot * Vector3.up * size * 0.5f;
                Vector3 nrm = (p - crown + Vector3.up * 0.6f).normalized;
                float u0 = needles ? 0.5f : 0f, u1 = needles ? 1f : 0.5f;
                int i0 = v.Count;
                Vector3[] q = { p - r - u, p + r - u, p + r + u, p - r + u };
                Vector2[] t = { new Vector2(u0, 0f), new Vector2(u1, 0f), new Vector2(u1, 1f), new Vector2(u0, 1f) };
                for (int k = 0; k < 4; k++) { v.Add(q[k]); n.Add((nrm + (q[k] - crown).normalized * 0.4f).normalized); uv.Add(t[k]); }
                leaves.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                m.SetVertices(v);
                m.SetNormals(n);
                m.SetUVs(0, uv);
                m.subMeshCount = 2;
                m.SetTriangles(bark, 0);
                m.SetTriangles(leaves, 1);
                m.RecalculateBounds();
                return m;
            }
        }

        static Quaternion Facing(Vector3 outward, System.Random r, float tilt)
        {
            outward.y *= 0.5f;
            if (outward.sqrMagnitude < 1e-4f) outward = Vector3.forward;
            return Quaternion.LookRotation(outward.normalized) * Quaternion.Euler(((float)r.NextDouble() - 0.5f) * tilt, ((float)r.NextDouble() - 0.5f) * tilt, (float)r.NextDouble() * 360f);
        }

        /// <summary>The near model of a tree type: bark trunk and branches (submesh 0) and leaf cards (submesh 1).</summary>
        public static Mesh Model(int type)
        {
            var b = new Build();
            var r = new System.Random(500 + type);
            switch (type)
            {
                case 0:   // deodar: straight trunk, drooping tiers of needle sprays
                {
                    b.Limb(Vector3.zero, new Vector3(0, 15.6f, 0), 0.4f, 0.08f);
                    for (int tier = 0; tier < 8; tier++)
                    {
                        float y = 2.4f + tier * 1.75f, rad = 3.6f * (1f - (y - 2.2f) / 14.8f);
                        int count = 7 + (int)(rad * 2.2f);
                        for (int i = 0; i < count; i++)
                        {
                            float a = (i + (float)r.NextDouble() * 0.6f) / count * Mathf.PI * 2f;
                            Vector3 o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                            Vector3 p = new Vector3(0, y - rad * 0.12f, 0) + o * rad * (0.45f + (float)r.NextDouble() * 0.35f);
                            if (i % 3 == 0) b.Limb(new Vector3(0, y, 0), p, 0.06f, 0.02f, 4);
                            var rot = Quaternion.LookRotation(o) * Quaternion.Euler(62f + (float)r.NextDouble() * 18f, 0, (float)r.NextDouble() * 360f);   // lying out and drooping
                            b.Card(p, rad * 0.75f + 0.6f, rot, new Vector3(0, y, 0), true);
                        }
                    }
                    for (int i = 0; i < 4; i++) b.Card(new Vector3(0, 15.4f + i * 0.25f, 0), 1.0f, Quaternion.Euler(0, i * 45f, 0), new Vector3(0, 14f, 0), true);
                    break;
                }
                case 1:   // chir pine: tall bare trunk, a few crooked branches, open umbrella of needles
                {
                    b.Limb(Vector3.zero, new Vector3(0.2f, 11.6f, 0.1f), 0.34f, 0.12f);
                    var crown = new Vector3(0.2f, 11.4f, 0.1f);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i / 6f * Mathf.PI * 2f + (float)r.NextDouble() * 0.5f;
                        Vector3 end = crown + new Vector3(Mathf.Cos(a) * 2.2f, -0.8f + (float)r.NextDouble() * 1.6f, Mathf.Sin(a) * 2.2f);
                        b.Limb(new Vector3(0.2f, 9.4f + i * 0.35f, 0.1f), end, 0.09f, 0.03f, 5);
                    }
                    for (int i = 0; i < 48; i++)
                    {
                        float a = (float)r.NextDouble() * Mathf.PI * 2f, rr = Mathf.Sqrt((float)r.NextDouble()) * 2.8f;
                        Vector3 p = crown + new Vector3(Mathf.Cos(a) * rr, ((float)r.NextDouble() - 0.4f) * 2.6f * (1f - rr / 4f), Mathf.Sin(a) * rr);
                        b.Card(p, 1.4f + (float)r.NextDouble() * 0.8f, Facing(p - crown, r, 70f), crown, true);
                    }
                    break;
                }
                case 2:   // banj oak: short stout trunk, spreading limbs, a broad dome of leaf clusters
                {
                    b.Limb(Vector3.zero, new Vector3(0, 4.4f, 0), 0.4f, 0.26f);
                    var crown = new Vector3(0, 6.6f, 0);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i / 5f * Mathf.PI * 2f + (float)r.NextDouble() * 0.6f;
                        Vector3 end = crown + new Vector3(Mathf.Cos(a) * 2.3f, ((float)r.NextDouble() - 0.3f) * 1.6f, Mathf.Sin(a) * 2.3f);
                        b.Limb(new Vector3(0, 3.6f + i * 0.2f, 0), end, 0.16f, 0.05f, 6);
                    }
                    for (int i = 0; i < 78; i++)
                    {
                        float a = (float)r.NextDouble() * Mathf.PI * 2f, el = Mathf.Asin((float)r.NextDouble() * 1.7f - 0.7f);
                        float shell = Mathf.Pow((float)r.NextDouble(), 0.35f);   // mostly near the surface, like a real crown
                        Vector3 dir = new Vector3(Mathf.Cos(a) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Sin(a) * Mathf.Cos(el));
                        Vector3 p = crown + Vector3.Scale(dir, new Vector3(3.2f, 2.4f, 3.2f)) * shell;
                        b.Card(p, 1.5f + (float)r.NextDouble() * 0.9f, Facing(dir, r, 80f), crown, false);
                    }
                    break;
                }
                default:  // cypress: a narrow dark flame
                {
                    b.Limb(Vector3.zero, new Vector3(0, 10.5f, 0), 0.25f, 0.06f);
                    var crown = new Vector3(0, 6.4f, 0);
                    for (int i = 0; i < 46; i++)
                    {
                        float y = 1.4f + (float)r.NextDouble() * 10.4f, rad = 1.2f * Mathf.Sin(Mathf.Clamp01((y - 1.2f) / 10.8f) * Mathf.PI) + 0.1f;
                        float a = (float)r.NextDouble() * Mathf.PI * 2f;
                        Vector3 p = new Vector3(Mathf.Cos(a) * rad * 0.7f, y, Mathf.Sin(a) * rad * 0.7f);
                        b.Card(p, 0.9f + rad * 0.8f, Facing(new Vector3(Mathf.Cos(a), 0.2f, Mathf.Sin(a)), r, 50f), new Vector3(0, y, 0), true);
                    }
                    break;
                }
            }
            return b.ToMesh("TreeCards" + type);
        }

        /// <summary>The far model: three crossed cards showing this type's picture from the impostor atlas.</summary>
        public static Mesh Billboard(int type)
        {
            float H = Height[type], W = H * 0.5f, u0 = type / 4f, u1 = (type + 1) / 4f, vTop = 512f / 768f;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int k = 0; k < 3; k++)
            {
                var rot = Quaternion.Euler(0, k * 60f, 0);
                Vector3 r = rot * Vector3.right * (W * 0.5f);
                int i0 = v.Count;
                v.Add(-r); v.Add(r); v.Add(r + Vector3.up * H); v.Add(-r + Vector3.up * H);
                Vector3 nrm = (Vector3.up * 0.6f + rot * Vector3.forward * 0.4f).normalized;
                for (int q = 0; q < 4; q++) n.Add(nrm);
                uv.Add(new Vector2(u0, 0f)); uv.Add(new Vector2(u1, 0f)); uv.Add(new Vector2(u1, vTop)); uv.Add(new Vector2(u0, vTop));
                t.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
            }
            {   // the level card: the crown seen from above, so hillsides read as forest from the peaks and from the air
                float R = CrownRadius[type], y = H * CrownLevel[type];
                float tu0 = (type * 256f + 0f) / 1024f, tu1 = (type * 256f + 256f) / 1024f;
                int i0 = v.Count;
                v.Add(new Vector3(-R, y, -R)); v.Add(new Vector3(R, y, -R)); v.Add(new Vector3(R, y, R)); v.Add(new Vector3(-R, y, R));
                for (int q = 0; q < 4; q++) n.Add(Vector3.up);
                uv.Add(new Vector2(tu0, vTop)); uv.Add(new Vector2(tu1, vTop)); uv.Add(new Vector2(tu1, 1f)); uv.Add(new Vector2(tu0, 1f));
                t.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
            }
            var m = new Mesh { name = "TreeImpostor" + type };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
