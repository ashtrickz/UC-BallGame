using System;
using UnityEngine;

namespace Slime.UI
{
    /// <summary>Publishes a coherent safe-area and screen-size snapshot for view layout.</summary>
    public interface IScreenService
    {
        Rect SafeArea
        {
            get;
        }
        Vector2Int Size
        {
            get;
        }
        event Action SafeAreaChanged;
    }
}
