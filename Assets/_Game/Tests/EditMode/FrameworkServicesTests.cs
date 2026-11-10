using Slime.Foundation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Slime.App.Loading;
using Slime.App.Scenes;
using Slime.Audio;
using Slime.Gameplay.Presentation;
using Slime.Infrastructure.Audio;
using Slime.Composition;
using Slime.Infrastructure.Frame;
using Slime.Infrastructure.Settings;
using Slime.Settings;
using Slime.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;

namespace Slime.Tests.EditMode
{
    /// <summary>Protects feedback bindings, scene context and source gains during framework decomposition.</summary>
    public sealed class FrameworkServicesTests
    {
        [TestCase(AudioGroup.UI, 0.1f)]
        [TestCase(AudioGroup.Button, 0.1f)]
        [TestCase(AudioGroup.Sfx, 0.3f)]
        [TestCase(AudioGroup.Ambience, 0.3f)]
        [TestCase(AudioGroup.Music, 0.2f)]
        [TestCase(AudioGroup.Voice, 0.16f)]
        public void Source_gain_applies_master_channel_and_authored_volume_once(AudioGroup group, float expected)
        {
            var settings = new SettingsService(new MemorySettings());
            var references = new AudioReferences(null, null, false);
            using var volumes = new AudioMixerVolumes(settings, new AudioLibraryProvider(references), references);
            Assert.AreEqual(expected, volumes.EffectiveVolume(group, 0.5f), 0.00001f);
        }

        [Test]
        public void Volume_changes_are_forwarded_once_and_disposal_unsubscribes()
        {
            var settings = new SettingsService(new MemorySettings());
            var references = new AudioReferences(null, null, false);
            var volumes = new AudioMixerVolumes(settings, new AudioLibraryProvider(references), references);
            var changes = 0;
            volumes.Changed += () => changes++;
            volumes.Initialize();
            volumes.Initialize();
            settings.SetVolume(VolumeChannel.Master, 0.6f);
            Assert.AreEqual(1, changes);
            volumes.Dispose();
            settings.SetVolume(VolumeChannel.Master, 0.4f);
            Assert.AreEqual(1, changes);
        }

        [TestCase(UiFeedbackSound.Click, GameSound.UiClick)]
        [TestCase(UiFeedbackSound.Confirm, GameSound.UiConfirm)]
        [TestCase(UiFeedbackSound.Back, GameSound.UiBack)]
        [TestCase(UiFeedbackSound.PanelOpen, GameSound.UiPanelOpen)]
        [TestCase(UiFeedbackSound.PanelClose, GameSound.UiPanelClose)]
        [TestCase(UiFeedbackSound.ToggleOn, GameSound.UiToggleOn)]
        [TestCase(UiFeedbackSound.ToggleOff, GameSound.UiToggleOff)]
        [TestCase(UiFeedbackSound.SliderTick, GameSound.UiSliderTick)]
        [TestCase(UiFeedbackSound.Coin, GameSound.UiCoin)]
        public void Authored_feedback_ids_keep_their_sound_and_native_tap(UiFeedbackSound sound, GameSound expected)
        {
            var audio = new RecordingAudio();
            var haptics = new RecordingHaptics();
            var feedback = new UiFeedback(audio, haptics);
            feedback.Play(sound);
            Assert.AreEqual((int)expected, (int)sound, "Existing serialized button values must remain valid.");
            CollectionAssert.AreEqual(new[] { expected }, audio.Played);
            Assert.AreEqual(1, haptics.Taps);
            audio.NotReady = true;
            Assert.DoesNotThrow(() => feedback.Play(sound));
            Assert.AreEqual(2, haptics.Taps, "Feedback remains usable during loading.");
        }

        [Test]
        public void Binder_reaches_inactive_dynamic_buttons_and_feedback_precedes_click_listeners()
        {
            var root = new GameObject("Feedback binding test");
            var events = new GameObject("Feedback event system", typeof(EventSystem));
            try
            {
                var child = new GameObject("Dynamic button", typeof(RectTransform), typeof(FeedbackButton));
                child.transform.SetParent(root.transform, false);
                child.SetActive(false);
                var feedback = new RecordingFeedback();
                new UiFeedbackBinder(feedback,
                    new UiLifecycleBinder(new TickRegistry(new UnityFrameTimeSource()),
                        new ScreenService(new UnityScreenMetrics()))).Bind(root);
                child.SetActive(true);
                var button = child.GetComponent<FeedbackButton>();
                var clicks = 0;
                button.onClick.AddListener(() =>
                {
                    Assert.AreEqual(1, feedback.Calls);
                    clicks++;
                });
                button.OnPointerClick(new PointerEventData(events.GetComponent<EventSystem>()));
                Assert.AreEqual(1, clicks);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(events);
            }
        }

