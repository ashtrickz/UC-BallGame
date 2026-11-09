using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;

namespace Game.Assets
{
    public static class AssetReferenceExtensions
    {
        public static UniTask<AssetLease<T>> LoadAsync<T>(this IAssetService service,
            AssetReference reference, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            if (reference == null)
                throw new ArgumentNullException(nameof(reference));

            if (string.IsNullOrEmpty(reference.AssetGUID))
                throw new ArgumentException("Asset reference has no assigned asset.", nameof(reference));

            return service.LoadAsync<T>(reference.AssetGUID, cancellationToken);
        }
    }
}
