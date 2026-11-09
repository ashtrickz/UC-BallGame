using System.Threading;
using Cysharp.Threading.Tasks;

namespace Slime.UI
{
    public interface IPopupService
    {
        UniTask ShowAsync<TPopup>(CancellationToken cancellationToken = default)
            where TPopup : ISimplePopupController;

        UniTask ShowAsync<TPopup, TModel>(TModel model, CancellationToken cancellationToken = default)
            where TPopup : IPopupController<TModel>;

        UniTask<TResult> ShowAsync<TPopup, TModel, TResult>(TModel model, CancellationToken cancellationToken = default)
            where TPopup : IPopupController<TModel, TResult>;
    }

}
