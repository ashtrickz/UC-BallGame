using System;
using System.Collections.Generic;
using Slime.Gameplay.Core.Config;
using Slime.Gameplay.Core.Geometry;

namespace Slime.Gameplay.Core.Level
{
    public enum CrystalRole { Single, Pair, Group, Decor }

    /// <summary>A placed crystal. Its id is its index in <see cref="LevelData.Crystals"/>.</summary>
    public sealed class CrystalPlacement
    {
        public int Id { get; }
        public int VariantId { get; }
        public Vec2 Position { get; }
        public int RotationStep { get; }
        public double Scale { get; }
        public CrystalRole Role { get; }
        public int ObstacleIndex { get; }
        public ConvexPolygon Contour { get; }

        public CrystalPlacement(int id, int variantId, Vec2 position, int rotationStep, double scale, CrystalRole role,
            int obstacleIndex, ConvexPolygon contour)
        {
            Id = id;
            VariantId = variantId;
            Position = position;
            RotationStep = rotationStep;
            Scale = scale;
            Role = role;
            ObstacleIndex = obstacleIndex;
            Contour = contour;
        }
    }

    /// <summary>Hold durations of the control solution and what replaying them proved.</summary>
    public sealed class ControlPlan
    {
        public IReadOnlyList<double> HoldSeconds { get; }
        public double TotalCost { get; }
        public double FinalDiameter { get; }

        public ControlPlan(IReadOnlyList<double> holdSeconds, double totalCost, double finalDiameter)
        {
            HoldSeconds = holdSeconds;
            TotalCost = totalCost;
            FinalDiameter = finalDiameter;
        }
    }

    public sealed class GenerationReport
    {
        public int CandidatesTried { get; }
        public bool UsedFallback { get; }
        public string Note { get; }
        public GenerationReport(int candidatesTried, bool usedFallback, string note)
        {
            CandidatesTried = candidatesTried;
            UsedFallback = usedFallback;
            Note = note ?? "";
        }
    }

    /// <summary>Final, validated level: what the attempt plays and what the view builds.</summary>
    public sealed class LevelData
    {
        public int Seed { get; }
        public int GeneratorVersion { get; }
        public string RulesFingerprint { get; }
        public double StartZ { get; }
        public double FinishZ { get; }
        public double DoorZ { get; }
        public IReadOnlyList<CrystalPlacement> Crystals { get; }
        public ControlPlan Control { get; }
        public GenerationReport Report { get; }
        public string Name { get; }

        public LevelData(int seed, int generatorVersion, GameRules rules, IReadOnlyList<CrystalPlacement> crystals,
            ControlPlan control, GenerationReport report, string name = null)
        {
            Name = name;
            if (rules == null)
                throw new ArgumentNullException(nameof(rules));

            Seed = seed;
            GeneratorVersion = generatorVersion;
            RulesFingerprint = rules.Fingerprint;
            StartZ = 0;
            FinishZ = rules.FinishZ;
            DoorZ = rules.DoorZ;
            Crystals = crystals;
            Control = control;
            Report = report;
        }

        public CrystalField CreateField()
        {
            var polygons = new ConvexPolygon[Crystals.Count];
            for (var i = 0; i < polygons.Length; i++)
                polygons[i] = Crystals[i].Contour;
            return new CrystalField(polygons);
        }

        public string Describe() =>
            (Name != null ? $"level '{Name}' · " : "") + $"seed {Seed} · gen v{GeneratorVersion} · rules {RulesFingerprint} · crystals {Crystals.Count} · " +
            $"control {Control?.HoldSeconds.Count ?? 0} shots, cost {Control?.TotalCost ?? 0:0.###}" +
            (Report != null && Report.UsedFallback ? " · fallback" : "") +
            (Report != null ? $" · candidates {Report.CandidatesTried}" : "");
    }
}
