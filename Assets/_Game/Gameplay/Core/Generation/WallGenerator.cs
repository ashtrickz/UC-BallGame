using System;
using System.Collections.Generic;
using Slime.Gameplay.Core.Config;
using Slime.Gameplay.Core.Geometry;
using Slime.Gameplay.Core.Level;

namespace Slime.Gameplay.Core.Generation
{
    /// <summary>
    /// "Wall" mode generator: a dense, jittered field of crystals covering the whole frame. Only the core band can
    /// block the slime; its fill (with a gentle density wave along the route) is tuned per candidate so that the
    /// control plan — found by the shared planner and replayed through the attempt model — fits B / 1.2.
    /// The candidate count is bounded and every accepted wall is proven.
    /// </summary>
    public sealed class WallGenerator
    {
        public const int Version = 1;
        private const int StreamFill = 11;
        private const int StreamLayout = 12;
        private const double MinFill = 0.12;
        private const double FillShrink = 0.82;
        private const double FillGrowth = 1.15;
        private const int CheapCandidateShare = 2;
        private const double ThinStart = 0.2;
        private const double ThinStep = 0.05;
        private const double Epsilon = 1e-9;
        private const double WavePhaseMax = 6.283;
        private const double WavePeriodMin = 2.2;
        private const double WavePeriodMax = 3.4;
        private const double FieldStartZ = -2.0;
        private const double DoorRowClearance = 0.6;
        private const double RowStep = 0.866;
        private const double RowOffset = 0.5;
        private const double CoreChanceBase = 0.78;
        private const double CoreChanceWave = 0.3;
        private const double StartClearZ = 1.0;
        private const double StartClearPadding = 0.6;
        private const double DoorFrameDepth = 1.8;
        private const double DoorFrameHalfWidth = 2.6;
        private const double TriangleScale = 4;
        private const double TriangleCenter = 0.5;
        private const double SmoothA = 1.5;
        private const double SmoothB = 0.5;

        public LevelData Generate(GameRules baseRules, WallValues wall, int seed, out GameRules rules)
        {
            rules = baseRules.WithRouteLength(wall.RouteLength);
            var lowerSpend = rules.Values.MinSpendFraction * rules.ControlBudgetLimit;
            var fill = new Prng(Prng.Mix(seed, StreamFill, 0)).Range(wall.CoreFillMin, wall.CoreFillMax);
            LevelData cheapest = null;
            string failure = null;
            var candidates = Math.Max(1, wall.MaxCandidates);
            for (var candidate = 0; candidate < candidates; candidate++)
            {
                var level = TryBuild(rules, wall, seed, candidate, fill, false, out failure);
                if (level == null)
                {
                    fill = Math.Max(MinFill, fill * FillShrink);
                    continue;
                }
                if (level.Control.TotalCost + rules.Tolerance < lowerSpend && candidate < candidates / CheapCandidateShare)
                {
                    if (cheapest == null || level.Control.TotalCost > cheapest.Control.TotalCost)
                        cheapest = level;
                    fill = Math.Min(1, fill * FillGrowth);
                    continue;
                }
                return level;
            }
            if (cheapest != null)
                return cheapest;

            // Simplified, still proven: an ever thinner core until the plan fits (an empty core always does).
            for (var thin = ThinStart; thin >= -Epsilon; thin -= ThinStep)
            {
                var level = TryBuild(rules, wall, seed, candidates, Math.Max(0, thin), true, out _);
                if (level != null)
                    return level;
            }
            throw new InvalidOperationException("Wall generator could not build a passable wall: " + failure);
        }

        private static LevelData TryBuild(GameRules rules, WallValues wall, int seed, int candidate, double coreFill, bool fallback,
            out string failure)
        {
            var placements = Layout(rules, wall, seed, candidate, coreFill);
            var report = new GenerationReport(candidate + 1, fallback, $"wall v{Version} {wall.Fingerprint()} core fill {coreFill:0.00}");
            var draft = new LevelData(seed, Version, rules, placements, null, report, "Wall");
            var holds = LevelGenerator.PlanControl(rules, draft, out failure);
            if (holds == null)
                return null;

            var replay = ControlReplay.Run(rules, draft, holds);
            if (!replay.Success)
            {
                failure = replay.Failure;
                return null;
            }
            return new LevelData(seed, Version, rules, placements, new ControlPlan(holds, replay.Spent, replay.FinalDiameter), report, "Wall");
        }

        private static List<CrystalPlacement> Layout(GameRules rules, WallValues wall, int seed, int candidate, double coreFill)
        {
            var rng = new Prng(Prng.Mix(seed, StreamLayout, candidate));
            var result = new List<CrystalPlacement>();
            var doorZ = rules.DoorZ;
            var wallEnd = doorZ - wall.WallEndBeforeDoor;
            // Density wave along the route: denser bands and thinner pockets, so the wall has a shape to read.
            var phase = rng.Range(0, WavePhaseMax);
            var period = rng.Range(WavePeriodMin, WavePeriodMax);
            var row = 0;
            for (var z = FieldStartZ; z < doorZ - DoorRowClearance; z += wall.Spacing * RowStep, row++)
            {
                var offset = (row & 1) * wall.Spacing * RowOffset;
                for (var x = -wall.FieldHalfWidth + offset; x <= wall.FieldHalfWidth; x += wall.Spacing)
                {
                    var px = x + rng.Range(-wall.Jitter, wall.Jitter) * wall.Spacing;
                    var pz = z + rng.Range(-wall.Jitter, wall.Jitter) * wall.Spacing;
                    var roll = rng.NextDouble();
                    var variant = rng.Range(0, rules.Variants.Count - 1);
                    var rotation = rng.Range(0, ConvexPolygon.RotationSteps - 1);
                    var scale = rng.Range(wall.ScaleMin, wall.ScaleMax);
                    var core = Math.Abs(px) <= wall.CoreHalfWidth;
                    double chance;
                    if (core)
                    {
                        if (pz < wall.WallStartZ || pz > wallEnd) // free start and approach to the doors
                            continue;

                        chance = coreFill * (CoreChanceBase + CoreChanceWave * Wave(pz, phase, period));
                    }
                    else
                    {
                        if (pz < StartClearZ && Math.Abs(px) < wall.CoreHalfWidth + StartClearPadding) // keep the start readable
                            continue;

                        if (pz > doorZ - DoorFrameDepth && Math.Abs(px) < DoorFrameHalfWidth) // keep the door frame clear
                            continue;

                        chance = wall.SideFill;
                    }
                    if (roll >= chance)
                        continue;

                    var position = new Vec2(px, pz);
                    var contour = rules.Variants[variant].Hull.Transformed(rotation, scale, position);
                    result.Add(new CrystalPlacement(result.Count, variant, position, rotation, scale,
                        core ? CrystalRole.Group : CrystalRole.Decor, core ? 0 : -1, contour));
                }
            }
            return result;
        }

        private static double Wave(double z, double phase, double period)
        {
            var t = (z + phase) / period;
            var f = t - Math.Floor(t); // 0..1
            var tri = 1 - TriangleScale * Math.Abs(f - TriangleCenter); // triangle -1..1

            return tri * (SmoothA - SmoothB * tri * tri); // smoothed
        }
    }
}
