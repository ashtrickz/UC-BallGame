using DG.Tweening;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// DOTween setup before the first scene: a fixed pool capacity, no recycling (views keep and replay their own
    /// sequences), safety checks and verbose logs only in debug builds.
    /// </summary>
    public static class TweenSettings
    {
        private const int TweenersCapacity = 64;
        private const int SequencesCapacity = 32;
        private const bool RecycleByDefault = false;
#if GAME_DEBUG
        private const bool SafeMode = true;
        private const LogBehaviour Logs = LogBehaviour.Verbose;
#else
        private const bool SafeMode = false;
        private const LogBehaviour Logs = LogBehaviour.ErrorsOnly;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            DOTween.Init(RecycleByDefault, SafeMode, Logs).SetCapacity(TweenersCapacity, SequencesCapacity);
        }
    }
}
