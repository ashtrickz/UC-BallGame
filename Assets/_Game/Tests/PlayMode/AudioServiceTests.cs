using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Assets;
using Game.Audio;
using Game.Integration.Audio;
using Game.Integration.Settings;
using Game.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Foundation.Tests
{
    public sealed partial class AudioServiceTests
    {
        private sealed class UnusedAssets : IAssetService
        {
            public UniTask<AssetLease<T>> LoadAsync<T>(string key, CancellationToken token = default) where T : UnityEngine.Object =>
                throw new InvalidOperationException("No library clips should be loaded in this test.");
        }

        [Test]
        public void Playback_handle_checks_its_own_slot_and_cannot_stop_a_reused_source()
        {
            var root = new GameObject("HandleTest");
            try
            {
                var source = root.AddComponent<AudioSource>();
                var versions = new[] { 8, 2 };
                var handle = new AudioPlaybackHandle(source, 2, versions, 1);
                Assert.IsTrue(handle.IsActive);
                versions[0]++;
                Assert.IsTrue(handle.IsActive);
                versions[1]++;
                Assert.IsFalse(handle.IsActive);
                handle.Stop();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator Stopping_one_group_keeps_other_groups_playing() => UniTask.ToCoroutine(async () =>
        {
            var previousFps = UnityEngine.Application.targetFrameRate;
            var previousVsync = QualitySettings.vSyncCount;
            var clip = AudioClip.Create("AudioServiceTest", 44100 * 3, 1, 44100, false);
            var listener = new GameObject("TestListener", typeof(AudioListener));
            var audio = new AudioService(new SettingsService(new UnitySettingsRuntime(SettingsPack.Mobile)), new UnusedAssets(), null, null);
            try
            {
                await audio.InitializeAsync(default);
                var ui = audio.PlayClip(clip, AudioGroup.UI);
                var sfx = audio.PlayClip(clip, AudioGroup.Sfx);
                var music = audio.PlayClip(clip, AudioGroup.Music);
                await UniTask.Yield();
                Assert.IsTrue(ui.IsPlaying);
                Assert.IsTrue(sfx.IsPlaying);
                audio.StopGroup(AudioGroup.Sfx);
                Assert.IsTrue(ui.IsPlaying);
                Assert.IsFalse(sfx.IsPlaying);
                Assert.IsFalse(sfx.IsActive);
                audio.StopGroup(AudioGroup.Music);
                Assert.IsFalse(music.IsPlaying);
                Assert.IsFalse(music.IsActive);
                Assert.IsTrue(ui.IsPlaying);
            }
            finally
            {
                audio.Dispose();
                UnityEngine.Application.targetFrameRate = previousFps;
                QualitySettings.vSyncCount = previousVsync;
                UnityEngine.Object.Destroy(listener);
                UnityEngine.Object.Destroy(clip);
                await UniTask.Yield();
            }
        });
    }
}
