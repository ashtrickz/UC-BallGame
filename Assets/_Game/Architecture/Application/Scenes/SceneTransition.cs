using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.App.Scenes
{
    public interface ISceneRuntime
    {
        string ActiveScene { get; }
        string BootstrapScene { get; }
        string EnsureAnchor();
        bool IsLoaded(string name);
        void ValidateDestination(string name);
        void Activate(string name);
        UniTask LoadAsync(string name, IProgress<float> progress);
        UniTask WaitUntilReadyAsync(string name, CancellationToken token);
        UniTask UnloadAsync(string name);
    }

    // Unity loads cannot be cancelled. Keep ownership until load/cleanup completes.
    // Dispose the previous scene before creating the next scope; the project container survives (DontDestroyOnLoad).
    // Once the destination is active the anchor goes too, the bootstrap scene included: only the game scene stays loaded, so nothing of the
    // bootstrap (its camera) draws over the game world.
    public sealed class SceneTransition
    {
        private const float CurtainClosedProgress = 0.1f;
        private const float SceneLoadShare = 0.7f;
        private const float SceneLoadedProgress = 0.85f;
        private const float SceneReadyProgress = 0.95f;

        private readonly ISceneRuntime _runtime;
        private bool _running;
        public SceneTransition(ISceneRuntime runtime) => _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

        public async UniTask LoadAsync(string destination, CancellationToken token, IProgress<float> progress = null)
        {
            if (_running)
                throw new InvalidOperationException("A scene transition is already running.");

            token.ThrowIfCancellationRequested();
            _runtime.ValidateDestination(destination);
            if (_runtime.ActiveScene == destination && _runtime.IsLoaded(destination))
                return;

            if (_runtime.IsLoaded(destination))
                throw new InvalidOperationException("Destination is already loaded outside navigation.");

            _running = true;
            try
            {
                var previous = _runtime.ActiveScene;
                var anchor = _runtime.EnsureAnchor();
                _runtime.Activate(anchor);
                if (previous != anchor && _runtime.IsLoaded(previous))
                    await _runtime.UnloadAsync(previous);
                progress?.Report(CurtainClosedProgress);
                token.ThrowIfCancellationRequested();
                try
                {
                    await _runtime.LoadAsync(destination, Progress.CreateOnlyValueChanged<float>(
                        value => progress?.Report(CurtainClosedProgress + value * SceneLoadShare)));
                    token.ThrowIfCancellationRequested();
                    progress?.Report(SceneLoadedProgress);
                    await _runtime.WaitUntilReadyAsync(destination, token);
                    token.ThrowIfCancellationRequested();
                    progress?.Report(SceneReadyProgress);
                    _runtime.Activate(destination);
                }
                catch
                {
                    // The anchor stays: the loading screen (persistent) offers a retry from here.
                    _runtime.Activate(anchor);
                    if (_runtime.IsLoaded(destination))
                        await _runtime.UnloadAsync(destination);
                    throw;
                }
                if (_runtime.IsLoaded(anchor))
                    await _runtime.UnloadAsync(anchor);
                progress?.Report(1f);
            }
            finally
            {
                _running = false;
            }
        }
    }
}
