using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AlicizaX.Localization.Runtime;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditorInternal;
using UnityEngine;

namespace AlicizaX.Localization.Editor
{
    public sealed class LocalizationWindow : EditorWindow
    {
        [SerializeField] private LocalizationTableCollection _selected;
        private LocalizationTableCollection[] _tables = Array.Empty<LocalizationTableCollection>();
        private List<LocalizationTableCollection> _preview;
        private ReorderableList _order;
        private Vector2 _scroll, _listScroll, _previewScroll, _optionsScroll;
        private string _search = "", _newKey = "", _key, _locale, _text = "", _error;
        private string _validationError;
        private LocalizationValidationException _diagnostic, _validationDiagnostic;
        private bool _revealEntry;
        private bool _missing, _options, _creating;
        private string _newId = "NewTable", _directory = "Assets/Bundles/Configs/sciptableObject", _code = "";
        private string _sourceDirectory = "Assets";
        private int _tab;
        private List<string> _filtered = new();

        [MenuItem("AlicizaX/Localization/Tables")]
        public static void Open() => GetWindow<LocalizationWindow>("Localization");
        public static void FocusEntry(LocalizationTableCollection table, string key, string locale)
        {
            if (table == null) return;
            var window = GetWindow<LocalizationWindow>("Localization");
            if (Event.current != null) GUI.FocusControl(null);
            window._selected = table;
            window._tab = 0;
            window._creating = false;
            window._search = "";
            window._missing = false;
            window._error = null;
            window._diagnostic = null;
            window._key = key;
            window._locale = locale;
            window._options = key == null;
            window._revealEntry = key != null;
            window.Refresh();
            window.Focus();
            EditorGUIUtility.PingObject(table);
        }
        [MenuItem("Assets/Create/AlicizaX/Localization/Table")]
        [MenuItem("AlicizaX/Localization/Create Table")]
        public static void CreateTable()
        {
            var window = GetWindow<LocalizationWindow>("Localization");
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            window._sourceDirectory = string.IsNullOrEmpty(path) || !path.StartsWith("Assets", StringComparison.Ordinal) ? "Assets"
                : AssetDatabase.IsValidFolder(path) ? path : Path.GetDirectoryName(path).Replace('\\', '/');
            window._tab = 0;
            window._creating = true;
            window.Repaint();
        }
        // Unity 6.5 起 InstanceIDToObject(int) 过时并报错，回调改用 EntityId 签名（与 UnityObjectId 的宏保持一致）。
#if UNITY_6000_5_OR_NEWER
        [OnOpenAsset]
        private static bool OpenAsset(EntityId id, int line) => TryOpenAsset(EditorUtility.EntityIdToObject(id));
#else
        [OnOpenAsset]
        private static bool OpenAsset(int id, int line) => TryOpenAsset(EditorUtility.InstanceIDToObject(id));
#endif

