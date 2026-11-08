using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.UI
{
    public interface IPopupController : IDisposable
    {
        
    }

    public interface ISimplePopupController : IPopupController
    {
        UniTask ShowAsync(CancellationToken cancellationToken = default);
    }
    
    public interface IPopupController<in TModel> : IPopupController
    {
        UniTask ShowAsync(TModel model, CancellationToken cancellationToken = default);
    }

    public interface IPopupController<in TModel, TResult> : IPopupController
    {
        UniTask<TResult> ShowAsync(TModel model, CancellationToken cancellationToken = default);
    }
}