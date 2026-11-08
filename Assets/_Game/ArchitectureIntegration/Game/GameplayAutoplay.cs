using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Gameplay.Model;

namespace Game.Integration.Game
{
    public enum AutoplayMode
    {
        /// <summary>Plays the generator's control plan (G01 proof through the real presentation).</summary>
        ControlPlan,
        /// <summary>Holds at the first stop until the critical size is reached (overcharge defeat).</summary>
        Overcharge,
        /// <summary>One huge shot that leaves too little for a minimum shot, then taps (out-of-resource defeat).</summary>
        Starve,
    }

    /// <summary>
    /// Debug driver that presses and releases a virtual pointer through the normal input path, so the attempt runs
    /// with real animations, waits and confirmations. Used by PlayMode tests and for recording the demo videos.
    /// </summary>
    public static class GameplayAutoplay
    {
        public static async UniTask<AttemptPhase> PlayAsync(GameplayController gameplay, AutoplayMode mode, CancellationToken token,
            float thinkSeconds = 0)
        {
            gameplay.SkipTutorial();
            var context = gameplay.Current ?? throw new InvalidOperationException("No attempt.");
            var model = context.Model;
            var input = gameplay.Stage.Input;
            var plan = context.Level.Control.HoldSeconds;
            var shot = 0;
            try
            {
                while (!model.IsFinished)
                {
                    token.ThrowIfCancellationRequested();
                    if (gameplay.Current != context) throw new OperationCanceledException("Attempt was replaced.");
                    if (model.Phase == AttemptPhase.Waiting && !gameplay.IsBusy && gameplay.Pause == PauseReason.None)
                    {
                        double hold;
                        switch (mode)
                        {
                            case AutoplayMode.ControlPlan: hold = shot < plan.Count ? plan[shot] : 0; break;
                            case AutoplayMode.Overcharge: hold = double.MaxValue; break;
                            default:
                                hold = shot == 0
                                    ? (model.Diameter - model.Rules.Dcrit - model.Rules.Values.MinShotCost * 0.6 - model.Rules.Values.MinShotCost) / model.Rules.Values.ChargeRate
                                    : 0;
                                break;
                        }
                        // Optional "player looks at the layout" pause, in presentation time (pauses with the game).
                        if (thinkSeconds > 0) await gameplay.Stage.Clock.Wait(thinkSeconds, token);
                        if (model.Phase != AttemptPhase.Waiting) continue;
                        await HoldAsync(gameplay, model, hold, token);
                        shot++;
                    }
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
                return model.Phase;
            }
            finally { input.SetVirtualPointer(null); }
        }

        /// <summary>Presses, holds for exactly <paramref name="seconds"/> of model hold time (or until the outcome), releases.</summary>
        public static async UniTask HoldAsync(GameplayController gameplay, AttemptModel model, double seconds, CancellationToken token)
        {
            var input = gameplay.Stage.Input;
            var scripted = !double.IsInfinity(seconds) && seconds < 1e6;
            gameplay.SetScriptedHold(scripted ? seconds : (double?)null);
            try
            {
                input.SetVirtualPointer(true);
                for (var i = 0; i < 30 && model.Phase == AttemptPhase.Waiting; i++) await UniTask.Yield(PlayerLoopTiming.Update, token);
                // The controller releases at the exact hold; an unscripted hold ends with the overcharge defeat.
                while (model.Phase == AttemptPhase.Charging) await UniTask.Yield(PlayerLoopTiming.Update, token);
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
