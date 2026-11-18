using System;
using System.Globalization;
using UnityEngine;

namespace AlicizaX.Localization.Runtime
{
    public sealed class LocalizationSettings : ScriptableObject
    {
        public const string EditorLanguagePrefsKey = "AlicizaX.Localization.Editor.Language";

        private static LocalizationSettings _current;

#if UNITY_EDITOR
        public const string ProjectPath = "ProjectSettings/LocalizationSettings.asset";
        public static LocalizationSettings Current
        {
            get
            {
                if (_current == null && System.IO.File.Exists(ProjectPath))
                    _current = Array.Find(UnityEditorInternal.InternalEditorUtility.LoadSerializedFileAndForget(ProjectPath),
                        obj => obj is LocalizationSettings) as LocalizationSettings;
                return _current;
            }
        }

        public static void CreateProjectSettings()
        {
            _current = CreateInstance<LocalizationSettings>();
            _current.Languages = new[] { new LanguageDefinition { Id = "zh-Hans", LocaleCode = "zh-Hans", DisplayName = "简体中文" } };
            _current.DefaultLocale = "zh-Hans";
            _current.SaveProjectSettings();
        }

        public void SaveProjectSettings()
        {
            UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(new UnityEngine.Object[] { this }, ProjectPath, true);
            UnityEditor.EditorUtility.ClearDirty(this);
        }
#else
        internal static LocalizationSettings Current => _current;
        private void OnEnable() => _current = this;
#endif

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
