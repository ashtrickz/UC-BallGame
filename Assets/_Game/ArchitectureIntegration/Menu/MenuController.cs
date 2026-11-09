using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Slime.App.Game;
using Slime.Gameplay.Core.Levels;
using Slime.Infrastructure.Settings;
using Slime.UI;
using UnityEngine;

namespace Slime.Menu
{
    /// <summary>Menu scenario: level choice, the crystal wall and settings. Starting a round is delegated to the launcher.</summary>
    public sealed class MenuController : IController
    {
        private const int ContinueCampaign = 0;

        private readonly IMenuView _view;
        private readonly GameSession _session;
        private readonly IPopupService _popups;
        private readonly IGameLauncher _launcher;
        private readonly ILevelSelect _levelSelect;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private bool _initialized;
        private bool _popupOpen;
        private bool _disposed;

        public MenuController(IMenuView view, GameSession session, IPopupService popups, IGameLauncher launcher, ILevelSelect levelSelect)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _popups = popups ?? throw new ArgumentNullException(nameof(popups));
            _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
            _levelSelect = levelSelect ?? throw new ArgumentNullException(nameof(levelSelect));
        }

        private bool CanAct => !_disposed && _initialized && !_popupOpen && _session.Phase == GamePhase.Menu;

        public async UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MenuController));

            cancellationToken.ThrowIfCancellationRequested();
            if (_initialized)
                return;

            _initialized = true;
            _view.PlayRequested += OnPlay;
            _view.WallRequested += OnWall;
            _view.SettingsRequested += OnSettings;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
            await RefreshAsync(linked.Token);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _view.PlayRequested -= OnPlay;
            _view.WallRequested -= OnWall;
            _view.SettingsRequested -= OnSettings;
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        private void OnPlay()
        {
            if (CanAct)
                ChooseLevelAsync().Forget(Debug.LogException);
        }

        private void OnWall()
        {
            Launch(new WallLevelRequest(null));
        }

        private void OnSettings()
        {
            if (CanAct)
                ShowSettingsAsync().Forget(Debug.LogException);
        }

        private async UniTask ChooseLevelAsync()
        {
            _popupOpen = true;
            _view.SetInteractable(false);
            int? level = null;
            try
            {
                level = await _levelSelect.ChooseAsync(_lifetime.Token);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _popupOpen = false;
                if (!_disposed)
                    _view.SetInteractable(true);
            }

            if (!level.HasValue)
                return;

            // A null request continues the campaign from the saved progress.
            var request = level.Value > ContinueCampaign
                ? new CampaignLevelRequest(level.Value)
                : null;
            Launch(request);
        }

        private void Launch(LevelRequest request)
        {
            if (!CanAct)
                return;

            _view.SetInteractable(false);
            _launcher.LaunchAsync(request).Forget(Debug.LogException);
        }

        private async UniTask ShowSettingsAsync()
        {
            _popupOpen = true;
            _view.SetInteractable(false);
            try
            {
                await _popups.ShowAsync<SettingsPopupController>(_lifetime.Token);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _popupOpen = false;
                if (!_disposed)
                    _view.SetInteractable(true);
            }
        }

        private async UniTask RefreshAsync(CancellationToken token)
        {
            try
            {
                _view.SetInteractable(!_popupOpen);
                if (_session.Phase == GamePhase.Menu)
                    await _view.ShowAsync(token);
                else
                    await _view.HideAsync(token);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
