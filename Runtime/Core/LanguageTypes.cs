using System;
using System.Collections.Generic;

namespace AlicizaX.Localization.Runtime
{
    public static class LanguageTypes
    {
        public static IReadOnlyList<string> Languages => LocalizationSettings.Current?.Locales ?? Array.Empty<string>();
        public static string IndexToString(int index) => index >= 0 && index < Languages.Count ? Languages[index] : string.Empty;
        public static int StringToIndex(string locale)
        {
            var languages = Languages;
            for (int i = 0; i < languages.Count; i++) if (languages[i] == locale) return i;
            return -1;
        }
    }
}
