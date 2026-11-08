using Game.App.Scenes;
using Game.Integration.Composition;
using VContainer;
using VContainer.Unity;

namespace Game.Integration.Game
{
    public sealed class GameScope : ReadySceneScope
    {
        [UnityEngine.SerializeField] private GameView view;
        [UnityEngine.SerializeField] private global::Game.Gameplay.Config.SlimeGameConfig gameConfig;
        [UnityEngine.SerializeField] private global::Game.Gameplay.View.GameplayStage gameplayStage;

        protected override void Awake()
        {
            parentReference = ParentReference.Create<ProjectScope>();
            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            if (view == null) throw new System.InvalidOperationException("GameScope requires its scene view.");
            if (gameConfig == null) throw new System.InvalidOperationException("GameScope requires the slime game config.");
            if (gameplayStage == null) throw new System.InvalidOperationException("GameScope requires the gameplay stage.");
            builder.RegisterInstance(view);
            builder.RegisterInstance(gameConfig);
            builder.RegisterInstance(gameplayStage);
            builder.Register<global::Game.Gameplay.Generation.LevelGenerator>(Lifetime.Scoped);
            builder.Register<ICampaignProgress, PlayerPrefsCampaignProgress>(Lifetime.Scoped);
            builder.Register<GameplayController>(Lifetime.Scoped);
            builder.Register<GameController>(Lifetime.Scoped);
            builder.RegisterEntryPoint<GameEntryPoint>().As<global::Game.Foundation.ISceneReadiness>();
        }
    }
}
