#if UNITY_STANDALONE
using System;
using System.Collections.Generic;
using Slime.Settings;
using UnityEngine;

namespace Slime.Infrastructure.Settings
{
    /// <summary>Applies and persists resolution and window mode in standalone players.</summary>
    public sealed class StandaloneDisplaySettings : IDisplaySettings
    {
        private const int MinimumWidth = 640;
        private const int MinimumHeight = 480;
        private const int DefaultRefreshRate = 60;

        public ResolutionOption Resolution { get; private set; }
        public WindowMode WindowMode { get; private set; }
        public IReadOnlyList<ResolutionOption> Resolutions => GetResolutions();

        public StandaloneDisplaySettings(SettingsPack pack)
        {
            Resolution = ToOption(Screen.currentResolution);
            WindowMode = (WindowMode)Screen.fullScreenMode;
            if (!pack.Contains(SettingId.Resolution) || !PlayerPrefs.HasKey(SettingsStorageKeys.Width))
                return;

            var mode = PlayerPrefs.GetInt(SettingsStorageKeys.WindowMode, (int)WindowMode);
            if (!Enum.IsDefined(typeof(WindowMode), mode))
                mode = (int)WindowMode;
            SetDisplay(new ResolutionOption(
                Math.Max(MinimumWidth, PlayerPrefs.GetInt(SettingsStorageKeys.Width)),
                Math.Max(MinimumHeight, PlayerPrefs.GetInt(SettingsStorageKeys.Height)),
                Math.Max(1, PlayerPrefs.GetInt(SettingsStorageKeys.RefreshRate, DefaultRefreshRate))), (WindowMode)mode);
        }

        public void SetDisplay(ResolutionOption resolution, WindowMode mode)
        {
            Resolution = resolution;
            WindowMode = mode;
            Screen.SetResolution(resolution.Width, resolution.Height, (FullScreenMode)mode,
                new RefreshRate { numerator = (uint)Math.Max(1, resolution.RefreshRate), denominator = 1 });
            PlayerPrefs.SetInt(SettingsStorageKeys.Width, resolution.Width);
            PlayerPrefs.SetInt(SettingsStorageKeys.Height, resolution.Height);
            PlayerPrefs.SetInt(SettingsStorageKeys.RefreshRate, resolution.RefreshRate);
            PlayerPrefs.SetInt(SettingsStorageKeys.WindowMode, (int)mode);
        }

        public void Reset() => SetDisplay(ToOption(Screen.currentResolution), WindowMode.FullscreenWindow);

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
#endif
