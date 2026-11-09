using System;
using System.Collections.Generic;
using Slime.Settings;
using UnityEngine;

namespace Slime.Infrastructure.Settings
{
    /// <summary>Loads player-facing language names from bundled data instead of source literals.</summary>
    public sealed class LanguageOptionsProvider
    {
        public const string ResourcePath = "Localization/LanguageOptions";
        private const string MissingDataMessage = "Install the bundled language options.";
        private IReadOnlyList<LanguageOption> _value;

        /// <summary>A single bundled language option.</summary>
        [Serializable]
        private sealed class LanguageData
        {
            public string name = string.Empty;
            public string culture = string.Empty;
            public string displayName = string.Empty;
        }

        /// <summary>Serialization wrapper for bundled options.</summary>
        [Serializable]
        private sealed class LanguageCollection
        {
            public LanguageData[] languages = Array.Empty<LanguageData>();
        }

        public IReadOnlyList<LanguageOption> Value
        {
            get
            {
                if (_value == null)
                {
                    var asset = Resources.Load<TextAsset>(ResourcePath);
                    if (asset == null)
                        throw new InvalidOperationException(MissingDataMessage);

                    var data = JsonUtility.FromJson<LanguageCollection>(asset.text);
                    var options = new LanguageOption[data.languages.Length];
                    for (var i = 0; i < options.Length; i++)
                    {
                        var language = data.languages[i];
                        options[i] = new LanguageOption(language.name, language.culture, language.displayName);
                    }

                    _value = Array.AsReadOnly(options);
                }

                return _value;
            }
        }
    }
}
