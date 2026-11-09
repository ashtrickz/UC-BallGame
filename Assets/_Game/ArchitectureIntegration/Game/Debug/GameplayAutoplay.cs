#if GAME_DEBUG
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Gameplay.Model;

namespace Game.Integration.Game
{
    public enum AutoplayMode
    {
        ControlPlan,
        Overcharge,
        Starve,
    }

    /// <summary>
    /// Debug driver that presses and releases a virtual pointer through the normal input path, so the attempt runs
    /// with real animations, waits and confirmations. Used by PlayMode tests and for recording the demo videos.
    /// </summary>
    public static class GameplayAutoplay
    {
        private const double StarveReserve = 0.6;
        private const int PressFrames = 30;
        private const double UnscriptedHoldSeconds = 1e6;

        public static async UniTask<AttemptPhase> PlayAsync(IGameplayDebug gameplay, AutoplayMode mode, CancellationToken token,
            float thinkSeconds = 0)
        {
            gameplay.SkipTutorial();
            var context = gameplay.Current ?? throw new InvalidOperationException("No attempt.");
            var model = context.Model;
            var input = gameplay.Input;
            var plan = context.Level.Control.HoldSeconds;
            var shot = 0;
            try
            {
                while (!model.IsFinished)
                {
                    token.ThrowIfCancellationRequested();
                    if (gameplay.Current != context)
                        throw new OperationCanceledException("Attempt was replaced.");

                    if (model.Phase == AttemptPhase.Waiting && !gameplay.IsBusy && gameplay.Pause == PauseReason.None)
                    {
                        double hold;
                        switch (mode)
                        {
                            case AutoplayMode.ControlPlan: hold = shot < plan.Count ? plan[shot] : 0;
                            break;
                            case AutoplayMode.Overcharge: hold = double.MaxValue;
                            break;
                            default:
                                hold = shot == 0
                                    ? (model.Diameter - model.Rules.Dcrit - model.Rules.Values.MinShotCost * StarveReserve - model.Rules.Values.MinShotCost) / model.Rules.Values.ChargeRate
                                    : 0;
                                break;
                        }
                        // Optional "player looks at the layout" pause, in presentation time (pauses with the game).
                        if (thinkSeconds > 0)
                            await gameplay.Clock.Wait(thinkSeconds, token);
                        if (model.Phase != AttemptPhase.Waiting)
                            continue;

                        await HoldAsync(gameplay, model, hold, token);
                        shot++;
                    }
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
                return model.Phase;
            }
            finally
            {
                input.SetVirtualPointer(null);
            }
        }

        public static async UniTask HoldAsync(IGameplayDebug gameplay, AttemptModel model, double seconds, CancellationToken token)
        {
            var input = gameplay.Input;
            var scripted = !double.IsInfinity(seconds) && seconds < UnscriptedHoldSeconds;
            gameplay.SetScriptedHold(scripted ? seconds : (double?)null);
            try
            {
                input.SetVirtualPointer(true);
                for (var i = 0; i < PressFrames && model.Phase == AttemptPhase.Waiting; i++)
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                // The controller releases at the exact hold; an unscripted hold ends with the overcharge defeat.
                while (model.Phase == AttemptPhase.Charging)
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            finally
            {
                gameplay.SetScriptedHold(null);
                input.SetVirtualPointer(false);
            }
            await UniTask.Yield(PlayerLoopTiming.Update, token);
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
    }
}
#endif
