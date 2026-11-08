using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Foundation;

namespace Game.App.Scenes
{

    public sealed class GameNavigation : IDisposable
    {
        public const string GameScene = "Game";
        public const string MenuScene = "Menu";
        private readonly ISceneService _scenes;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private string _failed;
        public bool IsTransitioning { get; private set; }
        public string LastError { get; private set; }
        public event Action Changed;
        private bool _disposed;
        public GameNavigation(ISceneService scenes) => _scenes = scenes;
        // Owned by the project, not the button's scene: unloading the source must not cancel navigation.
        public async UniTask OpenAsync(string destination)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GameNavigation));
            if (IsTransitioning) return;
            if (string.IsNullOrWhiteSpace(destination)) throw new ArgumentException("A scene name is required.", nameof(destination));
            IsTransitioning = true; LastError = null; _failed = null;
            try
            {
                Changed?.Invoke();
                await _scenes.LoadSceneAsync(destination, _lifetime.Token);
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
            catch (Exception e) { LastError = e.Message; _failed = destination; throw; }
            finally { IsTransitioning = false; Changed?.Invoke(); }
        }
        public UniTask RetryAsync() => _failed != null ? OpenAsync(_failed) : UniTask.CompletedTask;
        public void Dispose() { if (_disposed) return; _disposed = true; _lifetime.Cancel(); _lifetime.Dispose(); }
    }
}
