using System;
using System.Collections.Generic;

namespace Game.Settings
{
    public enum SettingsTab { Video, Audio, Game, Controls }
    public enum SettingId
    {
        Resolution, WindowMode, VSync, TargetFps, Cursor, Language,
        MasterVolume, MusicVolume, SfxVolume, UiVolume, VoiceVolume,
        FixTooltipOnlyByClick, MarketAutoLevelUp
    }

    public sealed class SettingsTabDefinition
    {
        public SettingsTab Tab { get; }
        public IReadOnlyList<SettingId> Settings { get; }

        public SettingsTabDefinition(SettingsTab tab, params SettingId[] settings)
        {
            Tab = tab;
            Settings = Array.AsReadOnly((SettingId[])(settings ?? Array.Empty<SettingId>()).Clone());
        }
    }

    public sealed class SettingsPack
    {
        public IReadOnlyList<SettingsTabDefinition> Tabs { get; }
        public int MinimumFps { get; }
        public int MaximumFps { get; }
        /// <summary>The frame rate until the player picks another: 60, or the pack's nearest limit. The highest rate only heats a phone in an game.</summary>
        public int DefaultFps => Math.Max(MinimumFps, Math.Min(MaximumFps, 60));
        private readonly HashSet<SettingId> _settings = new HashSet<SettingId>();

        public SettingsPack(int minimumFps, int maximumFps, params SettingsTabDefinition[] tabs)
        {
            if (minimumFps < 30 || maximumFps < minimumFps)
                throw new ArgumentOutOfRangeException(nameof(maximumFps));
            MinimumFps = minimumFps;
            MaximumFps = maximumFps;
            var tabIds = new HashSet<SettingsTab>();
            var visibleTabs = new List<SettingsTabDefinition>();
            foreach (var tab in tabs ?? Array.Empty<SettingsTabDefinition>())
            {
                if (tab == null || tab.Settings.Count == 0) continue;
                if (!Enum.IsDefined(typeof(SettingsTab), tab.Tab) || !tabIds.Add(tab.Tab))
                    throw new ArgumentException("Invalid or duplicate settings tab.", nameof(tabs));
                foreach (var setting in tab.Settings)
                    if (!Enum.IsDefined(typeof(SettingId), setting) || !_settings.Add(setting))
                        throw new ArgumentException("Invalid or duplicate setting in pack.", nameof(tabs));
                visibleTabs.Add(tab);
            }
            Tabs = visibleTabs.AsReadOnly();
        }

        public bool Contains(SettingId setting) => _settings.Contains(setting);

        public bool Contains(SettingsTab tab)
        {
            foreach (var definition in Tabs)
                if (definition.Tab == tab) return true;
            return false;
        }

        public static SettingId VolumeSetting(VolumeChannel channel)
        {
            if (!Enum.IsDefined(typeof(VolumeChannel), channel)) throw new ArgumentOutOfRangeException(nameof(channel));
            return (SettingId)((int)SettingId.MasterVolume + (int)channel);
        }

        public static SettingId ToggleSetting(PreferenceToggle toggle)
        {
            switch (toggle)
            {
                case PreferenceToggle.VSync: return SettingId.VSync;
                case PreferenceToggle.FixTooltipOnlyByClick: return SettingId.FixTooltipOnlyByClick;
                case PreferenceToggle.MarketAutoLevelUp: return SettingId.MarketAutoLevelUp;
                default: throw new ArgumentOutOfRangeException(nameof(toggle));
            }
        }

        public static SettingsPack Desktop { get; } = new SettingsPack(30, 240,
            new SettingsTabDefinition(SettingsTab.Video, SettingId.Resolution, SettingId.WindowMode, SettingId.VSync, SettingId.TargetFps),
            new SettingsTabDefinition(SettingsTab.Audio, SettingId.MasterVolume, SettingId.MusicVolume, SettingId.SfxVolume, SettingId.UiVolume, SettingId.VoiceVolume),
            new SettingsTabDefinition(SettingsTab.Game, SettingId.Language),
            new SettingsTabDefinition(SettingsTab.Controls, SettingId.Cursor));

        public static SettingsPack Mobile { get; } = new SettingsPack(30, 120,
            new SettingsTabDefinition(SettingsTab.Video, SettingId.TargetFps),
            new SettingsTabDefinition(SettingsTab.Audio, SettingId.MasterVolume, SettingId.MusicVolume, SettingId.SfxVolume, SettingId.UiVolume, SettingId.VoiceVolume),
            new SettingsTabDefinition(SettingsTab.Game, SettingId.Language));
    }
}
