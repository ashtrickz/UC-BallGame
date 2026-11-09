using System;
using Game.Localization;
using TMPro;
using UnityEngine;
using VContainer;

namespace Game.Integration.Localization
{
    [DisallowMultipleComponent, RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string key;
        [SerializeField] private string[] arguments = Array.Empty<string>();
        private object[] _runtimeArguments;
        private TMP_Text _text;
        private ILocalizationService _localization;
        public string Key => key;

        [Inject]
        public void Construct(ILocalizationService localization)
        {
            Unsubscribe();
            _localization = localization;
            _text = GetComponent<TMP_Text>();
            if (isActiveAndEnabled)
                Subscribe();
        }

        public void SetKey(string value, params object[] values)
        {
            key = value;
            _runtimeArguments = values;
            Refresh();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (_localization == null)
                return;

            _localization.Changed -= Refresh;
            _localization.Changed += Refresh;
            Refresh();
        }

        private void Unsubscribe()
        {
            if (_localization != null)
                _localization.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (_localization != null && _text != null && !string.IsNullOrEmpty(key))
                _text.text = (_runtimeArguments?.Length ?? 0) > 0
                    ? _localization.Format(key, _runtimeArguments)
                    : arguments.Length > 0 ? _localization.Format(key, arguments) : _localization.Get(key);
        }
    }
}
