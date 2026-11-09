#if GAME_DEBUG
using System;
using Slime.Gameplay.Core.Levels;
using Slime.Gameplay.View;
using Slime.Gameplay.Presentation;

namespace Slime.Diagnostics
{
    /// <summary>Narrow simulation surface for the autoplay, demo runs, the editor debug menu and tests.</summary>
    public interface IGameplayDebug
    {
        AttemptContext Current { get; }

        bool IsBusy { get; }

        PauseReason Pause { get; }

        PointerChargeInput Input { get; }

        GameClock Clock { get; }

        TutorialRunner Tutorial { get; }

        bool TutorialEnabled { get; set; }

        void SkipTutorial();

        void SetScriptedHold(double? seconds);

        void PlaySeed(int seed);
    }

    /// <summary>Settable overrides for debug runs; a new attempt clears the scripted hold.</summary>
    public sealed class DebugGameplayOverrides : IGameplayOverrides, IAttemptStartHook
    {
        public double? HoldLimit { get; set; }

        public bool TutorialAllowed { get; set; } = true;

        public void OnAttemptEnding()
        {
            HoldLimit = null;
        }

        public void OnAttemptStarted(AttemptContext context)
        {
        }
    }

    /// <summary>Debug access on top of the gameplay services; production classes do not know about it.</summary>
    public sealed class GameplayDebug : IGameplayDebug
    {
        private readonly GameplayFlow _flow;
        private readonly CurrentAttempt _attempt;
        private readonly IPauseService _pause;
        private readonly DebugGameplayOverrides _overrides;
        private readonly TutorialAttemptHook _tutorial;
        private readonly GameplayStage _stage;

        public GameplayDebug(GameplayFlow flow, CurrentAttempt attempt, IPauseService pause, DebugGameplayOverrides overrides,
            TutorialAttemptHook tutorial, GameplayStage stage)
        {
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _attempt = attempt ?? throw new ArgumentNullException(nameof(attempt));
            _pause = pause ?? throw new ArgumentNullException(nameof(pause));
            _overrides = overrides ?? throw new ArgumentNullException(nameof(overrides));
            _tutorial = tutorial ?? throw new ArgumentNullException(nameof(tutorial));
            _stage = stage ? stage : throw new ArgumentNullException(nameof(stage));
        }

        public AttemptContext Current => _attempt.Context;

        public bool IsBusy => _attempt.IsBusy;

        public PauseReason Pause => _pause.Reason;

        public PointerChargeInput Input => _stage.Input;

        public GameClock Clock => _stage.Clock;

        public TutorialRunner Tutorial => _tutorial.Tutorial;

        public bool TutorialEnabled
        {
            get => _overrides.TutorialAllowed;
            set => _overrides.TutorialAllowed = value;
        }

        public void SkipTutorial()
        {
            _tutorial.Skip();
        }

        public void SetScriptedHold(double? seconds)
        {
            _overrides.HoldLimit = seconds;
        }

        public void PlaySeed(int seed)
        {
            _flow.Play(new EndlessLevelRequest(seed));
        }
    }
}
#endif
