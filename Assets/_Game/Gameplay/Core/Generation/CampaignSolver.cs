using System;
using System.Collections.Generic;
using System.Text;
using Game.Gameplay.Config;
using Game.Gameplay.Geometry;
using Game.Gameplay.Level;
using Model = Game.Gameplay.Model;

namespace Game.Gameplay.Generation
{
    /// <summary>
    /// Design tool for fixed levels: exhaustive cheapest winning sequence (branch and bound over distinct shot
    /// outcomes), cost of a given sequence, and the hold window around each control shot. Uses the shared rules only.
    /// </summary>
    public static class CampaignSolver
    {
        public const double LadderStep = 0.01;

        private const int DefaultMaxShots = 8;
        private const double Epsilon = 1e-9;
        private const double ProgressEpsilon = 1e-6;
        private const double HoldInset = 0.1;
        private const double HalfWindow = 0.5;
        private const int HoldDecimals = 2;
        private const double DefaultWindowStep = 0.005;
        private const double MaxWindowSeconds = 5;
        [ThreadStatic] private static double _maxHold, _firstMaxHold, _minWindow;

        public sealed class Solution
        {
            public bool Found;
            public double Cost = double.PositiveInfinity;
            public List<double> Holds = new List<double>();
            public int Nodes;
        }

        public readonly struct Window
        {
            public readonly double Min, Max;
            public Window(double min, double max)
            {
                Min = min;
                Max = max;
            }
            public double Width => Max - Min;
            public override string ToString() => $"[{Min:0.00}..{Max:0.00}]s";
        }

        public static Solution Cheapest(GameRules rules, LevelData level, int maxShots = DefaultMaxShots, double costLimit = double.PositiveInfinity,
            double maxHold = double.PositiveInfinity, double firstMaxHold = double.PositiveInfinity, double minWindow = 0)
        {
            var solution = new Solution { Cost = costLimit };
            _maxHold = maxHold;
            _firstMaxHold = firstMaxHold;
            _minWindow = minWindow;
            var field = level.CreateField();
            var advance = Model.ShotRules.ComputeAdvance(rules, field, level.StartZ, rules.D0);
            var seen = new Dictionary<string, double>();
            Search(rules, level, field, advance, rules.D0, 0, new List<double>(), maxShots, solution, seen);
            if (!solution.Found)
                solution.Cost = double.PositiveInfinity;
            return solution;
        }

        private static void Search(GameRules rules, LevelData level, CrystalField field, Model.AdvancePlan advance, double diameter,
            double spent, List<double> holds, int shotsLeft, Solution best, Dictionary<string, double> seen)
        {
            best.Nodes++;
            if (advance.ReachesFinish)
            {
                if (spent < best.Cost - Epsilon)
                {
                    best.Found = true;
                    best.Cost = spent;
                    best.Holds = new List<double>(holds);
                }
                return;
            }
            if (shotsLeft == 0 || !rules.CanAffordMinShot(diameter) || spent + rules.Values.MinShotCost >= best.Cost - Epsilon)
                return;

            var key = StateKey(field, advance.ToZ, diameter);
            if (seen.TryGetValue(key, out var spentBefore) && spentBefore <= spent + Epsilon)
                return;

            seen[key] = spent;

            var holdLimit = holds.Count == 0 ? Math.Min(_maxHold, _firstMaxHold) : _maxHold;
            foreach (var candidate in Candidates(rules, level, field, advance.ToZ, diameter, best.Cost - spent, holdLimit))
            {
                var next = field.Clone();
                foreach (var id in candidate.Shot.HitIds)
                    next.Kill(id);
                holds.Add(candidate.Hold);
                Search(rules, level, next, candidate.Next, candidate.Shot.DiameterAfter, spent + candidate.Shot.Cost, holds, shotsLeft - 1, best, seen);
                holds.RemoveAt(holds.Count - 1);
            }
        }

        private readonly struct Candidate
        {
            public readonly double Hold;
            public readonly Model.ShotPlan Shot;
            public readonly Model.AdvancePlan Next;
            public Candidate(double hold, Model.ShotPlan shot, Model.AdvancePlan next)
            {
                Hold = hold;
                Shot = shot;
                Next = next;
            }
        }

