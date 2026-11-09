using System.Threading;
using System;
using Cysharp.Threading.Tasks;
using Slime.Foundation;
using UnityEngine;

namespace Slime.Gameplay.View
{
    /// <summary>Pausable presentation time of the gameplay scene, advanced once per frame by the scene frame.</summary>
    public interface IGameClock
    {
        bool Paused { get; set; }

        float Delta { get; }

        UniTask NextFrame(CancellationToken token);

        UniTask Animate(float duration, Action<float> step, CancellationToken token);

        UniTask Wait(float seconds, CancellationToken token);
    }

    /// <summary>
    /// Pausable presentation time. Every gameplay animation advances with <see cref="Delta"/>, so a pause freezes
    /// actions in place and they resume and finish only after the pause is lifted. Awaiters of a frame resume
    /// inside <see cref="Tick"/>, so the frame order is set by the caller, not by the player loop.
    /// </summary>
    public sealed class GameClock : IGameClock
    {
        private const float SmoothStepA = 3f;
        private const float SmoothStepB = 2f;

        private UniTaskCompletionSource _frame = new UniTaskCompletionSource();
        private float _frameDelta;
        private bool _stopped;

        public bool Paused { get; set; }

        public float Delta => Paused ? 0f : _frameDelta;

        public static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);

            return t * t * (SmoothStepA - SmoothStepB * t);
        }

        public static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);

            return 1 - (1 - t) * (1 - t);
        }

        public void Tick(float deltaTime)
        {
            if (_stopped)
                return;

            _frameDelta = deltaTime;
            var frame = _frame;
            _frame = new UniTaskCompletionSource();
            frame.TrySetResult();
        }

        public void Stop()
        {
            _stopped = true;
            _frame.TrySetCanceled();
        }

        public async UniTask NextFrame(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            await _frame.Task;
            token.ThrowIfCancellationRequested();
        }

        public async UniTask Animate(float duration, Action<float> step, CancellationToken token)
        {
            step(0);
            if (duration <= 0)
            {
                step(1);
                return;
            }

            var t = 0f;
            while (t < duration)
            {
                await NextFrame(token);
                t += Delta;
                step(Mathf.Clamp01(t / duration));
            }
        }

        public async UniTask Wait(float seconds, CancellationToken token)
        {
            var t = 0f;
            while (t < seconds)
            {
                await NextFrame(token);
                t += Delta;
            }
        }
    }
}
