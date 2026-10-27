using System;
using System.Collections.Generic;
using System.IO;
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
        private const string RuntimeOptionsFileName = "RuntimeOptions.json";

        [Serializable]
        private sealed class RuntimeOptions
        {
            public int Version;
            public string[] Catalogs;
        }

        private static Dictionary<string, string> _entries = new Dictionary<string, string>(StringComparer.Ordinal);
        private static LocalizationSettings _settings;
        private static bool _dirty = true;
        private static bool _refreshScheduled;
        internal static event Action Changed;

        static LocalizationRefreshHelper()
        {
            EditorApplication.projectChanged += InvalidateCache;
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

        internal static string GetPreviewLabel(string key) => key != null && EntriesByKey.TryGetValue(key, out var value)
            ? value ?? key : key;

        private static string CurrentLanguage(LocalizationSettings settings)
        {
            if (settings == null) return string.Empty;
            string language = EditorPrefs.GetString(LocalizationSettings.EditorLanguagePrefsKey, settings.DefaultLocale);
            return settings.Supports(language) ? language : settings.DefaultLocale;
        }

        private static string FindRuntimeOptions()
        {
            var matches = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("RuntimeOptions", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(Path.GetFileName(path), RuntimeOptionsFileName, StringComparison.Ordinal))
                    matches.Add(path);
            }
            if (matches.Count == 0) return null;
            if (matches.Count > 1)
            {
                matches.Sort(StringComparer.Ordinal);
                throw new InvalidOperationException("Multiple localization runtime options found: " + string.Join(", ", matches));
            }
            return matches[0];
        }

        private static void RefreshIfNeeded()
        {
            if (!_dirty) return;
            _dirty = false;
            _settings = null;
            _entries = new Dictionary<string, string>(StringComparer.Ordinal);

            string optionsPath = FindRuntimeOptions();
            if (optionsPath == null) return;
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(optionsPath);
            if (asset == null) throw new InvalidOperationException("Cannot load localization runtime options: " + optionsPath);

            LocalizationSettings settings = LocalizationSettings.Parse(asset.text);
            RuntimeOptions options = JsonUtility.FromJson<RuntimeOptions>(asset.text);
            if (options == null || options.Version != 1 || options.Catalogs == null)
                throw new FormatException("Unsupported localization runtime options: " + optionsPath);

            string directory = Path.GetDirectoryName(Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(Application.dataPath), optionsPath)));
            string locale = CurrentLanguage(settings);
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string template in options.Catalogs)
            {
                if (string.IsNullOrWhiteSpace(template))
                    throw new FormatException("Localization runtime options have an empty catalog path: " + optionsPath);
                string relativePath = template.Replace("{locale}", locale).Replace('/', Path.DirectorySeparatorChar);
                string path = Path.GetFullPath(Path.Combine(directory, relativePath));
                if (!File.Exists(path)) continue;
                foreach (var pair in LocalizationCatalog.Read(File.ReadAllBytes(path)))
                    if (!entries.TryAdd(pair.Key, pair.Value))
                        throw new InvalidOperationException("Duplicate localization key in editor preview: " + pair.Key);
            }
            _settings = settings;
            _entries = entries;
        }

        private static void RefreshOpenPreviews()
        {
            _refreshScheduled = false;
            if (Application.isPlaying) return;
#if TEXTMESHPRO_SUPPORT
            foreach (LocalizedText binding in Resources.FindObjectsOfTypeAll<LocalizedText>())
                if (!EditorUtility.IsPersistent(binding) && binding.gameObject.scene.IsValid())
                    LocalizedTextEditor.Preview(binding);
#endif
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
    }
}
