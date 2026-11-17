using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// The boot scene (splash, language, loading screen: Boot.cs) and the build's scene list: Boot first, Nainital second
    /// (user, 2026-11-17: splash, an Uttarakhandi loading screen, main menu). The loading photos (Resources/Loading) are
    /// imported for the screen: no mipmaps, at most 2048, compressed.
    /// </summary>
    public static class FrontEndSetup
    {
        public const string BootScenePath = "Assets/Nainital/Scenes/Boot.unity";

        /// <summary>The scenes a build carries, in order (the boot scene first when it exists).</summary>
        public static string[] BuildScenes() =>
            File.Exists(BootScenePath) ? new[] { BootScenePath, WorldBuilder.ScenePath } : new[] { WorldBuilder.ScenePath };

        public static void SetBuildScenes()
        {
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (var s in BuildScenes()) list.Add(new EditorBuildSettingsScene(s, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        [MenuItem("Nainital/Front End/Make Boot Scene")]
        public static void MakeBootScene()
        {
            if (EditorApplication.isPlaying) throw new System.Exception("stop play mode first");
            var active = EditorSceneManager.GetActiveScene().path;
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = 0;
            new GameObject("Boot", typeof(Boot));
            EditorSceneManager.SaveScene(scene, BootScenePath);
            SetBuildScenes();
            Debug.Log("[Nainital] boot scene saved: " + BootScenePath + "; build scenes: " + string.Join(", ", BuildScenes()));
            if (!string.IsNullOrEmpty(active) && active != BootScenePath) EditorSceneManager.OpenScene(active);
        }
    }

    /// <summary>The loading screen's photos: full colour, no mipmaps, at most 2048 px.</summary>
    public class LoadingPhotoImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Loading/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.mipmapEnabled = false;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.maxTextureSize = 2048;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.ignoreMipmapLimit = true;
        }
    }
}
