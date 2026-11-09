using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Slime.App.Loading;
using Slime.Foundation;

namespace Slime.App.Scenes
{
    /// <summary>Coordinates loading presentation around an exclusive scene transition.</summary>
    public sealed class SceneService : ISceneRequestService
    {
        private const float CompletePercentage = 100f;
        private const string TransitionRunningMessage = "A scene transition is already running.";
        private readonly Func<string, IProgress<float>, CancellationToken, UniTask> _loadScene;
        private readonly ILoadingScreen _loading;
        private readonly IInitializationModel _initialization;
        private bool _transitioning;

        public SceneService(Func<string, IProgress<float>, CancellationToken, UniTask> loadScene, ILoadingScreen loading,
            IInitializationModel initialization)
        {
            _loadScene = loadScene;
            _loading = loading;
            _initialization = initialization;
        }

        public UniTask LoadSceneAsync(string sceneName, CancellationToken cancellationToken) =>
            LoadSceneAsync(sceneName, LoadingMessageKeys.Game, cancellationToken);

        public async UniTask LoadSceneAsync(string sceneName, string loadingMessageKey, CancellationToken cancellationToken)
        {
            if (_transitioning)
                throw new InvalidOperationException(TransitionRunningMessage);

            _transitioning = true;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var startPercentage = !_initialization.IsComplete
                    ? _initialization.InitPercentage : 0f;
                await _loading.ShowLoadingAsync(startPercentage, loadingMessageKey, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var progress = Progress.CreateOnlyValueChanged<float>(value =>
                    _loading.ShowLoading(startPercentage + value * (CompletePercentage - startPercentage), loadingMessageKey));
                await _loadScene(sceneName, progress, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                _loading.ShowLoading(CompletePercentage, LoadingMessageKeys.Ready);
                // Present the completed bar before the reveal starts.
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
                await _loading.HideLoadingAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _loading.ShowError(LoadingMessageKeys.Cancelled, false);
                throw;
            }
            catch (Exception exception)
            {
                _loading.ShowError(exception.Message, true);
                throw;
            }
            finally
            {
                _transitioning = false;
            }
        }

    }
}
