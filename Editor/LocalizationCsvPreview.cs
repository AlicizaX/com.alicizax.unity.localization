using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AlicizaX.Localization.Editor
{
    public sealed class LocalizationCsvPreview : EditorWindow
    {
        private LocalizationTableCollection _table;
        private LocalizationCsv.ImportPlan _plan;
        private string _path, _error;
        private LocalizationValidationException _diagnostic;
        private Vector2 _scroll;

        internal static void Open(LocalizationTableCollection table, string path)
        {
            var window = GetWindow<LocalizationCsvPreview>(true, "CSV 导入预览", true);
            window.minSize = new Vector2(850, 480);
            window._table = table;
            window._path = path;
            window.RefreshDiff();
        }

        private void RefreshDiff()
        {
            _plan = null;
            _error = null;
            _diagnostic = null;
            try { _plan = LocalizationCsv.Preview(_table, File.ReadAllText(_path, new UTF8Encoding(false, true))); }
            catch (Exception error) { _error = error.Message; _diagnostic = error as LocalizationValidationException; }
            Repaint();
        }

        private void OnGUI()
        {
            if (_table == null) { Close(); return; }
            GUILayout.Space(8);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(_table.TableId + " · " + Path.GetFileName(_path), EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(_path, EditorStyles.miniLabel, GUILayout.Height(18));
                if (_plan != null)
                {
                    int removed = _plan.Changes.Count(c => !c.NewKey && c.After == null);
                    int edited = _plan.Changes.Count(c => !c.NewKey && c.After != null);
                    GUILayout.Label($"新增 Key {_plan.NewKeys.Count}    新增/修改翻译 {edited}    移除翻译 {removed}    无变化单元格 {_plan.UnchangedCells}");
                    GUILayout.Label("未出现的 Key 和语言列保持原样。〈未翻译〉与〈空文本〉分别表示缺失和显式空字符串。", EditorStyles.miniLabel);
                }
            }
            if (_error != null)
            {
                EditorGUILayout.HelpBox(_error, MessageType.Error);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (_diagnostic != null && GUILayout.Button("定位配置")) _diagnostic.Locate();
                    if (_diagnostic?.CsvLine > 0 && GUILayout.Button("打开 CSV 出错行"))
                        UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(_path, _diagnostic.CsvLine);
                }
            }
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Key / 语言", GUILayout.Width(230));
                GUILayout.Label("原值", GUILayout.ExpandWidth(true));
                GUILayout.Label("导入值", GUILayout.ExpandWidth(true));
            }
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_plan != null)
                foreach (var cell in _plan.Changes)
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.VerticalScope(GUILayout.Width(230)))
                        {
                            if (GUILayout.Button(new GUIContent(cell.Key, cell.Key), EditorStyles.wordWrappedLabel)) LocalizationWindow.FocusEntry(_table, cell.Key, cell.Locale);
                            GUILayout.Label(cell.Locale + (cell.NewKey ? " · 新 Key" : cell.After == null ? " · 移除翻译" : " · 更新"), EditorStyles.miniLabel);
                        }
                        DrawValue(cell.Before);
                        DrawValue(cell.After);
                    }
            EditorGUILayout.EndScrollView();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("刷新差异", GUILayout.Width(100), GUILayout.Height(28))) RefreshDiff();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("取消", GUILayout.Width(80), GUILayout.Height(28))) Close();
                using (new EditorGUI.DisabledScope(_plan == null || _error != null || _plan.Changes.Count == 0))
                    if (GUILayout.Button("确认导入", GUILayout.Width(110), GUILayout.Height(28)))
                    {
                        try { LocalizationCsv.Import(_plan); Close(); }
                        catch (Exception error) { _error = error.Message; _diagnostic = error as LocalizationValidationException; }
                    }
            }
            GUILayout.Space(8);
        }

        private void DrawValue(string value)
        {
            float width = Mathf.Max(150, (position.width - 280) / 2);
            var style = new GUIStyle(EditorStyles.wordWrappedLabel);
            string text = value == null ? "〈未翻译〉" : value.Length == 0 ? "〈空文本〉" : value;
            float height = Mathf.Max(40, style.CalcHeight(new GUIContent(text), width));
            EditorGUILayout.SelectableLabel(text, style, GUILayout.Width(width), GUILayout.Height(height));
        }
    }
}
