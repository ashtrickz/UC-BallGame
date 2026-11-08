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
        public double RouteLength;
        public double WallStartZ;        // first row of the blocking wall
        public double WallEndBeforeDoor; // the blocking wall ends this far before the doors
        public double FieldHalfWidth;    // crystals cover the frame up to this lateral distance
        public double Spacing;           // grid cell; one crystal per cell at most
        public double Jitter;            // share of the cell a crystal may move
        public double CoreHalfWidth;     // band whose crystals can block the corridor
        public double CoreFillMin;
        public double CoreFillMax;
        public double SideFill;
        public double ScaleMin;
        public double ScaleMax;
        public int MaxCandidates;

        public static WallValues Defaults => new WallValues
        {
            RouteLength = 14,
            WallStartZ = 2.6,
            WallEndBeforeDoor = 4.2,
            FieldHalfWidth = 4.6,
            Spacing = 0.62,
            Jitter = 0.32,
            CoreHalfWidth = 1.45,
            CoreFillMin = 0.55,
            CoreFillMax = 0.95,
            SideFill = 0.88,
            ScaleMin = 0.78,
            ScaleMax = 1.1,
            MaxCandidates = 16,
        };

        public string Fingerprint()
        {
            var hash = 14695981039346656037UL;
            foreach (var v in new[] { RouteLength, WallStartZ, WallEndBeforeDoor, FieldHalfWidth, Spacing, Jitter, CoreHalfWidth,
                         CoreFillMin, CoreFillMax, SideFill, ScaleMin, ScaleMax, MaxCandidates })
            {
                var bits = (ulong)BitConverter.DoubleToInt64Bits(v);
                for (var i = 0; i < 8; i++) { hash ^= (bits >> (i * 8)) & 0xFF; hash *= 1099511628211UL; }
            }
            return hash.ToString("x16");
        }
    }
}
