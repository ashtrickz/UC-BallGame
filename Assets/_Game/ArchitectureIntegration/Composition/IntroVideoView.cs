using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.App.Loading;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Game.Integration.Composition
{
    /// <summary>
    /// The company's intro (prefab IntroVideo, a canvas above everything) over the loading screen, which works underneath. One full-screen
    /// picture and nothing behind it: a landscape video reaches the screen's edges through its own clamped white border, because a separate
    /// white backing would fade differently from the part covered twice. At its end the last frame stays a moment and fades into the loading screen; "skip" fades at once from the frame it stopped on. A video
    /// that does not start, fails or stalls never holds the game: the same fade follows.
    /// </summary>
    public sealed class IntroVideoView : MonoBehaviour, IIntroScreen
    {
        [SerializeField] private VideoPlayer player;
        [SerializeField] private RawImage picture;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Button skipButton;
        [Tooltip("How long the last frame stays before the fade, seconds.")]
        [SerializeField, Min(0f)] private float holdSeconds = 0.6f;
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.4f;
        [Tooltip("A video not playing after this long is given up, seconds.")]
        [SerializeField, Min(0.1f)] private float startTimeoutSeconds = 3f;
        [Tooltip("A video still not at its end this long after its length has stalled, seconds.")]
        [SerializeField, Min(0.1f)] private float stallSeconds = 2f;

        /// <summary>The timers' longest step: the hitches of a starting game (or a return from the background) are not time the video stalled.</summary>
        private const float MaxFrameSeconds = 0.1f;

        private enum State { Waiting, Starting, Playing, Holding, Fading, Over }

        private readonly UniTaskCompletionSource _over = new UniTaskCompletionSource();
        private State _state;
        private float _clock;
        private RenderTexture _frame;
        private Vector2Int _fittedFor;
        private Action _ended;

        public bool IsOver => _state == State.Over;

        public UniTask WaitAsync(CancellationToken cancellationToken = default) =>
            IsOver ? UniTask.CompletedTask : _over.Task.AttachExternalCancellation(cancellationToken);

        /// <param name="volume">0..1, from the player's settings.</param>
        /// <param name="ended">Called once when the intro is over (the game's music comes back).</param>
        public void Begin(float volume, Action ended)
        {
            if (_state != State.Waiting) return;
            _ended = ended;
            if (skipButton != null) skipButton.onClick.AddListener(Skip);
            if (player == null || player.clip == null) { Fade(); return; }

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

        private void OnPrepared(VideoPlayer source)
        {
            if (_state != State.Starting) return;
            _state = State.Playing;
            _clock = 0f;
            source.Play();
        }

        /// <summary>The picture appears with its first real frame: until then it is the plain white the video starts on.</summary>
        private void OnFirstFrame(VideoPlayer source, long frame)
        {
            source.sendFrameReadyEvents = false;
            source.frameReady -= OnFirstFrame;
            if (picture != null) picture.texture = _frame;
        }

        private void OnReachedEnd(VideoPlayer source)
        {
            if (_state != State.Playing) return;
            _state = State.Holding;
            _clock = 0f;
            HideSkip();
        }

        private void OnError(VideoPlayer source, string message)
        {
            Debug.LogWarning("Intro video: " + message);
            if (_state == State.Starting || _state == State.Playing) Fade();
        }

        private void Skip()
        {
            if (_state != State.Starting && _state != State.Playing) return;
            if (player != null && player.isPlaying) player.Pause();
            Fade();
        }

        /// <summary>The whole video stays on screen, centred: the texture coordinates reach past it on the longer side, where the clamp repeats its edge.</summary>
        private void FitPicture()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (size == _fittedFor || picture == null || player == null || player.clip == null || player.clip.height == 0 || size.y == 0) return;
            _fittedFor = size;
            float video = (float)player.clip.width / player.clip.height, screen = (float)size.x / size.y;
            float width = screen > video ? screen / video : 1f, height = screen > video ? 1f : video / screen;
            picture.uvRect = new Rect(0.5f - width / 2f, 0.5f - height / 2f, width, height);
        }

        private void HideSkip()
        {
            if (skipButton != null) skipButton.gameObject.SetActive(false);
        }

        private void Fade()
        {
            _state = State.Fading;
            _clock = 0f;
            HideSkip();
            if (group != null) group.blocksRaycasts = false;   // the loading screen is live again: its "retry"
        }

        private void Update()
        {
            FitPicture();
            _clock += Mathf.Min(Time.unscaledDeltaTime, MaxFrameSeconds);
            switch (_state)
            {
                case State.Starting:
                    if (_clock >= startTimeoutSeconds) Fade();
                    break;
                case State.Playing:
                    if (_clock >= player.clip.length + stallSeconds) Fade();
                    break;
                case State.Holding:
                    if (_clock >= holdSeconds) Fade();
                    break;
                case State.Fading:
                    float share = Mathf.Clamp01(_clock / fadeSeconds);
                    if (group != null) group.alpha = 1f - share;
                    if (share >= 1f) Finish();
                    break;
            }
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
            if (skipButton != null) skipButton.onClick.RemoveListener(Skip);
            if (player != null)
            {
                player.prepareCompleted -= OnPrepared;
                player.frameReady -= OnFirstFrame;
                player.loopPointReached -= OnReachedEnd;
                player.errorReceived -= OnError;
                player.Stop();
                player.targetTexture = null;
            }
            if (picture != null) picture.texture = null;
            if (_frame != null) { _frame.Release(); Destroy(_frame); }
            _state = State.Over;
            Notify();
        }
    }
}
