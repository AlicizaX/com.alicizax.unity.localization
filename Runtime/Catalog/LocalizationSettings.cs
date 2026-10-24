using System;
using UnityEngine;

namespace AlicizaX.Localization.Runtime
{
    [Serializable]
    public sealed class LocalizationSettings
    {
        public const string ResourcePath = "Localization/Settings";
        public const string EditorLanguagePrefsKey = "AlicizaX.Localization.Editor.Language";

        public string[] Locales;
        public string DefaultLocale;
        public string ResourcePrefix;
        public string PatchAddressPrefix;

        private static LocalizationSettings _current;
        private static bool _loaded;

        public static LocalizationSettings Current
        {
            get
            {
                if (_loaded) return _current;
                _loaded = true;
                TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
                if (asset == null) return null;
                _current = JsonUtility.FromJson<LocalizationSettings>(asset.text);
                Resources.UnloadAsset(asset);
                if (_current != null && _current.Locales == null) _current.Locales = Array.Empty<string>();
                return _current;
            }
        }

        public static void Reload()
        {
            _current = null;
            _loaded = false;
        }

        public bool Supports(string locale) => Array.IndexOf(Locales, locale) >= 0;
    }
}
