using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace AlicizaX.Localization.Editor
{
    internal sealed class LocalizationKeyPopup : PopupWindowContent
    {
        private const float RowHeight = 40;
        private readonly Action<string> _select;
        private readonly SearchField _searchField = new();
        private readonly KeyValuePair<string, string>[] _all;
        private KeyValuePair<string, string>[] _filtered;
        private string _search = string.Empty;
        private Vector2 _scroll;
        private int _selected;

        internal LocalizationKeyPopup(Action<string> select)
        {
            _select = select;
            _all = LocalizationRefreshHelper.EntriesByKey.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
            _filtered = _all;
        }

        public override Vector2 GetWindowSize() => new(560, 400);
        public override void OnOpen() => _searchField.SetFocus();

        public override void OnGUI(Rect rect)
        {
            string search = _searchField.OnGUI(new Rect(8, 8, rect.width - 16, 20), _search);
            if (search != _search)
            {
                _search = search;
                _filtered = _all.Where(pair => pair.Key.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (pair.Value?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0).ToArray();
                _selected = 0;
                _scroll = Vector2.zero;
            }
            if (GUI.Button(new Rect(8, 32, 70, 20), "Clear")) Choose(string.Empty);
            GUI.Label(new Rect(90, 32, rect.width - 100, 20), _filtered.Length + " keys · " + LocalizationRefreshHelper.Language);
            Rect viewport = new(0, 58, rect.width, rect.height - 58);
            if (Event.current.type == EventType.KeyDown && _filtered.Length > 0)
            {
                if (Event.current.keyCode == KeyCode.Return) { Choose(_filtered[_selected].Key); Event.current.Use(); }
                else if (Event.current.keyCode == KeyCode.DownArrow || Event.current.keyCode == KeyCode.UpArrow)
                {
                    _selected = Mathf.Clamp(_selected + (Event.current.keyCode == KeyCode.DownArrow ? 1 : -1), 0, _filtered.Length - 1);
                    _scroll.y = Mathf.Clamp(_scroll.y, (_selected + 1) * RowHeight - viewport.height, _selected * RowHeight);
                    Event.current.Use();
                }
            }
            _scroll = GUI.BeginScrollView(viewport, _scroll, new Rect(0, 0, rect.width - 18, _filtered.Length * RowHeight));
            int start = Mathf.Max(0, Mathf.FloorToInt(_scroll.y / RowHeight));
            int end = Mathf.Min(_filtered.Length, start + Mathf.CeilToInt(viewport.height / RowHeight) + 1);
            for (int i = start; i < end; i++)
            {
                Rect row = new(0, i * RowHeight, rect.width - 18, RowHeight);
                if (i == _selected) EditorGUI.DrawRect(row, new Color(0.2f, 0.4f, 0.7f, 0.3f));
                if (GUI.Button(row, GUIContent.none, GUIStyle.none)) Choose(_filtered[i].Key);
                GUI.Label(new Rect(8, row.y, row.width - 12, 20), _filtered[i].Key, EditorStyles.boldLabel);
                GUI.Label(new Rect(8, row.y + 20, row.width - 12, 20), (_filtered[i].Value ?? "(null)").Replace('\n', ' '), EditorStyles.miniLabel);
            }
            GUI.EndScrollView();
        }

        private void Choose(string key) { _select(key); editorWindow.Close(); }
    }
}
