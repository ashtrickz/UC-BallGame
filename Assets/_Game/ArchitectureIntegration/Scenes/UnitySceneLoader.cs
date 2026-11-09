using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.App.Bootstrap;
using Game.App.Scenes;
using Game.Integration.Composition;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Game.Integration.Scenes
{
    /// <summary>Adapts native scene operations and readiness scopes to the scene-transition service.</summary>
    public sealed class UnitySceneLoader : ISceneRuntime
    {
        public const string TransitScene = "Transit";
        private const float NativeActivationProgress = 0.9f;
        private const string InvalidSceneMessage = "Invalid game scene or scene not enabled in build settings: ";
        private const string ActivationFailedMessage = "Cannot activate scene: ";
        private const string LoadFailedMessage = "Cannot load scene: ";
        private const string UnloadFailedMessage = "Cannot unload scene: ";
        private const string InvalidScopeMessage = "Scene must have one active readiness scope parented to ProjectScope: ";
        private readonly ProjectScope _project;

        public string ActiveScene => SceneManager.GetActiveScene().name;
        // The persistent scope lives outside the bootstrap scene after Awake.
        public string BootstrapScene => BootstrapEntry.SceneName;

        public UnitySceneLoader(ProjectScope project)
        {
            _project = project;
        }

        public bool IsLoaded(string name) => SceneManager.GetSceneByName(name).isLoaded;

        public void ValidateDestination(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name == BootstrapScene || name == TransitScene
                || !Application.CanStreamedLevelBeLoaded(name))
                throw new InvalidOperationException(InvalidSceneMessage + name);
        }

        public string EnsureAnchor()
        {
            if (IsLoaded(BootstrapScene))
                return BootstrapScene;

            if (!IsLoaded(TransitScene))
                SceneManager.CreateScene(TransitScene);
            return TransitScene;
        }

        public void Activate(string name)
        {
            if (ActiveScene == name)
                return;

            if (!SceneManager.SetActiveScene(SceneManager.GetSceneByName(name)))
                throw new InvalidOperationException(ActivationFailedMessage + name);
        }

        public async UniTask LoadAsync(string name, IProgress<float> progress)
        {
            using (LifetimeScope.EnqueueParent(_project))
            {
                var operation = SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive);
                if (operation == null)
                    throw new InvalidOperationException(LoadFailedMessage + name);

                var nativeProgress = Progress.Create<float>(value =>
                    progress?.Report(Mathf.Clamp01(value / NativeActivationProgress)));
                await operation.ToUniTask(progress: nativeProgress);
                progress?.Report(1f);
            }
        }

        public async UniTask WaitUntilReadyAsync(string name, CancellationToken token)
        {
            var scopes = SceneManager.GetSceneByName(name).GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<ReadySceneScope>(true)).ToArray();
            if (scopes.Length != 1 || !scopes[0].isActiveAndEnabled || scopes[0].Container == null || scopes[0].Parent != _project)
                throw new InvalidOperationException(InvalidScopeMessage + name);

            await scopes[0].Ready.AttachExternalCancellation(token);
        }

        public async UniTask UnloadAsync(string name)
        {
            var operation = SceneManager.UnloadSceneAsync(SceneManager.GetSceneByName(name));
            if (operation == null)
                throw new InvalidOperationException(UnloadFailedMessage + name);

            await operation.ToUniTask();
        }
    }
}
