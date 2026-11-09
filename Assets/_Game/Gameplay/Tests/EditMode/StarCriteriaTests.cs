using System;
using NUnit.Framework;
using Slime.Gameplay.Config;
using Slime.Gameplay.Core.Config;
using Slime.Gameplay.Core.Generation;
using Slime.Gameplay.Core.Levels;
using UnityEditor;

namespace Slime.Gameplay.Tests.EditMode
{
    /// <summary>Authored independent star thresholds, boundary behavior and achievable targets on real generated layouts.</summary>
    public sealed class StarCriteriaTests
    {
        [TestCase(false, 2, 80, 0)]
        [TestCase(true, 4, 69, 1)]
        [TestCase(true, 3, 69, 2)]
        [TestCase(true, 4, 70, 2)]
        [TestCase(true, 3, 70, 3)]
        [TestCase(true, 2, 80, 3)]
        public void Bonus_criteria_are_independent_and_include_their_boundaries(bool won, int shots, double mass, int expected)
        {
            Assert.AreEqual(expected, new StarCriteria(3, 70).Evaluate(won, shots, mass));
        }

        [Test]
        public void Mass_is_the_remaining_initial_resource_without_subtracting_the_critical_reserve()
        {
            Assert.AreEqual(50, StarCriteria.RemainingMassPercent(0.8, 1.6), 1e-9);
            Assert.AreEqual(100, StarCriteria.RemainingMassPercent(1.6, 1.6), 1e-9);
            Assert.AreEqual(28.125, StarCriteria.RemainingMassPercent(0.45, 1.6), 1e-9);
        }

        [Test]
        public void Invalid_authored_thresholds_are_rejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StarCriteria(-1, 50));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StarCriteria(2, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StarCriteria(2, 101));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StarCriteria(2, double.NaN));
        }

        [Test]
        public void Every_campaign_control_earns_three_and_menu_uses_the_same_record_key()
        {
            var config = AssetDatabase.LoadAssetAtPath<SlimeGameConfig>("Assets/_Game/Gameplay/Config/SlimeGameConfig.asset");
            var source = new CampaignLevelSource(config);
            for (var number = 1; number <= config.CampaignCount; number++)
            {
                var built = source.Build(new CampaignLevelRequest(number));
                var replay = ControlReplay.Run(built.Rules, built.Level, built.Level.Control.HoldSeconds);
                Assert.IsTrue(replay.Won);
                Assert.AreEqual(3, built.Stars.Evaluate(replay.Won, replay.Shots,
                    StarCriteria.RemainingMassPercent(replay.FinalDiameter, built.Rules.D0)), "level " + number);
                Assert.AreEqual(built.ResultKey, config.Campaign.StarsKey(number - 1, config.CreateSnapshot()));
            }
        }

        [Test]
        public void Two_hundred_wall_controls_earn_three_and_same_seed_keeps_targets()
        {
            var config = AssetDatabase.LoadAssetAtPath<SlimeGameConfig>("Assets/_Game/Gameplay/Config/SlimeGameConfig.asset");
            var source = new WallLevelSource(config, new WallGenerator(), new SeedProvider(0, 1));
            for (var seed = 1; seed <= 200; seed++)
            {
                var built = source.Build(new WallLevelRequest(seed));
                var control = built.Level.Control;
                Assert.AreEqual(3, built.Stars.Evaluate(true, control.HoldSeconds.Count,
                    StarCriteria.RemainingMassPercent(control.FinalDiameter, built.Rules.D0)), "seed " + seed);
            }

            var first = source.Build(new WallLevelRequest(77));
            var second = source.Build(new WallLevelRequest(77));
            Assert.AreEqual(first.ResultKey, second.ResultKey);
            Assert.AreEqual(first.Stars.MaxShots, second.Stars.MaxShots);
            Assert.AreEqual(first.Stars.MinRemainingMassPercent, second.Stars.MinRemainingMassPercent);
            var changed = new StarCriteria(first.Stars.MaxShots + 1, first.Stars.MinRemainingMassPercent);
            Assert.AreNotEqual(first.ResultKey, changed.ResultKey(nameof(WallLevelRequest), first.Level));
        }
    }
}
