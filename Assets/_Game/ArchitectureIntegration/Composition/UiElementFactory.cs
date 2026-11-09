using System;
using Game.Integration.Audio;
using Game.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Integration.Composition
{
    /// <summary>Instantiates UI elements through the scene scope: injects their components and binds button feedback.</summary>
    public sealed class UiElementFactory : IUiElementFactory
    {
        private readonly IObjectResolver _resolver;
        private readonly UiFeedbackBinder _feedback;

        public UiElementFactory(IObjectResolver resolver, UiFeedbackBinder feedback)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _feedback = feedback ?? throw new ArgumentNullException(nameof(feedback));
        }

        public T Clone<T>(T template, Transform parent) where T : Component
        {
            var clone = UnityEngine.Object.Instantiate(template, parent);
            Prepare(clone.gameObject);

            return clone;
        }

        public GameObject Create(string name, Transform parent, params Type[] components)
        {
            var element = new GameObject(name, components);
            element.transform.SetParent(parent, false);
            Prepare(element);

            return element;
        }

        private void Prepare(GameObject element)
        {
            _resolver.InjectGameObject(element);
            _feedback.Bind(element);
        }
    }
}
