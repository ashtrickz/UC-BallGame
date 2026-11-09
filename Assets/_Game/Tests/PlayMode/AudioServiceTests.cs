using Slime.Foundation;
using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Slime.Assets;
using Slime.Audio;
using Slime.Gameplay.Presentation;
using Slime.Infrastructure.Audio;
using Slime.Infrastructure.Settings;
using Slime.Settings;
using UnityEngine;
using UnityEngine.TestTools;

namespace Slime.Tests.PlayMode
{
    public sealed partial class AudioServiceTests
    {
        private static AudioService CreateAudio(ISettingsService settings, IAssetService assets, AudioLibrary library,
            UnityEngine.Audio.AudioMixer mixer)
        {
            var configuration = new AudioReferences(library, mixer, false);
            var provider = new AudioLibraryProvider(configuration);
            var variation = new AudioVariation(new AudioRandom());
            var volumes = new AudioMixerVolumes(settings, provider, configuration);
            var sources = new AudioSourcePool(volumes);
            var music = new MusicPlayer(assets, provider, volumes, sources, variation);
            return new AudioService(assets, provider, sources, music, volumes, variation);
        }

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
            var audio = CreateAudio(new SettingsService(new UnitySettingsRuntime(SettingsPack.Mobile, new LanguageOptionsProvider(), new NullDisplaySettings())), new UnusedAssets(), null, null);
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
