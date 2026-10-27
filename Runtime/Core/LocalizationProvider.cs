using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace AlicizaX.Localization.Runtime
{
    public sealed class LocalizationProvider
    {
        private readonly LocalizationSettings _settings;
        private readonly List<ILocalizationCatalogSource> _sources = new List<ILocalizationCatalogSource>();
        private Dictionary<string, string> _strings;
        private bool _loading;

        public string Language { get; private set; }
        public int Revision { get; private set; } = 1;
        public IReadOnlyList<string> SupportedLanguages => _settings.Locales;

        private LocalizationProvider(LocalizationSettings settings, ILocalizationCatalogSource source,
            string locale, Dictionary<string, string> strings)
        {
            _settings = settings;
            _sources.Add(source);
            _strings = strings;
            Language = locale;
        }

        public static async UniTask<LocalizationProvider> CreateAsync(LocalizationSettings settings,
            ILocalizationCatalogSource source, string locale)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!settings.Supports(locale)) throw new ArgumentException("Unsupported locale: " + locale, nameof(locale));
            var strings = LocalizationCatalog.Read(await source.LoadAsync(locale));
            return new LocalizationProvider(settings, source, locale, strings);
        }

        public string GetString(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;
            return _strings.TryGetValue(key, out string value) && value != null ? value : key;
        }

        public async UniTask<bool> AddCatalogAsync(ILocalizationCatalogSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (_loading) return false;
            _loading = true;
            try
            {
                var added = LocalizationCatalog.Read(await source.LoadAsync(Language));
                var merged = new Dictionary<string, string>(_strings, StringComparer.Ordinal);
                MergeUnique(merged, added);
                _sources.Add(source);
                _strings = merged;
                Revision++;
                return true;
            }
            finally { _loading = false; }
        }

        public async UniTask<bool> SetLanguageAsync(string locale)
        {
            if (!_settings.Supports(locale)) throw new ArgumentException("Unsupported locale: " + locale, nameof(locale));
            if (_loading) return false;
            if (locale == Language) return true;
            _loading = true;
            try
            {
                var merged = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (ILocalizationCatalogSource source in _sources)
                    MergeUnique(merged, LocalizationCatalog.Read(await source.LoadAsync(locale)));
                _strings = merged;
                Language = locale;
                Revision++;
                return true;
            }
            finally { _loading = false; }
        }

        private static void MergeUnique(Dictionary<string, string> target, Dictionary<string, string> added)
        {
            foreach (var pair in added)
                if (!target.TryAdd(pair.Key, pair.Value))
                    throw new InvalidOperationException("Duplicate localization key across catalogs: " + pair.Key);
        }
    }
}
