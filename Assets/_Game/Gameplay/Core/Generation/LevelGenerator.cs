using System;
using System.Collections.Generic;
using Slime.Gameplay.Core.Config;
using Slime.Gameplay.Core.Geometry;
using Slime.Gameplay.Core.Level;
using Slime.Gameplay.Core.Model;

namespace Slime.Gameplay.Core.Generation
{
    /// <summary>
    /// Seeded level generator. Builds a random obstacle plan, fills decor, derives a control solution with the
    /// shared shot and corridor rules, and replays it through the attempt model before accepting the candidate.
    /// The number of candidates is bounded, so device speed never changes the result of a seed.
    /// </summary>
    public sealed class LevelGenerator
    {
        public const int Version = 1;
        private const int StreamLayout = 1;
        private const int StreamDecor = 2;
        private const int StreamCount = 3;
        private const int MaxPlannedShots = 64;
        private const int CheapCandidateShare = 2;
        private const int ExtraObstacles = 2;
        private const double SingleDepth = 0.6;
        private const double PairSpreadMin = 0.3;
        private const double PairSpreadMax = 0.48;
        private const double PairStaggerMax = 0.45;
        private const double FlipChance = 0.5;
        private const int GroupAttemptsPerMember = 30;
        private const double GroupDepthPadding = 0.4;
        private const double DiameterPerRadius = 2;
        private const double SideChance = 0.5;
        private const double DecorStartZ = -1.5;
        private const double DecorDoorClearance = 0.8;
        private const double Epsilon = 1e-9;
        private const double FinishBonus = 5;
        private const double ProgressBias = 0.1;
        private const double ScoreEpsilon = 1e-12;

        private struct Draft
        {
            public int Variant, Rotation, Obstacle;
            public Vec2 Position;
            public double Scale;
            public CrystalRole Role;
        }

        public LevelData Generate(GameRules rules, int seed)
        {
            if (rules == null)
                throw new ArgumentNullException(nameof(rules));

            var v = rules.Values;
            var count = new Prng(Prng.Mix(seed, StreamCount, 0)).Range(v.ObstacleCountMin, v.ObstacleCountMax);
            var lowerSpend = v.MinSpendFraction * rules.ControlBudgetLimit;
            LevelData cheapest = null;
            string lastFailure = null;
            for (var candidate = 0; candidate < v.MaxCandidates; candidate++)
            {
                var drafts = BuildLayout(rules, seed, candidate, count, simplified: false);
                var level = TryAccept(rules, seed, drafts, new GenerationReport(candidate + 1, false, null), out var failure);
                if (level == null)
                {
                    lastFailure = failure;
                    count = Math.Max(1, count - 1);
                    continue;
                }
                // Prefer levels that use a meaningful share of the budget; relax that wish in the second half.
                if (level.Control.TotalCost + rules.Tolerance < lowerSpend && candidate < v.MaxCandidates / CheapCandidateShare)
                {
                    if (cheapest == null || level.Control.TotalCost > cheapest.Control.TotalCost)
                        cheapest = level;
                    count = Math.Min(v.ObstacleCountMax + ExtraObstacles, count + 1);
                    continue;
                }
                return level;
            }
            if (cheapest != null)
                return cheapest;

            return Fallback(rules, seed, lastFailure);
        }

        private LevelData Fallback(GameRules rules, int seed, string reason)
        {
            var v = rules.Values;
            for (var count = v.ObstacleCountMin; count >= 0; count--)
            {
                var drafts = BuildLayout(rules, seed, v.MaxCandidates, count, simplified: true);
                var level = TryAccept(rules, seed, drafts, new GenerationReport(v.MaxCandidates, true, reason), out _);
                if (level != null)
                    return level;
            }
            throw new InvalidOperationException("Generator could not build even an empty route; rules are inconsistent.");
        }

