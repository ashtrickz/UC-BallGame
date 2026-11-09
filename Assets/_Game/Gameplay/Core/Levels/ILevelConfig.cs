using Game.Gameplay.Config;
using Game.Gameplay.Level;

namespace Game.Gameplay.Levels
{
    /// <summary>What the level sources need from the game configuration, without depending on Unity assets.</summary>
    public interface ILevelConfig
    {
        WallValues Wall { get; }

        int CampaignCount { get; }

        GameRules CreateSnapshot();

        CampaignLevelSpec CampaignSpec(int index);
    }
}
