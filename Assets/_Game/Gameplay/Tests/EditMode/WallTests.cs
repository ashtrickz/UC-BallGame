using System.Diagnostics;
using System.Text;
using NUnit.Framework;
using Slime.Gameplay.Config;
using Slime.Gameplay.Core.Config;
using Slime.Gameplay.Core.Generation;
using Slime.Gameplay.Core.Level;
using Slime.Gameplay.Core.Model;
using UnityEditor;

namespace Slime.Gameplay.Tests.EditMode
{
    /// <summary>The generated crystal wall: reproducible, really blocking, and always proven passable within B / 1.2.</summary>
    public sealed class WallTests
    {
        private static SlimeGameConfig Config() =>
            AssetDatabase.LoadAssetAtPath<SlimeGameConfig>("Assets/_Game/Gameplay/Config/SlimeGameConfig.asset");

        private static string Dump(LevelData level)
        {
            var sb = new StringBuilder(level.Describe());
            foreach (var c in level.Crystals) sb.Append($"|{c.VariantId}:{c.RotationStep}:{c.Scale:R}:{c.Position.X:R}:{c.Position.Z:R}");
            foreach (var h in level.Control.HoldSeconds) sb.Append(';').Append(h.ToString("R"));
            return sb.ToString();
        }

        [Test]
        public void Same_seed_builds_the_same_wall_and_plan()
        {
            var config = Config();
            var generator = new WallGenerator();
            var a = generator.Generate(config.CreateSnapshot(), config.Wall, 77, out _);
            var b = new WallGenerator().Generate(config.CreateSnapshot(), config.Wall, 77, out _);
            Assert.AreEqual(Dump(a), Dump(b));
            Assert.AreNotEqual(Dump(a), Dump(generator.Generate(config.CreateSnapshot(), config.Wall, 78, out _)));
        }

        [Test]
        public void Wall_fills_the_frame_and_blocks_the_way()
        {
            var config = Config();
            var level = new WallGenerator().Generate(config.CreateSnapshot(), config.Wall, 5, out var rules);
            Assert.Greater(level.Crystals.Count, 200, "the whole field is covered");
            var core = 0;
            foreach (var c in level.Crystals) if (c.Role == CrystalRole.Group) core++;
            Assert.Greater(core, 30, "a dense blocking band");
            var first = ShotRules.ComputeAdvance(rules, level.CreateField(), level.StartZ, rules.D0);
            Assert.IsFalse(first.ReachesFinish);
            Assert.Less(first.ToZ, config.Wall.WallStartZ + 0.5, "the slime stops at the wall");
        }

        [Test, Category("Slow")]
        public void Two_hundred_walls_are_proven_within_the_budget()
        {
            var config = Config();
            var generator = new WallGenerator();
            var failures = new StringBuilder();
            var watch = Stopwatch.StartNew();
            long worst = 0;
            var fallbacks = 0;
            for (var seed = 1; seed <= 200; seed++)
            {
                var start = watch.ElapsedMilliseconds;
                var level = generator.Generate(config.CreateSnapshot(), config.Wall, seed, out var rules);
                worst = System.Math.Max(worst, watch.ElapsedMilliseconds - start);
                if (level.Report.UsedFallback) fallbacks++;
                var replay = ControlReplay.Run(rules, level, level.Control.HoldSeconds);
                if (!replay.Success) failures.AppendLine($"seed {seed}: {replay.Failure}");
                if (replay.Spent * GameRules.BudgetReserve > rules.Budget + 1e-9) failures.AppendLine($"seed {seed}: reserve below 20%");
            }
            UnityEngine.Debug.Log($"[Wall] 200 seeds: fallbacks {fallbacks}, worst {worst} ms, total {watch.ElapsedMilliseconds} ms");
            Assert.IsEmpty(failures.ToString());
        }
    }
}
