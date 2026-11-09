using System;
using System.Collections.Generic;

namespace Slime.Settings
{
    public enum VolumeChannel { Master, Music, Sfx, Ui, Voice }
    public enum PreferenceToggle { VSync, FixTooltipOnlyByClick, MarketAutoLevelUp }
    public enum WindowMode { ExclusiveFullscreen, FullscreenWindow, MaximizedWindow, Windowed }

    public readonly struct ResolutionOption
    {
        public int Width { get; }
        public int Height { get; }
        public int RefreshRate { get; }

        public ResolutionOption(int width, int height, int refreshRate)
        {
            Width = width;
            Height = height;
            RefreshRate = refreshRate;
        }
    }

    public readonly struct LanguageOption
    {
        public string Name { get; }
        public string Code { get; }
        public string DisplayName { get; }

        public LanguageOption(string name, string code, string displayName)
        {
            Name = name;
            Code = code;
            DisplayName = displayName;
        }
    }

    public interface ISettingsRuntime
    {
        public SettingsPack Pack { get; }
        public ResolutionOption AppliedResolution { get; }
        public int AppliedFps { get; }
        public IReadOnlyList<ResolutionOption> Resolutions { get; }
        public IReadOnlyList<LanguageOption> Languages { get; }
        public int CursorCount { get; }
        public int CursorIndex { get; }
        public int TargetFps { get; }
        public int MaximumFps { get; }
        public ResolutionOption Resolution { get; }
        public WindowMode WindowMode { get; }
        public string CurrentLanguage { get; }
        public float GetVolume(VolumeChannel channel);
        public bool GetToggle(PreferenceToggle toggle);
        public void SetVolume(VolumeChannel channel, float value);
        public void SetToggle(PreferenceToggle toggle, bool value);
        public void SetTargetFps(int value);
        public void SetCursor(int index);
        public void SetLanguage(LanguageOption language);
        public void SetDisplay(ResolutionOption resolution, WindowMode mode);
        public void ResetSetting(SettingId setting);
    }

    public interface ISettingsService : ISettingsRuntime
    {
        public event Action Changed;
        public void ResetTab(SettingsTab tab);
        public void ResetAll();
    }
}
