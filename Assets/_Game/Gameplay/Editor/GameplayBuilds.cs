using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Slime.Gameplay.Editor
{
    /// <summary>Player builds for delivery (Android APK) and for demo recordings (Windows player).</summary>
    public static class GameplayBuilds
    {
        public const string AndroidPath = "Builds/Android/UC_BallGame.apk";
        public const string WindowsPath = "Builds/Windows/UC_BallGame.exe";

        public static void BuildAndroid()
        {
            if (string.IsNullOrEmpty(PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android)))
                PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.ucballgame.slime");
            // Pixel-class phones are 64-bit only: IL2CPP + ARM64.
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            EditorUserBuildSettings.buildAppBundle = false;
            Build(BuildTarget.Android, AndroidPath);
        }

        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, WindowsPath);

        private static void Build(BuildTarget target, string path)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var group = BuildPipeline.GetBuildTargetGroup(target);
            if (EditorUserBuildSettings.activeBuildTarget != target && !EditorUserBuildSettings.SwitchActiveBuildTarget(group, target))
                throw new InvalidOperationException("Could not switch to " + target);
            AddressableAssetSettings.BuildPlayerContent(out var addressables);
            if (!string.IsNullOrEmpty(addressables.Error)) throw new InvalidOperationException("Addressables build failed: " + addressables.Error);
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes, locationPathName = path, target = target, targetGroup = group, options = BuildOptions.None,
            });
            var summary = report.summary;
            Debug.Log($"[Build] {target}: {summary.result}, {summary.totalSize / (1024f * 1024f):0.0} MB, {summary.totalTime.TotalSeconds:0} s, " +
                      $"errors {summary.totalErrors}, warnings {summary.totalWarnings} → {path}");
            if (summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Build failed: " + summary.result);
        }
    }
}
