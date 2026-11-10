#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Slime.Foundation;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Slime.Editor
{
    /// <summary>Enables editor diagnostics and stages player defines before the player compilation starts.</summary>
    [InitializeOnLoad]
    public static class GameDebugDefines
    {
        private const string PreparingKey = "UC_BallGame.GameDebug.PreparingPlayer";
        private const string BuildingKey = "UC_BallGame.GameDebug.BuildStarted";
        private const string QueuedBuildKey = "UC_BallGame.GameDebug.QueuedBuild";
        public const string BuildStatusPath = "Library/SlimeBuildStatus.json";
        private const char Separator = ';';
        private const string AndroidMenu = "Tools/Ball Game/Build/Android APK";
        private const string WindowsMenu = "Tools/Ball Game/Build/Windows Demo Player";
        private const string AndroidOutput = "Builds/Android/UC_BallGame.apk";
        private const string WindowsOutput = "Builds/Windows/UC_BallGame.exe";
        private const string AndroidIdentifier = "com.ucballgame.slime";
        private const string TemplateIdentifier = "com.UnityTechnologies.com.unity.template.urpblank";
        private const string DevelopmentMenu = "Tools/Ball Game/Build/Prepare Development Defines";
        private const string ReleaseMenu = "Tools/Ball Game/Build/Prepare Release Defines";
        private const string RestoreMenu = "Tools/Ball Game/Build/Restore Editor Defines";
#if GAME_DEBUG
        private const bool DiagnosticsCompiled = true;
#else
        private const bool DiagnosticsCompiled = false;
#endif

        /// <summary>Build options retained across the script reload required by define changes.</summary>
        [Serializable]
        private sealed class PendingBuild
        {
            public string[] scenes;
            public string output;
            public string manifest;
            public BuildTarget target;
            public BuildTargetGroup group;
            public int subtarget;
            public BuildOptions options;
            public string[] defines;

            public BuildPlayerOptions PlayerOptions => new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                assetBundleManifestPath = manifest,
                target = target,
                targetGroup = group,
                subtarget = subtarget,
                options = options,
                extraScriptingDefines = defines,
            };
        }

        /// <summary>Progress and the final result of the queued player build.</summary>
        [Serializable]
        private sealed class BuildStatus
        {
            public string state;
            public string output;
            public string updatedUtc;
            public string error;
            public int errors;
            public int warnings;
            public long bytes;
            public long artifactBytes;
            public double seconds;
        }

        static GameDebugDefines()
        {
            BuildPlayerWindow.RegisterBuildPlayerHandler(RequestBuild);
            EditorApplication.delayCall += EnableEditor;
            EditorApplication.update += ResumeBuild;
            EditorApplication.update += RestoreAfterBuild;
        }

        [MenuItem(AndroidMenu)]
        public static void BuildAndroid()
        {
            var identifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if (string.IsNullOrEmpty(identifier) || identifier == TemplateIdentifier)
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidIdentifier);

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            EditorUserBuildSettings.buildAppBundle = false;
            RequestBuild(new BuildPlayerOptions
            {
                scenes = EnabledScenes(),
                locationPathName = AndroidOutput,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });
        }

        [MenuItem(WindowsMenu)]
        public static void BuildWindows()
        {
            RequestBuild(new BuildPlayerOptions
            {
                scenes = EnabledScenes(),
                locationPathName = WindowsOutput,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.Development,
            });
        }

        public static void RequestBuild(BuildPlayerOptions options)
        {
            if (EditorApplication.isPlaying || BuildPipeline.isBuildingPlayer || SessionState.GetString(QueuedBuildKey, string.Empty).Length > 0)
                throw new InvalidOperationException("Stop play mode or finish the current build before starting another one.");

            var development = (options.options & BuildOptions.Development) != 0;
            var profile = BuildProfile.GetActiveBuildProfile();
            SessionState.SetBool(PreparingKey, true);
            SessionState.EraseBool(BuildingKey);
            if (profile != null)
            {
                profile.scriptingDefines = Configure(string.Join(Separator.ToString(), profile.scriptingDefines ?? Array.Empty<string>()), development)
                    .Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(profile);
            }

            var request = new PendingBuild
            {
                scenes = options.scenes,
                output = options.locationPathName,
                manifest = options.assetBundleManifestPath,
                target = options.target,
                group = options.targetGroup,
                subtarget = options.subtarget,
                options = options.options,
                defines = (options.extraScriptingDefines ?? Array.Empty<string>())
                    .SelectMany(symbol => Configure(symbol, false).Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries)).ToArray(),
            };
            SessionState.SetString(QueuedBuildKey, JsonUtility.ToJson(request));
            WriteStatus(request, "Queued");
            PreparePlayerBuild(options.target, development);
        }

        private static string[] EnabledScenes()
        {
            return EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        }

        private static void ResumeBuild()
        {
            var json = SessionState.GetString(QueuedBuildKey, string.Empty);
            if (json.Length == 0 || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
                return;

            var request = JsonUtility.FromJson<PendingBuild>(json);
            BuildReport report = null;
            try
            {
                if (EditorUtility.scriptCompilationFailed)
                    throw new InvalidOperationException("Resolve script compilation errors before building.");

                if (EditorUserBuildSettings.activeBuildTarget != request.target)
                {
                    if (!EditorUserBuildSettings.SwitchActiveBuildTarget(request.group, request.target))
                        throw new InvalidOperationException("Could not switch to " + request.target);

                    return;
                }

                var development = (request.options & BuildOptions.Development) != 0;
                var namedTarget = NamedBuildTarget.FromBuildTargetGroup(request.group);
                if (Contains(PlayerSettings.GetScriptingDefineSymbols(namedTarget)) != development)
                {
                    PreparePlayerBuild(request.target, development);
                    return;
                }

                if (DiagnosticsCompiled != development)
                    return;

                SessionState.EraseString(QueuedBuildKey);
                if (!EditorSceneManager.SaveOpenScenes())
                    throw new InvalidOperationException("Could not save the open scenes before building.");

                Directory.CreateDirectory(Path.GetDirectoryName(request.output));
                GameSpriteAtlasPrebuild.Prepare();
                WriteStatus(request, "Addressables");
                AddressableAssetSettings.BuildPlayerContent(out var addressables);
                if (!string.IsNullOrEmpty(addressables.Error))
                    throw new InvalidOperationException("Addressables build failed: " + addressables.Error);

                WriteStatus(request, "Player");
                report = BuildPipeline.BuildPlayer(request.PlayerOptions);
                WriteStatus(request, report.summary.result.ToString(), report);
                File.Copy(BuildStatusPath, Path.ChangeExtension(request.output, ".build.json"), true);
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Player build failed: " + report.summary.result);

                RestoreEditor();
            }
            catch (Exception exception)
            {
                WriteStatus(request, "Failed", report, exception);
                RestoreEditor();
                Debug.LogException(exception);
            }
        }

        private static void WriteStatus(PendingBuild request, string state, BuildReport report = null, Exception error = null)
        {
            var status = new BuildStatus
            {
                state = state,
                output = request.output,
                updatedUtc = DateTime.UtcNow.ToString("O"),
                error = error?.ToString(),
                errors = report != null ? report.summary.totalErrors : 0,
                warnings = report != null ? report.summary.totalWarnings : 0,
                bytes = report != null ? (long)report.summary.totalSize : 0,
                artifactBytes = report != null && File.Exists(request.output) ? new FileInfo(request.output).Length : 0,
                seconds = report != null ? report.summary.totalTime.TotalSeconds : 0,
            };
            File.WriteAllText(BuildStatusPath, JsonUtility.ToJson(status, true));
        }

        public static string Configure(string symbols, bool enabled)
        {
            var result = new List<string>();
            foreach (var token in (symbols ?? string.Empty).Split(Separator))
            {
                var symbol = token.Trim();
                if (symbol.Length > 0 && !string.Equals(symbol, GameLog.DebugSymbol, StringComparison.Ordinal))
                    result.Add(symbol);
            }

            if (enabled)
                result.Add(GameLog.DebugSymbol);
            return string.Join(Separator.ToString(), result);
        }

        public static bool Contains(string symbols)
        {
            foreach (var symbol in (symbols ?? string.Empty).Split(Separator))
                if (string.Equals(symbol.Trim(), GameLog.DebugSymbol, StringComparison.Ordinal))
                    return true;

            return false;
        }

        public static bool MatchesPlayer(bool development, string symbols, IEnumerable<string> extraSymbols)
        {
            var enabled = Contains(symbols);
            if (extraSymbols != null)
                foreach (var symbol in extraSymbols)
                    enabled |= Contains(symbol);

            return development == enabled;
        }

        public static void PreparePlayerBuild(BuildTarget target, bool development)
        {
            // This is a separate build step: Unity must finish reloading scripts before BuildPlayer is called.
            SessionState.SetBool(PreparingKey, true);
            var namedTarget = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target));
            SetForTarget(namedTarget, development);
        }

        [MenuItem(DevelopmentMenu)]
        public static void PrepareDevelopment() => PreparePlayerBuild(EditorUserBuildSettings.activeBuildTarget, true);

        [MenuItem(ReleaseMenu)]
        public static void PrepareRelease() => PreparePlayerBuild(EditorUserBuildSettings.activeBuildTarget, false);

        [MenuItem(RestoreMenu)]
        public static void RestoreEditor()
        {
            SessionState.EraseString(QueuedBuildKey);
            SessionState.EraseBool(PreparingKey);
            SessionState.EraseBool(BuildingKey);
            EnableEditor();
        }

        public static void MarkBuildStarted() => SessionState.SetBool(BuildingKey, true);

        private static void EnableEditor()
        {
            if (BuildPipeline.isBuildingPlayer || SessionState.GetBool(PreparingKey, false))
                return;

            if (EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += EnableEditor;
                return;
            }

            var namedTarget = NamedBuildTarget.FromBuildTargetGroup(
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));
            SetForTarget(namedTarget, true);
        }

        private static void SetForTarget(NamedBuildTarget target, bool enabled)
        {
            var before = PlayerSettings.GetScriptingDefineSymbols(target);
            var after = Configure(before, enabled);
            if (!string.Equals(before, after, StringComparison.Ordinal))
            {
                PlayerSettings.SetScriptingDefineSymbols(target, after);
                AssetDatabase.SaveAssets();
            }
        }

        private static void RestoreAfterBuild()
        {
            if (SessionState.GetBool(BuildingKey, false) && !BuildPipeline.isBuildingPlayer && !EditorApplication.isCompiling)
                RestoreEditor();
        }
    }
}
#endif