        [Test]
        public void Ui_assembly_has_no_container_or_audio_reference()
        {
            var references = typeof(FeedbackButton).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name);
            CollectionAssert.DoesNotContain(references, "VContainer");
            CollectionAssert.DoesNotContain(references, "Slime.Audio");
        }

        [Test]
        public void Navigation_retries_the_same_custom_loading_context_and_keeps_menu_defaults()
        {
            var scenes = new RecordingScenes();
            using var navigation = new GameNavigation(scenes);
            navigation.OpenAsync(GameNavigation.MenuScene).GetAwaiter().GetResult();
            Assert.AreEqual(LoadingMessageKeys.Menu, scenes.Message);
            scenes.Fail = true;
            Assert.Throws<InvalidOperationException>(() => navigation.OpenAsync("CustomScene", "custom.loading").GetAwaiter().GetResult());
            scenes.Fail = false;
            navigation.RetryAsync().GetAwaiter().GetResult();
            Assert.AreEqual("CustomScene", scenes.Scene);
            Assert.AreEqual("custom.loading", scenes.Message);
            Assert.AreEqual(0, scenes.LegacyCalls);
        }

        [Test]
        public void Settings_prefab_has_no_player_identity_copy_control()
        {
            var root = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/UI/Popups/SettingsPopup.prefab");
            Assert.IsNull(root.transform.Find("RootTransform/CopyPlayerId"));
            Assert.IsNotNull(root.transform.Find("RootTransform/Reset"));
        }

        [Test]
        public void Language_options_load_all_supported_codes_from_bundled_data()
        {
            var languages = new LanguageOptionsProvider().Value;
            CollectionAssert.AreEqual(new[] { "en", "uk", "es", "zh-CN" }, languages.Select(language => language.Code));
            Assert.IsTrue(languages.All(language => !string.IsNullOrWhiteSpace(language.DisplayName)));
        }

