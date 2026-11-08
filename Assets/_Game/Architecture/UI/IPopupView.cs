using System;

namespace Game.UI
{
    public interface IPopupView : IView, IDisposable
    {
        public event Action CloseRequested;
    }
}
