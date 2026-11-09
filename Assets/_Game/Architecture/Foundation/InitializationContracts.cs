using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Slime.Foundation
{
    public interface IAsyncInitializable
    {
        UniTask InitializeAsync(CancellationToken cancellationToken);
    }

    public interface IInitializationStep : IAsyncInitializable
    {
        string Name { get; }
        bool CanRetry { get; }
    }

    public interface ISceneService
    {
        UniTask LoadSceneAsync(string sceneName, CancellationToken cancellationToken);
    }

    public interface ISceneReadiness
    {
        UniTask Ready { get; }
    }
}
