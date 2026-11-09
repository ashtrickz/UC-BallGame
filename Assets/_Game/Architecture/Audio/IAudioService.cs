using Game.Foundation;
using System;
using UnityEngine;

namespace Game.Audio
{
    public enum MusicTheme
    {
        MainMenu,
        Gameplay
    }

    public enum UiSound
    {
        OnEnter,
        Click,
        /// <summary>A coin landing in the gold counter.</summary>
        Coin
    }

    public enum AudioGroup { UI, Button, Music, Ambience, Sfx, Voice }

    public interface IAudioService : IInitializationStep, IDisposable
    {
        public void ChangeTheme(MusicTheme theme);
        /// <summary>Silences the music without stopping it (the company's intro has its own sound).</summary>
        void MuteMusic(bool muted);
        public void PlayUiSound(UiSound sound);
        AudioPlaybackHandle PlaySound(GameSound sound, float volume = 1f);
        /// <summary>Freezes gameplay SFX/ambience, leaving UI and music available.</summary>
        void SetGameplayPaused(bool paused);
        AudioPlaybackHandle PlayClip(AudioClip clip, AudioGroup group = AudioGroup.UI,
            AudioSource settings = null, float? pitch = null);
        void StopGroup(AudioGroup group);
    }
}
