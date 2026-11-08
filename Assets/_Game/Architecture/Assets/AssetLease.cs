using System;

namespace Game.Assets
{
    public sealed class AssetLease<T> : IDisposable where T : UnityEngine.Object
    {
        private readonly T _asset;
        private readonly Action _release;
        private readonly Action _assertMainThread;
        private bool _disposed;

        internal AssetLease(T asset, Action release, Action assertMainThread)
        {
            _asset = asset;
            _release = release;
            _assertMainThread = assertMainThread;
        }

        public T Asset
        {
            get
            {
                _assertMainThread();
                if (_disposed) throw new ObjectDisposedException(nameof(AssetLease<T>));
                return _asset;
            }
        }

        public void Dispose()
        {
            _assertMainThread();
            if (_disposed) 
                return;
            _disposed = true;
            _release();
        }
    }
}
