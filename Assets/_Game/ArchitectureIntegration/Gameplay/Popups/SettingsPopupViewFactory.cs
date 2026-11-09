using System;
using Slime.Infrastructure.Audio;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Slime.Infrastructure.Settings
{
    /// <summary>Instantiates popup views and binds their feedback at the integration boundary.</summary>
    public sealed class SettingsPopupViewFactory
    {
        private const string MissingPrefabMessage = "Assign the settings popup prefab in ProjectScope.";
        private readonly SettingsPopupView _prefab;
        private readonly Transform _parent;
        private readonly IObjectResolver _resolver;
        private readonly UiFeedbackBinder _feedback;

        public SettingsPopupViewFactory(SettingsPopupView prefab, Transform parent,
            IObjectResolver resolver, UiFeedbackBinder feedback)
        {
            _prefab = prefab;
            _parent = parent;
            _resolver = resolver;
            _feedback = feedback;
        }

        public ISettingsPopupView Create()
        {
            if (_prefab == null)
                throw new InvalidOperationException(MissingPrefabMessage);

            var view = _resolver.Instantiate(_prefab, _parent);
            _feedback.Bind(view.gameObject);
            return view;
        }
    }
}
