using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>Import settings for the recorded sounds in Resources/Audio (Tools/fetch_sounds.py): beds stay compressed
    /// in memory and load in the background; short effects are mono and decompressed once, so playing them costs nothing.</summary>
    class GameAudioImport : AssetPostprocessor
    {
        public const string Dir = "Assets/Resources/Audio";

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Dir + "/")) return;
            var ai = (AudioImporter)assetImporter;
            string f = Path.GetFileNameWithoutExtension(assetPath);
            bool bed = f.StartsWith("amb_") || f.StartsWith("loop_");
            ai.forceToMono = !f.StartsWith("amb_");
            ai.loadInBackground = bed;
            var s = ai.defaultSampleSettings;
            s.loadType = bed ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = bed ? 0.45f : 0.6f;
            s.preloadAudioData = true;
            ai.defaultSampleSettings = s;
        }

        /// <summary>Re-import the sounds and vehicles with these settings (they were copied in before the scripts compiled).</summary>
        public static void Reimport()
        {
            foreach (var d in new[] { Dir, VehicleKit.Dir })
                if (AssetDatabase.IsValidFolder(d)) AssetDatabase.ImportAsset(d, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            Debug.Log("[Nainital] re-imported sounds and vehicles");
        }
    }
}
