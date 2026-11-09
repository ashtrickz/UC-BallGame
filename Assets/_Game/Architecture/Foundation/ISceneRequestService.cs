using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Foundation
{
    /// <summary>Accepts loading presentation context while preserving the original scene-service contract.</summary>
    public interface ISceneRequestService : ISceneService
    {
        UniTask LoadSceneAsync(string sceneName, string loadingMessageKey, CancellationToken cancellationToken);
    }
}
