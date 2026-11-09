#if GAME_DEBUG
using System;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Slime.App.Game;
using Slime.App.Scenes;
using Slime.Foundation;
using Slime.Gameplay.View;
using UnityEngine;
using VContainer.Unity;
using Slime.Gameplay.Presentation;

namespace Slime.Diagnostics
{
    /// <summary>
    /// Development arguments for recordings and smoke runs of a player build:
    /// <c>-demo-autoplay ControlPlan|Overcharge|Starve [-demo-seed N] [-demo-record folder] [-demo-quit]</c>.
    /// The demo entry points are registered only when these arguments are present.
    /// </summary>
    public static class DemoCommandLine
    {
        private const string AutoplayArgument = "-demo-autoplay";
        private const string SeedArgument = "-demo-seed";
        private const string RecordArgument = "-demo-record";
        private const string QuitArgument = "-demo-quit";

        public static bool IsRequested => TryGetMode(out _);

        public static bool TryGetMode(out AutoplayMode mode)
        {
            return Enum.TryParse(Value(AutoplayArgument), true, out mode);
        }

        public static int? Seed => int.TryParse(Value(SeedArgument), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed)
            ? seed
            : (int?)null;

        public static string RecordFolder => Value(RecordArgument);

        public static bool QuitAfterRun => Array.IndexOf(Environment.GetCommandLineArgs(), QuitArgument) >= 0;

        private static string Value(string key)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, key);

            return index >= 0 && index + 1 < args.Length
                ? args[index + 1]
                : null;
        }
    }

    /// <summary>Menu side of a demo run: once the menu is ready, starts the campaign.</summary>
    public sealed class DemoMenuEntryPoint : IStartable, IDisposable
    {
        private readonly ISceneReadiness _menu;
        private readonly IGameLauncher _launcher;
        private readonly GameNavigation _navigation;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        public DemoMenuEntryPoint(ISceneReadiness menu, IGameLauncher launcher, GameNavigation navigation)
        {
            _menu = menu;
            _launcher = launcher;
            _navigation = navigation;
        }

        public void Start()
        {
            OpenGameAsync().Forget(Debug.LogException);
        }

        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        private async UniTask OpenGameAsync()
        {
            await _menu.Ready.AttachExternalCancellation(_lifetime.Token);
            // Finish Loading -> Menu before requesting Menu -> Slime.
            await UniTask.WaitUntil(() => !_navigation.IsTransitioning, cancellationToken: _lifetime.Token);
            await _launcher.LaunchAsync(null);
        }
    }

    /// <summary>Game side of a demo run: plays the attempt with the autoplay, optionally recording frames and quitting.</summary>
    public sealed class DemoGameEntryPoint : IStartable, IDisposable
    {
        private const float StartDelaySeconds = 1.5f;
        private const float ThinkSeconds = 0.7f;
        private const float EndDelaySeconds = 3f;
        private const string RecorderName = "Frame Recorder";

        private readonly ISceneReadiness _game;
        private readonly GameSession _session;
        private readonly IGameplayDebug _gameplay;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        public DemoGameEntryPoint(ISceneReadiness game, GameSession session, IGameplayDebug gameplay)
        {
            _game = game;
            _session = session;
            _gameplay = gameplay;
        }

        public void Start()
        {
            if (DemoCommandLine.TryGetMode(out var mode))
                RunAsync(mode, _lifetime.Token).Forget(Debug.LogException);
        }

        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        private async UniTask RunAsync(AutoplayMode mode, CancellationToken token)
        {
            await _game.Ready.AttachExternalCancellation(token);
            await UniTask.Delay(TimeSpan.FromSeconds(StartDelaySeconds), cancellationToken: token);
            if (_session.Phase != GamePhase.Playing)
                _session.StartRound();

            var seed = DemoCommandLine.Seed;
            if (seed.HasValue)
                _gameplay.PlaySeed(seed.Value);

            FrameRecorder recorder = null;
            var folder = DemoCommandLine.RecordFolder;
            if (!string.IsNullOrEmpty(folder))
            {
                recorder = new GameObject(RecorderName).AddComponent<FrameRecorder>();
                recorder.Begin(folder);
            }

            try
            {
                var result = await GameplayAutoplay.PlayAsync(_gameplay, mode, token, ThinkSeconds);
                await UniTask.Delay(TimeSpan.FromSeconds(EndDelaySeconds), DelayType.DeltaTime, cancellationToken: token);
                GameLog.Info($"[Demo] {mode} seed {_gameplay.Current.Seed}: {result}, frames {recorder?.Frames ?? 0}");
            }
            finally
            {
                recorder?.End();
                if (DemoCommandLine.QuitAfterRun)
                    Application.Quit();
            }
        }
    }
}
#endif
