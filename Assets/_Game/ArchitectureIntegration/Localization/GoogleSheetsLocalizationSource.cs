using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Localization;
using UnityEngine;
using UnityEngine.Networking;

namespace Game.Integration.Localization
{
    /// <summary>Reads a public Google Sheets CSV URL without embedding account credentials in the game.</summary>
    public sealed class GoogleSheetsLocalizationSource : ILocalizationSource
    {
        private const int DefaultTimeoutSeconds = 5;

        private readonly string _csvUrl;
        private readonly int _timeoutSeconds;

        public GoogleSheetsLocalizationSource(string csvUrl, int timeoutSeconds = DefaultTimeoutSeconds)
        {
            _csvUrl = csvUrl?.Trim();
            _timeoutSeconds = Math.Max(1, timeoutSeconds);
        }

        public async UniTask<LocalizationTable> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(_csvUrl))
                return null;

            if (!Uri.TryCreate(_csvUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw new FormatException("The localization CSV URL must use HTTPS.");

            using var request = UnityWebRequest.Get(_csvUrl);
            request.timeout = _timeoutSeconds;
            await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);
            var table = LocalizationTable.FromCsv(request.downloadHandler.text);
            var fonts = Resources.Load<LocalizationFonts>(LocalizationFonts.ResourcePath);
            if (fonts == null)
                throw new InvalidOperationException("The bundled localization fonts are missing.");

            fonts.ValidateChineseCoverage(table);
            return table;
        }
    }
}
