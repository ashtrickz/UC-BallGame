using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Slime.App.Bootstrap;
using Slime.App.Game;
using Slime.App.Loading;
using Slime.App.Popups;
using Slime.App.Scenes;
using Slime.Assets;
using Slime.Audio;
using Slime.Foundation;
using Slime.Gameplay.Presentation;
using Slime.Infrastructure.Audio;
using Slime.Infrastructure.Bootstrap;
using Slime.Infrastructure.Settings;
using Slime.Infrastructure.Localization;
using Slime.Composition.Scenes;
using Slime.Localization;
using Slime.Settings;
using Slime.UI;
using Slime.Users;
using UnityEngine;
using UnityEngine.Audio;
using VContainer;
using VContainer.Unity;
using Slime.Infrastructure.Frame;

namespace Slime.Composition
{
    /// <summary>Registers project-lifetime services under the persistent bootstrap root.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Slime.Integration.Composition", sourceAssembly: "Game.Composition", sourceClassName: "ProjectScope")]
    public sealed class ProjectScope : LifetimeScope
    {
        private const string UserDirectoryName = "Users";
        private const string MissingLoadingViewMessage = "ProjectScope needs a loading view under its persistent root.";
        private const string MissingPopupRootMessage = "ProjectScope needs a popup root under its persistent root.";

        [SerializeField] private LoadingScreenView loadingView;
        [SerializeField] private Transform popupRoot;
        [SerializeField] private SettingsPopupView settingsPopupPrefab;
        [SerializeField] private AudioMixer audioMixer;

        [Tooltip("Optional override. Empty loads Resources/Audio/AudioLibrary.")] [SerializeField]
        private AudioLibrary audioLibrary;

        [SerializeField] private IntroVideoView introVideoPrefab;
        public static ProjectScope Instance { get; private set; }
        private GameNavigation _navigation;
        private ILoadingScreen _loading;
        private IInitializationModel _initialization;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance() => Instance = null;

        protected override void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            if (loadingView == null || !loadingView.transform.IsChildOf(transform))
                throw new InvalidOperationException(MissingLoadingViewMessage);

            if (popupRoot == null || !popupRoot.IsChildOf(transform))
                throw new InvalidOperationException(MissingPopupRootMessage);

