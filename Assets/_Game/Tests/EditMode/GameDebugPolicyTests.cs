using Slime.Foundation;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Slime.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.TestTools;

namespace Slime.Tests.EditMode
{
    /// <summary>Protects conditional diagnostics and development versus release build preparation.</summary>
    public sealed class GameDebugPolicyTests
    {
#if GAME_DEBUG
        private const bool DebugCompiledForEditor = true;
#else
        private const bool DebugCompiledForEditor = false;
#endif

        [Test]
        public void Define_updates_preserve_other_symbols_and_never_duplicate_the_debug_symbol()
        {
            Assert.AreEqual("FIRST;LAST;GAME_DEBUG", GameDebugDefines.Configure("FIRST;GAME_DEBUG;LAST; GAME_DEBUG ", true));
            Assert.AreEqual("FIRST;LAST", GameDebugDefines.Configure("FIRST;GAME_DEBUG;LAST", false));
            Assert.IsFalse(GameDebugDefines.Contains("MY_GAME_DEBUG;GAME_DEBUG_EXTRA"));
        }

        [TestCase(false, "FIRST", "", true)]
        [TestCase(false, "FIRST;GAME_DEBUG", "", false)]
        [TestCase(false, "FIRST", "GAME_DEBUG", false)]
        [TestCase(true, "FIRST;GAME_DEBUG", "", true)]
        [TestCase(true, "FIRST", "GAME_DEBUG", true)]
        [TestCase(true, "FIRST", "", false)]
        public void Player_guard_matches_both_target_and_additional_defines(bool development, string symbols, string extra, bool expected)
        {
            Assert.AreEqual(expected, GameDebugDefines.MatchesPlayer(development, symbols, new[] { extra }));
        }

        [Test]
        public void Editor_compilation_has_debug_and_log_calls_work_after_the_installer_reload()
        {
            var target = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));
            Assert.IsTrue(GameDebugDefines.Contains(PlayerSettings.GetScriptingDefineSymbols(target)));
            Assert.IsTrue(DebugCompiledForEditor);
            LogAssert.Expect(LogType.Log, "Framework debug log");
            GameLog.Info("Framework debug log");
        }

        [TestCase(nameof(GameLog.Info))]
        [TestCase(nameof(GameLog.Warning))]
        public void Diagnostic_methods_declare_the_shared_conditional_symbol(string method)
        {
            var attributes = typeof(GameLog).GetMethod(method).GetCustomAttributes(typeof(ConditionalAttribute), false)
                .Cast<ConditionalAttribute>().ToArray();
            Assert.AreEqual(1, attributes.Length);
            Assert.AreEqual(GameLog.DebugSymbol, attributes[0].ConditionString);
        }
    }
}
