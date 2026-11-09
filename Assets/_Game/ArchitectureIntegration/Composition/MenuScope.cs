using System;
using Slime.App.Scenes;
using Slime.Gameplay.Config;
using Slime.Gameplay.Core.Config;
using Slime.Infrastructure.Frame;
using Slime.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Slime.Menu;

namespace Slime.Composition
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Slime.Integration.Menu", sourceAssembly: "Game.Composition", sourceClassName: "MenuScope")]
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
                var feedback = r.Resolve<Slime.Infrastructure.Audio.UiFeedbackBinder>();
                r.InjectGameObject(menuView.gameObject);
                feedback.Bind(menuView.gameObject);
                r.InjectGameObject(levelSelectView.gameObject);
                feedback.Bind(levelSelectView.gameObject);
            });
            builder.RegisterEntryPoint<MenuEntryPoint>().As<Slime.Foundation.ISceneReadiness>();
#if GAME_DEBUG
            if (Slime.Diagnostics.DemoCommandLine.IsRequested)
                builder.RegisterEntryPoint<Slime.Diagnostics.DemoMenuEntryPoint>();
#endif
            builder.Register(_ => SceneViewTicker.ForScene(gameObject.scene), Lifetime.Scoped).As<ITickable>();
        }
    }
}
