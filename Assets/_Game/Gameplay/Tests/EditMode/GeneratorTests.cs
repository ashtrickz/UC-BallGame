using System.Diagnostics;
using System.Text;
using Game.Gameplay.Config;
using Game.Gameplay.Generation;
using Game.Gameplay.Level;
using NUnit.Framework;

namespace Game.Gameplay.Tests
{
    public sealed class GeneratorTests
    {
        private static string Dump(LevelData level)
        {
            var sb = new StringBuilder(level.Describe()).Append('\n');
            foreach (var c in level.Crystals)
                sb.Append($"{c.Id}:{c.VariantId}:{c.RotationStep}:{c.Scale:R}:{c.Position.X:R}:{c.Position.Z:R}:{c.Role}\n");
            foreach (var h in level.Control.HoldSeconds) sb.Append(h.ToString("R")).Append(';');
            return sb.ToString();
        }

        [Test]
        public void Same_seed_and_rules_reproduce_the_level_and_control_plan()
        {
            var rules = TestRules.Create();
            var generator = new LevelGenerator();
            foreach (var seed in new[] { 1, 42, -7, int.MaxValue })
                Assert.AreEqual(Dump(generator.Generate(rules, seed)), Dump(generator.Generate(TestRules.Create(), seed)), "seed " + seed);
        }

        [Test]
        public void Unrelated_random_streams_do_not_change_the_level()
        {
            var rules = TestRules.Create();
            var first = Dump(new LevelGenerator().Generate(rules, 99));
            // Effects use UnityEngine.Random / System.Random; consuming them must not shift the generator.
            UnityEngine.Random.InitState(5);
            for (var i = 0; i < 100; i++) UnityEngine.Random.value.ToString();
            new System.Random(3).Next();
            Assert.AreEqual(first, Dump(new LevelGenerator().Generate(rules, 99)));
        }

        [Test]
        public void Different_seeds_give_different_layouts()
        {
            var rules = TestRules.Create();
            var generator = new LevelGenerator();
            Assert.AreNotEqual(Dump(generator.Generate(rules, 1)), Dump(generator.Generate(rules, 2)));
        }

        [Test]
        public void Fingerprint_changes_with_any_rule_value()
        {
            var a = TestRules.Create();
            var b = TestRules.Create(v => { v.ChargeRate += 1e-6; return v; });
            Assert.AreNotEqual(a.Fingerprint, b.Fingerprint);
            Assert.AreEqual(a.Fingerprint, TestRules.Create().Fingerprint);
        }

        [Test]
        public void Invalid_rules_are_reported_separately()
        {
            Assert.Throws<InvalidRulesException>(() => TestRules.Create(v => { v.CriticalDiameter = v.InitialDiameter; return v; }));
            Assert.Throws<InvalidRulesException>(() => TestRules.Create(v => { v.MinShotCost = 0; return v; }));
            Assert.Throws<InvalidRulesException>(() => TestRules.Create(v => { v.ChargeRate = double.NaN; return v; }));
            Assert.Throws<InvalidRulesException>(() => TestRules.Create(v => { v.MinShotCost = 2; return v; }));
            Assert.Throws<InvalidRulesException>(() => new GameRules(RuleValues.Defaults, null));
        }

        [Test]
        public void Tight_budget_is_bounded_and_falls_back_to_a_validated_layout()
        {
            // A budget that only just allows a control shot: dense layouts fail, the fallback must still be proven.
            var rules = TestRules.Create(v => { v.CriticalDiameter = 1.45; v.MaxCandidates = 4; return v; });
            var watch = Stopwatch.StartNew();
            var level = new LevelGenerator().Generate(rules, 123);
            Assert.Less(watch.ElapsedMilliseconds, 5000);
            Assert.LessOrEqual(level.Control.TotalCost, rules.ControlBudgetLimit + rules.Tolerance);
            Assert.IsTrue(ControlReplay.Run(rules, level, level.Control.HoldSeconds).Success);
        }

        [Test]
        public void Accepted_levels_have_a_control_plan_within_the_budget([Values(0, 1)] int block)
        {
            AssertSeeds(TestRules.Create(), block * 100, 100);
        }

        [Test, Category("Slow")]
        public void Thousand_seeds_are_valid_on_the_test_rules()
        {
            AssertSeeds(TestRules.Create(), 1000, 1000);
        }

        internal static void AssertSeeds(GameRules rules, int firstSeed, int count)
        {
            var generator = new LevelGenerator();
            var failures = new StringBuilder();
            var fallbacks = 0;
            for (var seed = firstSeed; seed < firstSeed + count; seed++)
            {
                LevelData level;
                try { level = generator.Generate(rules, seed); }
                catch (System.Exception e) { failures.AppendLine($"seed {seed}: {e.Message}"); continue; }
                if (level.Report.UsedFallback) fallbacks++;
                if (level.Control.TotalCost > rules.ControlBudgetLimit + rules.Tolerance)
                    failures.AppendLine($"seed {seed}: cost {level.Control.TotalCost} > {rules.ControlBudgetLimit} ({rules.Fingerprint})");
                var replay = ControlReplay.Run(rules, level, level.Control.HoldSeconds);
                if (!replay.Success) failures.AppendLine($"seed {seed}: replay failed: {replay.Failure} ({rules.Fingerprint})");
            }
            UnityEngine.Debug.Log($"[Generator] seeds {firstSeed}..{firstSeed + count - 1}: fallbacks {fallbacks}, rules {rules.Fingerprint}");
            Assert.IsEmpty(failures.ToString());
        }
    }
}
