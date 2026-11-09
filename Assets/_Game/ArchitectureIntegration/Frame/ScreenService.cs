using System;
using Slime.UI;
using UnityEngine;
using VContainer.Unity;

namespace Slime.Infrastructure.Frame
{
    /// <summary>Reads the native screen state independently of layout consumers.</summary>
    public interface IScreenMetrics
    {
        Rect SafeArea
        {
            get;
        }
        Vector2Int Size
        {
            get;
        }
    }

    /// <summary>Provides the Unity screen state at the project loop boundary.</summary>
    public sealed class UnityScreenMetrics : IScreenMetrics
    {
        public Rect SafeArea => Screen.safeArea;
        public Vector2Int Size => new Vector2Int(Screen.width, Screen.height);
    }

    /// <summary>Polls once per project frame and publishes only layout-relevant changes.</summary>
    public sealed class ScreenService : IScreenService, ITickable, IDisposable
    {
        private readonly IScreenMetrics _source;
        private bool _disposed;

        public Rect SafeArea
        {
            get;
            private set;
        }
        public Vector2Int Size
        {
            get;
            private set;
        }
        public event Action SafeAreaChanged;

        public ScreenService(IScreenMetrics source)
        {
            _source = source;
            SafeArea = source.SafeArea;
            Size = source.Size;
        }

        public void Tick()
        {
            if (_disposed)
                return;

            var safeArea = _source.SafeArea;
            var size = _source.Size;
            if (safeArea == SafeArea && size == Size)
                return;

            SafeArea = safeArea;
            Size = size;
            SafeAreaChanged?.Invoke();
        }

        public void Dispose()
        {
            _disposed = true;
            SafeAreaChanged = null;
        }
    }
}
