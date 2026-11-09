using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Audio;
using Game.Integration.Audio;
using Game.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Foundation.Tests
{
    public sealed partial class AudioServiceTests
    {
        private sealed class MemoryAudioSettings : ISettingsRuntime
        {
            private readonly Dictionary<VolumeChannel, float> _volumes = new Dictionary<VolumeChannel, float>();
            public SettingsPack Pack => SettingsPack.Mobile;
            public ResolutionOption AppliedResolution => default;
            public int AppliedFps => 60;
            public IReadOnlyList<ResolutionOption> Resolutions => Array.Empty<ResolutionOption>();
            public IReadOnlyList<LanguageOption> Languages => Array.Empty<LanguageOption>();
            public int CursorCount => 0;
            public int CursorIndex => 0;
            public int TargetFps => 60;
            public int MaximumFps => 120;
            public ResolutionOption Resolution => default;
            public WindowMode WindowMode => WindowMode.Windowed;
            public string CurrentLanguage => "en";
            public float GetVolume(VolumeChannel channel) => _volumes.TryGetValue(channel, out var value) ? value : 1;
            public bool GetToggle(PreferenceToggle toggle) => false;
            public void SetVolume(VolumeChannel channel, float value) => _volumes[channel] = value;
            public void SetToggle(PreferenceToggle toggle, bool value) { }
            public void SetTargetFps(int value) { }
            public void SetCursor(int index) { }
            public void SetLanguage(LanguageOption language) { }
            public void SetDisplay(ResolutionOption resolution, WindowMode mode) { }
            public void ResetSetting(SettingId setting) { }
        }

        private sealed class CueFixture : IDisposable
        {
            public readonly AudioClip A = AudioClip.Create("Cue A", 48000 * 3, 1, 48000, false);
            public readonly AudioClip B = AudioClip.Create("Cue B", 48000 * 3, 1, 48000, false);
            public readonly AudioLibrary Library = ScriptableObject.CreateInstance<AudioLibrary>();
            public readonly SettingsService Settings = new SettingsService(new MemoryAudioSettings());
            public readonly AudioService Audio;
            private readonly GameObject _listener;

            public CueFixture()
            {
                if (Object.FindFirstObjectByType<AudioListener>() == null)
                    _listener = new GameObject("CueTest Listener", typeof(AudioListener));
                Audio = new AudioService(Settings, new UnusedAssets(), Library, null);
            }

            public void Dispose()
            {
                Audio.Dispose();
                Object.Destroy(A);
                Object.Destroy(B);
                Object.Destroy(Library);
                if (_listener != null) Object.Destroy(_listener);
            }
        }

        [UnityTest]
        public IEnumerator Cue_variation_gain_and_stale_handles_use_the_authored_settings() => UniTask.ToCoroutine(async () =>
        {
            var fixture = new CueFixture();
            var randomState = UnityEngine.Random.state;
            try
            {
                fixture.Library.Sounds = new[] { new SoundDefinition
                {
                    id = GameSound.SlimeJump, clip = fixture.A, variants = new[] { fixture.B },
                    volume = 0.6f, pitchRange = new Vector2(0.9f, 1.1f), group = AudioGroup.Sfx,
                    maxVoices = 2
                } };
                fixture.Settings.SetVolume(VolumeChannel.Master, 0.8f);
                fixture.Settings.SetVolume(VolumeChannel.Sfx, 0.25f);
                await fixture.Audio.InitializeAsync(default);
                AudioClip lastClip = null;
                var lastPitch = float.NaN;
                AudioPlaybackHandle stale = null;
                for (var i = 0; i < 16; i++)
                {
                    var handle = fixture.Audio.PlaySound(GameSound.SlimeJump);
                    Assert.IsNotNull(handle);
                    Assert.AreNotEqual(lastClip, handle.Clip, "Take must not immediately repeat.");
                    Assert.That(handle.Pitch, Is.InRange(0.9f, 1.1f));
                    if (!float.IsNaN(lastPitch)) Assert.Greater(Mathf.Abs(handle.Pitch - lastPitch), 0.001f);
                    Assert.AreEqual(0.12f, handle.Volume, 0.00001f);
                    lastClip = handle.Clip;
                    lastPitch = handle.Pitch;
                    handle.Stop();
                    stale = handle;
                }
                var current = fixture.Audio.PlaySound(GameSound.SlimeJump);
                current.SetVolume(0.5f);
                Assert.AreEqual(0.06f, current.Volume, 0.00001f);
                fixture.Settings.SetVolume(VolumeChannel.Sfx, 0.5f);
                Assert.AreEqual(0.12f, current.Volume, 0.00001f, "Settings update retains the cue/handle gains.");
                stale.SetVolume(0);
                stale.SetPitch(2);
                stale.Stop();
                Assert.IsTrue(current.IsActive);
                Assert.AreEqual(0.12f, current.Volume, 0.00001f);
                Assert.That(current.Pitch, Is.InRange(0.9f, 1.1f));
                current.Stop();

                UnityEngine.Random.InitState(731);
                var expected = UnityEngine.Random.Range(1, int.MaxValue);
                UnityEngine.Random.InitState(731);
                fixture.Audio.PlaySound(GameSound.SlimeJump)?.Stop();
                Assert.AreEqual(expected, UnityEngine.Random.Range(1, int.MaxValue), "Audio must not consume Unity/gameplay RNG.");
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                fixture.Dispose();
                await UniTask.Yield();
            }
        });

        [UnityTest]
        public IEnumerator Gameplay_pause_freezes_the_cursor_and_keeps_UI_and_manual_pause_independent() => UniTask.ToCoroutine(async () =>
        {
            var fixture = new CueFixture();
            try
            {
                fixture.Library.Sounds = new[]
                {
                    new SoundDefinition { id = GameSound.ChargeLoop, clip = fixture.A, loop = true, group = AudioGroup.Sfx },
                    new SoundDefinition { id = GameSound.UiClick, clip = fixture.B, group = AudioGroup.UI }
                };
                await fixture.Audio.InitializeAsync(default);
                var gameplay = fixture.Audio.PlaySound(GameSound.ChargeLoop);
                var ui = fixture.Audio.PlaySound(GameSound.UiClick);
                await UniTask.Delay(80, ignoreTimeScale: true);
                fixture.Audio.SetGameplayPaused(true);
                var position = gameplay.TimeSamples;
                Assert.IsTrue(gameplay.IsPaused);
                Assert.IsTrue(ui.IsPlaying);
                await UniTask.Delay(80, ignoreTimeScale: true);
                Assert.AreEqual(position, gameplay.TimeSamples, "Pause preserves the playback cursor.");
                gameplay.Pause();
                fixture.Audio.SetGameplayPaused(false);
                Assert.IsTrue(gameplay.IsPaused, "Global resume must not undo an owner's pause.");
                gameplay.Resume();
                Assert.IsFalse(gameplay.IsPaused);
                Assert.IsTrue(gameplay.IsPlaying);
                Assert.IsTrue(ui.IsPlaying);
                fixture.Audio.StopGroup(AudioGroup.Sfx);
                Assert.IsFalse(gameplay.IsActive);
                Assert.IsTrue(ui.IsPlaying);
            }
            finally { fixture.Dispose(); await UniTask.Yield(); }
        });

        [UnityTest]
        public IEnumerator Voice_limit_and_cooldown_prevent_repeated_overlap() => UniTask.ToCoroutine(async () =>
        {
            var fixture = new CueFixture();
            try
            {
                fixture.Library.Sounds = new[] { new SoundDefinition
                {
                    id = GameSound.CrystalBreakSmall, clip = fixture.A,
                    group = AudioGroup.Sfx, minInterval = 0.15f, maxVoices = 1
                } };
                await fixture.Audio.InitializeAsync(default);
                var first = fixture.Audio.PlaySound(GameSound.CrystalBreakSmall);
                Assert.IsNotNull(first);
                Assert.IsNull(fixture.Audio.PlaySound(GameSound.CrystalBreakSmall));
                await UniTask.Delay(180, ignoreTimeScale: true);
                Assert.IsNull(fixture.Audio.PlaySound(GameSound.CrystalBreakSmall), "Voice limit still applies after the cooldown.");
                first.Stop();
                Assert.IsNotNull(fixture.Audio.PlaySound(GameSound.CrystalBreakSmall));
            }
            finally { fixture.Dispose(); await UniTask.Yield(); }
        });

        [Test]
        public void Library_lookup_uses_the_populated_entry_after_an_empty_duplicate()
        {
            var fixture = new CueFixture();
            try
            {
                var placeholder = new SoundDefinition { id = GameSound.MainMenuMusic };
                var authored = new SoundDefinition { id = GameSound.MainMenuMusic, variants = new[] { fixture.A } };
                fixture.Library.Sounds = new[] { placeholder, authored };
                Assert.IsTrue(fixture.Library.TryGet(GameSound.MainMenuMusic, out var resolved));
                Assert.AreSame(authored, resolved);
                Assert.IsFalse(fixture.Library.TryGet(GameSound.GameplayMusic, out _));
                fixture.Library.Sounds = new[] { placeholder };
                Assert.IsTrue(fixture.Library.TryGet(GameSound.MainMenuMusic, out resolved));
                Assert.AreSame(placeholder, resolved);
            }
            finally { fixture.Dispose(); }
        }

        [UnityTest]
        public IEnumerator Music_themes_retry_missing_clips_switch_and_keep_the_current_track_playing() => UniTask.ToCoroutine(async () =>
        {
            var fixture = new CueFixture();
            try
            {
                await fixture.Audio.InitializeAsync(default); // Main menu has no assigned clip yet.
                fixture.Library.Sounds = new[]
                {
                    new SoundDefinition { id = GameSound.MainMenuMusic },
                    new SoundDefinition { id = GameSound.MainMenuMusic, clip = fixture.A,
                        group = AudioGroup.Music, volume = 0.4f, pitchRange = Vector2.one, loop = true },
                    new SoundDefinition { id = GameSound.GameplayMusic, clip = fixture.B,
                        group = AudioGroup.Music, volume = 0.6f, pitchRange = Vector2.one, loop = true }
                };
                fixture.Settings.SetVolume(VolumeChannel.Master, 0.8f);
                fixture.Settings.SetVolume(VolumeChannel.Music, 0.5f);
                fixture.Audio.ChangeTheme(MusicTheme.MainMenu);
                var source = Array.Find(Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None), s => s.clip == fixture.A);
                Assert.IsNotNull(source, "Retry must start a theme that was previously empty.");
                Assert.IsTrue(source.isPlaying);
                Assert.IsTrue(source.loop);
                Assert.AreEqual(0.16f, source.volume, 0.00001f);
                await UniTask.Delay(80, ignoreTimeScale: true);
                var cursor = source.timeSamples;
                Assert.Greater(cursor, 0);
                fixture.Audio.ChangeTheme(MusicTheme.MainMenu);
                Assert.GreaterOrEqual(source.timeSamples, cursor, "Repeated scene initialization must not restart an active theme.");
                fixture.Audio.ChangeTheme(MusicTheme.Gameplay);
                Assert.AreSame(fixture.B, source.clip);
                Assert.IsTrue(source.isPlaying);
                Assert.AreEqual(0.24f, source.volume, 0.00001f);
                Assert.AreEqual(1, Array.FindAll(Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None),
                    s => s.clip == fixture.A || s.clip == fixture.B).Length, "Themes share one music source.");
                fixture.Audio.SetGameplayPaused(true);
                Assert.IsTrue(source.isPlaying, "Gameplay pause leaves background music running.");
                fixture.Audio.ChangeTheme(MusicTheme.MainMenu);
                Assert.AreSame(fixture.A, source.clip);
                fixture.Audio.StopGroup(AudioGroup.Music);
                Assert.IsNull(source.clip);
            }
            finally { fixture.Dispose(); await UniTask.Yield(); }
        });

        [Test]
        public void Selected_library_contains_individual_chosen_hits_and_all_gameplay_cues()
        {
            var library = Resources.Load<AudioLibrary>(AudioLibrary.ResourcePath);
            Assert.IsNotNull(library);
            var ids = new HashSet<GameSound>();
            foreach (var cue in library.Sounds)
                if (cue != null) Assert.IsTrue(ids.Add(cue.id), "Duplicate audio ID: " + cue.id);
            foreach (var id in new[] { GameSound.SlimeJump, GameSound.SlimeShoot, GameSound.ChargeStart,
                GameSound.ChargeLoop, GameSound.ProjectileImpact, GameSound.CrystalBreakSmall,
                GameSound.GateOpen, GameSound.Victory, GameSound.Defeat, GameSound.UiClick })
            {
                Assert.IsTrue(library.TryGet(id, out var cue), id.ToString());
                Assert.Greater(cue.ClipCount, 0, id.ToString());
            }
            Assert.IsTrue(library.TryGet(GameSound.SlimeJump, out var jump));
            Assert.AreEqual(3, jump.ClipCount);
            Assert.IsTrue(library.TryGet(GameSound.SlimeShoot, out var shoot));
            Assert.AreEqual(2, shoot.ClipCount);
            for (var i = 0; i < jump.ClipCount; i++) Assert.Less(jump.GetClip(i).length, 0.4f, "Do not import the three-hit audition montage.");
            for (var i = 0; i < shoot.ClipCount; i++) Assert.Less(shoot.GetClip(i).length, 0.6f, "Do not import the two-hit audition montage.");
        }
    }
}
