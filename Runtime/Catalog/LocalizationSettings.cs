using System;
using System.Globalization;
using UnityEngine;

namespace AlicizaX.Localization.Runtime
{
    public sealed class LocalizationSettings : ScriptableObject
    {
        public const string EditorLanguagePrefsKey = "AlicizaX.Localization.Editor.Language";

        public const string AssetPath = "Assets/Resources/Localization/Settings.asset";
        public LanguageDefinition[] Languages = Array.Empty<LanguageDefinition>();
        public string DefaultLocale;
        public bool UseSystemLanguage;

        public string[] Locales => Array.ConvertAll(Languages, language => language.Id);

        public void Validate()
        {
            if (Languages == null || Languages.Length == 0 || !Supports(DefaultLocale))
                throw new FormatException("Localization settings require languages and a valid default language.");
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            var codes = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var language in Languages)
            {
                if (language == null || string.IsNullOrWhiteSpace(language.Id) || !seen.Add(language.Id))
                    throw new FormatException("Localization languages must be nonempty and unique.");
                if (!string.IsNullOrEmpty(language.LocaleCode) && !codes.Add(language.LocaleCode))
                    throw new FormatException("Duplicate locale code: " + language.LocaleCode);
            }
        }

        public string SelectLanguage(string initialLanguage, string savedLanguage, string systemLocale)
        {
            if (Supports(initialLanguage)) return initialLanguage;
            if (Supports(savedLanguage)) return savedLanguage;
            if (UseSystemLanguage && !string.IsNullOrEmpty(systemLocale))
            {
                string code = systemLocale;
                while (!string.IsNullOrEmpty(code))
                {
                    var language = Array.Find(Languages, value => string.Equals(value.LocaleCode, code, StringComparison.OrdinalIgnoreCase));
                    if (language != null) return language.Id;
                    try { code = CultureInfo.GetCultureInfo(code).Parent.Name; }
                    catch (CultureNotFoundException) { break; }
                }
            }
            return DefaultLocale;
        }

        public bool Supports(string locale) => Languages != null && Array.Exists(Languages, language => language != null && language.Id == locale);
        public string DisplayName(string locale) => Array.Find(Languages, language => language.Id == locale)?.DisplayName ?? locale;
    }

    [Serializable]
    public sealed class LanguageDefinition
    {
        public string Id;
        public string LocaleCode;
        public string DisplayName;
    }
}
