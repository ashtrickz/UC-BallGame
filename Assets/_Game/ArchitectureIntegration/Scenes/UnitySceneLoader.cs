using Game.App.Bootstrap;
using Game.App.Scenes;
using Game.Integration.Composition;
using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Game.Integration.Scenes
{
    public sealed class UnitySceneLoader : ISceneRuntime
    {
        private readonly ProjectScope _project;
        private readonly SceneTransition _transition;
        public UnitySceneLoader(ProjectScope project) { _project = project; _transition = new SceneTransition(this); }
        public string ActiveScene => SceneManager.GetActiveScene().name;
        // ProjectScope is moved to DontDestroyOnLoad, so gameObject.scene is NOT the bootstrap scene.
        public string BootstrapScene => BootstrapEntry.SceneName;
        /// <summary>The empty scene a transition stands on once the bootstrap scene is gone.</summary>
        public const string TransitScene = "Transit";
        public bool IsLoaded(string name) => SceneManager.GetSceneByName(name).isLoaded;
        public UniTask LoadAsync(string name, IProgress<float> progress, CancellationToken token) => _transition.LoadAsync(name, token, progress);
        public void ValidateDestination(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name == BootstrapScene || name == TransitScene || !UnityEngine.Application.CanStreamedLevelBeLoaded(name))
                throw new InvalidOperationException($"Invalid game scene or scene not enabled in build settings: {name}");
        }
        public string EnsureAnchor()
        {
            if (IsLoaded(BootstrapScene)) return BootstrapScene;
            if (!IsLoaded(TransitScene)) SceneManager.CreateScene(TransitScene);
            return TransitScene;
        }
        public void Activate(string name)
        {
            if (ActiveScene == name) return;
            if (!SceneManager.SetActiveScene(SceneManager.GetSceneByName(name)))
                throw new InvalidOperationException($"Cannot activate scene: {name}");
        }
        public async UniTask LoadAsync(string name, IProgress<float> progress)
        {
            using (LifetimeScope.EnqueueParent(_project))
            {
                var op = SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive);
                if (op == null) throw new InvalidOperationException($"Cannot load scene: {name}");
                while (!op.isDone)
                {
                    // Native loading reaches 0.9 before scene activation.
                    progress?.Report(UnityEngine.Mathf.Clamp01(op.progress / 0.9f));
                    await UniTask.Yield();
                }
                progress?.Report(1f);
            }
        }
        public async UniTask WaitUntilReadyAsync(string name, CancellationToken token)
        {
            var scopes = SceneManager.GetSceneByName(name).GetRootGameObjects()
                .SelectMany(x => x.GetComponentsInChildren<ReadySceneScope>(true)).ToArray();
            if (scopes.Length != 1 || !scopes[0].isActiveAndEnabled || scopes[0].Container == null || scopes[0].Parent != _project)
                throw new InvalidOperationException($"Scene must have one active readiness scope parented to ProjectScope: {name}");
            await scopes[0].Ready.AttachExternalCancellation(token);
        }
        public async UniTask UnloadAsync(string name)
        {
            var op = SceneManager.UnloadSceneAsync(SceneManager.GetSceneByName(name));
            if (op == null) throw new InvalidOperationException($"Cannot unload scene: {name}");
            await UniTask.WaitUntil(() => op.isDone);
        }
    }
}
