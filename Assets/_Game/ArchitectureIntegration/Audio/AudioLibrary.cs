using Game.Audio;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Game.Integration.Audio
{
    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "Game/Audio/Audio Library")]
    public sealed class AudioLibrary : ScriptableObject
    {
        [SerializeField] private AssetReference onEnter;
        [SerializeField] private AssetReference onClick;
        [SerializeField] private AssetReference onCoin;
        [SerializeField] private AssetReference mainMenuTheme;
        [SerializeField] private AssetReference gameplayTheme;

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
