using System;
using System.Collections.Generic;
using System.IO;
using AlicizaX.Localization.Runtime;
using AlicizaX.Localization.UI;
using UnityEditor;
using UnityEngine;

namespace AlicizaX.Localization.Editor
{
    [InitializeOnLoad]
    internal static class LocalizationRefreshHelper
    {
        private static Dictionary<string, string> _entries = new(StringComparer.Ordinal);
        private static bool _dirty = true;
        internal static event Action Changed;

        static LocalizationRefreshHelper() => EditorApplication.projectChanged += InvalidateCache;

        internal static string Language
        {
            get
            {
                var settings = LocalizationSettings.Current;
                if (settings == null) return string.Empty;
                string language = EditorPrefs.GetString(LocalizationSettings.EditorLanguagePrefsKey, settings.DefaultLocale);
                return settings.Supports(language) ? language : settings.DefaultLocale;
            }
        }

        internal static IReadOnlyDictionary<string, string> EntriesByKey
        {
            get { RefreshIfNeeded(); return _entries; }
        }

        internal static void InvalidateCache()
        {
            _dirty = true;
            LocalizationSettings.Reload();
            Changed?.Invoke();
        }

        internal static void SetLanguage(string locale)
        {
            if (Application.isPlaying) return;
            EditorPrefs.SetString(LocalizationSettings.EditorLanguagePrefsKey, locale);
            InvalidateCache();
            foreach (LocalizedText binding in Resources.FindObjectsOfTypeAll<LocalizedText>())
                if (!EditorUtility.IsPersistent(binding) && binding.gameObject.scene.IsValid()) LocalizedTextEditor.Preview(binding);
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        internal static string GetPreviewLabel(string key) => key != null && EntriesByKey.TryGetValue(key, out var value)
            ? value ?? key : key;

        private static void RefreshIfNeeded()
        {
            if (!_dirty) return;
            _dirty = false;
            _entries.Clear();
            var settings = LocalizationSettings.Current;
            if (settings == null) return;
            var builtin = Resources.Load<TextAsset>(settings.ResourcePrefix + Language);
            if (builtin != null)
            {
                _entries = LocalizationCatalog.Read(builtin.bytes);
                Resources.UnloadAsset(builtin);
            }
            if (string.IsNullOrEmpty(settings.PatchAddressPrefix)) return;
            string name = settings.PatchAddressPrefix + Language;
            foreach (string guid in AssetDatabase.FindAssets(name))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path) != name + ".bytes") continue;
                var patch = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (patch != null) LocalizationCatalog.ApplyPatch(_entries, LocalizationCatalog.Read(patch.bytes));
                break;
            }
        }
    }
}