        private LevelData TryAccept(GameRules rules, int seed, List<Draft> drafts, GenerationReport report, out string failure)
        {
            var placements = Place(rules, drafts);
            var draftLevel = new LevelData(seed, Version, rules, placements, null, report);
            var holds = PlanControl(rules, draftLevel, out failure);
            if (holds == null)
                return null;

            var replay = ControlReplay.Run(rules, draftLevel, holds);
            if (!replay.Success)
            {
                failure = "replay: " + replay.Failure;
                return null;
            }
            return new LevelData(seed, Version, rules, placements, new ControlPlan(holds, replay.Spent, replay.FinalDiameter), report);
        }

        private static List<CrystalPlacement> Place(GameRules rules, List<Draft> drafts)
        {
            var result = new List<CrystalPlacement>(drafts.Count);
            for (var i = 0; i < drafts.Count; i++)
            {
                var d = drafts[i];
                var contour = rules.Variants[d.Variant].Hull.Transformed(d.Rotation, d.Scale, d.Position);
                result.Add(new CrystalPlacement(i, d.Variant, d.Position, d.Rotation, d.Scale, d.Role, d.Obstacle, contour));
            }
            return result;
        }

        private static List<Draft> BuildLayout(GameRules rules, int seed, int candidate, int obstacleCount, bool simplified)
        {
            var v = rules.Values;
            var rng = new Prng(Prng.Mix(seed, StreamLayout, candidate));
            var drafts = new List<Draft>();
            var z = v.FirstObstacleZ;
            var lastZ = v.RouteLength - v.FinishClearance;
            var totalWeight = v.SingleWeight + v.PairWeight + v.GroupWeight;
            for (var obstacle = 0; obstacle < obstacleCount && z <= lastZ; obstacle++)
            {
                var roll = rng.NextDouble() * totalWeight;
                var role = simplified ? CrystalRole.Single
                    : roll < v.SingleWeight ? CrystalRole.Single
                    : roll < v.SingleWeight + v.PairWeight ? CrystalRole.Pair : CrystalRole.Group;
                var cx = rng.Range(-v.LateralJitter, v.LateralJitter);
                double depth;
                switch (role)
                {
                    case CrystalRole.Single:
                        drafts.Add(Crystal(rules, rng, new Vec2(cx, z), role, obstacle));
                        depth = SingleDepth;
                        break;
                    case CrystalRole.Pair:
                    {
                        var spread = rng.Range(PairSpreadMin, PairSpreadMax);
                        var stagger = rng.Range(0, PairStaggerMax);
                        var flip = rng.Chance(FlipChance) ? 1 : -1;
                        drafts.Add(Crystal(rules, rng, new Vec2(cx - spread, z + (flip > 0 ? 0 : stagger)), role, obstacle));
                        drafts.Add(Crystal(rules, rng, new Vec2(cx + spread, z + (flip > 0 ? stagger : 0)), role, obstacle));
                        depth = SingleDepth + stagger;
                        break;
                    }
                    default:
                    {
                        var radius = rng.Range(v.GroupRadiusMin, v.GroupRadiusMax);
                        var size = rng.Range(v.GroupSizeMin, v.GroupSizeMax);
                        var center = new Vec2(cx, z + radius);
                        var members = new List<Vec2> { center };
                        for (var attempt = 0; members.Count < size && attempt < size * GroupAttemptsPerMember; attempt++)
                        {
                            // Rejection sampling inside the disk keeps members apart without trigonometry.
                            var p = new Vec2(rng.Range(-radius, radius), rng.Range(-radius, radius));
                            if (p.LengthSquared > radius * radius)
                                continue;

                            var candidatePoint = center + p;
                            var free = true;
                            foreach (var m in members)
                                if (Vec2.Distance(m, candidatePoint) < v.MemberSpacing)
                                {
                                    free = false;
                                    break;
                                }
                            if (free)
                                members.Add(candidatePoint);
                        }
                        foreach (var m in members)
                            drafts.Add(Crystal(rules, rng, m, role, obstacle));
                        depth = radius * DiameterPerRadius + GroupDepthPadding;
                        break;
                    }
                }
                z += depth + rng.Range(v.ObstacleSpacingMin, v.ObstacleSpacingMax);
            }

            // Decor fills the sides before validation: it is part of the final layout the control plan must survive.
            var decor = new Prng(Prng.Mix(seed, StreamDecor, candidate));
            var decorCount = (int)Math.Floor(v.RouteLength * v.DecorPerMetre);
            for (var i = 0; i < decorCount; i++)
            {
                var side = decor.Chance(SideChance) ? 1 : -1;
                var position = new Vec2(side * decor.Range(v.DecorLateralMin, v.DecorLateralMax),
                    decor.Range(DecorStartZ, rules.DoorZ - DecorDoorClearance));
                drafts.Add(Crystal(rules, decor, position, CrystalRole.Decor, -1));
            }
            return drafts;
        }

