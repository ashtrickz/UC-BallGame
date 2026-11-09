using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Slime.Localization;

namespace Slime.App.Loading
{
    public sealed class LoadingScreenController : ILoadingScreen, IDisposable
    {
        private const string CancelledKey = "loading.cancelled";
        private const string ErrorKey = "loading.error";

        private readonly ILoadingScreenView _view;
        private readonly IInitializationModel _model;
        private UniTaskCompletionSource _showTransition;
        private readonly ILocalizationService _localization;
        private float _percentage;
        private string _message;
        private bool _error, _canRetry;
        public event Action RetryRequested;

        public LoadingScreenController(ILoadingScreenView view, IInitializationModel model, ILocalizationService localization)
        {
            _view = view;
            _model = model;
            _localization = localization;
            view.RetryRequested += OnRetry;
            model.Changed += Refresh;
            _localization.Changed += RefreshContent;
            Refresh();
        }

        public void ShowLoading(float initPercentage, string message) =>
            ShowLoadingAsync(initPercentage, message).Forget();

        public UniTask ShowLoadingAsync(float initPercentage, string message,
            CancellationToken cancellationToken = default)
        {
            SetContent(initPercentage, message, false, false);
            return EnsureShown(cancellationToken);
        }

        public void HideLoading() => HideLoadingAsync().Forget();

        public UniTask HideLoadingAsync(CancellationToken cancellationToken = default) =>
            _view.HideAsync(cancellationToken);

        public void ShowError(string message, bool canRetry)
        {
            SetContent(0, message, true, canRetry);
            EnsureShown().Forget();
        }

        private UniTask EnsureShown(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_showTransition == null && !_view.IsVisible)
            {
                var completion = new UniTaskCompletionSource();
                _showTransition = completion;
                CompleteShowAsync(completion, cancellationToken).Forget();
            }

            return (_showTransition?.Task ?? UniTask.CompletedTask).AttachExternalCancellation(cancellationToken);
        }

        private async UniTask CompleteShowAsync(UniTaskCompletionSource completion, CancellationToken cancellationToken)
        {
            try
            {
                await _view.ShowAsync(cancellationToken);
                completion.TrySetResult();
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
            finally
            {
                if (ReferenceEquals(_showTransition, completion))
                    _showTransition = null;
            }
        }

        private void Refresh()
        {
            if (_model.HasError)
                ShowError(_model.Message, _model.CanRetry);
            else if (!_model.IsComplete)
                ShowLoading(_model.InitPercentage, _model.Message);
        }

        private void OnRetry() => RetryRequested?.Invoke();

        private void SetContent(float percentage, string message, bool error, bool canRetry)
        {
            _percentage = percentage;
            _message = message;
            _error = error;
            _canRetry = canRetry;
            RefreshContent();
        }

        private void RefreshContent()
        {
            var key = _error && _message != CancelledKey ? ErrorKey : _message;
            _view.SetContent(_percentage, _localization.Get(key), _error, _canRetry);
        }

        public void Dispose()
        {
            _view.RetryRequested -= OnRetry;
            _model.Changed -= Refresh;
            _localization.Changed -= RefreshContent;
        }
    }
}
