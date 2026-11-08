using System;
using UnityEngine;

namespace Game.Integration.Game
{
    /// <summary>Highest playable campaign level; <see cref="LevelCount"/> + 1 means the campaign is complete.</summary>
    public interface ICampaignProgress
    {
        int Unlocked { get; }
        void MarkCompleted(int level, int levelCount);
        void Reset();
    }

    /// <summary>Stores the unlocked level number between launches (PlayerPrefs).</summary>
    public sealed class PlayerPrefsCampaignProgress : ICampaignProgress
    {
        public const string Key = "campaign.unlocked";

        public int Unlocked => Math.Max(1, PlayerPrefs.GetInt(Key, 1));

        public void MarkCompleted(int level, int levelCount)
        {
            var next = Math.Min(level + 1, levelCount + 1);
            if (next <= Unlocked) return;
            PlayerPrefs.SetInt(Key, next);
            PlayerPrefs.Save();
        }

        public void Reset()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
