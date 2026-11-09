using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.UI
{
    /// <summary>
    /// The shared life of a popup controller: show the view, wait until the popup is closed, hide it. The popup service disposes the
    /// controller afterwards, and with it the view (the popup is a prefab made for this one showing).
    /// </summary>
    public abstract class PopupController<TView> : IPopupController where TView : class, IPopupView
    {
        private readonly UniTaskCompletionSource _closed = new UniTaskCompletionSource(); // created up front: a popup that closes itself while building must still end
        private bool _disposed;

        protected PopupController(TView view) => View = view ?? throw new ArgumentNullException(nameof(view));

        protected TView View { get; }

        protected async UniTask PresentAsync(CancellationToken cancellationToken)
        {
            View.CloseRequested += OnCloseRequested;
            try
            {
                await View.ShowAsync(cancellationToken);
                await _closed.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                View.CloseRequested -= OnCloseRequested;
                try { if (cancellationToken.IsCancellationRequested) View.HideInstant(); else await View.HideAsync(); } catch (OperationCanceledException) { /* the view is going away anyway */ }
            }
        }

        protected void Close() => _closed.TrySetResult();

        protected virtual void OnCloseRequested() => Close();

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            View.Dispose();
        }
    }
}
