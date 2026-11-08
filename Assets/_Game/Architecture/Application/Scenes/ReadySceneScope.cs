using Game.Foundation;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using VContainer;
using VContainer.Unity;

namespace Game.App.Scenes
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Game.Architecture.Bootstrap.Unity", sourceAssembly: "Assembly-CSharp", sourceClassName: "ReadySceneScope")]
    public abstract class ReadySceneScope : LifetimeScope
    {
        private readonly UniTaskCompletionSource _ready = new UniTaskCompletionSource();
        public UniTask Ready => _ready.Task;

        async void Start()
        {
            try
            {
                if (Container == null)
                    throw new InvalidOperationException("Scene scope was not built.");

                await InitializeContentAsync(this.GetCancellationTokenOnDestroy());
               
                _ready.TrySetResult();
            }
            catch (OperationCanceledException)
            {
                _ready.TrySetCanceled();
            }
            catch (Exception exception)
            {
                _ready.TrySetException(exception);
            }
        }

        // VContainer starts the entry point; the scene scope only waits for its result.
        protected virtual UniTask InitializeContentAsync(CancellationToken cancellationToken)
            => Container.Resolve<ISceneReadiness>().Ready.AttachExternalCancellation(cancellationToken);

        protected override void OnDestroy()
        {
            _ready.TrySetCanceled();
            base.OnDestroy();
        }
    }
}