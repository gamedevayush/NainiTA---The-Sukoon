using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nainital.EditorTools
{
    /// <summary>
    /// The user's vehicle models (gamedevayush/SamjhaaoSkills models/library; user, 2026-11-11: "5-10 vehicles, poly 20k
    /// ke upar na ho"), made game-ready by Tools/make_vehicle_lods.py and copied in by Tools/install_vehicles.py.
    /// Each FBX (Mesh_L0/L1/L2) and its JSON sidecar becomes a prefab with URP materials and an LOD group, and all of
    /// them are listed in Resources/VehicleLibrary for VehicleFactory.
    /// </summary>
    public static class VehicleKit
    {
        public const string Dir = "Assets/Nainital/Vehicles";
        const string LibraryPath = "Assets/Resources/VehicleLibrary.asset";

        [System.Serializable] class Sidecar { public string name; public float[] size; public int[] tris; public Mat[] materials; public Part[] objects; public Lamps lamps; }
        [System.Serializable] class Part { public string name; public int[] mats; public int[] tris; }
        [System.Serializable] class Lamps { public float[] front; public float[] rear; }
        [System.Serializable] class Mat { public string name; public string color; public string normal; public string ms; public float[] rgba; }

        /// <summary>What each model is in the town (kind, white taxi or not, how common).</summary>
        static readonly Dictionary<string, (CarType type, bool taxi, float weight, bool tint)> Roles = new Dictionary<string, (CarType, bool, float, bool)>
        {
            { "Ertiga", (CarType.Suv, true, 1.4f, true) },
            { "InnovaCrysta", (CarType.Suv, true, 1.2f, true) },
            { "Bolero", (CarType.Suv, true, 1.0f, true) },
            { "Scorpio", (CarType.Suv, false, 0.9f, true) },
            { "WagonR", (CarType.Hatchback, false, 1f, true) },
            { "HondaAmaze", (CarType.Sedan, false, 1f, true) },
            { "BoleroPickup", (CarType.Pickup, false, 1f, false) },
            { "Tata407", (CarType.Pickup, false, 0.6f, false) },
            { "TouristCoach", (CarType.Bus, false, 1f, false) },
            { "TouristBus", (CarType.Bus, false, 0.45f, false) },
        };

        [MenuItem("Nainital/7. Import vehicles", priority = 7)]
        public static void Build()
        {
            AssetDatabase.Refresh();
            string models = Dir + "/Models";
            if (!Directory.Exists(models)) { Debug.LogWarning("[Nainital] no vehicles in " + models); return; }
            Directory.CreateDirectory(Dir + "/Materials");
            Directory.CreateDirectory(Dir + "/Prefabs");
            var entries = new List<VehicleLibrary.Entry>();
            foreach (var json in Directory.GetFiles(models, "*.json"))
            {
                var sc = JsonUtility.FromJson<Sidecar>(File.ReadAllText(json));
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{models}/{sc.name}.fbx");
                if (!model) { Debug.LogWarning($"[Nainital] vehicle {sc.name}: FBX missing"); continue; }
                var mats = new Material[Mathf.Max(1, sc.materials?.Length ?? 0)];
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = MakeMaterial($"{sc.name}_m{i}", sc.materials != null && i < sc.materials.Length ? sc.materials[i] : null);

                var root = new GameObject(sc.name);
                var inst = Object.Instantiate(model, root.transform, false);
                inst.name = "Model";
                var lods = new Renderer[3];
                var wheelR = new List<Renderer>();
                float hub = 0f;
                foreach (var mr in inst.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mf = mr.GetComponent<MeshFilter>();
                    int n = mf && mf.sharedMesh ? mf.sharedMesh.subMeshCount : 1;
                    Part part = null;
                    if (sc.objects != null) foreach (var o in sc.objects) if (o.name == mr.name) part = o;
                    var arr = new Material[n];
                    var usedSlot = new bool[part != null && part.mats != null ? part.mats.Length : 0];
                    for (int k = 0; k < n; k++)
                    {
                        // which of this part's material slots is sub-mesh k: the one with the same number of triangles
                        // (Unity does not always keep Blender's slot order), else the slot in the same place
                        int slot = k;
                        if (part != null && part.tris != null && part.tris.Length == usedSlot.Length && mf && mf.sharedMesh)
                        {
                            int nk = (int)mf.sharedMesh.GetSubMesh(k).indexCount / 3, bestD = int.MaxValue;
                            for (int j = 0; j < usedSlot.Length; j++)
                            {
                                if (usedSlot[j]) continue;
                                int dd = Mathf.Abs(part.tris[j] - nk);
                                if (dd < bestD) { bestD = dd; slot = j; }
                            }
                        }
                        if (slot < usedSlot.Length) usedSlot[slot] = true;
                        int g = part != null && part.mats != null && slot < part.mats.Length ? part.mats[slot] : slot;
                        arr[k] = mats[Mathf.Clamp(g, 0, mats.Length - 1)];
                    }
                    mr.sharedMaterials = arr;
                    mr.shadowCastingMode = mr.name.EndsWith("L2") ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    for (int l = 0; l < 3; l++) if (mr.name.EndsWith("L" + l)) lods[l] = mr;
                    if (mr.name.StartsWith("Wheel_")) { wheelR.Add(mr); hub += mr.transform.position.y; }
                }
                if (wheelR.Count > 0) hub /= wheelR.Count;
                var list = new List<LOD>();
                float[] h = { 0.1f, 0.025f, 0.006f };   // a 4.4 m car: full detail to ~40 m, ~150 m, culled near 600 m
                for (int l = 0; l < 3; l++)
                {
                    if (!lods[l]) continue;
                    var rs = new List<Renderer> { lods[l] };
                    if (l == 0) rs.AddRange(wheelR);   // the spinning tyres belong to the near detail; farther LODs keep them on the body
                    list.Add(new LOD(h[l], rs.ToArray()));
                }
                if (sc.lamps != null) AddLamps(root.transform, sc.lamps, sc.size[1]);
                if (list.Count == 0) { Object.DestroyImmediate(root); continue; }
                var lg = root.AddComponent<LODGroup>();
                lg.SetLODs(list.ToArray());
                lg.RecalculateBounds();
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Dir}/Prefabs/{sc.name}.prefab");
                Object.DestroyImmediate(root);

                (CarType type, bool taxi, float weight, bool tint) role = Roles.TryGetValue(sc.name, out var r) ? r : (CarType.Sedan, false, 1f, true);
                entries.Add(new VehicleLibrary.Entry
                {
                    name = sc.name, prefab = prefab, size = new Vector3(sc.size[0], sc.size[2], sc.size[1]),
                    type = role.type, taxi = role.taxi, weight = role.weight, tint = role.tint, wheelRadius = hub,
                });
            }
            Directory.CreateDirectory("Assets/Resources");
            var lib = AssetDatabase.LoadAssetAtPath<VehicleLibrary>(LibraryPath);
            if (!lib) { lib = ScriptableObject.CreateInstance<VehicleLibrary>(); AssetDatabase.CreateAsset(lib, LibraryPath); }
            lib.entries = entries.ToArray();
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Nainital] vehicles: {entries.Count} prefabs -> {Dir}/Prefabs, library {LibraryPath}");
        }

        /// <summary>Two head lamps (warm white) and two tail lamps (red) as glowing quads where the model's lamps are
        /// (found by make_vehicle_lods.py: [half spacing, height, distance from the middle]). Off until VehicleLamps lights
        /// them.</summary>
        static void AddLamps(Transform root, Lamps l, float length)
        {
            if (l.front == null || l.front.Length < 3 || l.rear == null || l.rear.Length < 3) return;
            bool big = length > 6.5f;
            float w = big ? 0.32f : 0.24f, hgt = big ? 0.16f : 0.11f;
            var verts = new List<Vector3>();
            var tf = new List<int>();
            var tr = new List<int>();
            void Quad(Vector3 c, float dir, List<int> tris)
            {
                int b = verts.Count;
                Vector3 r = new Vector3(w * 0.5f, 0f, 0f), u = new Vector3(0f, hgt * 0.5f, 0f);
                verts.Add(c - r - u); verts.Add(c + r - u); verts.Add(c + r + u); verts.Add(c - r + u);
                if (dir > 0) tris.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });   // faces +Z
                else tris.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });           // faces -Z
            }
            foreach (float sx in new[] { -1f, 1f })
            {
                Quad(new Vector3(sx * l.front[0], l.front[1], l.front[2] + 0.03f), 1f, tf);
                Quad(new Vector3(sx * l.rear[0], l.rear[1], -(l.rear[2] + 0.03f)), -1f, tr);
            }
            var mesh = new Mesh { name = root.name + "_Lamps" };
            mesh.SetVertices(verts);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(tf, 0);
            mesh.SetTriangles(tr, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            string mp = $"{Dir}/Prefabs/{root.name}_Lamps.asset";
            if (File.Exists(mp)) AssetDatabase.DeleteAsset(mp);
            AssetDatabase.CreateAsset(mesh, mp);
            var go = new GameObject("Lamps");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { LampMaterial("M_LampHead", new Color(4f, 3.7f, 3.1f)), LampMaterial("M_LampTail", new Color(3f, 0.18f, 0.1f)) };
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.enabled = false;
        }

        static Material LampMaterial(string id, Color hdr)
        {
            string path = $"{Dir}/Materials/{id}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = id };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", hdr);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material MakeMaterial(string id, Mat m)
        {
            string path = $"{Dir}/Materials/{id}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = id };
                AssetDatabase.CreateAsset(mat, path);
            }
            Texture2D T(string f) => !string.IsNullOrEmpty(f) ? AssetDatabase.LoadAssetAtPath<Texture2D>($"{Dir}/Textures/{f}") : null;
            var col = T(m?.color);
            var nrm = T(m?.normal);
            var ms = T(m?.ms);
            string n = (m?.name ?? "").ToLowerInvariant();
            Color c = m != null && m.rgba != null && m.rgba.Length >= 3 ? new Color(m.rgba[0], m.rgba[1], m.rgba[2], 1f) : Color.white;
            float smooth = 0.45f, metal = 0.1f;
            if (!col)   // untextured part: what it is decides how it looks
            {
                if (n.Contains("glass") || n.Contains("window")) { c = new Color(0.07f, 0.09f, 0.11f); smooth = 0.92f; metal = 0f; }
                else if (n.Contains("light") || n.Contains("lamp")) { c = new Color(0.85f, 0.85f, 0.8f); smooth = 0.85f; metal = 0f; }
                else if (n.Contains("tyre") || n.Contains("tire") || n.Contains("rubber") || n.Contains("wheel")) { c = new Color(0.06f, 0.06f, 0.06f); smooth = 0.2f; metal = 0f; }
                else if (n.Contains("chrome") || n.Contains("metal")) { c = new Color(0.7f, 0.7f, 0.72f); smooth = 0.75f; metal = 0.9f; }
                else if (n.Contains("emred") || n.Contains("taillight") || n.Contains("brake")) { c = new Color(0.62f, 0.05f, 0.04f); smooth = 0.8f; metal = 0f; }
                else if (n.Contains("emwhite") || n.Contains("headlight")) { c = new Color(0.92f, 0.92f, 0.88f); smooth = 0.85f; metal = 0f; }
            }
            mat.SetTexture("_BaseMap", col);
            mat.mainTexture = col;
            mat.SetColor("_BaseColor", c);
            mat.SetFloat("_Metallic", metal);
            mat.SetFloat("_Smoothness", smooth);
            if (ms)
            {
                mat.SetTexture("_MetallicGlossMap", ms);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetFloat("_Smoothness", 0.85f);   // scales the map's smoothness
                mat.SetTexture("_OcclusionMap", ms);
                mat.EnableKeyword("_OCCLUSIONMAP");
            }
            if (nrm)
            {
                mat.SetTexture("_BumpMap", nrm);
                mat.EnableKeyword("_NORMALMAP");
            }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }

    /// <summary>Import settings for the vehicle FBX and textures.</summary>
    class VehicleAssetImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(VehicleKit.Dir + "/Models/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importAnimation = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;   // VehicleKit assigns URP materials
            mi.importNormals = ModelImporterNormals.Calculate;              // decimated meshes shade better re-smoothed
            mi.normalSmoothingAngle = 45f;
            mi.meshCompression = ModelImporterMeshCompression.Low;
            mi.isReadable = false;
            mi.addCollider = false;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(VehicleKit.Dir + "/Textures/")) return;
            var ti = (TextureImporter)assetImporter;
            bool normal = assetPath.Contains("_normal");
            bool mask = assetPath.Contains("_ms");
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = !normal && !mask;
            ti.maxTextureSize = 1024;
            ti.mipmapEnabled = true;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android", overridden = true, maxTextureSize = 1024, format = TextureImporterFormat.ASTC_6x6,
            });
        }
    }
}
