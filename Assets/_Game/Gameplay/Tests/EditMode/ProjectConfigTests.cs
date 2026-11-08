using Game.Gameplay.Config;
using Game.Gameplay.Generation;
using NUnit.Framework;
using UnityEditor;

namespace Game.Gameplay.Tests
{
    /// <summary>Checks on the shipped configuration assets (final balance and baked crystal contours).</summary>
    public sealed class ProjectConfigTests
    {
        private const string ConfigPath = "Assets/_Game/Gameplay/Config/SlimeGameConfig.asset";

        private static SlimeGameConfig Config()
        {
            var config = AssetDatabase.LoadAssetAtPath<SlimeGameConfig>(ConfigPath);
            Assert.IsNotNull(config, "Run Tools/Ball Game/Gameplay/Create Or Update Assets");
            return config;
        }

        [Test]
        public void Baked_contours_match_the_visible_shards()
        {
            var catalog = Config().Crystals;
            Assert.IsNotNull(catalog);
            Assert.Greater(catalog.variants.Length, 0);
            foreach (var variant in catalog.variants)
            {
                var fresh = CrystalCatalog.ComputeContour(variant);
                Assert.AreEqual(fresh.Count, variant.contour.Length, variant.name);
                for (var i = 0; i < fresh.Count; i++)
                {
                    Assert.AreEqual(fresh.Vertices[i].X, variant.contour[i].x, 1e-5, $"{variant.name} vertex {i}");
                    Assert.AreEqual(fresh.Vertices[i].Z, variant.contour[i].y, 1e-5, $"{variant.name} vertex {i}");
                }
                Assert.That(fresh.Count, Is.InRange(5, 16), variant.name + " contour should be a compact convex footprint");
            }
        }

        [Test]
        public void Shipped_rules_are_valid()
        {
            var rules = Config().CreateSnapshot();
            Assert.Greater(rules.ControlBudgetLimit, rules.Values.MinShotCost);
        }

        [Test, Category("Slow")]
        public void Thousand_seeds_on_the_shipped_config_have_proven_control_plans()
        {
            GeneratorTests.AssertSeeds(Config().CreateSnapshot(), 1, 1000);
        }
    }
}
