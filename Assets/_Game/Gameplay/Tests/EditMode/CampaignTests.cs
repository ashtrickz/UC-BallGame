using System.Collections.Generic;
using Game.Gameplay.Config;
using Game.Gameplay.Generation;
using Game.Gameplay.Level;
using Game.Gameplay.Model;
using NUnit.Framework;
using UnityEditor;

namespace Game.Gameplay.Tests
{
    /// <summary>The shipped campaign: every level proven, forgiving, and its stated lesson really occurs.</summary>
    public sealed class CampaignTests
    {
        private const string ConfigPath = "Assets/_Game/Gameplay/Config/SlimeGameConfig.asset";
        private static SlimeGameConfig _config;

        private static SlimeGameConfig Config()
        {
            _config ??= AssetDatabase.LoadAssetAtPath<SlimeGameConfig>(ConfigPath);
            Assert.IsNotNull(_config?.Campaign, "SlimeGameConfig must reference the campaign asset");
            return _config;
        }

        private static LevelData Level(int number, out GameRules rules) =>
            CampaignLevels.Build(Config().CreateSnapshot(), Config().Campaign.Spec(number - 1), out rules);

        private static AttemptModel Replay(GameRules rules, LevelData level)
        {
            var model = new AttemptModel(1, rules, level);
            var move = model.Start();
            model.ConfirmMove(1, move.ActionId);
            foreach (var hold in level.Control.HoldSeconds)
            {
                model.BeginCharge();
                model.SetHold(hold);
                var shot = model.ReleaseCharge();
                model.ConfirmImpact(1, shot.ActionId);
                model.ConfirmClearing(1, model.Pending.ActionId);
                model.ConfirmMove(1, model.Pending.ActionId);
            }
            return model;
        }

        [Test]
        public void Campaign_has_ten_levels_with_the_shared_balance()
        {
            Assert.AreEqual(10, Config().Campaign.Count);
            var baseRules = Config().CreateSnapshot();
            for (var n = 1; n <= 10; n++)
            {
                Level(n, out var rules);
                var a = rules.Values; var b = baseRules.Values;
                Assert.AreEqual(b.InitialDiameter, a.InitialDiameter);
                Assert.AreEqual(b.CriticalDiameter, a.CriticalDiameter);
                Assert.AreEqual(b.MinShotCost, a.MinShotCost);
                Assert.AreEqual(b.ChargeRate, a.ChargeRate);
                Assert.AreEqual(b.TransferCoefficient, a.TransferCoefficient);
                Assert.AreEqual(b.BlastCoefficient, a.BlastCoefficient);
            }
        }

        [Test]
        public void Every_level_is_won_by_its_control_sequence_with_the_twenty_percent_reserve([Range(1, 10)] int n)
        {
            var level = Level(n, out var rules);
            var replay = ControlReplay.Run(rules, level, level.Control.HoldSeconds);
            Assert.IsTrue(replay.Success, replay.Failure);
            Assert.GreaterOrEqual(rules.Budget, GameRules.BudgetReserve * replay.Spent - 1e-9, "D0 − Dcrit ≥ 1.2 × Cref");
            Assert.AreEqual(AttemptPhase.Won, Replay(rules, level).Phase);
        }

        [Test]
        public void Every_control_shot_has_a_comfortable_hold_window([Range(1, 10)] int n)
        {
            var level = Level(n, out var rules);
            for (var i = 0; i < level.Control.HoldSeconds.Count; i++)
            {
                var window = CampaignSolver.HoldWindow(rules, level, level.Control.HoldSeconds, i);
                Assert.GreaterOrEqual(window.Width, 0.149, $"level {n} shot {i}: window {window} — must not need one exact frame");
            }
        }

        [Test]
        public void Repeating_a_level_builds_the_same_layout()
        {
            var a = Level(7, out _);
            var b = Level(7, out _);
            Assert.AreEqual(a.Crystals.Count, b.Crystals.Count);
            for (var i = 0; i < a.Crystals.Count; i++) Assert.AreEqual(a.Crystals[i].Position, b.Crystals[i].Position);
        }

