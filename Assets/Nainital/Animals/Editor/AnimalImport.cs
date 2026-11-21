using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Import settings for the rigged street animals (22 Nov 2026, free-roam #8): every FBX under Assets/Nainital/Animals
    /// (cow, dog, cat from Tools/animals/rig_animal.py) imports with LEGACY clips (AnimalManager / Animal play them with the
    /// Animation component: idle, walk, run, chew / sit / lick), the axis conversion baked (Blender's front -Y = +Z),
    /// no imported materials (AnimalManager makes a URP Lit one with Resources/Animals/&lt;name&gt;_color) and no blend shapes.
    /// Their textures: 2K on a PC, 1K on phones.
    /// </summary>
    class AnimalImport : AssetPostprocessor
    {
        const string Dir = "Assets/Nainital/Animals/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Dir)) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.Legacy;
            mi.importAnimation = true;
            mi.bakeAxisConversion = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
            mi.isReadable = false;
        }

        void OnPostprocessAnimation(GameObject root, AnimationClip clip)
        {
            if (!assetPath.StartsWith(Dir)) return;
            clip.wrapMode = WrapMode.Loop;   // every animal clip is a cycle (Animal sets it again at runtime)
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Dir)) return;
            var ti = (TextureImporter)assetImporter;
            ti.maxTextureSize = 2048;
            ti.mipmapEnabled = true;
            foreach (var platform in new[] { "Android", "iPhone" })
                ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = platform, overridden = true, maxTextureSize = 1024, format = TextureImporterFormat.Automatic,
                });
        }
    }
}
