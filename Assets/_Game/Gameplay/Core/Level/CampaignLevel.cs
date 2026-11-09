using System;
using System.Collections.Generic;
using Slime.Gameplay.Core.Config;
using Slime.Gameplay.Core.Generation;
using Slime.Gameplay.Core.Geometry;
using Slime.Gameplay.Core.Levels;

namespace Slime.Gameplay.Core.Level
{
    /// <summary>One fixed crystal of a hand-designed level.</summary>
    public sealed class CrystalSpec
    {
        public int Variant { get; }
        public double X { get; }
        public double Z { get; }
        public int RotationStep { get; }
        public double Scale { get; }
        public CrystalRole Role { get; }

        public CrystalSpec(int variant, double x, double z, int rotationStep = 0, double scale = 1, CrystalRole role = CrystalRole.Single)
        {
            Variant = variant;
            X = x;
            Z = z;
            RotationStep = rotationStep;
            Scale = scale;
            Role = role;
        }
    }

    /// <summary>A campaign level: fixed geometry, route length and the stored control sequence of hold durations.</summary>
    public sealed class CampaignLevelSpec
    {
        public int Number { get; }
        public string Title { get; }
        public string Idea { get; }
        public double RouteLength { get; }
        public IReadOnlyList<CrystalSpec> Crystals { get; }
        public IReadOnlyList<double> ControlHolds { get; }
        public StarCriteria Stars { get; }

        public CampaignLevelSpec(int number, string title, string idea, double routeLength, IReadOnlyList<CrystalSpec> crystals,
            IReadOnlyList<double> controlHolds, StarCriteria? stars = null)
        {
            Number = number;
            Title = title ?? "";
            Idea = idea ?? "";
            RouteLength = routeLength;
            Crystals = crystals ?? Array.Empty<CrystalSpec>();
            ControlHolds = controlHolds ?? Array.Empty<double>();
            Stars = stars ?? new StarCriteria(ControlHolds.Count, 0);
        }
    }

    public sealed class InvalidLevelException : Exception
    {
        public InvalidLevelException(string message) : base(message) { }
    }

    /// <summary>
    /// Turns campaign specs into playable levels. The shared balance never changes between levels; only the route
    /// length and the geometry do. Every level's stored control sequence is replayed through the attempt model.
    /// </summary>
    public static class CampaignLevels
    {
        public const int Version = 1;

        public static GameRules RulesFor(GameRules campaignRules, CampaignLevelSpec spec) => campaignRules.WithRouteLength(spec.RouteLength);

        public static LevelData BuildLayout(GameRules levelRules, CampaignLevelSpec spec)
        {
            var placements = new List<CrystalPlacement>(spec.Crystals.Count);
            for (var i = 0; i < spec.Crystals.Count; i++)
            {
                var c = spec.Crystals[i];
                if (c.Variant < 0 || c.Variant >= levelRules.Variants.Count)
                    throw new InvalidLevelException($"Level {spec.Number}: crystal {i} uses unknown variant {c.Variant}.");

                var position = new Vec2(c.X, c.Z);
                var contour = levelRules.Variants[c.Variant].Hull.Transformed(c.RotationStep, c.Scale, position);
                placements.Add(new CrystalPlacement(i, c.Variant, position, c.RotationStep, c.Scale, c.Role, -1, contour));
            }
            return new LevelData(spec.Number, Version, levelRules, placements, null, null, spec.Title);
        }

        public static LevelData Build(GameRules campaignRules, CampaignLevelSpec spec, out GameRules levelRules)
        {
            levelRules = RulesFor(campaignRules, spec);
            var layout = BuildLayout(levelRules, spec);
            var replay = ControlReplay.Run(levelRules, layout, spec.ControlHolds);
            if (!replay.Success)
                throw new InvalidLevelException($"Level {spec.Number} '{spec.Title}': control sequence fails: {replay.Failure}");

            return new LevelData(spec.Number, Version, levelRules, layout.Crystals,
                new ControlPlan(spec.ControlHolds, replay.Spent, replay.FinalDiameter), null, spec.Title);
        }
    }
}
