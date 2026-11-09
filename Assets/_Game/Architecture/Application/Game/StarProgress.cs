using System;
using Slime.Gameplay.Core.Levels;
using UnityEngine;

namespace Slime.App.Game
{
    /// <summary>Best earned stars for an exact level layout, balance and criteria.</summary>
    public interface IStarProgress
    {
        int Best(string levelKey);

        void Record(string levelKey, int stars);
    }

    /// <summary>Local star records; unsuccessful and worse replays never overwrite a better result.</summary>
    public sealed class PlayerPrefsStarProgress : IStarProgress
    {
        private const string Prefix = "stars.v1.";

        public int Best(string levelKey)
        {
            return Math.Max(0, Math.Min(StarCriteria.MaximumStars, PlayerPrefs.GetInt(Prefix + levelKey, 0)));
        }

        public void Record(string levelKey, int stars)
        {
            if (stars <= Best(levelKey))
                return;

            PlayerPrefs.SetInt(Prefix + levelKey, Math.Min(StarCriteria.MaximumStars, stars));
            PlayerPrefs.Save();
        }
    }
}