        private static List<Candidate> Candidates(GameRules rules, LevelData level, CrystalField field, double z, double diameter, double budget,
            double holdLimit)
        {
            var result = new List<Candidate>();
            var currentStop = Model.ShotRules.ComputeAdvance(rules, field, z, diameter);
            string runKey = null;
            double runStart = 0, runEnd = 0;
            var runUseful = false;

            void Close()
            {
                if (runKey == null || !runUseful)
                    return;

                double hold;
                if (_minWindow <= 0)
                    hold = runStart;
                else
                {
                    if (runEnd - runStart + LadderStep < _minWindow)
                        return;

                    hold = runStart <= 0 ? 0 : Math.Round(runStart + Math.Min(HoldInset, (runEnd - runStart) * HalfWindow), HoldDecimals);
                }
                var shot = Model.ShotRules.ComputeShot(rules, field, z, diameter, hold);
                var next = field.Clone();
                foreach (var id in shot.HitIds)
                    next.Kill(id);
                result.Add(new Candidate(hold, shot, Model.ShotRules.ComputeAdvance(rules, next, z, shot.DiameterAfter)));
            }

            for (var i = 0; ; i++)
            {
                var hold = i * LadderStep;
                if (hold > holdLimit + Epsilon)
                    break;

                var cost = rules.ChargeCost(hold);
                if (cost >= budget || rules.IsCritical(diameter - cost))
                    break;

                var shot = Model.ShotRules.ComputeShot(rules, field, z, diameter, hold);
                var next = field.Clone();
                foreach (var id in shot.HitIds)
                    next.Kill(id);
                var advance = Model.ShotRules.ComputeAdvance(rules, next, z, shot.DiameterAfter);
                var kills = Signature(level, shot.HitIds);
                var useful = kills.Length > 0 || advance.ToZ > currentStop.ToZ + ProgressEpsilon || advance.ReachesFinish;
                var key = kills + "|" + (advance.ReachesFinish ? "F" : advance.BlockingCrystalId.ToString());
                if (key != runKey)
                {
                    Close();
                    runKey = key;
                    runStart = hold;
                    runUseful = useful;
                }
                runEnd = hold;
            }
            Close();
            return result;
        }

        public static string Signature(LevelData level, IReadOnlyList<int> ids)
        {
            var sb = new StringBuilder();
            foreach (var id in ids)
                if (level.Crystals[id].Role != CrystalRole.Decor)
                    sb.Append(id).Append(',');
            return sb.ToString();
        }

        private static string StateKey(CrystalField field, double z, double diameter)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < field.Count; i++)
                sb.Append(field.IsAlive(i) ? '1' : '0');
            return sb.Append('|').Append(z.ToString("0.000")).Append('|').Append(diameter.ToString("0.0000")).ToString();
        }

        public static Window HoldWindow(GameRules rules, LevelData level, IReadOnlyList<double> holds, int index, double step = DefaultWindowStep)
        {
            var field = level.CreateField();
            var z = Model.ShotRules.ComputeAdvance(rules, field, level.StartZ, rules.D0).ToZ;
            var diameter = rules.D0;
            for (var i = 0; i < index; i++)
            {
                var s = Model.ShotRules.ComputeShot(rules, field, z, diameter, holds[i]);
                foreach (var id in s.HitIds)
                    field.Kill(id);
                diameter = s.DiameterAfter;
                z = Model.ShotRules.ComputeAdvance(rules, field, z, diameter).ToZ;
            }
            var reference = Signature(level, Model.ShotRules.ComputeShot(rules, field, z, diameter, holds[index]).HitIds);
            bool Works(double hold)
            {
                if (hold < 0)
                    return false;

                if (Signature(level, Model.ShotRules.ComputeShot(rules, field, z, diameter, hold).HitIds) != reference)
                    return false;

                var trial = new List<double>(holds) { [index] = hold };
                return ControlReplay.Run(rules, level, trial).Success;
            }
            double min = holds[index], max = holds[index];
            while (Works(min - step))
                min -= step;
            while (max - holds[index] < MaxWindowSeconds && Works(max + step))
                max += step;
            return new Window(Math.Max(0, min), max);
        }

        public static double CostOf(GameRules rules, LevelData level, IReadOnlyList<double> holds)
        {
            var replay = ControlReplay.Run(rules, level, holds);
            return replay.Won ? replay.Spent : double.PositiveInfinity;
        }
    }
}
