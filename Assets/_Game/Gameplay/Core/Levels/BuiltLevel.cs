using System;
using Game.Gameplay.Config;
using Game.Gameplay.Level;

namespace Game.Gameplay.Levels
{
    /// <summary>A playable level produced for a request: geometry, the rule snapshot it was proven with and its seed.</summary>
    public sealed class BuiltLevel
    {
        public BuiltLevel(LevelRequest request, LevelData level, GameRules rules, int seed)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Level = level ?? throw new ArgumentNullException(nameof(level));
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Seed = seed;
        }

        public LevelRequest Request { get; }

        public LevelData Level { get; }

        public GameRules Rules { get; }

        public int Seed { get; }
    }
}
