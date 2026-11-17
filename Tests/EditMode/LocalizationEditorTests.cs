using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AlicizaX.Localization.Editor;
using AlicizaX.Localization.Runtime;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlicizaX.Localization.Tests
{
    public sealed class LocalizationEditorTests
    {
        private string _folder;
        private LocalizationSettings _settings;
        private LocalizationTableCollection _table;
        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/Localization/Editor/Test" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(_folder); AssetDatabase.Refresh();
            _settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            _settings.Languages = new[] { new LanguageDefinition { Id = "cn" }, new LanguageDefinition { Id = "en" } };
            _settings.DefaultLocale = "cn";
            AssetDatabase.CreateAsset(_settings, _folder + "/Settings.asset");
            _table = Create("Test" + Guid.NewGuid().ToString("N"));
        }
        private LocalizationTableCollection Create(string id)
        {
            var table = ScriptableObject.CreateInstance<LocalizationTableCollection>();
            table.TableId = id; table.Settings = _settings; table.AssetDirectory = _folder;
            AssetDatabase.CreateAsset(table, _folder + "/" + id + ".asset");
            return table;
        }
        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            AssetDatabase.DeleteAsset(_folder);
        }
        private void Key(string key, string cn, string en)
        {
            _table.Keys.Add(key);
            foreach (var pair in new[] { ("cn", cn), ("en", en) })
                if (pair.Item2 != null) LocalizationTableEditing.EnsureLanguage(_table, pair.Item1).Entries.Add(new LocalizationEntry(key, pair.Item2));
        }
        [Test]
        public void CsvRoundTripPreservesWhitespaceQuotesNewlinesAndEmptyState()
        {
            Key("test.quote", " 你好, \"世界\"\r\n下一行 ", "hello\nnext");
            Key("test.empty", "", null);
            string csv = LocalizationCsv.Export(_table);
            var rows = LocalizationCsv.Read(_table, "\uFEFF" + csv);
            Assert.That(rows["test.quote"]["cn"], Is.EqualTo(LocalizationTableEditing.Value(_table, "test.quote", "cn")));
            Assert.That(rows["test.empty"]["cn"], Is.Empty);
            Assert.That(rows["test.empty"]["en"], Is.Null);
        }
        [Test]
        public void CsvPartialImportPreservesUnmentionedValuesAndUndoRestoresChanges()
        {
            Key("test.one", "old", "english"); Key("test.two", "two", "second");
            Undo.ClearAll();
            LocalizationCsv.Import(_table, "Key,cn\r\ntest.one,new\r\ntest.add,added\r\n");
            Assert.That(LocalizationTableEditing.Value(_table, "test.one", "cn"), Is.EqualTo("new"));
            Assert.That(LocalizationTableEditing.Value(_table, "test.one", "en"), Is.EqualTo("english"));
            Assert.That(LocalizationTableEditing.Value(_table, "test.two", "cn"), Is.EqualTo("two"));
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Assert.That(LocalizationTableEditing.Value(_table, "test.one", "cn"), Is.EqualTo("old"));
            Assert.That(_table.Keys, Does.Not.Contain("test.add"));
            Undo.PerformRedo();
            Assert.That(LocalizationTableEditing.Value(_table, "test.one", "cn"), Is.EqualTo("new"));
        }
        [TestCase("Key,xx\nkey,value")]
        [TestCase("Key,cn\nkey,one\nkey,two")]
        [TestCase("Key,cn\nkey,\"unclosed")]
        public void InvalidCsvDoesNotChangeAssets(string csv)
        {
            Key("key", "old", null);
            Assert.Catch<FormatException>(() => LocalizationCsv.Import(_table, csv));
            Assert.That(LocalizationTableEditing.Value(_table, "key", "cn"), Is.EqualTo("old"));
        }
        [Test]
        public void CrossTableFormatContractAllowsEmptyButRejectsDifferentIndexes()
        {
            Key("test.format", "value {0}", "value {0}");
            var patch = Create("Patch" + Guid.NewGuid().ToString("N"));
            patch.Keys.Add("test.format");
            var language = LocalizationTableEditing.EnsureLanguage(patch, "cn");
            language.Entries.Add(new LocalizationEntry("test.format", ""));
            Assert.That(LocalizationCodeGenerator.Validate(patch)["test.format"], Is.EqualTo(1));
            language.Entries[0].Value = "{1}";
            Assert.Catch<FormatException>(() => LocalizationCodeGenerator.Validate(patch));
        }
        [Test]
        public void UnchangedAccessorIsNotRewritten()
        {
            Key("test.hello", "hello", "hello");
            // Outside Assets so this test does not trigger an assembly reload.
            _table.CodeOutput = Path.GetFullPath("Temp/LocalizationMigration/TestAccessor.cs");
            try
            {
                LocalizationCodeGenerator.Generate(_table);
                var before = File.GetLastWriteTimeUtc(_table.CodeOutput);
                _table.Find("cn").Entries[0].Value = "updated";
                LocalizationCodeGenerator.Generate(_table);
                Assert.That(File.GetLastWriteTimeUtc(_table.CodeOutput), Is.EqualTo(before));
            }
            finally { if (File.Exists(_table.CodeOutput)) File.Delete(_table.CodeOutput); }
        }
        [Test]
        public void MigratedAssetsAndAccessorsMatchOriginalKeys()
        {
            foreach (string id in new[] { "StartupTable", "CommonTable" })
            {
                var table = LocalizationTableEditing.FindAll().Single(t => t.TableId == id);
                Assert.That(table.Tables.Count, Is.EqualTo(3));
                string before = File.ReadAllText(table.CodeOutput);
                LocalizationCodeGenerator.Generate(table);
                Assert.That(File.ReadAllText(table.CodeOutput), Is.EqualTo(before));
                foreach (var language in table.Tables)
                {
                    Assert.That(AssetDatabase.IsSubAsset(language), Is.True);
                    Assert.That(AssetDatabase.GetAssetPath(language), Is.EqualTo(AssetDatabase.GetAssetPath(table)));
                    Assert.That(language.TableId, Is.EqualTo(id));
                    Assert.That(language.Read().Keys.All(table.Keys.Contains), Is.True);
                    string entry = LocalizationLanguageImporter.EntryPath(table, language.Locale);
                    var runtime = AssetDatabase.LoadAssetAtPath<LocalizationLanguageTable>(entry);
                    Assert.That(runtime, Is.Not.Null, entry);
                    Assert.That(runtime.Read(), Is.EquivalentTo(language.Read()));
                }
            }
        }
        [Test]
        public void ImportedLanguageHasNoSourceReferenceAndUpdatesThroughImportDependency()
        {
            Key("test.value", "中文", "English");
            LocalizationTableEditing.Commit(_table);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string cn = LocalizationLanguageImporter.EntryPath(_table, "cn");
            string en = LocalizationLanguageImporter.EntryPath(_table, "en");
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(cn).OfType<LocalizationLanguageTable>().Count(), Is.EqualTo(1));
            Assert.That(AssetDatabase.LoadAssetAtPath<LocalizationLanguageTable>(cn).Read()["test.value"], Is.EqualTo("中文"));
            Assert.That(AssetDatabase.GetDependencies(cn), Does.Not.Contain(AssetDatabase.GetAssetPath(_table)));
            Assert.That(File.ReadAllText(cn), Does.Not.Contain("中文"));

            // Save the source directly: the imported result must update without the window's Commit method.
            var english = _table.Find("en");
            english.Entries[0].Value = "Changed";
            EditorUtility.SetDirty(english);
            AssetDatabase.SaveAssetIfDirty(english);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Assert.That(AssetDatabase.LoadAssetAtPath<LocalizationLanguageTable>(en).Read()["test.value"], Is.EqualTo("Changed"));
            Assert.That(AssetDatabase.LoadAssetAtPath<LocalizationLanguageTable>(cn).Read()["test.value"], Is.EqualTo("中文"));

            string error = AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(_table), _folder + "/Moved.asset");
            Assert.That(error, Is.Empty);
            AssetDatabase.ImportAsset(cn, ImportAssetOptions.ForceSynchronousImport);
            Assert.That(AssetDatabase.LoadAssetAtPath<LocalizationLanguageTable>(cn).Read()["test.value"], Is.EqualTo("中文"));
        }
        [Test]
        public void CsvCreatingLanguageCanUndoAndRedoSubAssetAndRuntimeEntry()
        {
            Undo.ClearAll();
            LocalizationCsv.Import(_table, "Key,cn\ntest.new,你好\n");
            string entry = LocalizationLanguageImporter.EntryPath(_table, "cn");
            Assert.That(AssetDatabase.IsSubAsset(_table.Find("cn")), Is.True);
            Assert.That(File.Exists(entry), Is.True);
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Assert.That(_table.Find("cn"), Is.Null);
            Assert.That(File.Exists(entry), Is.False);
            Undo.PerformRedo();
            Assert.That(AssetDatabase.IsSubAsset(_table.Find("cn")), Is.True);
            Assert.That(File.Exists(entry), Is.True);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Assert.That(AssetDatabase.LoadAssetAtPath<LocalizationLanguageTable>(entry).Read()["test.new"], Is.EqualTo("你好"));
        }
        [Test]
        public void ChangingEntryDirectoryMovesEntriesWithoutDuplicatingThem()
        {
            Key("test.value", "中文", "English");
            LocalizationTableEditing.Commit(_table);
            string before = LocalizationLanguageImporter.EntryPath(_table, "cn");
            string guid = AssetDatabase.AssetPathToGUID(before);
            _table.AssetDirectory += "/Runtime";
            LocalizationTableEditing.Commit(_table);
            Assert.That(File.Exists(before), Is.False);
            Assert.That(AssetDatabase.AssetPathToGUID(LocalizationLanguageImporter.EntryPath(_table, "cn")), Is.EqualTo(guid));
        }
        [Test]
        public void CsvRejectsCollidingEntryNamesBeforeChangingSource()
        {
            _table.AddressTemplate = "same";
            var error = Assert.Throws<LocalizationValidationException>(() => LocalizationCsv.Import(_table, "Key,cn,en\ntest.new,中文,English\n"));
            Assert.That(error.Table, Is.SameAs(_table));
            Assert.That(error.Key, Is.Null);
            Assert.That(_table.Keys, Is.Empty);
            Assert.That(_table.Tables, Is.Empty);
        }
        [UnityTest]
        public IEnumerator ScrollingSelectedValueDoesNotOverwriteAnotherKey() => ScrollValue(false);

        [UnityTest]
        public IEnumerator ScrollingPendingEditCommitsOnlyToOriginalKey() => ScrollValue(true);

        private IEnumerator ScrollValue(bool edit)
        {
            for (int i = 0; i < 60; i++) Key($"scroll.{i:D2}", $"Chinese {i}", $"English {i}");
            LocalizationTableEditing.Commit(_table);
            Assert.That(OpenWindowAsset(_table), Is.True);
            var window = EditorWindow.GetWindow<LocalizationWindow>();
            try
            {
                WindowField("_options").SetValue(window, false);
                window.position = new Rect(300, 200, 1100, 800);
                window.Focus(); window.Repaint();
                yield return null;
                yield return null;
                var cell = new Vector2(500, 205);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = cell });
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = cell });
                Assert.That(WindowField("_key").GetValue(window), Is.EqualTo("scroll.00"), "The regression must focus the first value cell.");
                if (edit)
                {
                    window.SendEvent(Event.KeyboardEvent("^a"));
                    window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Z, character = 'z' });
                }
                window.SendEvent(new Event { type = EventType.ScrollWheel, mousePosition = cell, delta = new Vector2(0, 30) });
                window.Repaint();
                yield return null;
                yield return null;
                Assert.That(((Vector2)WindowField("_scroll").GetValue(window)).y, Is.GreaterThan(28));
                window.SendEvent(Event.KeyboardEvent("return"));
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = cell });
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = cell });
                yield return null;
                for (int i = 0; i < 60; i++)
                {
                    Assert.That(LocalizationTableEditing.Value(_table, $"scroll.{i:D2}", "cn"), Is.EqualTo(edit && i == 0 ? "z" : $"Chinese {i}"), $"cn row {i}");
                    Assert.That(LocalizationTableEditing.Value(_table, $"scroll.{i:D2}", "en"), Is.EqualTo($"English {i}"), $"en row {i}");
                }
                var selectedKey = WindowField("_key").GetValue(window);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = new Vector2(500, 700) });
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = new Vector2(500, 700) });
                Assert.That(WindowField("_key").GetValue(window), Is.EqualTo(selectedKey), "Clicking the detail panel must not select an off-screen row.");
            }
            finally { window.Close(); }
        }

        [Test]
        public void OpeningLanguageSubAssetsAndEntriesSelectsTheirSourceTableAndLocale()
        {
            Key("test.open", "中文", "English");
            LocalizationTableEditing.Commit(_table);
            Assert.That(AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(_table), _folder + "/Renamed.asset"), Is.Empty);
            var window = EditorWindow.GetWindow<LocalizationWindow>();
            try
            {
                foreach (string locale in _settings.Locales)
                    foreach (var asset in new UnityEngine.Object[] { _table.Find(locale), AssetDatabase.LoadMainAssetAtPath(LocalizationLanguageImporter.EntryPath(_table, locale)) })
                    {
                        WindowField("_creating").SetValue(window, true);
                        WindowField("_tab").SetValue(window, 1);
                        WindowField("_search").SetValue(window, "does-not-match");
                        Assert.That(OpenWindowAsset(asset), Is.True);
                        Assert.That(WindowField("_selected").GetValue(window), Is.SameAs(_table));
                        Assert.That(WindowField("_locale").GetValue(window), Is.EqualTo(locale));
                        Assert.That(WindowField("_text").GetValue(window), Is.EqualTo(LocalizationTableEditing.Value(_table, "test.open", locale)));
                        Assert.That(WindowField("_creating").GetValue(window), Is.False);
                        Assert.That(WindowField("_tab").GetValue(window), Is.EqualTo(0));
                        Assert.That((List<string>)WindowField("_filtered").GetValue(window), Does.Contain("test.open"));
                    }
                Assert.That(OpenWindowAsset(_table), Is.True);
                Assert.That(WindowField("_selected").GetValue(window), Is.SameAs(_table));
                Assert.That(OpenWindowAsset(_settings), Is.False);
            }
            finally { window.Close(); }
        }

        private static FieldInfo WindowField(string name) => typeof(LocalizationWindow).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        // 直接传对象给 TryOpenAsset，不经过 InstanceID/EntityId，测试在各 Unity 版本下都不用改。
        private static bool OpenWindowAsset(UnityEngine.Object asset) => (bool)typeof(LocalizationWindow)
            .GetMethod("TryOpenAsset", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { asset });

        [Test]
        public void LocalePickerIncludesRegionsAndScriptsAndRejectsDuplicateCodes()
        {
            var codes = LocalizationLocalePicker.AvailableLocales.Select(c => c.Name).ToArray();
            foreach (string code in new[] { "en", "en-US", "en-GB", "zh-Hans", "zh-Hant", "ja", "ar", "pt-BR", "de-DE", "ko-KR" })
                Assert.That(codes, Does.Contain(code));
            Assert.That(codes, Does.Not.Contain(""));
            Assert.That(codes.Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(codes.Length));
            LocalizationLocalePicker.AddLanguages(_settings, new[] { new LanguageDefinition { Id = "british", LocaleCode = "en-GB", DisplayName = "English (UK)" } });
            Assert.That(_settings.Supports("british"), Is.True);
            Assert.Throws<FormatException>(() => LocalizationLocalePicker.AddLanguages(_settings,
                new[] { new LanguageDefinition { Id = "duplicate", LocaleCode = "en-gb" } }));
            Assert.That(_settings.Supports("duplicate"), Is.False);
            TestContext.WriteLine($"Available locales in this Unity Editor: {codes.Length}");
        }

        [Test]
        public void CsvPreviewMatchesAppliedChangesIncludingMissingAndExplicitEmpty()
        {
            Key("diff.same", "same", "english"); Key("diff.edit", "old", "english");
            Key("diff.remove", "old", null); Key("diff.empty", "old", null); Key("diff.keep", "keep", null);
            string csv = "Key,cn\ndiff.same,same\ndiff.edit,new\ndiff.remove,\ndiff.empty," + new string('"', 6) + "\ndiff.new,added\ndiff.newempty,\n";
            var plan = LocalizationCsv.Preview(_table, csv);
            Assert.That(plan.NewKeys, Is.EquivalentTo(new[] { "diff.new", "diff.newempty" }));
            Assert.That(plan.Changes.Count, Is.EqualTo(5));
            Assert.That(plan.UnchangedCells, Is.EqualTo(1));
            Assert.That(plan.Changes.Single(c => c.Key == "diff.remove").After, Is.Null);
            Assert.That(plan.Changes.Single(c => c.Key == "diff.empty").After, Is.Empty);
            Assert.That(_table.Keys.Count, Is.EqualTo(5));
            Assert.That(LocalizationTableEditing.Value(_table, "diff.edit", "cn"), Is.EqualTo("old"));
            Undo.ClearAll();
            LocalizationCsv.Import(plan);
            foreach (var change in plan.Changes)
                Assert.That(LocalizationTableEditing.Value(_table, change.Key, change.Locale), Is.EqualTo(change.After));
            Assert.That(LocalizationTableEditing.Value(_table, "diff.edit", "en"), Is.EqualTo("english"));
            Assert.That(LocalizationTableEditing.Value(_table, "diff.keep", "cn"), Is.EqualTo("keep"));
            Assert.That(LocalizationCsv.Preview(_table, csv).Changes, Is.Empty);
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Assert.That(_table.Keys.Count, Is.EqualTo(5));
            foreach (var change in plan.Changes)
                Assert.That(LocalizationTableEditing.Value(_table, change.Key, change.Locale), Is.EqualTo(change.Before));
        }

        [Test]
        public void CsvPreviewCannotOverwriteEditsMadeAfterPreview()
        {
            Key("diff.edit", "old", null);
            var plan = LocalizationCsv.Preview(_table, "Key,cn\ndiff.edit,imported\n");
            _table.Find("cn").Entries[0].Value = "edited after preview";
            Assert.Throws<InvalidOperationException>(() => LocalizationCsv.Import(plan));
            Assert.That(LocalizationTableEditing.Value(_table, "diff.edit", "cn"), Is.EqualTo("edited after preview"));
        }

        [Test]
        public void CsvErrorsReportPhysicalLineAndColumnAfterMultilineCells()
        {
            var error = Assert.Throws<LocalizationValidationException>(() => LocalizationCsv.Read(_table,
                "Key,cn\ndiff.one,\"first\nsecond\"\ndiff.two,{0\n"));
            Assert.That(error.CsvLine, Is.EqualTo(4));
            Assert.That(error.CsvColumn, Is.EqualTo(2));
            Assert.That(error.Key, Is.EqualTo("diff.two"));
            Assert.That(error.Locale, Is.EqualTo("cn"));
            Assert.That(_table.Keys, Is.Empty);
        }

        [UnityTest]
        public IEnumerator ValidationNavigationRevealsExactCellAndClearsFilters()
        {
            for (int i = 0; i < 60; i++) Key($"diagnostic.{i:D2}", "中文", "English");
            _table.Find("en").Entries[59].Value = "broken {0";
            var error = Assert.Throws<LocalizationValidationException>(() => LocalizationCodeGenerator.Validate(_table));
            Assert.That(error.Table, Is.SameAs(_table));
            Assert.That(error.Key, Is.EqualTo("diagnostic.59"));
            Assert.That(error.Locale, Is.EqualTo("en"));
            var window = EditorWindow.GetWindow<LocalizationWindow>();
            try
            {
                window.position = new Rect(300, 200, 1100, 800);
                WindowField("_search").SetValue(window, "hidden");
                WindowField("_missing").SetValue(window, true);
                error.Locate();
                yield return null; yield return null;
                Assert.That(WindowField("_selected").GetValue(window), Is.SameAs(_table));
                Assert.That(WindowField("_key").GetValue(window), Is.EqualTo("diagnostic.59"));
                Assert.That(WindowField("_locale").GetValue(window), Is.EqualTo("en"));
                Assert.That(WindowField("_text").GetValue(window), Is.EqualTo("broken {0"));
                Assert.That(((Vector2)WindowField("_scroll").GetValue(window)).y, Is.GreaterThan(0));
            }
            finally { window.Close(); }
        }

        [UnityTest]
        public IEnumerator ResourcesReloadKeepsSharedAssetAliveUntilLastRelease() => UniTask.ToCoroutine(async () =>
        {
            var source = new ResourcesTableSource("Localization/StartupTable_{locale}");
            var first = await source.LoadAsync("ChineseSimplified");
            var second = await source.LoadAsync("ChineseSimplified");
            Assert.That(second.Asset, Is.SameAs(first.Asset));
            first.Dispose();
            Assert.That(second.Asset != null, Is.True);
            Assert.That(second.Asset.Read().Count, Is.EqualTo(9));
            second.Dispose();
        });
    }
}
