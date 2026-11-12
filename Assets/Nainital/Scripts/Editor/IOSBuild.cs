using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>The iPhone build (user, 2026-11-12: "kuch bhi karke mere iPhone tak install karvao"; no Mac, no paid Apple
    /// account). This Windows editor writes the Xcode project; GitHub Actions compiles it on a free macOS runner into an
    /// unsigned .ipa (.github/workflows/ios-ipa.yml); Sideloadly on the PC signs it with the user's own free Apple ID and
    /// installs it (it then runs for 7 days at a time). Same two steps as Android: SwitchToIOS, then Build.</summary>
    public static class IOSBuild
    {
        public static readonly string OutDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/iOS"));

        [MenuItem("Nainital/8. iOS: switch platform", priority = 8)]
        public static void SwitchToIOS()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS) { Debug.Log("[Nainital] already on iOS"); return; }
            bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
            Debug.Log("[Nainital] switch to iOS: " + (ok ? "done" : "FAILED"));
            if (!ok) throw new Exception("could not switch the build target to iOS (is the iOS module installed?)");
        }

        [MenuItem("Nainital/9. iOS: build Xcode project", priority = 9)]
        public static void Build()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                throw new Exception("switch to iOS first (IOSBuild.SwitchToIOS)");
            var ios = NamedBuildTarget.iOS;
            PlayerSettings.SetApplicationIdentifier(ios, "com.nainital.openworld");
            PlayerSettings.SetScriptingBackend(ios, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;   // Sideloadly signs it with the user's Apple ID
            PlayerSettings.iOS.appleDeveloperTeamID = "";
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.hideHomeButton = true;
            PlayerSettings.iOS.buildNumber = "2";
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1.0") PlayerSettings.bundleVersion = "0.2.0";
            EditorUserBuildSettings.development = false;

            if (Directory.Exists(OutDir)) Directory.Delete(OutDir, true);   // a clean Xcode project each time
            Directory.CreateDirectory(OutDir);
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { WorldBuilder.ScenePath },
                locationPathName = OutDir,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.None,
            };
            var t0 = DateTime.Now;
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[Nainital] iOS build {s.result}: {OutDir}, {s.totalSize / 1048576} MB, {s.totalErrors} errors, {(DateTime.Now - t0).TotalMinutes:F1} min");
            if (s.result != BuildResult.Succeeded)
            {
                foreach (var step in report.steps)
                    foreach (var m in step.messages)
                        if (m.type == LogType.Error || m.type == LogType.Exception) Debug.Log("[Nainital] build error: " + m.content);
                throw new Exception("iOS build " + s.result);
            }
        }
    }
}
