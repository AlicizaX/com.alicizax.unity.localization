using AlicizaX.Localization.Runtime;
using UnityEditor;
using UnityEngine;
#if UNITY_6000_3_OR_NEWER
using UnityEditor.Toolbars;
#else
using UnityEngine.UIElements;
#endif

namespace AlicizaX.Localization.Editor
{
    [InitializeOnLoad]
    internal static class LocalizationToolbarDropdown
    {
        private const string ElementPath = "AlicizaX/Localization";
        private const string Tooltip = "切换编辑器多语言预览";
        private static readonly Texture2D SettingsIcon = EditorGUIUtility.IconContent("Settings").image as Texture2D;

        static LocalizationToolbarDropdown()
        {
#if UNITY_6000_3_OR_NEWER
            LocalizationRefreshHelper.Changed += () => MainToolbar.Refresh(ElementPath);
            EditorApplication.playModeStateChanged += _ => MainToolbar.Refresh(ElementPath);
#else
            OldToolbarMinimalExtender.RegisterRight(ElementPath, CreateDropdown);
#endif
        }

#if UNITY_6000_3_OR_NEWER
        [MainToolbarElement(ElementPath, defaultDockPosition = MainToolbarDockPosition.Right, defaultDockIndex = 0)]
        public static MainToolbarElement CreateElement() => new MainToolbarDropdown(
            new MainToolbarContent(LocalizationRefreshHelper.Language, SettingsIcon, Tooltip), ShowMenu);

        private static void ShowMenu(Rect rect)
        {
            var menu = new GenericMenu();
            foreach (string locale in LanguageTypes.Languages)
            {
                if (Application.isPlaying) menu.AddDisabledItem(new GUIContent(locale));
                else menu.AddItem(new GUIContent(locale), locale == LocalizationRefreshHelper.Language,
                    () => LocalizationRefreshHelper.SetLanguage(locale));
            }
            menu.DropDown(rect);
        }
#else
        private static VisualElement CreateDropdown()
        {
            var dropdown = new IMGUIContainer(() =>
            {
                GUILayout.BeginHorizontal();
                var content = new GUIContent(LocalizationRefreshHelper.Language, SettingsIcon, Tooltip);
                using (new EditorGUI.DisabledScope(Application.isPlaying || LanguageTypes.Languages.Count == 0))
                {
                    if (EditorGUILayout.DropdownButton(content, FocusType.Passive, EditorStyles.toolbarPopup,
                            GUILayout.MaxWidth(120)))
                    {
                        var menu = new GenericMenu();
                        foreach (string locale in LanguageTypes.Languages)
                            menu.AddItem(new GUIContent(locale), locale == LocalizationRefreshHelper.Language,
                                () => LocalizationRefreshHelper.SetLanguage(locale));
                        menu.ShowAsContext();
                    }
                }
                GUILayout.Space(5);
                GUILayout.EndHorizontal();
            });
            void Refresh() => dropdown.MarkDirtyRepaint();
            void PlayChanged(PlayModeStateChange state) => Refresh();
            dropdown.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                LocalizationRefreshHelper.Changed += Refresh;
                EditorApplication.playModeStateChanged += PlayChanged;
            });
            dropdown.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                LocalizationRefreshHelper.Changed -= Refresh;
                EditorApplication.playModeStateChanged -= PlayChanged;
            });
            return dropdown;
        }
#endif
    }
}
