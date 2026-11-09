using Game.App.Loading;
using Game.Foundation;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.App.Scenes
{
    public sealed class SceneService : ISceneService
    {
        private readonly Func<string, IProgress<float>, CancellationToken, UniTask> _loadScene;
        private readonly ILoadingScreen _loading;
        private readonly IInitializationModel _initialization;
        private bool _transitioning;

        public SceneService(Func<string, CancellationToken, UniTask> loadScene, ILoadingScreen loading)
            : this((name, _, token) => loadScene(name, token), loading) { }

        public SceneService(Func<string, IProgress<float>, CancellationToken, UniTask> loadScene, ILoadingScreen loading,
            IInitializationModel initialization = null)
        {
            this._loadScene = loadScene;
            this._loading = loading;
            _initialization = initialization;
        }

        public async UniTask LoadSceneAsync(string sceneName, CancellationToken cancellationToken)
        {
            if (_transitioning)
                throw new InvalidOperationException("A scene transition is already running.");

            _transitioning = true;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var startPercentage = _initialization != null && !_initialization.IsComplete
                    ? _initialization.InitPercentage : 0f;
                var messageKey = sceneName == GameNavigation.MenuScene ? "loading.menu" : "loading.game";
                await _loading.ShowLoadingAsync(startPercentage, messageKey, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var progress = Progress.CreateOnlyValueChanged<float>(value =>
                    _loading.ShowLoading(startPercentage + value * (100f - startPercentage), messageKey));
                await _loadScene(sceneName, progress, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                _loading.ShowLoading(100f, "loading.ready");
                // Present the completed bar before the reveal starts.
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
                await _loading.HideLoadingAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _loading.ShowError("loading.cancelled", false);
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
