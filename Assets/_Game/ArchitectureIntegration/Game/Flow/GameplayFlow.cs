using System.Collections.Generic;
using System.Threading;
using System;
using Slime.App.Game;
using Slime.Gameplay.Config;
using Slime.Gameplay.Core.Config;
using Slime.Gameplay.Core.Levels;
using UnityEngine;

namespace Slime.Gameplay.Presentation
{
    /// <summary>
    /// Scene scenario of the gameplay: request, level, attempt, scene, start. Owns restart and level changes and
    /// wires the attempt runner to the result card.
    /// </summary>
    public sealed class GameplayFlow : IDisposable
    {
        private const int FirstCampaignLevel = 1;

        private readonly SlimeGameConfig _config;
        private readonly ILevelFactory _levels;
        private readonly IAttemptFactory _attempts;
        private readonly ILevelPresenter _presenter;
        private readonly ICampaignProgress _progress;
        private readonly CurrentAttempt _attempt;
        private readonly PauseService _pause;
        private readonly AttemptRunner _runner;
        private readonly ChargeInputController _charge;
        private readonly HudPresenter _hud;
        private readonly ResultPresenter _results;
        private readonly IReadOnlyList<IAttemptStartHook> _hooks;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private bool _initialized;
        private bool _disposed;

        public GameplayFlow(SlimeGameConfig config, ILevelFactory levels, IAttemptFactory attempts, ILevelPresenter presenter,
            ICampaignProgress progress, CurrentAttempt attempt, PauseService pause, AttemptRunner runner,
            ChargeInputController charge, HudPresenter hud, ResultPresenter results, IReadOnlyList<IAttemptStartHook> hooks)
        {
            _config = config ? config : throw new ArgumentNullException(nameof(config));
            _levels = levels ?? throw new ArgumentNullException(nameof(levels));
            _attempts = attempts ?? throw new ArgumentNullException(nameof(attempts));
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _attempt = attempt ?? throw new ArgumentNullException(nameof(attempt));
            _pause = pause ?? throw new ArgumentNullException(nameof(pause));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _charge = charge ?? throw new ArgumentNullException(nameof(charge));
            _hud = hud ?? throw new ArgumentNullException(nameof(hud));
            _results = results ?? throw new ArgumentNullException(nameof(results));
            _hooks = hooks ?? throw new ArgumentNullException(nameof(hooks));
            _hud.RestartRequested += Restart;
            _results.Chosen += Play;
            _results.MenuRequested += OnMenuRequested;
            _runner.Won += _results.ShowWon;
            _runner.Lost += _results.ShowLost;
        }

        public event Action MenuRequested;

        public AttemptContext Current => _attempt.Context;

        public bool HasCampaign => CampaignLength > 0;

        public int CampaignLength => _config.Campaign != null ? _config.Campaign.Count : 0;

        public ICampaignProgress Progress => _progress;

        public int ContinueLevel => Math.Min(Math.Max(FirstCampaignLevel, _progress.Unlocked), CampaignLength);

        public void Initialize(PauseReason initialPause)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GameplayFlow));

            if (_initialized)
                return;

            _initialized = true;
            _pause.Initialize(initialPause);
            if (HasCampaign)
                PlayCampaignLevel(ContinueLevel);
            else
                PlayEndless();

            _charge.Start();
        }

        public void Play(LevelRequest request)
        {
            switch (request)
            {
                case null:
                    PlayCampaignLevel(ContinueLevel);
                    break;
                case CampaignLevelRequest _ when !HasCampaign:
                    PlayEndless();
                    break;
                case CampaignLevelRequest campaign:
                    PlayCampaignLevel(campaign.Number);
                    break;
                default:
                    if (!_disposed)
                        Begin(_levels.Build(request));
                    break;
            }
        }

        public void Restart()
        {
            var context = _attempt.Context;
            if (context == null)
            {
                PlayEndless();
                return;
            }

            Play(context.Policy.Retry(context));
        }

        public void NextLevel()
        {
            var context = _attempt.Context;
            var next = context != null
                ? context.Policy.Next(context)
                : new CampaignLevelRequest(FirstCampaignLevel);

            Play(next);
        }

        public void PlayWall(int? seed = null)
        {
            Play(new WallLevelRequest(seed));
        }

        public void PlayEndless()
        {
            Play(new EndlessLevelRequest(null));
        }

        public void PlayCampaignLevel(int number)
        {
            if (_disposed)
                return;

            if (!HasCampaign)
                throw new InvalidOperationException("No campaign configured.");

            var unlocked = Math.Max(FirstCampaignLevel, _progress.Unlocked);
            number = Math.Max(FirstCampaignLevel, Math.Min(number, Math.Min(CampaignLength, unlocked)));
            Begin(_levels.Build(new CampaignLevelRequest(number)));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _hud.RestartRequested -= Restart;
            _results.Chosen -= Play;
            _results.MenuRequested -= OnMenuRequested;
            _runner.Won -= _results.ShowWon;
            _runner.Lost -= _results.ShowLost;
            _attempt.Replace(null);
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        private void Begin(BuiltLevel built)
        {
            foreach (var hook in _hooks)
                hook.OnAttemptEnding();

            _charge.ResetForAttempt();
            var context = _attempts.Create(built, _lifetime.Token);
            _attempt.Replace(context);
#if GAME_DEBUG
            Debug.Log($"[Gameplay] attempt {context.Id}: {built.Level.Describe()}");
#endif

            _presenter.Present(built);
            _results.Reset();
            _hud.BeginAttempt();
            _runner.Prepare(context);
            context.Model.SetPaused(_pause.IsPaused);
            _runner.Run(context, context.Model.Start());
            foreach (var hook in _hooks)
                hook.OnAttemptStarted(context);
        }

        private void OnMenuRequested()
        {
            MenuRequested?.Invoke();
        }
    }
}
