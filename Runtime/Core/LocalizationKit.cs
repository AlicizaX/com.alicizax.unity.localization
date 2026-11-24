using System;
using System.Collections.Generic;
using System.Globalization;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AlicizaX.Localization.Runtime
{
    public static class LocalizationKit
    {
        private const string LanguagePrefsKey = "AlicizaX.Localization.Language";
        private static LocalizationProvider _provider;

        public static event Action<string> Changed;

        public static string CurrentLanguage => CheckInitialized() ? _provider.Language : string.Empty;
        public static int Revision => CheckInitialized() ? _provider.Revision : 0;
        public static IReadOnlyList<string> SupportedLanguages => CheckInitialized() ? _provider.SupportedLanguages : Array.Empty<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _provider?.Dispose();
            _provider = null;
            Changed = null;
            Settings = null;
        }

        public static IReadOnlyList<LocalizationLanguageTable> LoadedTables => CheckInitialized() ? _provider.LoadedTables : Array.Empty<LocalizationLanguageTable>();
        public static LocalizationSettings Settings { get; private set; }

        public static void Initialize() => Initialize(LocalizationSettings.Current);

        public static void Initialize(LocalizationSettings settings, string initialLanguage = null)
        {
            if (settings == null)
            {
                Debug.LogError("Localization settings are missing.");
                return;
            }
            settings.Validate();
            string saved = PlayerPrefs.GetString(LanguagePrefsKey, string.Empty);
            string locale = settings.SelectLanguage(initialLanguage, saved, CultureInfo.CurrentUICulture.Name);
#if UNITY_EDITOR
            if (initialLanguage == null)
            {
                string preview = UnityEditor.EditorPrefs.GetString(LocalizationSettings.EditorLanguagePrefsKey, locale);
                if (settings.Supports(preview)) locale = preview;
            }
#endif
            _provider = new LocalizationProvider(settings, locale);
            Settings = settings;
        }

        public static IDisposable RegisterLanguageLoader(Func<string, UniTask> loader) =>
            CheckInitialized() ? _provider.RegisterLanguageLoader(loader) : null;

        public static string Get(string key) => CheckInitialized() ? _provider.GetString(key) : key;

        public static string Format(string key, params object[] arguments) =>
            LocalizationFormatter.Format(Get(key), arguments);

        public static string Format<T0>(string key, T0 arg0) =>
            ZString.Format(Get(key), arg0);

        public static string Format<T0, T1>(string key, T0 arg0, T1 arg1) =>
            ZString.Format(Get(key), arg0, arg1);

        public static string Format<T0, T1, T2>(string key, T0 arg0, T1 arg1, T2 arg2) =>
            ZString.Format(Get(key), arg0, arg1, arg2);

        public static async UniTask AddTableAsync(string tableId, ILocalizationTableSource source)
        {
            if (!CheckInitialized()) return;
            int revision = _provider.Revision;
            await _provider.AddTableAsync(tableId, source);
            if (_provider.Revision != revision) Publish(false);
        }

        public static async UniTask ReloadTableAsync(string tableId, ILocalizationTableSource replacementSource = null)
        {
            if (!CheckInitialized()) return;
            int revision = _provider.Revision;
            await _provider.ReloadTableAsync(tableId, replacementSource);
            if (_provider.Revision != revision) Publish(false);
        }

        public static async UniTask SetLanguageAsync(string locale, bool persistLanguage = true)
        {
            if (!CheckInitialized()) return;
            int revision = _provider.Revision;
            await _provider.SetLanguageAsync(locale);
            if (_provider.Revision != revision) Publish(persistLanguage);
        }

        public static IDisposable SubscribeChanged(Action<string> callback)
        {
            Changed += callback;
            return new Subscription(callback);
        }

        private static bool CheckInitialized()
        {
            if (_provider != null) return true;
            Debug.LogError("Localization is not initialized.");
            return false;
        }

        private static void Publish(bool persistLanguage)
        {
            if (persistLanguage)
            {
                PlayerPrefs.SetString(LanguagePrefsKey, _provider.Language);
                PlayerPrefs.Save();
#if UNITY_EDITOR
                UnityEditor.EditorPrefs.SetString(LocalizationSettings.EditorLanguagePrefsKey, _provider.Language);
#endif
            }
            Changed?.Invoke(_provider.Language);
        }

        private sealed class Subscription : IDisposable
        {
            private Action<string> _callback;
            internal Subscription(Action<string> callback) => _callback = callback;
            public void Dispose()
            {
                Changed -= _callback;
                _callback = null;
            }
        }
    }
}
