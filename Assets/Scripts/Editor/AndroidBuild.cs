using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BlockPuzzle.Editor
{
    /// <summary>
    /// One-click Android APK builds. Configures the Android player settings, makes sure the
    /// Main scene exists, and writes the APK to Builds/Android/.
    ///
    /// Command line (CI):
    ///   Unity -batchmode -quit -projectPath . -buildTarget Android
    ///         -executeMethod BlockPuzzle.Editor.AndroidBuild.BuildDevelopmentApk -logFile -
    /// </summary>
    public static class AndroidBuild
    {
        const string PackageName = "com.blockpuzzle.aimodel";
        const string ProductName = "Block Puzzle";
        const string OutputDir = "Builds/Android";

        [MenuItem("Block Puzzle/Build Android APK (Development)")]
        public static void BuildDevelopmentApk() => Build(development: true);

        [MenuItem("Block Puzzle/Build Android APK (Release)")]
        public static void BuildReleaseApk() => Build(development: false);

        static void Build(bool development)
        {
            if (!File.Exists(SceneSetup.ScenePath)) SceneSetup.CreateMainScene();
            if (!File.Exists(SceneSetup.ScenePath)) { Fail("Main scene could not be created."); return; }

            ConfigurePlayerSettings();

            string file = $"BlockPuzzle-{(development ? "dev" : "release")}.apk";
            Directory.CreateDirectory(OutputDir);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { SceneSetup.ScenePath },
                locationPathName = Path.Combine(OutputDir, file),
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                // Development builds show the monetisation debug readout in the corner.
                options = development ? BuildOptions.Development : BuildOptions.None,
            };

            EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"Block Puzzle: APK built at {Path.GetFullPath(options.locationPathName)} " +
                          $"({summary.totalSize / (1024f * 1024f):0.0} MB, {summary.totalTime.TotalSeconds:0}s)");
                if (!Application.isBatchMode) EditorUtility.RevealInFinder(options.locationPathName);
            }
            else Fail($"Android build {summary.result} with {summary.totalErrors} error(s). See the Console.");
        }

        static void ConfigurePlayerSettings()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(android, PackageName);
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1")
                PlayerSettings.bundleVersion = "0.1.0";

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            AssetDatabase.SaveAssets();
        }

        static void Fail(string message)
        {
            Debug.LogError("Block Puzzle: " + message);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else EditorUtility.DisplayDialog("Android build failed", message, "OK");
        }
    }
}
