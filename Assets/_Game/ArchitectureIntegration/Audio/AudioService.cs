using Game.Assets;
using Game.Audio;
using Game.Settings;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

namespace Game.Integration.Audio
{
    /// <summary>Authored cues, independent variation RNG, bounded voices and optional mixer routing.</summary>
    public sealed class AudioService : IAudioService
    {
        private const int PoolSize = 24;
        private readonly ISettingsService _settings;
        private readonly IAssetService _assets;
        private readonly AudioLibrary _library;
        private readonly AudioMixer _mixer;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly System.Random _random = new System.Random();
        private readonly Dictionary<GameSound, CueState> _cueStates = new Dictionary<GameSound, CueState>();
        private readonly AudioSource[] _pool = new AudioSource[PoolSize];
        private readonly AudioGroup[] _poolGroups = new AudioGroup[PoolSize];
        private readonly GameSound?[] _poolSounds = new GameSound?[PoolSize];
        private readonly float[] _poolVolumes = new float[PoolSize];
        private readonly float[] _poolBaseVolumes = new float[PoolSize];
        private readonly bool[] _poolPaused = new bool[PoolSize];
        private readonly bool[] _manualPaused = new bool[PoolSize];
        private readonly double[] _startedAt = new double[PoolSize];
        private readonly int[] _poolVersion = new int[PoolSize];
        private readonly AssetLease<AudioClip>[] _poolLeases = new AssetLease<AudioClip>[PoolSize];
        private GameObject _root;
        private AudioSource _musicSource;
        private AssetLease<AudioClip> _musicLease;
        private MusicTheme? _currentTheme;
        private float _musicVolume = 1;
        private int _themeRevision;
        private bool _initialized, _disposed, _musicMuted, _gameplayPaused;

        private sealed class CueState
        {
            public int LastClip = -1;
            public float LastPitch = float.NaN;
            public double NextAllowed;
        }

        public string Name => "loading.audio";
        public bool CanRetry => true;
        public event Action<GameSound, AudioPlaybackHandle> SoundPlayed;

        public AudioService(ISettingsService settings, IAssetService assets, AudioLibrary library, AudioMixer mixer)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _library = library;
            _mixer = mixer;
        }

