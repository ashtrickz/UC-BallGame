using System;
using Slime.Audio;
using Slime.Settings;
using UnityEngine;
using UnityEngine.Audio;

namespace Slime.Infrastructure.Audio
{
    /// <summary>Applies source gains and authored routing without applying mixer gains twice.</summary>
    public sealed class AudioMixerVolumes : IDisposable
    {
        private const string UiMixerGroup = "UI";
        private readonly ISettingsService _settings;
        private readonly AudioLibraryProvider _library;
        private readonly AudioReferences _configuration;
        private bool _initialized;

        public event Action Changed;

        public AudioMixerVolumes(ISettingsService settings, AudioLibraryProvider library, AudioReferences configuration)
        {
            _settings = settings;
            _library = library;
            _configuration = configuration;
        }

        public void Initialize()
        {
            if (_initialized)
                return;

            _settings.Changed += OnSettingsChanged;
            _initialized = true;
        }

        public void ApplyMixerGroup(AudioSource source, AudioGroup group, AudioMixerGroup output = null)
        {
            output = output != null ? output : _library.Value != null ? _library.Value.GetMixerGroup(group) : null;
            if (output == null && _configuration.Mixer != null)
            {
                var groups = _configuration.Mixer.FindMatchingGroups(group == AudioGroup.Button ? UiMixerGroup : group.ToString());
                if (groups.Length > 0)
                    output = groups[0];
            }
            source.outputAudioMixerGroup = output;
        }

        public float EffectiveVolume(AudioGroup group, float authoredVolume)
        {
            var channel = group == AudioGroup.Music ? VolumeChannel.Music :
                group == AudioGroup.Voice ? VolumeChannel.Voice :
                group == AudioGroup.UI || group == AudioGroup.Button ? VolumeChannel.Ui : VolumeChannel.Sfx;
            return authoredVolume * _settings.GetVolume(VolumeChannel.Master) * _settings.GetVolume(channel);
        }

        public void Dispose()
        {
            if (!_initialized)
                return;

            _settings.Changed -= OnSettingsChanged;
            _initialized = false;
            Changed = null;
        }

        private void OnSettingsChanged()
        {
            Changed?.Invoke();
        }
    }
}
