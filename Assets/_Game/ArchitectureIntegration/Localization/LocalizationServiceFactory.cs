using Slime.Localization;
using Slime.Settings;

namespace Slime.Infrastructure.Localization
{
    /// <summary>Creates localization after its bundled configuration and fallback fonts are ready.</summary>
    public sealed class LocalizationServiceFactory
    {
        private readonly LocalizationConfigProvider _config;
        private readonly LocalizationFontsProvider _fonts;
        private readonly ISettingsService _settings;

        public LocalizationServiceFactory(LocalizationConfigProvider config,
            LocalizationFontsProvider fonts, ISettingsService settings)
        {
            _config = config;
            _fonts = fonts;
            _settings = settings;
        }

        public LocalizationService Create()
        {
            _fonts.Configure();
            return _config.Value.CreateService(_settings);
        }
    }
}
