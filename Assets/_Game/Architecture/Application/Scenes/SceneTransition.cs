using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.App.Scenes
{
    public interface ISceneRuntime
    {
        string ActiveScene { get; }
        string BootstrapScene { get; }
        /// <summary>
        /// A scene to stand on while one game scene is swapped for another (Unity always needs a loaded scene): the bootstrap scene if it
        /// is still loaded (the first transition), otherwise an empty transit scene made for the occasion. Returns its name.
        /// </summary>
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
        private readonly ISceneRuntime _runtime;
        private bool _running;
        public SceneTransition(ISceneRuntime runtime) => _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

        public async UniTask LoadAsync(string destination, CancellationToken token, IProgress<float> progress = null)
        {
            if (_running) throw new InvalidOperationException("A scene transition is already running.");
            token.ThrowIfCancellationRequested();
            _runtime.ValidateDestination(destination);
            if (_runtime.ActiveScene == destination && _runtime.IsLoaded(destination)) return;
            if (_runtime.IsLoaded(destination)) throw new InvalidOperationException("Destination is already loaded outside navigation.");
            _running = true;
            try
            {
                var previous = _runtime.ActiveScene;
                var anchor = _runtime.EnsureAnchor();
                _runtime.Activate(anchor);
                if (previous != anchor && _runtime.IsLoaded(previous))
                    await _runtime.UnloadAsync(previous);
                progress?.Report(0.1f);
                token.ThrowIfCancellationRequested();
                try
                {
                    await _runtime.LoadAsync(destination, Progress.CreateOnlyValueChanged<float>(
                        value => progress?.Report(0.1f + value * 0.7f)));
                    token.ThrowIfCancellationRequested();
                    progress?.Report(0.85f);
                    await _runtime.WaitUntilReadyAsync(destination, token);
                    token.ThrowIfCancellationRequested();
                    progress?.Report(0.95f);
                    _runtime.Activate(destination);
                }
                catch
                {
                    // The anchor stays: the loading screen (persistent) offers a retry from here.
                    _runtime.Activate(anchor);
                    if (_runtime.IsLoaded(destination)) await _runtime.UnloadAsync(destination);
                    throw;
                }
                if (_runtime.IsLoaded(anchor)) await _runtime.UnloadAsync(anchor);
                progress?.Report(1f);
            }
            finally { _running = false; }
        }
    }
}
