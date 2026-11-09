using System;
using Game.Assets;
using Game.Audio;
using UnityEngine;
using UnityEngine.Audio;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Game.Integration.Audio
{
    /// <summary>
    /// Owns bounded effect voices, leases and versioned playback handles. Handles call back into the pool by slot,
    /// and finished leased clips are released by the pool's own tick, so a playback allocates only its handle.
    /// </summary>
    public sealed class AudioSourcePool : IAudioVoices, ITickable, IDisposable
    {
        private const int PoolSize = 24;
        private const string RootName = "AudioPool";
        private const float MinimumPitch = 0.1f;
        private const float MaximumPitch = 3f;
        private const int UiPriority = 32;
        private const int LoopPriority = 64;
        private const int EffectPriority = 128;
        private readonly AudioMixerVolumes _volumes;
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
        private int _leasedSlots;
        private bool _disposed;
        private bool _gameplayPaused;

        public AudioSourcePool(AudioMixerVolumes volumes)
        {
            _volumes = volumes;
        }

        public void Initialize()
        {
            _root = new GameObject(RootName);
            Object.DontDestroyOnLoad(_root);
            for (var i = 0; i < PoolSize; i++)
                _pool[i] = CreateSource();
        }

        public AudioSource CreateSource()
        {
            var source = _root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.dopplerLevel = 0;
            return source;
        }

        public int ActiveVoices(GameSound sound)
        {
            var voices = 0;
            for (var i = 0; i < PoolSize; i++)
                if (_poolSounds[i] == sound && (_pool[i].isPlaying || _poolPaused[i]))
                    voices++;

            return voices;
        }

        public void ApplyVolumes()
        {
            for (var i = 0; i < PoolSize; i++)
                if (_pool[i] != null)
                    _pool[i].volume = _volumes.EffectiveVolume(_poolGroups[i], _poolVolumes[i]);
        }

        public void StopGroup(AudioGroup group)
        {
            for (var i = 0; i < PoolSize; i++)
                if (_poolGroups[i] == group)
                    ReleaseSlot(i);
        }

        public AudioPlaybackHandle StartClip(AudioClip clip, AudioGroup group, float baseVolume,
            float multiplier, float pitch, bool loop, AudioMixerGroup output, GameSound? sound,
            AssetLease<AudioClip> lease = null)
        {
            var index = RentSource();
            if (index < 0)
                return null;

            var source = _pool[index];
            _poolGroups[index] = group;
            _poolSounds[index] = sound;
            _poolBaseVolumes[index] = Mathf.Clamp01(baseVolume);
            _poolLeases[index] = lease;
            if (lease != null)
                _leasedSlots++;

            _poolVolumes[index] = _poolBaseVolumes[index] * Mathf.Clamp01(multiplier);
            _startedAt[index] = AudioSettings.dspTime;
            source.clip = clip;
            source.pitch = Mathf.Clamp(pitch, MinimumPitch, MaximumPitch);
            source.volume = _volumes.EffectiveVolume(group, _poolVolumes[index]);
            source.loop = loop;
            source.priority = group == AudioGroup.UI || group == AudioGroup.Button ? UiPriority : loop ? LoopPriority : EffectPriority;
            _volumes.ApplyMixerGroup(source, group, output);
            source.Play();
            ApplyPause(index);

            return new AudioPlaybackHandle(source, _poolVersion[index], _poolVersion, index, this);
        }

        public void SetGameplayPaused(bool paused)
        {
            _gameplayPaused = paused;
            for (var i = 0; i < PoolSize; i++)
                if (_pool[i].clip != null)
                    ApplyPause(i);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            for (var i = 0; i < PoolSize; i++)
                ReleaseSlot(i);

            if (_root != null)
                Object.Destroy(_root);
        }

        // Releases the clip lease of every playback that has finished on its own.
        public void Tick()
        {
            if (_disposed || _leasedSlots == 0)
                return;

            for (var i = 0; i < PoolSize; i++)
            {
                if (_poolLeases[i] != null && !_pool[i].isPlaying && !_poolPaused[i])
                    ReleaseSlot(i);
            }
        }

        void IAudioVoices.Stop(int slot)
        {
            ReleaseSlot(slot);
        }

        void IAudioVoices.SetVolume(int slot, float multiplier)
        {
            SetVolume(slot, multiplier);
        }

        void IAudioVoices.SetPaused(int slot, bool paused)
        {
            SetManualPaused(slot, paused);
        }

        bool IAudioVoices.IsPaused(int slot)
        {
            return _poolPaused[slot];
        }

        private void SetVolume(int index, float value)
        {
            _poolVolumes[index] = _poolBaseVolumes[index] * value;
            _pool[index].volume = _volumes.EffectiveVolume(_poolGroups[index], _poolVolumes[index]);
        }

        private void SetManualPaused(int index, bool paused)
        {
            _manualPaused[index] = paused;
            ApplyPause(index);
        }

        private int RentSource()
        {
            for (var i = 0; i < PoolSize; i++)
                if (!_pool[i].isPlaying && !_poolPaused[i])
                {
                    ReleaseSlot(i);
                    return i;
                }
            // Never steal a charge loop or a paused action. Other short effects remain bounded.
            var oldest = -1;
            for (var i = 0; i < PoolSize; i++)
                if (!_pool[i].loop && !_poolPaused[i] && (oldest < 0 || _startedAt[i] < _startedAt[oldest]))
                    oldest = i;
            if (oldest >= 0)
                ReleaseSlot(oldest);
            return oldest;
        }

        private void ReleaseSlot(int index)
        {
            if (_pool[index] != null)
            {
                _pool[index].Stop();
                _pool[index].clip = null;
            }
            _poolPaused[index] = _manualPaused[index] = false;
            _poolSounds[index] = null;
            ++_poolVersion[index];
            if (_poolLeases[index] == null)
                return;

            _poolLeases[index].Dispose();
            _poolLeases[index] = null;
            _leasedSlots--;
        }

        private void ApplyPause(int index)
        {
            var paused = _manualPaused[index] || (_gameplayPaused && IsGameplay(_poolGroups[index]));
            if (paused == _poolPaused[index])
                return;

            _poolPaused[index] = paused;
            if (paused)
                _pool[index].Pause();
            else
                _pool[index].UnPause();
        }

        private static bool IsGameplay(AudioGroup group) => group == AudioGroup.Sfx || group == AudioGroup.Ambience;
    }
}
