using System.Globalization;

namespace Game.Localization
{
    public static class LocalizationFormatting
    {
        public static string Format(this ILocalizationService localization, string key, params object[] arguments) =>
            string.Format(CultureInfo.InvariantCulture, localization.Get(key), arguments);
    }
}
