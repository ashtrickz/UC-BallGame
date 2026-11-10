#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;

namespace Slime.Editor
{
    /// <summary>Rejects mismatched player diagnostics before compilation and restores the editor afterwards.</summary>
    public sealed class GameDebugBuildProcessor : BuildPlayerProcessor, IPostprocessBuildWithReport
    {
        private const int CallbackOrder = int.MinValue;
        private const string PreparationMessage =
            "GAME_DEBUG does not match the player build type. Use GameDebugDefines.RequestBuild(options) or "
            + "Tools/Ball Game/Build to prepare defines and wait for compilation automatically. "
            + "Direct BuildPipeline.BuildPlayer calls require PreparePlayerBuild(target, development) and a completed script reload.";

        public override int callbackOrder => CallbackOrder;

        public override void PrepareForBuild(BuildPlayerContext context)
        {
            GameDebugDefines.MarkBuildStarted();
            var options = context.BuildPlayerOptions;
            var namedTarget = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(options.target));
            var symbols = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
            var development = (options.options & BuildOptions.Development) != 0;
            var profile = BuildProfile.GetActiveBuildProfile();
            var profileSymbols = profile != null ? profile.scriptingDefines : System.Array.Empty<string>();
            var extra = (options.extraScriptingDefines ?? System.Array.Empty<string>()).Concat(profileSymbols);
            if (!GameDebugDefines.MatchesPlayer(development, symbols, extra))
                throw new BuildFailedException(PreparationMessage);
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            EditorApplication.delayCall += GameDebugDefines.RestoreEditor;
        }
    }
}
#endif
