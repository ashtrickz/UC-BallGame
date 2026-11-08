using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.App.Game;
using Game.App.Scenes;
using Game.Foundation;
using Game.Integration.Game;
using VContainer.Unity;

namespace Game.Integration.Menu
{
    public sealed class MenuEntryPoint : IStartable, ISceneReadiness, IDisposable
    {
        private readonly MenuController _menu;
        private readonly GameSession _session;
        private readonly GameNavigation _navigation;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly UniTaskCompletionSource _ready = new UniTaskCompletionSource();
        public UniTask Ready => _ready.Task;

        public MenuEntryPoint(MenuController menu, GameSession session, GameNavigation navigation)
        { _menu = menu; _session = session; _navigation = navigation; }
        public void Start() => InitializeAsync().Forget();

        private async UniTask InitializeAsync()
        {
            try
            {
                _session.ReturnToMenu();
                await _menu.InitializeAsync(_lifetime.Token);
                _ready.TrySetResult();
                if (DemoCommandLine.IsRequested) OpenDemoAsync().Forget();
            }
            catch (OperationCanceledException) { _ready.TrySetCanceled(); }
            catch (Exception error) { _ready.TrySetException(error); }
        }

        private async UniTask OpenDemoAsync()
        {
            // Finish Loading -> Menu before requesting Menu -> Game.
            await UniTask.WaitUntil(() => !_navigation.IsTransitioning, cancellationToken: _lifetime.Token);
            _session.StartRound();
            await _navigation.OpenAsync(GameNavigation.GameScene);
        }

        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
            _ready.TrySetCanceled();
        }
    }
}
