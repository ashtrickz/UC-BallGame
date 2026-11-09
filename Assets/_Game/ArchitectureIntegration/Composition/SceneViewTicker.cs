using System.Collections.Generic;
using System;
using Slime.Gameplay.View;
using Slime.Foundation;
using UnityEngine.SceneManagement;
using UnityEngine;
using VContainer.Unity;
using Slime.Infrastructure.Frame;

namespace Slime.Composition
{
    /// <summary>
    /// Ticks frame views that live outside the gameplay frame, such as the slime preview in the menu,
    /// with unpaused real time.
    /// </summary>
    public sealed class SceneViewTicker : ITickable
    {
        private readonly IReadOnlyList<IFrameTickable> _views;

        public SceneViewTicker(IReadOnlyList<IFrameTickable> views)
        {
            _views = views ?? throw new ArgumentNullException(nameof(views));
        }

        public static SceneViewTicker ForScene(Scene scene)
        {
            var views = new List<IFrameTickable>();
            foreach (var root in scene.GetRootGameObjects())
                views.AddRange(root.GetComponentsInChildren<IFrameTickable>(true));

            return new SceneViewTicker(views);
        }

        public void Tick()
        {
            var delta = Time.deltaTime;
            for (var i = 0; i < _views.Count; i++)
            {
                if (_views[i] is Behaviour behaviour && !behaviour)
                    continue;

                _views[i].Tick(delta);
            }
        }
    }
}
