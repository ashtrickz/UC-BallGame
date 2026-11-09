using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Foundation;
using VContainer.Unity;

namespace Game.Integration.Game
{
    public sealed class GameEntryPoint : IStartable, ISceneReadiness, IDisposable
    {
        private readonly GameController _game;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly UniTaskCompletionSource _ready = new UniTaskCompletionSource();
        public UniTask Ready => _ready.Task;
        public GameEntryPoint(GameController game) => _game = game;
        public void Start() => InitializeAsync().Forget();

        private async UniTask InitializeAsync()
        {
            try
            {
                await _game.InitializeAsync(_lifetime.Token);
                _lifetime.Token.ThrowIfCancellationRequested();
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
