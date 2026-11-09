using Slime.Foundation;
using System;
using NUnit.Framework;
using Slime.App.Game;
using UnityEngine;

namespace Slime.Tests.EditMode
{
    /// <summary>Best local star results survive service recreation and cannot be lowered by replays or defeats.</summary>
    public sealed class StarProgressTests
    {
        private string _key;

        [SetUp]
        public void SetUp()
        {
            _key = "test-" + Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey("stars.v1." + _key);
        }

        [Test]
        public void Better_results_persist_and_worse_results_cannot_replace_them()
        {
            var progress = new PlayerPrefsStarProgress();
            Assert.AreEqual(0, progress.Best(_key));
            progress.Record(_key, 2);
            progress.Record(_key, 1);
            progress.Record(_key, 0);
            Assert.AreEqual(2, new PlayerPrefsStarProgress().Best(_key));
            progress.Record(_key, 3);
            Assert.AreEqual(3, new PlayerPrefsStarProgress().Best(_key));
            Assert.AreEqual(0, progress.Best(_key + "-another-level"));
        }
    }
}
