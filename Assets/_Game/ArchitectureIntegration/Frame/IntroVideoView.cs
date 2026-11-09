using System;
using Game.Foundation;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.App.Loading;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Game.Integration.Composition
{
    /// <summary>Shows the authored intro, with unscaled skip, stall and fade handling.</summary>
    public sealed class IntroVideoView : MonoBehaviour, IIntroScreen, ITickRegistryClient
    {
        private const float MaxFrameSeconds = 0.1f;
        private const float DefaultHoldSeconds = 0.6f;
        private const float DefaultFadeSeconds = 0.4f;
        private const float MinimumFadeSeconds = 0.01f;
        private const float MinimumVideoSeconds = 0.1f;
        private const float DefaultStartTimeoutSeconds = 3f;
        private const float DefaultStallSeconds = 2f;
        private const float UvCenter = 0.5f;
        private const float CenterDivisor = 2f;
        private const string VideoErrorPrefix = "Intro video: ";
        [SerializeField] private VideoPlayer player;
        [SerializeField] private UnityEngine.UI.RawImage picture;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private UnityEngine.UI.Button skipButton;
        [Tooltip("How long the last frame stays before the fade, seconds.")]
        [SerializeField, Min(0f)] private float holdSeconds = DefaultHoldSeconds;
        [SerializeField, Min(MinimumFadeSeconds)] private float fadeSeconds = DefaultFadeSeconds;
        [Tooltip("A video not playing after this long is given up, seconds.")]
        [SerializeField, Min(MinimumVideoSeconds)] private float startTimeoutSeconds = DefaultStartTimeoutSeconds;
        [Tooltip("A video still not at its end this long after its length has stalled, seconds.")]
        [SerializeField, Min(MinimumVideoSeconds)] private float stallSeconds = DefaultStallSeconds;

        /// <summary>Represents the finite intro playback lifecycle.</summary>
        private enum State
        {
            Waiting, Starting, Playing, Holding, Fading, Over
        }

        private ITickRegistry _ticks;
        private readonly UniTaskCompletionSource _over = new UniTaskCompletionSource();
        private State _state;
        private float _clock;
        private RenderTexture _frame;
        private Vector2Int _fittedFor;
        private Action _ended;

        public bool IsOver => _state == State.Over;

        public void BindTicks(ITickRegistry registry)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            if (ReferenceEquals(_ticks, registry))
                return;

            _ticks?.Unregister(this);
            _ticks = registry;
            if (isActiveAndEnabled)
                _ticks.Register(this);
        }

        public UniTask WaitAsync(CancellationToken cancellationToken = default) =>
            IsOver ? UniTask.CompletedTask : _over.Task.AttachExternalCancellation(cancellationToken);

        public void Tick(float deltaTime)
        {
            if (!isActiveAndEnabled)
                return;

            FitPicture();
            _clock += Mathf.Min(deltaTime, MaxFrameSeconds);
            switch (_state)
            {
                case State.Starting:
                    if (_clock >= startTimeoutSeconds)
                        Fade();
                    break;
                case State.Playing:
                    if (_clock >= player.clip.length + stallSeconds)
                        Fade();
                    break;
                case State.Holding:
                    if (_clock >= holdSeconds)
                        Fade();
                    break;
                case State.Fading:
                    float share = Mathf.Clamp01(_clock / fadeSeconds);
                    if (group != null)
                        group.alpha = 1f - share;
                    if (share >= 1f)
                        Finish();
                    break;
            }
        }

        public void Begin(float volume, Action ended)
        {
            if (_state != State.Waiting)
                return;

            _ended = ended;
            if (skipButton != null)
                skipButton.onClick.AddListener(Skip);
            if (player == null || player.clip == null)
            {
                Fade();
                return;
            }

            _frame = new RenderTexture((int)player.clip.width, (int)player.clip.height, 0, RenderTextureFormat.ARGB32) { wrapMode = TextureWrapMode.Clamp };
            FitPicture();
            player.playOnAwake = false;
            player.isLooping = false;
            player.skipOnDrop = true;
            player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = _frame;
            player.audioOutputMode = VideoAudioOutputMode.Direct;
            player.controlledAudioTrackCount = 1;
            player.EnableAudioTrack(0, true);
            player.SetDirectAudioVolume(0, Mathf.Clamp01(volume));
            player.sendFrameReadyEvents = true;
            player.prepareCompleted += OnPrepared;
            player.frameReady += OnFirstFrame;
            player.loopPointReached += OnReachedEnd;
            player.errorReceived += OnError;
            _state = State.Starting;
            _clock = 0f;
            player.Prepare();
        }

        private void OnEnable() => _ticks?.Register(this);
        private void OnDisable() => _ticks?.Unregister(this);

        private void OnPrepared(VideoPlayer source)
        {
            if (_state != State.Starting)
                return;

            _state = State.Playing;
            _clock = 0f;
            source.Play();
        }

        private void OnFirstFrame(VideoPlayer source, long frame)
        {
            source.sendFrameReadyEvents = false;
            source.frameReady -= OnFirstFrame;
            if (picture != null)
                picture.texture = _frame;
        }

        private void OnReachedEnd(VideoPlayer source)
        {
            if (_state != State.Playing)
                return;

            _state = State.Holding;
            _clock = 0f;
            HideSkip();
        }

        private void OnError(VideoPlayer source, string message)
        {
            GameLog.Warning(VideoErrorPrefix + message);
            if (_state == State.Starting || _state == State.Playing)
                Fade();
        }

        private void Skip()
        {
            if (_state != State.Starting && _state != State.Playing)
                return;

            if (player != null && player.isPlaying)
                player.Pause();
            Fade();
        }

        private void FitPicture()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (size == _fittedFor || picture == null || player == null || player.clip == null || player.clip.height == 0 || size.y == 0)
                return;

            _fittedFor = size;
            float video = (float)player.clip.width / player.clip.height, screen = (float)size.x / size.y;
            float width = screen > video ? screen / video : 1f, height = screen > video ? 1f : video / screen;
            picture.uvRect = new Rect(UvCenter - width / CenterDivisor, UvCenter - height / CenterDivisor, width, height);
        }

        private void HideSkip()
        {
            if (skipButton != null)
                skipButton.gameObject.SetActive(false);
        }

        private void Fade()
        {
            _state = State.Fading;
            _clock = 0f;
            HideSkip();
            if (group != null)
                group.blocksRaycasts = false; // the loading screen is live again: its "retry"
        }

        private void Finish()
        {
            _state = State.Over;
            Notify();
            Destroy(gameObject);
        }

        private void Notify()
        {
            var ended = _ended;
            _ended = null;
            ended?.Invoke();
            _over.TrySetResult();
        }

        private void OnDestroy()
        {
            _ticks?.Unregister(this);
            if (skipButton != null)
                skipButton.onClick.RemoveListener(Skip);
            if (player != null)
            {
                player.prepareCompleted -= OnPrepared;
                player.frameReady -= OnFirstFrame;
                player.loopPointReached -= OnReachedEnd;
                player.errorReceived -= OnError;
                player.Stop();
                player.targetTexture = null;
            }
            if (picture != null)
                picture.texture = null;
            if (_frame != null)
            {
                _frame.Release();
                Destroy(_frame);
            }
            _state = State.Over;
            Notify();
        }
    }
}
