using System;
using System.Collections.Generic;
using Slime.Settings;
using UnityEngine;

namespace Slime.Infrastructure.Settings
{
    /// <summary>Persists shared preferences and delegates display controls to the compiled platform.</summary>
    public sealed class UnitySettingsRuntime : ISettingsRuntime
    {
        private const float DefaultVolume = 1;
        private readonly IReadOnlyList<LanguageOption> _languages;
        private readonly IDisplaySettings _display;

        public SettingsPack Pack
        {
            get;
        }
        public ResolutionOption AppliedResolution => _display.Resolution;
        public int AppliedFps
        {
            get;
            private set;
        }
        public IReadOnlyList<ResolutionOption> Resolutions => _display.Resolutions;
        public IReadOnlyList<LanguageOption> Languages => _languages;
        public int CursorCount => 0;
        public int CursorIndex => 0;
        public int TargetFps
        {
            get;
            private set;
        }
        public int MaximumFps => Pack.MaximumFps;
        public ResolutionOption Resolution => _display.Resolution;
        public WindowMode WindowMode => _display.WindowMode;
        public string CurrentLanguage
        {
            get;
            private set;
        }

        public UnitySettingsRuntime(SettingsPack pack, LanguageOptionsProvider languages, IDisplaySettings display)
        {
            Pack = pack ?? throw new ArgumentNullException(nameof(pack));
            _languages = languages.Value;
            _display = display;
            TargetFps = Mathf.Clamp(PlayerPrefs.GetInt(SettingsStorageKeys.TargetFps, Pack.DefaultFps), Pack.MinimumFps, Pack.MaximumFps);
            AppliedFps = TargetFps;
            CurrentLanguage = PlayerPrefs.GetString(SettingsStorageKeys.Language, _languages[0].Name);
            if (!HasLanguage(CurrentLanguage))
                CurrentLanguage = _languages[0].Name;
            QualitySettings.vSyncCount = Pack.Contains(SettingId.VSync) && GetToggle(PreferenceToggle.VSync) ? 1 : 0;
            Application.targetFrameRate = TargetFps;
        }

        public float GetVolume(VolumeChannel channel)
        {
            var value = PlayerPrefs.GetFloat(SettingsStorageKeys.VolumePrefix + channel, DefaultVolume);
            return float.IsNaN(value) || float.IsInfinity(value) ? DefaultVolume : Mathf.Clamp01(value);
        }

        public bool GetToggle(PreferenceToggle toggle) =>
            PlayerPrefs.GetInt(SettingsStorageKeys.TogglePrefix + toggle, toggle == PreferenceToggle.VSync ? 1 : 0) != 0;

        public void SetVolume(VolumeChannel channel, float value) =>
            PlayerPrefs.SetFloat(SettingsStorageKeys.VolumePrefix + channel, Mathf.Clamp01(value));

        public void SetToggle(PreferenceToggle toggle, bool value)
        {
            PlayerPrefs.SetInt(SettingsStorageKeys.TogglePrefix + toggle, value ? 1 : 0);
            if (toggle == PreferenceToggle.VSync)
                QualitySettings.vSyncCount = value ? 1 : 0;
        }

        public void SetTargetFps(int value)
        {
            TargetFps = value;
            AppliedFps = value;
            Application.targetFrameRate = value;
            PlayerPrefs.SetInt(SettingsStorageKeys.TargetFps, value);
        }

        public void SetCursor(int index)
        {
        }

        public void SetLanguage(LanguageOption language)
        {
            CurrentLanguage = language.Name;
            PlayerPrefs.SetString(SettingsStorageKeys.Language, language.Name);
        }

        public void SetDisplay(ResolutionOption resolution, WindowMode mode) => _display.SetDisplay(resolution, mode);

        public void ResetSetting(SettingId setting)
        {
            switch (setting)
            {
                case SettingId.Resolution:
                case SettingId.WindowMode:
                    _display.Reset();
                    break;

                case SettingId.TargetFps:
                    SetTargetFps(Pack.DefaultFps);
                    break;

                case SettingId.VSync:
                    SetToggle(PreferenceToggle.VSync, true);
                    break;

                case SettingId.MasterVolume:
                    SetVolume(VolumeChannel.Master, DefaultVolume);
                    break;

                case SettingId.MusicVolume:
                    SetVolume(VolumeChannel.Music, DefaultVolume);
                    break;

                case SettingId.SfxVolume:
                    SetVolume(VolumeChannel.Sfx, DefaultVolume);
                    break;

                case SettingId.UiVolume:
                    SetVolume(VolumeChannel.Ui, DefaultVolume);
                    break;

                case SettingId.VoiceVolume:
                    SetVolume(VolumeChannel.Voice, DefaultVolume);
                    break;

                case SettingId.Language:
                    SetLanguage(_languages[0]);
                    break;

                case SettingId.FixTooltipOnlyByClick:
                    SetToggle(PreferenceToggle.FixTooltipOnlyByClick, false);
                    break;

                case SettingId.MarketAutoLevelUp:
                    SetToggle(PreferenceToggle.MarketAutoLevelUp, false);
                    break;
            }
        }

        private bool HasLanguage(string name)
        {
            foreach (var language in _languages)
                if (language.Name == name)
                    return true;

            return false;
        }
    }
}