        private static List<int> AliveOfRole(AttemptModel model, LevelData level, CrystalRole role)
        {
            var ids = new List<int>();
            foreach (var c in level.Crystals) if (c.Role == role && model.IsAlive(c.Id)) ids.Add(c.Id);
            return ids;
        }

        [Test]
        public void Level1_taps_are_enough()
        {
            var level = Level(1, out var rules);
            CollectionAssert.AreEqual(new[] { 0.0, 0.0 }, level.Control.HoldSeconds);
            Assert.AreEqual(2 * rules.Values.MinShotCost, CampaignSolver.Cheapest(rules, level).Cost, 1e-9);
        }

        [Test]
        public void Level2_one_bigger_shot_beats_a_series_of_small_ones()
        {
            var level = Level(2, out var rules);
            Assert.AreEqual(1, level.Control.HoldSeconds.Count);
            var one = CampaignSolver.Cheapest(rules, level, maxShots: 1).Cost;
            var small = CampaignSolver.Cheapest(rules, level, maxShots: 12, maxHold: 0.3).Cost;
            Assert.Less(one * 1.5, small, $"one shot {one:0.###} vs small shots {small:0.###}");
        }

        [Test]
        public void Level4_the_smaller_slime_passes_between_surviving_side_crystals()
        {
            var level = Level(4, out var rules);
            var model = Replay(rules, level);
            Assert.AreEqual(AttemptPhase.Won, model.Phase);
            Assert.AreEqual(4, AliveOfRole(model, level, CrystalRole.Pair).Count, "the side crystals are not destroyed");
            var full = new Geometry.CrystalField(new[] { level.Crystals[2].Contour, level.Crystals[3].Contour });
            var stopZ = level.Crystals[2].Position.Z - 2;
            Assert.IsTrue(full.SweepCircle(new Geometry.Vec2(0, stopZ), Geometry.Vec2.Forward, rules.CorridorHalfWidth(rules.D0), 4, rules.Tolerance).Hit,
                "the full-size slime does not fit between them");
        }

        [Test]
        public void Level5_one_shot_through_both_groups_costs_noticeably_more()
        {
            var level = Level(5, out var rules);
            var one = CampaignSolver.Cheapest(rules, level, maxShots: 1).Cost;
            var two = CampaignSolver.Cheapest(rules, level, maxShots: 2).Cost;
            Assert.Greater(one - two, 0.1, $"one shot {one:0.###} vs separately {two:0.###}");
        }

        [Test]
        public void Level6_the_side_crowd_is_optional()
        {
            var level = Level(6, out var rules);
            var model = Replay(rules, level);
            Assert.AreEqual(AttemptPhase.Won, model.Phase);
            var crowd = 0;
            foreach (var c in level.Crystals) if (c.Role == CrystalRole.Group) crowd++;
            Assert.AreEqual(crowd, AliveOfRole(model, level, CrystalRole.Group).Count, "the crowd never had to go");
            Assert.Less(model.TotalSpent, 0.15);
        }

        [Test]
        public void Level8_a_small_first_shot_beats_a_big_shot_from_the_start()
        {
            var level = Level(8, out var rules);
            var big = CampaignSolver.Cheapest(rules, level, maxShots: 1).Cost;
            var smallFirst = CampaignSolver.Cheapest(rules, level, firstMaxHold: 0.1).Cost;
            Assert.Greater(big, smallFirst * 2, $"big from start {big:0.###} vs small first {smallFirst:0.###}");
        }

        [Test]
        public void Narrow_passage_levels_require_shrinking()
        {
            foreach (var n in new[] { 7, 9, 10 })
            {
                var level = Level(n, out var rules);
                var model = Replay(rules, level);
                Assert.Greater(AliveOfRole(model, level, CrystalRole.Pair).Count, 0, $"level {n}: passages are squeezed through, not demolished");
            }
        }
    }
}
