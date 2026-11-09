using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Slime.App.Game;
using Slime.Foundation;
using VContainer.Unity;
using Slime.Menu;

namespace Slime.Composition
{
    public sealed class MenuEntryPoint : IStartable, ISceneReadiness, IDisposable
    {
        private readonly MenuController _menu;
        private readonly GameSession _session;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly UniTaskCompletionSource _ready = new UniTaskCompletionSource();
        public UniTask Ready => _ready.Task;

        public MenuEntryPoint(MenuController menu, GameSession session)
        {
            _menu = menu;
            _session = session;
        }
        public void Start() => InitializeAsync().Forget();

        private async UniTask InitializeAsync()
        {
            try
            {
                _session.ReturnToMenu();
                await _menu.InitializeAsync(_lifetime.Token);
                _ready.TrySetResult();
            }
            catch (OperationCanceledException)
            {
                _ready.TrySetCanceled();
            }
            catch (Exception error)
            {
                _ready.TrySetException(error);
            }
        }

        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
            _ready.TrySetCanceled();
        }
    }
}