        private static bool TryOpenAsset(UnityEngine.Object asset)
        {
            var table = asset as LocalizationTableCollection;
            var language = asset as LocalizationLanguageTable;
            if (language != null)
            {
                string path = AssetDatabase.GetAssetPath(language);
                table = AssetDatabase.LoadAssetAtPath<LocalizationTableCollection>(path);
                if (table == null && AssetImporter.GetAtPath(path) is LocalizationLanguageImporter importer)
                    table = importer.GetSourceTable();
            }
            if (table == null) return false;
            var window = GetWindow<LocalizationWindow>("Localization");
            window._selected = table;
            window._tab = 0;
            window._creating = false;
            window._search = "";
            window._missing = false;
            window._scroll = Vector2.zero;
            window._error = null;
            window._key = language != null ? table.Keys.FirstOrDefault() : null;
            if (language != null) window._locale = language.Locale;
            window.Refresh();
            return true;
        }
        private void OnEnable()
        {
            minSize = new Vector2(960, 720);
            LocalizationRefreshHelper.Changed += Refresh;
            Refresh();
            _preview = LocalizationRefreshHelper.PreviewTables;
            _order = new ReorderableList(_preview, typeof(LocalizationTableCollection), true, true, false, true);
            _order.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "模拟加入顺序（越靠后优先）");
            _order.drawElementBackgroundCallback = (rect, index, active, focused) => EditorGUI.DrawRect(rect, active ? Selected : RowColor(index));
            _order.drawElementCallback = (rect, index, active, focused) => EditorGUI.LabelField(rect, $"{index + 1}. {_preview[index].TableId}");
            _order.onChangedCallback = _ => LocalizationRefreshHelper.SetPreviewTables(_preview);
        }
        private void OnDisable() => LocalizationRefreshHelper.Changed -= Refresh;
        private void Refresh()
        {
            _tables = LocalizationTableEditing.FindAll();
            if (_selected == null || !_selected.Keys.Contains(_key)) _key = null;
            Filter();
            _validationError = null;
            _validationDiagnostic = null;
            if (_selected != null)
                try { LocalizationCodeGenerator.Validate(_selected); }
                catch (Exception error) { _validationError = error.Message; _validationDiagnostic = error as LocalizationValidationException; }
            if (_selected != null && _selected.Settings != null && !_selected.Settings.Supports(_locale)) _locale = _selected.Settings.Locales.FirstOrDefault();
            if (_selected != null && _key != null && _locale != null) _text = LocalizationTableEditing.Value(_selected, _key, _locale) ?? "";
            Repaint();
        }
        private void Filter()
        {
            _filtered = _selected == null ? new List<string>() : _selected.Keys.Where(key =>
                ((key ?? "").IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 || _selected.Tables.Any(t => t != null && t.Entries.Any(e => e.Key == key && (e.Value?.IndexOf(_search, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0))) &&
                (!_missing || (_selected.Settings?.Locales ?? Array.Empty<string>()).Any(locale => LocalizationTableEditing.Value(_selected, key, locale) == null))).ToList();
        }
        private void Run(Action action)
        {
            try { action(); _error = null; _diagnostic = null; }
            catch (Exception error) { _error = error.Message; _diagnostic = error as LocalizationValidationException; }
        }
        private static Color Tone(float dark, float light)
        {
            float value = EditorGUIUtility.isProSkin ? dark : light;
            return new Color(value, value, value);
        }
        private static Color Canvas => Tone(.16f, .90f);
        private static Color Sidebar => Tone(.12f, .84f);
        private static Color Header => Tone(24f / 255f, .80f);
        private static Color Detail => Tone(.20f, .95f);
        private static Color Divider => Tone(.31f, .65f);
        private static Color Selected => Tone(.32f, .72f);
        private static Color RowColor(int row) => row % 2 == 0 ? Canvas : Tone(.19f, .94f);
        private GUIStyle _titleStyle, _rowStyle, _detailStyle, _tabStyle;
        private void Styles()
        {
            _titleStyle ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
            _rowStyle ??= new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 8, 0, 0), clipping = TextClipping.Clip };
            _detailStyle ??= new GUIStyle { padding = new RectOffset(10, 10, 10, 10) };
            _tabStyle ??= new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter };
        }
        private static Rect Inset(Rect rect, float amount) => new(rect.x + amount, rect.y + amount, Mathf.Max(1, rect.width - amount * 2), Mathf.Max(1, rect.height - amount * 2));
        private static void Outline(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), color);
        }
        private void OnGUI()
        {
            Styles();
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), Canvas);
            EditorGUI.DrawRect(new Rect(0, 0, position.width, 42), Header);
            GUI.Label(new Rect(14, 6, 190, 30), "Localization", _titleStyle);
            for (int i = 0; i < 2; i++)
            {
                var tab = new Rect(222 + i * 105, 9, 105, 25);
                EditorGUI.DrawRect(tab, _tab == i ? Selected : Canvas);
                Outline(tab, Divider);
                if (GUI.Button(tab, i == 0 ? "词库编辑" : "叠加预览", _tabStyle)) _tab = i;
            }
            if (GUI.Button(new Rect(position.width - 135, 9, 120, 25), "语言设置 ↗")) LocalizationSettingsProvider.OpenSettings();
            float top = 43;
            string error = _error ?? _validationError;
            if (!string.IsNullOrEmpty(error))
            {
                var diagnostic = _error != null ? _diagnostic : _validationDiagnostic;
                float buttons = diagnostic == null ? 0 : diagnostic.RelatedTable != null ? 145 : 75;
                EditorGUI.HelpBox(new Rect(8, top + 5, position.width - 16 - buttons, 38), error, MessageType.Error);
                if (diagnostic != null)
                {
                    if (GUI.Button(new Rect(position.width - buttons, top + 10, 65, 26), "定位")) diagnostic.Locate();
                    if (diagnostic.RelatedTable != null && GUI.Button(new Rect(position.width - 70, top + 10, 62, 26), "对照")) diagnostic.Locate(true);
                }
                top += 48;
            }
            var body = new Rect(0, top, position.width, position.height - top);
            if (_tab == 1) { DrawPreview(body); return; }
            var side = new Rect(body.x, body.y, 210, body.height);
            EditorGUI.DrawRect(side, Sidebar);
            EditorGUI.DrawRect(new Rect(side.xMax, side.y, 1, side.height), Divider);
            GUILayout.BeginArea(Inset(side, 10));
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("词库", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label(_tables.Length.ToString(), EditorStyles.miniLabel);
            }
            GUILayout.Space(8);
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            foreach (var table in _tables)
            {
                Rect row = GUILayoutUtility.GetRect(1, 34, GUILayout.ExpandWidth(true));
                if (table == _selected && !_creating)
                {
                    EditorGUI.DrawRect(row, Selected);
                    EditorGUI.DrawRect(new Rect(row.x, row.y, 3, row.height), Tone(.70f, .35f));
                }
                if (GUI.Button(row, new GUIContent(table.TableId, table.Keys.Count + " 条词条"), _rowStyle) && (_selected != table || _creating))
                { _selected = table; _key = null; _creating = false; Refresh(); }
                GUILayout.Space(2);
            }
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("+ 新建词库", GUILayout.Height(28))) CreateTable();
            GUILayout.EndArea();
            var content = new Rect(side.xMax + 1, body.y, body.width - side.width - 1, body.height);
            GUILayout.BeginArea(Inset(content, 12));
            if (_creating) DrawCreate();
            else if (_selected == null) EditorGUILayout.HelpBox("从左侧选择词库，或创建一份新词库。", MessageType.Info);
            else if (_selected.Settings == null) EditorGUILayout.HelpBox("此词库缺少全局语言设置。", MessageType.Error);
            else DrawTable();
            GUILayout.EndArea();
        }
        private void DrawTable()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(_selected.TableId, _titleStyle, GUILayout.Height(30));
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{_selected.Keys.Count} 条词条 · {_selected.Settings.Languages.Length} 种语言", EditorStyles.miniLabel);
                if (GUILayout.Button("定位资产", GUILayout.Width(70), GUILayout.Height(24))) EditorGUIUtility.PingObject(_selected);
            }
            GUILayout.Space(5);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(120));
                _missing = GUILayout.Toggle(_missing, "只看缺失", GUILayout.Width(78));
                if (EditorGUI.EndChangeCheck()) Filter();
                if (GUILayout.Button("导入 CSV", GUILayout.Width(78))) Run(Import);
                if (GUILayout.Button("导出 CSV", GUILayout.Width(78))) Run(Export);
                if (GUILayout.Button("生成代码", GUILayout.Width(78))) Run(() => LocalizationCodeGenerator.Generate(_selected));
            }
            GUILayout.Space(5);
            _options = EditorGUILayout.Foldout(_options, "词库设置与语言资产", true);
            if (_options)
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    _optionsScroll = EditorGUILayout.BeginScrollView(_optionsScroll, GUILayout.Height(150));
                    DrawOptions();
                    EditorGUILayout.EndScrollView();
                }
            GUILayout.Space(4);
            DrawGrid();
            GUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("新 Key", GUILayout.Width(50));
                _newKey = EditorGUILayout.TextField(_newKey);
                if (GUILayout.Button("新增", GUILayout.Width(55))) Run(() => LocalizationTableEditing.AddKey(_selected, _newKey));
                using (new EditorGUI.DisabledScope(_key == null))
                {
                    if (GUILayout.Button("复制", GUILayout.Width(55))) Run(() =>
                    {
                        string copy = _key + "_Copy";
                        LocalizationTableEditing.AddKey(_selected, copy);
                        foreach (string locale in _selected.Settings.Locales)
                            LocalizationTableEditing.SetValue(_selected, copy, locale, LocalizationTableEditing.Value(_selected, _key, locale));
                    });
                    if (GUILayout.Button("删除", GUILayout.Width(55))) Run(() => { LocalizationTableEditing.DeleteKey(_selected, _key); _key = null; });
                }
            }
            GUILayout.Space(10);
            Rect detail = EditorGUILayout.BeginVertical(_detailStyle, GUILayout.Height(180));
            EditorGUI.DrawRect(detail, Detail);
            Outline(detail, Divider);
            DrawDetail();
            EditorGUILayout.EndVertical();
        }
        private void DrawCreate()
        {
            _newId = EditorGUILayout.TextField("TableId", _newId);
            _directory = EditorGUILayout.TextField(new GUIContent("运行入口目录", "自动创建 .loclang 入口；译文统一保存在词库的语言子资产中。"), _directory);
            _code = EditorGUILayout.TextField("常量代码输出", _code);
            if (GUILayout.Button("创建")) Run(() =>
            {
                var settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(LocalizationSettings.AssetPath);
                if (settings == null) throw new InvalidOperationException("请先创建全局语言设置。");
                if (string.IsNullOrWhiteSpace(_newId) || _tables.Any(t => t.TableId == _newId)) throw new ArgumentException("TableId 不能为空或重复。");
                string path = EditorUtility.SaveFilePanelInProject("创建 Localization Table", _newId, "asset", "选择 Table 的保存位置", _sourceDirectory);
                if (path.Length == 0) return;
                var table = CreateInstance<LocalizationTableCollection>();
                table.TableId = _newId; table.Settings = settings; table.AssetDirectory = _directory; table.CodeOutput = _code;
                AssetDatabase.CreateAsset(table, AssetDatabase.GenerateUniqueAssetPath(path));
                Undo.RegisterCreatedObjectUndo(table, "Create localization table");
                _selected = table; _creating = false;
                LocalizationTableEditing.Commit(table);
            });
        }
        private void DrawOptions()
        {
            var serialized = new SerializedObject(_selected);
            serialized.Update();
            EditorGUILayout.PropertyField(serialized.FindProperty("AssetDirectory"), new GUIContent("运行入口目录", "此 Table 的 .loclang 入口共用一个目录；入口只记录词库 GUID 和语言。"));
            EditorGUILayout.PropertyField(serialized.FindProperty("AddressTemplate"), new GUIContent("运行入口命名"));
            EditorGUILayout.PropertyField(serialized.FindProperty("CodeOutput"), new GUIContent("常量代码输出"));
            if (serialized.ApplyModifiedProperties()) Run(() => LocalizationTableEditing.Commit(_selected));
            foreach (string locale in _selected.Settings.Locales)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var current = _selected.Find(locale);
                    using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField(locale, current, typeof(LocalizationLanguageTable), false);
                    if (current == null && GUILayout.Button("创建", GUILayout.Width(50))) Run(() => { LocalizationTableEditing.EnsureLanguage(_selected, locale); LocalizationTableEditing.Commit(_selected); });
                    if (current != null && GUILayout.Button("定位入口", GUILayout.Width(65)))
                        EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(LocalizationLanguageImporter.EntryPath(_selected, locale)));
                }
            }
            if (GUILayout.Button("定位集合资产")) EditorGUIUtility.PingObject(_selected);
        }
        private void DrawGrid()
        {
            const float keyWidth = 215, width = 205, height = 28;
            var keys = _filtered.ToArray();
            string[] locales = _selected.Settings.Locales;
            Rect area = GUILayoutUtility.GetRect(100, 10000, 80, 10000, GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(area, Canvas);
            Rect header = new(area.x, area.y, area.width, height);
            EditorGUI.DrawRect(header, Header);
            GUI.Label(new Rect(header.x + 8, header.y, keyWidth - 8, height), "Key", EditorStyles.boldLabel);
            GUI.BeginGroup(new Rect(header.x + keyWidth, header.y, Mathf.Max(1, header.width - keyWidth), height));
            for (int c = 0; c < locales.Length; c++)
            {
                Rect cell = new(c * width - _scroll.x, 0, width, height);
                GUI.Label(Inset(cell, 4), locales[c] + (_selected.Find(locales[c]) == null ? " · 未创建" : ""), EditorStyles.boldLabel);
                EditorGUI.DrawRect(new Rect(cell.x, 0, 1, height), Divider);
            }
            GUI.EndGroup();
            var body = new Rect(area.x + keyWidth, area.y + height, Mathf.Max(1, area.width - keyWidth), Mathf.Max(1, area.height - height));
            if (_revealEntry && Event.current.type == EventType.Repaint)
            {
                _scroll.y = Mathf.Max(0, Array.IndexOf(keys, _key) * height - (body.height - height) / 2);
                _scroll.x = Mathf.Max(0, Array.IndexOf(locales, _locale) * width - (body.width - width) / 2);
                _revealEntry = false;
            }
            bool selectCell = Event.current.type == EventType.MouseDown && body.Contains(Event.current.mousePosition);
            _scroll = GUI.BeginScrollView(body, _scroll, new Rect(0, 0, locales.Length * width, keys.Length * height));
            // Keep off-screen editors alive so delayed edits commit to the original key after scrolling.
            for (int i = 0; i < keys.Length; i++)
                for (int c = 0; c < locales.Length; c++)
                {
                    string key = keys[i], locale = locales[c];
                    string value = LocalizationTableEditing.Value(_selected, key, locale);
                    Rect cell = new(c * width, i * height, width, height);
                    bool selected = _key == key && _locale == locale;
                    EditorGUI.DrawRect(cell, selected ? Selected : RowColor(i));
                    EditorGUI.DrawRect(new Rect(cell.x, cell.y, 1, cell.height), Divider);
                    if (selectCell && cell.Contains(Event.current.mousePosition)) { _key = key; _locale = locale; _text = value ?? ""; }
                    EditorGUI.BeginChangeCheck();
                    int controlId = GUIUtility.GetControlID((UnityObjectId.Get(_selected), key, locale).GetHashCode(), FocusType.Keyboard);
                    string edited = EditorGUI.DelayedTextField(Inset(cell, 4), GUIContent.none, controlId, value ?? "");
                    if (value == null && string.IsNullOrEmpty(edited)) GUI.Label(Inset(cell, 4), "未翻译", EditorStyles.centeredGreyMiniLabel);
                    else if (value == "") GUI.Label(Inset(cell, 4), "空文本", EditorStyles.centeredGreyMiniLabel);
                    if (EditorGUI.EndChangeCheck()) Run(() => LocalizationTableEditing.SetValue(_selected, key, locale, edited.Length == 0 ? null : edited));
                }
            GUI.EndScrollView();
            float horizontalBarHeight = locales.Length * width > body.width - GUI.skin.verticalScrollbar.fixedWidth ? GUI.skin.horizontalScrollbar.fixedHeight : 0;
            GUI.BeginGroup(new Rect(area.x, body.y, keyWidth, Mathf.Max(1, body.height - horizontalBarHeight)));
            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i];
                Rect row = new(0, i * height - _scroll.y, keyWidth, height);
                EditorGUI.DrawRect(row, key == _key ? Selected : RowColor(i));
                if (GUI.Button(row, new GUIContent(key, key), _rowStyle))
                { _key = key; if (!_selected.Settings.Supports(_locale)) _locale = locales.FirstOrDefault(); _text = LocalizationTableEditing.Value(_selected, key, _locale) ?? ""; }
            }
            GUI.EndGroup();
            EditorGUI.DrawRect(new Rect(area.x + keyWidth - 1, area.y, 1, area.height), Divider);
            Outline(area, Divider);
        }
        private void DrawDetail()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("文本详情", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (_key != null) GUILayout.Label(_locale, EditorStyles.miniLabel);
            }
            if (_key == null || !_selected.Keys.Contains(_key))
            {
                GUILayout.Space(35);
                GUILayout.Label("选择单元格以编辑长文本、换行与格式化参数。", EditorStyles.centeredGreyMiniLabel);
                return;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Key", GUILayout.Width(35));
                string renamed = EditorGUILayout.DelayedTextField(_key);
                if (renamed != _key) Run(() => { LocalizationTableEditing.RenameKey(_selected, _key, renamed); _key = renamed; });
            }
            GUILayout.Space(4);
            _text = EditorGUILayout.TextArea(_text, GUILayout.ExpandHeight(true));
            GUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("设为未翻译", GUILayout.Width(100))) Run(() => LocalizationTableEditing.SetValue(_selected, _key, _locale, null));
                if (GUILayout.Button("设为空文本", GUILayout.Width(100))) Run(() => LocalizationTableEditing.SetValue(_selected, _key, _locale, ""));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("应用文本", GUILayout.Width(100))) Run(() => LocalizationTableEditing.SetValue(_selected, _key, _locale, _text.Length == 0 ? null : _text));
            }
        }
        private void Import()
        {
            string path = EditorUtility.OpenFilePanel("导入 CSV", "", "csv");
            if (path.Length == 0) return;
            LocalizationCsvPreview.Open(_selected, path);
        }
        private void Export()
        {
            string path = EditorUtility.SaveFilePanel("导出 CSV", "", _selected.TableId, "csv");
            if (path.Length != 0) File.WriteAllText(path, LocalizationCsv.Export(_selected), new UTF8Encoding(true));
        }
        private void DrawPreview(Rect body)
        {
            var side = new Rect(body.x, body.y, 245, body.height);
            EditorGUI.DrawRect(side, Sidebar);
            EditorGUI.DrawRect(new Rect(side.xMax, side.y, 1, side.height), Divider);
            GUILayout.BeginArea(Inset(side, 12));
            GUILayout.Label(Application.isPlaying ? "实际加载顺序" : "预览组合", _titleStyle, GUILayout.Height(30));
            if (!Application.isPlaying)
            {
                var locales = LocalizationRefreshHelper.Languages.ToArray();
                int index = Array.IndexOf(locales, LocalizationRefreshHelper.Language);
                int next = EditorGUILayout.Popup(index, locales);
                if (next >= 0 && next != index) LocalizationRefreshHelper.SetLanguage(locales[next]);
                GUILayout.Space(12);
                _order.DoLayoutList();
                if (GUILayout.Button("+ 添加 Table", GUILayout.Height(26)))
                {
                    var menu = new GenericMenu();
                    foreach (var table in _tables.Where(t => !_preview.Contains(t))) menu.AddItem(new GUIContent(table.TableId), false, () => { _preview.Add(table); LocalizationRefreshHelper.SetPreviewTables(_preview); });
                    menu.ShowAsContext();
                }
            }
            else
            {
                GUILayout.Label(LocalizationKit.CurrentLanguage, EditorStyles.miniLabel);
                int order = 0;
                foreach (var table in LocalizationKit.LoadedTables) GUILayout.Label($"{++order}. {table.TableId}", GUILayout.Height(28));
            }
            GUILayout.EndArea();
            var content = new Rect(side.xMax + 12, body.y + 12, body.width - side.width - 24, body.height - 24);
            GUILayout.BeginArea(content);
            GUILayout.Label("最终文本与来源", _titleStyle, GUILayout.Height(30));
            var loaded = Application.isPlaying ? LocalizationKit.LoadedTables.ToList() : _preview.Select(t => t.Find(LocalizationRefreshHelper.Language)).Where(t => t != null).ToList();
            var values = new Dictionary<string, List<(LocalizationLanguageTable table, string value)>>(StringComparer.Ordinal);
            foreach (var table in loaded)
                foreach (var entry in table.Entries)
                {
                    if (entry.Value == null) continue;
                    if (!values.TryGetValue(entry.Key, out var list)) values.Add(entry.Key, list = new());
                    list.Add((table, entry.Value));
                }
            if (values.Count == 0) EditorGUILayout.HelpBox("添加 Table 后显示叠加结果。拖动左侧列表可调整预览顺序。", MessageType.Info);
            _previewScroll = EditorGUILayout.BeginScrollView(_previewScroll);
            int rowIndex = 0;
            foreach (var pair in values.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                var winner = pair.Value[pair.Value.Count - 1];
                Rect row = GUILayoutUtility.GetRect(1, pair.Value.Count > 1 ? 74 : 52, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(row, RowColor(rowIndex++));
                if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                {
                    _selected = _tables.FirstOrDefault(t => t.TableId == winner.table.TableId);
                    _key = pair.Key; _locale = winner.table.Locale; _text = winner.value; _tab = 0; Filter();
                }
                GUI.Label(new Rect(row.x + 8, row.y + 4, row.width - 170, 20), pair.Key, EditorStyles.boldLabel);
                GUI.Label(new Rect(row.xMax - 155, row.y + 4, 150, 20), winner.table.TableId, EditorStyles.miniLabel);
                GUI.Label(new Rect(row.x + 8, row.y + 26, row.width - 16, 20), new GUIContent(winner.value.Replace('\n', ' '), winner.value));
                if (pair.Value.Count > 1) GUI.Label(new Rect(row.x + 8, row.y + 48, row.width - 16, 20), string.Join(" → ", pair.Value.Select(v => v.table.TableId + ": " + v.value)), EditorStyles.miniLabel);
                GUILayout.Space(2);
            }
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
