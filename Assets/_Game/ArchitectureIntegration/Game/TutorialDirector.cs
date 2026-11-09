using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Gameplay.Geometry;
using Game.Gameplay.Model;
using Game.Gameplay.View;
using Game.Localization;
using UnityEngine;

namespace Game.Integration.Game
{
    /// <summary>
    /// The level-1 tutorial: a camera tour to the doors, then spotlights on the slime, the size meter and the
    /// blocking crystal, two guided shots and the doors. Explanation steps pause the game and wait for a tap;
    /// action steps let the player shoot for real and react to the charge. SKIP or a restart ends it cleanly.
    /// </summary>
    public sealed class TutorialDirector
    {
        private readonly GameplayController _gameplay;
        private readonly AttemptContext _context;
        private readonly GameplayStage _stage;
        private readonly GameView _view;
        private readonly TutorialOverlay _overlay;
        private readonly CancellationTokenSource _skip = new CancellationTokenSource();
        private Func<List<SpotlightHole>> _holes = () => new List<SpotlightHole>();
        private UniTaskCompletionSource _tap;
        private readonly ILocalizationService _localization;
        private string _messageKey;
        private bool _waitsForTap;

        public bool IsRunning { get; private set; }
        public string Step { get; private set; } = "";

        public TutorialDirector(GameplayController gameplay, AttemptContext context, GameView view, ILocalizationService localization)
        {
            _gameplay = gameplay;
            _context = context;
            _stage = gameplay.Stage;
            _view = view;
            _overlay = _stage.Tutorial;
            _localization = localization;
        }

        public void Skip()
        {
            if (!_skip.IsCancellationRequested) _skip.Cancel();
        }

        public async UniTask RunAsync()
        {
            if (_overlay == null) return;
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
                await UniTask.WaitUntil(() => (_gameplay.Pause & PauseReason.Menu) == 0 && model.Phase == AttemptPhase.Waiting && !_gameplay.IsBusy,
                    cancellationToken: token);
                RunHolesAsync(token).Forget();

                var doorPoint = new Vector3(0, 0, (float)_context.Level.DoorZ - 3.6f);
                _stage.CameraRig.ShowPoint(doorPoint, 0.7f);
                await Explain("tour", "tutorial.tour", () => Holes(DoorsHole()), token);
                _stage.CameraRig.ShowPoint(null, 0.6f);
                var deadline = Time.realtimeSinceStartup + 3f;
                await UniTask.WaitUntil(() => !_stage.CameraRig.IsTouring || Time.realtimeSinceStartup > deadline, cancellationToken: token);

                await Explain("slime", "tutorial.slime", () => Holes(SlimeHole()), token);
                await Explain("meter", "tutorial.meter", () => Holes(MeterHole()), token);
                await Explain("crystal", "tutorial.crystal", () => Holes(SlimeHole(), BlockerHole(model)), token);

                if (!await Act("shot1", 1, token)) return;
                if (!await WaitForStop(model, token)) return;
                await Explain("hop", "tutorial.hop", () => Holes(SlimeHole(), BlockerHole(model)), token);
                if (!await Act("shot2", 2, token)) return;

                await UniTask.WaitUntil(() => model.DoorsOpen || model.IsFinished || model.Phase == AttemptPhase.DefeatAnimation, cancellationToken: token);
                if (model.DoorsOpen && !model.IsFinished && model.Phase != AttemptPhase.DefeatAnimation)
                    await Explain("doors", "tutorial.doors", () => Holes(DoorsHole()), token);
            }
            catch (OperationCanceledException) { }
            finally
            {
                _overlay.Continue -= OnContinue;
                _overlay.Skip -= Skip;
                _localization.Changed -= RefreshMessage;
                // A newer tutorial (restart of level 1) owns the overlay now: leave it alone.
                var owner = _gameplay.Tutorial == null || _gameplay.Tutorial == this;
                if (owner && _overlay) _overlay.Hide();
                if (owner && _stage && _stage.CameraRig) _stage.CameraRig.ShowPoint(null);
                if (_gameplay.Current == _context) _gameplay.SetPaused(PauseReason.Tutorial, false);
                IsRunning = false;
                Step = "done";
            }
        }

        // ------------------------------------------------------------------ steps

        /// <summary>Paused explanation: spotlight, message, wait for a tap.</summary>
        private async UniTask Explain(string step, string textKey, Func<List<SpotlightHole>> holes, CancellationToken token)
        {
            EnsureAlive();
            Step = step;
            _gameplay.SetPaused(PauseReason.Tutorial, true);
            _holes = holes;
            ShowMessage(textKey, true);
            // A short guard so the tap that closed the previous card cannot skip this one.
            await UniTask.Delay(TimeSpan.FromSeconds(0.35f), DelayType.UnscaledDeltaTime, cancellationToken: token);
            _tap = new UniTaskCompletionSource();
            await _tap.Task.AttachExternalCancellation(token);
            _tap = null;
        }

