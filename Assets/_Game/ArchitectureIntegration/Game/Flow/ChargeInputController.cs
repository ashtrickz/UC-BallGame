using System;
using Game.Gameplay.Geometry;
using Game.Gameplay.Model;
using Game.Gameplay.View;
using Game.Integration.Audio;
using UnityEngine;

namespace Game.Integration.Game
{
    /// <summary>
    /// Per-frame input of the attempt: press starts the charge, holding grows it with a live shot preview, release
    /// fires. Keeps the slime, corridor, camera and size meter in step with the model. Ticked by <see cref="GameplayFrame"/>.
    /// </summary>
    public sealed class ChargeInputController : IDisposable
    {
        private const double MinBudget = 1e-6;
        private const double HoldEpsilon = 1e-9;
        private const int CorridorSides = 2;

        private readonly GameplayStage _stage;
        private readonly CurrentAttempt _attempt;
        private readonly IPauseService _pause;
        private readonly AttemptRunner _runner;
        private readonly HudPresenter _hud;
        private readonly GameplayAudioPresenter _sounds;
        private readonly GameplayHapticsPresenter _haptics;
        private readonly IGameplayOverrides _overrides;

        private bool _running;

        public ChargeInputController(GameplayStage stage, CurrentAttempt attempt, IPauseService pause, AttemptRunner runner,
            HudPresenter hud, GameplayAudioPresenter sounds, GameplayHapticsPresenter haptics, IGameplayOverrides overrides)
        {
            _stage = stage ? stage : throw new ArgumentNullException(nameof(stage));
            _attempt = attempt ?? throw new ArgumentNullException(nameof(attempt));
            _pause = pause ?? throw new ArgumentNullException(nameof(pause));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _hud = hud ?? throw new ArgumentNullException(nameof(hud));
            _sounds = sounds ?? throw new ArgumentNullException(nameof(sounds));
            _haptics = haptics ?? throw new ArgumentNullException(nameof(haptics));
            _overrides = overrides ?? throw new ArgumentNullException(nameof(overrides));
            _pause.ChargeInterrupted += CancelCharge;
        }

        public void Start()
        {
            _running = true;
        }

        public void ResetForAttempt()
        {
            _haptics.CancelCharge();
            _stage.Input.RequireFreshPress();
        }

        public void Tick()
        {
            if (!_running)
                return;

            var context = _attempt.Context;
            if (context == null || context.IsDisposed)
                return;

            var model = context.Model;
            var input = _stage.Input;
            input.Enabled = !_pause.IsPaused && !_attempt.IsBusy && model.Phase == AttemptPhase.Waiting;
            input.Poll();
            try
            {
                if (!_pause.IsPaused && !_attempt.IsBusy)
                    Step(context);

                Sync(model);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }

        public void Dispose()
        {
            _running = false;
            _pause.ChargeInterrupted -= CancelCharge;
        }

        private void Step(AttemptContext context)
        {
            var model = context.Model;
            var input = _stage.Input;
            if (model.Phase == AttemptPhase.Waiting && input.PressedThisFrame && model.BeginCharge())
            {
                BeginCharge(model);
            }
            else if (model.Phase == AttemptPhase.Charging && input.IsHeld)
            {
                double delta = _stage.Clock.Delta;
                var limit = _overrides.HoldLimit;
                if (limit.HasValue)
                    delta = Math.Min(delta, Math.Max(0, limit.Value - model.HoldSeconds));

                model.TickCharge(delta);
            }

            var holdLimit = _overrides.HoldLimit;
            var scriptedRelease = holdLimit.HasValue && model.HoldSeconds >= holdLimit.Value - HoldEpsilon;
            if (model.Phase == AttemptPhase.DefeatAnimation && model.Pending?.Kind == ActionKind.Defeat)
            {
                // Held too long: the critical size was reached during the hold. The outcome is fixed; the view only shows it.
                ClearPreview();
                _haptics.CancelCharge();
                _stage.CameraRig.SettleTo((float)model.Diameter);
                _runner.Run(context, model.Pending);
            }
            else if (model.Phase == AttemptPhase.Charging && (!input.IsHeld || scriptedRelease))
            {
                var shot = model.ReleaseCharge();
                _haptics.CancelCharge();
                _stage.Preview.Hide();
                _stage.Crystals.CommitPreview(shot?.Shot?.HitIds);
                _stage.CameraRig.SettleTo((float)model.Diameter);
                _runner.Run(context, shot);
            }
            else if (model.Phase == AttemptPhase.Charging)
            {
                ShowPreview(model);
            }
        }

        private void BeginCharge(AttemptModel model)
        {
            _stage.Slime.BeginCharge();
            _sounds.BeginCharge();
            var rules = model.Rules;
            var budget = Math.Max(MinBudget, rules.Budget);
            var rate = rules.Values.ChargeRate;
            _haptics.BeginCharge(
                (float)(rules.ChargeCost(0) / budget),
                (float)(rate / budget),
                (float)Math.Max(0, (model.Diameter - rules.Dcrit) / rate));
            _stage.CameraRig.OnChargeStarted((float)model.ChargeStartDiameter);
            _hud.HideHint();
        }

        private void Sync(AttemptModel model)
        {
            var rules = model.Rules;
            _stage.Slime.SetDiameter((float)model.Diameter);
            _stage.Track.SetCorridorWidth((float)(rules.CorridorHalfWidth(model.Diameter) * CorridorSides));
            _stage.CameraRig.Follow(_stage.Slime.Anchor);
            _hud.ShowSize(model);
        }

        private void ShowPreview(AttemptModel model)
        {
            var plan = model.PreviewShot();
            if (plan == null)
                return;

            var rules = model.Rules;
            var budgetLeft = Math.Max(MinBudget, model.ChargeStartDiameter - rules.Dcrit);
            var charge = (float)(plan.Cost / budgetLeft);
            _stage.Slime.UpdateCharge((float)plan.ProjectileDiameter, charge);
            _sounds.UpdateCharge(charge, (float)((model.Diameter - rules.Dcrit) / rules.Budget));
            _stage.Preview.Show(World(plan.ImpactCenter), (float)plan.BlastRadius, _stage.ViewConfig.previewColor);
            _stage.Crystals.SetPreview(plan.HitIds);
        }

        private void ClearPreview()
        {
            _stage.Preview.Hide();
            _stage.Crystals.SetPreview(null);
        }

        private void CancelCharge(AttemptModel model)
        {
            _stage.Slime.CancelCharge();
            _sounds.CancelCharge();
            _haptics.CancelCharge();
            ClearPreview();
            _stage.CameraRig.SettleTo((float)model.Diameter);
        }

        private static Vector3 World(Vec2 point)
        {
            return new Vector3((float)point.X, 0, (float)point.Z);
        }
    }
}
