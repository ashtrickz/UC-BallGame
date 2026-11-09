#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.Foundation;
using UnityEditor;
using UnityEditor.Build;

namespace Game.Editor
{
    /// <summary>Enables editor diagnostics and stages player defines before the player compilation starts.</summary>
    [InitializeOnLoad]
    public static class GameDebugDefines
    {
        private const string PreparingKey = "UC_BallGame.GameDebug.PreparingPlayer";
        private const string BuildingKey = "UC_BallGame.GameDebug.BuildStarted";
        private const char Separator = ';';
        private const string DevelopmentMenu = "Tools/Ball Game/Build/Prepare Development Defines";
        private const string ReleaseMenu = "Tools/Ball Game/Build/Prepare Release Defines";
        private const string RestoreMenu = "Tools/Ball Game/Build/Restore Editor Defines";

        static GameDebugDefines()
        {
            EditorApplication.delayCall += EnableEditor;
            EditorApplication.update += RestoreAfterBuild;
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
