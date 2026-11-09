using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Foundation;
using Game.Settings;
using UnityEngine;

namespace Game.Localization
{
    /// <summary>Main-thread singleton. Bundled translations are available before remote initialization.</summary>
    public sealed class LocalizationService : ILocalizationService, IInitializationStep, IDisposable
    {
        public event Action Changed;
        public string CurrentLanguage => _settings.CurrentLanguage;
        public string Name => "loading.translations";
        public bool CanRetry => false;
        public Exception LastLoadError { get; private set; }

        private readonly ISettingsService _settings;
        private readonly LocalizationTable _fallback;
        private readonly ILocalizationSource _primary;
        private readonly string _defaultLanguage;
        private LocalizationTable _remote;
        private string _previousLanguage;
        private bool _disposed;

        public LocalizationService(ISettingsService settings, LocalizationTable fallback,
            ILocalizationSource primary = null, string defaultLanguage = "en")
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
            _primary = primary;
            _defaultLanguage = string.IsNullOrWhiteSpace(defaultLanguage)
                ? throw new ArgumentException("A default language is required.", nameof(defaultLanguage))
                : defaultLanguage.Trim();
            _previousLanguage = CurrentLanguage;
            _settings.Changed += OnSettingsChanged;
        }

        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (_remote != null && _remote.TryGet(key, CurrentLanguage, out var remote)) return remote;
            if (_fallback.TryGet(key, CurrentLanguage, out var local)) return local;
            if (_remote != null && _remote.TryGet(key, _defaultLanguage, out remote)) return remote;
            return _fallback.TryGet(key, _defaultLanguage, out local) ? local : key;
        }

        public async UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LocalizationService));
            cancellationToken.ThrowIfCancellationRequested();
            if (_primary == null) return;
            LocalizationTable next;
            try
            {
                next = await _primary.LoadAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                LastLoadError = exception;
                Debug.LogWarning($"Localization: remote translations unavailable; using bundled JSON. {exception.Message}");
                return;
            }
            if (_disposed) return;
            LastLoadError = null;
            if (next == null) return; // Remote source is not configured.
            _remote = next;
            Changed?.Invoke();
        }

        private void OnSettingsChanged()
        {
            if (string.Equals(_previousLanguage, CurrentLanguage, StringComparison.OrdinalIgnoreCase)) return;
            _previousLanguage = CurrentLanguage;
            Changed?.Invoke();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _settings.Changed -= OnSettingsChanged;
        }
    }
}
