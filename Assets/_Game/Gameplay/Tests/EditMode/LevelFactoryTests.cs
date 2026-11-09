using System;
using System.Collections.Generic;
using NUnit.Framework;
using Slime.Gameplay.Config;
using Slime.Gameplay.Core.Config;
using Slime.Gameplay.Core.Generation;
using Slime.Gameplay.Core.Levels;
using UnityEditor;

namespace Slime.Gameplay.Tests.EditMode
{
    /// <summary>Level requests are built by the source registered for their type, with seeds from the seed provider.</summary>
    public sealed class LevelFactoryTests
    {
        private const string ConfigPath = "Assets/_Game/Gameplay/Config/SlimeGameConfig.asset";
        private const int RandomSeed = 12345;
        private const int NoFixedSeed = 0;
        private const int FixedSeed = 777;
        private const int ExplicitSeed = 4242;
        private const int CampaignLevel = 3;

        private sealed class UnknownRequest : LevelRequest
        {
        }

        private sealed class QueueSeeds : ISeedProvider
        {
            private readonly Queue<int> _seeds;

            public QueueSeeds(params int[] seeds)
            {
                _seeds = new Queue<int>(seeds);
            }

            public int Next()
            {
                return _seeds.Dequeue();
            }
        }

        private static SlimeGameConfig Config()
        {
            var config = AssetDatabase.LoadAssetAtPath<SlimeGameConfig>(ConfigPath);
            Assert.IsNotNull(config);

            return config;
        }

        private static LevelFactory Factory(ISeedProvider seeds)
        {
            var config = Config();

            return new LevelFactory(new ILevelSource[]
            {
                new CampaignLevelSource(config),
                new WallLevelSource(config, new WallGenerator(), seeds),
                new EndlessLevelSource(config, new LevelGenerator(), seeds),
            });
        }

        [Test]
        public void Campaign_request_builds_the_same_layout_every_time()
        {
            var factory = Factory(new QueueSeeds());

            var first = factory.Build(new CampaignLevelRequest(CampaignLevel));
            var second = factory.Build(new CampaignLevelRequest(CampaignLevel));

            Assert.IsInstanceOf<CampaignLevelRequest>(first.Request);
            Assert.AreEqual(first.Level.Crystals.Count, second.Level.Crystals.Count);
            for (var i = 0; i < first.Level.Crystals.Count; i++)
                Assert.AreEqual(first.Level.Crystals[i].Position, second.Level.Crystals[i].Position);

            Assert.IsNotNull(first.Level.Control, "campaign levels carry their proven control sequence");
        }

        [Test]
        public void Campaign_request_outside_the_campaign_is_rejected()
        {
            var factory = Factory(new QueueSeeds());
            var beyond = Config().Campaign.Count + 1;

            Assert.Throws<ArgumentOutOfRangeException>(() => factory.Build(new CampaignLevelRequest(beyond)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignLevelRequest(0));
        }

        [Test]
        public void Generated_requests_draw_a_seed_only_when_none_is_given()
        {
            var factory = Factory(new QueueSeeds(11, 22));

            var drawnWall = factory.Build(new WallLevelRequest(null));
            var drawnEndless = factory.Build(new EndlessLevelRequest(null));
            var explicitWall = factory.Build(new WallLevelRequest(ExplicitSeed));

            Assert.AreEqual(11, drawnWall.Seed);
            Assert.AreEqual(22, drawnEndless.Seed);
            Assert.AreEqual(ExplicitSeed, explicitWall.Seed);
            Assert.AreEqual(ExplicitSeed, explicitWall.Level.Seed);
        }

        [Test]
        public void Same_seed_builds_the_same_wall()
        {
            var factory = Factory(new QueueSeeds());

            var first = factory.Build(new WallLevelRequest(ExplicitSeed)).Level;
            var second = factory.Build(new WallLevelRequest(ExplicitSeed)).Level;

            Assert.AreEqual(first.Crystals.Count, second.Crystals.Count);
            Assert.AreEqual(first.Control.TotalCost, second.Control.TotalCost);
        }

        [Test]
        public void Unknown_request_type_and_duplicate_sources_are_errors()
        {
            var factory = Factory(new QueueSeeds());
            var config = Config();

            Assert.Throws<InvalidOperationException>(() => factory.Build(new UnknownRequest()));
            Assert.Throws<ArgumentNullException>(() => factory.Build(null));
            Assert.Throws<InvalidOperationException>(() => new LevelFactory(new ILevelSource[]
            {
                new CampaignLevelSource(config),
                new CampaignLevelSource(config),
            }));
        }

        [Test]
        public void Seed_provider_returns_the_debug_seed_or_new_positive_seeds()
        {
            var fixedSeeds = new SeedProvider(FixedSeed, RandomSeed);
            var randomSeeds = new SeedProvider(NoFixedSeed, RandomSeed);

            Assert.AreEqual(FixedSeed, fixedSeeds.Next());
            Assert.AreEqual(FixedSeed, fixedSeeds.Next());
            Assert.Greater(randomSeeds.Next(), 0);
            Assert.AreNotEqual(randomSeeds.Next(), randomSeeds.Next());
        }
    }
}
