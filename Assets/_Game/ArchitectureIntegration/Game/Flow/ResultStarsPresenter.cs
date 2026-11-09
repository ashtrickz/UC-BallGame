using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Audio;
using Game.Gameplay.View;
using UnityEngine;

namespace Game.Integration.Game
{
    /// <summary>Owns celebration cancellation and audio; views only animate the already earned result.</summary>
    public sealed class ResultStarsPresenter : IDisposable
    {
        private const float PitchStep = 0.12f;

        private readonly ResultView _view;
        private readonly GameplayStage _stage;
        private readonly IAudioService _audio;
        private readonly List<AudioPlaybackHandle> _sounds = new List<AudioPlaybackHandle>();
        private CancellationTokenSource _animation;

        public ResultStarsPresenter(ResultView view, GameplayStage stage, IAudioService audio)
        {
            _view = view;
            _stage = stage;
            _audio = audio;
            _view.Hidden += Reset;
            if (_view.StarFlight != null)
            {
                _view.StarFlight.Launched += OnLaunched;
                _view.StarFlight.Arrived += OnArrived;
            }
        }

        public void Show(AttemptContext context, bool animate)
        {
            Reset();
            if (_view.Stars == null)
                return;

            if (!animate || _view.StarFlight == null || _stage.FinishPool == null)
            {
                _view.Stars.Show(context.EarnedStars);
                return;
            }

            _animation = CancellationTokenSource.CreateLinkedTokenSource(context.Token);
            PlayAsync(context, _animation.Token).Forget(Debug.LogException);
        }

        public void Reset()
        {
            var animation = _animation;
            _animation = null;
            animation?.Cancel();
            animation?.Dispose();
            if (_view.StarFlight != null)
                _view.StarFlight.Stop();

            foreach (var sound in _sounds)
                sound?.Stop();

            _sounds.Clear();
        }

        public void Dispose()
        {
            Reset();
            _view.Hidden -= Reset;
            if (_view.StarFlight != null)
            {
                _view.StarFlight.Launched -= OnLaunched;
                _view.StarFlight.Arrived -= OnArrived;
            }
        }

        private async UniTask PlayAsync(AttemptContext context, CancellationToken token)
        {
            try
            {
                await _view.StarFlight.PlayAsync(context.EarnedStars, _stage.FinishPool.position,
                    _stage.CameraRig.Camera, _view.Stars, _stage.Clock, token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void OnLaunched(int index)
        {
            PlaySound(GameSound.StarLaunch, index);
        }

        private void OnArrived(int index)
        {
            PlaySound(GameSound.StarCollect, index);
        }

        private void PlaySound(GameSound sound, int index)
        {
            var handle = _audio.PlaySound(sound);
            handle?.SetPitch(1 + index * PitchStep);
            _sounds.Add(handle);
        }
    }
}
