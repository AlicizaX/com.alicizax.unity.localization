using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AlicizaX.Localization.Runtime
{
    public sealed class LocalizationProvider : IDisposable
    {
        private sealed class Table
        {
            internal string Id;
            internal ILocalizationTableSource Source;
            internal LoadedLocalizationTable Loaded;
            internal Dictionary<string, string> Strings;
        }
        private readonly List<Table> _tables = new();
        private Dictionary<string, string> _strings = new(StringComparer.Ordinal);
        private readonly string[] _languages;
        private bool _loading;
        private LanguageBinding _binding;
        public string Language { get; private set; }
        public int Revision { get; private set; }
        public IReadOnlyList<string> SupportedLanguages => _languages;
        public IReadOnlyList<LocalizationLanguageTable> LoadedTables => _tables.Select(t => t.Loaded.Asset).ToArray();

        public LocalizationProvider(LocalizationSettings settings, string locale)
        {
            _languages = settings.Locales;
            Language = locale;
        }
        public string GetString(string key) => key != null && _strings.TryGetValue(key, out var value) ? value ?? key : key;
        public UniTask AddTableAsync(string tableId, ILocalizationTableSource source) => ChangeTableAsync(tableId, source, false);
        public UniTask ReloadTableAsync(string tableId, ILocalizationTableSource source = null) => ChangeTableAsync(tableId, source, true);

        private async UniTask ChangeTableAsync(string id, ILocalizationTableSource source, bool reload)
        {
            int index = _tables.FindIndex(t => t.Id == id);
            if (reload ? index < 0 : index >= 0)
            {
                Debug.LogError($"Table '{id}' is {(reload ? "not registered" : "already registered")}.");
                return;
            }
            if (_loading)
            {
                Debug.LogError("Localization is already loading. Await the current operation first.");
                return;
            }
            if (reload) source ??= _tables[index].Source;
            _loading = true;
            try
            {
                var added = await Load(id, source, Language);
                var old = reload ? _tables[index] : null;
                if (reload) _tables[index] = added; else _tables.Add(added);
                _strings = Merge(_tables);
                Revision++;
                old?.Loaded.Dispose();
            }
            finally { _loading = false; }
        }
        private static async UniTask<Table> Load(string id, ILocalizationTableSource source, string locale)
        {
            var loaded = await source.LoadAsync(locale);
            try
            {
                if (loaded?.Asset == null || loaded.Asset.TableId != id || loaded.Asset.Locale != locale)
                    throw new InvalidOperationException($"Localization source did not return {id}/{locale}.");
                return new Table { Id = id, Source = source, Loaded = loaded, Strings = loaded.Asset.Read() };
            }
            catch { loaded?.Dispose(); throw; }
        }
        private static Dictionary<string, string> Merge(IEnumerable<Table> tables)
        {
            var merged = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var table in tables)
                foreach (var pair in table.Strings)
                    if (pair.Value != null) merged[pair.Key] = pair.Value;
            return merged;
        }
        public async UniTask SetLanguageAsync(string locale)
        {
            if (Array.IndexOf(_languages, locale) < 0)
            {
                Debug.LogError("Unsupported locale: " + locale);
                return;
            }
            if (_loading)
            {
                Debug.LogError("Localization is already loading. Await the current operation first.");
                return;
            }
            if (locale == Language) return;
            _loading = true;
            var staged = new List<Table>();
            try
            {
                foreach (var table in _tables)
                    staged.Add(await Load(table.Id, table.Source, locale));
                var merged = Merge(staged);
                if (_binding != null) await _binding.Loader(locale);
                var old = _tables.ToArray();
                _tables.Clear();
                _tables.AddRange(staged);
                staged.Clear();
                _strings = merged;
                Language = locale;
                Revision++;
                foreach (var table in old) table.Loaded.Dispose();
            }
            finally
            {
                foreach (var table in staged) table.Loaded.Dispose();
                _loading = false;
            }
        }
        public IDisposable RegisterLanguageLoader(Func<string, UniTask> loader)
        {
            return _binding = new LanguageBinding(this, loader);
        }
        private sealed class LanguageBinding : IDisposable
        {
            private readonly LocalizationProvider _owner;
            internal readonly Func<string, UniTask> Loader;
            internal LanguageBinding(LocalizationProvider owner, Func<string, UniTask> loader) { _owner = owner; Loader = loader; }
            public void Dispose() { if (_owner._binding == this) _owner._binding = null; }
        }
        public void Dispose()
        {
            _binding = null;
            foreach (var table in _tables) table.Loaded.Dispose();
            _tables.Clear();
            _strings.Clear();
        }
    }
}
