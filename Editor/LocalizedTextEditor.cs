using System;
using AlicizaX.Localization.Runtime;
using AlicizaX.Localization.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AlicizaX.Localization.Editor
{
    [CustomEditor(typeof(LocalizedText))]
    [CanEditMultipleObjects]
    internal sealed class LocalizedTextEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_target"));
            SerializedProperty key = serializedObject.FindProperty("_key");
            Rect keyRect = EditorGUILayout.GetControlRect();
            EditorGUI.BeginProperty(keyRect, new GUIContent("Language Key"), key);
            Rect buttonRect = EditorGUI.PrefixLabel(keyRect, new GUIContent("Language Key"));
            if (EditorGUI.DropdownButton(buttonRect, new GUIContent(key.hasMultipleDifferentValues ? "—" : key.stringValue), FocusType.Keyboard))
            {
                PopupWindow.Show(buttonRect, new LocalizationKeyPopup(selectedKey =>
                {
                    serializedObject.Update();
                    serializedObject.FindProperty("_key").stringValue = selectedKey;
                    serializedObject.ApplyModifiedProperties();
                    RefreshSelection();
                }));
            }
            EditorGUI.EndProperty();
            if (!key.hasMultipleDifferentValues && !string.IsNullOrEmpty(key.stringValue))
                EditorGUILayout.HelpBox(LocalizationRefreshHelper.GetPreviewLabel(key.stringValue), MessageType.None);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_arguments"), true);
            bool changed = serializedObject.ApplyModifiedProperties();
            if (changed) RefreshSelection();

            if (GUILayout.Button("Refresh Preview"))
            {
                LocalizationRefreshHelper.InvalidateCache();
                foreach (UnityEngine.Object selected in targets)
                {
                    Preview((LocalizedText)selected);
                }
            }
        }

        private void RefreshSelection()
        {
            foreach (UnityEngine.Object selected in targets)
            {
                var binding = (LocalizedText)selected;
                if (Application.isPlaying) binding.Refresh();
                else Preview(binding);
            }
        }

        internal static void Preview(LocalizedText binding)
        {
            if (binding == null || Application.isPlaying) return;
            var serialized = new SerializedObject(binding);
            string key = serialized.FindProperty("_key").stringValue;
            if (string.IsNullOrEmpty(key)) return;
            TMP_Text target = serialized.FindProperty("_target").objectReferenceValue as TMP_Text;
            if (target == null) target = binding.GetComponent<TMP_Text>();
            if (target == null) return;

            string format = LocalizationRefreshHelper.GetPreviewLabel(key);
            SerializedProperty args = serialized.FindProperty("_arguments");
            object[] values = new object[args.arraySize];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = args.GetArrayElementAtIndex(i).stringValue;
            }

            string text;
            try
            {
                text = LocalizationFormatter.Format(format, values);
            }
            catch (FormatException)
            {
                text = format;
            }
            if (target.text == text) return;
            Undo.RecordObject(target, "Preview Localization");
            target.SetText(text);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            EditorUtility.SetDirty(target);
            if (target.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
        }
    }
}
