using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Writes the BUILT world (Assets/Nainital/Scenes/Nainital.unity) for the Unreal project, so Unity stays the source of
    /// truth and Unreal re-imports it with one command (D:\NainiTA-Unreal\sync.ps1).
    ///
    /// Batch: Unity -batchmode -quit -projectPath . -executeMethod Nainital.EditorTools.UnrealExport.Batch [-ntExport D:\out]
    ///
    /// Output (default D:\NainiTA-Unreal\Export):
    ///   manifest.json             everything below, with Unity coordinates (x east, y up, z north, metres)
    ///   Terrain/heights.r16       16-bit little-endian heights, res x res, ROW 0 = NORTH (max z), column 0 = west
    ///   Terrain/splat0.png        terrain layer weights (RGBA = layers 0-3), top row = north, like every map texture here
    ///   Terrain/road_mask.png     roads painted into the terrain (RGBA = asphalt, cement, grey pavers, red pavers)
    ///   Meshes/*.glb              every static mesh, merged per category and 250 m chunk, one primitive per material.
    ///                             glTF positions = (x, y, -z) relative to the chunk origin (the manifest's "origin"), so the
    ///                             Unreal glTF importer's (X, Z, Y) swap gives UE X = east, Y = south, Z = up.
    ///   Trees/tree_N.glb          one mesh per tree type (local, same axes); Trees/trees.bin float32 x,y,z,type,scale,yawDeg
    ///   Textures/*                every texture a material uses (copied from the source file when it is a png/jpg)
    /// </summary>
    public static partial class UnrealExport
    {
        public const string DefaultOut = @"D:\NainiTA-Unreal\Export";
        const float ChunkSize = 250f;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        [MenuItem("Nainital/Unreal: export built world", priority = 40)]
        public static void Menu() => Run(OutDir());

        public static void Batch()
        {
            try { Run(OutDir()); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>Batch for sync.ps1: build the world, then export it, in one Unity start.</summary>
        public static void BuildAndExportBatch()
        {
            try
            {
                ProjectSetup.Run();
                WorldBuilder.Build();
                Run(OutDir());
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static string OutDir()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "-ntExport") return args[i + 1];
            var env = Environment.GetEnvironmentVariable("NT_UNREAL_EXPORT");
            return string.IsNullOrEmpty(env) ? DefaultOut : env;
        }

        // ------------------------------------------------------------------ state of one run
        static string outDir;
        static Dictionary<Texture, string> texFiles;
        static Dictionary<Material, string> matIds;
        static Dictionary<int, MeshArrays> meshCache;
        static J matsJson;
        static int texCopied, texBaked;

        public static void Run(string dir)
        {
            var t0 = DateTime.Now;
            outDir = dir;
            texFiles = new Dictionary<Texture, string>();
            matIds = new Dictionary<Material, string>();
            meshCache = new Dictionary<int, MeshArrays>();
            matsJson = J.Obj();
            texCopied = texBaked = 0;

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != WorldBuilder.ScenePath)
                scene = EditorSceneManager.OpenScene(WorldBuilder.ScenePath, OpenSceneMode.Single);

            Directory.CreateDirectory(outDir);
            foreach (var sub in new[] { "Meshes", "Textures", "Terrain", "Trees", "Characters", "Network" })
            {
                var p = Path.Combine(outDir, sub);
                if (Directory.Exists(p)) Directory.Delete(p, true);   // re-runnable: the export always matches the scene
                Directory.CreateDirectory(p);
            }

            var world = JsonUtility.FromJson<WorldJson>(File.ReadAllText(WorldBuilder.Root + "/Data/world.json"));
            var man = J.Obj();
            man["version"] = 1;
            man["exportedAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Inv);
            man["unity"] = Application.unityVersion;
            man["gitCommit"] = GitHead();
            man["axes"] = "Unity: x east, y up, z north (metres). Unreal: X = x*100, Y = -z*100, Z = y*100 (cm).";
            man["chunkSize"] = ChunkSize;
            var meta = world.meta;
            man["world"] = J.Obj().Set("sizeM", meta.sizeM).Set("hMin", meta.hMin).Set("hMax", meta.hMax).Set("lakeLevel", meta.lakeLevel)
                .Set("mapCx", meta.mapCx).Set("mapCz", meta.mapCz).Set("centerLat", meta.centerLat).Set("centerLon", meta.centerLon);

            Terrain terrain = null;
            Step("terrain", () => terrain = ExportTerrain(man));
            Step("meshes", () => ExportMeshes(man));
            Step("trees", () => ExportTrees(man));
            Step("lights", () => ExportLights(man));
            Step("lake + markers", () => ExportLakeAndMarkers(man, world, terrain));
            Step("characters", () => ExportCharacters(man));
            Step("crowd", () => ExportCrowd(man));
            man["materials"] = matsJson;

            File.WriteAllText(Path.Combine(outDir, "manifest.json"), man.ToString(), new UTF8Encoding(false));
            Debug.Log($"[UnrealExport] DONE in {(DateTime.Now - t0).TotalSeconds:F0}s -> {outDir} ({matIds.Count} materials, {texFiles.Count} textures: {texCopied} copied, {texBaked} baked)");
        }

        static void Step(string name, Action a)
        {
            var t = DateTime.Now;
            a();
            Debug.Log($"[UnrealExport] {name}: {(DateTime.Now - t).TotalSeconds:F1}s");
        }

        static string GitHead()
        {
            try
            {
                string head = File.ReadAllText(".git/HEAD").Trim();
                if (head.StartsWith("ref: "))
                {
                    var rp = Path.Combine(".git", head.Substring(5));
                    if (File.Exists(rp)) return File.ReadAllText(rp).Trim();
                    var packed = File.Exists(".git/packed-refs") ? File.ReadAllLines(".git/packed-refs") : new string[0];
                    foreach (var l in packed) if (l.EndsWith(head.Substring(5))) return l.Split(' ')[0];
                }
                return head;
            }
            catch { return ""; }
        }

        // ------------------------------------------------------------------ terrain
        static Terrain ExportTerrain(J man)
        {
            var terrain = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.terrainData).OrderByDescending(t => t.terrainData.size.x).FirstOrDefault();
            if (!terrain) { Debug.LogWarning("[UnrealExport] no terrain in the scene"); return null; }
            var td = terrain.terrainData;
            int res = td.heightmapResolution;
            var h = td.GetHeights(0, 0, res, res);   // [z, x]
            var bytes = new byte[res * res * 2];
            int k = 0;
            for (int z = res - 1; z >= 0; z--)      // row 0 = north
                for (int x = 0; x < res; x++)
                {
                    int v = Mathf.Clamp(Mathf.RoundToInt(h[z, x] * 65535f), 0, 65535);
                    bytes[k++] = (byte)(v & 0xff);
                    bytes[k++] = (byte)(v >> 8);
                }
            File.WriteAllBytes(Path.Combine(outDir, "Terrain/heights.r16"), bytes);

            // layer weights, 4 per png
            int ar = td.alphamapResolution, nl = td.alphamapLayers;
            var splats = J.Arr();
            for (int baseL = 0; baseL < nl; baseL += 4)
            {
                var px = new Color32[ar * ar];
                const int Band = 256;
                for (int y0 = 0; y0 < ar; y0 += Band)
                {
                    int rows = Mathf.Min(Band, ar - y0);
                    var a = td.GetAlphamaps(0, y0, ar, rows);
                    for (int yy = 0; yy < rows; yy++)
                        for (int x = 0; x < ar; x++)
                        {
                            byte C(int l) => l < nl ? (byte)Mathf.Clamp(Mathf.RoundToInt(a[yy, x, l] * 255f), 0, 255) : (byte)0;
                            px[(y0 + yy) * ar + x] = new Color32(C(baseL), C(baseL + 1), C(baseL + 2), C(baseL + 3));
                        }
                }
                // Texture2D row 0 is the bottom (south); EncodeToPNG writes it at the bottom, so the png's top row is north
                var tex = new Texture2D(ar, ar, TextureFormat.RGBA32, false, true);
                tex.SetPixels32(px);
                tex.Apply(false);
                string f = $"Terrain/splat{baseL / 4}.png";
                File.WriteAllBytes(Path.Combine(outDir, f), tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                splats.Add(f);
            }

            var layers = J.Arr();
            foreach (var l in td.terrainLayers)
            {
                if (!l) { layers.Add(null); continue; }
                layers.Add(J.Obj().Set("name", l.name)
                    .Set("diffuse", Tex(l.diffuseTexture)).Set("normal", Tex(l.normalMapTexture, true)).Set("mask", Tex(l.maskMapTexture))
                    .Set("tileSize", V2(l.tileSize)).Set("tileOffset", V2(l.tileOffset))
                    .Set("normalScale", l.normalScale).Set("metallic", l.metallic).Set("smoothness", l.smoothness)
                    .Set("diffuseRemapMax", V4(l.diffuseRemapMax)).Set("specular", Col(l.specular)));
            }

            var tj = J.Obj();
            tj["name"] = terrain.name;
            tj["heights"] = "Terrain/heights.r16";
            tj["res"] = res;
            tj["position"] = V3(terrain.transform.position);   // the south-west corner at y = 0 of the height range
            tj["size"] = V3(td.size);                          // world y = position.y + h * size.y
            tj["splat"] = splats;
            tj["splatRes"] = ar;
            tj["layers"] = layers;
            tj["material"] = terrain.materialTemplate ? Mat(terrain.materialTemplate) : null;

            var roads = terrain.GetComponent<TerrainRoads>();
            if (roads)
            {
                string maskFile = null;
                string mp = roads.mask ? AssetDatabase.GetAssetPath(roads.mask) : null;
                if (!string.IsNullOrEmpty(mp) && File.Exists(mp))
                {
                    maskFile = "Terrain/road_mask.png";
                    File.Copy(mp, Path.Combine(outDir, maskFile), true);
                }
                tj["roads"] = J.Obj().Set("mask", maskFile).Set("rect", V4(roads.rect)).Set("tiles", V4(roads.tiles))
                    .Set("asphalt", Tex(roads.asphalt)).Set("asphaltNormal", Tex(roads.asphaltNormal, true))
                    .Set("concrete", Tex(roads.concrete)).Set("paver", Tex(roads.paver)).Set("paverNormal", Tex(roads.paverNormal, true))
                    .Set("dirt", Tex(roads.dirt)).Set("redTint", Col(roads.redTint)).Set("greyTint", Col(roads.greyTint))
                    .Set("channels", "r asphalt, g cement, b grey pavers, a red pavers");
            }
            man["terrain"] = tj;
            Debug.Log($"[UnrealExport] terrain {res}^2 heights, {nl} layers at {ar}^2, size {td.size}, at {terrain.transform.position}");
            return terrain;
        }

        // ------------------------------------------------------------------ meshes
        class MeshArrays
        {
            public Vector3[] v, n;
            public Vector2[] uv;
            public Color32[] c;
            public int[][] sub;   // null entry = not triangles
        }

        class Prim
        {
            public Material mat;
            public readonly List<Vector3> p = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<Color32> c = new List<Color32>();
            public readonly List<int> idx = new List<int>();
            public bool anyColor;
        }

        class Group
        {
            public string name, category, kind;
            public Vector2Int cell;
            public Vector3 origin;
            public int objects;
            public readonly Dictionary<Material, Prim> prims = new Dictionary<Material, Prim>();
            public readonly List<string> objectNames = new List<string>();
        }

        static MeshArrays Arrays(Mesh m)
        {
            if (meshCache.TryGetValue(m.GetInstanceID(), out var a)) return a;
            a = new MeshArrays();
            try
            {
                a.v = m.vertices;
                a.n = m.normals;
                a.uv = m.uv;
                a.c = m.colors32;
                a.sub = new int[m.subMeshCount][];
                for (int s = 0; s < m.subMeshCount; s++)
                    a.sub[s] = m.GetTopology(s) == MeshTopology.Triangles ? m.GetTriangles(s) : null;
                if (a.v == null || a.v.Length == 0) a = null;
            }
            catch (Exception e) { Debug.LogWarning($"[UnrealExport] mesh {m.name} unreadable: {e.Message}"); a = null; }
            meshCache[m.GetInstanceID()] = a;
            return a;
        }

        static string Category(Transform t)
        {
            // the first level under the scene root; under "World (real Nainital data)" the level below that
            var chain = new List<Transform>();
            for (var p = t; p; p = p.parent) chain.Add(p);
            chain.Reverse();
            if (chain.Count > 1 && chain[0].name.StartsWith("World")) return chain[1].name;
            return chain[0].name;
        }

        static bool IsWater(Material m) => m && m.shader && m.shader.name.Contains("Water");

        static void ExportMeshes(J man)
        {
            var skipLod = new HashSet<Renderer>();
            foreach (var lg in UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var lods = lg.GetLODs();
                for (int i = 1; i < lods.Length; i++) foreach (var r in lods[i].renderers) if (r) skipLod.Add(r);
            }

            var groups = new Dictionary<string, Group>();
            int used = 0, skipped = 0, glow = 0, unreadable = 0;
            // hierarchy order, so the same scene always writes byte-identical chunk files (Unreal re-imports only what changed)
            var renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .OrderBy(r => HierarchyKey(r.transform), System.StringComparer.Ordinal).ToArray();
            foreach (var r in renderers)
            {
                if (!r.enabled || skipLod.Contains(r) || r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) { skipped++; continue; }
                if (r.GetComponentInParent<Rigidbody>() || r.GetComponentInParent<Animator>()) { skipped++; continue; }   // vehicles, people: gameplay, not world
                var mf = r.GetComponent<MeshFilter>();
                var mesh = mf ? mf.sharedMesh : null;
                if (!mesh) { skipped++; continue; }
                var mats = r.sharedMaterials;
                if (mats.Length == 0 || !mats[0]) { skipped++; continue; }
                string sh = mats[0].shader ? mats[0].shader.name : "";
                if (sh.Contains("LampGlow") || sh.Contains("Particles")) { glow++; continue; }   // night glow cards: Unreal lights do this
                var a = Arrays(mesh);
                if (a == null) { unreadable++; continue; }

                string cat = Category(r.transform);
                bool water = IsWater(mats[0]);
                var c = r.bounds.center;
                var cell = new Vector2Int(Mathf.FloorToInt(c.x / ChunkSize), Mathf.FloorToInt(c.z / ChunkSize));
                string kind = water ? "water" : "static";
                string key = Safe(cat) + "__" + cell.x + "_" + cell.y + (water ? "__water" : "");
                if (!groups.TryGetValue(key, out var g))
                {
                    g = new Group { name = key, category = cat, kind = kind, cell = cell, origin = new Vector3((cell.x + 0.5f) * ChunkSize, 0, (cell.y + 0.5f) * ChunkSize) };
                    groups[key] = g;
                }
                g.objects++;
                if (g.objectNames.Count < 400) g.objectNames.Add(r.name);
                Append(g, a, r.localToWorldMatrix, mats);
                used++;
            }

            var meshes = J.Arr();
            foreach (var g in groups.Values.OrderBy(x => x.name))
            {
                if (g.prims.Count == 0) continue;
                string file = "Meshes/" + g.name + ".glb";
                var prims = g.prims.Values.Where(p => p.idx.Count > 0).ToList();
                var slots = prims.Select(p => Mat(p.mat)).ToList();
                WriteGlb(Path.Combine(outDir, file), g.name, prims, slots);
                int verts = prims.Sum(p => p.p.Count), tris = prims.Sum(p => p.idx.Count) / 3;
                var mj = J.Obj().Set("name", g.name).Set("file", file).Set("category", g.category).Set("kind", g.kind)
                    .Set("cell", J.Arr().Add(g.cell.x).Add(g.cell.y)).Set("origin", V3(g.origin))
                    .Set("verts", verts).Set("tris", tris).Set("objects", g.objects);
                var sj = J.Arr(); foreach (var s in slots) sj.Add(s);
                mj["materials"] = sj;
                var nj = J.Arr(); foreach (var s in g.objectNames.Distinct()) nj.Add(s);
                mj["objectNames"] = nj;
                meshes.Add(mj);
            }
            man["meshes"] = meshes;
            Debug.Log($"[UnrealExport] meshes: {used} renderers into {groups.Count} chunk files; skipped {skipped} (LOD>0, shadow-only, vehicles, people), {glow} glow cards, {unreadable} unreadable");
        }

        static void Append(Group g, MeshArrays a, Matrix4x4 l2w, Material[] mats)
        {
            var m = Matrix4x4.Translate(-g.origin) * l2w;
            var nm = l2w.inverse.transpose;
            bool flip = l2w.determinant < 0;
            for (int s = 0; s < a.sub.Length; s++)
            {
                var tri = a.sub[s];
                if (tri == null || tri.Length == 0) continue;
                var mat = mats[Mathf.Min(s, mats.Length - 1)];
                if (!mat) continue;
                if (!g.prims.TryGetValue(mat, out var p)) { p = new Prim { mat = mat }; g.prims[mat] = p; }
                // only the vertices this submesh uses
                var remap = new Dictionary<int, int>();
                int baseCount = p.p.Count;
                for (int i = 0; i < tri.Length; i++)
                {
                    int vi = tri[i];
                    if (!remap.TryGetValue(vi, out int ni))
                    {
                        ni = p.p.Count;
                        remap[vi] = ni;
                        p.p.Add(m.MultiplyPoint3x4(a.v[vi]));
                        var n = a.n != null && a.n.Length == a.v.Length ? nm.MultiplyVector(a.n[vi]).normalized : Vector3.up;
                        p.n.Add(n);
                        p.uv.Add(a.uv != null && a.uv.Length == a.v.Length ? a.uv[vi] : Vector2.zero);
                        if (a.c != null && a.c.Length == a.v.Length) { p.c.Add(a.c[vi]); p.anyColor = true; }
                        else p.c.Add(new Color32(255, 255, 255, 255));
                    }
                }
                for (int i = 0; i + 2 < tri.Length; i += 3)
                {
                    int i0 = remap[tri[i]], i1 = remap[tri[i + 1]], i2 = remap[tri[i + 2]];
                    if (flip) { p.idx.Add(i0); p.idx.Add(i2); p.idx.Add(i1); }
                    else { p.idx.Add(i0); p.idx.Add(i1); p.idx.Add(i2); }
                }
            }
        }

        static string Safe(string s)
        {
            var sb = new StringBuilder();
            foreach (var ch in s)
                sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            var r = sb.ToString();
            while (r.Contains("__")) r = r.Replace("__", "_");
            r = r.Trim('_');
            return r.Length > 48 ? r.Substring(0, 48) : r;
        }

        // ------------------------------------------------------------------ glTF binary
        /// <summary>One mesh, one primitive per material slot. Unity (x, y, z) -> glTF (x, y, -z); winding reversed (the
        /// mirror), v flipped. Materials carry the slot name only; Unreal builds the real materials from manifest.json.</summary>
        static void WriteGlb(string path, string name, List<Prim> prims, List<string> slotNames)
        {
            var bin = new MemoryStream();
            var bw = new BinaryWriter(bin);
            var views = J.Arr();
            var accessors = J.Arr();
            var jprims = J.Arr();

            int View(long start, long len, int target)
            {
                var v = J.Obj().Set("buffer", 0).Set("byteOffset", start).Set("byteLength", len);
                if (target != 0) v["target"] = target;
                views.Add(v);
                return views.Count - 1;
            }
            void Pad() { while (bin.Length % 4 != 0) bw.Write((byte)0); }

            for (int pi = 0; pi < prims.Count; pi++)
            {
                var p = prims[pi];
                int n = p.p.Count;
                // positions
                Pad(); long s0 = bin.Length;
                Vector3 mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), mx = -mn;
                for (int i = 0; i < n; i++)
                {
                    var v = p.p[i]; var g = new Vector3(v.x, v.y, -v.z);
                    mn = Vector3.Min(mn, g); mx = Vector3.Max(mx, g);
                    bw.Write(g.x); bw.Write(g.y); bw.Write(g.z);
                }
                int aPos = accessors.Count;
                accessors.Add(J.Obj().Set("bufferView", View(s0, bin.Length - s0, 34962)).Set("componentType", 5126).Set("count", n).Set("type", "VEC3")
                    .Set("min", J.Arr().Add(mn.x).Add(mn.y).Add(mn.z)).Set("max", J.Arr().Add(mx.x).Add(mx.y).Add(mx.z)));
                // normals
                s0 = bin.Length;
                for (int i = 0; i < n; i++) { var v = p.n[i]; bw.Write(v.x); bw.Write(v.y); bw.Write(-v.z); }
                int aNrm = accessors.Count;
                accessors.Add(J.Obj().Set("bufferView", View(s0, bin.Length - s0, 34962)).Set("componentType", 5126).Set("count", n).Set("type", "VEC3"));
                // uv
                s0 = bin.Length;
                for (int i = 0; i < n; i++) { var v = p.uv[i]; bw.Write(v.x); bw.Write(1f - v.y); }
                int aUv = accessors.Count;
                accessors.Add(J.Obj().Set("bufferView", View(s0, bin.Length - s0, 34962)).Set("componentType", 5126).Set("count", n).Set("type", "VEC2"));
                int aCol = -1;
                if (p.anyColor)
                {
                    s0 = bin.Length;
                    for (int i = 0; i < n; i++) { var c = p.c[i]; bw.Write(c.r); bw.Write(c.g); bw.Write(c.b); bw.Write(c.a); }
                    aCol = accessors.Count;
                    accessors.Add(J.Obj().Set("bufferView", View(s0, bin.Length - s0, 34962)).Set("componentType", 5121).Set("normalized", true).Set("count", n).Set("type", "VEC4"));
                }
                // indices (reversed winding)
                Pad(); s0 = bin.Length;
                for (int i = 0; i + 2 < p.idx.Count; i += 3) { bw.Write((uint)p.idx[i]); bw.Write((uint)p.idx[i + 2]); bw.Write((uint)p.idx[i + 1]); }
                int aIdx = accessors.Count;
                accessors.Add(J.Obj().Set("bufferView", View(s0, bin.Length - s0, 34963)).Set("componentType", 5125).Set("count", p.idx.Count).Set("type", "SCALAR"));

                var attrs = J.Obj().Set("POSITION", aPos).Set("NORMAL", aNrm).Set("TEXCOORD_0", aUv);
                if (aCol >= 0) attrs["COLOR_0"] = aCol;
                jprims.Add(J.Obj().Set("attributes", attrs).Set("indices", aIdx).Set("material", pi).Set("mode", 4));
            }
            Pad();

            var mats = J.Arr();
            for (int i = 0; i < prims.Count; i++)
            {
                var col = prims[i].mat.HasProperty("_BaseColor") ? prims[i].mat.GetColor("_BaseColor") : (prims[i].mat.HasProperty("_Color") ? prims[i].mat.GetColor("_Color") : Color.white);
                mats.Add(J.Obj().Set("name", slotNames[i]).Set("pbrMetallicRoughness", J.Obj()
                    .Set("baseColorFactor", J.Arr().Add(col.linear.r).Add(col.linear.g).Add(col.linear.b).Add(1f)).Set("metallicFactor", 0f).Set("roughnessFactor", 0.8f)));
            }

            var root = J.Obj();
            root["asset"] = J.Obj().Set("version", "2.0").Set("generator", "NainiTA UnrealExport");
            root["scene"] = 0;
            root["scenes"] = J.Arr().Add(J.Obj().Set("nodes", J.Arr().Add(0)));
            root["nodes"] = J.Arr().Add(J.Obj().Set("name", name).Set("mesh", 0));
            root["meshes"] = J.Arr().Add(J.Obj().Set("name", name).Set("primitives", jprims));
            root["materials"] = mats;
            root["buffers"] = J.Arr().Add(J.Obj().Set("byteLength", bin.Length));
            root["bufferViews"] = views;
            root["accessors"] = accessors;

            var json = Encoding.UTF8.GetBytes(root.ToString());
            int jsonPad = (4 - json.Length % 4) % 4;
            using (var f = new BinaryWriter(File.Create(path)))
            {
                uint total = (uint)(12 + 8 + json.Length + jsonPad + 8 + bin.Length);
                f.Write(0x46546C67u); f.Write(2u); f.Write(total);
                f.Write((uint)(json.Length + jsonPad)); f.Write(0x4E4F534Au); f.Write(json); for (int i = 0; i < jsonPad; i++) f.Write((byte)0x20);
                f.Write((uint)bin.Length); f.Write(0x004E4942u); bin.Position = 0; bin.CopyTo(f.BaseStream);
            }
        }

        // ------------------------------------------------------------------ materials and textures
        static string Mat(Material m)
        {
            if (!m) return null;
            if (matIds.TryGetValue(m, out var id)) return id;
            id = "M_" + Safe(m.name);
            string baseId = id; int k = 2;
            while (matIds.ContainsValue(id)) id = baseId + "_" + k++;
            matIds[m] = id;

            var j = J.Obj();
            j["unityName"] = m.name;
            j["shader"] = m.shader ? m.shader.name : "";
            j["renderQueue"] = m.renderQueue;
            j["keywords"] = J.Arr().AddAll(m.shaderKeywords);
            var tex = J.Obj(); var cols = J.Obj(); var floats = J.Obj(); var vecs = J.Obj();
            var sh = m.shader;
            if (sh)
                for (int i = 0; i < sh.GetPropertyCount(); i++)
                {
                    string pn = sh.GetPropertyName(i);
                    switch (sh.GetPropertyType(i))
                    {
                        case ShaderPropertyType.Texture:
                            var t = m.GetTexture(pn);
                            if (t) tex[pn] = J.Obj().Set("file", Tex(t, pn.Contains("Bump") || pn.Contains("Normal")))
                                .Set("scale", V2(m.GetTextureScale(pn))).Set("offset", V2(m.GetTextureOffset(pn)));
                            break;
                        case ShaderPropertyType.Color: cols[pn] = Col(m.GetColor(pn)); break;
                        case ShaderPropertyType.Float:
                        case ShaderPropertyType.Range: floats[pn] = m.GetFloat(pn); break;
                        case ShaderPropertyType.Vector: vecs[pn] = V4(m.GetVector(pn)); break;
                    }
                }
            j["textures"] = tex; j["colors"] = cols; j["floats"] = floats; j["vectors"] = vecs;
            matsJson[id] = j;
            return id;
        }

        /// <summary>Texture file relative to the export folder: the source png/jpg copied, or the texture baked to png.</summary>
        static string Tex(Texture t, bool normal = false)
        {
            if (!t) return null;
            if (texFiles.TryGetValue(t, out var f)) return f;
            string ap = AssetDatabase.GetAssetPath(t);
            string ext = Path.GetExtension(ap).ToLowerInvariant();
            string guid = string.IsNullOrEmpty(ap) ? t.GetInstanceID().ToString("x8") : AssetDatabase.AssetPathToGUID(ap).Substring(0, 8);
            string stem = Safe(t.name) + "_" + guid;
            try
            {
                if (t is Texture2D && (ext == ".png" || ext == ".jpg" || ext == ".jpeg") && File.Exists(ap) && AssetDatabase.IsMainAsset(t))
                {
                    f = "Textures/" + stem + ext;
                    File.Copy(ap, Path.Combine(outDir, f), true);
                    texCopied++;
                }
                else if (t is Texture2D t2)
                {
                    f = "Textures/" + stem + ".png";
                    File.WriteAllBytes(Path.Combine(outDir, f), Bake(t2, normal, ap));
                    texBaked++;
                }
                else return null;   // arrays, cubemaps, render textures
            }
            catch (Exception e) { Debug.LogWarning($"[UnrealExport] texture {t.name} ({ap}): {e.Message}"); f = null; }
            texFiles[t] = f;
            return f;
        }

        static byte[] Bake(Texture2D t, bool normal, string assetPath)
        {
            bool srgb = true;
            if (AssetImporter.GetAtPath(assetPath) is TextureImporter imp) { srgb = imp.sRGBTexture && imp.textureType != TextureImporterType.NormalMap; normal |= imp.textureType == TextureImporterType.NormalMap; }
            if (normal) srgb = false;
            int w = Mathf.Min(t.width, 4096), h = Mathf.Min(t.height, 4096);
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, srgb ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
            var prev = RenderTexture.active;
            Graphics.Blit(t, rt);
            RenderTexture.active = rt;
            var o = new Texture2D(w, h, TextureFormat.RGBA32, false, !srgb);
            o.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            o.Apply(false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            if (normal)
            {
                // Unity stores normal maps as AG (DXT5nm) or RG (BC5): rebuild a plain tangent-space RGB map
                bool ag = t.format == TextureFormat.DXT5 || t.format == TextureFormat.DXT5Crunched || t.format == TextureFormat.RGBA32 || t.format == TextureFormat.ARGB32;
                var px = o.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                {
                    float x = (ag ? px[i].a : px[i].r) / 255f * 2f - 1f, y = px[i].g / 255f * 2f - 1f;
                    float z = Mathf.Sqrt(Mathf.Clamp01(1f - x * x - y * y));
                    px[i] = new Color32((byte)((x * 0.5f + 0.5f) * 255f), (byte)((y * 0.5f + 0.5f) * 255f), (byte)((z * 0.5f + 0.5f) * 255f), 255);
                }
                o.SetPixels32(px);
                o.Apply(false);
            }
            var bytes = o.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(o);
            return bytes;
        }

        // ------------------------------------------------------------------ trees
        static void ExportTrees(J man)
        {
            var tr = UnityEngine.Object.FindObjectsByType<TreeRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(x => x.field);
            if (!tr) { Debug.LogWarning("[UnrealExport] no TreeRenderer"); return; }
            var field = tr.field;
            var types = J.Arr();
            for (int t = 0; t < field.lod0.Length; t++)
            {
                var mesh = field.lod0[t];
                if (!mesh) { types.Add(null); continue; }
                bool cards = field.cards != null && t < field.cards.Length && field.cards[t];
                var mats = cards ? new[] { tr.barkMaterial, tr.leafMaterial } : new[] { tr.material };
                var a = Arrays(mesh);
                if (a == null) { types.Add(null); continue; }
                var g = new Group { name = "tree_" + t, origin = Vector3.zero };
                Append(g, a, Matrix4x4.identity, mats);
                var prims = g.prims.Values.Where(p => p.idx.Count > 0).ToList();
                var slots = prims.Select(p => Mat(p.mat)).ToList();
                string file = $"Trees/tree_{t}.glb";
                WriteGlb(Path.Combine(outDir, file), g.name, prims, slots);
                var sj = J.Arr(); foreach (var s in slots) sj.Add(s);
                types.Add(J.Obj().Set("file", file).Set("cards", cards).Set("materials", sj)
                    .Set("trunkRadius", field.trunkRadius != null && t < field.trunkRadius.Length ? field.trunkRadius[t] : 0.3f)
                    .Set("trunkHeight", field.trunkHeight != null && t < field.trunkHeight.Length ? field.trunkHeight[t] : 3f)
                    .Set("lod1", field.lod1 != null && t < field.lod1.Length && field.lod1[t] ? field.lod1[t].name : null));
            }
            var bytes = new byte[field.data.Length * 4];
            Buffer.BlockCopy(field.data, 0, bytes, 0, bytes.Length);
            File.WriteAllBytes(Path.Combine(outDir, "Trees/trees.bin"), bytes);
            man["trees"] = J.Obj().Set("count", field.Count).Set("instances", "Trees/trees.bin").Set("stride", 6)
                .Set("layout", "float32 x, y, z (Unity world), type, uniform scale, yaw degrees about +y").Set("types", types)
                .Set("impostorMaterial", tr.impostorMaterial ? Mat(tr.impostorMaterial) : null)
                .Set("lod0Distance", tr.lod0Distance).Set("lod1Distance", tr.lod1Distance).Set("maxDistance", tr.maxDistance);
            Debug.Log($"[UnrealExport] trees: {field.Count} instances, {field.lod0.Length} types");
        }

        // ------------------------------------------------------------------ lights
        static void ExportLights(J man)
        {
            var arr = J.Arr();
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                arr.Add(J.Obj().Set("name", l.name).Set("path", PathOf(l.transform)).Set("type", l.type.ToString()).Set("enabled", l.enabled && l.gameObject.activeInHierarchy)
                    .Set("position", V3(l.transform.position)).Set("forward", V3(l.transform.forward)).Set("rotation", V4(new Vector4(l.transform.rotation.x, l.transform.rotation.y, l.transform.rotation.z, l.transform.rotation.w)))
                    .Set("color", Col(l.color)).Set("intensity", l.intensity).Set("range", l.range).Set("spotAngle", l.spotAngle)
                    .Set("colorTemperature", l.useColorTemperature ? l.colorTemperature : 0f).Set("shadows", l.shadows.ToString()));
            }
            man["lights"] = arr;

            // the sun the game uses at each hour (DayNightCycle), so Unreal's sun matches for the same hour
            var dn = UnityEngine.Object.FindAnyObjectByType<DayNightCycle>();
            if (dn && dn.sun)
            {
                float keep = dn.timeOfDay;
                var hours = J.Arr();
                for (float hr = 0f; hr < 24f; hr += 0.5f)
                {
                    dn.timeOfDay = hr;
                    dn.Apply();
                    hours.Add(J.Obj().Set("hour", hr).Set("forward", V3(dn.sun.transform.forward)).Set("color", Col(dn.sun.color))
                        .Set("intensity", dn.sun.intensity).Set("night", dn.NightFactor).Set("elevation", dn.SunElevation)
                        .Set("ambientSky", Col(RenderSettings.ambientSkyColor)).Set("fogColor", Col(RenderSettings.fogColor)).Set("fogDensity", RenderSettings.fogDensity));
                }
                dn.timeOfDay = keep;
                dn.Apply();
                man["sunByHour"] = J.Obj().Set("latitude", dn.latitude).Set("valleyHeight", dn.valleyHeight).Set("sceneHour", keep).Set("hours", hours);

                // the atlas materials whose windows light up at night (DayNightCycle.emissiveMaterials)
                var glowIds = J.Arr();
                if (dn.emissiveMaterials != null)
                    foreach (var m in dn.emissiveMaterials)
                        if (m && matIds.TryGetValue(m, out var gid)) glowIds.Add(gid);
                man["nightEmission"] = J.Obj().Set("color", Col(dn.emissionColor)).Set("intensity", dn.emissionIntensity).Set("materials", glowIds);
            }

            var lm = UnityEngine.Object.FindObjectsByType<LampManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (lm)
            {
                var heads = J.Arr(); if (lm.lampHeads != null) foreach (var p in lm.lampHeads) heads.Add(V3(p));
                var hc = J.Arr(); if (lm.lampColors != null) foreach (var c in lm.lampColors) hc.Add(Col(c));
                var shops = J.Arr(); if (lm.shopLights != null) foreach (var p in lm.shopLights) shops.Add(V3(p));
                var sc = J.Arr(); if (lm.shopColors != null) foreach (var c in lm.shopColors) sc.Add(Col(c));
                man["lamps"] = J.Obj().Set("heads", heads).Set("headColors", hc).Set("shops", shops).Set("shopColors", sc)
                    .Set("range", lm.range).Set("intensity", lm.intensity).Set("shopRange", lm.shopRange).Set("shopIntensity", lm.shopIntensity)
                    .Set("color", Col(lm.color)).Set("poolSize", lm.poolSize);
                Debug.Log($"[UnrealExport] lamps: {lm.lampHeads?.Length ?? 0} street lamps, {lm.shopLights?.Length ?? 0} shop lights");
            }
        }

        static string PathOf(Transform t)
        {
            var s = t.name;
            for (var p = t.parent; p; p = p.parent) s = p.name + "/" + s;
            return s;
        }

        // ------------------------------------------------------------------ lake, ponds, places
        static void ExportLakeAndMarkers(J man, WorldJson world, Terrain terrain)
        {
            var meta = world.meta;
            float lakeY = meta.lakeLevel - meta.hMin;
            man["lake"] = J.Obj().Set("name", world.lake.name).Set("waterY", lakeY).Set("outline", J.Arr().AddAll(world.lake.pts))
                .Set("outlineLayout", "x, z pairs (Unity world)");
            var ponds = J.Arr();
            if (world.ponds != null)
                foreach (var p in world.ponds) ponds.Add(J.Obj().Set("name", p.name).Set("level", p.level).Set("outline", J.Arr().AddAll(p.pts)));
            man["ponds"] = ponds;

            float H(float x, float z) => terrain ? terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y : 0f;
            var pois = J.Arr();
            if (world.pois != null)
                foreach (var p in world.pois)
                    pois.Add(J.Obj().Set("name", p.name).Set("kind", p.kind).Set("sub", p.sub).Set("position", V3(new Vector3(p.x, H(p.x, p.z), p.z))));
            man["pois"] = pois;

            var areas = J.Arr();
            if (world.areas != null)
                foreach (var a in world.areas) areas.Add(J.Obj().Set("name", a.name).Set("kind", a.kind).Set("outline", J.Arr().AddAll(a.pts)));
            man["areas"] = areas;

            // view cameras the builder leaves in the scene ("ViewShot ...", "MallShotSplit" ...)
            var views = J.Arr();
            foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                views.Add(J.Obj().Set("name", cam.name).Set("position", V3(cam.transform.position)).Set("forward", V3(cam.transform.forward)).Set("fov", cam.fieldOfView));
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if ((t.name.StartsWith("ViewShot") || t.name.EndsWith("Shot") || t.name.Contains("ShotSplit")) && !t.GetComponent<Camera>())
                    views.Add(J.Obj().Set("name", t.name).Set("position", V3(t.position)).Set("forward", V3(t.forward)));
            man["views"] = views;
        }

        // ------------------------------------------------------------------ json
        static J V2(Vector2 v) => J.Arr().Add(v.x).Add(v.y);
        static J V3(Vector3 v) => J.Arr().Add(v.x).Add(v.y).Add(v.z);
        static J V4(Vector4 v) => J.Arr().Add(v.x).Add(v.y).Add(v.z).Add(v.w);
        static J Col(Color c) => J.Arr().Add(c.r).Add(c.g).Add(c.b).Add(c.a);

        /// <summary>Root order + sibling indices down to t: a stable order for the scene's objects.</summary>
        static string HierarchyKey(Transform t)
        {
            var parts = new List<string>();
            for (; t; t = t.parent) parts.Add(t.GetSiblingIndex().ToString("D6"));   // root objects: their order in the scene
            parts.Reverse();
            return string.Join("/", parts);
        }

        /// <summary>Tiny ordered JSON tree (JsonUtility cannot write dictionaries).</summary>
        public class J
        {
            readonly List<KeyValuePair<string, object>> obj;
            readonly List<object> arr;
            J(bool isArr) { if (isArr) arr = new List<object>(); else obj = new List<KeyValuePair<string, object>>(); }
            public static J Obj() => new J(false);
            public static J Arr() => new J(true);
            public object this[string k]
            {
                set
                {
                    int i = obj.FindIndex(p => p.Key == k);
                    if (i >= 0) obj[i] = new KeyValuePair<string, object>(k, value); else obj.Add(new KeyValuePair<string, object>(k, value));
                }
            }
            public J Set(string k, object v) { this[k] = v; return this; }
            public J Add(object v) { arr.Add(v); return this; }
            public J AddAll(System.Collections.IEnumerable items) { if (items != null) foreach (var v in items) arr.Add(v); return this; }
            public int Count => arr != null ? arr.Count : obj.Count;

            public override string ToString() { var sb = new StringBuilder(); Write(sb, this); return sb.ToString(); }

            static void Write(StringBuilder sb, object v)
            {
                switch (v)
                {
                    case null: sb.Append("null"); break;
                    case J j when j.arr != null:
                        sb.Append('[');
                        for (int i = 0; i < j.arr.Count; i++) { if (i > 0) sb.Append(','); Write(sb, j.arr[i]); }
                        sb.Append(']');
                        break;
                    case J j:
                        sb.Append('{');
                        for (int i = 0; i < j.obj.Count; i++)
                        {
                            if (i > 0) sb.Append(',');
                            Str(sb, j.obj[i].Key); sb.Append(':'); Write(sb, j.obj[i].Value);
                        }
                        sb.Append('}');
                        break;
                    case string s: Str(sb, s); break;
                    case bool b: sb.Append(b ? "true" : "false"); break;
                    case float f: sb.Append(float.IsNaN(f) || float.IsInfinity(f) ? "0" : f.ToString("R", Inv)); break;
                    case double d: sb.Append(double.IsNaN(d) || double.IsInfinity(d) ? "0" : d.ToString("R", Inv)); break;
                    case int i: sb.Append(i.ToString(Inv)); break;
                    case long l: sb.Append(l.ToString(Inv)); break;
                    case uint u: sb.Append(u.ToString(Inv)); break;
                    default: Str(sb, v.ToString()); break;
                }
            }

            static void Str(StringBuilder sb, string s)
            {
                sb.Append('"');
                foreach (var c in s)
                {
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default:
                            if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4")); else sb.Append(c);
                            break;
                    }
                }
                sb.Append('"');
            }
        }
    }
}
