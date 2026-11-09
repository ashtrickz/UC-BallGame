using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Assets;
using Game.Audio;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Game.Integration.Audio
{
    /// <summary>Coordinates authored audio cues through dedicated playback, music and volume services.</summary>
    public sealed class AudioService : IAudioService
    {
        private const string NotInitializedMessage = "AudioService must initialize before playback.";
        private const string LoadingMessageKey = "loading.audio";
        private readonly IAssetService _assets;
        private readonly AudioLibraryProvider _library;
        private readonly AudioSourcePool _sources;
        private readonly MusicPlayer _music;
        private readonly AudioMixerVolumes _volumes;
        private readonly AudioVariation _variation;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly Dictionary<GameSound, CueState> _cueStates = new Dictionary<GameSound, CueState>();
        private bool _initialized;
        private bool _disposed;

        /// <summary>Remembers the last successful variation and cooldown for one cue.</summary>
        private sealed class CueState
        {
            public int LastClip = -1;
            public float LastPitch = float.NaN;
            public double NextAllowed;
        }

        public string Name => LoadingMessageKey;
        public bool CanRetry => true;
        public event Action<GameSound, AudioPlaybackHandle> SoundPlayed;

        public AudioService(IAssetService assets, AudioLibraryProvider library, AudioSourcePool sources,
            MusicPlayer music, AudioMixerVolumes volumes, AudioVariation variation)
        {
            _assets = assets;
            _library = library;
            _sources = sources;
            _music = music;
            _volumes = volumes;
            _variation = variation;
        }

        public UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_disposed)
                throw new ObjectDisposedException(nameof(AudioService));

            if (_initialized)
                return UniTask.CompletedTask;

            _sources.Initialize();
            _music.Initialize();
            _volumes.Changed += ApplyVolumes;
            _volumes.Initialize();
            _initialized = true;
            ApplyVolumes();
            ChangeTheme(MusicTheme.MainMenu);
            return UniTask.CompletedTask;
        }

        public AudioPlaybackHandle PlaySound(GameSound sound, float volume = 1f)
        {
            EnsureInitialized();
            if (_library.Value == null || !_library.Value.TryGet(sound, out var definition) || definition.ClipCount == 0)
                return null;

            if (!_cueStates.TryGetValue(sound, out var state))
                _cueStates.Add(sound, state = new CueState());
            var now = AudioSettings.dspTime;
            if (now < state.NextAllowed)
                return null;

            var voices = _sources.ActiveVoices(sound);
            if (voices >= Mathf.Max(1, definition.maxVoices))
                return null;

            var index = _variation.PickClip(definition.ClipCount, state.LastClip);
            var pitch = _variation.PickPitch(definition.pitchRange, state.LastPitch);
            var handle = _sources.StartClip(definition.GetClip(index), definition.group,
                Mathf.Clamp01(definition.volume), volume, pitch, definition.loop, definition.mixerGroup, sound);
            if (handle == null)
                return null;

            state.LastClip = index;
            state.LastPitch = pitch;
            state.NextAllowed = now + Mathf.Max(0, definition.minInterval);
            SoundPlayed?.Invoke(sound, handle);
            return handle;
        }

        public AudioPlaybackHandle PlayClip(AudioClip clip, AudioGroup group = AudioGroup.UI,
            AudioSource settings = null, float? pitch = null)
        {
            EnsureInitialized();
            if (clip == null)
                return null;

            return _sources.StartClip(clip, group, settings != null ? settings.volume : 1, 1,
                pitch ?? (settings != null ? settings.pitch : 1), settings != null && settings.loop,
                settings != null ? settings.outputAudioMixerGroup : null, null);
        }

        public void PlayUiSound(UiSound sound)
        {
            EnsureInitialized();
            if (sound != UiSound.Click && sound != UiSound.OnEnter && sound != UiSound.Coin)
                throw new ArgumentOutOfRangeException(nameof(sound));

            var id = sound == UiSound.Click ? GameSound.UiClick : sound == UiSound.OnEnter ? GameSound.UiPanelOpen : GameSound.UiCoin;
            if (_library.Value == null)
                return;

            if (_library.Value.TryGet(id, out var definition) && definition.ClipCount > 0)
            {
                PlaySound(id);
                return;
            }
            var reference = sound == UiSound.Click ? _library.Value.OnClick : sound == UiSound.OnEnter ? _library.Value.OnEnter : _library.Value.OnCoin;
            PlayLibraryClipAsync(reference, AudioGroup.UI).Forget(Debug.LogException);
        }

        public void MuteMusic(bool muted) => _music.MuteMusic(muted);

        public void ChangeTheme(MusicTheme theme)
        {
            EnsureInitialized();
            _music.ChangeTheme(theme);
        }

        public void SetGameplayPaused(bool paused)
        {
            EnsureInitialized();
            _sources.SetGameplayPaused(paused);
        }

        public void StopGroup(AudioGroup group)
        {
            EnsureInitialized();
            if (group == AudioGroup.Music)
                _music.Stop();
            _sources.StopGroup(group);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _initialized = false;
            _lifetime.Cancel();
            _volumes.Changed -= ApplyVolumes;
            _music.Dispose();
            _sources.Dispose();
            _volumes.Dispose();
            SoundPlayed = null;
            _lifetime.Dispose();
        }

        private async UniTask PlayLibraryClipAsync(AssetReference reference, AudioGroup group)
        {
            if (reference == null || string.IsNullOrEmpty(reference.AssetGUID))
                return;

            AssetLease<AudioClip> lease;
            try
            {
                lease = await _assets.LoadAsync<AudioClip>(reference, _lifetime.Token);
            }
            catch (OperationCanceledException) when (_disposed)
            {
                return;
            }
            if (_disposed)
            {
                lease.Dispose();
                return;
            }
            var handle = _sources.StartClip(lease.Asset, group, 1, 1, 1, false, null, null, lease);
            if (handle == null)
                lease.Dispose();
        }

        private void ApplyVolumes()
        {
            _sources.ApplyVolumes();
            _music.ApplyVolumes();
        }

        private void EnsureInitialized()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AudioService));

            if (!_initialized)
                throw new InvalidOperationException(NotInitializedMessage);

        }
    }
}
