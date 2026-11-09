using System.Threading;
using Cysharp.Threading.Tasks;

namespace Slime.Assets
{
    public interface IAssetService
    {
        UniTask<AssetLease<T>> LoadAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object;
    }
}
