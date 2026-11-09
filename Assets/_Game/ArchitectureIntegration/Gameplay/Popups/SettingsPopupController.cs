using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Settings;
using Game.UI;
using UnityEngine;

namespace Game.Integration.Gameplay
{
    /// <summary>Presents volume, frame-rate and language settings with reset and build information.</summary>
    public sealed class SettingsPopupController : PopupController<ISettingsPopupView>, ISimplePopupController
    {
        private const string VersionPrefix = "v";
        private static readonly VolumeChannel[] Channels = { VolumeChannel.Master, VolumeChannel.Music, VolumeChannel.Sfx, VolumeChannel.Ui, VolumeChannel.Voice };

        private readonly ISettingsService _settings;

        public SettingsPopupController(ISettingsPopupView view, ISettingsService settings) : base(view)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async UniTask ShowAsync(CancellationToken cancellationToken = default)
        {
            ShowAll();
            View.ShowBuild(VersionPrefix + Application.version);
            _settings.Changed += ShowAll;
            View.VolumeChanged += OnVolume;
            View.FpsChosen += OnFps;
            View.ResetClicked += OnReset;
            View.LanguageChosen += OnLanguage;
            try
            {
                await PresentAsync(cancellationToken);
            }
            finally
            {
                _settings.Changed -= ShowAll;
                View.VolumeChanged -= OnVolume;
                View.FpsChosen -= OnFps;
                View.ResetClicked -= OnReset;
                View.LanguageChosen -= OnLanguage;
            }
        }

        private void OnVolume(VolumeChannel channel, float value) => _settings.SetVolume(channel, value);

        private void OnFps(int fps)
        {
            _settings.SetTargetFps(fps);
            View.ShowFps(_settings.TargetFps);
        }

        private void OnReset()
        {
            _settings.ResetAll();
            ShowAll();
        }

        private void OnLanguage(string code)
        {
            foreach (var language in _settings.Languages)
                if (language.Code == code)
                {
                    _settings.SetLanguage(language);
                    return;
                }
        }

        private void ShowAll()
        {
            foreach (var channel in Channels)
                View.ShowVolume(channel, _settings.GetVolume(channel));
            View.ShowFps(_settings.TargetFps);
            View.ShowLanguages(_settings.Languages, _settings.CurrentLanguage);
        }
    }
}
