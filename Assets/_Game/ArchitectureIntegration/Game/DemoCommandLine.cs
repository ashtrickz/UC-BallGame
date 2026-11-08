using System;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.App.Game;
using Game.Gameplay.View;
using UnityEngine;

namespace Game.Integration.Game
{
    /// <summary>
    /// Development entry for recordings and smoke runs of a player build:
    /// <c>-demo-autoplay ControlPlan|Overcharge|Starve [-demo-seed N] [-demo-record folder] [-demo-quit]</c>.
    /// Without these arguments it does nothing.
    /// </summary>
    public static class DemoCommandLine
    {
        public static bool IsRequested => Enum.TryParse<AutoplayMode>(
            Value(Environment.GetCommandLineArgs(), "-demo-autoplay"), true, out _);

        public static bool TryRun(GameSession session, GameplayController gameplay, CancellationToken token)
        {
            var args = Environment.GetCommandLineArgs();
            var mode = Value(args, "-demo-autoplay");
            if (mode == null || !Enum.TryParse(mode, true, out AutoplayMode autoplay)) return false;
            RunAsync(session, gameplay, autoplay, Value(args, "-demo-seed"), Value(args, "-demo-record"),
                Array.IndexOf(args, "-demo-quit") >= 0, token).Forget(Debug.LogException);
            return true;
        }

        private static async UniTask RunAsync(GameSession session, GameplayController gameplay, AutoplayMode mode, string seed,
            string record, bool quit, CancellationToken token)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(1.5f), cancellationToken: token);
            if (session.Phase != GamePhase.Playing) session.StartRound();
            if (seed != null && int.TryParse(seed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) gameplay.RestartWithSeed(value);
            FrameRecorder recorder = null;
            if (!string.IsNullOrEmpty(record))
            {
                recorder = new GameObject("Frame Recorder").AddComponent<FrameRecorder>();
                recorder.Begin(record);
            }
            try
            {
                var result = await GameplayAutoplay.PlayAsync(gameplay, mode, token, thinkSeconds: 0.7f);
                await UniTask.Delay(TimeSpan.FromSeconds(3), DelayType.DeltaTime, cancellationToken: token);
                Debug.Log($"[Demo] {mode} seed {gameplay.Current.Seed}: {result}, frames {recorder?.Frames ?? 0}");
            }
            finally
            {
                recorder?.End();
                if (quit) Application.Quit();
            }
        }

        private static string Value(string[] args, string key)
        {
            var i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
