using System;
using System.Collections.Generic;

namespace Game.Settings
{
    public sealed class SettingsPresentation
    {
        public SettingsPack Pack { get; }
        public SettingsTab ActiveTab { get; }
        public ResolutionOption AppliedResolution { get; }
        public int AppliedFps { get; }
        public ResolutionOption Resolution { get; }
        public bool HasResolutions { get; }
        public WindowMode WindowMode { get; }
        public string Language { get; }
        public bool HasLanguages { get; }
        public int CursorIndex { get; }
        public int CursorCount { get; }
        public int TargetFps { get; }
        public int MaximumFps { get; }
        public IReadOnlyDictionary<VolumeChannel, float> Volumes { get; }
        public IReadOnlyDictionary<PreferenceToggle, bool> Toggles { get; }
        public IReadOnlyList<ResolutionOption> Resolutions { get; }
        public IReadOnlyList<LanguageOption> Languages { get; }
        public string CurrentLanguageName { get; }

        public SettingsPresentation(ResolutionOption resolution, bool hasResolutions, WindowMode windowMode,
            string language, bool hasLanguages, int cursorIndex, int cursorCount, int targetFps, int maximumFps,
            IReadOnlyDictionary<VolumeChannel, float> volumes, IReadOnlyDictionary<PreferenceToggle, bool> toggles,
            IReadOnlyList<ResolutionOption> resolutions = null, IReadOnlyList<LanguageOption> languages = null,
            string currentLanguageName = null, SettingsPack pack = null,
            SettingsTab activeTab = SettingsTab.Video, ResolutionOption? appliedResolution = null,
            int? appliedFps = null)
        {
            Pack = pack ?? SettingsPack.Desktop;
            ActiveTab = activeTab;
            AppliedResolution = appliedResolution ?? resolution;
            AppliedFps = appliedFps ?? targetFps;
            Resolution = resolution;
            HasResolutions = hasResolutions;
            WindowMode = windowMode;
            Language = language;
            HasLanguages = hasLanguages;
            CursorIndex = cursorIndex;
            CursorCount = cursorCount;
            TargetFps = targetFps;
            MaximumFps = maximumFps;
            Volumes = volumes;
            Toggles = toggles;
            Resolutions = resolutions ?? Array.Empty<ResolutionOption>();
            Languages = languages ?? Array.Empty<LanguageOption>();
            CurrentLanguageName = currentLanguageName ?? language;
        }
    }
}
