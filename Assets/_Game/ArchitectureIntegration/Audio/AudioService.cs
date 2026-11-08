using Game.Assets;
using Game.Audio;
using Game.Settings;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

namespace Game.Integration.Audio
{
    // A minimal AudioService: clips are Addressable references (AudioLibrary), loaded on
    // demand through IAssetService so a mobile build does not keep every sound in memory.
    // Playback goes through a small pooled AudioSource set; no third-party audio-event package.
    public sealed class AudioService : IAudioService
    {
        private const int PoolSize = 8;

        private readonly ISettingsService _settings;
        private readonly IAssetService _assets;
        private readonly AudioLibrary _library;
        private readonly AudioMixer _mixer;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly AudioSource[] _pool = new AudioSource[PoolSize];
        private readonly AudioGroup[] _poolGroups = new AudioGroup[PoolSize];
        private readonly float[] _poolVolumes = new float[PoolSize];
        private int _nextSlot;
        private readonly int[] _poolVersion = new int[PoolSize];
        private readonly AssetLease<AudioClip>[] _poolLeases = new AssetLease<AudioClip>[PoolSize];
        private GameObject _root;
        private AudioSource _musicSource;
        private AssetLease<AudioClip> _musicLease;
        private MusicTheme? _currentTheme;
        private int _themeRevision;
        private bool _initialized;
        private bool _disposed;

        public string Name => "Initializing audio...";
        public bool CanRetry => true;

        public AudioService(ISettingsService settings, IAssetService assets, AudioLibrary library,
            AudioMixer mixer)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _library = library;
            _mixer = mixer;
        }

