using System;
using Game.Gameplay.Model;
using Game.Localization;

namespace Game.Integration.Game
{
    /// <summary>Gameplay HUD: size meter, attempt label from the mode policy, hint and the restart button.</summary>
    public sealed class HudPresenter : IDisposable
    {
        private readonly GameView _view;
        private readonly ILocalizationService _localization;
        private readonly CurrentAttempt _attempt;

        public HudPresenter(GameView view, ILocalizationService localization, CurrentAttempt attempt)
        {
            _view = view ? view : throw new ArgumentNullException(nameof(view));
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
            _attempt = attempt ?? throw new ArgumentNullException(nameof(attempt));
            _view.BindLocalization(_localization);
            _view.RestartRequested += OnRestartRequested;
            _localization.Changed += RefreshLabel;
        }

        public event Action RestartRequested;

        public void BeginAttempt()
        {
            RefreshLabel();
            HideHint();
        }

        public void HideHint()
        {
            _view.ShowHint(null);
        }

        public void ShowSize(AttemptModel model)
        {
            var rules = model.Rules;
            var budget = (float)rules.Budget;
            var size = (float)(model.Diameter - rules.Dcrit) / budget;
            var beforeCharge = (float)(model.ChargeStartDiameter - rules.Dcrit) / budget;
            _view.ShowSize(size, beforeCharge);
        }

        public void Dispose()
        {
            _localization.Changed -= RefreshLabel;
            if (_view != null)
            {
                _view.RestartRequested -= OnRestartRequested;
                _view.BindLocalization(null);
            }
        }

        private void RefreshLabel()
        {
            var context = _attempt.Context;
            if (context == null)
                return;

            _view.ShowSeed(context.Policy.Label(context));
        }

        private void OnRestartRequested()
        {
            RestartRequested?.Invoke();
        }
    }
}
