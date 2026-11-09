using System.Diagnostics;
using UnityEngine;

namespace Slime.Foundation
{
    /// <summary>Compiles diagnostic calls and their arguments out of players without the debug symbol.</summary>
    public static class GameLog
    {
        public const string DebugSymbol = "GAME_DEBUG";

        [Conditional(DebugSymbol)]
        public static void Info(object message) => UnityEngine.Debug.Log(message);

        [Conditional(DebugSymbol)]
        public static void Warning(object message) => UnityEngine.Debug.LogWarning(message);
    }
}
