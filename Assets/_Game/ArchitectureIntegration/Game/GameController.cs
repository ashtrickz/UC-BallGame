using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.App.Game;
using Game.App.Scenes;
using Game.Integration.Gameplay;
using Game.UI;
using Game.Users;
using UnityEngine;

namespace Game.Integration.Game
{
    public sealed class GameController : IController
    {
        private readonly GameView _view;
        private readonly IUserRepository _users;
        private readonly IPopupService _popups;
        private readonly GameSession _session;
        private readonly GameplayController _gameplay;
        private readonly GameNavigation _navigation;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private bool _initialized;
        private bool _popupOpen;
        private bool _disposed;
        public GameController(GameView view, IUserRepository users, IPopupService popups, GameSession session, GameplayController gameplay, GameNavigation navigation)
        { _view = view; _users = users; _popups = popups; _session = session; _gameplay = gameplay; _navigation = navigation; }

        public async UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_initialized) return;
            _initialized = true;
            if (_session.Phase != GamePhase.Playing) _session.StartRound();
            _view.SettingsRequested += OpenSettings;
            _users.Changed += Refresh;
            _session.Changed += OnPhaseChanged;
            _session.RoundStarted += OnRoundStarted;
            _gameplay.MenuRequested += OnMenuRequested;
            _navigation.Changed += OnNavigationChanged;
            // Build the chosen attempt under the opaque loader; accept input after its reveal completes.
            _gameplay.Initialize(_navigation.IsTransitioning ? PauseReason.Menu : PauseReason.None);
            OnRoundStarted();
            Refresh();
            DemoCommandLine.TryRun(_session, _gameplay, _lifetime.Token);
            if (_session.Phase == GamePhase.Playing) await _view.ShowAsync(cancellationToken);
            else _view.HideInstant();
        }

        private void Refresh() => _view.ShowPlayer(_users.Profile?.PlayerName ?? "Player");
        private void OnPhaseChanged()
        {
            _gameplay.SetPaused(PauseReason.Menu, _session.Phase != GamePhase.Playing);
            RefreshVisibilityAsync().Forget(Debug.LogException);
        }

        private void OnRoundStarted()
        {
            if (_session.Mode == GameMode.Wall)
            {
                _gameplay.PlayWall();
                return;
            }
            // Levels: the level picked in the grid (or the continue level); keep the attempt prepared behind the menu
            // when it is exactly that level and still untouched.
            if (!_gameplay.HasCampaign) { _gameplay.Restart(); return; }
            var level = _session.RequestedLevel > 0 ? _session.RequestedLevel : _gameplay.ContinueLevel;
            var current = _gameplay.Current;
            var prepared = current != null && current.IsCampaign && current.LevelNumber == level
                && current.Model.ShotsFired == 0 && !current.Model.IsFinished;
            if (!prepared) _gameplay.PlayCampaignLevel(level);
        }

        private void OnMenuRequested()
        {
            if (!_disposed && !_navigation.IsTransitioning)
                _navigation.OpenAsync(GameNavigation.MenuScene).Forget(Debug.LogException);
        }
        private void OnNavigationChanged() => _gameplay.SetPaused(PauseReason.Menu,
            _navigation.IsTransitioning || _session.Phase != GamePhase.Playing);
        private async UniTask RefreshVisibilityAsync()
        {
            try
            {
                if (_session.Phase == GamePhase.Playing) await _view.ShowAsync(_lifetime.Token);
                else await _view.HideAsync(_lifetime.Token);
            }
            catch (OperationCanceledException) { }
        }
        private void OpenSettings()
        {
            if (!_disposed && !_popupOpen && _session.Phase == GamePhase.Playing)
                ShowSettingsAsync().Forget(Debug.LogException);
        }
        private async UniTask ShowSettingsAsync()
        {
            _popupOpen = true;
            _gameplay.SetPaused(PauseReason.Settings, true);
            try { await _popups.ShowAsync<SettingsPopupController>(_lifetime.Token); }
            // The popup can also be cancelled by the project lifetime before this scene is disposed.
            catch (OperationCanceledException) { }
            finally
            {
                _popupOpen = false;
                if (!_disposed) _gameplay.SetPaused(PauseReason.Settings, false);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _view.SettingsRequested -= OpenSettings;
            _users.Changed -= Refresh;
            _session.Changed -= OnPhaseChanged;
            _session.RoundStarted -= OnRoundStarted;
            _gameplay.MenuRequested -= OnMenuRequested;
            _navigation.Changed -= OnNavigationChanged;
            _lifetime.Cancel();
            _lifetime.Dispose();
        }
    }
}
