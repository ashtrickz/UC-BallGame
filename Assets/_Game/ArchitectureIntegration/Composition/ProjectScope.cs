using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Game.App.Bootstrap;
using Game.App.Game;
using Game.App.Loading;
using Game.App.Popups;
using Game.App.Scenes;
using Game.Assets;
using Game.Audio;
using Game.Foundation;
using Game.Integration.Audio;
using Game.Integration.Bootstrap;
using Game.Integration.Gameplay;
using Game.Integration.Scenes;
using Game.Integration.Settings;
using Game.Settings;
using Game.UI;
using Game.Users;
using UnityEngine;
using UnityEngine.Audio;
using VContainer;
using VContainer.Unity;

namespace Game.Integration.Composition
{
    public sealed class ProjectScope : LifetimeScope
    {
        [SerializeField] private LoadingScreenView loadingView;
        [SerializeField] private Transform popupRoot;
        [SerializeField] private SettingsPopupView settingsPopupPrefab;
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private AudioLibrary audioLibrary;
        [SerializeField] private IntroVideoView introVideoPrefab;
        [SerializeField] private bool mobileSettingsPack = true;
        public static ProjectScope Instance { get; private set; }
        private GameNavigation _navigation;
        private ILoadingScreen _loading;
        private IInitializationModel _initialization;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance() => Instance = null;

        protected override void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            if (loadingView == null || !loadingView.transform.IsChildOf(transform))
                throw new InvalidOperationException("ProjectScope needs a loading view under its persistent root.");
            if (popupRoot == null || !popupRoot.IsChildOf(transform))
                throw new InvalidOperationException("ProjectScope needs a popup root under its persistent root.");

            builder.Register<IInitializationModel, InitializationModel>(Lifetime.Singleton);
            builder.RegisterInstance<ILoadingScreenView>(loadingView);
            builder.Register<ILoadingScreen, LoadingScreenController>(Lifetime.Singleton);
            builder.Register<IAssetBackend, AddressablesAssetBackend>(Lifetime.Singleton);
            builder.Register<AssetService>(Lifetime.Singleton).AsSelf().As<IAssetService>();
            builder.Register<ISettingsRuntime>(_ => new UnitySettingsRuntime(mobileSettingsPack ? SettingsPack.Mobile : SettingsPack.Desktop), Lifetime.Singleton);
            builder.Register<ISettingsService, SettingsService>(Lifetime.Singleton);
            builder.Register<AudioService>(r => new AudioService(r.Resolve<ISettingsService>(), r.Resolve<IAssetService>(), audioLibrary, audioMixer), Lifetime.Singleton)
                .AsSelf().As<IAudioService>();
            builder.Register<UserRepository>(Lifetime.Singleton).AsSelf().As<IUserRepository>();
            builder.Register<IUserCache>(_ => new FileUserCache(Path.Combine(Application.persistentDataPath, "Users")), Lifetime.Singleton);
            builder.Register<IUserService, UserService>(Lifetime.Singleton);
            builder.Register<UserInitializationStep>(Lifetime.Singleton);
            builder.Register<IPopupService, PopupService>(Lifetime.Singleton);
            builder.Register(r =>
            {
                if (settingsPopupPrefab == null) throw new InvalidOperationException("Assign the settings popup prefab in ProjectScope.");
                return new SettingsPopupController(r.Instantiate(settingsPopupPrefab, popupRoot), r.Resolve<ISettingsService>(), r.Resolve<IUserRepository>());
            }, Lifetime.Transient);
            builder.Register<ISceneService>(r => new SceneService(new UnitySceneLoader(this).LoadAsync,
                r.Resolve<ILoadingScreen>(), r.Resolve<IInitializationModel>()), Lifetime.Singleton);
            builder.Register<GameNavigation>(Lifetime.Singleton);
            builder.Register<GameSession>(Lifetime.Singleton);

            if (introVideoPrefab == null) builder.Register<IIntroScreen, NoIntroScreen>(Lifetime.Singleton);
            else
            {
                var intro = Instantiate(introVideoPrefab, transform);
                builder.RegisterInstance<IIntroScreen>(intro);
                builder.RegisterBuildCallback(r =>
                {
                    var audio = r.Resolve<IAudioService>();
                    var settings = r.Resolve<ISettingsService>();
                    audio.MuteMusic(true);
                    intro.Begin(settings.GetVolume(VolumeChannel.Master) * settings.GetVolume(VolumeChannel.Sfx), () => audio.MuteMusic(false));
                });
            }

            builder.Register<IReadOnlyList<IInitializationStep>>(r => new IInitializationStep[]
            {
                r.Resolve<AssetService>(), r.Resolve<IAudioService>(), r.Resolve<UserInitializationStep>()
            }, Lifetime.Singleton);
            builder.RegisterBuildCallback(r =>
            {
                _navigation = r.Resolve<GameNavigation>();
                _loading = r.Resolve<ILoadingScreen>();
                _initialization = r.Resolve<IInitializationModel>();
                _loading.RetryRequested += RetryNavigation;
                gameObject.AddComponent<SettingsPersistence>();
                gameObject.AddComponent<HapticsLifecycle>();
                gameObject.AddComponent<UiClickSounds>().Bind(r.Resolve<IAudioService>());
            });
            builder.RegisterEntryPoint<BootstrapEntryPoint>();
        }

        private void RetryNavigation()
        {
            if (_initialization.IsComplete && _navigation.LastError != null)
                _navigation.RetryAsync().Forget(Debug.LogException);
        }

        protected override void OnDestroy()
        {
            if (_loading != null) _loading.RetryRequested -= RetryNavigation;
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }
    }
}
