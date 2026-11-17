using System;
using Cysharp.Threading.Tasks;

namespace AlicizaX.Localization.Runtime
{
    public interface ILocalizationTableSource
    {
        UniTask<LoadedLocalizationTable> LoadAsync(string locale);
    }

    public sealed class LoadedLocalizationTable : IDisposable
    {
        private Action _release;
        public LocalizationLanguageTable Asset { get; }
        public LoadedLocalizationTable(LocalizationLanguageTable asset, Action release) { Asset = asset; _release = release; }
        public void Dispose()
        {
            var release = _release;
            _release = null;
            release?.Invoke();
        }
    }
}
