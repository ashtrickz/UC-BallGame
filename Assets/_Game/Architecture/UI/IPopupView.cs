using System;

namespace Slime.UI
{
    public interface IPopupView : IView, IDisposable
    {
        public event Action CloseRequested;
    }
}