        public UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_disposed) throw new ObjectDisposedException(nameof(AudioService));
            if (_initialized) return UniTask.CompletedTask;
            _root = new GameObject("AudioPool");
            Object.DontDestroyOnLoad(_root);
            for (var i = 0; i < PoolSize; i++)
            {
                var source = _root.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0;
                source.dopplerLevel = 0;
                _pool[i] = source;
            }
            _musicSource = _root.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop = true;
            _musicSource.mute = _musicMuted;
            ApplyMixerGroup(_musicSource, AudioGroup.Music);
            _settings.Changed += ApplyVolumes;
            _initialized = true;
            ApplyVolumes();
            ChangeTheme(MusicTheme.MainMenu);
            return UniTask.CompletedTask;
        }

        private void ApplyMixerGroup(AudioSource source, AudioGroup group, AudioMixerGroup output = null)
        {
            output = output != null ? output : _library != null ? _library.GetMixerGroup(group) : null;
            if (output == null && _mixer != null)
            {
                var groups = _mixer.FindMatchingGroups(group == AudioGroup.Button ? "UI" : group.ToString());
                if (groups.Length > 0) output = groups[0];
            }
            source.outputAudioMixerGroup = output;
        }

        private float EffectiveVolume(AudioGroup group, float authoredVolume)
        {
            var channel = group == AudioGroup.Music ? VolumeChannel.Music :
                group == AudioGroup.Voice ? VolumeChannel.Voice :
                group == AudioGroup.UI || group == AudioGroup.Button ? VolumeChannel.Ui : VolumeChannel.Sfx;
            return authoredVolume * _settings.GetVolume(VolumeChannel.Master) * _settings.GetVolume(channel);
        }

        private void ApplyVolumes()
        {
            // Settings always apply on the sources, including when a mixer exists. No exposed
            // mixer parameter names are required, and no second gain is applied to the mixer.
            for (var i = 0; i < PoolSize; i++)
                if (_pool[i] != null) _pool[i].volume = EffectiveVolume(_poolGroups[i], _poolVolumes[i]);
            if (_musicSource != null) _musicSource.volume = EffectiveVolume(AudioGroup.Music, _musicVolume);
        }

        public AudioPlaybackHandle PlaySound(GameSound sound, float volume = 1f)
        {
            EnsureInitialized();
            if (_library == null || !_library.TryGet(sound, out var definition) || definition.ClipCount == 0) return null;
            if (!_cueStates.TryGetValue(sound, out var state))
                _cueStates.Add(sound, state = new CueState());
            var now = AudioSettings.dspTime;
            if (now < state.NextAllowed) return null;
            var voices = 0;
            for (var i = 0; i < PoolSize; i++)
                if (_poolSounds[i] == sound && (_pool[i].isPlaying || _poolPaused[i])) voices++;
            if (voices >= Mathf.Max(1, definition.maxVoices)) return null;
            var count = definition.ClipCount;
            var last = state.LastClip >= count ? -1 : state.LastClip;
            var index = count == 1 ? 0 : last < 0 ? _random.Next(count) : _random.Next(count - 1);
            if (count > 1 && last >= 0 && index >= last) index++;
            var pitch = PickPitch(definition.pitchRange, state.LastPitch);
            var handle = StartClip(definition.GetClip(index), definition.group,
                Mathf.Clamp01(definition.volume), volume, pitch, definition.loop, definition.mixerGroup, sound);
            if (handle == null) return null;
            state.LastClip = index;
            state.LastPitch = pitch;
            state.NextAllowed = now + Mathf.Max(0, definition.minInterval);
            SoundPlayed?.Invoke(sound, handle);
            return handle;
        }

        private float PickPitch(Vector2 range, float previous)
        {
            var min = Mathf.Clamp(Mathf.Min(range.x, range.y), 0.1f, 3f);
            var max = Mathf.Clamp(Mathf.Max(range.x, range.y), min, 3f);
            var width = max - min;
            if (width < 0.00001f) return min;
            var pitch = min + (float)_random.NextDouble() * width;
            if (!float.IsNaN(previous) && Mathf.Abs(pitch - previous) < Mathf.Min(0.015f, width * 0.2f))
                pitch = min + ((pitch - min + width * 0.5f) % width);
            return pitch;
        }

        public AudioPlaybackHandle PlayClip(AudioClip clip, AudioGroup group = AudioGroup.UI,
            AudioSource settings = null, float? pitch = null)
        {
            EnsureInitialized();
            if (clip == null) return null;
            return StartClip(clip, group, settings != null ? settings.volume : 1, 1,
                pitch ?? (settings != null ? settings.pitch : 1), settings != null && settings.loop,
                settings != null ? settings.outputAudioMixerGroup : null, null);
        }

        private AudioPlaybackHandle StartClip(AudioClip clip, AudioGroup group, float baseVolume,
            float multiplier, float pitch, bool loop, AudioMixerGroup output, GameSound? sound,
            AssetLease<AudioClip> lease = null)
        {
            var index = RentSource();
            if (index < 0) return null;
            var source = _pool[index];
            _poolGroups[index] = group;
            _poolSounds[index] = sound;
            _poolBaseVolumes[index] = Mathf.Clamp01(baseVolume);
            _poolLeases[index] = lease;
            _poolVolumes[index] = _poolBaseVolumes[index] * Mathf.Clamp01(multiplier);
            _startedAt[index] = AudioSettings.dspTime;
            source.clip = clip;
            source.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
            source.volume = EffectiveVolume(group, _poolVolumes[index]);
            source.loop = loop;
            source.priority = group == AudioGroup.UI || group == AudioGroup.Button ? 32 : loop ? 64 : 128;
            ApplyMixerGroup(source, group, output);
            source.Play();
            ApplyPause(index);
            var handle = new AudioPlaybackHandle(source, _poolVersion[index], _poolVersion, index,
                () => ReleaseSlot(index),
                value => { _poolVolumes[index] = _poolBaseVolumes[index] * value; source.volume = EffectiveVolume(group, _poolVolumes[index]); },
                paused => { _manualPaused[index] = paused; ApplyPause(index); },
                () => _poolPaused[index]);
            if (lease != null) ReleaseWhenFinishedAsync(index, _poolVersion[index]).Forget(Debug.LogException);
            return handle;
        }

        private int RentSource()
        {
            for (var i = 0; i < PoolSize; i++)
                if (!_pool[i].isPlaying && !_poolPaused[i]) { ReleaseSlot(i); return i; }
            // Never steal a charge loop or a paused action. Other short effects remain bounded.
            var oldest = -1;
            for (var i = 0; i < PoolSize; i++)
                if (!_pool[i].loop && !_poolPaused[i] && (oldest < 0 || _startedAt[i] < _startedAt[oldest])) oldest = i;
            if (oldest >= 0) ReleaseSlot(oldest);
            return oldest;
        }

        private void ReleaseSlot(int index)
        {
            if (_pool[index] != null) { _pool[index].Stop(); _pool[index].clip = null; }
            _poolPaused[index] = _manualPaused[index] = false;
            _poolSounds[index] = null;
            ++_poolVersion[index];
            _poolLeases[index]?.Dispose();
            _poolLeases[index] = null;
        }

        private static bool IsGameplay(AudioGroup group) => group == AudioGroup.Sfx || group == AudioGroup.Ambience;

        private void ApplyPause(int index)
        {
            var paused = _manualPaused[index] || (_gameplayPaused && IsGameplay(_poolGroups[index]));
            if (paused == _poolPaused[index]) return;
            _poolPaused[index] = paused;
            if (paused) _pool[index].Pause();
            else _pool[index].UnPause();
        }

        public void SetGameplayPaused(bool paused)
        {
            EnsureInitialized();
            _gameplayPaused = paused;
            for (var i = 0; i < PoolSize; i++) if (_pool[i].clip != null) ApplyPause(i);
        }

        public void StopGroup(AudioGroup group)
        {
            EnsureInitialized();
            if (group == AudioGroup.Music)
            {
                ++_themeRevision;
                _musicSource.Stop();
                _musicSource.clip = null;
                _musicLease?.Dispose();
                _musicLease = null;
                _currentTheme = null;
            }
            for (var i = 0; i < PoolSize; i++) if (_poolGroups[i] == group) ReleaseSlot(i);
        }

        public void PlayUiSound(UiSound sound)
        {
            EnsureInitialized();
            if (sound != UiSound.Click && sound != UiSound.OnEnter && sound != UiSound.Coin)
                throw new ArgumentOutOfRangeException(nameof(sound));
            var id = sound == UiSound.Click ? GameSound.UiClick : sound == UiSound.OnEnter ? GameSound.UiPanelOpen : GameSound.UiCoin;
            if (_library == null) return;
            if (_library.TryGet(id, out var definition) && definition.ClipCount > 0) { PlaySound(id); return; }
            var reference = sound == UiSound.Click ? _library.OnClick : sound == UiSound.OnEnter ? _library.OnEnter : _library.OnCoin;
            PlayLibraryClipAsync(reference, AudioGroup.UI).Forget(Debug.LogException);
        }

        private async UniTask PlayLibraryClipAsync(AssetReference reference, AudioGroup group)
        {
            if (reference == null || string.IsNullOrEmpty(reference.AssetGUID)) return;
            AssetLease<AudioClip> lease;
            try { lease = await _assets.LoadAsync<AudioClip>(reference, _lifetime.Token); }
            catch (OperationCanceledException) when (_disposed) { return; }
            if (_disposed) { lease.Dispose(); return; }
            var handle = StartClip(lease.Asset, group, 1, 1, 1, false, null, null, lease);
            if (handle == null) lease.Dispose();
        }

        private async UniTask ReleaseWhenFinishedAsync(int index, int version)
        {
            try
            {
                await UniTask.WaitUntil(() => _disposed || _poolVersion[index] != version ||
                    (!_pool[index].isPlaying && !_poolPaused[index]), cancellationToken: _lifetime.Token);
                if (!_disposed && _poolVersion[index] == version) ReleaseSlot(index);
            }
            catch (OperationCanceledException) when (_disposed) { }
        }

        public void MuteMusic(bool muted)
        {
            _musicMuted = muted;
            if (_musicSource != null) _musicSource.mute = muted;
        }

        public void ChangeTheme(MusicTheme theme)
        {
            EnsureInitialized();
            // Allow retry after a missing clip is assigned or a theme finishes playing.
            if (_currentTheme == theme && _musicSource.clip != null && _musicSource.isPlaying) return;
            _currentTheme = theme;
            var revision = ++_themeRevision;
            var id = theme == MusicTheme.MainMenu ? GameSound.MainMenuMusic : GameSound.GameplayMusic;
            if (_library != null && _library.TryGet(id, out var definition) && definition.ClipCount > 0)
            {
                _musicSource.Stop();
                _musicSource.clip = null;
                _musicLease?.Dispose();
                _musicLease = null;
                _musicVolume = Mathf.Clamp01(definition.volume);
                _musicSource.clip = definition.GetClip(0);
                _musicSource.pitch = PickPitch(definition.pitchRange, float.NaN);
                _musicSource.volume = EffectiveVolume(AudioGroup.Music, _musicVolume);
                _musicSource.loop = definition.loop;
                ApplyMixerGroup(_musicSource, AudioGroup.Music, definition.mixerGroup);
                _musicSource.Play();
            }
            else LoadThemeAsync(theme, revision).Forget(Debug.LogException);
        }

        private async UniTask LoadThemeAsync(MusicTheme theme, int revision)
        {
            var reference = _library != null ? _library.GetTheme(theme) : null;
            if (reference == null || string.IsNullOrEmpty(reference.AssetGUID))
            {
                _musicSource.Stop();
                _musicSource.clip = null;
                _musicLease?.Dispose();
                _musicLease = null;
                return;
            }
            AssetLease<AudioClip> lease;
            try { lease = await _assets.LoadAsync<AudioClip>(reference, _lifetime.Token); }
            catch (OperationCanceledException) when (_disposed) { return; }
            if (_disposed || revision != _themeRevision) { lease.Dispose(); return; }
            _musicSource.Stop();
            _musicSource.clip = null;
            _musicLease?.Dispose();
            _musicLease = lease;
            _musicVolume = 1;
            _musicSource.volume = EffectiveVolume(AudioGroup.Music, 1);
            _musicSource.pitch = 1;
            _musicSource.loop = true;
            ApplyMixerGroup(_musicSource, AudioGroup.Music);
            _musicSource.clip = lease.Asset;
            _musicSource.Play();
        }

        private void EnsureInitialized()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AudioService));
            if (!_initialized) throw new InvalidOperationException("AudioService must initialize before playback.");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _initialized = false;
            ++_themeRevision;
            _lifetime.Cancel();
            _settings.Changed -= ApplyVolumes;
            for (var i = 0; i < PoolSize; i++) ReleaseSlot(i);
            if (_musicSource != null) { _musicSource.Stop(); _musicSource.clip = null; }
            _musicLease?.Dispose();
            _musicLease = null;
            SoundPlayed = null;
            if (_root != null) Object.Destroy(_root);
            _musicSource = null;
            _lifetime.Dispose();
        }
    }
}
