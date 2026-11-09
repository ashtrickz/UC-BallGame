using Slime.App.Loading;
using Slime.Audio;
using Slime.Gameplay.Presentation;
using Slime.Infrastructure.Audio;
using Slime.Settings;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Slime.Infrastructure.Frame;

namespace Slime.Composition
{
    /// <summary>Creates the authored intro and coordinates its temporary music mute.</summary>
    public sealed class IntroScreenFactory
    {
        private readonly IntroVideoView _prefab;
        private readonly Transform _parent;
        private readonly IAudioService _audio;
        private readonly ISettingsService _settings;
        private readonly IObjectResolver _resolver;
        private readonly UiFeedbackBinder _bindings;

        public IntroScreenFactory(IntroVideoView prefab, Transform parent, IAudioService audio,
            ISettingsService settings, IObjectResolver resolver, UiFeedbackBinder bindings)
        {
            _prefab = prefab;
            _parent = parent;
            _audio = audio;
            _settings = settings;
            _resolver = resolver;
            _bindings = bindings;
        }

        public IIntroScreen Create()
        {
            var intro = _resolver.Instantiate(_prefab, _parent);
            _bindings.Bind(intro.gameObject);
            _audio.MuteMusic(true);
            intro.Begin(_settings.GetVolume(VolumeChannel.Master) * _settings.GetVolume(VolumeChannel.Sfx), RestoreMusic);
            return intro;
        }

        private void RestoreMusic() => _audio.MuteMusic(false);
    }
}
