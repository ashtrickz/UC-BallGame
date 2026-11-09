using System;
using Game.App.Scenes;
using Game.Gameplay.Config;
using Game.Integration.Composition;
using Game.Integration.Game;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Integration.Menu
{
    public sealed class MenuScope : ReadySceneScope
    {
        [SerializeField] private MenuView menuView;
        [SerializeField] private LevelSelectView levelSelectView;
        [SerializeField] private SlimeGameConfig gameConfig;

        protected override void Awake()
        {
            parentReference = ParentReference.Create<ProjectScope>();
            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            if (!menuView || !levelSelectView || !gameConfig)
                throw new InvalidOperationException("MenuScope requires its menu, level selection and config.");
            builder.RegisterInstance<IMenuView>(menuView);
            builder.RegisterInstance(levelSelectView);
            builder.RegisterInstance(gameConfig);
            builder.Register<ICampaignProgress, PlayerPrefsCampaignProgress>(Lifetime.Scoped);
            builder.Register<LevelSelectController>(Lifetime.Scoped).As<ILevelSelect>();
            builder.Register<MenuController>(Lifetime.Scoped);
            builder.RegisterBuildCallback(r =>
            {
                r.InjectGameObject(menuView.gameObject);
                r.InjectGameObject(levelSelectView.gameObject);
            });
            builder.RegisterEntryPoint<MenuEntryPoint>().As<global::Game.Foundation.ISceneReadiness>();
        }
    }
}
