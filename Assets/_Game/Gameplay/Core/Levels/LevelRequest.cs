using System;

namespace Game.Gameplay.Levels
{
    /// <summary>What the player asked to play: the level type and its parameters, without how it is built.</summary>
    public abstract class LevelRequest
    {
    }

    /// <summary>A fixed campaign level by its 1-based number.</summary>
    public sealed class CampaignLevelRequest : LevelRequest
    {
        private const int FirstLevel = 1;

        public CampaignLevelRequest(int number)
        {
            if (number < FirstLevel)
                throw new ArgumentOutOfRangeException(nameof(number));

            Number = number;
        }

        public int Number { get; }
    }

    /// <summary>A generated crystal wall; without a seed a new one is drawn.</summary>
    public sealed class WallLevelRequest : LevelRequest
    {
        public WallLevelRequest(int? seed)
        {
            Seed = seed;
        }

        public int? Seed { get; }
    }

    /// <summary>A generated obstacle course; without a seed a new one is drawn.</summary>
    public sealed class EndlessLevelRequest : LevelRequest
    {
        public EndlessLevelRequest(int? seed)
        {
            Seed = seed;
        }

        public int? Seed { get; }
    }
}
