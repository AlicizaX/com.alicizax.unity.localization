using System;
using UnityEngine;

namespace AlicizaX.Localization.Runtime
{
    [Serializable]
    public sealed class LocalizationSettings
    {
        public const string EditorLanguagePrefsKey = "AlicizaX.Localization.Editor.Language";

        public string[] Locales;
        public string DefaultLocale;

        public static LocalizationSettings Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Localization options are empty.", nameof(json));
            var settings = JsonUtility.FromJson<LocalizationSettings>(json);
            if (settings?.Locales == null || settings.Locales.Length == 0 ||
                string.IsNullOrEmpty(settings.DefaultLocale) || !settings.Supports(settings.DefaultLocale))
                throw new FormatException("Localization options require languages and a valid default language.");
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (string locale in settings.Locales)
                if (string.IsNullOrWhiteSpace(locale) || !seen.Add(locale))
                    throw new FormatException("Localization languages must be nonempty and unique.");
            return settings;
        }

        public bool Supports(string locale) => Array.IndexOf(Locales, locale) >= 0;
    }
}
