using System.Threading;
using Cysharp.Threading.Tasks;

namespace Slime.UI
{
    public interface IView
    {
        public bool IsVisible { get; }

        public UniTask ShowAsync(CancellationToken token =  default);
        public UniTask HideAsync(CancellationToken token =  default);
        public void ShowInstant();
        public void HideInstant();
    }
}
