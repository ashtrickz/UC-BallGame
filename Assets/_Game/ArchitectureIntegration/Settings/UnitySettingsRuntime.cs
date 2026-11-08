using Game.Settings;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Integration.Settings
{
    // A minimal, dependency-free ISettingsRuntime: PlayerPrefs-backed, no legacy
    // ScrObjLib/SettingsRepository and no external localization service. Replace with a
    // richer implementation if the project needs one.
    public sealed class UnitySettingsRuntime : ISettingsRuntime
    {
        private const string KeyPrefix = "Settings.";

        private static readonly LanguageOption[] SupportedLanguages =
        {
            new LanguageOption("en", "en", "English"),
            new LanguageOption("ru", "ru", "Русский")
        };

        public SettingsPack Pack { get; }
        public ResolutionOption AppliedResolution { get; private set; }
        public int AppliedFps { get; private set; }
        public IReadOnlyList<ResolutionOption> Resolutions => GetResolutions();
        public IReadOnlyList<LanguageOption> Languages => Array.AsReadOnly(SupportedLanguages);
        public int CursorCount => 0;
        public int CursorIndex => 0;
        public int TargetFps { get; private set; }
        public int MaximumFps => Pack.MaximumFps;
        public ResolutionOption Resolution { get; private set; }
        public WindowMode WindowMode { get; private set; }
        public string CurrentLanguage { get; private set; }


        public UnitySettingsRuntime(SettingsPack pack)
        {
            Pack = pack ?? throw new ArgumentNullException(nameof(pack));

            Resolution = ToOption(Screen.currentResolution);
            AppliedResolution = Resolution;
            WindowMode = (WindowMode)Screen.fullScreenMode;
            TargetFps = Mathf.Clamp(PlayerPrefs.GetInt(KeyPrefix + "TargetFps", Pack.DefaultFps), Pack.MinimumFps, Pack.MaximumFps);
            AppliedFps = TargetFps;
            CurrentLanguage = PlayerPrefs.GetString(KeyPrefix + "Language", SupportedLanguages[0].Name);

            if (!Array.Exists(SupportedLanguages, language => language.Name == CurrentLanguage))
                CurrentLanguage = SupportedLanguages[0].Name;
            if (Pack.Contains(SettingId.VSync)) QualitySettings.vSyncCount = GetToggle(PreferenceToggle.VSync) ? 1 : 0;
            else QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFps;
            if (Pack.Contains(SettingId.Resolution) && PlayerPrefs.HasKey(KeyPrefix + "Width"))
            {
                var mode = PlayerPrefs.GetInt(KeyPrefix + "WindowMode", (int)WindowMode);
                if (!Enum.IsDefined(typeof(WindowMode), mode)) mode = (int)WindowMode;
                SetDisplay(new ResolutionOption(Math.Max(640, PlayerPrefs.GetInt(KeyPrefix + "Width")),
                    Math.Max(480, PlayerPrefs.GetInt(KeyPrefix + "Height")),
                    Math.Max(1, PlayerPrefs.GetInt(KeyPrefix + "RefreshRate", 60))), (WindowMode)mode);
            }
        }

        public float GetVolume(VolumeChannel channel)
        {
            var value = PlayerPrefs.GetFloat(KeyPrefix + "Volume." + channel, 1f);
            return float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp01(value);
        }

        public bool GetToggle(PreferenceToggle toggle) =>
            PlayerPrefs.GetInt(KeyPrefix + "Toggle." + toggle, toggle == PreferenceToggle.VSync ? 1 : 0) != 0;

        public void SetVolume(VolumeChannel channel, float value)
        {
            PlayerPrefs.SetFloat(KeyPrefix + "Volume." + channel, Mathf.Clamp01(value));
        }

        public void SetToggle(PreferenceToggle toggle, bool value)
        {
            PlayerPrefs.SetInt(KeyPrefix + "Toggle." + toggle, value ? 1 : 0);

            if (toggle == PreferenceToggle.VSync)
                QualitySettings.vSyncCount = value ? 1 : 0;
        }

        public void SetTargetFps(int value)
        {
            TargetFps = value;
            AppliedFps = value;
            Application.targetFrameRate = value;
            PlayerPrefs.SetInt(KeyPrefix + "TargetFps", value);
        }

        public void SetCursor(int index)
        {
        }

        public void SetLanguage(LanguageOption language)
        {
            CurrentLanguage = language.Name;
            PlayerPrefs.SetString(KeyPrefix + "Language", language.Name);
        }

        public void SetDisplay(ResolutionOption resolution, WindowMode mode)
        {
            Resolution = resolution;
            AppliedResolution = resolution;
            WindowMode = mode;
            Screen.SetResolution(resolution.Width, resolution.Height, (FullScreenMode)mode,
                new RefreshRate { numerator = (uint)Math.Max(1, resolution.RefreshRate), denominator = 1 });
            PlayerPrefs.SetInt(KeyPrefix + "Width", resolution.Width);
            PlayerPrefs.SetInt(KeyPrefix + "Height", resolution.Height);
            PlayerPrefs.SetInt(KeyPrefix + "RefreshRate", resolution.RefreshRate);
            PlayerPrefs.SetInt(KeyPrefix + "WindowMode", (int)mode);
        }

        public void ResetSetting(SettingId setting)
        {
            switch (setting)
            {
                case SettingId.Resolution:
                case SettingId.WindowMode:
                    SetDisplay(ToOption(Screen.currentResolution), WindowMode.FullscreenWindow);
                    break;
                case SettingId.TargetFps:
                    SetTargetFps(Pack.DefaultFps);
                    break;
                case SettingId.VSync:
                    SetToggle(PreferenceToggle.VSync, true);
                    break;
                case SettingId.MasterVolume:
                    SetVolume(VolumeChannel.Master, 1f);
                    break;
                case SettingId.MusicVolume:
                    SetVolume(VolumeChannel.Music, 1f);
                    break;
                case SettingId.SfxVolume:
                    SetVolume(VolumeChannel.Sfx, 1f);
                    break;
                case SettingId.UiVolume:
                    SetVolume(VolumeChannel.Ui, 1f);
                    break;
                case SettingId.VoiceVolume:
                    SetVolume(VolumeChannel.Voice, 1f);
                    break;
                case SettingId.Language:
                    SetLanguage(SupportedLanguages[0]);
                    break;
                case SettingId.FixTooltipOnlyByClick:
                    SetToggle(PreferenceToggle.FixTooltipOnlyByClick, false);
                    break;
                case SettingId.MarketAutoLevelUp:
                    SetToggle(PreferenceToggle.MarketAutoLevelUp, false);
                    break;
            }
        }

        private static IReadOnlyList<ResolutionOption> GetResolutions()
        {
            var resolutions = new List<ResolutionOption>();

            foreach (var resolution in Screen.resolutions)
            {
                if (resolutions.Exists(option => option.Width == resolution.width && option.Height == resolution.height))
                    continue;

                resolutions.Add(ToOption(resolution));
            }

            return resolutions;
        }

        private static ResolutionOption ToOption(Resolution resolution) =>
            new ResolutionOption(resolution.width, resolution.height, (int)Math.Round(resolution.refreshRateRatio.value));
    }
}
