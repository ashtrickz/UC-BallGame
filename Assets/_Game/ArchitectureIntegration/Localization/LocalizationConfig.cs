using System;
using Slime.Localization;
using Slime.Settings;
using UnityEngine;

namespace Slime.Infrastructure.Localization
{
    [CreateAssetMenu(menuName = "Ball Game/Localization Config")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Slime.Integration.Localization", sourceAssembly: "Game.Services", sourceClassName: "LocalizationConfig")]
    public sealed class LocalizationConfig : ScriptableObject
    {
        public const string ResourcePath = "Localization/LocalizationConfig";

        [SerializeField, Tooltip("Public Google Sheets CSV URL. Leave empty to use bundled translations only.")]
        private string googleSheetsCsvUrl;
        [SerializeField] private TextAsset fallbackJson;
        [SerializeField] private string defaultLanguage = "en";
        [SerializeField, Min(1)] private int timeoutSeconds = 5;

        public LocalizationService CreateService(ISettingsService settings)
        {
            if (fallbackJson == null)
                throw new InvalidOperationException("LocalizationConfig needs a fallback JSON asset.");

            return new LocalizationService(settings, LocalizationTable.FromJson(fallbackJson.text),
                new GoogleSheetsLocalizationSource(googleSheetsCsvUrl, timeoutSeconds), defaultLanguage);
        }
    }
}
