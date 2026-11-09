using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Slime.App.Loading;
using Slime.Foundation;

namespace Slime.App.Scenes
{
    /// <summary>Owns project-lifetime navigation, loading context and retry state.</summary>
    public sealed class GameNavigation : IDisposable
    {
        public const string GameScene = "Game";
        public const string MenuScene = "Menu";
        private const string MissingDestinationMessage = "A scene name is required.";
        private readonly ISceneService _scenes;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private string _failed;
        private string _failedLoadingMessage;
        private bool _disposed;

        public bool IsTransitioning
        {
            get;
            private set;
        }
        public string LastError
        {
            get;
            private set;
        }
        public event Action Changed;

        public GameNavigation(ISceneService scenes)
        {
            _scenes = scenes;
        }

        public UniTask OpenAsync(string destination) =>
            OpenAsync(destination, destination == MenuScene ? LoadingMessageKeys.Menu : LoadingMessageKeys.Game);

        public async UniTask OpenAsync(string destination, string loadingMessageKey)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GameNavigation));

            if (IsTransitioning)
                return;

            if (string.IsNullOrWhiteSpace(destination))
                throw new ArgumentException(MissingDestinationMessage, nameof(destination));

            IsTransitioning = true;
            LastError = null;
            _failed = null;
            _failedLoadingMessage = null;
            try
            {
                Changed?.Invoke();
                // Existing fakes and integrations can keep the original contract until migration.
                if (_scenes is ISceneRequestService requests)
                    await requests.LoadSceneAsync(destination, loadingMessageKey, _lifetime.Token);
                else
                    await _scenes.LoadSceneAsync(destination, _lifetime.Token);
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception error)
            {
                LastError = error.Message;
                _failed = destination;
                _failedLoadingMessage = loadingMessageKey;
                throw;
            }
            finally
            {
                IsTransitioning = false;
                Changed?.Invoke();
            }
        }

        public UniTask RetryAsync() =>
            _failed != null ? OpenAsync(_failed, _failedLoadingMessage) : UniTask.CompletedTask;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _lifetime.Cancel();
            _lifetime.Dispose();
        }
    }
}
