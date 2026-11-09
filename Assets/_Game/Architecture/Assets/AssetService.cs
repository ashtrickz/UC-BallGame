using Game.Foundation;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Assets
{
    public sealed class AssetService : IAssetService, IInitializationStep, IDisposable
    {
        private const string LoadingKey = "loading.assets";

        private readonly IAssetBackend _backend;
        private readonly SynchronizationContext _mainContext;
        private readonly int _mainThread;
        private readonly HashSet<IRequest> _requests = new HashSet<IRequest>();
        private UniTaskCompletionSource _initialization;
        private bool _initialized;
        private bool _initializationFailed;
        private bool _disposed;
        private long _nextId;

        public AssetService(IAssetBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _mainContext = SynchronizationContext.Current
                           ?? throw new InvalidOperationException(
                               "AssetService must be constructed on the Unity main thread.");
            _mainThread = Thread.CurrentThread.ManagedThreadId;
        }

        public string Name => LoadingKey;

        // Addressables retains initialization state after native failure. Retry has not been
        // demonstrated safe; callers may retry waiting after cancellation, not native failure.
        public bool CanRetry => !_disposed && !_initializationFailed;

        public int ActiveRequestCount
        {
            get
            {
                AssertMainThread();
                return _requests.Count;
            }
        }

        public string[] GetRequestDiagnostics()
        {
            AssertMainThread();
            var snapshots = new List<string>(_requests.Count);
            foreach (var request in _requests)
                snapshots.Add(request.Diagnostic);
            return snapshots.ToArray();
        }

        public UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            AssertMainThread();
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            if (_initialized)
                return UniTask.CompletedTask;

            if (_initialization == null)
            {
                _initialization = new UniTaskCompletionSource();
                InitializeNativeAsync().Forget(Debug.LogException);
            }

            return cancellationToken.CanBeCanceled
                ? AwaitInitializationAsync(_initialization.Task, cancellationToken)
                : _initialization.Task;
        }

        private async UniTask AwaitInitializationAsync(UniTask shared, CancellationToken token)
        {
            var waiter = new UniTaskCompletionSource<bool>();
            using (token.Register(() =>
                   {
                       if (Thread.CurrentThread.ManagedThreadId == _mainThread)
                           waiter.TrySetCanceled(token);
                       else
                           _mainContext.Post(_ => waiter.TrySetCanceled(token), null);
                   }))
            {
                ForwardInitializationAsync(shared, waiter, token).Forget(Debug.LogException);
                await waiter.Task;
            }
        }

        private static async UniTask ForwardInitializationAsync(UniTask shared,
            UniTaskCompletionSource<bool> waiter, CancellationToken token)
        {
            try
            {
                await shared;
                if (token.IsCancellationRequested)
                    waiter.TrySetCanceled(token);
                else
                    waiter.TrySetResult(true);
            }
            catch (Exception error)
            {
                waiter.TrySetException(error);
            }
        }

        private async UniTask InitializeNativeAsync()
        {
            IAssetInitializationOperation operation = null;
            Exception failure = null;
            try
            {
                operation = _backend.InitializeAsync();
                await operation.Completion;
                AssertMainThread();
            }
            catch (Exception error)
            {
                failure = error;
            }
            finally
            {
                operation?.Dispose();
            }

            if (_disposed)
                _initialization.TrySetException(new ObjectDisposedException(nameof(AssetService)));

            else if (failure != null)
            {
                _initializationFailed = true;
                _initialization.TrySetException(new InvalidOperationException(
                    "[AssetService] Addressables initialization failed.",
                    failure));
            }
            else
            {
                _initialized = true;
                _initialization.TrySetResult();
            }
        }

        public UniTask<AssetLease<T>> LoadAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            AssertMainThread();
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("[AssetService] Asset key must not be empty.", nameof(key));

            if (!_initialized)
                throw new InvalidOperationException(
                    "[AssetService] AssetService must finish initialization before loading assets.");

            var request = new Request<T>(this, ++_nextId, key, cancellationToken);
            _requests.Add(request);
            request.Start();
            return request.Task;
        }

        public void Dispose()
        {
            AssertMainThread();

            if (_disposed)
                return;

            _disposed = true;
            _initialization?.TrySetException(new ObjectDisposedException(nameof(AssetService)));
            // Successful leases remove their request on Dispose; pending requests stay tracked
            // until native completion so the service continues to own their eventual release.
            foreach (var request in new List<IRequest>(_requests))
                request.Shutdown();
        }

        private void AssertMainThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThread)
                throw new InvalidOperationException(
                    "[AssetService] Asset ownership must be accessed on the Unity main thread.");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AssetService));
        }

        private interface IRequest
        {
            string Diagnostic { get; }
            void Shutdown();
        }

        private sealed class Request<T> : IRequest where T : UnityEngine.Object
        {
            private readonly AssetService _owner;
            private readonly long _id;
            private readonly string _key;
            private readonly CancellationToken _token;

            private readonly UniTaskCompletionSource<AssetLease<T>> _source =
                new UniTaskCompletionSource<AssetLease<T>>();

            private CancellationTokenRegistration _registration;
            private IAssetOperation<T> _operation;
            private AssetLease<T> _lease;
            private bool _completed;
            private bool _abandoned;
            private bool _released;

            public Request(AssetService owner, long id, string key, CancellationToken token)
            {
                _owner = owner;
                _id = id;
                _key = key;
                _token = token;
            }

            public UniTask<AssetLease<T>> Task => _source.Task;

            public string Diagnostic => $"[AssetService] {_id}: key '{_key}', type '{typeof(T).FullName}', " +
                                        (_lease != null ? "leased" :
                                            _abandoned ? "abandoned; awaiting native completion" : "loading");

            public void Start()
            {
                if (_token.CanBeCanceled)
                    _registration = _token.Register(() =>
                    {
                        // Background cancellation only queues intent; native state and client
                        // completion are changed on the main thread at its next pump.
                        if (Thread.CurrentThread.ManagedThreadId == _owner._mainThread)
                            Cancel();
                        else
                            _owner._mainContext.Post(_ => Cancel(), null);
                    });
                CompleteAsync().Forget(Debug.LogException);
            }

            private async UniTask CompleteAsync()
            {
                try
                {
                    // Cancellation may have raced the registration before native acquisition.
                    if (_token.IsCancellationRequested)
                    {
                        Cancel();
                        return;
                    }

                    _operation = _owner._backend.LoadAsync<T>(_key);
                    var asset = await _operation.Completion;
                    _owner.AssertMainThread();
                    _completed = true;
                    if (_abandoned || _owner._disposed || _token.IsCancellationRequested)
                    {
                        if (_token.IsCancellationRequested)
                            Cancel();
                        Release();
                        return;
                    }

                    if (asset == null)
                        throw new InvalidOperationException(
                            "[AssetService] The native operation returned a null asset.");
                    
                    _lease = new AssetLease<T>(asset, Release, _owner.AssertMainThread);
                    // Token lifetime ends at lease delivery, not lease disposal.
                    _registration.Dispose();
                    _source.TrySetResult(_lease);
                }
                catch (Exception error)
                {
                    _completed = true;
                    if (_token.IsCancellationRequested)
                        Cancel();
                    else
                        _source.TrySetException(new InvalidOperationException(
                            $"[AssetService] Asset request {_id} failed: key '{_key}', type '{typeof(T).FullName}'.",
                            error));
                    Release();
                }
                finally
                {
                    if (_operation == null)
                        Release();
                    if (_lease == null)
                        _registration.Dispose();
                }
            }

            private void Cancel()
            {
                _owner.AssertMainThread();
                if (_lease != null || _released)
                    return;

                _abandoned = true;
                _source.TrySetCanceled(_token);

                if (_completed)
                    Release();
            }

            public void Shutdown()
            {
                if (_lease != null)
                    _lease.Dispose();
                else
                {
                    _abandoned = true;
                    _source.TrySetException(new ObjectDisposedException(nameof(AssetService)));
                    _registration.Dispose();
                    if (_completed)
                        Release();
                }
            }

            private void Release()
            {
                _owner.AssertMainThread();
                
                if (_released)
                    return;
                
                _released = true;
                _registration.Dispose();
                _operation?.Dispose();
                _owner._requests.Remove(this);
            }
        }
    }
}
