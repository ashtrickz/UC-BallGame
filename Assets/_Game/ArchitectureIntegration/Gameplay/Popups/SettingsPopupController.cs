using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Users;
using Game.Settings;
using Game.UI;
using UnityEngine;

namespace Game.Integration.Gameplay
{
    /// <summary>The settings window: volume sliders and the frame rate applied at once, a reset to defaults, the build's version, and a copy of the player's id for reports (the id only finds the player, it does not log in).</summary>
    public sealed class SettingsPopupController : PopupController<ISettingsPopupView>, ISimplePopupController
    {
        private static readonly VolumeChannel[] Channels = { VolumeChannel.Master, VolumeChannel.Music, VolumeChannel.Sfx, VolumeChannel.Ui, VolumeChannel.Voice };

        private readonly ISettingsService _settings;
        private readonly IUserRepository _users;

        public SettingsPopupController(ISettingsPopupView view, ISettingsService settings, IUserRepository users) : base(view)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _users = users ?? throw new ArgumentNullException(nameof(users));
        }

        public async UniTask ShowAsync(CancellationToken cancellationToken = default)
        {
            ShowAll();
            View.ShowBuild("v" + Application.version, _users.HasProfile);
            _settings.Changed += ShowAll;
            View.VolumeChanged += OnVolume;
            View.FpsChosen += OnFps;
            View.CopyKeyClicked += OnCopyKey;
            View.ResetClicked += OnReset;
            try { await PresentAsync(cancellationToken); }
            finally
            {
                _settings.Changed -= ShowAll;
                View.VolumeChanged -= OnVolume;
                View.FpsChosen -= OnFps;
                View.CopyKeyClicked -= OnCopyKey;
                View.ResetClicked -= OnReset;
            }
        }

        private void OnVolume(VolumeChannel channel, float value) => _settings.SetVolume(channel, value);

        private void OnFps(int fps)
        {
            _settings.SetTargetFps(fps);
            View.ShowFps(_settings.TargetFps);
        }

        private void OnCopyKey()
        {
            if (_users.HasProfile) View.CopyKey(_users.Profile.UserId);
        }

        private void OnReset()
        {
            _settings.ResetAll();
            ShowAll();
        }

        private void ShowAll()
        {
            foreach (var channel in Channels) View.ShowVolume(channel, _settings.GetVolume(channel));
            View.ShowFps(_settings.TargetFps);
        }
    }
}
