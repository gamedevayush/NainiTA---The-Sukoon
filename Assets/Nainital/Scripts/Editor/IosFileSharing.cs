#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace Nainital.EditorTools
{
    /// <summary>
    /// The radio's own-music folder on an iPhone (Runtime/Audio/Radio.cs, owner 21 Nov 2026: "iPhone - a folder in the
    /// Files app"): the app's Documents show in the Files app (On My iPhone > NainiTA), so the player can drop songs into
    /// its Music folder. Sets UIFileSharingEnabled + LSSupportsOpeningDocumentsInPlace in the built Info.plist.
    /// </summary>
    public static class IosFileSharing
    {
        [PostProcessBuild(100)]
        public static void OnBuilt(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetBoolean("UIFileSharingEnabled", true);
            plist.root.SetBoolean("LSSupportsOpeningDocumentsInPlace", true);
            plist.WriteToFile(plistPath);
        }
    }
}
#endif