        [Test]
        public void Authored_project_scope_resolves_framework_services_without_starting_gameplay()
        {
            var fps = UnityEngine.Application.targetFrameRate;
            var vsync = QualitySettings.vSyncCount;
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/_Game/Scenes/Loading.unity");
            try
            {
                var scope = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ProjectScope>(true)).Single();
                var serialized = new UnityEditor.SerializedObject(scope);
                serialized.FindProperty("introVideoPrefab").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var builder = new ContainerBuilder();
                // A built LifetimeScope registers itself; Configure alone does not.
                builder.RegisterInstance(scope);
                var configure = typeof(ProjectScope).GetMethod("Configure", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                configure.Invoke(scope, new object[] { builder });
                using var container = builder.Build();
                Assert.IsNotNull(container.Resolve<IAudioService>());
                Assert.IsNotNull(container.Resolve<IHaptics>());
                Assert.IsNotNull(container.Resolve<IUiFeedback>());
                Assert.IsNotNull(container.Resolve<UiFeedbackBinder>());
                Assert.IsNotNull(container.Resolve<SettingsPopupViewFactory>());
                Assert.AreSame(container.Resolve<ISceneService>(), container.Resolve<ISceneRequestService>());
                var gameScene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/_Game/Scenes/Game.unity");
                try
                {
                    var gameScope = gameScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameScope>(true)).Single();
                    var configureGame = typeof(GameScope).GetMethod("Configure", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    using var child = container.CreateScope(childBuilder =>
                    {
                        childBuilder.RegisterInstance<IAudioService>(new RecordingAudio());
                        configureGame.Invoke(gameScope, new object[] { childBuilder });
                    });
                    Assert.IsNotNull(child.Resolve<GameplayHapticsPresenter>());
                    var feedback = container.Resolve<IUiFeedback>();
                    var feedbackField = typeof(FeedbackButton).GetField("_feedback", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    foreach (var root in gameScene.GetRootGameObjects())
                        foreach (var button in root.GetComponentsInChildren<FeedbackButton>(true))
                            Assert.AreSame(feedback, feedbackField.GetValue(button), button.name);
                }
                finally
                {
                    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(gameScene);
                }

            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
                UnityEngine.Application.targetFrameRate = fps;
                QualitySettings.vSyncCount = vsync;
            }
        }

        /// <summary>Records UI feedback without a container or engine audio.</summary>
        private sealed class RecordingFeedback : IUiFeedback
        {
            public int Calls
            {
                get; private set;
            }
            public void Play(UiFeedbackSound sound) => Calls++;
        }

        /// <summary>Records native feedback independently from audio readiness.</summary>
        private sealed class RecordingHaptics : IHaptics
        {
            public bool Enabled { get; set; } = true;
            public int Taps
            {
                get; private set;
            }
            public void Play(HapticKind kind) => Taps++;
            public void Vibrate(int milliseconds, float intensity)
            {
            }
            public void StartCharge(float initialCharge, float chargePerSecond, float seconds)
            {
            }
            public void Cancel()
            {
            }
            public void Dispose()
            {
            }
        }

        /// <summary>Records audio calls while simulating startup before initialization.</summary>
        private sealed class RecordingAudio : IAudioService
        {
            public readonly List<GameSound> Played = new List<GameSound>();
            public bool NotReady;
            public string Name => nameof(RecordingAudio);
            public bool CanRetry => true;
            public UniTask InitializeAsync(CancellationToken token) => UniTask.CompletedTask;

            public AudioPlaybackHandle PlaySound(GameSound sound, float volume = 1f)
            {
                if (NotReady)
                    throw new InvalidOperationException();

                Played.Add(sound);
                return null;
            }

            public void ChangeTheme(MusicTheme theme)
            {
            }
            public void MuteMusic(bool muted)
            {
            }
            public void PlayUiSound(UiSound sound)
            {
            }
            public void SetGameplayPaused(bool paused)
            {
            }
            public AudioPlaybackHandle PlayClip(AudioClip clip, AudioGroup group = AudioGroup.UI, AudioSource settings = null, float? pitch = null) => null;
            public void StopGroup(AudioGroup group)
            {
            }
            public void Dispose()
            {
            }
        }

        /// <summary>Records both navigation contracts and can fail a request once.</summary>
        private sealed class RecordingScenes : ISceneRequestService
        {
            public bool Fail;
            public string Scene;
            public string Message;
            public int LegacyCalls;

            public UniTask LoadSceneAsync(string sceneName, CancellationToken token)
            {
                LegacyCalls++;
                return UniTask.CompletedTask;
            }

            public UniTask LoadSceneAsync(string sceneName, string loadingMessageKey, CancellationToken token)
            {
                Scene = sceneName;
                Message = loadingMessageKey;
                return Fail ? UniTask.FromException(new InvalidOperationException()) : UniTask.CompletedTask;
            }
        }

        /// <summary>Provides distinct gains without touching shared player preferences.</summary>
        private sealed class MemorySettings : ISettingsRuntime
        {
            private readonly Dictionary<VolumeChannel, float> _volumes = new Dictionary<VolumeChannel, float>
            {
                { VolumeChannel.Master, 0.8f },
                { VolumeChannel.Music, 0.5f },
                { VolumeChannel.Ui, 0.25f },
                { VolumeChannel.Sfx, 0.75f },
                { VolumeChannel.Voice, 0.4f }
            };

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
            public float GetVolume(VolumeChannel channel) => _volumes[channel];
            public bool GetToggle(PreferenceToggle toggle) => false;
            public void SetVolume(VolumeChannel channel, float value) => _volumes[channel] = value;
            public void SetToggle(PreferenceToggle toggle, bool value)
            {
            }
            public void SetTargetFps(int value)
            {
            }
            public void SetCursor(int index)
            {
            }
            public void SetLanguage(LanguageOption language)
            {
            }
            public void SetDisplay(ResolutionOption resolution, WindowMode mode)
            {
            }
            public void ResetSetting(SettingId setting)
            {
            }
        }
    }
}
