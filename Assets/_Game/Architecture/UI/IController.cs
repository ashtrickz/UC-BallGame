using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.UI
{
    public interface IController : IDisposable
    {
        public UniTask InitializeAsync(CancellationToken cancellationToken = default);
    }
}