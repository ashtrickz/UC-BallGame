using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI;
using VContainer;

namespace Game.App.Popups
{
    public sealed class PopupService : IPopupService, IDisposable
    {
        private readonly IObjectResolver _resolver;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private bool _disposed;
        public PopupService(IObjectResolver resolver) => _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

        public async UniTask ShowAsync<TPopup>(CancellationToken cancellationToken = default)
            where TPopup : ISimplePopupController
        {
            await RunAsync<TPopup, bool>(async (popup, token) => { await popup.ShowAsync(token); return true; }, cancellationToken);
        }

        public async UniTask ShowAsync<TPopup, TModel>(TModel model, CancellationToken cancellationToken = default)
            where TPopup : IPopupController<TModel>
        {
            await RunAsync<TPopup, bool>(async (popup, token) => { await popup.ShowAsync(model, token); return true; }, cancellationToken);
        }

        public UniTask<TResult> ShowAsync<TPopup, TModel, TResult>(TModel model, CancellationToken cancellationToken = default)
            where TPopup : IPopupController<TModel, TResult> =>
            RunAsync<TPopup, TResult>((popup, token) => popup.ShowAsync(model, token), cancellationToken);

        private async UniTask<TResult> RunAsync<TPopup, TResult>(Func<TPopup, CancellationToken, UniTask<TResult>> show, CancellationToken token)
            where TPopup : IPopupController
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PopupService));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token);
            await _semaphore.WaitAsync(linked.Token);
            try
            {
                linked.Token.ThrowIfCancellationRequested();
                using var popup = _resolver.Resolve<TPopup>();
                return await show(popup, linked.Token);
            }
            finally { _semaphore.Release(); }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _lifetime.Cancel();
            _lifetime.Dispose();
            // Pending calls still release the semaphore while unwinding. No WaitHandle is allocated.
        }
    }
}
