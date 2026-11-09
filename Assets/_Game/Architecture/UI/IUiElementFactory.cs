using System;
using UnityEngine;

namespace Game.UI
{
    /// <summary>Creates UI elements at runtime with their dependencies and feedback bound, so views never see a container.</summary>
    public interface IUiElementFactory
    {
        T Clone<T>(T template, Transform parent) where T : Component;

        GameObject Create(string name, Transform parent, params Type[] components);
    }
}
