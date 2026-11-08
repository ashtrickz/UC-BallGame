using System;
using System.Collections.Generic;
using Game.Gameplay.Config;
using Game.Gameplay.Geometry;
using Game.Gameplay.Level;

namespace Game.Gameplay.Generation
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
        private const int StreamFill = 11, StreamLayout = 12;

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
                if (level == null) { fill = Math.Max(0.12, fill * 0.82); continue; }
                if (level.Control.TotalCost + rules.Tolerance < lowerSpend && candidate < candidates / 2)
                {
                    if (cheapest == null || level.Control.TotalCost > cheapest.Control.TotalCost) cheapest = level;
                    fill = Math.Min(1, fill * 1.15);
                    continue;
                }
                return level;
            }
            if (cheapest != null) return cheapest;
            // Simplified, still proven: an ever thinner core until the plan fits (an empty core always does).
            for (var thin = 0.2; thin >= -1e-9; thin -= 0.05)
            {
                var level = TryBuild(rules, wall, seed, candidates, Math.Max(0, thin), true, out _);
                if (level != null) return level;
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
            if (holds == null) return null;
            var replay = ControlReplay.Run(rules, draft, holds);
            if (!replay.Success) { failure = replay.Failure; return null; }
            return new LevelData(seed, Version, rules, placements, new ControlPlan(holds, replay.Spent, replay.FinalDiameter), report, "Wall");
        }

        private static List<CrystalPlacement> Layout(GameRules rules, WallValues wall, int seed, int candidate, double coreFill)
        {
            var rng = new Prng(Prng.Mix(seed, StreamLayout, candidate));
            var result = new List<CrystalPlacement>();
            var doorZ = rules.DoorZ;
            var wallEnd = doorZ - wall.WallEndBeforeDoor;
            // Density wave along the route: denser bands and thinner pockets, so the wall has a shape to read.
            var phase = rng.Range(0, 6.283);
            var period = rng.Range(2.2, 3.4);
            var row = 0;
            for (var z = -2.0; z < doorZ - 0.6; z += wall.Spacing * 0.866, row++)
            {
                var offset = (row & 1) * wall.Spacing * 0.5;
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
                        if (pz < wall.WallStartZ || pz > wallEnd) continue; // free start and approach to the doors
                        chance = coreFill * (0.78 + 0.3 * Wave(pz, phase, period));
                    }
                    else
                    {
                        if (pz < 1.0 && Math.Abs(px) < wall.CoreHalfWidth + 0.6) continue; // keep the start readable
                        if (pz > doorZ - 1.8 && Math.Abs(px) < 2.6) continue;              // keep the door frame clear
                        chance = wall.SideFill;
                    }
                    if (roll >= chance) continue;
                    var position = new Vec2(px, pz);
                    var contour = rules.Variants[variant].Hull.Transformed(rotation, scale, position);
                    result.Add(new CrystalPlacement(result.Count, variant, position, rotation, scale,
                        core ? CrystalRole.Group : CrystalRole.Decor, core ? 0 : -1, contour));
                }
            }
            return result;
        }

        /// <summary>Smooth deterministic wave in [-1, 1] built from a fixed polynomial (no platform trigonometry).</summary>
        private static double Wave(double z, double phase, double period)
        {
            var t = (z + phase) / period;
            var f = t - Math.Floor(t);           // 0..1
            var tri = 1 - 4 * Math.Abs(f - 0.5); // triangle −1..1
            return tri * (1.5 - 0.5 * tri * tri); // smoothed
        }
    }
}
