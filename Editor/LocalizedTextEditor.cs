#if TEXTMESHPRO_SUPPORT
using System;
using System.Reflection;
using AlicizaX.Localization.Runtime;
using TMPro;
#endif
using AlicizaX.Localization.UI;
using UnityEditor;
using UnityEngine;

namespace AlicizaX.Localization.Editor
{
    [CustomEditor(typeof(LocalizedTextBase), true)]
    [CanEditMultipleObjects]
    internal sealed class LocalizedTextEditor : UnityEditor.Editor
    {
#if TEXTMESHPRO_SUPPORT
        private static readonly FieldInfo TextField = typeof(TMP_Text).GetField("m_text", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo BackingDirtyField = typeof(TMP_Text).GetField("m_IsTextBackingStringDirty", BindingFlags.Instance | BindingFlags.NonPublic);
#endif

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "_key", "_arguments");
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

#if TEXTMESHPRO_SUPPORT
            if (target is LocalizedTMPText && GUILayout.Button("Refresh Preview"))
            {
                LocalizationRefreshHelper.InvalidateCache();
                foreach (UnityEngine.Object selected in targets)
                {
                    if (selected is LocalizedTMPText binding) Preview(binding);
                }
            }
#endif
        }

        private void RefreshSelection()
        {
            foreach (LocalizedTextBase binding in targets)
            {
                if (Application.isPlaying) binding.Refresh();
#if TEXTMESHPRO_SUPPORT
                else if (binding is LocalizedTMPText tmpBinding) Preview(tmpBinding);
#endif
            }
        }

#if TEXTMESHPRO_SUPPORT
        internal static void Preview(LocalizedTMPText binding)
        {
            if (binding == null || Application.isPlaying || EditorUtility.IsPersistent(binding)) return;
            var serialized = new SerializedObject(binding);
            string key = serialized.FindProperty("_key").stringValue;
            TMP_Text target = serialized.FindProperty("_target").objectReferenceValue as TMP_Text;
            if (target == null) target = binding.GetComponent<TMP_Text>();
            if (target == null || EditorUtility.IsPersistent(target)) return;
            if (TextField == null || BackingDirtyField == null)
                throw new MissingFieldException("TMP preview requires m_text and m_IsTextBackingStringDirty.");

            string original = (string)TextField.GetValue(target);
            string text = original ?? string.Empty;
            if (!string.IsNullOrEmpty(key))
            {
                string format = LocalizationRefreshHelper.GetPreviewLabel(key);
                SerializedProperty args = serialized.FindProperty("_arguments");
                object[] values = new object[args.arraySize];
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = args.GetArrayElementAtIndex(i).stringValue;
                }

                try
                {
                    text = LocalizationFormatter.Format(format, values);
                }
                catch (FormatException)
                {
                    text = format;
                }
            }
            char[] characters = text.ToCharArray();
            target.SetCharArray(characters, 0, characters.Length);
            // TMP 3.0.9 assigns m_text inside SetCharArray in the Editor. Restore the
            // serialized value while retaining the preview characters used by its mesh.
            TextField.SetValue(target, original);
            BackingDirtyField.SetValue(target, false);
        }
#endif
    }
}
