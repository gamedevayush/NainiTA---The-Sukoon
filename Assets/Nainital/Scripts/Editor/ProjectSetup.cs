using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Nainital.EditorTools
{
    public static class ProjectSetup
    {
        public const int PlayerLayer = 8, CharacterLayer = 9;

        [MenuItem("Nainital/1. Project Setup", priority = 1)]
        public static void Run()
        {
            EnsureLayers();
            ConfigurePlayer();
            ConfigureUrp();
            AssetDatabase.SaveAssets();
            Debug.Log("[Nainital] Project setup done");
        }

        static void EnsureLayers()
        {
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tm.FindProperty("layers");
            layers.GetArrayElementAtIndex(PlayerLayer).stringValue = "Player";
            layers.GetArrayElementAtIndex(CharacterLayer).stringValue = "Character";
            layers.GetArrayElementAtIndex(VehicleFactory.VehicleLayer).stringValue = "Vehicle";
            tm.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "NainitalGame";
            PlayerSettings.productName = "Nainital Open World";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.nainital.openworld");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)28;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        }

        static void ConfigureUrp()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/")) continue;   // never touch read-only package assets
                var a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (!a) continue;
                bool mobile = path.ToLowerInvariant().Contains("mobile");
                a.shadowDistance = mobile ? 70f : 160f;
                a.shadowCascadeCount = mobile ? 1 : 3;
                a.supportsHDR = true;
                a.msaaSampleCount = mobile ? 2 : 2;
                a.renderScale = mobile ? 0.85f : 1f;
                a.supportsCameraDepthTexture = true;    // transparent lake: depth-based colour + shore foam
                a.supportsCameraOpaqueTexture = true;   // transparent lake: refraction of the lake bed
                EditorUtility.SetDirty(a);
                Debug.Log($"[Nainital] URP asset tuned: {path}");
            }
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/")) continue;
                var r = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (!r) continue;
                r.renderingMode = RenderingMode.ForwardPlus;    // many street lamps without per-object limits
                bool mobileR = path.ToLowerInvariant().Contains("mobile");
                foreach (var f in r.rendererFeatures)
                {
                    if (f && f.GetType().Name.Contains("ScreenSpaceAmbientOcclusion"))
                    {
                        f.SetActive(!mobileR);                    // PC renderer: on (HUD turns it off below HIGH)
                        EditorUtility.SetDirty(f);
                    }
                }
                AddAtmosphere(r);
                EditorUtility.SetDirty(r);
                Debug.Log($"[Nainital] Renderer set to Forward+ with atmosphere pass: {path}");
            }
        }

        static Material AtmosphereMaterial()
        {
            const string path = "Assets/Nainital/Settings/M_AtmosphereFog.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m) return m;
            var sh = Shader.Find("Nainital/AtmosphereFog");
            if (!sh) return null;
            System.IO.Directory.CreateDirectory("Assets/Nainital/Settings");
            m = new Material(sh) { name = "M_AtmosphereFog" };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static void AddAtmosphere(UniversalRendererData r)
        {
            foreach (var f in r.rendererFeatures)
                if (f is FullScreenPassRendererFeature && f.name == "Atmosphere Fog") return;
            var mat = AtmosphereMaterial();
            if (!mat) { Debug.LogWarning("[Nainital] AtmosphereFog shader missing"); return; }
            var feat = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feat.name = "Atmosphere Fog";
            feat.passMaterial = mat;
            feat.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            feat.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color;
            feat.fetchColorBuffer = true;
            AssetDatabase.AddObjectToAsset(feat, r);
            r.rendererFeatures.Add(feat);
            // keep the renderer's feature map in sync (used by URP to validate sub-assets)
            var mapField = typeof(ScriptableRendererData).GetField("m_RendererFeatureMap",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (mapField != null && mapField.GetValue(r) is System.Collections.Generic.List<long> map &&
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feat, out string _, out long localId))
                map.Add(localId);
            EditorUtility.SetDirty(feat);
        }
    }
}
