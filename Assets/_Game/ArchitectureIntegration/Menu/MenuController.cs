using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.App.Game;
using Game.App.Scenes;
using Game.Integration.Gameplay;
using Game.Integration.Game;
using Game.Gameplay.Config;
using Game.UI;
using UnityEngine;

namespace Game.Integration.Menu
{
    public sealed class MenuController : IController
    {
        private readonly IMenuView _view;
        private readonly GameSession _session;
        private readonly GameNavigation _navigation;
        private readonly IPopupService _popups;
        private readonly ILevelSelect _levelSelect;
        private readonly SlimeGameConfig _config;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private bool _initialized;
        private bool _popupOpen;
        private bool _disposed;

        public MenuController(IMenuView view, GameSession session, IPopupService popups, GameNavigation navigation,
            ILevelSelect levelSelect = null, SlimeGameConfig config = null)
        {
            _config = config;
            _levelSelect = levelSelect;
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _popups = popups ?? throw new ArgumentNullException(nameof(popups));
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        }

        public async UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MenuController));
            cancellationToken.ThrowIfCancellationRequested();
            if (_initialized) return;
            _initialized = true;
            _view.PlayRequested += OnPlay;
            _view.WallRequested += OnWall;
            _view.SettingsRequested += OnSettings;
            _view.SlimeColorRequested += OnSlimeColor;
            if (_config != null) _view.SetSlimeColors(_config.SlimeColors, SlimeColorPreferences.SelectedIndex(_config));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
            await RefreshAsync(linked.Token);
        }

        private void OnPlay()
        {
            if (_levelSelect == null) Start(GameMode.Campaign);
            else if (!_disposed && _initialized && !_popupOpen && _session.Phase == GamePhase.Menu) ChooseLevelAsync().Forget(Debug.LogException);
        }

        /// <summary>LEVELS opens the level grid; a picked level starts the round, BACK returns to the menu.</summary>
        private async UniTask ChooseLevelAsync()
        {
            _popupOpen = true;
            _view.SetInteractable(false);
            int? level = null;
            try { level = await _levelSelect.ChooseAsync(_lifetime.Token); }
            catch (OperationCanceledException) { }
            finally
            {
                _popupOpen = false;
                if (!_disposed) _view.SetInteractable(true);
            }
            if (level.HasValue && !_disposed && _session.Phase == GamePhase.Menu)
            {
                _view.SetInteractable(false);
                StartGameAsync(GameMode.Campaign, level.Value).Forget(Debug.LogException);
            }
        }
        private void OnWall() => Start(GameMode.Wall);

        private void OnSlimeColor(int index)
        {
            if (_disposed || !_initialized || _popupOpen || _session.Phase != GamePhase.Menu) return;
            if (SlimeColorPreferences.Select(_config, index)) _view.SetSelectedSlimeColor(index);
        }

        private void Start(GameMode mode)
        {
            if (_disposed || !_initialized || _popupOpen || _session.Phase != GamePhase.Menu) return;
            _view.SetInteractable(false);
            StartGameAsync(mode).Forget(Debug.LogException);
        }

        private async UniTask StartGameAsync(GameMode mode, int level = 0)
        {
            _session.StartRound(mode, level);
            // Navigation belongs to ProjectScope and survives disposal of this menu.
            await _navigation.OpenAsync(GameNavigation.GameScene);
        }

        private void OnSettings()
        {
            if (_disposed || !_initialized || _popupOpen || _session.Phase != GamePhase.Menu) return;
            ShowSettingsAsync().Forget(Debug.LogException);
        }

        private async UniTask ShowSettingsAsync()
        {
            _popupOpen = true;
            _view.SetInteractable(false);
            try { await _popups.ShowAsync<SettingsPopupController>(_lifetime.Token); }
            catch (OperationCanceledException) { }
            finally
            {
                _popupOpen = false;
                if (!_disposed) _view.SetInteractable(true);
            }
        }

        private async UniTask RefreshAsync(CancellationToken token)
        {
            try
            {
                var inMenu = _session.Phase == GamePhase.Menu;
                _view.SetInteractable(!_popupOpen);
                if (inMenu) await _view.ShowAsync(token);
                else await _view.HideAsync(token);
            }
            catch (OperationCanceledException) { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _view.PlayRequested -= OnPlay;
            _view.WallRequested -= OnWall;
            _view.SettingsRequested -= OnSettings;
            _view.SlimeColorRequested -= OnSlimeColor;
            _lifetime.Cancel();
            _lifetime.Dispose();
        }
    }
}
