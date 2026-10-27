using System;
using System.Collections.Generic;
#if ALICIZAX_BUILD_LOCALE_FILE && !UNITY_EDITOR
using System.IO;
#if UNITY_ANDROID || UNITY_WEBGL
using UnityEngine.Networking;
#endif
#endif
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AlicizaX.Localization.Runtime
{
    public static class LocalizationKit
    {
        private const string LanguagePrefsKey = "AlicizaX.Localization.Language";
        private static LocalizationProvider _provider;
        private static bool _initializing;

        public static event Action<string> Changed;

        public static string CurrentLanguage => _provider?.Language ?? string.Empty;
        public static int Revision => _provider?.Revision ?? 0;
        public static IReadOnlyList<string> SupportedLanguages => _provider?.SupportedLanguages ?? Array.Empty<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _provider = null;
            _initializing = false;
            Changed = null;
        }

        public static async UniTask InitializeAsync(string optionsJson, ILocalizationCatalogSource firstSource)
        {
            if (_provider != null || _initializing) throw new InvalidOperationException("Localization is already initialized.");
            LocalizationSettings settings = LocalizationSettings.Parse(optionsJson);
            _initializing = true;
            try
            {
#if UNITY_EDITOR
                string locale = PlayerPrefs.GetString(LanguagePrefsKey, settings.DefaultLocale);
                locale = UnityEditor.EditorPrefs.GetString(LocalizationSettings.EditorLanguagePrefsKey, locale);
                if (!settings.Supports(locale)) locale = settings.DefaultLocale;
#else
                string savedLocale = PlayerPrefs.GetString(LanguagePrefsKey, string.Empty);
                string buildLocale = null;
#if ALICIZAX_BUILD_LOCALE_FILE
                if (!settings.Supports(savedLocale)) buildLocale = await ReadBuildLocaleAsync(settings);
#endif
                string locale = SelectPlayerLocale(settings, savedLocale, buildLocale);
#endif
                LocalizationProvider provider = await LocalizationProvider.CreateAsync(settings, firstSource, locale);
                _provider = provider;
                Publish(provider, false);
            }
            finally { _initializing = false; }
        }

        private static string SelectPlayerLocale(LocalizationSettings settings, string savedLocale, string buildLocale)
        {
            if (settings.Supports(savedLocale)) return savedLocale;
            return settings.Supports(buildLocale) ? buildLocale : settings.DefaultLocale;
        }

#if ALICIZAX_BUILD_LOCALE_FILE && !UNITY_EDITOR
        private static async UniTask<string> ReadBuildLocaleAsync(LocalizationSettings settings)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "AlicizaX/Localization/build-locale.txt");
            string contents;
            try
            {
#if UNITY_ANDROID || UNITY_WEBGL
                using (var request = UnityWebRequest.Get(path))
                {
                    await request.SendWebRequest();
                    contents = request.downloadHandler.text;
                }
#else
                contents = File.ReadAllText(path);
#endif
            }
            catch (Exception error)
            {
                Debug.LogError($"Failed to read build localization language at {path}: {error}");
                return null;
            }

            string locale = contents.Trim();
            if (settings.Supports(locale)) return locale;
            Debug.LogError($"Unsupported build localization language at {path}: '{locale}'");
            return null;
        }
#endif

        public static string Get(string key) => _provider?.GetString(key) ?? key;

        public static string Format(string key, params object[] arguments) =>
            _provider == null ? key : LocalizationFormatter.Format(Get(key), arguments);

        public static string Format<T0>(string key, T0 arg0) =>
            _provider == null ? key : ZString.Format(Get(key), arg0);

        public static string Format<T0, T1>(string key, T0 arg0, T1 arg1) =>
            _provider == null ? key : ZString.Format(Get(key), arg0, arg1);

        public static string Format<T0, T1, T2>(string key, T0 arg0, T1 arg1, T2 arg2) =>
            _provider == null ? key : ZString.Format(Get(key), arg0, arg1, arg2);

        public static async UniTask<bool> AddCatalogAsync(ILocalizationCatalogSource source)
        {
            LocalizationProvider provider = _provider;
            if (provider == null) return false;
            bool loaded = await provider.AddCatalogAsync(source);
            if (!loaded || !ReferenceEquals(provider, _provider)) return false;
            Publish(provider, false);
            return true;
        }

        public static async UniTask<bool> SetLanguageAsync(string locale)
        {
            LocalizationProvider provider = _provider;
            if (provider == null) return false;
            int revision = provider.Revision;
            bool changed = await provider.SetLanguageAsync(locale);
            if (!changed || !ReferenceEquals(provider, _provider)) return false;
            if (provider.Revision != revision) Publish(provider, true);
            return true;
        }

        public static IDisposable SubscribeChanged(Action<string> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            Changed += callback;
            return new Subscription(callback);
        }

        private static void Publish(LocalizationProvider provider, bool persistLanguage)
        {
            if (persistLanguage)
            {
                PlayerPrefs.SetString(LanguagePrefsKey, provider.Language);
                PlayerPrefs.Save();
#if UNITY_EDITOR
                UnityEditor.EditorPrefs.SetString(LocalizationSettings.EditorLanguagePrefsKey, provider.Language);
#endif
            }
            if (Changed == null) return;
            foreach (Action<string> callback in Changed.GetInvocationList())
            {
                try { callback(provider.Language); }
                catch (Exception error) { Debug.LogException(error); }
            }
        }

        private sealed class Subscription : IDisposable
        {
            private Action<string> _callback;
            internal Subscription(Action<string> callback) => _callback = callback;
            public void Dispose()
            {
                if (_callback == null) return;
                Changed -= _callback;
                _callback = null;
            }
        }
    }
}
