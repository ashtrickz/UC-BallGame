namespace Game.Gameplay.Generation
{
    /// <summary>
    /// SplitMix64. A fixed, local algorithm: the generator owns its stream, so effects, sound or animation
    /// randomness can never shift a level, and the same seed replays on every platform.
    /// </summary>
    public sealed class Prng
    {
        private const int HalfBits = 32;
        private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;
        private const ulong IndexGamma = 0xD1B54A32D192ED03UL;
        private const ulong MixOffset = 0x632BE59BD9B4E019UL;
        private const int MantissaShift = 11;
        private const double MantissaRange = 9007199254740992.0;
        private const int MixShiftA = 30;
        private const int MixShiftB = 27;
        private const int MixShiftC = 31;
        private const ulong MixMultiplierA = 0xBF58476D1CE4E5B9UL;
        private const ulong MixMultiplierB = 0x94D049BB133111EBUL;

        private ulong _state;

        public Prng(ulong seed)
        {
            _state = seed;
        }

        public static ulong Mix(int seed, int stream, int index)
        {
            var x = (ulong)(uint)seed;
            x = (x << HalfBits) ^ (ulong)(uint)stream * GoldenGamma ^ (ulong)(uint)index * IndexGamma;
            return Finalize(x + MixOffset);
        }

        public ulong NextUlong()
        {
            _state += GoldenGamma;
            return Finalize(_state);
        }

        public double NextDouble() => (NextUlong() >> MantissaShift) * (1.0 / MantissaRange);

        public double Range(double min, double max) => min + (max - min) * NextDouble();

        public int Range(int min, int maxInclusive)
        {
            var span = (ulong)((long)maxInclusive - min + 1);
            return (int)(min + (long)(NextUlong() % span));
        }

        public bool Chance(double probability) => NextDouble() < probability;

        private static ulong Finalize(ulong z)
        {
            z = (z ^ (z >> MixShiftA)) * MixMultiplierA;
            z = (z ^ (z >> MixShiftB)) * MixMultiplierB;

            return z ^ (z >> MixShiftC);
        }
    }
}
