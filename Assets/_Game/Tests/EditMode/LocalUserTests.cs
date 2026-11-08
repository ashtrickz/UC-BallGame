using System;
using System.IO;
using Game.Users;
using NUnit.Framework;

namespace Game.Foundation.Tests
{
    public sealed class LocalUserTests
    {
        private string _directory;
        [SetUp] public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), "BallGameTests-" + Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }

        [Test]
        public void Profile_survives_restart_without_any_pending_operations()
        {
            var cache = new FileUserCache(_directory);
            var users = new UserService(new UserRepository(), cache);
            users.InitializeAsync(default).GetAwaiter().GetResult();
            var id = users.UserId;
            users.Submit(new UserChange { PlayerName = "Local Player", GoldDelta = 42 });
            var repository = new UserRepository();
            var restarted = new UserService(repository, cache);
            restarted.InitializeAsync(default).GetAwaiter().GetResult();
            Assert.AreEqual(id, restarted.UserId);
            Assert.AreEqual("Local Player", repository.Profile.PlayerName);
            Assert.AreEqual(42, repository.Profile.Gold);
            Assert.AreEqual(1, repository.Profile.Revision);
        }

        [Test]
        public void Invalid_change_or_overflow_does_not_change_persisted_or_published_state()
        {
            var repository = new UserRepository();
            var cache = new FileUserCache(_directory);
            var users = new UserService(repository, cache);
            users.SelectUser("local");
            users.Submit(new UserChange { GoldDelta = long.MaxValue });
            var before = repository.Profile;
            Assert.Throws<OverflowException>(() => users.Submit(new UserChange { GoldDelta = 1 }));
            Assert.Throws<InvalidOperationException>(() => users.Submit(new UserChange { PlayerName = " " }));
            Assert.AreSame(before, repository.Profile);
            Assert.AreEqual(long.MaxValue, cache.Load("local").Gold);
        }

        [Test]
        public void Failed_save_does_not_publish_a_change()
        {
            var cache = new FailingCache();
            var repository = new UserRepository();
            var users = new UserService(repository, cache);
            users.SelectUser("local");
            var before = repository.Profile;
            cache.Fail = true;
            Assert.Throws<IOException>(() => users.Submit(new UserChange { GoldDelta = 5 }));
            Assert.AreSame(before, repository.Profile);
            Assert.AreEqual(0, cache.Data.Gold);
        }

        [Test]
        public void Corrupted_primary_recovers_the_previous_valid_snapshot()
        {
            var cache = new FileUserCache(_directory);
            var users = new UserService(new UserRepository(), cache);
            users.SelectUser("local");
            users.Submit(new UserChange { GoldDelta = 7 });
            users.Submit(new UserChange { GoldDelta = 3 });
            File.WriteAllText(Directory.GetFiles(_directory, "*.json")[0], "broken JSON");
            Assert.AreEqual(7, cache.Load("local").Gold);
        }

        [Test]
        public void Accounts_are_isolated_and_cannot_escape_the_cache_directory()
        {
            var repository = new UserRepository();
            var users = new UserService(repository, new FileUserCache(_directory));
            users.SelectUser("../../first");
            users.Submit(new UserChange { GoldDelta = 9 });
            users.SelectUser("second");
            Assert.AreEqual(0, repository.Profile.Gold);
            users.SelectUser("../../first");
            Assert.AreEqual(9, repository.Profile.Gold);
            Assert.AreEqual(2, Directory.GetFiles(_directory, "*.json").Length);
        }

        private sealed class FailingCache : IUserCache
        {
            public bool Fail;
            public UserSaveData Data;
            public string LastUserId => Data?.UserId;
            public UserSaveData Load(string userId) => Data?.Copy();
            public void Save(UserSaveData data)
            {
                if (Fail) throw new IOException("disk full");
                Data = data.Copy();
            }
        }
    }
}
