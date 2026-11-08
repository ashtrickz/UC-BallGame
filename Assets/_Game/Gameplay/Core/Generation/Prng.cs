namespace Game.Gameplay.Generation
{
    /// <summary>
    /// SplitMix64. A fixed, local algorithm: the generator owns its stream, so effects, sound or animation
    /// randomness can never shift a level, and the same seed replays on every platform.
    /// </summary>
    public sealed class Prng
    {
        private ulong _state;

        public Prng(ulong seed) { _state = seed; }

        public static ulong Mix(int seed, int stream, int index)
        {
            var x = (ulong)(uint)seed;
            x = (x << 32) ^ (ulong)(uint)stream * 0x9E3779B97F4A7C15UL ^ (ulong)(uint)index * 0xD1B54A32D192ED03UL;
            return Finalize(x + 0x632BE59BD9B4E019UL);
        }

        public ulong NextUlong()
        {
            _state += 0x9E3779B97F4A7C15UL;
            return Finalize(_state);
        }

        /// <summary>Uniform in [0, 1) with 53 random bits.</summary>
        public double NextDouble() => (NextUlong() >> 11) * (1.0 / 9007199254740992.0);

        public double Range(double min, double max) => min + (max - min) * NextDouble();

        /// <summary>Uniform integer in [min, maxInclusive].</summary>
        public int Range(int min, int maxInclusive)
        {
            var span = (ulong)((long)maxInclusive - min + 1);
            return (int)(min + (long)(NextUlong() % span));
        }

        public bool Chance(double probability) => NextDouble() < probability;

        private static ulong Finalize(ulong z)
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
