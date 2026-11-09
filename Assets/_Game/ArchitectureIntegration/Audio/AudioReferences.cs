using UnityEngine.Audio;

namespace Slime.Infrastructure.Audio
{
    /// <summary>Scene-authored audio references consumed by the configuration provider.</summary>
    public sealed class AudioReferences
    {
        public AudioLibrary Library { get; }
        public AudioMixer Mixer { get; }
        public bool LoadDefaultLibrary { get; }

        public AudioReferences(AudioLibrary library, AudioMixer mixer, bool loadDefaultLibrary)
        {
            Library = library;
            Mixer = mixer;
            LoadDefaultLibrary = loadDefaultLibrary;
        }
    }
}
