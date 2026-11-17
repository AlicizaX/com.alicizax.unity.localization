using System;
using System.IO;
using System.Linq;
using AlicizaX.Localization.Runtime;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace AlicizaX.Localization.Editor
{
    [ScriptedImporter(1, "loclang")]
    public sealed class LocalizationLanguageImporter : ScriptedImporter
    {
        [Serializable]
        private sealed class Entry
        {
            public string TableGuid;
            public string Locale;
        }

        private static Entry Read(string path)
        {
            var entry = JsonUtility.FromJson<Entry>(File.ReadAllText(path));
            if (entry == null || string.IsNullOrEmpty(entry.TableGuid) || string.IsNullOrEmpty(entry.Locale))
                throw new FormatException("Invalid localization entry: " + path);
            return entry;
        }

        private static string[] GatherDependenciesFromSourceFile(string path)
        {
            string source = AssetDatabase.GUIDToAssetPath(Read(path).TableGuid);
            return string.IsNullOrEmpty(source) ? Array.Empty<string>() : new[] { source };
        }

        public LocalizationTableCollection GetSourceTable() => AssetDatabase.LoadAssetAtPath<LocalizationTableCollection>(
            AssetDatabase.GUIDToAssetPath(Read(assetPath).TableGuid));

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var entry = Read(ctx.assetPath);
            string path = AssetDatabase.GUIDToAssetPath(entry.TableGuid);
            var collection = AssetDatabase.LoadAssetAtPath<LocalizationTableCollection>(path);
            if (collection == null) throw new InvalidOperationException("Missing localization source: " + ctx.assetPath);
            var source = collection.Find(entry.Locale);
            if (source == null || AssetDatabase.GetAssetPath(source) != path || source.TableId != collection.TableId)
                throw new InvalidOperationException($"Missing language sub-asset: {collection.TableId}/{entry.Locale}");
            source.Read();
            if (source.Entries.Any(value => !collection.Keys.Contains(value.Key)))
                throw new FormatException($"Undefined key in {collection.TableId}/{entry.Locale}");

            var runtime = ScriptableObject.CreateInstance<LocalizationLanguageTable>();
            runtime.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
            runtime.TableId = collection.TableId;
            runtime.Locale = entry.Locale;
            runtime.Entries = source.Entries.Select(value => new LocalizationEntry(value.Key, value.Value)).ToList();
            ctx.AddObjectToAsset("LanguageTable", runtime);
            ctx.SetMainObject(runtime);
        }

        public static string EntryPath(LocalizationTableCollection table, string locale) =>
            table.AssetDirectory.TrimEnd('/') + "/" + table.AddressTemplate.Replace("{table}", table.TableId).Replace("{locale}", locale) + ".loclang";

        public static void ValidateEntryPath(LocalizationTableCollection table, string locale)
        {
            string path = EntryPath(table, locale);
            if (table.Settings.Locales.Any(other => other != locale && string.Equals(EntryPath(table, other), path, StringComparison.OrdinalIgnoreCase)))
                throw new LocalizationValidationException(table, "Language entry names collide: " + table.TableId);
            string assets = Path.GetFullPath("Assets") + Path.DirectorySeparatorChar;
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !Path.GetFullPath(path).StartsWith(assets, StringComparison.OrdinalIgnoreCase))
                throw new LocalizationValidationException(table, "Invalid language entry path: " + path);
            if (!File.Exists(path)) return;
            var existing = Read(path);
            if (existing.TableGuid != AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(table)) || existing.Locale != locale)
                throw new LocalizationValidationException(table, "Language entry path is occupied: " + path)
                    { RelatedTable = AssetDatabase.LoadAssetAtPath<LocalizationTableCollection>(AssetDatabase.GUIDToAssetPath(existing.TableGuid)) };
        }

        public static void SyncEntries(LocalizationTableCollection table)
        {
            var languages = table.Tables.Where(language => language != null).ToArray();
            var desired = languages.ToDictionary(language => language.Locale, language => EntryPath(table, language.Locale), StringComparer.Ordinal);
            if (desired.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != desired.Count)
                throw new LocalizationValidationException(table, "Language entry names collide: " + table.TableId);
            foreach (var language in languages) ValidateEntryPath(table, language.Locale);
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(table));
            foreach (string path in AssetDatabase.GetAllAssetPaths().Where(path => path.EndsWith(".loclang", StringComparison.Ordinal)))
            {
                Entry entry;
                try { entry = Read(path); }
                catch (ArgumentException) { continue; } // An unrelated malformed entry is reported by its importer.
                catch (FormatException) { continue; }
                if (entry.TableGuid != guid) continue;
                if (!desired.TryGetValue(entry.Locale, out string destination)) AssetDatabase.DeleteAsset(path);
                else if (path != destination)
                {
                    EnsureDirectory(Path.GetDirectoryName(destination).Replace('\\', '/'));
                    string error = AssetDatabase.MoveAsset(path, destination);
                    if (error.Length != 0) throw new IOException(error);
                }
            }
            foreach (var language in languages)
            {
                string path = desired[language.Locale];
                if (File.Exists(path)) continue;
                EnsureDirectory(Path.GetDirectoryName(path).Replace('\\', '/'));
                File.WriteAllText(path, JsonUtility.ToJson(new Entry { TableGuid = guid, Locale = language.Locale }, true) + "\n");
                AssetDatabase.ImportAsset(path);
            }
        }

        private static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
