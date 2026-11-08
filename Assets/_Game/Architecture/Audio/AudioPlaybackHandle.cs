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

        public AudioPlaybackHandle(AudioSource source, int version, int[] sourceVersion, int slot)
        {
            _source = source;
            _slot = slot;
            _version = version;
            _sourceVersion = sourceVersion;
        }

        public bool IsActive => _source != null && _sourceVersion[_slot] == _version;
        public bool IsPlaying => IsActive && _source.isPlaying;

        public void Stop()
        {
            if (IsActive) _source.Stop();
        }
    }
}
