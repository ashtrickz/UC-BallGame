using Slime.Foundation;
using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Slime.Assets;
using UnityEngine;
using UnityEngine.TestTools;

namespace Slime.Tests.EditMode
{
    public sealed class AssetOwnershipTests
    {
        private sealed class Backend : IAssetBackend
        {
            public readonly Operation Operation = new Operation();
            public IAssetInitializationOperation InitializeAsync() => new Initialization();
            public IAssetOperation<T> LoadAsync<T>(string key) where T : UnityEngine.Object => (IAssetOperation<T>)(object)Operation;
        }
        private sealed class Initialization : IAssetInitializationOperation
        {
            public UniTask Completion => UniTask.CompletedTask;
            public void Dispose() { }
        }
        private sealed class Operation : IAssetOperation<Texture2D>
        {
            public readonly UniTaskCompletionSource<Texture2D> Source = new UniTaskCompletionSource<Texture2D>();
            public int Releases;
            public UniTask<Texture2D> Completion => Source.Task;
            public void Dispose() => Releases++;
        }

        [UnityTest]
        public IEnumerator Cancelled_load_releases_only_after_native_completion() => UniTask.ToCoroutine(async () =>
        {
            var backend = new Backend();
            using var assets = new AssetService(backend);
            await assets.InitializeAsync(default);
            using var cancellation = new CancellationTokenSource();
            var pending = assets.LoadAsync<Texture2D>("test", cancellation.Token);
            cancellation.Cancel();
            try { await pending; Assert.Fail("Expected cancellation."); } catch (OperationCanceledException) { }
            Assert.AreEqual(0, backend.Operation.Releases);
            Assert.AreEqual(1, assets.ActiveRequestCount);
            backend.Operation.Source.TrySetResult(Texture2D.whiteTexture);
            Assert.AreEqual(1, backend.Operation.Releases);
            Assert.AreEqual(0, assets.ActiveRequestCount);
        });

        [UnityTest]
        public IEnumerator Delivered_lease_outlives_request_token_and_releases_once() => UniTask.ToCoroutine(async () =>
        {
            var backend = new Backend();
            using var assets = new AssetService(backend);
            await assets.InitializeAsync(default);
            using var cancellation = new CancellationTokenSource();
            var pending = assets.LoadAsync<Texture2D>("test", cancellation.Token);
            backend.Operation.Source.TrySetResult(Texture2D.whiteTexture);
            var lease = await pending;
            cancellation.Cancel();
            Assert.AreSame(Texture2D.whiteTexture, lease.Asset);
            Assert.AreEqual(0, backend.Operation.Releases);
            lease.Dispose(); lease.Dispose();
            Assert.AreEqual(1, backend.Operation.Releases);
            Assert.Throws<ObjectDisposedException>(() => { var ignored = lease.Asset; });
        });

        [UnityTest]
        public IEnumerator Disposed_service_keeps_pending_native_operation_owned_until_it_finishes() => UniTask.ToCoroutine(async () =>
        {
            var backend = new Backend();
            var assets = new AssetService(backend);
            await assets.InitializeAsync(default);
            var pending = assets.LoadAsync<Texture2D>("test");
            assets.Dispose();
            try { await pending; Assert.Fail("Expected disposal."); } catch (ObjectDisposedException) { }
            Assert.AreEqual(0, backend.Operation.Releases);
            backend.Operation.Source.TrySetResult(Texture2D.whiteTexture);
            Assert.AreEqual(1, backend.Operation.Releases);
        });
    }
}
