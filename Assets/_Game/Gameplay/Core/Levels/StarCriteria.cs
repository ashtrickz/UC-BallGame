using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Game.Gameplay.Level;

namespace Game.Gameplay.Levels
{
    /// <summary>Independent bonus stars: finish within the shot limit and retain the required initial mass percentage.</summary>
    public readonly struct StarCriteria
    {
        public const int MaximumStars = 3;
        public const double Percent = 100;
        private const double ComparisonTolerance = 1e-7;
        private const double DefaultMassAllowance = 5;

        public StarCriteria(int maxShots, double minRemainingMassPercent)
        {
            if (maxShots < 0)
                throw new ArgumentOutOfRangeException(nameof(maxShots));

            if (double.IsNaN(minRemainingMassPercent) || minRemainingMassPercent < 0 || minRemainingMassPercent > Percent)
                throw new ArgumentOutOfRangeException(nameof(minRemainingMassPercent));

            MaxShots = maxShots;
            MinRemainingMassPercent = minRemainingMassPercent;
        }

        public int MaxShots { get; }

        public double MinRemainingMassPercent { get; }

        public int Evaluate(bool won, int shots, double remainingMassPercent)
        {
            if (!won)
                return 0;

            var stars = 1;
            if (shots <= MaxShots)
                stars++;

            if (remainingMassPercent + ComparisonTolerance >= MinRemainingMassPercent)
                stars++;

            return stars;
        }

        public static double RemainingMassPercent(double diameter, double initialDiameter)
        {
            return Math.Max(0, Math.Min(1, diameter / initialDiameter)) * Percent;
        }

        public static StarCriteria FromControl(LevelData level, double initialDiameter, int extraShots = 0,
            double massAllowancePercent = DefaultMassAllowance)
        {
            var control = level.Control ?? throw new ArgumentException("A rated level requires a proven control plan.", nameof(level));
            var percent = RemainingMassPercent(control.FinalDiameter, initialDiameter);

            return new StarCriteria(control.HoldSeconds.Count + extraShots, Math.Max(0, percent - massAllowancePercent));
        }

        public string ResultKey(string mode, LevelData level)
        {
            var culture = CultureInfo.InvariantCulture;
            var text = new StringBuilder(mode).Append('|').Append(level.Seed).Append('|').Append(level.GeneratorVersion)
                .Append('|').Append(level.RulesFingerprint).Append('|').Append(MaxShots)
                .Append('|').Append(MinRemainingMassPercent.ToString("R", culture));
            foreach (var crystal in level.Crystals)
            {
                text.Append('|').Append(crystal.VariantId).Append(':').Append(crystal.RotationStep)
                    .Append(':').Append(crystal.Scale.ToString("R", culture))
                    .Append(':').Append(crystal.Position.X.ToString("R", culture))
                    .Append(':').Append(crystal.Position.Z.ToString("R", culture));
            }

            using var hash = SHA256.Create();

            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
        }
    }
}
