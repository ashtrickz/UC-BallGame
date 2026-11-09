using System;

namespace Game.Gameplay.Levels
{
    /// <summary>Source of seeds for generated levels.</summary>
    public interface ISeedProvider
    {
        int Next();
    }

    /// <summary>
    /// Draws a new positive seed for every generated level, or always returns a fixed debug seed when one is set.
    /// </summary>
    public sealed class SeedProvider : ISeedProvider
    {
        private const int NoFixedSeed = 0;
        private const int MinSeed = 1;

        private readonly int _fixedSeed;
        private readonly Random _random;

        public SeedProvider(int fixedSeed, int randomSeed)
        {
            _fixedSeed = fixedSeed;
            _random = new Random(randomSeed);
        }

        public int Next()
        {
            if (_fixedSeed != NoFixedSeed)
                return _fixedSeed;

            return _random.Next(MinSeed, int.MaxValue);
        }
    }
}
