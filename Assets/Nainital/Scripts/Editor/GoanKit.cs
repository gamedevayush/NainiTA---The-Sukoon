using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Buildings from the G.O.A.N. project (gamedevayush/G.O.A.N.-Game-Of-An-Indian-Nation), reduced to
    /// three game-ready LODs by Tools/make_building_lods.py. Each FBX (meshes Mesh_L0/L1/L2) with its
    /// JSON sidecar and textures becomes a prefab with URP materials, an LOD group and a box collider.
    /// </summary>
    public static class GoanKit
    {
        public const string Dir = "Assets/GOAN/Buildings";

        public class Building
        {
            public string name;
            public GameObject prefab;
            public Vector3 size;     // width (x), depth (z), height (y) in metres, pivot at bottom centre
        }

        [System.Serializable] class Sidecar { public string name; public float[] size; public int[] tris; public Mat[] materials; }
        [System.Serializable] class Mat { public string name; public string color; public string normal; }

        static Dictionary<string, Building> cache;

        /// <summary>Prefabs built by <see cref="BuildPrefabs"/>, keyed by name (empty if none).</summary>
        public static Dictionary<string, Building> Load()
        {
            if (cache != null) return cache;
            cache = new Dictionary<string, Building>();
            if (!Directory.Exists(Dir + "/Models")) return cache;
            foreach (var json in Directory.GetFiles(Dir + "/Models", "*.json"))
            {
                var sc = JsonUtility.FromJson<Sidecar>(File.ReadAllText(json));
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/Prefabs/{sc.name}.prefab");
                if (!prefab || sc.size == null || sc.size.Length < 3) continue;
                cache[sc.name] = new Building { name = sc.name, prefab = prefab, size = new Vector3(sc.size[0], sc.size[2], sc.size[1]) };
            }
            return cache;
        }

        [MenuItem("Nainital/5. Import G.O.A.N. buildings", priority = 5)]
        public static void BuildPrefabs()
        {
            cache = null;
            AssetDatabase.Refresh();
            if (!Directory.Exists(Dir + "/Models")) { Debug.LogWarning("[Nainital] no G.O.A.N. buildings in " + Dir); return; }
            Directory.CreateDirectory(Dir + "/Materials");
            Directory.CreateDirectory(Dir + "/Prefabs");
            int made = 0;
            foreach (var json in Directory.GetFiles(Dir + "/Models", "*.json"))
            {
                var sc = JsonUtility.FromJson<Sidecar>(File.ReadAllText(json));
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/Models/{sc.name}.fbx");
                if (!model) { Debug.LogWarning($"[Nainital] G.O.A.N. {sc.name}: FBX missing"); continue; }
                var mats = new Material[Mathf.Max(1, sc.materials?.Length ?? 0)];
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = MakeMaterial($"{sc.name}_m{i}", sc.materials != null && i < sc.materials.Length ? sc.materials[i] : null);

                var root = new GameObject(sc.name);
                var inst = Object.Instantiate(model, root.transform, false);
                inst.name = "Model";
                var lods = new Renderer[3];
                foreach (var mr in inst.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mf = mr.GetComponent<MeshFilter>();
                    int n = mf && mf.sharedMesh ? mf.sharedMesh.subMeshCount : 1;
                    var arr = new Material[n];
                    for (int k = 0; k < n; k++) arr[k] = mats[Mathf.Min(k, mats.Length - 1)];
                    mr.sharedMaterials = arr;
                    mr.shadowCastingMode = mr.name.EndsWith("L2") ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    for (int l = 0; l < 3; l++) if (mr.name.EndsWith("L" + l)) lods[l] = mr;
                }
                var list = new List<LOD>();
                float[] h = { 0.25f, 0.08f, 0.008f };   // ~10 m building: full detail to ~35 m, then ~110 m, culled near 1 km
                for (int l = 0; l < 3; l++) if (lods[l]) list.Add(new LOD(h[l], new[] { lods[l] }));
                if (list.Count == 0) { Object.DestroyImmediate(root); continue; }
                var lg = root.AddComponent<LODGroup>();
                lg.SetLODs(list.ToArray());
                lg.RecalculateBounds();

                var b = lods[0] ? lods[0].bounds : list[0].renderers[0].bounds;   // root at origin: world == local
                var box = root.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = new Vector3(b.size.x * 0.92f, b.size.y, b.size.z * 0.92f);
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic |
                        StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

                PrefabUtility.SaveAsPrefabAsset(root, $"{Dir}/Prefabs/{sc.name}.prefab");
                Object.DestroyImmediate(root);
                made++;
            }
            AssetDatabase.SaveAssets();
            cache = null;
            Debug.Log($"[Nainital] G.O.A.N. buildings: {made} prefabs -> {Dir}/Prefabs");
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
            var col = m != null && !string.IsNullOrEmpty(m.color) ? AssetDatabase.LoadAssetAtPath<Texture2D>($"{Dir}/Textures/{m.color}") : null;
            var nrm = m != null && !string.IsNullOrEmpty(m.normal) ? AssetDatabase.LoadAssetAtPath<Texture2D>($"{Dir}/Textures/{m.normal}") : null;
            mat.SetTexture("_BaseMap", col);
            mat.mainTexture = col;
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.1f);
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

    /// <summary>Import settings for the G.O.A.N. building FBX and textures.</summary>
    class GoanAssetImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(GoanKit.Dir + "/Models/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importAnimation = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;   // GoanKit assigns URP materials
            mi.meshCompression = ModelImporterMeshCompression.Low;
            mi.isReadable = false;
            mi.addCollider = false;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(GoanKit.Dir + "/Textures/")) return;
            var ti = (TextureImporter)assetImporter;
            bool normal = assetPath.Contains("_normal");
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.maxTextureSize = 1024;
            ti.mipmapEnabled = true;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android", overridden = true, maxTextureSize = normal ? 256 : 512, format = TextureImporterFormat.ASTC_6x6,
            });
        }
    }
}
