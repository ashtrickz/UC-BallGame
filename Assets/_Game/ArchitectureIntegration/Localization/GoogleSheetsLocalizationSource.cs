using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Localization;
using UnityEngine.Networking;

namespace Game.Integration.Localization
{
    /// <summary>Reads a public Google Sheets CSV URL without embedding account credentials in the game.</summary>
    public sealed class GoogleSheetsLocalizationSource : ILocalizationSource
    {
        private readonly string _csvUrl;
        private readonly int _timeoutSeconds;

        public GoogleSheetsLocalizationSource(string csvUrl, int timeoutSeconds = 5)
        {
            _csvUrl = csvUrl?.Trim();
            _timeoutSeconds = Math.Max(1, timeoutSeconds);
        }

        public async UniTask<LocalizationTable> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(_csvUrl)) return null;
            if (!Uri.TryCreate(_csvUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw new FormatException("The localization CSV URL must use HTTPS.");
            using var request = UnityWebRequest.Get(_csvUrl);
            request.timeout = _timeoutSeconds;
            await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);
            return LocalizationTable.FromCsv(request.downloadHandler.text);
        }
    }
}
