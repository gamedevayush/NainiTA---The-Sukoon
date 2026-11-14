using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Naina Devi's two gates, from the user's video stills (2026-11-14: "ye dono images ko usme create krdo dhang se").
    /// TempleGate: the temple's own gate. Red, two arched ways with white arch bands edged in yellow, a pointed red gable
    /// with a yellow Om disc, two red name boards sloping up to the peak, a red notice strip across the arches, red iron
    /// leaves standing open. MargGate: the gate on the way to the temple from Mall Road, by the police booth. Pink
    /// sandstone, three ways (the middle one a double scalloped arch), a deep chajja and three domed chhatris with
    /// golden kalash, bells under the arches and a police barricade beside it.
    /// </summary>
    public static partial class WorldBuilder
    {
        static Material SandstoneMaterial()
        {
            string path = GatePrefabDir + "/M_GateSandstone.mat";   // beside the prefab: the Generated folder may be made anew
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", new Color(0.86f, 0.63f, 0.52f));
            m.SetFloat("_Smoothness", 0.12f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void GateShot(Transform parent, string name, Vector3 from, Vector3 lookAt)
        {
            var m = new GameObject(name);
            m.transform.SetParent(parent, false);
            m.transform.SetPositionAndRotation(from, Quaternion.LookRotation(lookAt - from));
        }

        // ------------------------------------------------------------------ the temple's red gate
        static void TempleGate(WorldCtx ctx, Transform parent, MeshBuilder mb, MeshBuilder col, Font font, Vector2 at, Vector2 face2)
        {
            Vector3 face = Flat(face2).normalized, right = Vector3.Cross(Vector3.up, face);
            var rot = Quaternion.LookRotation(face);
            Vector3 o = V(at, ctx.H(at));
            Vector3 W(float x, float y, float z) => o + right * x + Vector3.up * y + face * z;
            Vector2 red = Atlas.SolidUV(Pal.TempleRed), white = Atlas.SolidUV(Pal.TempleWhite), yellow = Atlas.SolidUV(Pal.BoatYellow), gold = Atlas.SolidUV(Pal.Gold);
            const float HalfW = 3.75f, PierW = 1.0f, MidW = 0.8f, Depth = 0.6f, Spring = 2.7f, Rise = 1.0f;
            const float open0 = MidW * 0.5f, open1 = HalfW - PierW, oc = (open0 + open1) * 0.5f, ohw = (open1 - open0) * 0.5f;
            float Top(float x) => 4.5f + 1.9f * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(x) / HalfW), 0.85f);
            float ArchY(float x) { float d = (Mathf.Abs(x) - oc) / ohw; return Mathf.Abs(d) >= 1f ? -1f : Spring + Rise * Mathf.Sqrt(1f - d * d); }
            float slope = Mathf.Atan2(Top(1f) - Top(3f), 2f) * Mathf.Rad2Deg;   // the gable's pitch, for the name boards

            using (PropKit.At(mb, o, rot))
            {
                // the wall: thin upright strips from the ground (or the arch) to the gable line
                const float step = 0.125f;
                for (float x = -HalfW; x < HalfW - 1e-3f; x += step)
                {
                    float xm = x + step * 0.5f, a = ArchY(xm), bottom = a < 0f ? 0f : a, top = Top(xm);
                    mb.Box(new Vector3(xm, (top + bottom) * 0.5f, 0f), new Vector3(step + 0.002f, top - bottom, Depth), red);
                }
                // piers: the two outer ones and the middle one, a little proud of the wall, on white feet
                foreach (float px in new[] { -(HalfW - PierW * 0.5f), 0f, HalfW - PierW * 0.5f })
                {
                    float w = px == 0f ? MidW : PierW;
                    mb.Box(new Vector3(px, Spring * 0.5f, 0f), new Vector3(w + 0.08f, Spring, Depth + 0.24f), red);
                    mb.Box(new Vector3(px, 0.14f, 0f), new Vector3(w + 0.2f, 0.28f, Depth + 0.36f), white);
                }
                foreach (float s in new[] { -1f, 1f })
                {
                    // white jambs inside each way, and the white arch band with its yellow edge
                    mb.Box(new Vector3(s * (open1 + 0.0f), Spring * 0.5f + 0.14f, 0f), new Vector3(0.1f, Spring - 0.28f, Depth + 0.3f), white);
                    mb.Box(new Vector3(s * open0, Spring * 0.5f + 0.14f, 0f), new Vector3(0.1f, Spring - 0.28f, Depth + 0.3f), white);
                    Vector2 c = new Vector2(s * oc, Spring), last = default;
                    for (int k = 0; k <= 14; k++)
                    {
                        float t = k / 14f * Mathf.PI;
                        var p = new Vector2(c.x + Mathf.Cos(t) * ohw, c.y + Mathf.Sin(t) * Rise);
                        if (k > 0)
                        {
                            Vector2 mid = (last + p) * 0.5f, d = p - last, n = new Vector2(-d.y, d.x).normalized;
                            if (Vector2.Dot(n, mid - c) < 0f) n = -n;
                            var r = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                            mb.Box(new Vector3(mid.x + n.x * 0.17f, mid.y + n.y * 0.17f, 0f), new Vector3(d.magnitude + 0.05f, 0.34f, Depth + 0.14f), r, white);
                            mb.Box(new Vector3(mid.x + n.x * 0.39f, mid.y + n.y * 0.39f, 0f), new Vector3(d.magnitude + 0.06f, 0.09f, Depth + 0.2f), r, yellow);
                        }
                        last = p;
                    }
                    // the name board on this side of the gable, sloping up to the peak
                    mb.Box(new Vector3(s * 2.05f, Top(2.05f) + 0.62f, 0f), new Vector3(3.3f, 0.95f, 0.12f), Quaternion.Euler(0f, 0f, -s * slope), red);
                    // the red iron leaf, standing open into the temple side
                    Vector3 hinge = new Vector3(s * (open1 - 0.06f), 0f, -Depth * 0.5f), dir = new Vector3(-s * 0.26f, 0f, -0.97f);
                    for (int k = 0; k < 8; k++) mb.Box(hinge + dir * (0.1f + k * 0.15f) + Vector3.up * 1.3f, new Vector3(0.035f, 2.3f, 0.035f), red);
                    foreach (float ry in new[] { 0.25f, 1.3f, 2.4f }) mb.Limb(hinge + Vector3.up * ry, hinge + dir * 1.2f + Vector3.up * ry, 0.022f, 0.022f, 4, red);
                    // bells under the arch
                    for (int k = -1; k <= 1; k++) mb.Sphere(new Vector3(s * oc + k * 0.45f, Spring + Rise - 0.42f - Mathf.Abs(k) * 0.12f, 0f), new Vector3(0.09f, 0.12f, 0.09f), 6, 4, gold);
                    mb.Sphere(new Vector3(s * (HalfW - PierW * 0.5f), 1.9f, Depth * 0.5f + 0.14f), new Vector3(0.2f, 0.22f, 0.1f), 8, 4, gold);   // lion mask
                }
                // the yellow edge along the gable, and the pointed block at the peak
                for (float x = -HalfW; x < HalfW - 1e-3f; x += 0.375f)
                {
                    Vector2 p0 = new Vector2(x, Top(x)), p1 = new Vector2(x + 0.375f, Top(x + 0.375f)), d = p1 - p0, mid = (p0 + p1) * 0.5f;
                    mb.Box(new Vector3(mid.x, mid.y + 0.04f, 0f), new Vector3(d.magnitude + 0.04f, 0.13f, Depth + 0.16f), Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg), yellow);
                }
                mb.Box(new Vector3(0f, Top(0f) + 0.2f, 0f), new Vector3(0.5f, 0.5f, Depth), Quaternion.Euler(0f, 0f, 45f), red);
                foreach (float fz in new[] { -1f, 1f })
                {
                    // the Om disc: yellow on a gold rim, a red six-pointed star on it
                    float z = fz * (Depth * 0.5f + 0.02f);
                    mb.Sphere(new Vector3(0f, 4.7f, z), new Vector3(0.8f, 0.8f, 0.05f), 18, 4, gold);
                    mb.Sphere(new Vector3(0f, 4.7f, z + fz * 0.03f), new Vector3(0.68f, 0.68f, 0.06f), 18, 4, yellow);
                    for (int tri = 0; tri < 2; tri++)
                    {
                        Vector3 P(float deg) => new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad) * 0.42f, 4.7f + Mathf.Sin(deg * Mathf.Deg2Rad) * 0.42f, z + fz * 0.1f);
                        float a0 = tri == 0 ? 90f : 270f;
                        mb.Tri(P(a0), P(a0 + 120f), P(a0 + 240f), red, new Vector3(0f, 0f, fz));
                    }
                    mb.Box(new Vector3(0f, Spring + 0.27f, fz * (Depth * 0.5f + 0.2f)), new Vector3(HalfW * 2f - 0.7f, 0.44f, 0.05f), red);   // the notice strip across both ways
                    mb.Sphere(new Vector3(0f, 1.55f, fz * (Depth * 0.5f + 0.14f)), new Vector3(0.22f, 0.26f, 0.1f), 8, 4, gold);            // lion mask on the middle pier
                }
            }
            using (PropKit.At(col, o, rot))
                foreach (float px in new[] { -(HalfW - PierW * 0.5f), 0f, HalfW - PierW * 0.5f })
                    col.Box(new Vector3(px, 2f, 0f), new Vector3(px == 0f ? MidW : PierW, 4f, Depth + 0.24f), Vector2.zero);

            // the gate stands in a lane between prasad shops (user's video, 8:42 and 11:32): a one-storey block either
            // side, its front flush with the gate, a yellow board over shutters and a tin roof sloping back
            Vector2 wall = Atlas.WallSolid(3), board = Atlas.SolidUV(Pal.BoatYellow), shutter = Atlas.SolidUV(Pal.StoneGrey),
                    tin = Atlas.SolidUV(Pal.Concrete), counter = Atlas.SolidUV(Pal.WoodLight);
            using (PropKit.At(mb, o, rot))
            using (PropKit.At(col, o, rot))
                foreach (float s in new[] { -1f, 1f })
                {
                    float cx = s * (HalfW + 2.7f);
                    const float SW = 5.2f, SD = 6f, SH = 3.3f;
                    var c0 = new Vector3(cx, 0f, -SD * 0.5f + 0.35f);
                    mb.Box(c0 + Vector3.up * (SH * 0.5f), new Vector3(SW, SH, SD), wall);
                    col.Box(c0 + Vector3.up * (SH * 0.5f), new Vector3(SW, SH, SD), Vector2.zero);
                    mb.Box(new Vector3(cx, SH - 0.45f, 0.4f), new Vector3(SW - 0.1f, 0.8f, 0.08f), board);              // name board
                    mb.Box(new Vector3(cx, 1.2f, 0.37f), new Vector3(SW - 0.9f, 2.2f, 0.04f), shutter);                // open shop front
                    mb.Box(new Vector3(cx, 0.45f, 0.75f), new Vector3(SW - 1.2f, 0.9f, 0.7f), counter);                // prasad counter
                    var tilt = Quaternion.Euler(-8f, 0f, 0f);
                    mb.Box(new Vector3(cx, SH + 0.35f, -SD * 0.5f + 0.6f), new Vector3(SW + 0.4f, 0.08f, SD + 0.9f), tilt, tin);   // tin roof
                }

            var holder = new GameObject("Naina Devi gate").transform;
            holder.SetParent(parent, false);
            var cream = new Color(1f, 0.97f, 0.9f);
            foreach (float fz in new[] { -1f, 1f })
            {
                Vector3 f = face * fz;
                foreach (float s in new[] { -1f, 1f })
                {
                    // seen from the front the words read left to right: the viewer's left is the gate's +x
                    string words = s * fz > 0f ? "SHRI MAA NAINA" : "DEVI MANDIR";
                    Label(holder, font, words, W(s * 2.05f, Top(2.05f) + 0.62f, fz * 0.08f), f, 0.5f, cream);
                    var tt = holder.GetChild(holder.childCount - 1);
                    tt.RotateAround(tt.position, face, -s * slope);
                }
                Label(holder, font, "KRIPYA MARYADIT EVAM SHALEEN VASTRON MEIN HI MANDIR MEIN PRAVESH KAREN", W(0f, Spring + 0.27f, fz * (Depth * 0.5f + 0.23f)), f, 0.17f, cream);
            }
            GateShot(parent, "GateShotTemple", W(1.2f, 1.7f, 10.5f), W(0f, 3.3f, 0f));
        }

        // ------------------------------------------------------------------ the sandstone gate by the police booth
        /// <summary>Where the gate stands (user, 2026-11-14: "vo road ke just side me hai, tumne andar kar diya; chowki bhi
        /// gate ke side, both are attached with Mall Road"): at the mouth of the walk to the temple, right at the edge of
        /// Mall Road where Mall Road, Upper Mall and Lower Mall meet, its front to the road.</summary>
        static bool MargGateSite(WorldCtx ctx, out Vector2 at, out Vector2 face)
        {
            at = default;
            face = Vector2.up;
            var temple = ctx.Poi("Naina Devi Temple") ?? ctx.Poi("Naina Devi Gate");
            if (!temple.HasValue) return false;
            // the Mall's carriageways and how far each one's paving reaches from its centre line
            var mall = new System.Collections.Generic.List<(Vector2[] line, float reach)>();
            foreach (var r in ctx.world.roads)
            {
                if (string.IsNullOrEmpty(r.name)) continue;
                float reach = r.name == MallLower ? 6.4f : r.name == MallUpper ? MallHalf + MallStrip : r.name == "Mall Road" ? r.width * 0.5f : -1f;
                if (reach > 0f) mall.Add((Geo.ToPoints(r.pts), reach));
            }
            if (mall.Count == 0) return false;
            float Clear(Vector2 p)
            {
                float c = float.MaxValue;
                foreach (var (line, reach) in mall) c = Mathf.Min(c, Geo.DistanceToPolyline(line, p, false) - reach);
                return c;
            }
            // the walk to the temple: a footway or pedestrian way with one end on the Mall, heading for the temple
            Vector2[] walk = null;
            float bestScore = float.MinValue;
            foreach (var r in ctx.world.roads)
            {
                if (r.kind != "pedestrian" && r.kind != "footway" && r.kind != "living_street") continue;
                var l = Geo.ToPoints(r.pts);
                if (l.Length < 2) continue;
                if (Clear(l[l.Length - 1]) < Clear(l[0])) System.Array.Reverse(l);   // start on the Mall
                if (Clear(l[0]) > 1f) continue;
                float score = Vector2.Dot((l[Mathf.Min(2, l.Length - 1)] - l[0]).normalized, (temple.Value - l[0]).normalized) + r.width * 0.05f;
                if (score > bestScore) { bestScore = score; walk = l; }
            }
            if (walk == null) return false;
            // walk in until the gate (1.5 m deep, its front 0.3 m off the paving) is clear of every Mall carriageway
            var pts = Resample(walk, 0.5f, false);
            int at_i = pts.Count - 1;
            for (int i = 1; i < pts.Count; i++) if (Clear(pts[i]) > 1.05f) { at_i = i; break; }
            at = pts[at_i];
            face = (pts[Mathf.Max(0, at_i - 3)] - at).normalized;   // back down the walk, to the road
            if (face.sqrMagnitude < 0.5f) face = (walk[0] - at).normalized;
            return true;
        }

        // ------------------------------------------------------------------ the gate as a prefab, placed by hand
        // User, 2026-11-14: "shaktipeeth gate ko detach karo, main manually place kar dunga, ghante and all; tum theek jagah
        // set nahi kar paa rahe". The gate and its police chowki are a prefab (Assets/Nainital/Prefabs). It is made once and
        // then left alone, so changes made to the prefab by hand stay (Nainital > Shaktipeeth gate: remake prefab starts it
        // over). It uses its own materials: the world's atlas material is made new on every build.
        public const string GatePrefabDir = Root + "/Prefabs";
        public const string GatePrefabPath = GatePrefabDir + "/ShaktipeethGate.prefab";
        const int PGold = 0, PDark = 1, PYellow = 2, PRed = 3, PWhite = 4, PBlue = 5;
        static Vector2 PalUV(int i) => new Vector2((i + 0.5f) / 8f, 0.5f);

        static Material GatePaletteMaterial()
        {
            string tex = GatePrefabDir + "/T_GatePalette.png", path = GatePrefabDir + "/M_GatePalette.mat";
            System.IO.Directory.CreateDirectory(GatePrefabDir);
            var px = new Color32[]
            {
                new Color32(214, 166, 52, 255), new Color32(22, 22, 24, 255), new Color32(240, 196, 30, 255), new Color32(160, 26, 26, 255),
                new Color32(236, 236, 228, 255), new Color32(32, 64, 150, 255), new Color32(255, 242, 216, 255), new Color32(128, 128, 128, 255),
            };
            var t = new Texture2D(8, 1, TextureFormat.RGBA32, false);
            t.SetPixels32(px);
            t.Apply();
            System.IO.File.WriteAllBytes(tex, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(tex);
            if (AssetImporter.GetAtPath(tex) is TextureImporter imp)
            {
                imp.filterMode = FilterMode.Point;
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(m);
            return m;
        }

        [MenuItem("Nainital/Shaktipeeth gate: remake prefab", priority = 40)]
        public static void RemakeGatePrefab()
        {
            MakeGatePrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("[Nainital] Shaktipeeth gate prefab made: " + GatePrefabPath);
        }

        static GameObject MakeGatePrefab()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Vector3 o = Vector3.zero, face = Vector3.forward;
            var rot = Quaternion.identity;
            Vector3 W(float x, float y, float z) => new Vector3(x, y, z);
            Vector2 gold = PalUV(PGold), dark = PalUV(PDark), yellow = PalUV(PYellow), z0 = Vector2.zero;
            var mb = new MeshBuilder();
            var col = new MeshBuilder();
            float[] pierX = { -6.2f, -2.8f, 2.8f, 6.2f };
            const float PW = 1.0f, PD = 1.5f, PH = 4.4f, Beam = 4.45f;
            // the underside of the arches: a scalloped arch in each side way, two meeting at a drop in the middle way
            float ArchY(float x)
            {
                float ax = Mathf.Abs(x);
                if (ax < 2.3f) { float d = (ax - 1.15f) / 1.15f; return 3.2f + 1.0f * Mathf.Sqrt(Mathf.Max(0f, 1f - d * d)) - 0.12f * Mathf.Abs(Mathf.Sin(d * Mathf.PI * 2.5f)); }
                if (ax > 3.3f && ax < 5.7f) { float d = (ax - 4.5f) / 1.2f; return 3.0f + 1.2f * Mathf.Sqrt(Mathf.Max(0f, 1f - d * d)) - 0.13f * Mathf.Abs(Mathf.Sin(d * Mathf.PI * 2.5f)); }
                return -1f;
            }

            var st = new MeshBuilder();
            using (PropKit.At(st, o, rot))
            using (PropKit.At(mb, o, rot))
            {
                foreach (float px in pierX)
                {
                    st.Box(new Vector3(px, 0.3f, 0f), new Vector3(PW + 0.36f, 0.6f, PD + 0.36f), z0);
                    st.Box(new Vector3(px, (PH + 0.6f) * 0.5f, 0f), new Vector3(PW, PH - 0.6f, PD), z0);
                    st.Box(new Vector3(px, PH - 0.12f, 0f), new Vector3(PW + 0.22f, 0.24f, PD + 0.22f), z0);
                    foreach (float fz in new[] { -1f, 1f })   // a raised panel on each face, as carved
                        st.Box(new Vector3(px, 2.4f, fz * (PD * 0.5f + 0.03f)), new Vector3(PW * 0.55f, 2.6f, 0.06f), z0);
                }
                const float step = 0.1f;
                for (float x = -5.7f; x < 5.7f - 1e-3f; x += step)
                {
                    float xm = x + step * 0.5f, a = ArchY(xm);
                    if (a < 0f) continue;
                    st.Box(new Vector3(xm, (a + Beam) * 0.5f, 0f), new Vector3(step + 0.002f, Beam - a, 0.55f), z0);
                }
                st.Box(new Vector3(0f, Beam + 0.45f, 0f), new Vector3(13.9f, 0.9f, PD + 0.1f), z0);          // the beam over the piers
                st.Box(new Vector3(0f, Beam + 0.98f, 0f), new Vector3(15.1f, 0.16f, 2.6f), z0);             // the chajja
                st.Box(new Vector3(0f, Beam + 0.82f, 0f), new Vector3(14.5f, 0.18f, 2.0f), z0);
                st.Box(new Vector3(0f, Beam + 1.24f, 0f), new Vector3(14.1f, 0.36f, 1.8f), z0);             // the parapet the chhatris stand on

                float Chhatri(float cx, float baseY, float r, float drumH)
                {
                    st.Cylinder(new Vector3(cx, baseY, 0f), r, r, drumH, 8, z0, true, false, 1f, Mathf.PI / 8f);
                    st.Cylinder(new Vector3(cx, baseY + drumH, 0f), r * 1.3f, r * 1.2f, 0.14f, 8, z0, true, true, 1f, Mathf.PI / 8f);
                    st.Sphere(new Vector3(cx, baseY + drumH + 0.14f, 0f), new Vector3(r * 1.05f, r * 0.7f, r * 1.05f), 12, 5, z0, 0f, 90f);
                    foreach (float fz in new[] { -1f, 1f })   // a dark arched opening in the drum, front and back
                        mb.Box(new Vector3(cx, baseY + drumH * 0.5f, fz * (r * Mathf.Cos(Mathf.PI / 8f) + 0.02f)), new Vector3(r * 0.38f, drumH * 0.62f, 0.05f), dark);
                    return baseY + drumH + 0.14f + r * 0.7f;
                }
                void Kalash(float cx, float y)
                {
                    mb.Sphere(new Vector3(cx, y + 0.14f, 0f), new Vector3(0.17f, 0.2f, 0.17f), 8, 4, gold);
                    mb.Cylinder(new Vector3(cx, y + 0.3f, 0f), 0.07f, 0f, 0.5f, 6, gold);
                }
                float roof = Beam + 1.42f;
                float mid1 = Chhatri(0f, roof, 1.9f, 1.2f);
                Kalash(0f, Chhatri(0f, mid1 - 0.35f, 1.0f, 0.8f) - 0.05f);
                foreach (float s in new[] { -1f, 1f }) Kalash(s * 4.6f, Chhatri(s * 4.6f, roof, 1.5f, 0.6f) - 0.05f);

                // bells hanging under the arches
                foreach (float bx in new[] { -1.75f, -1.15f, -0.55f, 0f, 0.55f, 1.15f, 1.75f, -4.5f, 4.5f })
                {
                    float top = ArchY(bx), by = top - 0.5f - (bx == 0f ? 0.1f : 0f);
                    mb.Limb(new Vector3(bx, top, 0f), new Vector3(bx, by + 0.1f, 0f), 0.012f, 0.012f, 3, dark, false);
                    mb.Sphere(new Vector3(bx, by, 0f), new Vector3(0.1f, 0.13f, 0.1f), 6, 4, gold);
                }
                foreach (float fz in new[] { -1f, 1f })   // the name plaque over the middle way
                    mb.Box(new Vector3(0f, Beam + 0.45f, fz * (PD * 0.5f + 0.08f)), new Vector3(3.4f, 0.4f, 0.04f), gold);
                // a police barricade standing in front of one side way
                mb.Box(new Vector3(4.5f, 0.85f, 2.6f), new Vector3(2.3f, 1.0f, 0.05f), yellow);
                foreach (float lx in new[] { 3.45f, 5.55f }) mb.Box(new Vector3(lx, 0.65f, 2.6f), new Vector3(0.06f, 1.3f, 0.5f), yellow);
            }
            using (PropKit.At(col, o, rot))
            {
                foreach (float px in pierX) col.Box(new Vector3(px, 2.2f, 0f), new Vector3(PW, 4.4f, PD), Vector2.zero);
                col.Box(new Vector3(4.5f, 0.65f, 2.6f), new Vector3(2.3f, 1.3f, 0.5f), Vector2.zero);
            }

            System.IO.Directory.CreateDirectory(GatePrefabDir);
            var root = new GameObject("Shaktipeeth gate (Naina Devi marg)");
            var gate = new GameObject("Gate").transform;
            gate.SetParent(root.transform, false);
            foreach (float fz in new[] { -1f, 1f })
                Label(gate, font, "SHRI MAA NAINA DEVI MANDIR DWAR", W(0f, Beam + 0.45f, fz * (PD * 0.5f + 0.1f)), face * fz, 0.22f, new Color(0.35f, 0.08f, 0.05f));
            Label(gate, font, "UTTARAKHAND POLICE", W(4.5f, 1.05f, 2.63f), face, 0.2f, new Color(0.08f, 0.12f, 0.45f));
            Label(gate, font, "STOP", W(4.5f, 0.68f, 2.63f), face, 0.26f, new Color(0.75f, 0.08f, 0.08f));

            // the police chowki: its own child, so it can be moved beside the gate on its own
            var chowki = new GameObject("Police chowki").transform;
            chowki.SetParent(root.transform, false);
            var cb = new MeshBuilder();
            var cc = new MeshBuilder();
            {
                Vector2 red = PalUV(PRed), white = PalUV(PWhite), blue = PalUV(PBlue);
                cb.Box(Vector3.up * 0.5f, new Vector3(2.6f, 1f, 2.2f), red);
                cb.Box(Vector3.up * 1.7f, new Vector3(2.4f, 1.4f, 2.0f), white);
                cb.Box(Vector3.up * 1.75f + face * 1.01f, new Vector3(1.6f, 0.8f, 0.04f), dark);
                cb.Box(Vector3.up * 2.55f, new Vector3(2.9f, 0.18f, 2.5f), red);
                cb.Box(Vector3.up * 3.3f + face * 0.6f, new Vector3(2.8f, 1.1f, 0.08f), blue);
                cc.Box(Vector3.up * 1.3f, new Vector3(2.6f, 2.6f, 2.2f), Vector2.zero);
                Label(chowki, font, "POLICE SAHAYATA KENDRA", Vector3.up * 3.45f + face * 0.65f, face, 0.2f, Color.white);
                Label(chowki, font, "NAINITAL", Vector3.up * 3.05f + face * 0.65f, face, 0.24f, new Color(1f, 0.9f, 0.4f));
            }
            chowki.localPosition = new Vector3(9.4f, 0f, 0.3f);

            // the meshes live in one asset next to the prefab
            string bankPath = GatePrefabDir + "/ShaktipeethGate_meshes.asset";
            if (System.IO.File.Exists(bankPath)) AssetDatabase.DeleteAsset(bankPath);
            var bank = ScriptableObject.CreateInstance<AssetBank>();
            AssetDatabase.CreateAsset(bank, bankPath);
            var palette = GatePaletteMaterial();
            void Part(Transform parent, string name, MeshBuilder b, Material mat, MeshBuilder collider)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                var m = b.ToMesh(name);
                AssetDatabase.AddObjectToAsset(m, bank);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                if (collider != null && collider.VertexCount > 0)
                {
                    var cm = collider.ToMesh(name + "_col");
                    AssetDatabase.AddObjectToAsset(cm, bank);
                    go.AddComponent<MeshCollider>().sharedMesh = cm;
                }
            }
            Part(gate, "Sandstone", st, SandstoneMaterial(), col);
            Part(gate, "Bells, kalash and barricade", mb, palette, null);
            Part(chowki, "Chowki", cb, palette, cc);
            AssetDatabase.SaveAssets();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, GatePrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
