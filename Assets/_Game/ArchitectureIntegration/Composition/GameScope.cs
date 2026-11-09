using Game.App.Scenes;
using Game.Integration.Composition;
using VContainer;
using VContainer.Unity;

namespace Game.Integration.Game
{
    public sealed class GameScope : ReadySceneScope
    {
        [UnityEngine.SerializeField] private GameView view;
        [UnityEngine.SerializeField] private ResultView resultView;
        [UnityEngine.SerializeField] private global::Game.Gameplay.Config.SlimeGameConfig gameConfig;
        [UnityEngine.SerializeField] private global::Game.Gameplay.View.GameplayStage gameplayStage;

        protected override void Awake()
        {
            parentReference = ParentReference.Create<ProjectScope>();
            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            if (view == null)
                throw new System.InvalidOperationException("GameScope requires its scene view.");

            if (resultView == null)
                throw new System.InvalidOperationException("GameScope requires its result view.");

            if (gameConfig == null)
                throw new System.InvalidOperationException("GameScope requires the slime game config.");

            if (gameplayStage == null)
                throw new System.InvalidOperationException("GameScope requires the gameplay stage.");

            builder.RegisterInstance(view);
            builder.RegisterInstance(resultView);
            builder.RegisterInstance(gameConfig).AsSelf().As<global::Game.Gameplay.Levels.ILevelConfig>();
            builder.RegisterInstance(gameplayStage);
            builder.Register<global::Game.Gameplay.Generation.LevelGenerator>(Lifetime.Scoped);
            builder.Register<global::Game.Gameplay.Generation.WallGenerator>(Lifetime.Scoped);
            builder.Register<global::Game.Gameplay.Levels.ISeedProvider>(_ =>
                new global::Game.Gameplay.Levels.SeedProvider(gameConfig.DebugFixedSeed, System.Environment.TickCount), Lifetime.Scoped);
            builder.Register<global::Game.Gameplay.Levels.CampaignLevelSource>(Lifetime.Scoped).As<global::Game.Gameplay.Levels.ILevelSource>();
            builder.Register<global::Game.Gameplay.Levels.WallLevelSource>(Lifetime.Scoped).As<global::Game.Gameplay.Levels.ILevelSource>();
            builder.Register<global::Game.Gameplay.Levels.EndlessLevelSource>(Lifetime.Scoped).As<global::Game.Gameplay.Levels.ILevelSource>();
            builder.Register<global::Game.Gameplay.Levels.ILevelFactory, global::Game.Gameplay.Levels.LevelFactory>(Lifetime.Scoped);
            builder.Register<CampaignAttemptPolicy>(Lifetime.Scoped).As<IAttemptModePolicy>();
            builder.Register<WallAttemptPolicy>(Lifetime.Scoped).As<IAttemptModePolicy>();
            builder.Register<EndlessAttemptPolicy>(Lifetime.Scoped).As<IAttemptModePolicy>();
            builder.Register<IAttemptFactory, AttemptFactory>(Lifetime.Scoped);
            builder.Register<ILevelPresenter, LevelPresenter>(Lifetime.Scoped);
            builder.Register<global::Game.Integration.Audio.GameplayAudioPresenter>(Lifetime.Scoped);
            builder.Register<global::Game.Integration.Audio.GameplayHapticsPresenter>(Lifetime.Scoped);
            builder.Register<CurrentAttempt>(Lifetime.Scoped);
            builder.Register<PauseService>(Lifetime.Scoped).AsSelf().As<IPauseService>();
            builder.Register<AttemptRunner>(Lifetime.Scoped);
            builder.Register<HudPresenter>(Lifetime.Scoped);
            builder.Register<ResultPresenter>(Lifetime.Scoped);
            builder.Register<ChargeInputController>(Lifetime.Scoped);
            builder.RegisterEntryPoint<GameplayFrame>();
            builder.Register<SpotlightTargets>(Lifetime.Scoped).AsSelf().As<IPostLateTickable>();
            builder.Register<TutorialAttemptHook>(Lifetime.Scoped).AsSelf().As<IAttemptStartHook>();
            builder.Register<GameplayFlow>(Lifetime.Scoped);
#if GAME_DEBUG
            builder.Register<DebugGameplayOverrides>(Lifetime.Scoped).AsSelf().As<IGameplayOverrides>().As<IAttemptStartHook>();
            builder.Register<IGameplayDebug, GameplayDebug>(Lifetime.Scoped);
            if (DemoCommandLine.IsRequested)
                builder.RegisterEntryPoint<DemoGameEntryPoint>();
#else
            builder.Register<IGameplayOverrides, NoGameplayOverrides>(Lifetime.Scoped);
#endif
            builder.Register<GameController>(Lifetime.Scoped);
            builder.RegisterBuildCallback(r =>
            {
                r.InjectGameObject(view.gameObject);
                r.Resolve<global::Game.Integration.Audio.UiFeedbackBinder>().Bind(view.gameObject);
                r.InjectGameObject(resultView.gameObject);
                r.Resolve<global::Game.Integration.Audio.UiFeedbackBinder>().Bind(resultView.gameObject);
                if (gameplayStage.Tutorial != null)
                {
                    r.InjectGameObject(gameplayStage.Tutorial.gameObject);
                    r.Resolve<global::Game.Integration.Audio.UiFeedbackBinder>().Bind(gameplayStage.Tutorial.gameObject);
                }
            });
            builder.RegisterEntryPoint<GameEntryPoint>().As<global::Game.Foundation.ISceneReadiness>();
        }
    }
}