            builder.Register<IInitializationModel, InitializationModel>(Lifetime.Singleton);
            builder.RegisterInstance<ILoadingScreenView>(loadingView);
            builder.Register<ILoadingScreen, LoadingScreenController>(Lifetime.Singleton);
            builder.Register<IAssetBackend, AddressablesAssetBackend>(Lifetime.Singleton);
            builder.Register<AssetService>(Lifetime.Singleton).AsSelf().As<IAssetService>();
            builder.Register<SettingsPackProvider>(Lifetime.Singleton);
            builder.Register(r => r.Resolve<SettingsPackProvider>().Value, Lifetime.Singleton);
            builder.Register<LanguageOptionsProvider>(Lifetime.Singleton);
#if UNITY_STANDALONE
            builder.Register<IDisplaySettings, StandaloneDisplaySettings>(Lifetime.Singleton);
#else
            builder.Register<IDisplaySettings, NullDisplaySettings>(Lifetime.Singleton);
#endif
            builder.Register<ISettingsRuntime, UnitySettingsRuntime>(Lifetime.Singleton);
            builder.Register<ISettingsService, SettingsService>(Lifetime.Singleton);
            builder.Register<LocalizationConfigProvider>(Lifetime.Singleton);
            builder.Register<LocalizationFontsProvider>(Lifetime.Singleton);
            builder.Register<LocalizationServiceFactory>(Lifetime.Singleton);
            builder.Register(r => r.Resolve<LocalizationServiceFactory>().Create(), Lifetime.Singleton)
                .AsSelf().As<ILocalizationService>();
            builder.RegisterInstance(new AudioReferences(audioLibrary, audioMixer, true));
            builder.Register<AudioLibraryProvider>(Lifetime.Singleton);
            builder.Register<AudioRandom>(Lifetime.Singleton);
            builder.Register<AudioVariation>(Lifetime.Singleton);
            builder.Register<AudioMixerVolumes>(Lifetime.Singleton);
            builder.Register<AudioSourcePool>(Lifetime.Singleton).AsSelf().As<ITickable>();
            builder.Register<MusicPlayer>(Lifetime.Singleton);
            builder.Register<AudioService>(Lifetime.Singleton).AsSelf().As<IAudioService>();
#if UNITY_ANDROID && !UNITY_EDITOR
            builder.Register<IHaptics, AndroidHaptics>(Lifetime.Singleton);
#else
            builder.Register<IHaptics, NullHaptics>(Lifetime.Singleton);
#endif
            builder.Register<IUiFeedback, UiFeedback>(Lifetime.Singleton);
            builder.Register<UiFeedbackBinder>(Lifetime.Singleton);
            builder.Register<UiLifecycleBinder>(Lifetime.Singleton);
            builder.Register<IFrameTimeSource, UnityFrameTimeSource>(Lifetime.Singleton);
            builder.Register<IScreenMetrics, UnityScreenMetrics>(Lifetime.Singleton);
            builder.RegisterEntryPoint<ScreenService>().AsSelf();
            builder.RegisterEntryPoint<TickRegistry>().AsSelf();
            builder.Register<UserRepository>(Lifetime.Singleton).AsSelf().As<IUserRepository>();
            builder.Register<IUserCache, FileUserCache>(Lifetime.Singleton)
                .WithParameter(Path.Combine(Application.persistentDataPath, UserDirectoryName));
            builder.Register<IUserService, UserService>(Lifetime.Singleton);
            builder.Register<UserInitializationStep>(Lifetime.Singleton);
            builder.Register<IPopupService, PopupService>(Lifetime.Singleton);
            builder.Register<SettingsPopupViewFactory>(Lifetime.Singleton)
                .WithParameter(settingsPopupPrefab)
                .WithParameter(popupRoot);
            builder.Register<ISettingsPopupView>(r => r.Resolve<SettingsPopupViewFactory>().Create(),
                Lifetime.Transient);
            builder.Register<SettingsPopupController>(Lifetime.Transient);
            builder.Register<ISceneRuntime, UnitySceneLoader>(Lifetime.Singleton);
            builder.Register<SceneTransition>(Lifetime.Singleton);
            builder.Register<Func<string, IProgress<float>, System.Threading.CancellationToken, UniTask>>(r =>
            {
                var transition = r.Resolve<SceneTransition>();
                return (scene, progress, token) => transition.LoadAsync(scene, token, progress);
            }, Lifetime.Singleton);
            builder.Register<SceneService>(Lifetime.Singleton).As<ISceneService>().As<ISceneRequestService>();
            builder.Register<GameNavigation>(Lifetime.Singleton);
            builder.Register<GameSession>(Lifetime.Singleton);
            builder.Register<IGameLauncher, GameLauncher>(Lifetime.Singleton);
            builder.Register<ICampaignProgress, PlayerPrefsCampaignProgress>(Lifetime.Singleton);
            builder.Register<IStarProgress, PlayerPrefsStarProgress>(Lifetime.Singleton);
            builder.Register<ISlimeColorPreferences, PlayerPrefsSlimeColorPreferences>(Lifetime.Singleton);

            if (introVideoPrefab == null)
                builder.Register<IIntroScreen, NoIntroScreen>(Lifetime.Singleton);
            else
            {
                builder.Register<IntroScreenFactory>(Lifetime.Singleton)
                    .WithParameter(introVideoPrefab)
                    .WithParameter(transform);
                builder.Register<IIntroScreen>(r => r.Resolve<IntroScreenFactory>().Create(), Lifetime.Singleton);
                builder.RegisterBuildCallback(r => r.Resolve<IIntroScreen>());
            }

            builder.Register<IReadOnlyList<IInitializationStep>>(r => new IInitializationStep[]
            {
                r.Resolve<LocalizationService>(), r.Resolve<AssetService>(), r.Resolve<IAudioService>(),
                r.Resolve<UserInitializationStep>()
            }, Lifetime.Singleton);
            builder.RegisterBuildCallback(r =>
            {
                r.InjectGameObject(loadingView.gameObject);
                r.Resolve<UiFeedbackBinder>().Bind(loadingView.gameObject);
                _navigation = r.Resolve<GameNavigation>();
                _loading = r.Resolve<ILoadingScreen>();
                _initialization = r.Resolve<IInitializationModel>();
                _loading.RetryRequested += RetryNavigation;
                gameObject.AddComponent<SettingsPersistence>();
                gameObject.AddComponent<HapticsLifecycle>().Bind(r.Resolve<IHaptics>());
                var clicks = gameObject.AddComponent<UiClickSounds>();
                clicks.Bind(r.Resolve<IAudioService>(), r.Resolve<IHaptics>());
                clicks.BindTicks(r.Resolve<ITickRegistry>());
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
            if (_loading != null)
                _loading.RetryRequested -= RetryNavigation;
            
            if (Instance == this)
                Instance = null;
            
            base.OnDestroy();
        }
    }
}
