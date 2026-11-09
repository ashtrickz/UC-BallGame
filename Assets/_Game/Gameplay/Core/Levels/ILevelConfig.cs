using Slime.Gameplay.Core.Config;
using Slime.Gameplay.Core.Level;

namespace Slime.Gameplay.Core.Levels
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
