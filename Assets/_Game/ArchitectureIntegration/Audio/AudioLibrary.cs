using System;
using Slime.Audio;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;

namespace Slime.Infrastructure.Audio
{
    [Serializable]
    public sealed class SoundDefinition
    {
        private static readonly Vector2 DefaultPitchRange = new Vector2(0.95f, 1.05f);

        [Tooltip("Use one entry per ID. Fill the existing music entries instead of adding duplicates.")]
        public GameSound id;
        public AudioClip clip;
        [Tooltip("Optional additional takes; playback avoids choosing the same take twice in a row.")]
        public AudioClip[] variants = Array.Empty<AudioClip>();
        [Range(0, 1)] public float volume = 1f;
        [Tooltip("Random pitch on each play. Equal values disable variation (useful for music/loops).")]
        public Vector2 pitchRange = DefaultPitchRange;
        public AudioGroup group = AudioGroup.Sfx;
        [Tooltip("Optional per-sound override. Empty uses the library's routing for the selected group.")]
        public AudioMixerGroup mixerGroup;
        public bool loop;
        [Min(0)] public float minInterval;
        [Min(1)] public int maxVoices = 4;

        public int ClipCount
        {
            get
            {
                var count = clip != null ? 1 : 0;
                if (variants != null)
                    foreach (var variant in variants)
                        if (variant != null)
                            count++;
                return count;
            }
        }

        public AudioClip GetClip(int index)
        {
            if (clip != null)
            {
                if (index == 0)
                    return clip;

                index--;
            }
            if (variants != null)
                foreach (var variant in variants)
                    if (variant != null && index-- == 0)
                        return variant;

            return null;
        }
    }

    [Serializable]
    public sealed class AudioMixerRoute
    {
        public AudioGroup group;
        public AudioMixerGroup output;
    }

    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "Game/Audio/Audio Library")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Slime.Integration.Audio", sourceAssembly: "Game.Services", sourceClassName: "AudioLibrary")]
    public sealed class AudioLibrary : ScriptableObject
    {
        public const string ResourcePath = "Audio/AudioLibrary";

        [Tooltip("Assign your mixer groups here once. Cue group determines routing at runtime.")]
        [SerializeField] private AudioMixerRoute[] mixerRoutes = Array.Empty<AudioMixerRoute>();
        [SerializeField] private SoundDefinition[] sounds = Array.Empty<SoundDefinition>();

        // Keep existing Addressable references working for previously authored libraries.
        [SerializeField, HideInInspector] private AssetReference onEnter;
        [SerializeField, HideInInspector] private AssetReference onClick;
        [SerializeField, HideInInspector] private AssetReference onCoin;
        [SerializeField, HideInInspector] private AssetReference mainMenuTheme;
        [SerializeField, HideInInspector] private AssetReference gameplayTheme;

        public SoundDefinition[] Sounds
        {
            get => sounds;
            set => sounds = value ?? Array.Empty<SoundDefinition>();
        }
        public AudioMixerRoute[] MixerRoutes
        {
            get => mixerRoutes;
            set => mixerRoutes = value ?? Array.Empty<AudioMixerRoute>();
        }

        public bool TryGet(GameSound id, out SoundDefinition definition)
        {
            definition = null;
            foreach (var sound in sounds)
            {
                if (sound == null || sound.id != id)
                    continue;

                // An unfilled placeholder must not hide a later authored entry.
                if (sound.ClipCount > 0)
                {
                    definition = sound;
                    return true;
                }
                if (definition == null)
                    definition = sound;
            }
            return definition != null;
        }

        public AudioMixerGroup GetMixerGroup(AudioGroup group)
        {
            if (group == AudioGroup.Button)
                group = AudioGroup.UI;
            foreach (var route in mixerRoutes)
                if (route != null && route.group == group && route.output != null)
                    return route.output;

            return null;
        }

        public AssetReference OnEnter => onEnter;
        public AssetReference OnClick => onClick;
        public AssetReference OnCoin => onCoin;

        public AssetReference GetTheme(MusicTheme theme)
        {
            switch (theme)
            {
                case MusicTheme.MainMenu:
                    return mainMenuTheme;
                case MusicTheme.Gameplay:
                    return gameplayTheme;
                default:
                    throw new ArgumentOutOfRangeException(nameof(theme));
            }
        }
    }
}
