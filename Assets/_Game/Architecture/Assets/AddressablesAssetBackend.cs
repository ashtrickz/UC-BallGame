using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Slime.Assets
{
    public sealed class AddressablesAssetBackend : IAssetBackend
    {
        public IAssetInitializationOperation InitializeAsync() =>
            new InitializationOperation(Addressables.InitializeAsync(autoReleaseHandle: false));

        public IAssetOperation<T> LoadAsync<T>(string key) where T : UnityEngine.Object =>
            new LoadOperation<T>(Addressables.LoadAssetAsync<T>(key));

        private sealed class InitializationOperation : IAssetInitializationOperation
        {
            private readonly AsyncOperationHandle<IResourceLocator> _handle;
            private bool _released;
            public InitializationOperation(AsyncOperationHandle<IResourceLocator> handle)
            {
                _handle = handle;
            }
            // No cancellation token and no adapter auto-release: the service owns cleanup.
            public UniTask Completion => WaitAsync();
            private async UniTask WaitAsync()
            {
                await _handle.ToUniTask(autoReleaseWhenCanceled: false);
            }
            public void Dispose()
            {
                if (_released)
                    return;

                _released = true;
                if (_handle.IsValid())
                    Addressables.Release(_handle);
            }
        }

        private sealed class LoadOperation<T> : IAssetOperation<T> where T : UnityEngine.Object
        {
            private readonly AsyncOperationHandle<T> _handle;
            private bool _released;
            public LoadOperation(AsyncOperationHandle<T> handle)
            {
                _handle = handle;
            }
            public UniTask<T> Completion => _handle.ToUniTask(autoReleaseWhenCanceled: false);
            public void Dispose()
            {
                if (_released)
                    return;

                _released = true;
                if (_handle.IsValid())
                    Addressables.Release(_handle);
            }
        }
    }
}
