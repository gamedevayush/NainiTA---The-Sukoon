using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Turns the auto-rigged Tripo characters (FBX + JPG from Tools/rig_tripo_character.py, copied into
    /// Assets/Nainital/Characters/Models and /Textures) into prefabs with a URP material and two LODs,
    /// and lists them in Resources/CharacterLibrary for the world builder and the crowd spawner.
    /// </summary>
    public static class CharacterLibraryBuilder
    {
        public const string Dir = "Assets/Nainital/Characters";
        public const string LibraryPath = "Assets/Resources/CharacterLibrary.asset";
        public const string PlayerLook = "m_young_puffer";

        // id, body type, name shown in dialogs, height (m, same as passed to the rig script), crowd weight
        static readonly (string id, HumanoidRig.BodyType type, string name, float height, float weight)[] Cast =
        {
            ("m_young_puffer", HumanoidRig.BodyType.Man,      "Tourist bhaiya",  1.74f, 0.35f),   // the player's look: rare in crowds
            ("m_man_flannel",  HumanoidRig.BodyType.Man,      "Local bhaiya",    1.78f, 1f),
            ("f_young_hoodie", HumanoidRig.BodyType.Woman,    "College student", 1.62f, 1f),
            ("f_young_denim",  HumanoidRig.BodyType.Woman,    "Tourist didi",    1.64f, 1f),
            ("m_old_sweater",  HumanoidRig.BodyType.OldMan,   "Retired uncle",   1.68f, 1f),
            ("m_old_kurta",    HumanoidRig.BodyType.OldMan,   "Pahadi Dada",     1.66f, 1f),
            ("f_old_salwar",   HumanoidRig.BodyType.OldWoman, "Kumaoni Amma",    1.55f, 1f),
            ("f_old_saree",    HumanoidRig.BodyType.OldWoman, "Dadi",            1.53f, 1f),
            ("m_boy_school",   HumanoidRig.BodyType.Boy,      "School bachcha",  1.40f, 1f),
            ("m_boy_hoodie",   HumanoidRig.BodyType.Boy,      "Ladka",           1.45f, 1f),
            ("f_girl",         HumanoidRig.BodyType.Girl,     "School wali bachchi", 1.36f, 1f),
            ("f_girl_small",   HumanoidRig.BodyType.Girl,     "Chhoti bachchi",  1.12f, 1f),
        };

        [MenuItem("Nainital/4. Import rigged characters", priority = 4)]
        public static void Build()
        {
            AssetDatabase.Refresh();
            Directory.CreateDirectory(Dir + "/Materials");
            Directory.CreateDirectory(Dir + "/Prefabs");
            Directory.CreateDirectory("Assets/Resources");
            var entries = new List<CharacterLibrary.Entry>();
            foreach (var c in Cast)
            {
                string fbx = $"{Dir}/Models/{c.id}.fbx", tex = $"{Dir}/Textures/{c.id}.jpg";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(tex);
                if (!model || !texture)
                {
                    Debug.LogWarning($"[Nainital] character {c.id}: missing {(model ? tex : fbx)}, skipped");
                    continue;
                }
                var prefab = MakePrefab(c.id, model, new[] { MakeMaterial(c.id, texture) });
                if (!prefab) continue;
                entries.Add(new CharacterLibrary.Entry
                {
                    id = c.id, displayName = c.name, prefab = prefab, bodyType = c.type, height = c.height, weight = c.weight,
                });
            }
            // G.O.A.N. villagers: Tripo meshes with Mixamo rigs, 1 m tall as imported, own embedded materials
            foreach (var c in GoanCast)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(c.fbx);
                if (!model) { Debug.LogWarning($"[Nainital] character {c.id}: missing {c.fbx}, skipped"); continue; }
                var prefab = MakePrefab(c.id, model, GoanMaterials(c.id, c.fbx), c.height);
                if (!prefab) continue;
                entries.Add(new CharacterLibrary.Entry
                {
                    id = c.id, displayName = c.name, prefab = prefab, bodyType = c.type, height = c.height, weight = c.weight,
                });
            }
            var lib = AssetDatabase.LoadAssetAtPath<CharacterLibrary>(LibraryPath);
            if (!lib)
            {
                lib = ScriptableObject.CreateInstance<CharacterLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }
            lib.entries = entries.ToArray();
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Nainital] character library: {entries.Count}/{Cast.Length + GoanCast.Length} rigged characters -> {LibraryPath}");
        }

        // id, FBX, body type, name, height (m), crowd weight
        static readonly (string id, string fbx, HumanoidRig.BodyType type, string name, float height, float weight)[] GoanCast =
        {
            ("goan_kisaan", "Assets/GOAN/NPC/NPC1Idle.fbx",  HumanoidRig.BodyType.Man,    "Kisaan bhaiya", 1.70f, 0.7f),
            ("goan_buzurg", "Assets/GOAN/NPC/NPC2Idle.fbx",  HumanoidRig.BodyType.OldMan, "Buzurg chacha", 1.65f, 0.7f),
            ("goan_mistri", "Assets/GOAN/NPC/NPC3Idle.fbx",  HumanoidRig.BodyType.Man,    "Mistri ji",     1.70f, 0.7f),
            ("goan_ladka",  "Assets/GOAN/NPC/NPC4Idle.fbx",  HumanoidRig.BodyType.Boy,    "Gaon ka ladka", 1.35f, 0.7f),
            ("goan_kuli",   "Assets/GOAN/NPC/NPC5Idle.fbx",  HumanoidRig.BodyType.Man,    "Kuli bhaiya",   1.72f, 0.7f),
            ("goan_pandit", "Assets/GOAN/NPC/NPC6Idle.fbx",  HumanoidRig.BodyType.OldMan, "Pandit ji",     1.66f, 0.7f),
            ("goan_sardar", "Assets/GOAN/NPC/NPC7Idle.fbx",  HumanoidRig.BodyType.Man,    "Sardar ji",     1.76f, 0.7f),
            ("goan_didi",   "Assets/GOAN/NPC/NPCW1Idle.fbx", HumanoidRig.BodyType.Woman,  "Gaon ki didi",  1.58f, 0.7f),
        };

        // our Blender rigs use Humanoid names; G.O.A.N.'s are Mixamo ("mixamorig:LeftUpLeg")
        static Transform Bone(Dictionary<string, Transform> bones, string humanoid, string mixamo)
        {
            if (bones.TryGetValue(humanoid, out var t)) return t;
            if (bones.TryGetValue("mixamorig:" + mixamo, out t)) return t;
            return bones.TryGetValue(mixamo, out t) ? t : null;
        }

        [System.Serializable] class SlotList { public Slot[] slots; }
        [System.Serializable] class Slot { public string name; public bool textured; public float[] color; }

        /// <summary>
        /// G.O.A.N. villagers: sub-mesh 0 is the Tripo-textured body, the others are flat-coloured clothes.
        /// Their textures are embedded in the FBX (Unity does not extract them), so they were pulled out
        /// with Blender into GOAN/NPC/Textures together with a per-slot JSON.
        /// </summary>
        static Material[] GoanMaterials(string id, string fbx)
        {
            string stem = Path.GetFileNameWithoutExtension(fbx);
            string dir = fbx.Substring(0, fbx.LastIndexOf('/')) + "/Textures";   // asset paths always use '/'
            string json = $"{dir}/{stem}_slots.json";
            if (!File.Exists(json)) return null;
            var list = JsonUtility.FromJson<SlotList>(File.ReadAllText(json));
            var color = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{stem}_color.jpg");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{stem}_normal.jpg");
            var mats = new Material[list.slots.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                var sl = list.slots[i];
                mats[i] = MakeMaterial($"{id}_m{i}", sl.textured ? color : null, sl.textured ? normal : null,
                                       sl.textured ? Color.white : new Color(sl.color[0], sl.color[1], sl.color[2]));
            }
            return mats;
        }

        static Material MakeMaterial(string id, Texture2D tex) => MakeMaterial(id, tex, null, Color.white);

        static Material MakeMaterial(string id, Texture2D tex, Texture2D normal, Color tint)
        {
            string path = $"{Dir}/Materials/{id}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = id };
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", tex);
            m.mainTexture = tex;
            m.SetColor("_BaseColor", tint);
            if (normal)
            {
                m.SetTexture("_BumpMap", normal);
                m.EnableKeyword("_NORMALMAP");
            }
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", 0.12f);   // Tripo textures are matte cloth/skin with baked shading
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <param name="mats">one per sub-mesh (the last repeats); null keeps the model's own materials</param>
        /// <param name="height">metres; 0 keeps the imported size</param>
        static GameObject MakePrefab(string id, GameObject model, Material[] mats, float height = 0f)
        {
            var root = new GameObject(id);
            var inst = Object.Instantiate(model, root.transform, false);
            inst.name = "Model";
            var bones = new Dictionary<string, Transform>();
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
                if (!bones.ContainsKey(t.name)) bones[t.name] = t;
            var hips = Bone(bones, "Hips", "Hips");
            var lLeg = Bone(bones, "LeftUpperLeg", "LeftUpLeg");
            var rLeg = Bone(bones, "RightUpperLeg", "RightUpLeg");
            if (!hips || !lLeg || !rLeg)
            {
                Debug.LogError($"[Nainital] character {id}: Humanoid/Mixamo bones missing in FBX, skipped");
                Object.DestroyImmediate(root);
                return null;
            }
            // face +Z: the character's left leg on -X (turn about Y only)
            Vector3 lr = lLeg.position - rLeg.position;
            lr.y = 0f;
            float turn = Vector3.SignedAngle(lr, Vector3.left, Vector3.up);
            inst.transform.localRotation = Quaternion.AngleAxis(turn, Vector3.up) * inst.transform.localRotation;
            if (height > 0f)
            {
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var bb = smr.bounds;
                    lo = Mathf.Min(lo, bb.min.y);
                    hi = Mathf.Max(hi, bb.max.y);
                }
                if (hi > lo) root.transform.localScale = Vector3.one * (height / (hi - lo));
            }

            SkinnedMeshRenderer lod0 = null, lod1 = null;
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (mats != null && mats.Length > 0)
                {
                    int n = smr.sharedMesh ? smr.sharedMesh.subMeshCount : 1;
                    var arr = new Material[n];
                    for (int k = 0; k < n; k++) arr[k] = mats[Mathf.Min(k, mats.Length - 1)];
                    smr.sharedMaterials = arr;
                }
                smr.quality = SkinQuality.Bone4;
                smr.shadowCastingMode = ShadowCastingMode.On;
                smr.updateWhenOffscreen = false;
                if (smr.name.EndsWith("_Low")) { lod1 = smr; smr.skinnedMotionVectors = false; }
                else lod0 = smr;
            }
            if (!lod0)
            {
                Debug.LogError($"[Nainital] character {id}: no skinned body in FBX, skipped");
                Object.DestroyImmediate(root);
                return null;
            }
            var lg = root.AddComponent<LODGroup>();
            // full mesh until ~12 m away (1.7 m person, 60 deg FOV), light LOD to ~125 m, then culled
            var lods = new List<LOD> { new LOD(lod1 ? 0.12f : 0.012f, new Renderer[] { lod0 }) };
            if (lod1) lods.Add(new LOD(0.012f, new Renderer[] { lod1 }));
            lg.SetLODs(lods.ToArray());
            lg.RecalculateBounds();

            string path = $"{Dir}/Prefabs/{id}.prefab";
            int lodCount = lod1 ? 2 : 1;   // read before the scene objects are destroyed
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log($"[Nainital] character {id}: prefab {path} (turned {turn:F0} deg to face +Z, LODs {lodCount})");
            return prefab;
        }

        /// <summary>Batch: Unity -batchmode -quit -executeMethod Nainital.EditorTools.CharacterLibraryBuilder.BuildWorldAndCaptureBatch</summary>
        public static void BuildWorldAndCaptureBatch()
        {
            try
            {
                MecanimKit.Build();
                Build();
                GoanKit.BuildPrefabs();
                ProjectSetup.Run();
                WorldBuilder.Build();
                PreviewCapture.Capture();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }
    }

    /// <summary>Import settings for the rigged characters (applied before Unity imports them).</summary>
    class CharacterAssetImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(CharacterLibraryBuilder.Dir + "/Models/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.importBlendShapes = false;
            // Humanoid avatar (T-pose bind, Unity bone names) so the G.O.A.N. Mixamo walk/run/idle clips retarget onto it
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = false;
            mi.optimizeGameObjects = false;                        // bones must stay as Transforms
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.skinWeights = ModelImporterSkinWeights.Standard;    // 4 bones per vertex, as exported
            mi.isReadable = false;
        }

        void OnPreprocessTexture()
        {
            bool goan = assetPath.StartsWith("Assets/GOAN/NPC/Textures/");
            if (!assetPath.StartsWith(CharacterLibraryBuilder.Dir + "/Textures/") && !goan) return;
            var ti = (TextureImporter)assetImporter;
            if (assetPath.Contains("_normal")) ti.textureType = TextureImporterType.NormalMap;
            ti.maxTextureSize = 2048;
            ti.mipmapEnabled = true;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android", overridden = true, maxTextureSize = 1024, format = TextureImporterFormat.ASTC_6x6,
            });
        }
    }
}
