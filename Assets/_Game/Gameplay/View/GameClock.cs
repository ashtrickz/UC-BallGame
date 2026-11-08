using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Gameplay.View
{
    /// <summary>
    /// Pausable presentation time. Every gameplay animation advances with <see cref="Delta"/>, so a pause freezes
    /// actions in place and they resume and finish only after the pause is lifted.
    /// </summary>
    public sealed class GameClock
    {
        public bool Paused { get; set; }
        public float Delta => Paused ? 0f : Time.deltaTime;

        /// <summary>Runs <paramref name="step"/> with normalized time 0..1 and completes after step(1).</summary>
        public async UniTask Animate(float duration, Action<float> step, CancellationToken token)
        {
            step(0);
            if (duration <= 0) { step(1); return; }
            var t = 0f;
            while (t < duration)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                t += Delta;
                step(Mathf.Clamp01(t / duration));
            }
        }

        public async UniTask Wait(float seconds, CancellationToken token)
        {
            var t = 0f;
            while (t < seconds)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                t += Delta;
            }
        }

        public static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
        public static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1 - (1 - t) * (1 - t); }
    }
}