        /// <summary>Action step: the player shoots for real; the text follows the charge.</summary>
        private async UniTask<bool> Act(string step, int shotsTarget, CancellationToken token)
        {
            Step = step;
            var model = _context.Model;
            _gameplay.SetPaused(PauseReason.Tutorial, false);
            var tapKey = shotsTarget == 1 ? "tutorial.tap_first" : "tutorial.tap_again";
            const string holdKey = "tutorial.hold";
            ShowMessage(tapKey, false);
            while (model.ShotsFired < shotsTarget)
            {
                EnsureAlive();
                if (model.IsFinished || model.Phase == AttemptPhase.DefeatAnimation) return false;
                if (model.Phase == AttemptPhase.Charging)
                {
                    _holes = () => Holes(SlimeHole(), PreviewHole(model), MeterHole());
                    if (_messageKey != holdKey) ShowMessage(holdKey, false);
                }
                else
                {
                    _holes = () => Holes(SlimeHole(), BlockerHole(model));
                    if (_messageKey != tapKey) ShowMessage(tapKey, false);
                }
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            // Watch the shot land and the path clear without any overlay.
            _overlay.Hide();
            return true;
        }

        private async UniTask<bool> WaitForStop(AttemptModel model, CancellationToken token)
        {
            await UniTask.WaitUntil(() => (model.Phase == AttemptPhase.Waiting && !_gameplay.IsBusy) || model.IsFinished
                || model.Phase == AttemptPhase.DefeatAnimation, cancellationToken: token);
            return model.Phase == AttemptPhase.Waiting;
        }

        private void OnContinue() => _tap?.TrySetResult();

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
            if (!_overlay || !_stage) throw new OperationCanceledException("Tutorial scene objects are gone.");
        }

        private async UniTaskVoid RunHolesAsync(CancellationToken token)
        {
            // After the camera has moved this frame, so cut-outs stay glued to what they show.
            while (!token.IsCancellationRequested)
            {
                // The scene may unload before the attempt token is cancelled: never touch a destroyed overlay.
                if (!_overlay || !_stage || !_stage.CameraRig) return;
                if (_overlay.IsVisible) _overlay.SetHoles(_holes());
                if (await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, token).SuppressCancellationThrow()) return;
            }
        }

        // ------------------------------------------------------------------ targets (screen space)

        private static List<SpotlightHole> Holes(params SpotlightHole?[] holes)
        {
            var list = new List<SpotlightHole>();
            foreach (var hole in holes) if (hole.HasValue) list.Add(hole.Value);
            return list;
        }

        private Camera Camera => _stage.CameraRig.Camera;

        private SpotlightHole? SlimeHole()
        {
            var slime = _stage.Slime;
            var center = Camera.WorldToScreenPoint(slime.BodyCenter);
            var edge = Camera.WorldToScreenPoint(slime.BodyCenter + Camera.transform.right * (slime.Diameter * 0.5f));
            return SpotlightHole.Around(center, Vector2.Distance(center, edge) * 1.45f);
        }

        private SpotlightHole? MeterHole()
        {
            var meter = _view.SizeMeter;
            if (meter == null) return null;
            var corners = new Vector3[4];
            meter.GetWorldCorners(corners); // overlay canvas: world corners are screen pixels
            var rect = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
            return new SpotlightHole(Expand(rect, 18), false);
        }

        private SpotlightHole? BlockerHole(AttemptModel model)
        {
            var id = model.PeekAdvance().BlockingCrystalId;
            if (id < 0) return null;
            var placement = _context.Level.Crystals[id];
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var v in placement.Contour.Vertices)
                foreach (var h in new[] { 0f, 0.95f * (float)placement.Scale })
                {
                    Vector2 p = Camera.WorldToScreenPoint(new Vector3((float)v.X, h, (float)v.Z));
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }
            return new SpotlightHole(Expand(Rect.MinMaxRect(min.x, min.y, max.x, max.y), 22), false);
        }

        private SpotlightHole? PreviewHole(AttemptModel model)
        {
            var plan = model.PreviewShot();
            if (plan == null) return null;
            var center = new Vector3((float)plan.ImpactCenter.X, 0, (float)plan.ImpactCenter.Z);
            var c = Camera.WorldToScreenPoint(center);
            var e = Camera.WorldToScreenPoint(center + Camera.transform.right * (float)plan.BlastRadius);
            return SpotlightHole.Around(c, Vector2.Distance(c, e) + 16, 0.6f);
        }

        private SpotlightHole? DoorsHole()
        {
            var z = (float)_context.Level.DoorZ;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var x in new[] { -2.1f, 2.1f })
                foreach (var y in new[] { 0f, 3.3f })
                    foreach (var dz in new[] { -0.3f, 0.4f })
                    {
                        Vector2 p = Camera.WorldToScreenPoint(new Vector3(x, y, z + dz));
                        min = Vector2.Min(min, p);
                        max = Vector2.Max(max, p);
                    }
            return new SpotlightHole(Expand(Rect.MinMaxRect(min.x, min.y, max.x, max.y), 20), false);
        }

        private static Rect Expand(Rect rect, float by) => Rect.MinMaxRect(rect.xMin - by, rect.yMin - by, rect.xMax + by, rect.yMax + by);
    }
}
