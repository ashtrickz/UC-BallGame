using Slime.App.Game;
using Slime.Gameplay.View;
using Slime.Composition;
using Slime.Diagnostics;
using Slime.Gameplay.Presentation;
using VContainer;

namespace Slime.Gameplay.Tests.PlayMode
{
    /// <summary>Test access to the gameplay scene services resolved from its container.</summary>
    public sealed class GameplayHandle : IGameplayDebug
    {
        private readonly GameplayFlow _flow;
        private readonly IPauseService _pause;
        private readonly IGameplayDebug _debug;

        public GameplayHandle(IObjectResolver container)
        {
            _flow = container.Resolve<GameplayFlow>();
            _pause = container.Resolve<IPauseService>();
            _debug = container.Resolve<IGameplayDebug>();
            Stage = container.Resolve<GameplayStage>();
        }

        public GameplayStage Stage { get; }

        public AttemptContext Current => _debug.Current;

        public bool IsBusy => _debug.IsBusy;

        public PauseReason Pause => _debug.Pause;

        public PointerChargeInput Input => _debug.Input;

        public GameClock Clock => _debug.Clock;

        public TutorialRunner Tutorial => _debug.Tutorial;

        public bool TutorialEnabled
        {
            get => _debug.TutorialEnabled;
            set => _debug.TutorialEnabled = value;
        }

        public int CampaignLength => _flow.CampaignLength;

        public int ContinueLevel => _flow.ContinueLevel;

        public ICampaignProgress Progress => _flow.Progress;

        public void SkipTutorial()
        {
            _debug.SkipTutorial();
        }

        public void SetScriptedHold(double? seconds)
        {
            _debug.SetScriptedHold(seconds);
        }

        public void PlaySeed(int seed)
        {
            _debug.PlaySeed(seed);
        }

        public void RestartWithSeed(int seed)
        {
            _debug.PlaySeed(seed);
        }

        public void SetPaused(PauseReason reason, bool paused)
        {
            _pause.Set(reason, paused);
        }

        public void Restart()
        {
            _flow.Restart();
        }

        public void NextLevel()
        {
            _flow.NextLevel();
        }

        public void PlayWall(int? seed = null)
        {
            _flow.PlayWall(seed);
        }

        public void PlayEndless()
        {
            _flow.PlayEndless();
        }

        public void PlayCampaignLevel(int number)
        {
            _flow.PlayCampaignLevel(number);
        }
    }
}
