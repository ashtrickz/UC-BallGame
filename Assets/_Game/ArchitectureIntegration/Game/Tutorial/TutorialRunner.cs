using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Slime.Gameplay.Core.Model;
using Slime.Gameplay.View;
using Slime.Localization;
using UnityEngine;

namespace Slime.Gameplay.Presentation
{
    /// <summary>
    /// Runs the tutorial steps of one attempt. Explanation steps pause the game and wait for a tap; action steps let
    /// the player shoot for real and follow the charge. SKIP or a restart ends it cleanly.
    /// </summary>
    public sealed class TutorialRunner
    {
        private const string DoneStep = "done";
        private const float DoorTourOffset = 3.6f;
        private const float TourZoom = 0.7f;
        private const float ReturnZoom = 0.6f;
        private const float TourTimeoutSeconds = 3f;
        private const float TapGuardSeconds = 0.35f;

        private readonly TutorialAttemptHook _owner;
        private readonly AttemptContext _context;
        private readonly IReadOnlyList<TutorialStep> _steps;
        private readonly GameplayStage _stage;
        private readonly TutorialOverlay _overlay;
        private readonly SpotlightTargets _spotlight;
        private readonly ILocalizationService _localization;
        private readonly IPauseService _pause;
        private readonly CurrentAttempt _attempt;
        private readonly CancellationTokenSource _skip = new CancellationTokenSource();

        private UniTaskCompletionSource _tap;
        private string _messageKey;
        private bool _waitsForTap;

        public TutorialRunner(TutorialAttemptHook owner, AttemptContext context, IReadOnlyList<TutorialStep> steps,
            GameplayStage stage, SpotlightTargets spotlight, ILocalizationService localization, IPauseService pause,
            CurrentAttempt attempt)
        {
            _owner = owner;
            _context = context;
            _steps = steps;
            _stage = stage;
            _overlay = stage.Tutorial;
            _spotlight = spotlight;
            _localization = localization;
            _pause = pause;
            _attempt = attempt;
        }

        public bool IsRunning { get; private set; }

        public string Step { get; private set; } = string.Empty;

        public void Skip()
        {
            if (!_skip.IsCancellationRequested)
                _skip.Cancel();
        }

        public async UniTask RunAsync()
        {
            if (_overlay == null)
                return;

            IsRunning = true;
            _overlay.Continue += OnContinue;
            _overlay.Skip += Skip;
            _localization.Changed += RefreshMessage;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_context.Token, _skip.Token);
            var token = linked.Token;
            var model = _context.Model;
            try
            {
                // Start once the menu is gone and the slime stands at the first stop.
                await UniTask.WaitUntil(() => !_pause.Has(PauseReason.Menu) && IsStanding(model), cancellationToken: token);
                foreach (var step in _steps)
                {
                    if (!await RunStepAsync(step, model, token))
                        return;
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                Finish();
            }
        }

        private async UniTask<bool> RunStepAsync(TutorialStep step, AttemptModel model, CancellationToken token)
        {
            switch (step.Kind)
            {
                case TutorialStepKind.Tour:
                    await TourAsync(step, token);
                    return true;
                case TutorialStepKind.Explain:
                    await ExplainAsync(step, step.Targets, token);
                    return true;
                case TutorialStepKind.Act:
                    return await ActAsync(step, model, token);
                case TutorialStepKind.AwaitStop:
                    return await AwaitStopAsync(model, token);
                case TutorialStepKind.ExplainWhenDoorsOpen:
                    await UniTask.WaitUntil(() => model.DoorsOpen || IsOver(model), cancellationToken: token);
                    if (model.DoorsOpen && !IsOver(model))
                        await ExplainAsync(step, step.Targets, token);

                    return true;
                default:
                    return true;
            }
        }

