using Game.Foundation;
using Game.UI;
using UnityEngine;

namespace Game.Integration.Composition
{
    /// <summary>Binds dynamic frame clients and layout services without view-container coupling.</summary>
    public sealed class UiLifecycleBinder
    {
        private readonly ITickRegistry _ticks;
        private readonly IScreenService _screen;

        public UiLifecycleBinder(ITickRegistry ticks, IScreenService screen)
        {
            _ticks = ticks;
            _screen = screen;
        }

        public void Bind(GameObject root)
        {
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (component is ITickRegistryClient client)
                    client.BindTicks(_ticks);

            foreach (var fitter in root.GetComponentsInChildren<SafeAreaFitter>(true))
                fitter.BindScreen(_screen);

            // Scene scopes bind their views, while safe-area wrappers can sit above those views.
            foreach (var fitter in root.GetComponentsInParent<SafeAreaFitter>(true))
                fitter.BindScreen(_screen);
        }
    }
}
