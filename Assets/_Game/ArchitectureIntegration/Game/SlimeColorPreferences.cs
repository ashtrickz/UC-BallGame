using Game.Gameplay.Config;
using UnityEngine;

namespace Game.Integration.Game
{
    /// <summary>Stores a palette ID, so config reordering does not change the player's choice.</summary>
    public static class SlimeColorPreferences
    {
        public const string Key = "Slime.ColorPreset";

        public static int SelectedIndex(SlimeGameConfig config)
        {
            if (config == null || config.SlimeColors.Count == 0) return -1;
            var id = PlayerPrefs.GetString(Key, string.Empty);
            for (var i = 0; i < config.SlimeColors.Count; i++)
                if (config.SlimeColors[i].Id == id) return i;
            return 0;
        }

        public static bool Select(SlimeGameConfig config, int index)
        {
            if (config == null || index < 0 || index >= config.SlimeColors.Count) return false;
            var id = config.SlimeColors[index].Id;
            if (string.IsNullOrWhiteSpace(id)) return false;
            PlayerPrefs.SetString(Key, id);
            PlayerPrefs.Save();
            return true;
        }
    }
}
