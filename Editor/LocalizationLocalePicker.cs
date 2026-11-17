using System;
using System.Globalization;
using System.Linq;
using AlicizaX.Localization.Runtime;
using UnityEditor;
using UnityEngine;

namespace AlicizaX.Localization.Editor
{
    public sealed class LocalizationLocalePicker : EditorWindow
    {
        public static CultureInfo[] AvailableLocales => CultureInfo.GetCultures(CultureTypes.AllCultures)
            .Where(c => c.Name.Length != 0).GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First()).OrderBy(c => c.EnglishName, StringComparer.Ordinal).ToArray();

        private LocalizationSettings _settings;
        private CultureInfo[] _cultures;
        private readonly System.Collections.Generic.HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);
        private Vector2 _scroll;
        private string _search = "", _id = "", _name = "", _code = "", _error;
        private bool _custom;

        internal static void Open(LocalizationSettings settings, bool custom)
        {
            var window = GetWindow<LocalizationLocalePicker>(true, custom ? "添加自定义语言" : "添加语言", true);
            window._settings = settings;
            window._custom = custom;
            window._selected.Clear();
            window._search = "";
            window._error = null;
            window.minSize = new Vector2(600, 430);
        }

        public static void AddLanguages(LocalizationSettings settings, LanguageDefinition[] languages)
        {
            var previous = settings.Languages;
            string defaultLocale = settings.DefaultLocale;
            // Validate the complete selection before recording or changing the asset.
            var candidate = CreateInstance<LocalizationSettings>();
            try
            {
                candidate.Languages = previous.Concat(languages).ToArray();
                candidate.DefaultLocale = string.IsNullOrEmpty(defaultLocale) ? candidate.Languages.FirstOrDefault()?.Id : defaultLocale;
                candidate.Validate();
                Undo.RecordObject(settings, "Add localization languages");
                settings.Languages = candidate.Languages;
                settings.DefaultLocale = candidate.DefaultLocale;
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssetIfDirty(settings);
                LocalizationRefreshHelper.InvalidateCache();
            }
            finally { DestroyImmediate(candidate); }
        }

        private void OnGUI()
        {
            if (_settings == null) { Close(); return; }
            GUILayout.Space(8);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (_custom)
                {
                    _id = EditorGUILayout.TextField("资源 Id", _id);
                    _name = EditorGUILayout.TextField("显示名称", _name);
                    _code = EditorGUILayout.TextField(new GUIContent("语言代码（可选）", "用于匹配系统语言；自定义语言可留空。"), _code);
                }
                else
                {
                    _cultures ??= AvailableLocales;
                    _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                    {
                        GUILayout.Label("语言 / 地区", GUILayout.ExpandWidth(true));
                        GUILayout.Label("代码", GUILayout.Width(130));
                    }
                    _scroll = EditorGUILayout.BeginScrollView(_scroll);
                    int index = 0;
                    foreach (var culture in _cultures.Where(c => Matches(c, _search)))
                    {
                        bool added = _settings.Languages.Any(l => string.Equals(l.LocaleCode, culture.Name, StringComparison.OrdinalIgnoreCase) || l.Id == culture.Name);
                        Rect row = GUILayoutUtility.GetRect(1, 34, GUILayout.ExpandWidth(true));
                        float shade = EditorGUIUtility.isProSkin ? (index++ % 2 == 0 ? .18f : .22f) : (index++ % 2 == 0 ? .88f : .94f);
                        EditorGUI.DrawRect(row, new Color(shade, shade, shade));
                        using (new EditorGUI.DisabledScope(added))
                        {
                            bool selected = _selected.Contains(culture.Name);
                            bool next = GUI.Toggle(new Rect(row.x + 6, row.y + 7, 20, 20), added || selected, GUIContent.none);
                            if (!added && next != selected) { if (next) _selected.Add(culture.Name); else _selected.Remove(culture.Name); }
                            GUI.Label(new Rect(row.x + 30, row.y, row.width - 165, 18), culture.EnglishName);
                            GUI.Label(new Rect(row.x + 30, row.y + 16, row.width - 165, 18), culture.NativeName, EditorStyles.miniLabel);
                            GUI.Label(new Rect(row.xMax - 130, row.y + 7, 125, 20), culture.Name);
                        }
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
            if (_error != null) EditorGUILayout.HelpBox(_error, MessageType.Error);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (!_custom) GUILayout.Label($"已选择 {_selected.Count} 种语言", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("取消", GUILayout.Width(80), GUILayout.Height(28))) Close();
                using (new EditorGUI.DisabledScope(_custom ? string.IsNullOrWhiteSpace(_id) : _selected.Count == 0))
                    if (GUILayout.Button("添加", GUILayout.Width(100), GUILayout.Height(28)))
                    {
                        try
                        {
                            AddLanguages(_settings, _custom
                                ? new[] { new LanguageDefinition { Id = _id.Trim(), DisplayName = _name, LocaleCode = _code.Trim() } }
                                : _cultures.Where(c => _selected.Contains(c.Name)).Select(c => new LanguageDefinition { Id = c.Name, LocaleCode = c.Name, DisplayName = c.NativeName }).ToArray());
                            Close();
                        }
                        catch (Exception error) { _error = error.Message; }
                    }
            }
            GUILayout.Space(8);
        }

        private static bool Matches(CultureInfo culture, string search) => new[] { culture.Name, culture.EnglishName, culture.NativeName, culture.DisplayName }
            .Any(value => value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
