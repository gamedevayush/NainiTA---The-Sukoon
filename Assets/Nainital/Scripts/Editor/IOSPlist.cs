#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>Bug reports made on the iPhone (user, 2026-11-14: "maine kuch bugs daale the through my iphone") were stuck
    /// inside the app. These two Info.plist keys show the app's Documents folder (Application.persistentDataPath, where
    /// BugReports lives) in the Files app: On My iPhone > Nainital Open World > BugReports.</summary>
    public static class IOSPlist
    {
        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetBoolean("UIFileSharingEnabled", true);
            plist.root.SetBoolean("LSSupportsOpeningDocumentsInPlace", true);
            plist.WriteToFile(plistPath);
            Debug.Log("[Nainital] Info.plist: Files app access to Documents (BugReports) on");
        }
    }
}
#endif
