using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace AlicizaX.Localization.Runtime
{
    public sealed class LocalizationProvider
    {
        private readonly Dictionary<string, Dictionary<string, string>> _cache = new(StringComparer.Ordinal);
        private readonly LocalizationSettings _settings;
        private readonly ILocalizationCatalogSource _baseSource;
        private ILocalizationCatalogSource _patchSource;
        private Dictionary<string, string> _strings;
        private bool _loading;

        public string Language { get; private set; }
        public int Revision { get; private set; } = 1;
        public IReadOnlyList<string> SupportedLanguages => _settings.Locales;
        public bool PatchEnabled => _patchSource != null;

        public LocalizationProvider(LocalizationSettings settings, ILocalizationCatalogSource source,
            string locale, byte[] initialBytes)
        {
            _settings = settings;
            _baseSource = source;
            _strings = LocalizationCatalog.Read(initialBytes);
            _cache.Add(locale, _strings);
            Language = locale;
        }

        public string GetString(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;
            return _strings.TryGetValue(key, out string value) && value != null ? value : key;
        }

        public async UniTask<bool> SetLanguageAsync(string locale)
        {
            if (!_settings.Supports(locale)) throw new ArgumentException($"Unsupported locale: {locale}", nameof(locale));
            if (_loading) return false;
            if (locale == Language) return true;
            _loading = true;
            try
            {
                if (!_cache.TryGetValue(locale, out var strings))
                    strings = await LoadAsync(locale, _patchSource);
                _cache[locale] = strings;
                Publish(locale, strings);
                return true;
            }
            finally { _loading = false; }
        }

        public async UniTask<bool> MountPatchAsync(ILocalizationCatalogSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (_loading) return false;
            if (PatchEnabled) return true;
            _loading = true;
            try
            {
                var strings = await LoadAsync(Language, source);
                _patchSource = source;
                _cache.Clear();
                _cache.Add(Language, strings);
                Publish(Language, strings);
                return true;
            }
            finally { _loading = false; }
        }

        private async UniTask<Dictionary<string, string>> LoadAsync(string locale, ILocalizationCatalogSource patch)
        {
            var strings = LocalizationCatalog.Read(await _baseSource.LoadAsync(locale));
            if (patch != null)
                LocalizationCatalog.ApplyPatch(strings, LocalizationCatalog.Read(await patch.LoadAsync(locale)));
            return strings;
        }

        private void Publish(string locale, Dictionary<string, string> strings)
        {
            _strings = strings;
            Language = locale;
            Revision++;
        }
    }
}
