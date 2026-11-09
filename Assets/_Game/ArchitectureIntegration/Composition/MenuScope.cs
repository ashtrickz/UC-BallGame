using System;
using Game.App.Scenes;
using Game.Gameplay.Config;
using Game.Integration.Composition;
using Game.UI;
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
            builder.Register<IUiElementFactory, UiElementFactory>(Lifetime.Scoped);
            builder.Register<LevelSelectController>(Lifetime.Scoped).As<ILevelSelect>();
            builder.Register<MenuController>(Lifetime.Scoped);
            builder.RegisterEntryPoint<SlimeColorController>();
            builder.RegisterBuildCallback(r =>
            {
                var feedback = r.Resolve<global::Game.Integration.Audio.UiFeedbackBinder>();
                r.InjectGameObject(menuView.gameObject);
                feedback.Bind(menuView.gameObject);
                r.InjectGameObject(levelSelectView.gameObject);
                feedback.Bind(levelSelectView.gameObject);
            });
            builder.RegisterEntryPoint<MenuEntryPoint>().As<global::Game.Foundation.ISceneReadiness>();
#if GAME_DEBUG
            if (global::Game.Integration.Game.DemoCommandLine.IsRequested)
                builder.RegisterEntryPoint<global::Game.Integration.Game.DemoMenuEntryPoint>();
#endif
            builder.Register(_ => SceneViewTicker.ForScene(gameObject.scene), Lifetime.Scoped).As<ITickable>();
        }
    }
}
