using System;
using System.Collections.Generic;
using Game.Gameplay.Config;
using UnityEngine;

namespace Game.App.Game
{
    /// <summary>Highest playable campaign level; the level count + 1 means the campaign is complete.</summary>
    public interface ICampaignProgress
    {
        int Unlocked { get; }

        void MarkCompleted(int level, int levelCount);

        void Reset();
    }

    /// <summary>The player's slime colour as a palette id, so reordering the palette keeps the choice.</summary>
    public interface ISlimeColorPreferences
    {
        int SelectedIndex(IReadOnlyList<SlimeColorPreset> palette);

        bool Select(IReadOnlyList<SlimeColorPreset> palette, int index);
    }

    /// <summary>Stores the unlocked level number between launches.</summary>
    public sealed class PlayerPrefsCampaignProgress : ICampaignProgress
    {
        public const string Key = "campaign.unlocked";

        private const int FirstLevel = 1;

        public int Unlocked => Math.Max(FirstLevel, PlayerPrefs.GetInt(Key, FirstLevel));

        public void MarkCompleted(int level, int levelCount)
        {
            var next = Math.Min(level + 1, levelCount + 1);
            if (next <= Unlocked)
                return;

            PlayerPrefs.SetInt(Key, next);
            PlayerPrefs.Save();
        }

        public void Reset()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Stores the selected palette id between launches; an unknown id falls back to the first colour.</summary>
    public sealed class PlayerPrefsSlimeColorPreferences : ISlimeColorPreferences
    {
        public const string Key = "Slime.ColorPreset";

        private const int NoColor = -1;
        private const int FirstColor = 0;

        public int SelectedIndex(IReadOnlyList<SlimeColorPreset> palette)
        {
            if (palette == null || palette.Count == 0)
                return NoColor;

            var id = PlayerPrefs.GetString(Key, string.Empty);
            for (var i = 0; i < palette.Count; i++)
            {
                if (palette[i].Id == id)
                    return i;
            }

            return FirstColor;
        }

        public bool Select(IReadOnlyList<SlimeColorPreset> palette, int index)
        {
            if (palette == null || index < 0 || index >= palette.Count)
                return false;

            var id = palette[index].Id;
            if (string.IsNullOrWhiteSpace(id))
                return false;

            PlayerPrefs.SetString(Key, id);
            PlayerPrefs.Save();

            return true;
        }
    }
}
