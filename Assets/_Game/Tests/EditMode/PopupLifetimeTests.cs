using Slime.Foundation;
using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Slime.App.Popups;
using Slime.UI;
using UnityEngine.TestTools;
using VContainer;

namespace Slime.Tests.EditMode
{
    public sealed class PopupLifetimeTests
    {
        private sealed class Popup : ISimplePopupController
        {
            public int Shown, Disposed;
            public readonly UniTaskCompletionSource Closed = new UniTaskCompletionSource();
            public UniTask ShowAsync(CancellationToken token = default) { Shown++; return Closed.Task.AttachExternalCancellation(token); }
            public void Dispose() => Disposed++;
        }

        [UnityTest]
        public IEnumerator Cancelling_queued_popup_does_not_resolve_or_dispose_active_popup() => UniTask.ToCoroutine(async () =>
        {
            var popup = new Popup();
            var builder = new ContainerBuilder();
            builder.RegisterInstance(popup);
            using var container = builder.Build();
            using var service = new PopupService(container);
            using var cancellation = new CancellationTokenSource();
            var active = service.ShowAsync<Popup>();
            var queued = service.ShowAsync<Popup>(cancellation.Token);
            cancellation.Cancel();
            try { await queued; Assert.Fail("Expected cancellation."); } catch (OperationCanceledException) { }
            Assert.AreEqual(1, popup.Shown);
            Assert.AreEqual(0, popup.Disposed);
            popup.Closed.TrySetResult();
            await active;
            Assert.AreEqual(1, popup.Disposed);
        });

        [UnityTest]
        public IEnumerator Disposing_service_cancels_active_and_waiting_popups() => UniTask.ToCoroutine(async () =>
        {
            var popup = new Popup();
            var builder = new ContainerBuilder();
            builder.RegisterInstance(popup);
            using var container = builder.Build();
            var service = new PopupService(container);
            var active = service.ShowAsync<Popup>();
            var waiting = service.ShowAsync<Popup>();
            service.Dispose();
            try { await active; Assert.Fail("Expected cancellation."); } catch (OperationCanceledException) { }
            try { await waiting; Assert.Fail("Expected cancellation."); } catch (OperationCanceledException) { }
            Assert.AreEqual(1, popup.Disposed);
            Assert.AreEqual(1, popup.Shown);
        });
    }
}
