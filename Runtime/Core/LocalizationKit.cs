using System;
using System.Collections.Generic;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AlicizaX.Localization.Runtime
{
    public static class LocalizationKit
    {
        private const string LanguagePrefsKey = "AlicizaX.Localization.Language";
        private static LocalizationProvider _provider;

        public static event Action<string> LanguageChanged;

        public static string CurrentLanguage => _provider?.Language ?? string.Empty;
        public static int Revision => _provider?.Revision ?? 0;
        public static IReadOnlyList<string> SupportedLanguages => _provider?.SupportedLanguages ?? Array.Empty<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _provider = null;
            LanguageChanged = null;
            LocalizationSettings.Reload();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            LocalizationSettings settings = LocalizationSettings.Current;
            if (settings == null) return;
            string locale = PlayerPrefs.GetString(LanguagePrefsKey, settings.DefaultLocale);
#if UNITY_EDITOR
            locale = UnityEditor.EditorPrefs.GetString(LocalizationSettings.EditorLanguagePrefsKey, locale);
#endif
            if (!settings.Supports(locale)) locale = settings.DefaultLocale;
            var source = new ResourcesCatalogSource(settings.ResourcePrefix);
            _provider = new LocalizationProvider(settings, source, locale, source.Load(locale));
        }

        public static string Get(string key) => _provider?.GetString(key) ?? key;

        public static string Format(string key, params object[] arguments) =>
            _provider == null ? key : LocalizationFormatter.Format(Get(key), arguments);

        public static string Format<T0>(string key, T0 arg0) =>
            _provider == null ? key : ZString.Format(Get(key), arg0);

        public static string Format<T0, T1>(string key, T0 arg0, T1 arg1) =>
            _provider == null ? key : ZString.Format(Get(key), arg0, arg1);

        public static string Format<T0, T1, T2>(string key, T0 arg0, T1 arg1, T2 arg2) =>
            _provider == null ? key : ZString.Format(Get(key), arg0, arg1, arg2);

        public static async UniTask<bool> SetLanguageAsync(string locale)
        {
            LocalizationProvider provider = _provider;
            if (provider == null) return false;
            int revision = provider.Revision;
            bool changed = await provider.SetLanguageAsync(locale);
            if (!changed || !ReferenceEquals(provider, _provider)) return false;
            if (provider.Revision != revision) Publish(provider);
            return true;
        }

        public static async UniTask<bool> EnablePatchAsync()
        {
            LocalizationProvider provider = _provider;
            string prefix = LocalizationSettings.Current?.PatchAddressPrefix;
            if (provider == null || string.IsNullOrEmpty(prefix)) return false;
            int revision = provider.Revision;
            bool enabled = await provider.MountPatchAsync(new ResourceCatalogSource(prefix));
            if (!enabled || !ReferenceEquals(provider, _provider)) return false;
            if (provider.Revision != revision) Publish(provider);
            return true;
        }

        private static void Publish(LocalizationProvider provider)
        {
            PlayerPrefs.SetString(LanguagePrefsKey, provider.Language);
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorPrefs.SetString(LocalizationSettings.EditorLanguagePrefsKey, provider.Language);
#endif
            LanguageChanged?.Invoke(provider.Language);
        }
    }
}
