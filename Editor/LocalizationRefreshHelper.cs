using System;
using System.Collections.Generic;
using AlicizaX.Localization.Runtime;
#if TEXTMESHPRO_SUPPORT
using AlicizaX.Localization.UI;
#endif
using UnityEditor;
using UnityEngine;

namespace AlicizaX.Localization.Editor
{
    [InitializeOnLoad]
    internal static class LocalizationRefreshHelper
    {
        private static Dictionary<string, string> _entries = new Dictionary<string, string>(StringComparer.Ordinal);
        private static LocalizationSettings _settings;
        private static bool _dirty = true;
        private static bool _refreshScheduled;
        internal static event Action Changed;

        static LocalizationRefreshHelper()
        {
            EditorApplication.projectChanged += InvalidateCache;
            Undo.undoRedoPerformed += InvalidateCache;
            EditorApplication.hierarchyChanged += ScheduleRefreshOpenPreviews;
            ScheduleRefreshOpenPreviews();
        }

        internal static IReadOnlyList<string> Languages
        {
            get { RefreshIfNeeded(); return _settings?.Locales ?? Array.Empty<string>(); }
        }

        internal static string Language
        {
            get { RefreshIfNeeded(); return CurrentLanguage(_settings); }
        }

        internal static IReadOnlyDictionary<string, string> EntriesByKey
        {
            get { RefreshIfNeeded(); return _entries; }
        }

        internal static void InvalidateCache()
        {
            _dirty = true;
            Changed?.Invoke();
            ScheduleRefreshOpenPreviews();
        }

        private static void ScheduleRefreshOpenPreviews()
        {
            if (_refreshScheduled || Application.isPlaying) return;
            _refreshScheduled = true;
            EditorApplication.delayCall += RefreshOpenPreviews;
        }

        internal static void SetLanguage(string locale)
        {
            if (Application.isPlaying) return;
            EditorPrefs.SetString(LocalizationSettings.EditorLanguagePrefsKey, locale);
            InvalidateCache();
        }

        internal static string GetPreviewLabel(string key) => Application.isPlaying ? LocalizationKit.Get(key) : key != null && EntriesByKey.TryGetValue(key, out var value)
            ? value ?? key : key;

        private static string CurrentLanguage(LocalizationSettings settings)
        {
            if (settings == null) return string.Empty;
            string language = EditorPrefs.GetString(LocalizationSettings.EditorLanguagePrefsKey, settings.DefaultLocale);
            return settings.Supports(language) ? language : settings.DefaultLocale;
        }

        internal static string Sources(string key) => string.Join(" → ", System.Linq.Enumerable.Select(
            System.Linq.Enumerable.Where(LocalizationTableEditing.FindAll(), t => t.Keys.Contains(key)), t => t.TableId));

        private static void RefreshIfNeeded()
        {
            if (!_dirty) return;
            _dirty = false;
            _settings = LocalizationSettings.Current;
            _entries = new Dictionary<string, string>(StringComparer.Ordinal);
            string locale = CurrentLanguage(_settings);
            foreach (var table in LocalizationTableEditing.FindAll())
            {
                foreach (string key in table.Keys) _entries.TryAdd(key, null);
                var language = table.Find(locale);
                if (language == null) continue;
                foreach (var pair in language.Read())
                    if (pair.Value != null) _entries[pair.Key] = pair.Value;
            }
        }

        private static void RefreshOpenPreviews()
        {
            _refreshScheduled = false;
            if (Application.isPlaying) return;
#if TEXTMESHPRO_SUPPORT
            foreach (LocalizedTMPText binding in Resources.FindObjectsOfTypeAll<LocalizedTMPText>())
                if (!EditorUtility.IsPersistent(binding) && binding.gameObject.scene.IsValid())
                    LocalizedTextEditor.Preview(binding);
#endif
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
    }
}