        public UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_disposed)
                throw new ObjectDisposedException(nameof(AudioService));

            if (_initialized)
                return UniTask.CompletedTask;

            _root = new GameObject("AudioPool");
            Object.DontDestroyOnLoad(_root);

            for (var i = 0; i < PoolSize; i++)
            {
                var source = _root.AddComponent<AudioSource>();
                source.playOnAwake = false;
                ApplyMixerGroup(source, AudioGroup.UI);
                _pool[i] = source;
            }

            _musicSource = _root.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop = true;
            _musicSource.mute = _musicMuted;
            ApplyMixerGroup(_musicSource, AudioGroup.Music);

            var settings = _settings;
            settings.Changed += ApplyVolumes;
            ApplyVolumes();

            _initialized = true;
            ChangeTheme(MusicTheme.MainMenu);

            return UniTask.CompletedTask;
        }

        private void ApplyMixerGroup(AudioSource source, AudioGroup group)
        {
            if (_mixer == null) return;
            var groups = _mixer.FindMatchingGroups(group.ToString());
            source.outputAudioMixerGroup = groups != null && groups.Length > 0 ? groups[0] : null;
        }

        private void ApplyVolumes()
        {
            if (_mixer == null)
            {
                for (var i = 0; i < _pool.Length; i++)
                    if (_pool[i] != null) _pool[i].volume = EffectiveVolume(_poolGroups[i], _poolVolumes[i]);
                if (_musicSource != null) _musicSource.volume = EffectiveVolume(AudioGroup.Music, 1f);
                return;
            }
            var settings = _settings;
            SetMixerVolume(VolumeChannel.Master, settings);
            SetMixerVolume(VolumeChannel.Music, settings);
            SetMixerVolume(VolumeChannel.Sfx, settings);
            SetMixerVolume(VolumeChannel.Ui, settings);
            SetMixerVolume(VolumeChannel.Voice, settings);
        }

        private float EffectiveVolume(AudioGroup group, float volume)
        {
            if (_mixer != null) return volume;
            var channel = group == AudioGroup.Music ? VolumeChannel.Music :
                group == AudioGroup.Voice ? VolumeChannel.Voice :
                group == AudioGroup.UI || group == AudioGroup.Button ? VolumeChannel.Ui : VolumeChannel.Sfx;
            return volume * _settings.GetVolume(VolumeChannel.Master) * _settings.GetVolume(channel);
        }

        private void SetMixerVolume(VolumeChannel channel, ISettingsService settings)
        {
            if (!settings.Pack.Contains(SettingsPack.VolumeSetting(channel))) return;
            var linear = Mathf.Clamp(settings.GetVolume(channel), 0.0001f, 1f);
            var decibels = Mathf.Log10(linear) * 20f;
            _mixer.SetFloat(channel + "Volume", decibels);
        }

        // Plays an already-loaded clip (e.g. authored on a prefab) without going through
        // the asset service. Use PlayUiSound/ChangeTheme for Addressable library entries.
        public AudioPlaybackHandle PlayClip(AudioClip clip, AudioGroup group = AudioGroup.UI,
            AudioSource settings = null, float? pitch = null)
        {
            EnsureInitialized();

            if (clip == null)
                return null;

            var index = RentSource();
            var source = _pool[index];
            _poolGroups[index] = group;
            ApplyMixerGroup(source, group);
            source.clip = clip;
            source.pitch = pitch ?? (settings != null ? settings.pitch : 1f);
            _poolVolumes[index] = settings != null ? settings.volume : 1f;
            source.volume = EffectiveVolume(group, _poolVolumes[index]);
            source.loop = settings != null && settings.loop;
            source.Play();

            return new AudioPlaybackHandle(source, _poolVersion[index], _poolVersion, index);
        }

        private int RentSource()
        {
            for (var i = 0; i < _pool.Length; i++)
            {
                if (_pool[i].isPlaying) continue;
                _pool[i].clip = null;
                ReleasePoolLease(i);
                ++_poolVersion[i];
                return i;
            }

            // Pool exhausted: reuse slots in rotation and keep the pool bounded.
            var slot = _nextSlot++ % PoolSize;
            _pool[slot].Stop();
            _pool[slot].clip = null;
            ReleasePoolLease(slot);
            ++_poolVersion[slot];
            return slot;
        }

        private void ReleasePoolLease(int index)
        {
            _poolLeases[index]?.Dispose();
            _poolLeases[index] = null;
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

            for (var i = 0; i < _pool.Length; i++)
            {
                if (_poolGroups[i] != group) continue;
                _pool[i].Stop();
                _pool[i].clip = null;
                ++_poolVersion[i];
                ReleasePoolLease(i);
            }
        }

        public void PlayUiSound(UiSound sound)
        {
            EnsureInitialized();

            if (_library == null) return;

            switch (sound)
            {
                case UiSound.OnEnter:
                    PlayLibraryClipAsync(_library.OnEnter, AudioGroup.UI).Forget(Debug.LogException);
                    break;
                case UiSound.Click:
                    PlayLibraryClipAsync(_library.OnClick, AudioGroup.UI).Forget(Debug.LogException);
                    break;
                case UiSound.Coin:
                    PlayLibraryClipAsync(_library.OnCoin, AudioGroup.UI).Forget(Debug.LogException);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(sound));
            }
        }

        private async UniTask PlayLibraryClipAsync(AssetReference reference, AudioGroup group)
        {
            if (reference == null || string.IsNullOrEmpty(reference.AssetGUID)) return;

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

            var index = RentSource();
            _poolLeases[index] = lease;
            var source = _pool[index];
            _poolGroups[index] = group;
            ApplyMixerGroup(source, group);
            source.clip = lease.Asset;
            source.pitch = 1f;
            _poolVolumes[index] = 1f;
            source.volume = EffectiveVolume(group, 1f);
            source.loop = false;
            source.Play();
            ReleaseWhenFinishedAsync(index, _poolVersion[index]).Forget(Debug.LogException);
        }

        private async UniTask ReleaseWhenFinishedAsync(int index, int version)
        {
            try
            {
                await UniTask.WaitUntil(() => _disposed || _poolVersion[index] != version || !_pool[index].isPlaying,
                    cancellationToken: _lifetime.Token);
                if (_disposed || _poolVersion[index] != version) return;
                _pool[index].clip = null;
                ReleasePoolLease(index);
            }
            catch (OperationCanceledException) when (_disposed) { }
        }

        private bool _musicMuted;

        public void MuteMusic(bool muted)
        {
            _musicMuted = muted;
            if (_musicSource != null) _musicSource.mute = muted;
        }

        public void ChangeTheme(MusicTheme theme)
        {
            EnsureInitialized();

            if (_currentTheme == theme) return;
            _currentTheme = theme;
            var revision = ++_themeRevision;

            LoadThemeAsync(theme, revision).Forget(Debug.LogException);
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
            try
            {
                lease = await _assets.LoadAsync<AudioClip>(reference, _lifetime.Token);
            }
            catch (OperationCanceledException) when (_disposed)
            {
                return;
            }

            if (_disposed || revision != _themeRevision)
            {
                lease.Dispose();
                return;
            }

            _musicSource.Stop();
            _musicSource.clip = null;
            _musicLease?.Dispose();
            _musicLease = lease;
            _musicSource.volume = EffectiveVolume(AudioGroup.Music, 1f);
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
            _lifetime.Dispose();

            var settings = _settings;
            if (settings != null)
                settings.Changed -= ApplyVolumes;

            for (var i = 0; i < _pool.Length; i++)
            {
                if (_pool[i] != null) { _pool[i].Stop(); _pool[i].clip = null; }
                ++_poolVersion[i];
                ReleasePoolLease(i);
            }
            if (_musicSource != null) { _musicSource.Stop(); _musicSource.clip = null; }

            _musicLease?.Dispose();
            _musicLease = null;

            if (_root != null)
                Object.Destroy(_root);

            _musicSource = null;
        }
    }
}
