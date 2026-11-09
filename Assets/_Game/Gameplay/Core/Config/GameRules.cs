using System;
using System.Collections.Generic;
using Game.Gameplay.Geometry;

namespace Game.Gameplay.Config
{
    /// <summary>A crystal variant as the rules see it: a stable id and its baked local XZ hull.</summary>
    public sealed class CrystalVariantData
    {
        public int Id { get; }
        public string Name { get; }
        public ConvexPolygon Hull { get; }

        public CrystalVariantData(int id, string name, ConvexPolygon hull)
        {
            Id = id;
            Name = name ?? "";
            Hull = hull ?? throw new ArgumentNullException(nameof(hull));
        }
    }

    public sealed class InvalidRulesException : Exception
    {
        public IReadOnlyList<string> Problems { get; }
        public InvalidRulesException(IReadOnlyList<string> problems)
            : base("Invalid game rules: " + string.Join("; ", problems)) => Problems = problems;
    }

    /// <summary>
    /// Immutable per-attempt snapshot of the game rules and crystal definitions.
    /// Editing the source config after the snapshot is taken never changes a running attempt.
    /// </summary>
    public sealed class GameRules
    {
        public const double BudgetReserve = 1.2;

        private const double ShortRouteShare = 0.25;
        private const double HalfDiameter = 0.5;
        private const int MinGroupSize = 2;
        private const int BytesPerValue = 8;
        private const int BitsPerByte = 8;
        private const ulong ByteMask = 0xFF;
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        private readonly RuleValues _values;
        private readonly CrystalVariantData[] _variants;

        public RuleValues Values => _values;
        public IReadOnlyList<CrystalVariantData> Variants => _variants;
        public string Fingerprint { get; }

        public double D0 => _values.InitialDiameter;
        public double Dcrit => _values.CriticalDiameter;
        public double Tolerance => _values.Tolerance;
        public double Budget => _values.InitialDiameter - _values.CriticalDiameter;
        public double ControlBudgetLimit => Budget / BudgetReserve;
        public double FinishZ => _values.RouteLength;
        public double DoorZ => _values.RouteLength - _values.DoorSetback;

        public GameRules(RuleValues values, IReadOnlyList<CrystalVariantData> variants)
        {
            _values = values;
            _variants = variants == null ? Array.Empty<CrystalVariantData>() : new List<CrystalVariantData>(variants).ToArray();
            var problems = Validate(_values, _variants);
            if (problems.Count > 0)
                throw new InvalidRulesException(problems);

            Fingerprint = ComputeFingerprint(_values, _variants);
        }

        public GameRules WithRouteLength(double routeLength)
        {
            if (routeLength == _values.RouteLength)
                return this;

            var values = _values;
            values.RouteLength = routeLength;
            values.FirstObstacleZ = Math.Min(values.FirstObstacleZ, routeLength * ShortRouteShare);
            values.FinishClearance = Math.Min(values.FinishClearance, routeLength * ShortRouteShare);
            return new GameRules(values, _variants);
        }

        public double ChargeCost(double holdSeconds) => _values.MinShotCost + _values.ChargeRate * Math.Max(0, holdSeconds);
        public double ProjectileDiameter(double cost) => cost / _values.TransferCoefficient;
        public double BlastRadius(double cost) => _values.BlastCoefficient * ProjectileDiameter(cost);
        public double CorridorHalfWidth(double diameter) => diameter * HalfDiameter + _values.CorridorGap;
        public bool CanAffordMinShot(double diameter) => diameter - _values.MinShotCost > _values.CriticalDiameter + _values.Tolerance;
        public bool IsCritical(double diameter) => diameter <= _values.CriticalDiameter + _values.Tolerance;

