using System;
using UnityEngine;

namespace Game.Integration.Localization
{
    /// <summary>Validates and caches the bundled localization configuration.</summary>
    public sealed class LocalizationConfigProvider
    {
        private const string MissingConfigMessage = "Add LocalizationConfig to Resources/Localization.";
        private LocalizationConfig _value;

        public LocalizationConfig Value
        {
            get
            {
                if (_value == null)
                {
                    _value = Resources.Load<LocalizationConfig>(LocalizationConfig.ResourcePath);
                    if (_value == null)
                        throw new InvalidOperationException(MissingConfigMessage);
                }

                return _value;
            }
        }
    }
}
