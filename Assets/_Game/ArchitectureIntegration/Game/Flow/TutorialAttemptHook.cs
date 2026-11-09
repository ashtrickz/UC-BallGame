using System;
using Cysharp.Threading.Tasks;
using Slime.Gameplay.View;
using Slime.Localization;
using UnityEngine;

namespace Slime.Gameplay.Presentation
{
    /// <summary>Extension of the attempt start: runs after the attempt is presented, ends before the next one begins.</summary>
    public interface IAttemptStartHook
    {
        void OnAttemptEnding();

        void OnAttemptStarted(AttemptContext context);
    }

    /// <summary>Starts the tutorial when the mode policy offers it for the new attempt, and ends it cleanly on restart.</summary>
    public sealed class TutorialAttemptHook : IAttemptStartHook, IDisposable
    {
        private readonly GameplayStage _stage;
        private readonly SpotlightTargets _spotlight;
        private readonly ILocalizationService _localization;
        private readonly IPauseService _pause;
        private readonly CurrentAttempt _attempt;
        private readonly IGameplayOverrides _overrides;

        public TutorialAttemptHook(GameplayStage stage, SpotlightTargets spotlight, ILocalizationService localization,
            IPauseService pause, CurrentAttempt attempt, IGameplayOverrides overrides)
        {
            _stage = stage ? stage : throw new ArgumentNullException(nameof(stage));
            _spotlight = spotlight ?? throw new ArgumentNullException(nameof(spotlight));
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
            _pause = pause ?? throw new ArgumentNullException(nameof(pause));
            _attempt = attempt ?? throw new ArgumentNullException(nameof(attempt));
            _overrides = overrides ?? throw new ArgumentNullException(nameof(overrides));
        }

        public TutorialRunner Tutorial { get; private set; }

        public void Skip()
        {
            Tutorial?.Skip();
        }

        public void OnAttemptEnding()
        {
            Skip();
            Tutorial = null;
            if (_pause.Has(PauseReason.Tutorial))
                _pause.Set(PauseReason.Tutorial, false);
        }

        public void OnAttemptStarted(AttemptContext context)
        {
            if (!_overrides.TutorialAllowed || _stage.Tutorial == null || !context.Policy.OffersTutorial(context))
                return;

            Tutorial = new TutorialRunner(this, context, TutorialScript.FirstLevel, _stage, _spotlight, _localization, _pause, _attempt);
            Tutorial.RunAsync().Forget(Debug.LogException);
        }

        public void Dispose()
        {
            Skip();
        }
    }
}