        public static List<string> Validate(RuleValues v, IReadOnlyList<CrystalVariantData> variants)
        {
            var problems = new List<string>();
            void Positive(string name, double value)
            {
                if (!(value > 0) || double.IsInfinity(value))
                    problems.Add(name + " must be finite and positive");
            }
            void NonNegative(string name, double value)
            {
                if (!(value >= 0) || double.IsInfinity(value))
                    problems.Add(name + " must be finite and non-negative");
            }
            Positive(nameof(v.InitialDiameter), v.InitialDiameter);
            Positive(nameof(v.CriticalDiameter), v.CriticalDiameter);
            Positive(nameof(v.TransferCoefficient), v.TransferCoefficient);
            Positive(nameof(v.MinShotCost), v.MinShotCost);
            Positive(nameof(v.ChargeRate), v.ChargeRate);
            Positive(nameof(v.BlastCoefficient), v.BlastCoefficient);
            NonNegative(nameof(v.CorridorGap), v.CorridorGap);
            Positive(nameof(v.Tolerance), v.Tolerance);
            NonNegative(nameof(v.MinAdvance), v.MinAdvance);
            Positive(nameof(v.RouteLength), v.RouteLength);
            Positive(nameof(v.DoorOpenDistance), v.DoorOpenDistance);
            NonNegative(nameof(v.DoorSetback), v.DoorSetback);
            if (!(v.InitialDiameter > v.CriticalDiameter))
                problems.Add("D0 must be greater than Dcrit");
            var limit = (v.InitialDiameter - v.CriticalDiameter) / BudgetReserve;
            if (!(v.MinShotCost < limit))
                problems.Add("Budget B / 1.2 must exceed the minimum shot cost for a control shot");
            if (v.MaxCandidates < 1)
                problems.Add("MaxCandidates must be at least 1");
            if (v.ObstacleCountMin < 1 || v.ObstacleCountMax < v.ObstacleCountMin)
                problems.Add("Obstacle count range is invalid");
            if (v.GroupSizeMin < MinGroupSize || v.GroupSizeMax < v.GroupSizeMin)
                problems.Add("Group size range is invalid");
            Positive(nameof(v.ObstacleSpacingMin), v.ObstacleSpacingMin);
            if (v.ObstacleSpacingMax < v.ObstacleSpacingMin)
                problems.Add("Obstacle spacing range is invalid");
            Positive(nameof(v.GroupRadiusMin), v.GroupRadiusMin);
            if (v.GroupRadiusMax < v.GroupRadiusMin)
                problems.Add("Group radius range is invalid");
            Positive(nameof(v.ScaleMin), v.ScaleMin);
            if (v.ScaleMax < v.ScaleMin)
                problems.Add("Scale range is invalid");
            NonNegative(nameof(v.SingleWeight), v.SingleWeight);
            NonNegative(nameof(v.PairWeight), v.PairWeight);
            NonNegative(nameof(v.GroupWeight), v.GroupWeight);
            if (!(v.SingleWeight + v.PairWeight + v.GroupWeight > 0))
                problems.Add("At least one obstacle weight must be positive");
            NonNegative(nameof(v.DecorPerMetre), v.DecorPerMetre);
            if (v.DecorLateralMax < v.DecorLateralMin)
                problems.Add("Decor lateral range is invalid");
            Positive(nameof(v.ControlHoldStep), v.ControlHoldStep);
            NonNegative(nameof(v.ControlHoldMax), v.ControlHoldMax);
            if (!(v.FirstObstacleZ > 0) || !(v.FirstObstacleZ < v.RouteLength - v.FinishClearance))
                problems.Add("Obstacles do not fit between the start and the finish clearance");
            if (variants == null || variants.Count == 0)
                problems.Add("At least one crystal variant is required");
            else
                for (var i = 0; i < variants.Count; i++)
                    if (variants[i] == null || variants[i].Id != i)
                        problems.Add("Crystal variant ids must be dense and ordered (index " + i + ")");
            return problems;
        }

        private static string ComputeFingerprint(RuleValues v, IReadOnlyList<CrystalVariantData> variants)
        {
            var hash = FnvOffset;
            void Mix(ulong value)
            {
                for (var i = 0; i < BytesPerValue; i++)
                {
                    hash ^= (value >> (i * BitsPerByte)) & ByteMask;
                    hash *= FnvPrime;
                }
            }
            void D(double value) => Mix((ulong)BitConverter.DoubleToInt64Bits(value));
            void I(int value) => Mix((ulong)(uint)value);
            D(v.InitialDiameter);
            D(v.CriticalDiameter);
            D(v.TransferCoefficient);
            D(v.MinShotCost);
            D(v.ChargeRate);
            D(v.BlastCoefficient);
            D(v.CorridorGap);
            D(v.Tolerance);
            D(v.MinAdvance);
            D(v.RouteLength);
            D(v.DoorOpenDistance);
            D(v.DoorSetback);
            I(v.MaxCandidates);
            I(v.ObstacleCountMin);
            I(v.ObstacleCountMax);
            D(v.FirstObstacleZ);
            D(v.FinishClearance);
            D(v.ObstacleSpacingMin);
            D(v.ObstacleSpacingMax);
            D(v.SingleWeight);
            D(v.PairWeight);
            D(v.GroupWeight);
            I(v.GroupSizeMin);
            I(v.GroupSizeMax);
            D(v.GroupRadiusMin);
            D(v.GroupRadiusMax);
            D(v.LateralJitter);
            D(v.MemberSpacing);
            D(v.ScaleMin);
            D(v.ScaleMax);
            D(v.DecorPerMetre);
            D(v.DecorLateralMin);
            D(v.DecorLateralMax);
            D(v.MinSpendFraction);
            D(v.ControlHoldStep);
            D(v.ControlHoldMax);
            foreach (var variant in variants)
            {
                I(variant.Id);
                I(variant.Hull.Count);
                foreach (var p in variant.Hull.Vertices)
                {
                    D(p.X);
                    D(p.Z);
                }
            }
            return hash.ToString("x16");
        }
    }
}
