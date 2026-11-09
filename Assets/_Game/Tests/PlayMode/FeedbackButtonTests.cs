using Slime.Foundation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using VContainer.Unity;
using NUnit.Framework;
using Slime.Audio;
using Slime.Gameplay.Presentation;
using Slime.Infrastructure.Audio;
using Slime.Composition;
using Slime.Infrastructure.Frame;
using Slime.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using VContainer;
using Object = UnityEngine.Object;

namespace Slime.Tests.PlayMode
{
    public sealed class FeedbackButtonTests
    {
        private GameObject _root;
        private float _timeScale;
        private IObjectResolver _container;
        private RecordingAudio _audio;

        [SetUp]
        public void SetUp()
        {
            _timeScale = Time.timeScale;
            _root = new GameObject("Button feedback tests", typeof(Canvas), typeof(EventSystem));
            _audio = new RecordingAudio();
            var builder = new ContainerBuilder();
            builder.RegisterInstance<IAudioService>(_audio);
            builder.Register<IFrameTimeSource, UnityFrameTimeSource>(Lifetime.Singleton);
            builder.RegisterEntryPoint<TickRegistry>().AsSelf();
            _container = builder.Build();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = _timeScale;
            if (_root != null)
                Object.DestroyImmediate(_root);
            _container?.Dispose();
        }

        private T Make<T>() where T : FeedbackButton
        {
            var go = new GameObject(typeof(T).Name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(_root.transform, false);
            var button = go.AddComponent<T>();
            button.Bind(new UiFeedback(_audio, new NullHaptics()));
            if (button is ITickRegistryClient client)
                client.BindTicks(_container.Resolve<ITickRegistry>());
            return button;
        }

        private PointerEventData Pointer(PointerEventData.InputButton input = PointerEventData.InputButton.Left) =>
            new PointerEventData(_root.GetComponent<EventSystem>()) { button = input };

        [Test]
        public void Click_and_submit_play_injected_audio_once_before_listeners_close_the_button()
        {
            var button = Make<FeedbackButton>();
            button.OnPointerDown(Pointer());
            Assert.IsEmpty(_audio.Played, "Pointer down only animates; a cancelled click is silent.");
            button.onClick.AddListener(() =>
            {
                Assert.AreEqual(1, _audio.Played.Count);
                button.gameObject.SetActive(false);
            });
            button.OnPointerClick(Pointer());
            CollectionAssert.AreEqual(new[] { GameSound.UiClick }, _audio.Played);

            var submit = Make<FeedbackButton>();
            submit.OnSubmit(new BaseEventData(_root.GetComponent<EventSystem>()));
            Assert.AreEqual(2, _audio.Played.Count);
        }

        [Test]
        public void Disabled_non_interactable_and_right_click_buttons_are_silent()
        {
            var button = Make<FeedbackButton>();
            var calls = 0;
            button.onClick.AddListener(() => calls++);
            button.OnPointerDown(Pointer(PointerEventData.InputButton.Right));
            button.OnPointerClick(Pointer(PointerEventData.InputButton.Right));
            button.interactable = false;
            button.OnPointerDown(Pointer());
            button.OnPointerClick(Pointer());
            button.OnSubmit(new BaseEventData(_root.GetComponent<EventSystem>()));
            button.interactable = true;
            button.gameObject.SetActive(false);
            button.OnPointerClick(Pointer());
            button.OnSubmit(new BaseEventData(_root.GetComponent<EventSystem>()));
            Assert.IsEmpty(_audio.Played);
            Assert.AreEqual(0, calls);
            Assert.AreEqual(Vector3.one, button.transform.localScale);
        }

        [UnityTest]
        public IEnumerator Punch_uses_unscaled_time_and_restores_scale_after_repeated_presses_and_disable()
        {
            var button = Make<FeedbackButton>();
            var scale = new Vector3(1.4f, 0.75f, 1f);
            button.transform.localScale = scale;
            Time.timeScale = 0f;
            button.OnPointerDown(Pointer());
            yield return new WaitForSecondsRealtime(0.04f);
            Assert.Greater((button.transform.localScale - scale).sqrMagnitude, 0.000001f);
            button.OnPointerDown(Pointer());
            button.gameObject.SetActive(false);
            Assert.AreEqual(scale, button.transform.localScale, "Disabling must not leave the button enlarged.");
            button.gameObject.SetActive(true);
            button.OnPointerDown(Pointer());
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.Less((button.transform.localScale - scale).sqrMagnitude, 0.000001f);
            Assert.IsEmpty(_audio.Played);
        }

        [UnityTest]
        public IEnumerator Hold_plays_audio_only_on_completion_and_preserves_plain_button_mode()
        {
            var button = Make<HoldButton>();
            button.HoldSeconds = HoldButton.MinHoldSeconds;
            button.OnPointerDown(Pointer());
            yield return new WaitForSecondsRealtime(0.05f);
            button.OnPointerUp(Pointer());
            button.OnPointerClick(Pointer());
            Assert.IsEmpty(_audio.Played);

            button.ResetProgress();
            button.OnPointerDown(Pointer());
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(1, _audio.Played.Count);
            button.OnPointerUp(Pointer());
            button.OnPointerClick(Pointer());
            Assert.AreEqual(1, _audio.Played.Count, "The release after a completed hold must not click twice.");
            button.RequireHold = false;
            button.OnPointerClick(Pointer());
            Assert.AreEqual(2, _audio.Played.Count);
        }

        [Test]
        public void Audio_not_initialized_does_not_block_the_click()
        {
            var button = Make<FeedbackButton>();
            _audio.NotReady = true;
            var calls = 0;
            button.onClick.AddListener(() => calls++);
            Assert.DoesNotThrow(() => button.OnPointerClick(Pointer()));
            Assert.AreEqual(1, calls);
        }

        private sealed class RecordingAudio : IAudioService
        {
            public readonly List<GameSound> Played = new List<GameSound>();
            public bool NotReady;
            public string Name => "Button test audio";
            public bool CanRetry => true;
            public UniTask InitializeAsync(CancellationToken cancellationToken) => UniTask.CompletedTask;
            public AudioPlaybackHandle PlaySound(GameSound sound, float volume = 1f)
            {
                if (NotReady)
                    throw new InvalidOperationException("Not initialized");
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
            public AudioPlaybackHandle PlayClip(AudioClip clip, AudioGroup group = AudioGroup.UI,
                AudioSource settings = null, float? pitch = null) => null;
            public void StopGroup(AudioGroup group)
            {
            }
            public void Dispose()
            {
            }
        }
    }
}
