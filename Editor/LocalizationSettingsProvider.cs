using System;
using System.IO;
using System.Linq;
using AlicizaX.Localization.Runtime;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace AlicizaX.Localization.Editor
{
    internal static class LocalizationSettingsProvider
    {
        internal const string SettingsPath = "Project/AlicizaX/Localization";
        [MenuItem("AlicizaX/Localization/Settings")]
        internal static void OpenSettings() => SettingsService.OpenProjectSettings(SettingsPath);

        // Unity 6.5 起 InstanceIDToObject(int) 过时并报错，回调改用 EntityId 签名（与 UnityObjectId 的宏保持一致）。
#if UNITY_6000_5_OR_NEWER
        [OnOpenAsset]
        private static bool OpenAsset(EntityId entityId, int line) => TryOpenAsset(EditorUtility.EntityIdToObject(entityId));
#else
        [OnOpenAsset]
        private static bool OpenAsset(int instanceId, int line) => TryOpenAsset(EditorUtility.InstanceIDToObject(instanceId));
#endif

        private static bool TryOpenAsset(UnityEngine.Object asset)
        {
            if (asset is not LocalizationSettings) return false;
            OpenSettings();
            return true;
        }

        [SettingsProvider]
        private static SettingsProvider Create() => new(SettingsPath, SettingsScope.Project)
        {
            label = "Localization",
            guiHandler = _ => Draw()
        };

        private static void Draw()
        {
            var settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(LocalizationSettings.AssetPath);
            if (settings == null)
            {
                if (GUILayout.Button("创建设置"))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(LocalizationSettings.AssetPath));
                    AssetDatabase.Refresh();
                    settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                    settings.Languages = new[] { new LanguageDefinition { Id = "zh-Hans", LocaleCode = "zh-Hans", DisplayName = "简体中文" } };
                    settings.DefaultLocale = settings.Languages[0].Id;
                    AssetDatabase.CreateAsset(settings, LocalizationSettings.AssetPath);
                    Undo.RegisterCreatedObjectUndo(settings, "Create localization settings");
                    AssetDatabase.SaveAssets();
                }
                return;
            }
            EditorGUILayout.ObjectField("设置资产", settings, typeof(LocalizationSettings), false);
            var serialized = new SerializedObject(settings);
            serialized.Update();
            var languages = serialized.FindProperty("Languages");
            var tables = LocalizationTableEditing.FindAll().Where(t => t.Settings == settings).ToArray();
            GUILayout.Space(8);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("支持的语言", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUILayout.LabelField("资源 Id", GUILayout.Width(180));
                EditorGUILayout.LabelField("语言代码", GUILayout.Width(120));
                EditorGUILayout.LabelField("显示名称");
            }
            for (int i = 0; i < languages.arraySize; i++)
            {
                var language = languages.GetArrayElementAtIndex(i);
                string id = language.FindPropertyRelative("Id").stringValue;
                bool used = tables.Any(t => t.Find(id) != null);
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUI.DisabledScope(used))
                        EditorGUILayout.PropertyField(language.FindPropertyRelative("Id"), new GUIContent("", used ? "已有语言资产使用此 Id" : "用于资源命名与 Luban 别名"), GUILayout.Width(180));
                    if (id != language.FindPropertyRelative("Id").stringValue && serialized.FindProperty("DefaultLocale").stringValue == id)
                        serialized.FindProperty("DefaultLocale").stringValue = language.FindPropertyRelative("Id").stringValue;
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(language.FindPropertyRelative("LocaleCode"), GUIContent.none, GUILayout.Width(120));
                    EditorGUILayout.PropertyField(language.FindPropertyRelative("DisplayName"), GUIContent.none);
                    using (new EditorGUI.DisabledScope(used || languages.arraySize == 1))
                        if (GUILayout.Button(new GUIContent("−", used ? "请先处理使用此语言的 Table" : "移除此语言"), GUILayout.Width(25)))
                        {
                            languages.DeleteArrayElementAtIndex(i);
                            if (serialized.FindProperty("DefaultLocale").stringValue == id)
                                serialized.FindProperty("DefaultLocale").stringValue = languages.GetArrayElementAtIndex(0).FindPropertyRelative("Id").stringValue;
                            break;
                        }
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ 添加语言", GUILayout.Height(26))) LocalizationLocalePicker.Open(settings, false);
                if (GUILayout.Button("添加自定义语言", GUILayout.Height(26))) LocalizationLocalePicker.Open(settings, true);
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("启动语言", EditorStyles.boldLabel);
            var ids = new string[languages.arraySize];
            var labels = new string[languages.arraySize];
            for (int i = 0; i < ids.Length; i++)
            {
                var language = languages.GetArrayElementAtIndex(i);
                ids[i] = language.FindPropertyRelative("Id").stringValue;
                labels[i] = ids[i] + " / " + language.FindPropertyRelative("DisplayName").stringValue;
            }
            var defaultLocale = serialized.FindProperty("DefaultLocale");
            int current = Array.IndexOf(ids, defaultLocale.stringValue);
            int selected = EditorGUILayout.Popup("默认语言", current, labels);
            if (selected >= 0 && selected != current) defaultLocale.stringValue = ids[selected];
            EditorGUILayout.PropertyField(serialized.FindProperty("UseSystemLanguage"), new GUIContent("跟随系统语言", "没有有效的指定语言或玩家记录时，匹配系统语言，再回退到默认语言。"));
            EditorGUILayout.EndVertical();
            if (serialized.ApplyModifiedProperties())
            {
                AssetDatabase.SaveAssetIfDirty(settings);
                LocalizationRefreshHelper.InvalidateCache();
            }
            try { settings.Validate(); }
            catch (Exception error) { EditorGUILayout.HelpBox(error.Message, MessageType.Error); }
        }
    }
}
