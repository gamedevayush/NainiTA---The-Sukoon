using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Nainital.EditorTools
{
    /// <summary>Shared state while generating the scene.</summary>
    public class WorldCtx
    {
        public WorldJson world;
        public TextureKit.Materials mats;
        public Terrain terrain;
        public float lakeY;
        public Vector2[] lake;            // CCW
        public Vector2 lakeCenter;
        public Vector2 mallitalShore, tallitalShore, mallitalAlong;
        public readonly List<Vector2> jettyRoots = new List<Vector2>();
        public readonly List<Vector2[]> mallRoad = new List<Vector2[]>();
        public readonly System.Random rng = new System.Random(20261107);
        public readonly List<Vector3> lampHeads = new List<Vector3>();
        public readonly List<string> zoneNames = new List<string>();
        public readonly List<Vector3> zoneCenters = new List<Vector3>();
        public readonly List<float> zoneRadii = new List<float>();
        public readonly Dictionary<string, Vector3> landmarkSites = new Dictionary<string, Vector3>();
        public Vector3 spawn;
        public Quaternion spawnRot;
        public Vector3 busStand;
        public Quaternion busStandRot;
        public bool hasBusStand;
        public readonly List<(Vector3 pos, Vector3 normal, float width)> shopFronts = new List<(Vector3, Vector3, float)>();
        public List<Vector3> promenadePath;
        public AssetBank bank;
        public string bankPath;
        public Transform worldRoot, gameplayRoot;
        int meshCounter;

        // G.O.A.N. buildings (see WorldBuilder.Goan.cs): planned before the procedural houses are laid out
        public Dictionary<string, GoanKit.Building> goanKit = new Dictionary<string, GoanKit.Building>();
        public readonly List<(Vector2 c, float r)> keepClear = new List<(Vector2, float)>();
        public readonly List<WorldBuilder.GoanPlacement> goan = new List<WorldBuilder.GoanPlacement>();

        public bool InKeepClear(Vector2 p, float pad = 0f)
        {
            foreach (var k in keepClear)
                if ((p - k.c).sqrMagnitude < (k.r + pad) * (k.r + pad)) return true;
            return false;
        }

        public float H(float x, float z) => terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y;
        public float H(Vector2 p) => H(p.x, p.y);
        public bool InLake(Vector2 p) => Geo.PointInPolygon(lake, p);
        public float LakeDistance(Vector2 p) => Geo.DistanceToPolyline(lake, p, true);

        /// <summary>Move a point straight away from the lake until it is at least minDist from the shore.</summary>
        public Vector2 PushFromLake(Vector2 p, float minDist)
        {
            for (int i = 0; i < 60; i++)
            {
                bool inside = InLake(p);
                float d = LakeDistance(p);
                if (!inside && d >= minDist) return p;
                Vector2 away = (p - lakeCenter).normalized;
                p += away * Mathf.Max(1f, inside ? d + minDist : minDist - d);
            }
            return p;
        }

        public Vector2? Poi(string contains)
        {
            foreach (var p in world.pois)   // exact name first ("Snow View" vs "Hotel Snow View Heritage")
                if (string.Equals(p.name, contains, StringComparison.OrdinalIgnoreCase))
                    return new Vector2(p.x, p.z);
            foreach (var p in world.pois)
                if (!string.IsNullOrEmpty(p.name) && p.name.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0)
                    return new Vector2(p.x, p.z);
            return null;
        }

        public void Zone(string name, Vector3 c, float r) { zoneNames.Add(name); zoneCenters.Add(c); zoneRadii.Add(r); }

        public Mesh Save(MeshBuilder mb, string name)
        {
            var m = mb.ToMesh(name + "_" + (meshCounter++));
            if (!bank) bank = AssetDatabase.LoadAssetAtPath<AssetBank>(bankPath);
            AssetDatabase.AddObjectToAsset(m, bank);
            return m;
        }

        public GameObject MeshObject(string name, Transform parent, MeshBuilder mb, Material mat, MeshBuilder collision = null,
                                     bool castShadows = true, bool renderMeshAsCollider = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Mesh render = null;
            if (mb != null && mb.VertexCount > 0)
            {
                render = Save(mb, name);
                go.AddComponent<MeshFilter>().sharedMesh = render;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            if (collision != null && collision.VertexCount > 0)
                go.AddComponent<MeshCollider>().sharedMesh = Save(collision, name + "_col");
            else if (renderMeshAsCollider && render)
                go.AddComponent<MeshCollider>().sharedMesh = render;
            return go;
        }

        public GameObject Instance(string name, Transform parent, Mesh mesh, Vector3 pos, Quaternion rot, bool boxCollider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mats.atlas;
            if (boxCollider)
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.center = mesh.bounds.center;
                bc.size = mesh.bounds.size;
            }
            return go;
        }

        Mesh boardMesh;
        public void InfoSign(string title, string body, Vector3 pos, Quaternion rot, Transform parent)
        {
            if (!boardMesh)
            {
                var mb = new MeshBuilder();
                PropKit.InfoBoard(mb);
                boardMesh = Save(mb, "InfoBoard");
            }
            var go = Instance("Info: " + title, parent, boardMesh, pos, rot, true);
            var s = go.AddComponent<InfoSign>();
            s.title = title;
            s.body = body;
            s.radius = 2.6f;
        }
    }

    public static partial class WorldBuilder
    {
        public const string Root = "Assets/Nainital";
        public const string Gen = Root + "/Generated";
        public const string ScenePath = Root + "/Scenes/Nainital.unity";

        [MenuItem("Nainital/2. Build World (generate scene)", priority = 2)]
        public static void BuildMenu()
        {
            ProjectSetup.Run();
            Build();
        }

        /// <summary>Batch entry: Unity -batchmode -quit -executeMethod Nainital.EditorTools.WorldBuilder.BuildBatch</summary>
        public static void BuildBatch()
        {
            try
            {
                ProjectSetup.Run();
                Build();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static void Step(string name, Action a)
        {
            var t = DateTime.Now;
            EditorUtility.DisplayProgressBar("Nainital", name, 0.5f);
            try { a(); }
            finally { EditorUtility.ClearProgressBar(); }
            Debug.Log($"[Nainital] {name}: {(DateTime.Now - t).TotalSeconds:F1}s");
        }

        public static void Build()
        {
            var t0 = DateTime.Now;
            var world = JsonUtility.FromJson<WorldJson>(File.ReadAllText(Root + "/Data/world.json"));
            Directory.CreateDirectory(Gen);
            Directory.CreateDirectory(Root + "/Scenes");
            // New scene FIRST: opening a scene unloads unused assets, which would kill objects created before it.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var mats = TextureKit.Create(Gen);

            string bankPath = Gen + "/WorldMeshes.asset";
            if (File.Exists(bankPath)) AssetDatabase.DeleteAsset(bankPath);
            var bank = ScriptableObject.CreateInstance<AssetBank>();
            AssetDatabase.CreateAsset(bank, bankPath);

            var ctx = new WorldCtx { world = world, mats = mats, bank = bank, bankPath = bankPath };
            ctx.worldRoot = new GameObject("World (real Nainital data)").transform;
            ctx.gameplayRoot = new GameObject("Gameplay").transform;

            Step("Terrain", () => BuildTerrain(ctx));
            Step("Lake", () => BuildLake(ctx));
            Step("Small lakes (Sariyatal, Khurpatal)", () => BuildPonds(ctx));
            Step("Level the landmark sites", () => FlattenSites(ctx));
            Step("Roads", () => BuildRoads(ctx));
            Step("G.O.A.N. plan (civic sites)", () => PlanGoan(ctx));
            Step("Tallital plan", () => PlanTallital(ctx));
            Step("Mallital plan", () => PlanMallital(ctx));
            BuildingKit.ShopFront = (p, n, w) => ctx.shopFronts.Add((p, n, w));
            BuildingKit.Ground = p => ctx.H(p);   // plinths and aprons follow the real ground
            Step("Buildings", () => BuildBuildings(ctx));
            BuildingKit.ShopFront = null;
            Step("G.O.A.N. buildings", () => BuildGoan(ctx));
            Step("Landmarks", () => LandmarkBuilder.Build(ctx));
            Step("Promenade", () => BuildPromenade(ctx));
            Step("Road lamps", () => BuildRoadLamps(ctx));
            Step("Street details (signs, poles, wires, boats, gate)", () => BuildStreetDetails(ctx));
            Step("Mall Road (walls, railings, lamps, shops)", () => BuildMallRoad(ctx));
            Step("Town details (bazaars, gates, boards, zoo)", () => BuildTownDetails(ctx));
            Step("Road junctions (curved corners)", () => BuildJunctions(ctx));
            Step("Road walls and parapets", () => BuildRoadWalls(ctx));
            // Step("Sariyatal Falls", () => BuildWaterfall(ctx));   // parked (user, 2026-11-11): the waterfall is not at Sariyatal
            Step("Tallital (bus station, school, chowk)", () => BuildTallital(ctx));
            Step("Mallital (Capitol, Naina Devi gate, Tibetan market, ghat)", () => BuildMallital(ctx));
            Step("The Flats (DSA ground)", () => BuildFlats(ctx));
            Step("Trees", () => BuildTrees(ctx));
            Step("Lighting", () => BuildLighting(ctx));
            Step("Far ridges (beyond the map)", () => BuildFarRidges(ctx));
            Step("Surface look (weathering)", () => BuildSurfaceLook(ctx));
            Step("Gameplay", () => BuildGameplay(ctx));
            Step("Living town (traffic, crowds, grass, parked cars)", () => BuildLivingTown(ctx));
            BuildingKit.Ground = null;

            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            var builtTd = AssetDatabase.LoadAssetAtPath<TerrainData>(Gen + "/NainitalTerrain.asset");
            if (builtTd) Debug.Log("[Nainital] terrain texture weights after save: " + TerrainWeights(builtTd));
            Debug.Log($"[Nainital] WORLD BUILT in {(DateTime.Now - t0).TotalSeconds:F0}s -> {ScenePath}");
        }

        // ------------------------------------------------------------------ geometry helpers
        public static Vector2[] Offset(Vector2[] poly, float d)
        {
            int n = poly.Length;
            var o = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 p = poly[i], a = poly[(i - 1 + n) % n], b = poly[(i + 1) % n];
                Vector2 e0 = (p - a).normalized, e1 = (b - p).normalized;
                Vector2 n0 = new Vector2(e0.y, -e0.x), n1 = new Vector2(e1.y, -e1.x);
                Vector2 nm = n0 + n1;
                if (nm.sqrMagnitude < 1e-6f) nm = n0;
                nm.Normalize();
                float k = Mathf.Max(Vector2.Dot(nm, n0), 0.5f);
                o[i] = p + nm * (d / k);
            }
            return o;
        }

        public static List<Vector2> Resample(IList<Vector2> pts, float step, bool closed)
        {
            var outPts = new List<Vector2>();
            int n = pts.Count;
            if (n == 0) return outPts;
            int segs = closed ? n : n - 1;
            float carry = 0f;
            outPts.Add(pts[0]);
            for (int i = 0; i < segs; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % n];
                float len = Vector2.Distance(a, b);
                float s = step - carry;
                while (s <= len)
                {
                    outPts.Add(Vector2.Lerp(a, b, s / len));
                    s += step;
                }
                carry = len - (s - step);
            }
            if (!closed && (outPts[outPts.Count - 1] - pts[n - 1]).sqrMagnitude > 0.01f) outPts.Add(pts[n - 1]);
            return outPts;
        }

        static Vector2 Outward(Vector2[] poly, int i)
        {
            int n = poly.Length;
            Vector2 e = (poly[(i + 1) % n] - poly[(i - 1 + n) % n]).normalized;
            return new Vector2(e.y, -e.x);
        }

        static bool IsMinor(string kind) => kind == "path" || kind == "footway" || kind == "steps" || kind == "bridleway" || kind == "track";

        /// <summary>Mean weight of each terrain layer, from a coarse sample: a check that the splat really is on the terrain.</summary>
        internal static string TerrainWeights(TerrainData td)
        {
            int ar = td.alphamapResolution, n = td.alphamapLayers, cnt = 0;
            var sum = new double[n];
            for (int y = 0; y < ar; y += 64)
            {
                var row = td.GetAlphamaps(0, y, ar, 1);
                for (int x = 0; x < ar; x += 16) { for (int k = 0; k < n; k++) sum[k] += row[0, x, k]; cnt++; }
            }
            string m = "";
            for (int k = 0; k < n; k++) m += (sum[k] / Mathf.Max(1, cnt)).ToString("F3") + " ";
            return m.Trim();
        }

        // ------------------------------------------------------------------ terrain
        static void BuildTerrain(WorldCtx ctx)
        {
            var meta = ctx.world.meta;
            int res = meta.res;
            var td = new TerrainData();
            td.heightmapResolution = res;
            td.size = new Vector3(meta.sizeM, meta.hMax - meta.hMin, meta.sizeM);
            var raw = File.ReadAllBytes(Root + "/Data/heightmap.bytes");
            var h = new float[res, res];
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    int i = (z * res + x) * 2;
                    h[z, x] = (raw[i] | (raw[i + 1] << 8)) / 65535f;
                }
            td.SetHeights(0, 0, h);
            // The asset first, then the layers and their weights: the weight textures belong to the asset file, and those
            // made before the TerrainData is an asset were never saved - the built terrain was 100 % its first layer
            // (grass) everywhere, whatever splat.png said (found 2026-11-11 with TerrainProbe).
            string p = Gen + "/NainitalTerrain.asset";
            if (File.Exists(p)) AssetDatabase.DeleteAsset(p);
            AssetDatabase.CreateAsset(td, p);
            td.terrainLayers = ctx.mats.layers;
            var splat = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            splat.LoadImage(File.ReadAllBytes(Root + "/Data/splat.png"));
            td.alphamapResolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(splat.width), 512, 4096);   // 4096 on the 6 km map: 1.46 m texels, so road edges blend
            td.baseMapResolution = 1024;
            int ar = td.alphamapResolution, sw = splat.width, sh = splat.height;
            var px = splat.GetPixels32();
            const int Band = 256;   // rows per SetAlphamaps call: a 4096 x 4096 x 4 float array at once is 268 MB
            for (int y0 = 0; y0 < ar; y0 += Band)
            {
                int rows = Mathf.Min(Band, ar - y0);
                var a = new float[rows, ar, 4];
                for (int yy = 0; yy < rows; yy++)
                    for (int x = 0; x < ar; x++)
                    {
                        var c = px[((y0 + yy) * sh / ar) * sw + x * sw / ar];
                        float s = Mathf.Max(1f, c.r + c.g + c.b + c.a);
                        a[yy, x, 0] = c.r / s; a[yy, x, 1] = c.g / s; a[yy, x, 2] = c.b / s; a[yy, x, 3] = c.a / s;
                    }
                td.SetAlphamaps(0, y0, a);
            }
            UnityEngine.Object.DestroyImmediate(splat);
            EditorUtility.SetDirty(td);
            AssetDatabase.SaveAssets();
            Debug.Log("[Nainital] terrain texture weights (grass, forest floor, rock, earth): " + TerrainWeights(td));

            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Terrain (SRTM DEM)";
            go.transform.SetParent(ctx.worldRoot, false);
            go.transform.position = new Vector3(meta.mapCx - meta.sizeM / 2f, 0, meta.mapCz - meta.sizeM / 2f);
            var t = go.GetComponent<Terrain>();
            t.materialTemplate = ctx.mats.terrain;
            t.heightmapPixelError = 2;   // at 4 the far terrain LOD swallowed Lower Mall below its 1.6 m step
            t.basemapDistance = 400;
            t.drawInstanced = true;
            t.drawTreesAndFoliage = false;
            t.allowAutoConnect = false;
            ctx.terrain = t;

            ctx.lakeY = meta.lakeLevel - meta.hMin;
            ctx.lake = Geo.ToPoints(ctx.world.lake.pts);
            if (Geo.SignedArea(ctx.lake) < 0) Array.Reverse(ctx.lake);
            Vector2 cc = Vector2.zero;
            foreach (var q in ctx.lake) cc += q;
            ctx.lakeCenter = cc / ctx.lake.Length;
        }

        // ------------------------------------------------------------------ lake
        static void BuildLake(WorldCtx ctx)
        {
            var parent = new GameObject("Naini Lake").transform;
            parent.SetParent(ctx.worldRoot, false);
            float y = ctx.lakeY;

            // water surface (slightly larger than the shore line, the banks hide the edge)
            var water = Offset(ctx.lake, 5f);
            var tris = Geo.Triangulate(water);
            var wmb = new MeshBuilder();
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                Vector2 p0 = water[tris[i]], p1 = water[tris[i + 1]], p2 = water[tris[i + 2]];
                wmb.Tri(new Vector3(p0.x, y, p0.y), new Vector3(p1.x, y, p1.y), new Vector3(p2.x, y, p2.y), Vector2.zero, Vector3.up);
            }
            var wgo = ctx.MeshObject("Water", parent, wmb, ctx.mats.water, null, false);
            wgo.layer = 4;

            // stone embankment along the real shore line
            var shore = Resample(ctx.lake, 3f, true);
            var emb = new MeshBuilder();
            Rect stone = Atlas.Material(Atlas.MatStone);
            var shoreArr = shore.ToArray();
            for (int i = 0; i < shoreArr.Length; i++)
            {
                Vector2 a = shoreArr[i], b = shoreArr[(i + 1) % shoreArr.Length];
                Vector2 oa = Outward(shoreArr, i), ob = Outward(shoreArr, (i + 1) % shoreArr.Length);
                float ta = Mathf.Max(ctx.H(a + oa * 2f), y + 0.6f) + 0.05f, tb = Mathf.Max(ctx.H(b + ob * 2f), y + 0.6f) + 0.05f;
                Vector3 inward = new Vector3(-(oa.x + ob.x), 0, -(oa.y + ob.y));
                emb.Quad(new Vector3(a.x, y - 2.5f, a.y), new Vector3(b.x, y - 2.5f, b.y), new Vector3(b.x, tb, b.y), new Vector3(a.x, ta, a.y), stone, inward);
                Vector2 a2 = a + oa * 0.7f, b2 = b + ob * 0.7f;
                emb.Quad(new Vector3(a.x, ta, a.y), new Vector3(b.x, tb, b.y), new Vector3(b2.x, tb, b2.y), new Vector3(a2.x, ta, a2.y), Atlas.MaterialUV(Atlas.MatStone), Vector3.up);
            }
            ctx.MeshObject("Stone embankment", parent, emb, ctx.mats.atlas);

            // Mallital (north) and Tallital (south) ends
            int north = 0, south = 0;
            for (int i = 0; i < ctx.lake.Length; i++)
            {
                if (ctx.lake[i].y > ctx.lake[north].y) north = i;
                if (ctx.lake[i].y < ctx.lake[south].y) south = i;
            }
            ctx.mallitalShore = ctx.lake[north];
            var busStation = ctx.Poi("New Bus Station");
            if (busStation.HasValue)
            {
                float bd = float.MaxValue;
                for (int i = 0; i < ctx.lake.Length; i++)
                    if ((ctx.lake[i] - busStation.Value).sqrMagnitude < bd) { bd = (ctx.lake[i] - busStation.Value).sqrMagnitude; south = i; }
            }
            ctx.tallitalShore = ctx.lake[south];
            Vector2 tn = (ctx.lake[(north + 1) % ctx.lake.Length] - ctx.lake[(north - 1 + ctx.lake.Length) % ctx.lake.Length]).normalized;
            ctx.mallitalAlong = tn;
            ctx.Zone("Mallital", new Vector3(ctx.mallitalShore.x, y, ctx.mallitalShore.y), 260f);
            ctx.Zone("Tallital", new Vector3(ctx.tallitalShore.x, y, ctx.tallitalShore.y), 220f);

            BuildJetty(ctx, parent, north, "Mallital Boat Stand");
            BuildJetty(ctx, parent, south, "Tallital Boat Stand");

            // sailing yachts
            var inner = Offset(ctx.lake, -25f);
            var rnd = new System.Random(3);
            int[] sails = { Pal.SailWhite, Pal.Canopy, Pal.BoatYellow, Pal.SailWhite };
            for (int k = 0; k < 4; k++)
            {
                Vector2 q = ctx.lakeCenter;
                for (int tries = 0; tries < 100; tries++)
                {
                    q = ctx.lakeCenter + new Vector2((float)(rnd.NextDouble() - 0.5) * 500f, (float)(rnd.NextDouble() - 0.5) * 1200f);
                    if (Geo.PointInPolygon(inner, q)) break;
                }
                var ymb = new MeshBuilder();
                PropKit.Yacht(ymb, sails[k]);
                var ygo = ctx.MeshObject("Yacht " + (k + 1), parent, ymb, ctx.mats.atlas);
                ygo.transform.SetPositionAndRotation(new Vector3(q.x, y, q.y), Quaternion.Euler(0, rnd.Next(360), 0));
                ygo.AddComponent<Yacht>().speed = 1.8f + k * 0.4f;
            }
        }

        static void BuildJetty(WorldCtx ctx, Transform parent, int vertex, string name)
        {
            float y = ctx.lakeY;
            Vector2 shoreP = ctx.lake[vertex];
            Vector2 outward = Outward(ctx.lake, vertex);
            Vector2 dir = -outward;
            Vector2 root = shoreP + outward * 3f;
            ctx.jettyRoots.Add(root);
            float deck = y + 0.5f;
            float bankRise = Mathf.Max(0.2f, ctx.H(root) - deck + 0.05f);
            var mb = new MeshBuilder();
            var col = new MeshBuilder();
            PropKit.Jetty(mb, col, 18f, bankRise);
            var go = ctx.MeshObject(name, parent, mb, ctx.mats.atlas, col);
            Quaternion rot = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.y), Vector3.up);
            go.transform.SetPositionAndRotation(new Vector3(root.x, deck, root.y), rot);

            // dock point (where boat riders step off)
            Vector3 dockPos = go.transform.TransformPoint(new Vector3(0, 0.1f, 9f));
            var dock = new GameObject(name + " Dock").transform;
            dock.SetParent(parent, false);
            dock.position = dockPos;

            // boats moored beside the deck
            int[] colors = { Pal.BoatRed, Pal.BoatBlue, Pal.BoatYellow, Pal.BoatGreen };
            for (int b = 0; b < 4; b++)
            {
                float side = b % 2 == 0 ? 1f : -1f;
                Vector3 local = new Vector3(side * 2.9f, 0, 8f + (b / 2) * 5.5f);
                Vector3 pos = go.transform.TransformPoint(local);
                pos.y = y + 0.05f;
                var bmb = new MeshBuilder();
                PropKit.RowBoat(bmb, colors[(b + vertex) % colors.Length], true);
                var bgo = ctx.MeshObject($"{name} Boat {b + 1}", parent, bmb, ctx.mats.atlas);
                bgo.transform.SetPositionAndRotation(pos, rot);
                var seat = new GameObject("Seat").transform;
                seat.SetParent(bgo.transform, false);
                seat.localPosition = new Vector3(0, -0.05f, -0.45f);
                var bc = bgo.AddComponent<BoatController>();
                bc.seat = seat;
                bc.dockExit = dock;
                bc.radius = 3.2f;
            }
            ctx.Zone(name, new Vector3(root.x, y, root.y), 30f);
            ctx.InfoSign(name, "Yahan se naav kiraye pe milti hai. Naav pe E dabao, W/joystick se chalao, A/D se mudo. Wapas kisi bhi jetty pe aakar E dabake utro.",
                new Vector3(root.x + outward.x * 3f + dir.y * 3f, ctx.H(root + outward * 3f + new Vector2(dir.y, -dir.x) * 3f), root.y + outward.y * 3f - dir.x * 3f),
                Quaternion.LookRotation(new Vector3(outward.x, 0, outward.y)), parent);
        }

        // ------------------------------------------------------------------ roads
        /// <summary>How far a road's surface stands above the ground beside it (user, 2026-11-11: "road halki si ghaas se
        /// upar hogi, har jagah"): a laid slab of tarmac or concrete, not paint on the grass.</summary>
        internal const float RoadLift = 0.17f;

        /// <summary>The side of the road slab between two edge points: from the surface down into the ground, leaning
        /// out a little; on the downhill side it goes as deep as the ground falls (to 1.5 m).</summary>
        static void RoadKerb(WorldCtx ctx, MeshBuilder mb, Vector3 a, Vector3 b, Vector3 outA, Vector3 outB, float v0, float v1)
        {
            Vector3 fa = a + outA * 0.07f, fb = b + outB * 0.07f;
            fa.y = Mathf.Max(a.y - 1.5f, Mathf.Min(a.y - 0.24f, ctx.H(new Vector2(fa.x, fa.z)) - 0.06f));
            fb.y = Mathf.Max(b.y - 1.5f, Mathf.Min(b.y - 0.24f, ctx.H(new Vector2(fb.x, fb.z)) - 0.06f));
            mb.Quad(fa, fb, b, a, new Rect(0.004f, v0, 0.03f, v1 - v0), (outA + outB) * 0.5f);
        }

        static void BuildRoads(WorldCtx ctx)
        {
            var parent = new GameObject("Roads (OpenStreetMap)").transform;
            parent.SetParent(ctx.worldRoot, false);
            int idx = 0;
            var paint = new MeshBuilder();
            foreach (var r in ctx.world.roads)
            {
                var pts = Geo.ToPoints(r.pts);
                if (pts.Length < 2) continue;
                if (!string.IsNullOrEmpty(r.name) && r.name.IndexOf("Mall Road", StringComparison.OrdinalIgnoreCase) >= 0) ctx.mallRoad.Add(pts);
                var path = Resample(pts, 2f, false);
                if (path.Count < 2) continue;
                bool minor = IsMinor(r.kind);
                float half = r.width * 0.5f;
                bool dirtTrack = r.kind == "path" || r.kind == "track" || r.kind == "bridleway";   // trodden earth: level with the ground
                float lift = (dirtTrack ? 0.05f : minor ? 0.10f : RoadLift) + (idx % 7) * 0.004f;
                bool mallDeck = IsMallDeck(r.name);   // flat across, at its own level: the step between the two roads is built in
                bool lakeRoad = IsLakeRoad(ctx, r);   // cement concrete with white lines
                float run = 0f;
                var mb = new MeshBuilder();
                Vector3 prevL = default, prevR = default;
                Vector2 prevNrm = default;
                float v = 0f;
                var kerbL = new List<Vector3>(path.Count); var kerbR = new List<Vector3>(path.Count); var vs = new List<float>(path.Count);
                for (int i = 0; i < path.Count; i++)
                {
                    Vector2 p = path[i];
                    Vector2 tan = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                    Vector2 nrm = new Vector2(-tan.y, tan.x);
                    Vector2 l = p + nrm * half, rr = p - nrm * half;
                    float hc = ctx.H(p);
                    Vector3 L = new Vector3(l.x, (mallDeck ? hc : Mathf.Max(ctx.H(l), hc - 0.3f)) + lift, l.y);
                    Vector3 R = new Vector3(rr.x, (mallDeck ? hc : Mathf.Max(ctx.H(rr), hc - 0.3f)) + lift, rr.y);
                    if (i > 0)
                    {
                        float v1 = v + Vector2.Distance(path[i - 1], p) / Mathf.Max(1f, r.width);
                        mb.Quad(prevL, prevR, R, L, new Rect(0f, v, 1f, v1 - v), Vector3.up);
                        if (!mallDeck && !dirtTrack)
                        {
                            // the edge of the slab: a short face down into the ground either side (deeper where the ground falls away)
                            Vector3 n3 = new Vector3(nrm.x, 0f, nrm.y), pn3 = new Vector3(prevNrm.x, 0f, prevNrm.y);
                            RoadKerb(ctx, mb, prevL, L, pn3, n3, v, v1);
                            RoadKerb(ctx, mb, prevR, R, -pn3, -n3, v, v1);
                        }
                        if (lakeRoad && !mallDeck && !NearJunction(ctx, p)) PaintStripes(paint, prevL, prevR, R, L, r.width, run);
                        run += Vector2.Distance(path[i - 1], p);
                        v = v1;
                    }
                    prevL = L;
                    prevR = R;
                    prevNrm = nrm;
                    kerbL.Add(L); kerbR.Add(R); vs.Add(v);
                }
                bool walk = r.kind == "pedestrian" || r.kind == "footway" || r.kind == "steps";
                bool redTiles = walk && path.Count > 1 && InMallitalFront(ctx, path[path.Count / 2]) && ctx.LakeDistance(path[path.Count / 2]) < 45f;   // Mallital lakefront (2025 videos)
                if (redTiles && !redTileMat) redTileMat = RedTileMaterial();
                Material mat = lakeRoad ? ConcreteMaterial(ctx) : redTiles ? redTileMat : walk ? (PhotoMaterial("cobblestone_floor_08", "M_MallCobblePhoto", new Color(0.92f, 0.92f, 0.9f), 0.45f) ?? ctx.mats.paver)
                             : (r.kind == "path" || r.kind == "track" || r.kind == "bridleway") ? ctx.mats.dirtPath : ctx.mats.asphalt;
                string nm = string.IsNullOrEmpty(r.name) ? r.kind : r.name;
                if (r.name != MallLower) ctx.MeshObject($"{nm} ({r.kind})", parent, mb, mat, null, false, true);   // Lower Mall is built with the Mall Road; walked on, so a collider
                if (mat == ctx.mats.asphalt && !mallDeck && !lakeRoad) AddRoadFringe(ctx, path, kerbL, kerbR, vs);   // tarmac frays into the ground
                if (!string.IsNullOrEmpty(r.name) && (r.name.Contains("Mall Road") || r.name.Contains("Thandi Sadak")))
                    for (int i = 0; i < path.Count; i += 20)
                        ctx.Zone(r.name, new Vector3(path[i].x, 0, path[i].y), 22f);
                idx++;
            }
            ctx.MeshObject("Lake road paint", parent, paint, ctx.mats.atlas, null, false);
            EmitRoadFringes(ctx, parent);
        }

        // ------------------------------------------------------------------ buildings
        static bool NearMallRoad(WorldCtx ctx, Vector2 p, float dist)
        {
            foreach (var line in ctx.mallRoad)
                if (Geo.DistanceToPolyline(line, p, false) < dist) return true;
            return false;
        }

        static void BuildBuildings(WorldCtx ctx)
        {
            var parent = new GameObject("Buildings (OSM + generated)").transform;
            parent.SetParent(ctx.worldRoot, false);
            var chunks = new Dictionary<Vector2Int, MeshBuilder[]>();
            MeshBuilder[] Chunk(Vector2 c)
            {
                var key = new Vector2Int(Mathf.FloorToInt(c.x / 250f), Mathf.FloorToInt(c.y / 250f));
                if (!chunks.TryGetValue(key, out var ch)) { ch = new[] { new MeshBuilder(), new MeshBuilder() }; chunks[key] = ch; }
                return ch;
            }
            var bazaar = ctx.Poi("Bada Bazar");
            var tallitalBus = ctx.Poi("New Bus Station");
            var mallitalC = ctx.Poi("Nainital Stadium");
            var h = ctx.world.houses;
            int count = 0;
            for (int i = 0; i + 8 < h.Length; i += 9)
            {
                Vector2 c = new Vector2(h[i], h[i + 1]);
                float w = h[i + 2], d = h[i + 3], yaw = h[i + 4];
                int levels = Mathf.Max(1, (int)h[i + 5]);
                Quaternion q = Quaternion.Euler(0, yaw, 0);
                Vector3 R3 = q * Vector3.right * (w * 0.5f), F3 = q * Vector3.forward * (d * 0.5f);
                Vector2 R = new Vector2(R3.x, R3.z), F = new Vector2(F3.x, F3.z);
                float gMin = float.MaxValue, gMax = float.MinValue;
                for (int gu = -2; gu <= 2; gu++)   // a 5 x 5 grid over the footprint: a hump inside must not come up through the floor
                    for (int gv = -2; gv <= 2; gv++)
                    {
                        float g = ctx.H(c + R * (gu * 0.5f) + F * (gv * 0.5f));
                        gMin = Mathf.Min(gMin, g);
                        gMax = Mathf.Max(gMax, g);
                    }
                if (gMax < ctx.lakeY + 0.5f || ctx.InLake(c)) continue;
                if (ctx.InKeepClear(c, Mathf.Max(w, d) * 0.5f)) continue;   // a G.O.A.N. civic building stands here
                bool shop = NearMallRoad(ctx, c, 32f) || (bazaar.HasValue && (c - bazaar.Value).sqrMagnitude < BazaarShopRadius * BazaarShopRadius)
                         || (tallitalBus.HasValue && (c - tallitalBus.Value).sqrMagnitude < TallitalShopRadius * TallitalShopRadius * 0.8f)
                         || (mallitalC.HasValue && (c - mallitalC.Value).sqrMagnitude < MallitalShopRadius * MallitalShopRadius);
                if (TryGoanHouse(ctx, i / 9, c, w, d, yaw, gMin, gMax, shop, Chunk(c)[0])) { count++; continue; }
                var st = new BuildingKit.Style
                {
                    wall = (int)h[i + 6] % Atlas.WallColors, roof = (int)h[i + 7] % Atlas.RoofColors,
                    flatRoof = h[i + 8] > 0.5f, shop = shop, litChance = 0.45f,
                };
                var ch = Chunk(c);
                BuildingKit.House(ch[0], ch[1], c, w, d, yaw, levels, gMin, gMax, st, ctx.rng);
                count++;
            }
            foreach (var b in ctx.world.osmBuildings)
            {
                var poly = Geo.ToPoints(b.pts);
                if (poly.Length < 3) continue;
                float gMin = float.MaxValue, gMax = float.MinValue;
                Vector2 c = Vector2.zero;
                foreach (var p in poly) { float g = ctx.H(p); gMin = Mathf.Min(gMin, g); gMax = Mathf.Max(gMax, g); c += p; }
                for (int k = 0; k < poly.Length; k++)   // edge midpoints and the inside too
                {
                    float g = ctx.H((poly[k] + poly[(k + 1) % poly.Length]) * 0.5f);
                    gMin = Mathf.Min(gMin, g); gMax = Mathf.Max(gMax, g);
                }
                { float g = ctx.H(c / Mathf.Max(1, poly.Length)); gMin = Mathf.Min(gMin, g); gMax = Mathf.Max(gMax, g); }
                c /= poly.Length;
                if (ctx.InLake(c)) continue;
                if (string.IsNullOrEmpty(b.name) && ctx.InKeepClear(c, 3f)) continue;
                if (!string.IsNullOrEmpty(b.name) && b.name.IndexOf("Capitol", StringComparison.OrdinalIgnoreCase) >= 0) continue;   // built in WorldBuilder.Mallital.cs   // unnamed buildings give way to built places (Tallital, civic sites)
                bool hostel = b.kind == "hostel" || b.kind == "university" || b.kind == "school" || b.kind == "college";
                var st = new BuildingKit.Style
                {
                    wall = hostel ? 1 : ctx.rng.Next(Atlas.WallColors), roof = ctx.rng.Next(Atlas.RoofColors),
                    flatRoof = true, shop = false, litChance = 0.5f,
                };
                var ch = Chunk(c);
                BuildingKit.Footprint(ch[0], ch[1], poly, b.levels > 0 ? Mathf.Clamp(b.levels, 1, 8) : 3, gMin, gMax, st, ctx.rng);
                count++;
            }
            foreach (var kv in chunks)
            {
                var go = ctx.MeshObject($"Houses {kv.Key.x},{kv.Key.y}", parent, kv.Value[0], ctx.mats.atlas, kv.Value[1]);
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
            }
            Debug.Log($"[Nainital] buildings: {count} in {chunks.Count} chunks");
        }

        // ------------------------------------------------------------------ promenade (Mall Road / Thandi Sadak lake edge)
        static bool NearJetty(WorldCtx ctx, Vector2 p, float r)
        {
            foreach (var j in ctx.jettyRoots) if ((p - j).sqrMagnitude < r * r) return true;
            return false;
        }

        static void BuildPromenade(WorldCtx ctx)
        {
            var parent = new GameObject("Lake Promenade (Mall Road)").transform;
            parent.SetParent(ctx.worldRoot, false);

            // railing
            var railPts = Resample(Offset(ctx.lake, 1.2f), 2.5f, true);
            railPts.Add(railPts[0]);
            var rail = new MeshBuilder();
            var railCol = new MeshBuilder();
            var batch = new List<Vector3>();
            int part = 0;
            for (int i = 0; i < railPts.Count; i++)
            {
                Vector2 p = railPts[i];
                if (NearJetty(ctx, p, 5f) || NearMallRoad(ctx, p, 40f) || InMallitalFront(ctx, p)) continue;   // Mall Road and Mallital have their own railings
                batch.Add(new Vector3(p.x, ctx.H(p), p.y));
                if (batch.Count >= 160 || i == railPts.Count - 1)
                {
                    PropKit.Railing(rail, railCol, batch);
                    ctx.MeshObject("Railing " + (part++), parent, rail, ctx.mats.atlas, railCol, false);
                    rail = new MeshBuilder();
                    railCol = new MeshBuilder();
                    var last = batch[batch.Count - 1];
                    batch.Clear();
                    batch.Add(last);
                }
            }

            if (batch.Count > 1)
            {
                PropKit.Railing(rail, railCol, batch);
                ctx.MeshObject("Railing " + part, parent, rail, ctx.mats.atlas, railCol, false);
            }

            // Mall Road lamps every ~28 m
            var lampPts = Resample(Offset(ctx.lake, 3.2f), 28f, true);
            var lampMb = new MeshBuilder();
            var lampCol = new MeshBuilder();
            int lampCount = 0;
            foreach (var p in lampPts)
            {
                if (NearJetty(ctx, p, 6f) || NearMallRoad(ctx, p, 40f)) continue;
                Vector3 basePos = new Vector3(p.x, ctx.H(p), p.y);
                using (PropKit.At(lampMb, basePos, Quaternion.identity))
                {
                    Vector3 bulb = PropKit.MallLamp(lampMb);
                    ctx.lampHeads.Add(basePos + bulb);
                }
                lampCol.Box(basePos + Vector3.up * 2.2f, new Vector3(0.3f, 4.4f, 0.3f), Vector2.zero);
                if (++lampCount % 40 == 0)
                {
                    ctx.MeshObject("Mall Road Lamps " + lampCount / 40, parent, lampMb, ctx.mats.atlas, lampCol);
                    lampMb = new MeshBuilder();
                    lampCol = new MeshBuilder();
                }
            }
            ctx.MeshObject("Mall Road Lamps last", parent, lampMb, ctx.mats.atlas, lampCol);

            // benches facing the lake
            var benchMb = new MeshBuilder();
            PropKit.Bench(benchMb);
            var benchMesh = ctx.Save(benchMb, "Bench");
            var benchPts = Resample(Offset(ctx.lake, 3.8f), 75f, true);
            for (int i = 0; i < benchPts.Count; i++)
            {
                Vector2 p = benchPts[i];
                if (NearJetty(ctx, p, 8f) || NearMallRoad(ctx, p, 40f)) continue;
                Vector2 o = OutwardAt(ctx, p);
                var rot = Quaternion.LookRotation(new Vector3(-o.x, 0, -o.y));
                var go = ctx.Instance("Bench", parent, benchMesh, new Vector3(p.x, ctx.H(p), p.y), rot, true);
                var seat = new GameObject("Seat").transform;
                seat.SetParent(go.transform, false);
                seat.localPosition = new Vector3(0, 0.05f, 0f);
                go.AddComponent<Bench>().seat = seat;
            }

            // momos / chai stalls along the promenade
            var stallPts = Resample(Offset(ctx.lake, 10f), 1f, true);
            int[] awnings = { Pal.Canopy, Pal.Awning, Pal.BoatYellow, Pal.BoatGreen };
            string[] stallNames = { "Momos Stall", "Chai & Maggi Point", "Bhutta Wala", "Bun Makkhan Corner", "Momos Stall" };
            for (int k = 0; k < 5; k++)
            {
                Vector2 p = stallPts[(int)(stallPts.Count * (0.08f + k * 0.19f)) % stallPts.Count];
                int stallAt = (int)(stallPts.Count * (0.08f + k * 0.19f)) + 30 * k;
                for (int tries = 0; tries < 500 && (NearJetty(ctx, p, 12f) || NearMallRoad(ctx, p, 24f)); tries++)
                    p = stallPts[(stallAt += 20) % stallPts.Count];
                var smb = new MeshBuilder();
                PropKit.FoodStall(smb, awnings[k % awnings.Length]);
                Vector2 o = OutwardAt(ctx, p);
                var rot = Quaternion.LookRotation(new Vector3(-o.x, 0, -o.y));
                var go = ctx.MeshObject(stallNames[k], parent, smb, ctx.mats.atlas);
                go.transform.SetPositionAndRotation(new Vector3(p.x, ctx.H(p), p.y), rot);
                var bc = go.AddComponent<BoxCollider>();
                bc.center = new Vector3(0, 1f, 0);
                bc.size = new Vector3(2.3f, 2f, 1.1f);
                var fs = go.AddComponent<FoodStall>();
                fs.stallName = stallNames[k];
                fs.radius = 2.8f;
            }

            // walking path for tourists (6.5 m from the water, clear of houses/trees by construction)
            ctx.promenadePath = new List<Vector3>();
            var promRun = new List<Vector3>();
            foreach (var p in Resample(Offset(ctx.lake, 6.5f), 5f, true))
            {
                if (NearJetty(ctx, p, 7f)) continue;
                if (NearMallRoad(ctx, p, 24f))   // the shore below Mall Road is a bank, not a walk
                {
                    if (promRun.Count > ctx.promenadePath.Count) ctx.promenadePath = promRun;
                    promRun = new List<Vector3>();
                    continue;
                }
                promRun.Add(new Vector3(p.x, ctx.H(p) + 0.05f, p.y));
            }
            if (promRun.Count > ctx.promenadePath.Count) ctx.promenadePath = promRun;

            // spawn: Mallital promenade next to the boat stand, looking over the lake
            Vector2 o2 = OutwardAt(ctx, ctx.mallitalShore);
            Vector2 sp = ctx.mallitalShore + o2 * 7f + ctx.mallitalAlong * 10f;
            ctx.spawn = new Vector3(sp.x, ctx.H(sp) + 0.15f, sp.y);
            ctx.spawnRot = Quaternion.LookRotation(new Vector3(ctx.lakeCenter.x - sp.x, 0, ctx.lakeCenter.y - sp.y));
            ctx.InfoSign("Naini Jheel", LandmarkBuilder.Info["Naini Jheel"], new Vector3(sp.x + o2.x * 2f, ctx.H(sp + o2 * 2f), sp.y + o2.y * 2f) + new Vector3(ctx.mallitalAlong.x, 0, ctx.mallitalAlong.y) * 3f,
                         Quaternion.LookRotation(new Vector3(o2.x, 0, o2.y)), parent);
            if (ctx.mallRoad.Count > 0)
            {
                var mp = ctx.mallRoad[0][ctx.mallRoad[0].Length / 2];
                ctx.InfoSign("Mall Road", LandmarkBuilder.Info["Mall Road"], new Vector3(mp.x, ctx.H(mp), mp.y) + Vector3.right * 4f, Quaternion.identity, parent);
            }
        }

        static Vector2 OutwardAt(WorldCtx ctx, Vector2 p)
        {
            int best = 0;
            float bd = float.MaxValue;
            for (int i = 0; i < ctx.lake.Length; i++)
            {
                float d = (ctx.lake[i] - p).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return Outward(ctx.lake, best);
        }

        // ------------------------------------------------------------------ road lamps
        static void BuildRoadLamps(WorldCtx ctx)
        {
            var parent = new GameObject("Street Lights").transform;
            parent.SetParent(ctx.worldRoot, false);
            var mb = new MeshBuilder();
            var col = new MeshBuilder();
            int n = 0;
            foreach (var r in ctx.world.roads)
            {
                if (IsMinor(r.kind) || r.kind == "service" || r.kind == "track" || IsMallDeck(r.name)) continue;
                var pts = Resample(Geo.ToPoints(r.pts), 42f, false);
                for (int i = 1; i < pts.Count - 1; i++)
                {
                    Vector2 p = pts[i];
                    if (ctx.InLake(p) || ctx.LakeDistance(p) < 18f) continue;
                    Vector2 tan = (pts[i + 1] - pts[i - 1]).normalized;
                    float side = i % 2 == 0 ? 1f : -1f;
                    Vector2 nrm = new Vector2(-tan.y, tan.x) * side;
                    Vector2 lp = p + nrm * (r.width * 0.5f + 0.7f);
                    Vector3 basePos = new Vector3(lp.x, ctx.H(lp), lp.y);
                    Quaternion rot = Quaternion.LookRotation(new Vector3(-nrm.x, 0, -nrm.y));
                    using (PropKit.At(mb, basePos, rot))
                    {
                        Vector3 bulb = PropKit.RoadLamp(mb);
                        ctx.lampHeads.Add(basePos + rot * bulb);
                    }
                    col.Box(basePos + Vector3.up * 2.8f, new Vector3(0.25f, 5.6f, 0.25f), Vector2.zero);
                    if (++n % 120 == 0)
                    {
                        ctx.MeshObject("Street Lights " + n / 120, parent, mb, ctx.mats.atlas, col);
                        mb = new MeshBuilder();
                        col = new MeshBuilder();
                    }
                }
            }
            ctx.MeshObject("Street Lights last", parent, mb, ctx.mats.atlas, col);
            Debug.Log($"[Nainital] lamps total: {ctx.lampHeads.Count}");
        }

        // ------------------------------------------------------------------ trees
        static void BuildTrees(WorldCtx ctx)
        {
            var t = ctx.world.trees;
            var data = new List<float>(t.Length / 4 * 6);
            var rnd = new System.Random(11);
            for (int i = 0; i + 3 < t.Length; i += 4)
            {
                Vector2 p = new Vector2(t[i], t[i + 1]);
                if (ctx.InLake(p) || ctx.InKeepClear(p, 1.5f)) continue;
                float h = ctx.H(p);
                float type = t[i + 2];
                if ((int)type == 2 && h - ctx.lakeY > 90f && rnd.NextDouble() < 0.3) type = 4;   // buransh in the oak forests
                data.Add(p.x); data.Add(h - 0.15f); data.Add(p.y);
                data.Add(type); data.Add(t[i + 3]); data.Add((float)rnd.NextDouble() * 360f);
            }
            string path = Gen + "/TreeField.asset";
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            var field = ScriptableObject.CreateInstance<TreeField>();
            field.data = data.ToArray();
            // rocks & boulders on the steep slopes (types 5, 6)
            var td = ctx.terrain.terrainData;
            Vector3 tp = ctx.terrain.transform.position;
            int rocks = 0;
            float mcx = ctx.world.meta.mapCx, mcz = ctx.world.meta.mapCz;
            for (float z = mcz - HalfSize(ctx) + 30f; z < mcz + HalfSize(ctx) - 30f; z += 7f)
                for (float x = mcx - HalfSize(ctx) + 30f; x < mcx + HalfSize(ctx) - 30f; x += 7f)
                {
                    float jx = x + (float)(rnd.NextDouble() - 0.5) * 6f, jz = z + (float)(rnd.NextDouble() - 0.5) * 6f;
                    float u = (jx - tp.x) / td.size.x, v = (jz - tp.z) / td.size.z;
                    float steep = td.GetSteepness(u, v);
                    double chance = steep > 40f ? 0.4 : steep > 28f ? 0.04 : 0.004;
                    if (rnd.NextDouble() > chance) continue;
                    var p2 = new Vector2(jx, jz);
                    if (ctx.InLake(p2) || ctx.LakeDistance(p2) < 15f || NearAnyRoad(ctx, p2)) continue;   // no boulders on roads
                    float s = 0.5f + (float)rnd.NextDouble() * (steep > 40f ? 1.8f : 1.0f);
                    data.Add(jx); data.Add(ctx.H(p2) - 0.35f * s); data.Add(jz);
                    data.Add(rnd.NextDouble() < 0.5 ? 5 : 6); data.Add(s); data.Add((float)rnd.NextDouble() * 360f);
                    rocks++;
                }
            Debug.Log($"[Nainital] rocks: {rocks}");

            field.data = data.ToArray();
            field.lod0 = new Mesh[7];
            field.lod1 = new Mesh[7];
            field.lod2 = new Mesh[7];
            AssetDatabase.CreateAsset(field, path);
            for (int k = 0; k < 7; k++)
            {
                bool card = k <= 3;   // deodar, chir, banj oak, cypress: leaf cards near, impostor cards far
                field.lod0[k] = k >= 5 ? PropKit.Rock(k - 5, 0) : k == 4 ? PropKit.Rhododendron(0) : TreeKit.Model(k);
                field.lod1[k] = k >= 5 ? PropKit.Rock(k - 5, 1) : k == 4 ? PropKit.Rhododendron(1) : TreeKit.Billboard(k);
                field.lod2[k] = k >= 5 ? PropKit.Rock(k - 5, 2) : k == 4 ? PropKit.Rhododendron(2) : TreeKit.Billboard(k);
                AssetDatabase.AddObjectToAsset(field.lod0[k], field);
                AssetDatabase.AddObjectToAsset(field.lod1[k], field);
                AssetDatabase.AddObjectToAsset(field.lod2[k], field);
            }
            field.cards = new[] { true, true, true, true, false, false, false };
            field.trunkRadius = (float[])PropKit.TrunkRadius.Clone();
            field.trunkHeight = (float[])PropKit.TrunkHeight.Clone();
            EditorUtility.SetDirty(field);
            var go = new GameObject("Forest (" + field.Count + " trees, GPU instanced)");
            go.transform.SetParent(ctx.worldRoot, false);
            var tr = go.AddComponent<TreeRenderer>();
            tr.field = field;
            tr.material = ctx.mats.atlas;
            tr.leafMaterial = TreeKit.LeafMaterial();
            tr.barkMaterial = TreeKit.BarkMaterial(ctx.mats.atlas);
            tr.impostorMaterial = TreeKit.ImpostorMaterial();
            tr.lod0Distance = 140f;   // the card trees hold up close; impostors take over further out
            tr.maxDistance = 2800f;   // hills across the lake keep their forest (phones clamp this to 900)
        }

        // ------------------------------------------------------------------ lighting
        static void BuildLighting(WorldCtx ctx)
        {
            var root = new GameObject("Lighting").transform;
            var sunGo = new GameObject("Sun / Moon");
            sunGo.transform.SetParent(root, false);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.intensity = 1.2f;
            sunGo.transform.rotation = Quaternion.Euler(30, 210, 0);
            RenderSettings.sun = sun;
            RenderSettings.skybox = ctx.mats.sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 1200f;   // as on PC (GameManager); the atmosphere pass does the aerial perspective
            RenderSettings.fogEndDistance = 7000f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;

            var ls = new LightingSettings { name = "NainitalLighting", bakedGI = false, realtimeGI = false };
            string lsPath = Gen + "/NainitalLighting.lighting";
            if (File.Exists(lsPath)) AssetDatabase.DeleteAsset(lsPath);
            AssetDatabase.CreateAsset(ls, lsPath);
            Lightmapping.lightingSettings = ls;

            var probeGo = new GameObject("Lake Reflection Probe");
            probeGo.transform.SetParent(root, false);
            probeGo.transform.position = new Vector3(ctx.lakeCenter.x, ctx.lakeY + 25f, ctx.lakeCenter.y);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.resolution = 128;
            probe.size = new Vector3(2400f, 600f, 2400f);
            probe.hdr = true;
            probe.farClipPlane = 3000f;
            probe.cullingMask = ~((1 << 4) | (1 << ProjectSetup.PlayerLayer) | (1 << ProjectSetup.CharacterLayer));   // never the water itself

            string volPath = Gen + "/NainitalPost.asset";
            if (File.Exists(volPath)) AssetDatabase.DeleteAsset(volPath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, volPath);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.65f);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var ca = profile.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.2f);
            ca.contrast.Override(10f);
            ca.saturation.Override(-13f);   // street photos of Nainital: soft, slightly hazy colour, not neon
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.2f);
            // near = sharp, far hills softly blurred
            var dof = profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(160f);
            dof.gaussianEnd.Override(1100f);
            dof.gaussianMaxRadius.Override(1.1f);
            // cinematic camera feel
            var mblur = profile.Add<MotionBlur>(true);
            mblur.mode.Override(MotionBlurMode.CameraOnly);
            mblur.quality.Override(MotionBlurQuality.Low);
            mblur.intensity.Override(0.22f);
            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.07f);
            var chroma = profile.Add<ChromaticAberration>(true);
            chroma.intensity.Override(0.05f);
            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(2f);
            wb.tint.Override(0f);
            foreach (var comp in profile.components) AssetDatabase.AddObjectToAsset(comp, profile);
            EditorUtility.SetDirty(profile);
            var volGo = new GameObject("Post Processing");
            volGo.transform.SetParent(root, false);
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;

            // night grade: dark, slightly blue, bloom on lamps (blended in by DayNightCycle)
            string nightPath = Gen + "/NainitalNight.asset";
            if (File.Exists(nightPath)) AssetDatabase.DeleteAsset(nightPath);
            var night = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(night, nightPath);
            var nca = night.Add<ColorAdjustments>(true);
            nca.postExposure.Override(0.1f);
            nca.colorFilter.Override(new Color(0.82f, 0.9f, 1f));
            nca.saturation.Override(-6f);
            nca.contrast.Override(12f);
            var nb = night.Add<Bloom>(true);
            nb.threshold.Override(0.8f);
            nb.intensity.Override(1.8f);
            nb.scatter.Override(0.72f);
            foreach (var comp in night.components) AssetDatabase.AddObjectToAsset(comp, night);
            EditorUtility.SetDirty(night);
            var nightGo = new GameObject("Night Grade");
            nightGo.transform.SetParent(root, false);
            var nvol = nightGo.AddComponent<Volume>();
            nvol.isGlobal = true;
            nvol.priority = 1f;
            nvol.sharedProfile = night;
            nvol.weight = 0f;

            var dn = new GameObject("Day Night Cycle").AddComponent<DayNightCycle>();
            dn.transform.SetParent(root, false);
            dn.sun = sun;
            dn.skyMaterial = ctx.mats.sky;
            dn.emissiveMaterials = new[] { ctx.mats.atlas };
            dn.lakeProbe = probe;
            dn.nightVolume = nvol;
            dn.valleyHeight = ctx.lakeY;
            dn.emissionIntensity = 3.2f;
            dn.timeOfDay = 17.4f;
            dn.Apply();

            BuildLandmarkLamps(ctx);
            var lm = new GameObject("Street Lamp Lights").AddComponent<LampManager>();
            lm.transform.SetParent(root, false);
            lm.lampHeads = ctx.lampHeads.ToArray();
            BuildLampGlow(ctx, lm.transform);
        }

        // ------------------------------------------------------------------ gameplay
        static GameObject Character(WorldCtx ctx, string name, Transform parent, Vector3 pos, Quaternion rot, int layer,
                                    int skin, int hair, int top, int legs, int shoes, bool female, bool cap, bool shawl,
                                    CharacterLibrary.Entry look = null)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0, 0.92f, 0);
            cc.stepOffset = 0.4f;
            cc.slopeLimit = 52f;
            cc.skinWidth = 0.04f;
            cc.minMoveDistance = 0f;
            go.AddComponent<CharacterMotor>();
            var rigGo = new GameObject("Rig") { layer = layer };
            rigGo.transform.SetParent(go.transform, false);
            rigGo.SetActive(false);   // set the look first, then enable (the rig builds itself on enable)
            var rig = rigGo.AddComponent<HumanoidRig>();
            rig.skin = skin; rig.hair = hair; rig.top = top; rig.legs = legs; rig.shoes = shoes;
            rig.female = female; rig.woolCap = cap; rig.shawl = shawl;
            rig.material = ctx.mats.atlas;
            rig.bodyType = female ? HumanoidRig.BodyType.Woman : HumanoidRig.BodyType.Man;
            if (look != null) CharacterLibrary.Apply(look, rig, cc);   // rigged Tripo person instead of the code-built body
            rigGo.SetActive(true);
            return go;
        }

        /// <summary>A rigged look for a townsperson. child/elder: null = any age.</summary>
        static CharacterLibrary.Entry PickLook(bool female, bool? child, bool? elder, System.Random rnd)
        {
            var lib = CharacterLibrary.Instance;
            return lib ? lib.Pick(female, child, elder, rnd) : null;
        }

        static readonly Dictionary<HumanoidRig.BodyType, string[]> NamesByType = new Dictionary<HumanoidRig.BodyType, string[]>
        {
            { HumanoidRig.BodyType.Man, new[] { "Tourist (Delhi se)", "Honeymoon couple wala", "Photographer bhaiya", "College student", "Local bhaiya" } },
            { HumanoidRig.BodyType.Woman, new[] { "Local Didi", "Bengali tourist", "College student", "Honeymoon couple wali" } },
            { HumanoidRig.BodyType.OldMan, new[] { "Pahadi Dada", "Retired uncle", "Kumaoni Dada" } },
            { HumanoidRig.BodyType.OldWoman, new[] { "Kumaoni Aunty", "Dadi", "Amma ji" } },
            { HumanoidRig.BodyType.Boy, new[] { "School bachcha", "Chhota tourist" } },
            { HumanoidRig.BodyType.Girl, new[] { "Chhoti bachchi", "School wali bachchi" } },
        };

        static string NameFor(HumanoidRig.BodyType t, System.Random rnd)
        {
            var n = NamesByType[t];
            return n[rnd.Next(n.Length)];
        }

        static Vector3 GroundNear(WorldCtx ctx, Vector2 p) => new Vector3(p.x, ctx.H(p) + 0.1f, p.y);

        static void BuildGameplay(WorldCtx ctx)
        {
            var root = ctx.gameplayRoot;
            var gmGo = new GameObject("Game Manager");
            gmGo.transform.SetParent(root, false);
            gmGo.AddComponent<GameManager>().atlasMaterial = ctx.mats.atlas;
            gmGo.AddComponent<GameInput>();
            var lt = gmGo.AddComponent<LocationTracker>();
            lt.names = ctx.zoneNames.ToArray();
            lt.centers = ctx.zoneCenters.ToArray();
            lt.radii = ctx.zoneRadii.ToArray();

            var lakeGo = new GameObject("Lake Area");
            lakeGo.transform.SetParent(root, false);
            var la = lakeGo.AddComponent<LakeArea>();
            la.outline = ctx.lake;
            la.innerOutline = Offset(ctx.lake, -7f);
            la.waterY = ctx.lakeY;

            // ---- player
            var player = Character(ctx, "Player", root, ctx.spawn, ctx.spawnRot, ProjectSetup.PlayerLayer,
                                   Pal.SkinMid, Pal.HairBlack, Pal.JacketBlue, Pal.Jeans, Pal.ShoeWhite, false, false, false,
                                   CharacterLibrary.Instance ? CharacterLibrary.Instance.Find(CharacterLibraryBuilder.PlayerLook) : null);
            var ph = player.AddComponent<Health>();
            ph.max = ph.current = 100f;
            ph.regenPerSecond = 3f;
            ph.regenDelay = 6f;
            var pc = player.AddComponent<Combatant>();
            pc.hitMask = 1 << ProjectSetup.CharacterLayer;
            player.AddComponent<PlayerController>();
            player.AddComponent<PlayerInteractor>();

            // ---- camera
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(root, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 7200f;
            cam.fieldOfView = 60f;
            var urpCam = camGo.AddComponent<UniversalAdditionalCameraData>();
            urpCam.renderPostProcessing = true;
            urpCam.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            urpCam.antialiasingQuality = AntialiasingQuality.High;
            camGo.AddComponent<AudioListener>();
            var tpc = camGo.AddComponent<ThirdPersonCamera>();
            tpc.target = player.transform;
            tpc.collisionMask = ~((1 << ProjectSetup.PlayerLayer) | (1 << ProjectSetup.CharacterLayer) | (1 << 2) | (1 << 4) | (1 << 5));
            tpc.yaw = ctx.spawnRot.eulerAngles.y;
            tpc.pitch = 10f;
            camGo.transform.SetPositionAndRotation(ctx.spawn - ctx.spawnRot * Vector3.forward * 4.5f + Vector3.up * 2.2f, Quaternion.Euler(10f, tpc.yaw, 0));

            // ---- tourists on the promenade
            var npcRoot = new GameObject("Tourists & Locals").transform;
            npcRoot.SetParent(root, false);
            var rnd = new System.Random(42);
            int[] tops = { Pal.JacketRed, Pal.JacketBlue, Pal.JacketGreen, Pal.JacketYellow, Pal.JacketBlack, Pal.ShirtWhite, Pal.Shawl, Pal.SareeOrange, Pal.SareePink };
            int[] legsM = { Pal.Jeans, Pal.PantsKhaki, Pal.PantsBlack };
            int[] legsF = { Pal.SareeOrange, Pal.SareePink, Pal.Shawl, Pal.JacketBlue };
            int[] skins = { Pal.SkinLight, Pal.SkinMid, Pal.SkinDark };
            string[] names = { "Tourist (Delhi se)", "Local Didi", "Honeymoon couple wala", "College student", "Pahadi Dada", "Photographer bhaiya", "Bengali tourist", "Kumaoni Aunty" };
            var path = ctx.promenadePath.ToArray();
            for (int i = 0; i < 26 && path.Length > 10; i++)
            {
                int idx = rnd.Next(path.Length);
                bool female = rnd.NextDouble() < 0.45;
                double age = rnd.NextDouble();   // promenade mix: ~12% children, ~22% elders, rest adults
                var look = PickLook(female, age < 0.12, age < 0.12 ? (bool?)null : age < 0.34, rnd);
                var npc = Character(ctx, "NPC " + i, npcRoot, path[idx] + Vector3.up * 0.05f, Quaternion.identity, ProjectSetup.CharacterLayer,
                                    skins[rnd.Next(skins.Length)], rnd.NextDouble() < 0.15 ? Pal.HairBrown : Pal.HairBlack,
                                    tops[rnd.Next(tops.Length)], female ? legsF[rnd.Next(legsF.Length)] : legsM[rnd.Next(legsM.Length)],
                                    rnd.NextDouble() < 0.5 ? Pal.ShoeBrown : Pal.ShoeWhite, female, rnd.NextDouble() < 0.3, female && rnd.NextDouble() < 0.4,
                                    look);
                var type = npc.GetComponentInChildren<HumanoidRig>(true).bodyType;
                var w = npc.AddComponent<NpcWalker>();
                w.path = path;
                w.index = (idx + 1) % path.Length;
                w.direction = rnd.NextDouble() < 0.5 ? 1 : -1;
                w.speed = HumanoidRig.WalkSpeedFor(type) * (0.85f + (float)rnd.NextDouble() * 0.3f);
                w.npcName = look != null ? NameFor(type, rnd) : names[rnd.Next(names.Length)];
            }

            // ---- thugs (fight!)
            var enemyRoot = new GameObject("Gunde (enemies)").transform;
            enemyRoot.SetParent(root, false);
            void Gang(string where, Vector2 at, int count, bool nightOnly)
            {
                for (int i = 0; i < count; i++)
                {
                    Vector2 p = ctx.PushFromLake(at + new Vector2((i - count / 2f) * 2.2f, (i % 2) * 2f), 15f);
                    var e = Character(ctx, $"Gunda ({where}) {i + 1}", enemyRoot, GroundNear(ctx, p), Quaternion.Euler(0, rnd.Next(360), 0), ProjectSetup.CharacterLayer,
                                      Pal.SkinMid, Pal.HairBlack, i % 2 == 0 ? Pal.JacketBlack : Pal.JacketRed, Pal.PantsBlack, Pal.ShoeBrown, false, i == 0, false);
                    var h = e.AddComponent<Health>();
                    h.max = h.current = 55f;
                    var c = e.AddComponent<Combatant>();
                    c.hitMask = 1 << ProjectSetup.PlayerLayer;
                    c.heavyPuncher = i == 0;
                    c.damageMultiplier = 0.8f;
                    var ai = e.AddComponent<EnemyAI>();
                    ai.displayName = i == 0 ? "Gang ka Boss" : "Gunda";
                    ai.nightOnly = nightOnly;
                }
            }
            var bazaar = ctx.Poi("Bada Bazar");
            if (bazaar.HasValue) Gang("Bada Bazar", bazaar.Value + new Vector2(25f, -15f), 3, false);
            Gang("Tallital", ctx.tallitalShore + OutwardAt(ctx, ctx.tallitalShore) * 35f, 2, true);
            if (ctx.landmarkSites.TryGetValue("Snow View", out var snow))
                Gang("Snow View road", new Vector2(snow.x, snow.z) + (ctx.lakeCenter - new Vector2(snow.x, snow.z)).normalized * 60f, 2, false);

            // ---- stalls & benches at viewpoints, bazaar shopkeepers
            var extras = new GameObject("Viewpoint benches & stalls").transform;
            extras.SetParent(root, false);
            var benchMb = new MeshBuilder();
            PropKit.Bench(benchMb);
            var benchMesh = ctx.Save(benchMb, "BenchVP");
            foreach (var kv in ctx.landmarkSites)
            {
                if (!(kv.Key.Contains("Peak") || kv.Key.Contains("Snow View") || kv.Key.Contains("Tiffin") || kv.Key.Contains("Land's End") || kv.Key.Contains("Himalaya Darshan"))) continue;
                Vector3 c = kv.Value;
                Vector2 toLake = (ctx.lakeCenter - new Vector2(c.x, c.z)).normalized;
                var rot = Quaternion.LookRotation(new Vector3(toLake.x, 0, toLake.y));
                var go = ctx.Instance("Bench (" + kv.Key + ")", extras, benchMesh, c + rot * new Vector3(2f, 0, 1.5f), rot, true);
                var seat = new GameObject("Seat").transform;
                seat.SetParent(go.transform, false);
                seat.localPosition = new Vector3(0, 0.05f, 0);
                go.AddComponent<Bench>().seat = seat;
            }
            if (bazaar.HasValue && ctx.landmarkSites.TryGetValue("Bada Bazar", out var bz))
            {
                var smb = new MeshBuilder();
                PropKit.FoodStall(smb, Pal.Canopy);
                var go = ctx.MeshObject("Bazaar Momos", extras, smb, ctx.mats.atlas);
                go.transform.SetPositionAndRotation(bz + new Vector3(0, 0, -15f), Quaternion.identity);
                go.AddComponent<BoxCollider>().size = new Vector3(2.3f, 2f, 1.1f);
                var fs = go.AddComponent<FoodStall>();
                fs.stallName = "Bada Bazar Momos";
                fs.radius = 2.8f;
            }
        }
    }
}
