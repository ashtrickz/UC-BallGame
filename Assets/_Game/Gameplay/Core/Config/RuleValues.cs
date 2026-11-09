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
        private const double DefaultInitialDiameter = 1.6;
        private const double DefaultCriticalDiameter = 0.45;
        private const double DefaultTransferCoefficient = 0.5;
        private const double DefaultMinShotCost = 0.05;
        private const double DefaultChargeRate = 0.24;
        private const double DefaultBlastCoefficient = 2.2;
        private const double DefaultCorridorGap = 0.08;
        private const double DefaultTolerance = 1e-4;
        private const double DefaultMinAdvance = 0.03;
        private const double DefaultRouteLength = 26;
        private const double DefaultDoorOpenDistance = 5;
        private const int DefaultMaxCandidates = 24;
        private const int DefaultObstacleCountMin = 5;
        private const int DefaultObstacleCountMax = 7;
        private const double DefaultFirstObstacleZ = 3.2;
        private const double DefaultFinishClearance = 4.5;
        private const double DefaultObstacleSpacingMin = 2.4;
        private const double DefaultObstacleSpacingMax = 3.6;
        private const double DefaultSingleWeight = 0.35;
        private const double DefaultPairWeight = 0.2;
        private const double DefaultGroupWeight = 0.45;
        private const int DefaultGroupSizeMin = 4;
        private const int DefaultGroupSizeMax = 7;
        private const double DefaultGroupRadiusMin = 0.55;
        private const double DefaultGroupRadiusMax = 0.95;
        private const double DefaultLateralJitter = 0.25;
        private const double DefaultMemberSpacing = 0.42;
        private const double DefaultScaleMin = 0.85;
        private const double DefaultScaleMax = 1.2;
        private const double DefaultDecorPerMetre = 0.9;
        private const double DefaultDecorLateralMin = 2.1;
        private const double DefaultDecorLateralMax = 4.6;
        private const double DefaultMinSpendFraction = 0.55;
        private const double DefaultControlHoldStep = 0.05;
        private const double DefaultControlHoldMax = 2.5;

        // ---- Size and shots (GDD "Size and shot cost") ----
        public double InitialDiameter; // D0
        public double CriticalDiameter; // Dcrit
        public double TransferCoefficient; // a: cost = a × S
        public double MinShotCost; // cmin
        public double ChargeRate; // cost per second of hold: c(t) = cmin + rate × t
        public double BlastCoefficient; // b: R = b × S
        public double CorridorGap; // clearance on each side of the body
        public double Tolerance; // the single precision tolerance of all rule comparisons
        public double MinAdvance; // shorter advances are treated as "no movement"

        // ---- Route ----
        public double RouteLength; // finish threshold Z; the slime starts at Z = 0
        public double DoorOpenDistance; // doors open when the anchor is this close to the threshold
        public double DoorSetback; // doors stand this far before the threshold

        // ---- Generation ----
        public int MaxCandidates;
        public int ObstacleCountMin;
        public int ObstacleCountMax;
        public double FirstObstacleZ;
        public double FinishClearance; // no obstacles closer than this to the threshold
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
        public double MemberSpacing; // minimum centre distance inside a group
        public double ScaleMin;
        public double ScaleMax;
        public double DecorPerMetre;
        public double DecorLateralMin;
        public double DecorLateralMax;
        public double MinSpendFraction; // preferred lower bound of control spend relative to B / 1.2
        public double ControlHoldStep; // hold ladder the control planner tries, seconds
        public double ControlHoldMax;

        public static RuleValues Defaults => new RuleValues
        {
            InitialDiameter = DefaultInitialDiameter,
            CriticalDiameter = DefaultCriticalDiameter,
            TransferCoefficient = DefaultTransferCoefficient,
            MinShotCost = DefaultMinShotCost,
            ChargeRate = DefaultChargeRate,
            BlastCoefficient = DefaultBlastCoefficient,
            CorridorGap = DefaultCorridorGap,
            Tolerance = DefaultTolerance,
            MinAdvance = DefaultMinAdvance,

            RouteLength = DefaultRouteLength,
            DoorOpenDistance = DefaultDoorOpenDistance,
            DoorSetback = 1,

            MaxCandidates = DefaultMaxCandidates,
            ObstacleCountMin = DefaultObstacleCountMin,
            ObstacleCountMax = DefaultObstacleCountMax,
            FirstObstacleZ = DefaultFirstObstacleZ,
            FinishClearance = DefaultFinishClearance,
            ObstacleSpacingMin = DefaultObstacleSpacingMin,
            ObstacleSpacingMax = DefaultObstacleSpacingMax,
            SingleWeight = DefaultSingleWeight,
            PairWeight = DefaultPairWeight,
            GroupWeight = DefaultGroupWeight,
            GroupSizeMin = DefaultGroupSizeMin,
            GroupSizeMax = DefaultGroupSizeMax,
            GroupRadiusMin = DefaultGroupRadiusMin,
            GroupRadiusMax = DefaultGroupRadiusMax,
            LateralJitter = DefaultLateralJitter,
            MemberSpacing = DefaultMemberSpacing,
            ScaleMin = DefaultScaleMin,
            ScaleMax = DefaultScaleMax,
            DecorPerMetre = DefaultDecorPerMetre,
            DecorLateralMin = DefaultDecorLateralMin,
            DecorLateralMax = DefaultDecorLateralMax,
            MinSpendFraction = DefaultMinSpendFraction,
            ControlHoldStep = DefaultControlHoldStep,
            ControlHoldMax = DefaultControlHoldMax,
        };
    }
}
