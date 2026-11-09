using System;

namespace Game.Gameplay.Config
{
    /// <summary>
    /// Settings of the "Wall" mode: the whole field is filled with crystals; the core band around the route is the
    /// part that blocks the slime, and its fill adapts to the spendable budget. Units: metres.
    /// </summary>
    [Serializable]
    public struct WallValues
    {
        private const int BytesPerValue = 8;
        private const int BitsPerByte = 8;
        private const ulong ByteMask = 0xFF;
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;
        private const double DefaultRouteLength = 14;
        private const double DefaultWallStartZ = 2.6;
        private const double DefaultWallEndBeforeDoor = 4.2;
        private const double DefaultFieldHalfWidth = 4.6;
        private const double DefaultSpacing = 0.62;
        private const double DefaultJitter = 0.32;
        private const double DefaultCoreHalfWidth = 1.45;
        private const double DefaultCoreFillMin = 0.55;
        private const double DefaultCoreFillMax = 0.95;
        private const double DefaultSideFill = 0.88;
        private const double DefaultScaleMin = 0.78;
        private const double DefaultScaleMax = 1.1;
        private const int DefaultMaxCandidates = 16;

        public double RouteLength;
        public double WallStartZ; // first row of the blocking wall
        public double WallEndBeforeDoor; // the blocking wall ends this far before the doors
        public double FieldHalfWidth; // crystals cover the frame up to this lateral distance
        public double Spacing; // grid cell; one crystal per cell at most
        public double Jitter; // share of the cell a crystal may move
        public double CoreHalfWidth; // band whose crystals can block the corridor
        public double CoreFillMin;
        public double CoreFillMax;
        public double SideFill;
        public double ScaleMin;
        public double ScaleMax;
        public int MaxCandidates;
        public int StarExtraShots;
        public double StarMassAllowancePercent;

        public static WallValues Defaults => new WallValues
        {
            RouteLength = DefaultRouteLength,
            WallStartZ = DefaultWallStartZ,
            WallEndBeforeDoor = DefaultWallEndBeforeDoor,
            FieldHalfWidth = DefaultFieldHalfWidth,
            Spacing = DefaultSpacing,
            Jitter = DefaultJitter,
            CoreHalfWidth = DefaultCoreHalfWidth,
            CoreFillMin = DefaultCoreFillMin,
            CoreFillMax = DefaultCoreFillMax,
            SideFill = DefaultSideFill,
            ScaleMin = DefaultScaleMin,
            ScaleMax = DefaultScaleMax,
            MaxCandidates = DefaultMaxCandidates,
            StarExtraShots = 0,
            StarMassAllowancePercent = 5,
        };

        public string Fingerprint()
        {
            var hash = FnvOffset;
            foreach (var v in new[] { RouteLength, WallStartZ, WallEndBeforeDoor, FieldHalfWidth, Spacing, Jitter, CoreHalfWidth,
                         CoreFillMin, CoreFillMax, SideFill, ScaleMin, ScaleMax, MaxCandidates })
            {
                var bits = (ulong)BitConverter.DoubleToInt64Bits(v);
                for (var i = 0; i < BytesPerValue; i++)
                {
                    hash ^= (bits >> (i * BitsPerByte)) & ByteMask;
                    hash *= FnvPrime;
                }
            }
            return hash.ToString("x16");
        }
    }
}
