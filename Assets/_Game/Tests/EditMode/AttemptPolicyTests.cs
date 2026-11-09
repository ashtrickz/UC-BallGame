using Slime.Foundation;
using System.Threading;
using System;
using NUnit.Framework;
using Slime.App.Game;
using Slime.Gameplay.Config;
using Slime.Gameplay.Core.Config;
using Slime.Gameplay.Core.Generation;
using Slime.Gameplay.Core.Levels;
using Slime.Composition;
using Slime.Diagnostics;
using Slime.Gameplay.Presentation;
using Slime.Localization;
using UnityEditor;

namespace Slime.Tests.EditMode
{
    /// <summary>Mode policies decide progress, retry, next level, HUD label and result choices without any mode switch in the controller.</summary>
    public sealed class AttemptPolicyTests
    {
        private const string ConfigPath = "Assets/_Game/Gameplay/Config/SlimeGameConfig.asset";
        private const int Seed = 4242;
        private const int TutorialLevel = 1;
        private const int MiddleLevel = 2;

        private sealed class KeyLocalization : ILocalizationService
        {
            public event Action Changed
            {
                add { }
                remove { }
            }

            public string CurrentLanguage => "en";

            public string Get(string key)
            {
                return key;
            }
        }

        private sealed class Progress : ICampaignProgress
        {
            public int CompletedLevel;
            public int CompletedCount;

            public int Unlocked => int.MaxValue;

            public void MarkCompleted(int level, int levelCount)
            {
                CompletedLevel = level;
                CompletedCount = levelCount;
            }

            public void Reset()
            {
            }
        }

        private sealed class FixedSeeds : ISeedProvider
        {
            public int Next()
            {
                return Seed;
            }
        }

        private SlimeGameConfig _config;
        private Progress _progress;
        private LevelFactory _levels;
        private AttemptFactory _attempts;

        [SetUp]
        public void SetUp()
        {
            _config = AssetDatabase.LoadAssetAtPath<SlimeGameConfig>(ConfigPath);
            Assert.IsNotNull(_config);
            _progress = new Progress();
            var seeds = new FixedSeeds();
            _levels = new LevelFactory(new ILevelSource[]
            {
                new CampaignLevelSource(_config),
                new WallLevelSource(_config, new WallGenerator(), seeds),
                new EndlessLevelSource(_config, new LevelGenerator(), seeds),
            });
            var localization = new KeyLocalization();
            _attempts = new AttemptFactory(new IAttemptModePolicy[]
            {
                new CampaignAttemptPolicy(_config, _progress, localization),
                new WallAttemptPolicy(localization),
                new EndlessAttemptPolicy(localization),
            });
        }

        private int LastLevel => _config.Campaign.Count;

        private AttemptContext Attempt(LevelRequest request)
        {
            return _attempts.Create(_levels.Build(request), CancellationToken.None);
        }

        private static int Number(LevelRequest request)
        {
            return ((CampaignLevelRequest)request).Number;
        }

        [Test]
        public void Factory_numbers_attempts_and_picks_the_policy_by_request_type()
        {
            var campaign = Attempt(new CampaignLevelRequest(TutorialLevel));
            var wall = Attempt(new WallLevelRequest(Seed));
            var endless = Attempt(new EndlessLevelRequest(Seed));

            Assert.AreEqual(campaign.Id + 1, wall.Id);
            Assert.AreEqual(wall.Id + 1, endless.Id);
            Assert.AreEqual(wall.Id, wall.Model.AttemptId);
            Assert.IsInstanceOf<CampaignAttemptPolicy>(campaign.Policy);
            Assert.IsInstanceOf<WallAttemptPolicy>(wall.Policy);
            Assert.IsInstanceOf<EndlessAttemptPolicy>(endless.Policy);
            Assert.Throws<InvalidOperationException>(() => new AttemptFactory(new IAttemptModePolicy[]
            {
                new WallAttemptPolicy(new KeyLocalization()),
                new WallAttemptPolicy(new KeyLocalization()),
            }));
        }

        [Test]
        public void Campaign_records_the_win_and_offers_the_next_level_or_a_retry()
        {
            var attempt = Attempt(new CampaignLevelRequest(MiddleLevel));
            var policy = attempt.Policy;

            policy.RecordWin(attempt);
            var won = policy.Won(attempt);
            var lost = policy.Lost(attempt);

            Assert.AreEqual(MiddleLevel, _progress.CompletedLevel);
            Assert.AreEqual(LastLevel, _progress.CompletedCount);

            Assert.AreEqual(MiddleLevel + 1, Number(won.Primary.Request));

            Assert.AreEqual(MiddleLevel, Number(won.Secondary.Request));

            Assert.AreEqual(MiddleLevel, Number(lost.Primary.Request));
            Assert.IsTrue(lost.Secondary.IsNone);
            Assert.AreEqual("hud.level", policy.Label(attempt));
            Assert.IsFalse(policy.OffersTutorial(attempt));
            Assert.IsTrue(policy.OffersTutorial(Attempt(new CampaignLevelRequest(TutorialLevel))));
        }

        [Test]
        public void Last_campaign_level_completes_the_campaign()
        {
            var attempt = Attempt(new CampaignLevelRequest(LastLevel));
            var policy = attempt.Policy;

            var won = policy.Won(attempt);

            Assert.AreEqual("result.campaign_complete", won.Title);

            Assert.IsInstanceOf<WallLevelRequest>(won.Primary.Request);
            Assert.IsNull(((WallLevelRequest)won.Primary.Request).Seed);

            Assert.AreEqual(LastLevel, Number(won.Secondary.Request));
            Assert.IsInstanceOf<EndlessLevelRequest>(policy.Next(attempt));
        }

        [Test]
        public void Wall_win_builds_a_new_wall_and_defeat_offers_the_same_one()
        {
            var attempt = Attempt(new WallLevelRequest(Seed));
            var policy = attempt.Policy;

            policy.RecordWin(attempt);
            var won = policy.Won(attempt);
            var lost = policy.Lost(attempt);

            Assert.AreEqual(0, _progress.CompletedLevel, "walls never touch the campaign progress");

            Assert.IsNull(((WallLevelRequest)won.Primary.Request).Seed);

            Assert.AreEqual(Seed, ((WallLevelRequest)won.Secondary.Request).Seed);

            Assert.AreEqual(Seed, ((WallLevelRequest)lost.Primary.Request).Seed);

            Assert.IsNull(((WallLevelRequest)policy.Retry(attempt)).Seed);
            Assert.AreEqual("hud.wall_seed", policy.Label(attempt));
        }

        [Test]
        public void Endless_win_and_retry_draw_a_new_seed()
        {
            var attempt = Attempt(new EndlessLevelRequest(Seed));
            var policy = attempt.Policy;

            policy.RecordWin(attempt);
            var won = policy.Won(attempt);
            var lost = policy.Lost(attempt);

            Assert.AreEqual(0, _progress.CompletedLevel);

            Assert.IsNull(((EndlessLevelRequest)won.Primary.Request).Seed);
            Assert.IsTrue(won.Secondary.IsNone);

            Assert.IsNull(((EndlessLevelRequest)lost.Primary.Request).Seed);
            Assert.IsTrue(lost.Secondary.IsNone);
            Assert.AreEqual("hud.endless_seed", policy.Label(attempt));
        }
    }
}
