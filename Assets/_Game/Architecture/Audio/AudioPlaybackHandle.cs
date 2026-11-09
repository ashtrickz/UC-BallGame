using UnityEngine;

namespace Slime.Audio
{
    /// <summary>Controls the pooled voice behind a playback handle; implemented once by the owning pool, not per sound.</summary>
    public interface IAudioVoices
    {
        void Stop(int slot);

        void SetVolume(int slot, float multiplier);

        void SetPaused(int slot, bool paused);

        bool IsPaused(int slot);
    }

    /// <summary>
    /// One playback on a pooled source. A returned source can be reused; an old handle must never stop its next
    /// sound, so every call first checks that the slot still runs this playback's version.
    /// </summary>
    public sealed class AudioPlaybackHandle
    {
        private const float MinPitch = 0.1f;
        private const float MaxPitch = 3f;

        private readonly AudioSource _source;
        private readonly int _version;
        private readonly int _slot;
        private readonly int[] _sourceVersion;
        private readonly IAudioVoices _voices;

        public AudioPlaybackHandle(AudioSource source, int version, int[] sourceVersion, int slot, IAudioVoices voices = null)
        {
            _source = source;
            _slot = slot;
            _version = version;
            _sourceVersion = sourceVersion;
            _voices = voices;
        }

        public bool IsActive => _source != null && _sourceVersion[_slot] == _version;

        public bool IsPlaying => IsActive && _source.isPlaying;

        public bool IsPaused => IsActive && _voices != null && _voices.IsPaused(_slot);

        public AudioClip Clip => IsActive ? _source.clip : null;

        public float Pitch => IsActive ? _source.pitch : 0;

        public float Volume => IsActive ? _source.volume : 0;

        public int TimeSamples => IsActive ? _source.timeSamples : 0;

        public void SetVolume(float multiplier)
        {
            if (!IsActive)
                return;

            if (_voices != null)
                _voices.SetVolume(_slot, Mathf.Clamp01(multiplier));
            else
                _source.volume = Mathf.Clamp01(multiplier);
        }

        public void SetPitch(float pitch)
        {
            if (IsActive)
                _source.pitch = Mathf.Clamp(pitch, MinPitch, MaxPitch);
        }

        public void Pause()
        {
            if (!IsActive)
                return;

            if (_voices != null)
                _voices.SetPaused(_slot, true);
            else
                _source.Pause();
        }

        public void Resume()
        {
            if (!IsActive)
                return;

            if (_voices != null)
                _voices.SetPaused(_slot, false);
            else
                _source.UnPause();
        }

        public void Stop()
        {
            if (!IsActive)
                return;

            if (_voices != null)
                _voices.Stop(_slot);
            else
                _source.Stop();
        }
    }
}
