using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Slime.Localization
{
    public interface ILocalizationService
    {
        event Action Changed;
        string CurrentLanguage { get; }
        string Get(string key);
    }

    public interface ILocalizationSource
    {
        UniTask<LocalizationTable> LoadAsync(CancellationToken cancellationToken);
    }
}
