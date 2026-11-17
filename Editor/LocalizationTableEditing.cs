using System;
using System.Collections.Generic;
using System.Linq;
using AlicizaX.Localization.Runtime;
using UnityEditor;
using UnityEngine;

namespace AlicizaX.Localization.Editor
{
    [InitializeOnLoad]
    public static class LocalizationTableEditing
    {
        private static readonly HashSet<LocalizationTableCollection> Pending = new();
        static LocalizationTableEditing()
        {
            Undo.undoRedoPerformed += () =>
            {
                foreach (var table in FindAll())
                    if (EditorUtility.IsDirty(table) || table.Tables.Any(t => t != null && EditorUtility.IsDirty(t))) Commit(table);
            };
        }
        public static LocalizationTableCollection[] FindAll() => AssetDatabase.FindAssets("t:LocalizationTableCollection", new[] { "Assets" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<LocalizationTableCollection>(AssetDatabase.GUIDToAssetPath(guid)))
            .OrderBy(t => t.TableId, StringComparer.Ordinal).ToArray();

        public static LocalizationLanguageTable EnsureLanguage(LocalizationTableCollection collection, string locale)
        {
            var table = collection.Find(locale);
            if (table != null) return table;
            if (collection.Settings == null || !collection.Settings.Supports(locale)) throw new ArgumentException("Unknown language: " + locale);
            LocalizationLanguageImporter.ValidateEntryPath(collection, locale);
            table = ScriptableObject.CreateInstance<LocalizationLanguageTable>();
            table.name = locale;
            table.TableId = collection.TableId;
            table.Locale = locale;
            AssetDatabase.AddObjectToAsset(table, collection);
            Undo.RegisterCreatedObjectUndo(table, "Create language table");
            Undo.RecordObject(collection, "Add language table");
            collection.Tables.Add(table);
            return table;
        }

        public static void SetValue(LocalizationTableCollection collection, string key, string locale, string value)
        {
            if (!collection.Keys.Contains(key)) throw new ArgumentException("Unknown key: " + key);
            var table = collection.Find(locale);
            if (table == null && value == null) return;
            table ??= EnsureLanguage(collection, locale);
            Undo.RecordObject(table, "Edit localization");
            table.Entries.RemoveAll(entry => entry.Key == key);
            if (value != null) table.Entries.Add(new LocalizationEntry(key, value));
            Commit(collection);
        }

        public static string Value(LocalizationTableCollection collection, string key, string locale) =>
            collection.Find(locale)?.Entries.Find(entry => entry.Key == key)?.Value;

        public static void AddKey(LocalizationTableCollection table, string key)
        {
            if (string.IsNullOrWhiteSpace(key) || table.Keys.Contains(key)) throw new ArgumentException("Invalid or duplicate key: " + key);
            Undo.RecordObject(table, "Add localization key");
            table.Keys.Add(key);
            Commit(table);
        }

        public static void RenameKey(LocalizationTableCollection table, string oldKey, string newKey)
        {
            if (oldKey == newKey) return;
            if (string.IsNullOrWhiteSpace(newKey) || table.Keys.Contains(newKey)) throw new ArgumentException("Invalid or duplicate key: " + newKey);
            Undo.RecordObjects(table.Tables.Where(t => t != null).Cast<UnityEngine.Object>().Append(table).ToArray(), "Rename localization key");
            table.Keys[table.Keys.IndexOf(oldKey)] = newKey;
            foreach (var language in table.Tables.Where(t => t != null))
                foreach (var entry in language.Entries.Where(e => e.Key == oldKey)) entry.Key = newKey;
            Commit(table);
        }

        public static void DeleteKey(LocalizationTableCollection table, string key)
        {
            Undo.RecordObjects(table.Tables.Where(t => t != null).Cast<UnityEngine.Object>().Append(table).ToArray(), "Delete localization key");
            table.Keys.Remove(key);
            foreach (var language in table.Tables.Where(t => t != null)) language.Entries.RemoveAll(e => e.Key == key);
            Commit(table);
        }

        public static void Commit(LocalizationTableCollection table)
        {
            // Import only after both the source and its entry files reflect the same edit (including Undo).
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var language in table.Tables.Where(t => t != null)) EditorUtility.SetDirty(language);
                EditorUtility.SetDirty(table);
                AssetDatabase.SaveAssetIfDirty(table);
                LocalizationLanguageImporter.SyncEntries(table);
            }
            finally { AssetDatabase.StopAssetEditing(); }
            if (Pending.Add(table)) EditorApplication.delayCall += GeneratePending;
            LocalizationRefreshHelper.InvalidateCache();
        }

        private static void GeneratePending()
        {
            var pending = Pending.ToArray();
            Pending.Clear();
            foreach (var table in pending.Where(t => t != null))
                try { LocalizationCodeGenerator.Generate(table); }
                catch (Exception error) { Debug.LogError(error.Message, table); }
        }
    }
}
