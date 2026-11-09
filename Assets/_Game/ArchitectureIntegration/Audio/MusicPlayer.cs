using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Slime.Assets;
using Slime.Audio;
using UnityEngine;

namespace Slime.Infrastructure.Audio
{
    /// <summary>Owns the persistent music voice and rejects superseded asynchronous theme loads.</summary>
    public sealed class MusicPlayer : IDisposable
    {
        private const float DefaultDopplerLevel = 1f;
        private readonly IAssetService _assets;
        private readonly AudioLibraryProvider _library;
        private readonly AudioMixerVolumes _volumes;
        private readonly AudioSourcePool _sources;
        private readonly AudioVariation _variation;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private AudioSource _musicSource;
        private AssetLease<AudioClip> _musicLease;
        private MusicTheme? _currentTheme;
        private float _musicVolume = 1;
        private int _themeRevision;
        private bool _musicMuted;
        private bool _disposed;

        public MusicPlayer(IAssetService assets, AudioLibraryProvider library, AudioMixerVolumes volumes,
            AudioSourcePool sources, AudioVariation variation)
        {
            _assets = assets;
            _library = library;
            _volumes = volumes;
            _sources = sources;
            _variation = variation;
        }

        public void Initialize()
        {
            _musicSource = _sources.CreateSource();
            _musicSource.loop = true;
            _musicSource.dopplerLevel = DefaultDopplerLevel;
            _musicSource.mute = _musicMuted;
            _volumes.ApplyMixerGroup(_musicSource, AudioGroup.Music);
            ApplyVolumes();
        }

        public void ApplyVolumes()
        {
            if (_musicSource != null)
                _musicSource.volume = _volumes.EffectiveVolume(AudioGroup.Music, _musicVolume);
        }

        public void Stop()
        {
            ++_themeRevision;
            if (_musicSource != null)
            {
                _musicSource.Stop();
                _musicSource.clip = null;
            }

            _musicLease?.Dispose();
            _musicLease = null;
            _currentTheme = null;
        }

        public void MuteMusic(bool muted)
        {
            _musicMuted = muted;
            if (_musicSource != null)
                _musicSource.mute = muted;
        }

        public void ChangeTheme(MusicTheme theme)
        {
            // Allow retry after a missing clip is assigned or a theme finishes playing.
            if (_currentTheme == theme && _musicSource.clip != null && _musicSource.isPlaying)
                return;

            _currentTheme = theme;
            var revision = ++_themeRevision;
            var id = theme == MusicTheme.MainMenu ? GameSound.MainMenuMusic : GameSound.GameplayMusic;
            if (_library.Value != null && _library.Value.TryGet(id, out var definition) && definition.ClipCount > 0)
            {
                _musicSource.Stop();
                _musicSource.clip = null;
                _musicLease?.Dispose();
                _musicLease = null;
                _musicVolume = Mathf.Clamp01(definition.volume);
                _musicSource.clip = definition.GetClip(0);
                _musicSource.pitch = _variation.PickPitch(definition.pitchRange, float.NaN);
                _musicSource.volume = _volumes.EffectiveVolume(AudioGroup.Music, _musicVolume);
                _musicSource.loop = definition.loop;
                _volumes.ApplyMixerGroup(_musicSource, AudioGroup.Music, definition.mixerGroup);
                _musicSource.Play();
            }
            else
                LoadThemeAsync(theme, revision).Forget(Debug.LogException);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _lifetime.Cancel();
            Stop();
            _musicSource = null;
            _lifetime.Dispose();
        }

        private async UniTask LoadThemeAsync(MusicTheme theme, int revision)
        {
            var reference = _library.Value != null ? _library.Value.GetTheme(theme) : null;
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
            _musicVolume = 1;
            _musicSource.volume = _volumes.EffectiveVolume(AudioGroup.Music, 1);
            _musicSource.pitch = 1;
            _musicSource.loop = true;
            _volumes.ApplyMixerGroup(_musicSource, AudioGroup.Music);
            _musicSource.clip = lease.Asset;
            _musicSource.Play();
        }

    }
}
