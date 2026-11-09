using System;
using UnityEngine;

namespace Game.Integration.Localization
{
    /// <summary>Loads and configures fallback fonts once before localization starts.</summary>
    public sealed class LocalizationFontsProvider
    {
        private const string MissingFontsMessage = "Install the localization font assets.";
        private bool _configured;

        public void Configure()
        {
            if (_configured)
                return;

            var fonts = Resources.Load<LocalizationFonts>(LocalizationFonts.ResourcePath);
            if (fonts == null)
                throw new InvalidOperationException(MissingFontsMessage);

            fonts.ConfigureFallbacks();
            _configured = true;
        }
    }
}
