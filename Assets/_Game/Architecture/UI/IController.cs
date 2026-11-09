using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Slime.UI
{
    public interface IController : IDisposable
    {
        public UniTask InitializeAsync(CancellationToken cancellationToken = default);
    }
}
