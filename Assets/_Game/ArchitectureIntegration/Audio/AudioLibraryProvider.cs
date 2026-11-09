using UnityEngine;

namespace Game.Integration.Audio
{
    /// <summary>Loads the shared audio library while preserving an authored scene override.</summary>
    public sealed class AudioLibraryProvider
    {
        private readonly AudioReferences _configuration;
        private AudioLibrary _library;
        private bool _loaded;

        public AudioLibrary Value
        {
            get
            {
                if (!_loaded)
                {
                    _library = _configuration.Library != null
                        ? _configuration.Library
                        : _configuration.LoadDefaultLibrary ? Resources.Load<AudioLibrary>(AudioLibrary.ResourcePath) : null;
                    _loaded = true;
                }

                return _library;
            }
        }

        public AudioLibraryProvider(AudioReferences configuration)
        {
            _configuration = configuration;
        }
    }
}
