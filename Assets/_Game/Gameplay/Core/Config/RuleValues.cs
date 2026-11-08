using System;

namespace Game.Gameplay.Config
{
    /// <summary>
    /// Balance and generation numbers. Serialized by <see cref="SlimeGameConfig"/>; copied by value into an
    /// immutable <see cref="GameRules"/> snapshot when an attempt starts. Units: metres and seconds.
    /// </summary>
    [Serializable]
    public struct RuleValues
    {
        // ---- Size and shots (GDD "Size and shot cost") ----
        public double InitialDiameter;      // D0
        public double CriticalDiameter;     // Dcrit
        public double TransferCoefficient;  // a: cost = a × S
        public double MinShotCost;          // cmin
        public double ChargeRate;           // cost per second of hold: c(t) = cmin + rate × t
        public double BlastCoefficient;     // b: R = b × S
        public double CorridorGap;          // clearance on each side of the body
        public double Tolerance;            // the single precision tolerance of all rule comparisons
        public double MinAdvance;           // shorter advances are treated as "no movement"

        // ---- Route ----
        public double RouteLength;          // finish threshold Z; the slime starts at Z = 0
        public double DoorOpenDistance;     // doors open when the anchor is this close to the threshold
        public double DoorSetback;          // doors stand this far before the threshold

        // ---- Generation ----
        public int MaxCandidates;
        public int ObstacleCountMin;
        public int ObstacleCountMax;
        public double FirstObstacleZ;
        public double FinishClearance;      // no obstacles closer than this to the threshold
        public double ObstacleSpacingMin;
        public double ObstacleSpacingMax;
        public double SingleWeight;
        public double PairWeight;
        public double GroupWeight;
        public int GroupSizeMin;
        public int GroupSizeMax;
        public double GroupRadiusMin;
        public double GroupRadiusMax;
        public double LateralJitter;
        public double MemberSpacing;        // minimum centre distance inside a group
        public double ScaleMin;
        public double ScaleMax;
        public double DecorPerMetre;
        public double DecorLateralMin;
        public double DecorLateralMax;
        public double MinSpendFraction;     // preferred lower bound of control spend relative to B / 1.2
        public double ControlHoldStep;      // hold ladder the control planner tries, seconds
        public double ControlHoldMax;

        public static RuleValues Defaults => new RuleValues
        {
            InitialDiameter = 1.6,
            CriticalDiameter = 0.45,
            TransferCoefficient = 0.5,
            MinShotCost = 0.05,
            ChargeRate = 0.24,
            BlastCoefficient = 2.2,
            CorridorGap = 0.08,
            Tolerance = 1e-4,
            MinAdvance = 0.03,

            RouteLength = 26,
            DoorOpenDistance = 5,
            DoorSetback = 1,

            MaxCandidates = 24,
            ObstacleCountMin = 5,
            ObstacleCountMax = 7,
            FirstObstacleZ = 3.2,
            FinishClearance = 4.5,
            ObstacleSpacingMin = 2.4,
            ObstacleSpacingMax = 3.6,
            SingleWeight = 0.35,
            PairWeight = 0.2,
            GroupWeight = 0.45,
            GroupSizeMin = 4,
            GroupSizeMax = 7,
            GroupRadiusMin = 0.55,
            GroupRadiusMax = 0.95,
            LateralJitter = 0.25,
            MemberSpacing = 0.42,
            ScaleMin = 0.85,
            ScaleMax = 1.2,
            DecorPerMetre = 0.9,
            DecorLateralMin = 2.1,
            DecorLateralMax = 4.6,
            MinSpendFraction = 0.55,
            ControlHoldStep = 0.05,
            ControlHoldMax = 2.5,
        };
    }
}
