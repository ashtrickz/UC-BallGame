using System;
using Game.Gameplay.Levels;
using Game.Localization;
using UnityEngine;

namespace Game.Integration.Game
{
    /// <summary>
    /// Result card of a finished attempt: copy and choices from the mode policy, hidden while the menu covers the
    /// scene, re-rendered on a language change. A choice is reported as the level request it plays.
    /// </summary>
    public sealed class ResultPresenter : IDisposable
    {
        private readonly ResultView _resultView;
        private readonly GameView _view;
        private readonly ILocalizationService _localization;
        private readonly IPauseService _pause;
        private readonly CurrentAttempt _attempt;

        private bool _hasResult;
        private bool _won;

        public ResultPresenter(ResultView resultView, GameView view, ILocalizationService localization, IPauseService pause,
            CurrentAttempt attempt)
        {
            _resultView = resultView ? resultView : throw new ArgumentNullException(nameof(resultView));
            _view = view ? view : throw new ArgumentNullException(nameof(view));
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
            _pause = pause ?? throw new ArgumentNullException(nameof(pause));
            _attempt = attempt ?? throw new ArgumentNullException(nameof(attempt));
            _pause.Changed += OnPauseChanged;
            _localization.Changed += OnLanguageChanged;
        }

        public event Action<LevelRequest> Chosen;

        public event Action MenuRequested;

        public void Reset()
        {
            _resultView.Hide();
            _view.SetGameplayActionsVisible(true);
            _hasResult = false;
        }

        public void ShowWon(AttemptContext context)
        {
            Show(context, true);
#if GAME_DEBUG
            var model = context.Model;
            Debug.Log($"[Gameplay] attempt {context.Id} won: level {context.LevelNumber}, seed {context.Seed}, shots {model.ShotsFired}, spent {model.TotalSpent:0.###}");
#endif
        }

        public void ShowLost(AttemptContext context)
        {
            Show(context, false);
#if GAME_DEBUG
            var model = context.Model;
            Debug.Log($"[Gameplay] attempt {context.Id} lost ({model.Defeat}): level {context.LevelNumber}, seed {context.Seed}, shots {model.ShotsFired}");
#endif
        }

        public void Dispose()
        {
            _pause.Changed -= OnPauseChanged;
            _localization.Changed -= OnLanguageChanged;
            if (_resultView != null)
                _resultView.Hide();
        }

        private void Show(AttemptContext context, bool won)
        {
            if (!_attempt.Is(context))
                return;

            _hasResult = true;
            _won = won;
            Render(true);
        }

        private void Render(bool animate)
        {
            // Menu belongs only to the dedicated menu button; the policy picks the primary and secondary choices.
            var context = _attempt.Context;
            if (context == null)
                return;

            var result = _won
                ? context.Policy.Won(context)
                : context.Policy.Lost(context);
            var primary = result.Primary;
            var secondary = result.Secondary;
            var model = new ResultViewModel(
                result.Title,
                result.Subtitle,
                _localization.Get(primary.LabelKey),
                () => Choose(primary.Request),
                secondary.IsNone ? null : _localization.Get(secondary.LabelKey),
                secondary.IsNone ? null : (Action)(() => Choose(secondary.Request)),
                RequestMenu);

            _resultView.Initialize(model);
            _view.SetGameplayActionsVisible(false);
            if (animate && !_pause.Has(PauseReason.Menu))
                _resultView.Show();
        }

        private void Choose(LevelRequest request)
        {
            Chosen?.Invoke(request);
        }

        private void RequestMenu()
        {
            MenuRequested?.Invoke();
        }

        private void OnPauseChanged(PauseReason previous, PauseReason current)
        {
            var menuWasOpen = (previous & PauseReason.Menu) != 0;
            var menuIsOpen = (current & PauseReason.Menu) != 0;
            if (menuWasOpen == menuIsOpen)
                return;

            if (menuIsOpen)
                _resultView.Hide();
            else if (_hasResult)
                Render(true);
        }

        private void OnLanguageChanged()
        {
            if (_hasResult)
                Render(false);
        }
    }
}
