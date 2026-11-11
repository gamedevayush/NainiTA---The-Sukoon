using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>The phone build (user, 2026-11-11: "android pe chalna hai ... phir APK dedo"). Uses the Android SDK, NDK
    /// and JDK that came with this Unity's Android module. Two steps, because switching the platform reimports every
    /// texture and then reloads the scripts: SwitchToAndroid, then Build (both reachable through the editor bridge:
    /// "call AndroidBuild.SwitchToAndroid", "call AndroidBuild.Build"). The APK is signed with the debug key, for
    /// installing by hand; it is not a store build.</summary>
    public static class AndroidBuild
    {
        public static readonly string OutDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Android"));
        public const string ApkName = "NainitalOpenWorld.apk";

        [MenuItem("Nainital/5. Android: switch platform", priority = 5)]
        public static void SwitchToAndroid()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android) { Debug.Log("[Nainital] already on Android"); return; }
            bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            Debug.Log("[Nainital] switch to Android: " + (ok ? "done" : "FAILED"));
            if (!ok) throw new Exception("could not switch the build target to Android");
        }

        static void UseBundledTools()
        {
            // the SDK, NDK and JDK installed with this editor's Android module
            string root = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer");
            var t = Type.GetType("UnityEditor.Android.AndroidExternalToolsSettings, UnityEditor.Android.Extensions");
            if (t == null) { Debug.LogWarning("[Nainital] Android module tools settings not found; Unity's own choice is used"); return; }
            void Set(string prop, string sub)
            {
                string path = Path.GetFullPath(Path.Combine(root, sub));
                var pi = t.GetProperty(prop, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (pi != null && Directory.Exists(path)) { pi.SetValue(null, path); Debug.Log($"[Nainital] {prop} = {path}"); }
            }
            Set("sdkRootPath", "SDK");
            Set("ndkRootPath", "NDK");
            Set("jdkRootPath", "OpenJDK");
        }

        [MenuItem("Nainital/6. Android: build APK", priority = 6)]
        public static void Build()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new Exception("switch to Android first (AndroidBuild.SwitchToAndroid)");
            UseBundledTools();
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, "com.nainital.openworld");
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel28;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.Android.renderOutsideSafeArea = false;   // keep the buttons clear of the camera hole
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1.0") PlayerSettings.bundleVersion = "0.2.0";
            PlayerSettings.Android.bundleVersionCode = Mathf.Max(PlayerSettings.Android.bundleVersionCode, 2);
            EditorUserBuildSettings.buildAppBundle = false;      // an APK to install by hand
            EditorUserBuildSettings.development = false;

            Directory.CreateDirectory(OutDir);
            string apk = Path.Combine(OutDir, ApkName);
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { WorldBuilder.ScenePath },
                locationPathName = apk,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };
            var t0 = DateTime.Now;
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[Nainital] APK build {s.result}: {apk}, {s.totalSize / 1048576} MB, {s.totalErrors} errors, {(DateTime.Now - t0).TotalMinutes:F1} min");
            if (s.result != BuildResult.Succeeded)
            {
                foreach (var step in report.steps)
                    foreach (var m in step.messages)
                        if (m.type == LogType.Error || m.type == LogType.Exception) Debug.Log("[Nainital] build error: " + m.content);
                throw new Exception("APK build " + s.result);
            }
        }
    }
}
