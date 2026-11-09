using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Slime.App.Game;
using Slime.Gameplay.Core.Geometry;
using Slime.Gameplay.Core.Model;
using Slime.Gameplay.View;
using Slime.Infrastructure.Audio;
using UnityEngine;

namespace Slime.Gameplay.Presentation
{
    /// <summary>
    /// Runs the model's action chain through the view: every view action is awaited to its real end and confirmed
    /// to the model with the attempt and action ids. A replaced attempt cancels its chain; stale completions cannot
    /// reach the new one.
    /// </summary>
    public sealed class AttemptRunner : IDisposable
    {
        private const float VictoryBeatSeconds = 0.35f;
        private const float HalfDiameter = 0.5f;

        private readonly GameplayStage _stage;
        private readonly CurrentAttempt _attempt;
        private readonly GameplayAudioPresenter _sounds;
        private readonly GameplayHapticsPresenter _haptics;
        private readonly IStarProgress _stars;

        public AttemptRunner(GameplayStage stage, CurrentAttempt attempt, GameplayAudioPresenter sounds, GameplayHapticsPresenter haptics,
            IStarProgress stars)
        {
            _stage = stage ? stage : throw new ArgumentNullException(nameof(stage));
            _attempt = attempt ?? throw new ArgumentNullException(nameof(attempt));
            _sounds = sounds ?? throw new ArgumentNullException(nameof(sounds));
            _haptics = haptics ?? throw new ArgumentNullException(nameof(haptics));
            _stars = stars ?? throw new ArgumentNullException(nameof(stars));
            _stage.Slime.PresentationCue += _haptics.OnSlimeCue;
        }

        public event Action<AttemptContext> Won;

        public event Action<AttemptContext> Lost;

        public void Prepare(AttemptContext context)
        {
            _sounds.BeginAttempt();
            context.Model.DoorsOpened += () =>
            {
                if (_attempt.Is(context))
                    _stage.Doors.Open();
            };
        }

        public void Run(AttemptContext context, ActionRequest request)
        {
            if (request == null)
                return;

            _attempt.IsBusy = true;
            ExecuteAsync(context, request).Forget(Debug.LogException);
        }

        public void Dispose()
        {
            _stage.Slime.PresentationCue -= _haptics.OnSlimeCue;
        }

        private async UniTask ExecuteAsync(AttemptContext context, ActionRequest request)
        {
            var token = context.Token;
            try
            {
                while (request != null && !token.IsCancellationRequested)
                {
                    switch (request.Kind)
                    {
                        case ActionKind.Advance:
                            request = await AdvanceAsync(context, request, token);
                            break;
                        case ActionKind.Shot:
                            request = await ShootAsync(context, request, token);
                            break;
                        case ActionKind.Clearing:
                            request = await ClearAsync(context, request, token);
                            break;
                        case ActionKind.Defeat:
                            request = await DefeatAsync(context, request, token);
                            break;
                        default:
                            request = null;
                            break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (_attempt.Is(context))
                    _attempt.IsBusy = false;
            }
        }

        private async UniTask<ActionRequest> AdvanceAsync(AttemptContext context, ActionRequest request, CancellationToken token)
        {
            var model = context.Model;
            var actionId = request.ActionId;
            await _stage.Slime.MoveAsync(
                (float)request.Advance.ToZ,
                z => model.ReportProgress(context.Id, actionId, z),
                DoorGateAsync,
                token);
            if (!model.ConfirmMove(context.Id, actionId))
                return null;

            if (model.Phase != AttemptPhase.Won)
            {
                return model.Phase == AttemptPhase.DefeatAnimation
                    ? model.Pending
                    : null;
            }

            // The outcome is fixed and saved at once; the dive and a short beat play before the popup.
            if (!_attempt.Is(context))
                return null;

            context.CaptureStars();
            _stars.Record(context.Built.ResultKey, context.EarnedStars);
            context.Policy.RecordWin(context);
            var pool = _stage.FinishPool;
            if (pool != null)
                _stage.CameraRig.ShowVictory(pool.position);

            await _stage.Slime.DiveIntoAsync(pool, _stage.PoolSplash, token);
            await _stage.Clock.Wait(VictoryBeatSeconds, token);
            if (_attempt.Is(context))
            {
                _sounds.Won();
                Won?.Invoke(context);
            }

            return null;
        }

        private async UniTask<ActionRequest> ShootAsync(AttemptContext context, ActionRequest request, CancellationToken token)
        {
            var model = context.Model;
            var plan = request.Shot;
            _sounds.PrepareShot(plan.HitIds.Count > 0);
            await _stage.Slime.ShootAsync(
                World(plan.TouchCenter),
                World(plan.ImpactCenter),
                (float)plan.ProjectileDiameter,
                _stage.ViewConfig.ImpactStrength((float)plan.BlastRadius),
                token);
            if (!model.ConfirmImpact(context.Id, request.ActionId))
                return null;

            if (plan.Hit)
                _stage.Preview.PlayImpact(World(plan.ImpactCenter), (float)plan.BlastRadius, _stage.ViewConfig.infectColor);

            return model.Pending;
        }

        private async UniTask<ActionRequest> ClearAsync(AttemptContext context, ActionRequest request, CancellationToken token)
        {
            var model = context.Model;
            var shot = request.Shot;
            await _stage.Crystals.ClearAsync(shot.HitIds, World(shot.ImpactCenter), (float)shot.BlastRadius, token);
            _sounds.ClearingFinished(shot.HitIds.Count > 0);
            if (!model.ConfirmClearing(context.Id, request.ActionId))
                return null;

            return model.Pending;
        }

        private async UniTask<ActionRequest> DefeatAsync(AttemptContext context, ActionRequest request, CancellationToken token)
        {
            var model = context.Model;
            _sounds.BeginDefeat();
            Vector3? target = request.DefeatCrystalId >= 0
                ? World(request.DefeatTarget)
                : (Vector3?)null;
            await _stage.Slime.DefeatAsync(target, token);
            if (!model.ConfirmDefeat(context.Id, request.ActionId))
                return null;

            if (_attempt.Is(context))
            {
                _sounds.Lost();
                Lost?.Invoke(context);
            }

            return null;
        }

        private async UniTask DoorGateAsync(float targetZ, CancellationToken token)
        {
            var doors = _stage.Doors;
            if (doors.IsOpen || targetZ < doors.Z - _stage.Slime.Diameter * HalfDiameter)
                return;

            // The slime never jumps through a closed leaf: movement waits until the doors are open.
            var context = _attempt.Context;
            if (context != null && context.Model.DoorsOpen)
                doors.Open();

            await doors.WaitOpenAsync(token);
        }

        private static Vector3 World(Vec2 point)
        {
            return new Vector3((float)point.X, 0, (float)point.Z);
        }
    }
}
