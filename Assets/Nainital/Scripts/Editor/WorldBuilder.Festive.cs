using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    public static partial class WorldBuilder
    {
        // ------------------------------------------------------------------ LED strips, fairy lights, accent lights
        // Owner, 23 Nov 2026: "mall road, choki se naina devi mandir tk lighting add kro path pe ... kahin LED strip lights
        // rkhni ho to rkhlena...lighting mujhe bhdiya chahiye...specially mall raod to khatarnaak". From the night walks
        // (D:\Unity\ref\yt_nov23\mallnight, naina): warm-white strips on the lake railing, trees wrapped in fairy lights (warm,
        // some blue and violet), "jhalar" curtains of coloured bulbs over the shop fronts, a violet strip and white lantern
        // posts on the walk to Naina Devi with little uplights at the foot of the railing, gold outlines on the shrines.
        // The dots are LampGlow halos (one batched additive mesh per 250 m cell, faded in at dusk with the street lamps);
        // a few accent lights (LampManager's pool) put real light where it matters: the bandstand, the walk, the temples.

        /// <summary>LED dots for the night glow: filled by the steps, drawn by BuildLampGlow.</summary>
        internal static readonly List<(Vector3 p, Color c, float s)> ledDots = new List<(Vector3, Color, float)>();
        /// <summary>Real coloured lights (LampManager accent pool): position, colour, range, intensity.</summary>
        internal static readonly List<(Vector3 p, Color c, float range, float intensity)> accentLights = new List<(Vector3, Color, float, float)>();
        /// <summary>The top rail of the Lower Mall's lake railing, in runs (WorldBuilder.Mall.cs MallLakeEdge).</summary>
        internal static readonly List<List<Vector3>> mallRailRuns = new List<List<Vector3>>();

        internal static readonly Color LedWarm = new Color(1f, 0.74f, 0.4f), LedWhite = new Color(0.86f, 0.93f, 1f), LedViolet = new Color(0.62f, 0.22f, 1f),
                                       LedBlue = new Color(0.22f, 0.45f, 1f), LedGold = new Color(1f, 0.66f, 0.2f), LedPink = new Color(1f, 0.28f, 0.66f),
                                       LedGreen = new Color(0.3f, 1f, 0.45f), LedRed = new Color(1f, 0.2f, 0.18f);

        /// <summary>Everything made of cloth that should move in the wind (flags, tarps, awnings, clothes on show): the builders
        /// write it here (LandmarkBuilder.Cloth), FestiveStep makes it one mesh with the wind material (Shaders/FoliageWind).</summary>
        internal static MeshBuilder cloth = new MeshBuilder();

        /// <summary>Moored boats that rise and fall on the swell (the wind shader's bob), and where steam (tea and momo
        /// stalls, eateries) and chimney smoke rise from: AmbientLife reads them from the AmbientSpots it is given.</summary>
        internal static MeshBuilder bob = new MeshBuilder();
        internal static readonly List<Vector3> steamSpots = new List<Vector3>(), chimneySpots = new List<Vector3>();

        static void ClearFestive()
        {
            ledDots.Clear(); accentLights.Clear(); mallRailRuns.Clear(); cloth = new MeshBuilder(); bob = new MeshBuilder();
            steamSpots.Clear(); chimneySpots.Clear();
        }

        /// <summary>A URP particle material (unlit, transparent; additive for sparks) on a small made texture: a soft round
        /// puff (steam, smoke, mist, breath) or a leaf.</summary>
        static Material ParticleMaterial(string name, string texName, bool leaf, bool additive)
        {
            string tp = Gen + "/" + texName + ".png";
            if (!System.IO.File.Exists(tp))
            {
                const int N = 64;
                var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                        float a;
                        if (leaf)   // a pointed leaf along v, a darker vein down the middle
                        {
                            float half = 0.55f * Mathf.Sqrt(Mathf.Max(0f, 1f - v * v)) * (1f - 0.35f * v);
                            a = Mathf.Clamp01((half - Mathf.Abs(u)) * 14f);
                            byte g = (byte)(Mathf.Abs(u) < 0.05f ? 170 : 255);
                            px[y * N + x] = new Color32(g, g, g, (byte)(a * 255));
                            continue;
                        }
                        float d = Mathf.Sqrt(u * u + v * v);
                        a = Mathf.Clamp01(1f - d);
                        a = a * a * (3f - 2f * a);
                        a *= 0.8f + 0.2f * Mathf.PerlinNoise(x * 0.13f, y * 0.13f);
                        px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                t.SetPixels32(px);
                t.Apply();
                System.IO.File.WriteAllBytes(tp, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                UnityEditor.AssetDatabase.ImportAsset(tp, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
                if (UnityEditor.AssetImporter.GetAtPath(tp) is UnityEditor.TextureImporter ti)
                {
                    ti.alphaIsTransparency = true; ti.mipmapEnabled = true; ti.wrapMode = TextureWrapMode.Clamp;
                    ti.SaveAndReimport();
                }
            }
            var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            string path = Gen + "/" + name + ".mat";
            var m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(sh); UnityEditor.AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            UnityEditor.EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>The bob mesh with mb's transform: what is drawn on it rises and falls with the lake's swell.</summary>
        internal static MeshBuilder Bobbing(MeshBuilder mb) { bob.M = mb.M; return bob; }

        /// <summary>The wind material on the atlas (cloth, grass): URP Lit with Shaders/NtWind.hlsl, the atlas's texture.</summary>
        internal static Material WindAtlasMaterial(WorldCtx ctx, string name, float amount, float height, float flutter, float bobMetres = 0f)
        {
            var sh = Shader.Find("Nainital/FoliageWind");
            if (!sh) return ctx.mats.atlas;
            string path = Gen + "/" + name + ".mat";
            var m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(sh); UnityEditor.AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            var tex = ctx.mats.atlas.HasProperty("_BaseMap") ? ctx.mats.atlas.GetTexture("_BaseMap") : null;
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.1f);
            m.SetFloat("_Cull", 0f);
            m.SetFloat("_WindAmount", amount);
            m.SetFloat("_WindHeight", height);
            m.SetFloat("_WindFlutter", flutter);
            m.SetFloat("_WindBob", bobMetres);
            m.enableInstancing = true;
            UnityEditor.EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>A strip of LED dots from a to b.</summary>
        internal static void Led(Vector3 a, Vector3 b, Color c, float spacing = 0.11f, float size = 0.05f, float bright = 2.4f)
        {
            float len = Vector3.Distance(a, b);
            int n = Mathf.Max(1, Mathf.RoundToInt(len / spacing));
            var col = new Color(c.r * bright, c.g * bright, c.b * bright, 1f);
            for (int i = 0; i <= n; i++) ledDots.Add((Vector3.Lerp(a, b, i / (float)n), col, size));
        }

        internal static void LedPath(IList<Vector3> pts, Color c, float spacing = 0.11f, float size = 0.05f, float bright = 2.4f)
        {
            for (int i = 0; i + 1 < pts.Count; i++) Led(pts[i], pts[i + 1], c, spacing, size, bright);
        }

        /// <summary>A bulb (a bigger single dot).</summary>
        internal static void Bulb(Vector3 p, Color c, float size = 0.09f, float bright = 3f) => ledDots.Add((p, new Color(c.r * bright, c.g * bright, c.b * bright, 1f), size));

        internal static void Accent(Vector3 p, Color c, float range, float intensity) => accentLights.Add((p, c, range, intensity));

        internal static void SagString(Vector3 a, Vector3 b, float sag, Color c, float spacing, float size, float bright) => Sag(a, b, sag, c, spacing, size, bright);

        /// <summary>A sagging string of bulbs between two points (fairy lights, a jhalar's top line).</summary>
        static void Sag(Vector3 a, Vector3 b, float sag, Color c, float spacing, float size, float bright, Color? alt = null)
        {
            float len = Vector3.Distance(a, b);
            int n = Mathf.Max(2, Mathf.RoundToInt(len / spacing));
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * sag * 4f * t * (1f - t);
                Color k = alt.HasValue && (i % 2 == 1) ? alt.Value : c;
                ledDots.Add((p, new Color(k.r * bright, k.g * bright, k.b * bright, 1f), size));
            }
        }

        /// <summary>A tree dressed in fairy lights: a spiral up the trunk, strands draped over the crown (per tree type:
        /// deodar cone, chir umbrella, oak dome, cypress flame; TreeKit's crowns), every so often in blue or violet.</summary>
        static int FairyTree(Vector3 foot, int type, float scale, Color c, System.Random rnd)
        {
            int before = ledDots.Count;
            float s = Mathf.Max(0.5f, scale);
            // the trunk: a spiral from 0.6 m to the crown's foot
            float trunkTop = Mathf.Min((type == 1 ? 9f : type == 0 ? 2.4f : type == 2 ? 4.2f : 1.6f) * s, 3.6f);
            float trunkR = (type == 1 ? 0.22f : 0.3f) * s;
            Vector3 last = default;
            for (float h = 0.6f; h < trunkTop; h += 0.04f)
            {
                float a = h * 6.5f;
                Vector3 p = foot + new Vector3(Mathf.Cos(a) * trunkR, h, Mathf.Sin(a) * trunkR);
                if (h > 0.6f && Vector3.Distance(p, last) < 0.2f) continue;
                ledDots.Add((p, new Color(c.r * 2.6f, c.g * 2.6f, c.b * 2.6f, 1f), 0.045f));
                last = p;
            }
            // the crown: strands from the top down the outside, like lights thrown over it
            int strands = type == 3 ? 4 : 7;
            for (int k = 0; k < strands; k++)
            {
                float a = k / (float)strands * Mathf.PI * 2f + (float)rnd.NextDouble() * 0.6f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var pts = new List<Vector3>();
                for (int j = 0; j <= 9; j++)
                {
                    float t = 0.25f + j / 9f * 0.75f;   // the lower three quarters of the crown, where hands could throw them
                    Vector3 p;
                    switch (type)
                    {
                        case 0:  // deodar cone: from the tip at 16 m down to the 3.5 m skirt at 2.4 m
                            p = new Vector3(0f, Mathf.Lerp(15.5f, 2.6f, t), 0f) + dir * Mathf.Lerp(0.3f, 3.6f, t);
                            break;
                        case 1:  // chir umbrella: over the dome of needles
                            {
                                float ang = Mathf.Lerp(0.2f, 1.45f, t);
                                p = new Vector3(0f, 11.4f, 0f) + dir * Mathf.Sin(ang) * 3.0f + Vector3.up * Mathf.Cos(ang) * 1.9f;
                                break;
                            }
                        case 2:  // banj oak: the dome
                            {
                                float ang = Mathf.Lerp(0.15f, 1.7f, t);
                                p = new Vector3(0f, 6.6f, 0f) + dir * Mathf.Sin(ang) * 3.4f + Vector3.up * Mathf.Cos(ang) * 2.7f;
                                break;
                            }
                        default: // cypress
                            p = new Vector3(0f, Mathf.Lerp(11.5f, 1.6f, t), 0f) + dir * (1.3f * Mathf.Sin(Mathf.Lerp(0.15f, 0.95f, t) * Mathf.PI) + 0.2f);
                            break;
                    }
                    p += new Vector3((float)rnd.NextDouble() - 0.5f, ((float)rnd.NextDouble() - 0.5f) * 0.6f, (float)rnd.NextDouble() - 0.5f) * 0.35f;   // loose, not a cage
                    pts.Add(foot + p * s);
                }
                LedPath(pts, c, 0.42f, 0.055f, 2.8f);
            }
            return ledDots.Count - before;
        }

        /// <summary>A jhalar: a line of bulbs with drops hanging from it, the colours running along (Diwali lights over a shop).</summary>
        static void Jhalar(Vector3 a, Vector3 b, Vector3 outward, System.Random rnd, Color[] cols)
        {
            float len = Vector3.Distance(a, b);
            int drops = Mathf.Max(2, Mathf.RoundToInt(len / 0.32f));
            Vector3 o = outward * 0.06f;
            for (int i = 0; i <= drops; i++)
            {
                float t = i / (float)drops;
                Vector3 top = Vector3.Lerp(a, b, t) + o;
                float dl = 0.35f + (float)rnd.NextDouble() * 0.6f;
                Color c = cols[(i / 2) % cols.Length];
                for (float d = 0f; d <= dl; d += 0.13f)
                    ledDots.Add((top + Vector3.down * d, new Color(c.r * 2.4f, c.g * 2.4f, c.b * 2.4f, 1f), 0.04f));
            }
        }

        // ------------------------------------------------------------------ the step
        /// <summary>On the final ground: the strips and posts, then their glow meshes, and the new lamps and accents handed to
        /// the lamp manager the Lighting step made.</summary>
        static void FestiveStep(WorldCtx ctx)
        {
            int lampsBefore = ctx.lampHeads.Count;
            BuildFestiveLights(ctx);
            var lm = Object.FindFirstObjectByType<LampManager>(FindObjectsInactive.Include);   // (the Lighting step puts it under its own root)
            if (lm)
            {
                var newHeads = ctx.lampHeads.GetRange(lampsBefore, ctx.lampHeads.Count - lampsBefore);
                var heads = new List<Vector3>(lm.lampHeads ?? new Vector3[0]);
                var cols = new List<Color>(lm.lampColors ?? new Color[0]);
                heads.AddRange(newHeads);
                cols.AddRange(LampColors(newHeads));
                lm.lampHeads = heads.ToArray();
                lm.lampColors = cols.ToArray();
                lm.accentLights = new Vector3[accentLights.Count];
                lm.accentColors = new Color[accentLights.Count];
                lm.accentRanges = new Vector2[accentLights.Count];
                for (int i = 0; i < accentLights.Count; i++)
                {
                    lm.accentLights[i] = accentLights[i].p;
                    lm.accentColors[i] = accentLights[i].c;
                    lm.accentRanges[i] = new Vector2(accentLights[i].range, accentLights[i].intensity);
                }
                foreach (var h in newHeads) Bulb(h, new Color(1f, 0.85f, 0.6f), 0.9f, 2.8f);   // the new lamps' halos
            }
            else Debug.LogWarning("[Nainital] festive lights: no LampManager");

            // the glow meshes: one per 250 m cell, the lamp glow's material (faded in at dusk with the street lamps)
            var sh = Shader.Find("Nainital/LampGlow");
            var mat = lampGlowMat ? lampGlowMat : UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(Gen + "/M_LampGlow.mat");
            if (!sh || !mat) { Debug.LogWarning("[Nainital] festive lights: no glow material"); return; }
            var cells = new Dictionary<Vector2Int, (List<Vector3> v, List<Color> c, List<Vector2> uv, List<Vector2> uv2, List<int> t)>();
            void Halo(Vector3 at, Color col, float size)
            {
                var key = new Vector2Int(Mathf.FloorToInt(at.x / 250f), Mathf.FloorToInt(at.z / 250f));
                if (!cells.TryGetValue(key, out var m)) { m = (new List<Vector3>(), new List<Color>(), new List<Vector2>(), new List<Vector2>(), new List<int>()); cells[key] = m; }
                int b = m.v.Count;
                for (int k = 0; k < 4; k++) { m.v.Add(at); m.c.Add(col); m.uv2.Add(new Vector2(size, 0f)); }
                m.uv.Add(new Vector2(0, 0)); m.uv.Add(new Vector2(1, 0)); m.uv.Add(new Vector2(1, 1)); m.uv.Add(new Vector2(0, 1));
                m.t.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }
            foreach (var d in ledDots) Halo(d.p, d.c, d.s);
            foreach (var a in accentLights) Halo(a.p, new Color(a.c.r * 1.4f, a.c.g * 1.4f, a.c.b * 1.4f, 1f), 0.3f);
            var root = new GameObject("LED glow (night)").transform;
            root.SetParent(ctx.worldRoot, false);
            int idx = 0;
            foreach (var kv in cells)
            {
                var mesh = new Mesh { name = "LedGlow_" + kv.Key.x + "_" + kv.Key.y, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(kv.Value.v);
                mesh.SetColors(kv.Value.c);
                mesh.SetUVs(0, kv.Value.uv);
                mesh.SetUVs(1, kv.Value.uv2);
                mesh.SetTriangles(kv.Value.t, 0);
                mesh.RecalculateBounds();
                var bnd = mesh.bounds;
                bnd.Expand(4f);
                mesh.bounds = bnd;
                string path = Gen + "/" + mesh.name + ".asset";
                if (System.IO.File.Exists(path)) UnityEditor.AssetDatabase.DeleteAsset(path);
                UnityEditor.AssetDatabase.CreateAsset(mesh, path);
                var go = new GameObject(mesh.name);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                idx++;
            }
            Debug.Log($"[Nainital] LED glow: {ledDots.Count} dots, {accentLights.Count} accent lights, {ctx.lampHeads.Count - lampsBefore} new lamps, {idx} meshes");
            if (cloth.VertexCount > 0)
            {
                ctx.MeshObject("Cloth in the wind (flags, tarps, awnings, clothes)", ctx.worldRoot, cloth, WindAtlasMaterial(ctx, "M_ClothWind", 0.02f, 0f, 0.05f), null, true);
                Debug.Log($"[Nainital] cloth in the wind: {cloth.VertexCount} vertices");
            }
            if (bob.VertexCount > 0)
                ctx.MeshObject("Moored boats on the swell", ctx.worldRoot, bob, WindAtlasMaterial(ctx, "M_BoatBob", 0f, 0f, 0f, 0.05f), null, true);
            var spots = new GameObject("Ambient life (leaves, mist, breath, steam, smoke)").AddComponent<AmbientLife>();
            spots.transform.SetParent(ctx.worldRoot, false);
            spots.steam = steamSpots.ToArray();
            spots.chimneys = chimneySpots.ToArray();
            spots.softMat = ParticleMaterial("M_AmbientSoft", "T_AmbientSoft", false, false);
            spots.leafMat = ParticleMaterial("M_AmbientLeaf", "T_AmbientLeaf", true, false);
            spots.sparkMat = ParticleMaterial("M_AmbientSpark", "T_AmbientSoft", false, true);
            Debug.Log($"[Nainital] ambient: {bob.VertexCount} bobbing boat vertices, {steamSpots.Count} steam spots, {chimneySpots.Count} chimneys");
        }

        static void BuildFestiveLights(WorldCtx ctx)
        {
            var rnd = new System.Random(2311);
            int rail = 0, trees = 0, jhalar = 0, walk = 0;
            int d0 = ledDots.Count;

            // 1. Mall Road: a warm-white strip along the lake railing's top rail, with a bulb on each post
            foreach (var run in mallRailRuns)
            {
                int before = ledDots.Count;
                LedPath(run, LedWarm, 0.12f, 0.045f, 2.2f);
                for (int i = 0; i < run.Count; i += 2) Bulb(run[i] + Vector3.up * 0.04f, LedWhite, 0.07f, 2.6f);
                rail += ledDots.Count - before;
            }

            // 2. the Mall's own trees on the lake bank: fairy lights, mostly warm, every fourth blue / violet
            for (int k = 0; k < bankTrees.Count; k++)
            {
                var b = bankTrees[k];
                Vector2 p = new Vector2(b.x, b.y);
                float y = k < bankTreeY.Count && !float.IsNaN(bankTreeY[k]) ? bankTreeY[k] : ctx.H(p) - 0.15f;
                Color c = (k % 4) == 1 ? LedBlue : (k % 4) == 3 ? LedViolet : LedWarm;
                trees += FairyTree(new Vector3(p.x, y, p.y), (int)b.z, b.w, c, rnd);
            }

            // 3. jhalar over the shop fronts on Mall Road (every other one) and the bazaars: coloured drops from the sign's top
            Color[][] palettes =
            {
                new[] { LedGold, LedWarm }, new[] { LedRed, LedGold, LedGreen, LedBlue }, new[] { LedPink, LedViolet, LedBlue },
                new[] { LedWarm, LedWhite },
            };
            int fi = 0;
            foreach (var (front, n, width) in ctx.shopFronts)
            {
                fi++;
                Vector2 f2 = new Vector2(front.x, front.z);
                if (!NearMallRoad(ctx, f2, 30f) && (fi % 3) != 0) continue;   // all along the Mall, a third elsewhere
                if (NearMallRoad(ctx, f2, 30f))
                {
                    // every Mall shop: a white LED line along the top of its board, cool or warm
                    Vector3 n0 = new Vector3(n.x, 0f, n.z).normalized, al = Vector3.Cross(Vector3.up, n0).normalized;
                    float hw0 = Mathf.Clamp(width, 2f, 9f) * 0.5f - 0.1f;
                    Vector3 top0 = front + Vector3.up * 0.42f + n0 * 0.1f;
                    Led(top0 - al * hw0, top0 + al * hw0, (fi % 3 == 0) ? LedWhite : LedWarm, 0.1f, 0.045f, 2.4f);
                }
                if ((fi % 2) == 1) continue;
                Vector3 nf = new Vector3(n.x, 0f, n.z).normalized;
                Vector3 along = Vector3.Cross(Vector3.up, nf).normalized;
                float w = Mathf.Clamp(width, 2f, 9f) * 0.5f - 0.15f;
                Vector3 top = front + Vector3.up * 0.55f + nf * 0.12f;
                int before = ledDots.Count;
                Jhalar(top - along * w, top + along * w, nf, rnd, palettes[(fi / 2) % palettes.Length]);
                jhalar += ledDots.Count - before;
            }

            // 4. the walk from the Mall to Naina Devi: white lantern posts every 12 m on alternate sides, a violet strip at
            //    railing height along the lake side, little warm uplights at its foot (the 2025 night walk)
            var w2 = MallitalWalk(ctx);
            if (w2 != null && w2.Length > 1)
            {
                var mbL = new MeshBuilder();
                var colL = new MeshBuilder();
                var pts = Resample(w2, 1.5f, false);
                float along = 0f, nextPost = 6f;
                int side = 1;
                Physics.SyncTransforms();
                for (int i = 1; i < pts.Count; i++)
                {
                    Vector2 a = pts[i - 1], b = pts[i];
                    Vector2 t = (b - a).normalized, nrm = new Vector2(-t.y, t.x);
                    // the lake side: the edge nearer the water
                    Vector2 lakeSide = ctx.LakeDistance(a + nrm * 4f) < ctx.LakeDistance(a - nrm * 4f) ? nrm : -nrm;
                    float half = 3.2f;
                    Vector2 ea = a + lakeSide * half, eb = b + lakeSide * half;
                    // (the violet strip and foot lights went on the grass outside the railing and across the road at the
                    // chowki - owner, 23 Nov 2026 - so they now go on the walk's own railing, built with the DSA road's
                    // two levels: WorldBuilder.DsaRoad.cs)
                    along += (b - a).magnitude;
                    if (along >= nextPost)
                    {
                        nextPost += 12f;
                        side = -side;
                        Vector2 q = b + nrm * side * (half + 0.5f);
                        if (ctx.InLake(q) || SolidAt(ctx, q) || Clearance(ctx).Blocked(q, 0.3f)) continue;
                        var basePos = new Vector3(q.x, ctx.H(q), q.y);
                        var rot = Quaternion.LookRotation(new Vector3(-nrm.x * side, 0f, -nrm.y * side));
                        using (PropKit.At(mbL, basePos, rot))
                        {
                            Vector3 bulb = PropKit.RoadLamp(mbL);
                            ctx.lampHeads.Add(basePos + rot * bulb);
                        }
                        colL.Box(basePos + Vector3.up * 2.8f, new Vector3(0.25f, 5.6f, 0.25f), Vector2.zero);
                    }
                }
                var parent = new GameObject("Walk lights (Mall - Naina Devi)").transform;
                parent.SetParent(ctx.worldRoot, false);
                ctx.MeshObject("Walk lamp posts", parent, mbL, ctx.mats.atlas, colL);
            }

            Debug.Log($"[Nainital] festive lights: {ledDots.Count - d0} new dots (Mall railing {rail}, fairy trees {trees} on {bankTrees.Count} trees, " +
                      $"jhalar {jhalar}, walk {walk} m-pieces); {ledDots.Count} dots and {accentLights.Count} accent lights in all");
        }
    }
}
