using Game.Integration.Settings;
using Game.Settings;
using NUnit.Framework;
using UnityEngine;

namespace Game.Foundation.Tests
{
    public sealed class SettingsRuntimeTests
    {
        private bool _hadFps, _hadVolume, _hadLanguage;
        private int _fps, _targetFps, _vsync;
        private float _volume;
        private string _language;

        [SetUp]
        public void SetUp()
        {
            _hadFps = PlayerPrefs.HasKey("Settings.TargetFps");
            _hadVolume = PlayerPrefs.HasKey("Settings.Volume.Master");
            _hadLanguage = PlayerPrefs.HasKey("Settings.Language");
            _fps = PlayerPrefs.GetInt("Settings.TargetFps");
            _volume = PlayerPrefs.GetFloat("Settings.Volume.Master");
            _language = PlayerPrefs.GetString("Settings.Language");
            _targetFps = UnityEngine.Application.targetFrameRate;
            _vsync = QualitySettings.vSyncCount;
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadFps)
                PlayerPrefs.SetInt("Settings.TargetFps", _fps);
            else
                PlayerPrefs.DeleteKey("Settings.TargetFps");
            if (_hadVolume)
                PlayerPrefs.SetFloat("Settings.Volume.Master", _volume);
            else
                PlayerPrefs.DeleteKey("Settings.Volume.Master");
            if (_hadLanguage)
                PlayerPrefs.SetString("Settings.Language", _language);
            else
                PlayerPrefs.DeleteKey("Settings.Language");
            UnityEngine.Application.targetFrameRate = _targetFps;
            QualitySettings.vSyncCount = _vsync;
        }

        [Test]
        public void Invalid_saved_values_are_sanitized_before_applying_them()
        {
            PlayerPrefs.SetInt("Settings.TargetFps", int.MaxValue);
            PlayerPrefs.SetFloat("Settings.Volume.Master", float.NaN);
            PlayerPrefs.SetString("Settings.Language", "unsupported");
            var runtime = new UnitySettingsRuntime(SettingsPack.Mobile, new LanguageOptionsProvider(), new NullDisplaySettings());
            Assert.AreEqual(120, runtime.TargetFps);
            Assert.AreEqual(120, UnityEngine.Application.targetFrameRate);
            Assert.AreEqual(1f, runtime.GetVolume(VolumeChannel.Master));
            Assert.AreEqual("en", runtime.CurrentLanguage);
            Assert.AreEqual(0, QualitySettings.vSyncCount);
        }

        [Test]
        public void Changed_values_survive_runtime_recreation_and_stay_within_pack_limits()
        {
            var settings = new SettingsService(new UnitySettingsRuntime(SettingsPack.Mobile, new LanguageOptionsProvider(), new NullDisplaySettings()));
            settings.SetTargetFps(900);
            settings.SetVolume(VolumeChannel.Master, -2f);
            var restored = new UnitySettingsRuntime(SettingsPack.Mobile, new LanguageOptionsProvider(), new NullDisplaySettings());
            Assert.AreEqual(120, restored.TargetFps);
            Assert.AreEqual(0, restored.GetVolume(VolumeChannel.Master));
            settings.SetVolume(VolumeChannel.Master, 0.5f);
            settings.SetVolume(VolumeChannel.Master, float.PositiveInfinity);
            Assert.AreEqual(0.5f, settings.GetVolume(VolumeChannel.Master));
        }
    }
}
