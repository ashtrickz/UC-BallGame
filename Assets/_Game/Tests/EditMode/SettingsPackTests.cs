using Game.Settings;
using NUnit.Framework;

namespace Game.Settings.Tests
{
    public class SettingsPackTests
    {
        [Test]
        public void BothPacks_DefaultTo60Fps()
        {
            Assert.AreEqual(60, SettingsPack.Mobile.DefaultFps);
            Assert.AreEqual(60, SettingsPack.Desktop.DefaultFps);
        }

        [TestCase(30, 45, 45)]    // the limit below 60
        [TestCase(90, 120, 90)]   // the limit above 60
        [TestCase(30, 60, 60)]
        public void DefaultFps_StaysInsideThePacksLimits(int minimum, int maximum, int expected) =>
            Assert.AreEqual(expected, new SettingsPack(minimum, maximum).DefaultFps);

        [Test]
        public void Mobile_ShowsTheFrameRateSetting() => Assert.IsTrue(SettingsPack.Mobile.Contains(SettingId.TargetFps));
    }
}
