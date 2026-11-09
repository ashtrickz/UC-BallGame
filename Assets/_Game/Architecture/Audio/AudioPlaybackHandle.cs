using System;
using UnityEngine;

namespace Game.Audio
{
    // A returned source can be reused; an old handle must never stop its next sound.
    public sealed class AudioPlaybackHandle
    {
        private readonly AudioSource _source;
        private readonly int _version;
        private readonly int _slot;
        private readonly int[] _sourceVersion;
        private readonly Action _stop;
        private readonly Action<float> _volume;
        private readonly Action<bool> _pause;
        private readonly Func<bool> _isPaused;

        public AudioPlaybackHandle(AudioSource source, int version, int[] sourceVersion, int slot,
            Action stop = null, Action<float> volume = null, Action<bool> pause = null, Func<bool> isPaused = null)
        {
            _source = source;
            _slot = slot;
            _version = version;
            _sourceVersion = sourceVersion;
            _stop = stop;
            _volume = volume;
            _pause = pause;
            _isPaused = isPaused;
        }

        public bool IsActive => _source != null && _sourceVersion[_slot] == _version;
        public bool IsPlaying => IsActive && _source.isPlaying;
        public bool IsPaused => IsActive && (_isPaused?.Invoke() ?? false);
        public AudioClip Clip => IsActive ? _source.clip : null;
        public float Pitch => IsActive ? _source.pitch : 0;
        public float Volume => IsActive ? _source.volume : 0;
        public int TimeSamples => IsActive ? _source.timeSamples : 0;

        /// <summary>Multiplier of the sound's authored base volume; channel settings still apply.</summary>
        public void SetVolume(float multiplier)
        {
            if (!IsActive) return;
            if (_volume != null) _volume(Mathf.Clamp01(multiplier));
            else _source.volume = Mathf.Clamp01(multiplier);
        }

        public void SetPitch(float pitch)
        {
            if (IsActive) _source.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
        }

        public void Pause()
        {
            if (!IsActive) return;
            if (_pause != null) _pause(true);
            else _source.Pause();
        }

        public void Resume()
        {
            if (!IsActive) return;
            if (_pause != null) _pause(false);
            else _source.UnPause();
        }

        public void Stop()
        {
            if (!IsActive) return;
            if (_stop != null) _stop();
            else _source.Stop();
        }
    }
}
