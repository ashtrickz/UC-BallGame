using System;
using System.Collections.Generic;

namespace Game.Settings
{
    public sealed class SettingsService : ISettingsService
    {
        public event Action Changed;

        public SettingsPack Pack => _runtime.Pack;
        public ResolutionOption AppliedResolution => _runtime.AppliedResolution;
        public int AppliedFps => _runtime.AppliedFps;

        public IReadOnlyList<ResolutionOption> Resolutions => _runtime.Resolutions;
        public IReadOnlyList<LanguageOption> Languages => _runtime.Languages;
        public int CursorCount => _runtime.CursorCount;
        public int CursorIndex => _runtime.CursorIndex;
        public int TargetFps => _runtime.TargetFps;
        public int MaximumFps => _runtime.MaximumFps;
        public ResolutionOption Resolution => _runtime.Resolution;
        public WindowMode WindowMode => _runtime.WindowMode;
        public string CurrentLanguage => _runtime.CurrentLanguage;

        private readonly ISettingsRuntime _runtime;

        public SettingsService(ISettingsRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public float GetVolume(VolumeChannel channel) => _runtime.GetVolume(channel);
        public bool GetToggle(PreferenceToggle toggle) => _runtime.GetToggle(toggle);

        public void SetVolume(VolumeChannel channel, float value)
        {
            if (!Pack.Contains(SettingsPack.VolumeSetting(channel))) return;
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            value = Math.Max(0f, Math.Min(1f, value));
            _runtime.SetVolume(channel, value);
            Changed?.Invoke();
        }

        public void SetToggle(PreferenceToggle toggle, bool value)
        {
            if (!Pack.Contains(SettingsPack.ToggleSetting(toggle))) return;
            _runtime.SetToggle(toggle, value);
            Changed?.Invoke();
        }

        public void SetTargetFps(int value)
        {
            if (!Pack.Contains(SettingId.TargetFps)) return;
            value = Math.Max(Pack.MinimumFps, Math.Min(Pack.MaximumFps, value));
            _runtime.SetTargetFps(value);
            Changed?.Invoke();
        }

        public void SetCursor(int index)
        {
            if (!Pack.Contains(SettingId.Cursor) || index < 0 || index >= CursorCount) return;
            _runtime.SetCursor(index);
            Changed?.Invoke();
        }

        public void SetLanguage(LanguageOption language)
        {
            if (!Pack.Contains(SettingId.Language)) return;
            foreach (var supported in Languages)
            {
                if (supported.Name != language.Name) continue;
                _runtime.SetLanguage(supported);
                Changed?.Invoke();
                return;
            }
        }

        public void SetDisplay(ResolutionOption resolution, WindowMode mode)
        {
            if (!Pack.Contains(SettingId.Resolution) && !Pack.Contains(SettingId.WindowMode)) return;
            if (!Pack.Contains(SettingId.Resolution)) resolution = Resolution;
            if (!Pack.Contains(SettingId.WindowMode)) mode = WindowMode;
            if (Pack.Contains(SettingId.WindowMode) && !Enum.IsDefined(typeof(WindowMode), mode)) return;
            if (Pack.Contains(SettingId.Resolution) && (resolution.Width < 640 || resolution.Height < 480)) return;
            _runtime.SetDisplay(resolution, mode);
            Changed?.Invoke();
        }

        public void ResetSetting(SettingId setting)
        {
            if (!Pack.Contains(setting)) return;
            _runtime.ResetSetting(setting);
            Changed?.Invoke();
        }

        public void ResetTab(SettingsTab tab)
        {
            foreach (var definition in Pack.Tabs)
            {
                if (definition.Tab != tab) continue;
                foreach (var setting in definition.Settings) _runtime.ResetSetting(setting);
                Changed?.Invoke();
                return;
            }
        }

        public void ResetAll()
        {
            foreach (var tab in Pack.Tabs)
                foreach (var setting in tab.Settings) _runtime.ResetSetting(setting);
            Changed?.Invoke();
        }
    }
}
