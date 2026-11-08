using System;
using Cysharp.Threading.Tasks;

namespace Game.Assets
{
    // Each returned operation represents one native acquisition. Completion is consumed once,
    // and Dispose releases that acquisition after completion, including failed operations.
    // Creation, completion continuations and disposal must all run on the Unity main thread.
    public interface IAssetBackend
    {
        IAssetInitializationOperation InitializeAsync();
        IAssetOperation<T> LoadAsync<T>(string key) where T : UnityEngine.Object;
    }

    public interface IAssetInitializationOperation : IDisposable
    {
        UniTask Completion { get; }
    }

    public interface IAssetOperation<T> : IDisposable where T : UnityEngine.Object
    {
        UniTask<T> Completion { get; }
    }
}
