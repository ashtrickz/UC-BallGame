using System;
using Slime.Gameplay.Core.Model;
using Slime.Gameplay.View;
using Slime.Infrastructure.Audio;

namespace Slime.Gameplay.Presentation
{
    [Flags]
    public enum PauseReason
    {
        None = 0,
        Menu = 1,
        Settings = 2,
        Background = 4,
        Tutorial = 8,
    }

    /// <summary>Pause reasons of the gameplay scene; the scene is paused while any reason is set.</summary>
    public interface IPauseService
    {
        PauseReason Reason { get; }

        bool IsPaused { get; }

        event Action<PauseReason, PauseReason> Changed;

        event Action<AttemptModel> ChargeInterrupted;

        bool Has(PauseReason reason);

        void Set(PauseReason reason, bool paused);
    }

    /// <summary>
    /// Applies the pause to the presentation clock, sounds, haptics and the attempt model. Any pause or resume
    /// requires a fresh press, so an old touch never becomes a shot.
    /// </summary>
    public sealed class PauseService : IPauseService, IDisposable
    {
        private readonly GameplayStage _stage;
        private readonly CurrentAttempt _attempt;
        private readonly GameplayAudioPresenter _sounds;
        private readonly GameplayHapticsPresenter _haptics;

        private bool _disposed;

        public PauseService(GameplayStage stage, CurrentAttempt attempt, GameplayAudioPresenter sounds, GameplayHapticsPresenter haptics)
        {
            _stage = stage ? stage : throw new ArgumentNullException(nameof(stage));
            _attempt = attempt ?? throw new ArgumentNullException(nameof(attempt));
            _sounds = sounds ?? throw new ArgumentNullException(nameof(sounds));
            _haptics = haptics ?? throw new ArgumentNullException(nameof(haptics));
            _stage.BackgroundChanged += OnBackgroundChanged;
        }

        public event Action<PauseReason, PauseReason> Changed;

        public event Action<AttemptModel> ChargeInterrupted;

        public PauseReason Reason { get; private set; }

        public bool IsPaused => Reason != PauseReason.None;

        public bool Has(PauseReason reason)
        {
            return (Reason & reason) != 0;
        }

        public void Initialize(PauseReason reason)
        {
            Reason = reason;
            ApplyToFeedback();
        }

        public void Set(PauseReason reason, bool paused)
        {
            if (_disposed)
                return;

            var previous = Reason;
            Reason = paused
                ? previous | reason
                : previous & ~reason;
            var was = previous != PauseReason.None;
            var settingsWereOpen = (previous & PauseReason.Settings) != 0;

            ApplyToFeedback();
            Changed?.Invoke(previous, Reason);
            if (settingsWereOpen != Has(PauseReason.Settings))
                _sounds.PanelChanged(Has(PauseReason.Settings));

            PauseModel();
            if (was != IsPaused)
                _stage.Input.RequireFreshPress();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _stage.BackgroundChanged -= OnBackgroundChanged;
        }

        private void ApplyToFeedback()
        {
            _stage.Clock.Paused = IsPaused;
            _sounds.SetPaused(IsPaused);
            _haptics.SetPaused(IsPaused);
        }

        private void PauseModel()
        {
            var context = _attempt.Context;
            if (context == null)
                return;

            var model = context.Model;
            var wasCharging = model.Phase == AttemptPhase.Charging;
            model.SetPaused(IsPaused);
            if (wasCharging && model.Phase == AttemptPhase.Waiting)
                ChargeInterrupted?.Invoke(model);
        }

        private void OnBackgroundChanged(bool background)
        {
            Set(PauseReason.Background, background);
        }
    }
}
