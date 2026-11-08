using Game.UI;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.App.Loading
{
    public interface ILoadingScreenView : IView
    {
        event Action RetryRequested;
        void SetContent(float initPercentage, string message, bool isError, bool canRetry);
    }

    public interface ILoadingScreen
    {
        event Action RetryRequested;
        void ShowLoading(float initPercentage, string message);
        void HideLoading();
        UniTask ShowLoadingAsync(float initPercentage, string message, CancellationToken cancellationToken = default);
        UniTask HideLoadingAsync(CancellationToken cancellationToken = default);
        void ShowError(string message, bool canRetry);
    }

    public interface IInitializationModel
    {
        event Action Changed;
        float InitPercentage { get; }
        string Message { get; }
        bool HasError { get; }
        bool CanRetry { get; }
        bool IsComplete { get; }
        void SetStage(float percentage, string message);
        void Fail(string message, bool canRetry);
        void Complete();
    }
}