        private async UniTask TourAsync(TutorialStep step, CancellationToken token)
        {
            var rig = _stage.CameraRig;
            var doorPoint = new Vector3(0, 0, (float)_context.Level.DoorZ - DoorTourOffset);
            rig.ShowPoint(doorPoint, TourZoom);
            await ExplainAsync(step, step.Targets, token);
            rig.ShowPoint(null, ReturnZoom);
            var deadline = Time.realtimeSinceStartup + TourTimeoutSeconds;
            await UniTask.WaitUntil(() => !rig.IsTouring || Time.realtimeSinceStartup > deadline, cancellationToken: token);
        }

        private async UniTask ExplainAsync(TutorialStep step, SpotlightTarget targets, CancellationToken token)
        {
            EnsureAlive();
            SetStep(step);
            _pause.Set(PauseReason.Tutorial, true);
            _spotlight.Show(_context, targets);
            ShowMessage(step.TextKey, true);
            // A short guard so the tap that closed the previous card cannot skip this one.
            await UniTask.Delay(TimeSpan.FromSeconds(TapGuardSeconds), DelayType.UnscaledDeltaTime, cancellationToken: token);
            _tap = new UniTaskCompletionSource();
            await _tap.Task.AttachExternalCancellation(token);
            _tap = null;
        }

        private async UniTask<bool> ActAsync(TutorialStep step, AttemptModel model, CancellationToken token)
        {
            SetStep(step);
            _pause.Set(PauseReason.Tutorial, false);
            ShowMessage(step.TextKey, false);
            while (model.ShotsFired < step.Shots)
            {
                EnsureAlive();
                if (IsOver(model))
                    return false;

                var charging = model.Phase == AttemptPhase.Charging;
                var key = charging ? step.ChargingTextKey : step.TextKey;
                _spotlight.Show(_context, charging ? step.ChargingTargets : step.Targets);
                if (_messageKey != key)
                    ShowMessage(key, false);

                await _stage.Clock.NextFrame(token);
            }

            // Watch the shot land and the path clear without any overlay.
            _overlay.Hide();

            return true;
        }

        private async UniTask<bool> AwaitStopAsync(AttemptModel model, CancellationToken token)
        {
            await UniTask.WaitUntil(() => IsStanding(model) || IsOver(model), cancellationToken: token);

            return model.Phase == AttemptPhase.Waiting;
        }

        private bool IsStanding(AttemptModel model)
        {
            return model.Phase == AttemptPhase.Waiting && !_attempt.IsBusy;
        }

        private static bool IsOver(AttemptModel model)
        {
            return model.IsFinished || model.Phase == AttemptPhase.DefeatAnimation;
        }

        private void SetStep(TutorialStep step)
        {
            if (step.Id != null)
                Step = step.Id;
        }

        private void Finish()
        {
            _overlay.Continue -= OnContinue;
            _overlay.Skip -= Skip;
            _localization.Changed -= RefreshMessage;
            // A newer tutorial (restart of level 1) owns the overlay now: leave it alone.
            var owner = _owner.Tutorial == null || _owner.Tutorial == this;
            if (owner)
            {
                _spotlight.Clear();
                if (_overlay)
                    _overlay.Hide();

                if (_stage && _stage.CameraRig)
                    _stage.CameraRig.ShowPoint(null);
            }

            if (_attempt.Is(_context))
                _pause.Set(PauseReason.Tutorial, false);

            IsRunning = false;
            Step = DoneStep;
        }

        private void OnContinue()
        {
            _tap?.TrySetResult();
        }

        private void ShowMessage(string key, bool waitsForTap)
        {
            _messageKey = key;
            _waitsForTap = waitsForTap;
            _overlay.Show(_localization.Get(key), waitsForTap);
        }

        private void RefreshMessage()
        {
            if (_overlay && _overlay.IsVisible && !string.IsNullOrEmpty(_messageKey))
                _overlay.Show(_localization.Get(_messageKey), _waitsForTap);
        }

        private void EnsureAlive()
        {
            if (!_overlay || !_stage)
                throw new OperationCanceledException("Tutorial scene objects are gone.");
        }
    }
}
