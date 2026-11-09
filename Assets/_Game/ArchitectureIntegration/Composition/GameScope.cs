using Slime.App.Scenes;
using Slime.Infrastructure.Frame;
using UnityEngine;
using VContainer;
using VContainer.Unity;
#if GAME_DEBUG
using Slime.Diagnostics;
#endif
using Slime.Gameplay.Presentation;

namespace Slime.Composition
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Slime.Integration.Game", sourceAssembly: "Game.Composition", sourceClassName: "GameScope")]
    public sealed class GameScope : ReadySceneScope
    {
        [SerializeField] private GameView view;
        [SerializeField] private ResultView resultView;
        [SerializeField] private Slime.Gameplay.Config.SlimeGameConfig gameConfig;
        [SerializeField] private Slime.Gameplay.View.GameplayStage gameplayStage;

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
            builder.RegisterInstance(gameConfig).AsSelf().As<Slime.Gameplay.Core.Levels.ILevelConfig>();
            builder.RegisterInstance(gameplayStage);
            builder.Register<Slime.Gameplay.Core.Generation.LevelGenerator>(Lifetime.Scoped);
            builder.Register<Slime.Gameplay.Core.Generation.WallGenerator>(Lifetime.Scoped);
            builder.Register<Slime.Gameplay.Core.Levels.ISeedProvider>(_ =>
                    new Slime.Gameplay.Core.Levels.SeedProvider(gameConfig.DebugFixedSeed,
                        System.Environment.TickCount),
                Lifetime.Scoped);
            builder.Register<Slime.Gameplay.Core.Levels.CampaignLevelSource>(Lifetime.Scoped)
                .As<Slime.Gameplay.Core.Levels.ILevelSource>();
            builder.Register<Slime.Gameplay.Core.Levels.WallLevelSource>(Lifetime.Scoped)
                .As<Slime.Gameplay.Core.Levels.ILevelSource>();
            builder.Register<Slime.Gameplay.Core.Levels.EndlessLevelSource>(Lifetime.Scoped)
                .As<Slime.Gameplay.Core.Levels.ILevelSource>();
            builder.Register<Slime.Gameplay.Core.Levels.ILevelFactory, Slime.Gameplay.Core.Levels.LevelFactory>(
                Lifetime.Scoped);
            builder.Register<CampaignAttemptPolicy>(Lifetime.Scoped).As<IAttemptModePolicy>();
            builder.Register<WallAttemptPolicy>(Lifetime.Scoped).As<IAttemptModePolicy>();
            builder.Register<EndlessAttemptPolicy>(Lifetime.Scoped).As<IAttemptModePolicy>();
            builder.Register<IAttemptFactory, AttemptFactory>(Lifetime.Scoped);
            builder.Register<ILevelPresenter, LevelPresenter>(Lifetime.Scoped);
            builder.Register<Slime.Gameplay.Presentation.GameplayAudioPresenter>(Lifetime.Scoped);
            builder.Register<Slime.Gameplay.Presentation.GameplayHapticsPresenter>(Lifetime.Scoped);
            builder.Register<CurrentAttempt>(Lifetime.Scoped);
            builder.Register<PauseService>(Lifetime.Scoped).AsSelf().As<IPauseService>();
            builder.Register<AttemptRunner>(Lifetime.Scoped);
            builder.Register<HudPresenter>(Lifetime.Scoped);
            builder.Register<ResultPresenter>(Lifetime.Scoped);
            builder.Register<ResultStarsPresenter>(Lifetime.Scoped);
            builder.Register<ChargeInputController>(Lifetime.Scoped);
            builder.RegisterEntryPoint<GameplayFrame>();
            builder.Register<SpotlightTargets>(Lifetime.Scoped).AsSelf().As<IPostLateTickable>();
            builder.Register<TutorialAttemptHook>(Lifetime.Scoped).AsSelf().As<IAttemptStartHook>();
            builder.Register<GameplayFlow>(Lifetime.Scoped);
#if GAME_DEBUG
            builder.Register<DebugGameplayOverrides>(Lifetime.Scoped).AsSelf().As<IGameplayOverrides>()
                .As<IAttemptStartHook>();
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
                r.Resolve<Slime.Infrastructure.Audio.UiFeedbackBinder>().Bind(view.gameObject);
                r.InjectGameObject(resultView.gameObject);
                r.Resolve<Slime.Infrastructure.Audio.UiFeedbackBinder>().Bind(resultView.gameObject);
                if (gameplayStage.Tutorial != null)
                {
                    r.InjectGameObject(gameplayStage.Tutorial.gameObject);
                    r.Resolve<Slime.Infrastructure.Audio.UiFeedbackBinder>()
                        .Bind(gameplayStage.Tutorial.gameObject);
                }
            });
            builder.RegisterEntryPoint<GameEntryPoint>().As<Slime.Foundation.ISceneReadiness>();
        }
    }
}