        private static Draft Crystal(GameRules rules, Prng rng, Vec2 position, CrystalRole role, int obstacle)
        {
            var v = rules.Values;
            return new Draft
            {
                Variant = rng.Range(0, rules.Variants.Count - 1),
                Rotation = rng.Range(0, ConvexPolygon.RotationSteps - 1),
                Scale = rng.Range(v.ScaleMin, v.ScaleMax),
                Position = position,
                Role = role,
                Obstacle = obstacle,
            };
        }

        internal static List<double> PlanControl(GameRules rules, LevelData level, out string failure)
        {
            var v = rules.Values;
            var field = level.CreateField();
            var diameter = rules.D0;
            var spent = 0.0;
            var holds = new List<double>();
            var advance = ShotRules.ComputeAdvance(rules, field, level.StartZ, diameter);
            var z = advance.ToZ;
            var steps = (int)Math.Floor(v.ControlHoldMax / v.ControlHoldStep + Epsilon);
            while (!advance.ReachesFinish)
            {
                if (holds.Count >= MaxPlannedShots)
                {
                    failure = "too many control shots";
                    return null;
                }
                if (!rules.CanAffordMinShot(diameter)) { failure = $"out of resource at z={z:0.##}"; return null; }
                var bestScore = double.NegativeInfinity;
                double bestHold = -1;
                for (var i = 0; i <= steps; i++)
                {
                    var hold = i * v.ControlHoldStep;
                    var cost = rules.ChargeCost(hold);
                    if (rules.IsCritical(diameter - cost))
                        break;

                    if (spent + cost > rules.ControlBudgetLimit)
                        break;

                    var shot = ShotRules.ComputeShot(rules, field, z, diameter, hold);
                    if (!Contains(shot.HitIds, advance.BlockingCrystalId))
                        continue;

                    var trial = field.Clone();
                    foreach (var id in shot.HitIds)
                        trial.Kill(id);
                    var next = ShotRules.ComputeAdvance(rules, trial, z, shot.DiameterAfter);
                    var progress = next.ReachesFinish ? (rules.FinishZ - z) + FinishBonus : next.ToZ - z;
                    var score = (progress + ProgressBias) / cost;
                    if (score > bestScore + ScoreEpsilon)
                    {
                        bestScore = score;
                        bestHold = hold;
                    }
                }
                if (bestHold < 0) { failure = $"no affordable shot clears the blocker at z={z:0.##}"; return null; }
                var chosen = ShotRules.ComputeShot(rules, field, z, diameter, bestHold);
                foreach (var id in chosen.HitIds)
                    field.Kill(id);
                holds.Add(bestHold);
                spent += chosen.Cost;
                diameter = chosen.DiameterAfter;
                advance = ShotRules.ComputeAdvance(rules, field, z, diameter);
                z = advance.ToZ;
            }
            failure = null;
            return holds;
        }

        private static bool Contains(IReadOnlyList<int> ids, int id)
        {
            for (var i = 0; i < ids.Count; i++)
                if (ids[i] == id)
                    return true;

            return false;
        }
    }
}
