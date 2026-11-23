using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AlicizaX.Localization.Runtime;
using AlicizaX.Localization.UI;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlicizaX.Localization.Tests
{
    public sealed class LocalizationPlayModeTests
    {
        private LocalizationSettings _settings;
        private string _saved, _preview;
        [SetUp]
        public void SetUp()
        {
            ResetKit();
            _saved = PlayerPrefs.GetString("AlicizaX.Localization.Language", "");
#if UNITY_EDITOR
            _preview = UnityEditor.EditorPrefs.GetString(LocalizationSettings.EditorLanguagePrefsKey, "");
#endif
            _settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            _settings.Languages = new[] { new LanguageDefinition { Id = "A" }, new LanguageDefinition { Id = "B" } };
            _settings.DefaultLocale = "A";
            LocalizationKit.Initialize(_settings, "A");
        }
        [TearDown]
        public void TearDown()
        {
            ResetKit();
            UnityEngine.Object.DestroyImmediate(_settings);
            if (_saved.Length == 0) PlayerPrefs.DeleteKey("AlicizaX.Localization.Language");
            else PlayerPrefs.SetString("AlicizaX.Localization.Language", _saved);
#if UNITY_EDITOR
            if (_preview.Length == 0) UnityEditor.EditorPrefs.DeleteKey(LocalizationSettings.EditorLanguagePrefsKey);
            else UnityEditor.EditorPrefs.SetString(LocalizationSettings.EditorLanguagePrefsKey, _preview);
#endif
        }
        private static void ResetKit() => typeof(LocalizationKit).GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        private static void Add(string id, Source source) => LocalizationKit.AddTableAsync(id, source).GetAwaiter().GetResult();
        private static void Switch(string locale) => LocalizationKit.SetLanguageAsync(locale).GetAwaiter().GetResult();

        [Test]
        public void UninitializedOperationsLogErrorsWithoutThrowingOrLoading()
        {
            ResetKit();
            var source = new Source("Base");
            LogAssert.Expect(LogType.Error, "Localization is not initialized.");
            Add("Base", source);
            LogAssert.Expect(LogType.Error, "Localization is not initialized.");
            LocalizationKit.ReloadTableAsync("Base").GetAwaiter().GetResult();
            LogAssert.Expect(LogType.Error, "Localization is not initialized.");
            Switch("B");
            LogAssert.Expect(LogType.Error, "Localization is not initialized.");
            Assert.That(LocalizationKit.RegisterLanguageLoader(_ => UniTask.CompletedTask), Is.Null);
            LogAssert.Expect(LogType.Error, "Localization is not initialized.");
            Assert.That(LocalizationKit.Get("title"), Is.EqualTo("title"));
            LogAssert.Expect(LogType.Error, "Localization is not initialized.");
            Assert.That(LocalizationKit.CurrentLanguage, Is.Empty);
            Assert.That(source.Requests, Is.Empty);
        }

        [TestCase("A", "A")]
        [TestCase("B", "B")]
        [TestCase("missing", "A")]
        [TestCase(null, "A")]
        public void PlayerLocaleSelection(string saved, string expected)
        {
            Assert.That(_settings.SelectLanguage(null, saved, null), Is.EqualTo(expected));
        }
        [TestCase("B", "A", "en-US", "B")]
        [TestCase("unknown", "B", "zh-CN", "B")]
        [TestCase(null, "unknown", "en-US", "B")]
        [TestCase(null, null, "zh-CN", "A")]
        [TestCase(null, null, "fr-FR", "A")]
        public void StartupSelectionUsesSupportedChoicesBeforeDefault(string initial, string saved, string system, string expected)
        {
            _settings.Languages[0].LocaleCode = "zh-Hans";
            _settings.Languages[1].LocaleCode = "en";
            _settings.UseSystemLanguage = true;
            Assert.That(_settings.SelectLanguage(initial, saved, system), Is.EqualTo(expected));
        }
        [Test]
        public void RegionAndScriptSelectionDoesNotChooseAnUnrelatedVariant()
        {
            _settings.Languages = new[] {
                new LanguageDefinition { Id = "default", LocaleCode = "ja" },
                new LanguageDefinition { Id = "english", LocaleCode = "en" },
                new LanguageDefinition { Id = "british", LocaleCode = "en-GB" },
                new LanguageDefinition { Id = "traditional", LocaleCode = "zh-Hant" },
                new LanguageDefinition { Id = "simplified", LocaleCode = "zh-Hans" }
            };
            _settings.DefaultLocale = "default";
            _settings.UseSystemLanguage = true;
            Assert.That(_settings.SelectLanguage(null, null, "en-gb"), Is.EqualTo("british"));
            Assert.That(_settings.SelectLanguage(null, null, "en-US"), Is.EqualTo("english"));
            Assert.That(_settings.SelectLanguage(null, null, "zh-TW"), Is.EqualTo("traditional"));
            Assert.That(_settings.SelectLanguage(null, null, "zh-CN"), Is.EqualTo("simplified"));
            _settings.Languages = new[] { _settings.Languages[0], _settings.Languages[2], _settings.Languages[4] };
            Assert.That(_settings.SelectLanguage(null, null, "en-US"), Is.EqualTo("default"));
            Assert.That(_settings.SelectLanguage(null, null, "zh-TW"), Is.EqualTo("default"));
            _settings.UseSystemLanguage = false;
            Assert.That(_settings.SelectLanguage(null, null, "en-GB"), Is.EqualTo("default"));
        }
        [Test]
        public void InvalidInitialLanguageFallsBackBeforeAnyTableIsLoaded()
        {
            ResetKit();
            PlayerPrefs.SetString("AlicizaX.Localization.Language", "B");
            LocalizationKit.Initialize(_settings, "not-supported");
            Assert.That(LocalizationKit.CurrentLanguage, Is.EqualTo("B"));
            Assert.That(LocalizationKit.LoadedTables, Is.Empty);
            var source = new Source("Base");
            Add("Base", source);
            Assert.That(source.Requests, Is.EqualTo(new[] { "B" }));
        }
        [Test]
        public void IncrementalLayersLoadOnlyCurrentLanguageAndLaterProvidedValuesWin()
        {
            var first = new Source("Base"); first.Values["empty"] = "base";
            var patch = new Source("Patch"); patch.Values["empty"] = "";
            Add("Base", first); Add("Patch", patch);
            Assert.That(first.Requests, Is.EqualTo(new[] { "A" }));
            Assert.That(patch.Requests, Is.EqualTo(new[] { "A" }));
            Assert.That(LocalizationKit.Get("title"), Is.EqualTo("Patch A"));
            Assert.That(LocalizationKit.Get("empty"), Is.Empty);
            Assert.That(LocalizationKit.Get("unknown"), Is.EqualTo("unknown"));
            Assert.That(LocalizationKit.LoadedTables.Count, Is.EqualTo(2));
        }
        [Test]
        public void ReloadRemovesDeletedOverridesWithoutChangingPriority()
        {
            var first = new Source("Base"); first.Values["extra"] = "base extra";
            var patch = new Source("Patch"); patch.Values["extra"] = "patch extra";
            Add("Base", first); Add("Patch", patch);
            patch.Values.Remove("extra");
            LocalizationKit.ReloadTableAsync("Patch").GetAwaiter().GetResult();
            Assert.That(LocalizationKit.Get("extra"), Is.EqualTo("base extra"));
            LocalizationKit.ReloadTableAsync("Base").GetAwaiter().GetResult();
            Assert.That(LocalizationKit.Get("title"), Is.EqualTo("Patch A"));
            Assert.That(first.Releases, Is.EqualTo(1));
            Assert.That(patch.Releases, Is.EqualTo(1));
        }
        [Test]
        public void ReplacementSourceIsRetainedForFutureLanguages()
        {
            var old = new Source("Base"); var updated = new Source("Base") { Prefix = "new" };
            Add("Base", old);
            LocalizationKit.ReloadTableAsync("Base", updated).GetAwaiter().GetResult();
            Switch("B");
            Assert.That(LocalizationKit.Get("title"), Is.EqualTo("new B"));
            Assert.That(old.Requests, Is.EqualTo(new[] { "A" }));
            Assert.That(updated.Requests, Is.EqualTo(new[] { "A", "B" }));
        }
        [Test]
        public void FailedReplacementRetainsDataAndSource()
        {
            var source = new Source("Base"); Add("Base", source);
            var bad = new Source("Base") { Fail = true };
            Assert.Throws<InvalidOperationException>(() => LocalizationKit.ReloadTableAsync("Base", bad).GetAwaiter().GetResult());
            Assert.That(LocalizationKit.Get("title"), Is.EqualTo("Base A"));
            Switch("B");
            Assert.That(source.Requests, Is.EqualTo(new[] { "A", "B" }));
        }
        [Test]
        public void InvalidIdentityAndDuplicateRegistrationDoNotCommit()
        {
            var wrong = new Source("Wrong");
            Assert.Throws<InvalidOperationException>(() => Add("Base", wrong));
            Assert.That(wrong.Releases, Is.EqualTo(1));
            Add("Base", new Source("Base"));
            int revision = LocalizationKit.Revision;
            LogAssert.Expect(LogType.Error, "Table 'Base' is already registered.");
            Add("Base", new Source("Base"));
            LogAssert.Expect(LogType.Error, "Table 'unknown' is not registered.");
            LocalizationKit.ReloadTableAsync("unknown").GetAwaiter().GetResult();
            LogAssert.Expect(LogType.Error, "Unsupported locale: unknown");
            Switch("unknown");
            Assert.That(LocalizationKit.Revision, Is.EqualTo(revision));
            Assert.That(LocalizationKit.LoadedTables.Count, Is.EqualTo(1));
        }
        [Test]
        public void SwitchWaitsForOrdinaryTranslationAndNotifiesOnce()
        {
            var source = new Source("Base"); Add("Base", source);
            var pending = new UniTaskCompletionSource();
            string ordinary = "A";
            using var binding = LocalizationKit.RegisterLanguageLoader(async locale => { await pending.Task; ordinary = locale; });
            int notifications = 0;
            using var subscription = LocalizationKit.SubscribeChanged(locale =>
            {
                notifications++; Assert.That(ordinary, Is.EqualTo(locale));
                Assert.That(LocalizationKit.Get("title"), Is.EqualTo("Base B"));
            });
            var task = LocalizationKit.SetLanguageAsync("B");
            Assert.That(LocalizationKit.CurrentLanguage, Is.EqualTo("A"));
            Assert.That(LocalizationKit.Get("title"), Is.EqualTo("Base A"));
            LogAssert.Expect(LogType.Error, "Localization is already loading. Await the current operation first.");
            Switch("B");
            pending.TrySetResult();
            task.GetAwaiter().GetResult();
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(source.Releases, Is.EqualTo(1));
            Switch("B"); Assert.That(notifications, Is.EqualTo(1));
        }
        [Test]
        public void MissingOrdinaryLanguageKeepsPreviousStateAndReleasesStagedAssets()
        {
            var source = new Source("Base"); Add("Base", source);
            using var binding = LocalizationKit.RegisterLanguageLoader(_ => UniTask.FromException(new InvalidOperationException("missing ordinary language")));
            int revision = LocalizationKit.Revision;
            Assert.Throws<InvalidOperationException>(() => Switch("B"));
            Assert.That(LocalizationKit.CurrentLanguage, Is.EqualTo("A"));
            Assert.That(LocalizationKit.Get("title"), Is.EqualTo("Base A"));
            Assert.That(LocalizationKit.Revision, Is.EqualTo(revision));
            Assert.That(source.Releases, Is.EqualTo(1));
        }
        [Test]
        public void AddWaitsForSourceAndNotifiesAfterLoading()
        {
            var source = new Source("Base") { Pending = new UniTaskCompletionSource<LoadedLocalizationTable>() };
            int notifications = 0;
            using var subscription = LocalizationKit.SubscribeChanged(_ => notifications++);
            var task = LocalizationKit.AddTableAsync("Base", source);
            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(LocalizationKit.LoadedTables, Is.Empty);
            Assert.That(notifications, Is.Zero);
            source.Pending.TrySetResult(source.Create("A"));
            task.GetAwaiter().GetResult();
            Assert.That(LocalizationKit.LoadedTables.Count, Is.EqualTo(1));
            Assert.That(LocalizationKit.Get("title"), Is.EqualTo("Base A"));
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(source.Releases, Is.Zero);
        }
        [Test]
        public void DisposedLanguageBindingStopsLoadingOrdinaryData()
        {
            int calls = 0;
            var binding = LocalizationKit.RegisterLanguageLoader(_ => { calls++; return UniTask.CompletedTask; });
            binding.Dispose(); Switch("B"); Assert.That(calls, Is.Zero);
        }
        [UnityTest]
        public IEnumerator LocalizedTMPTextRefreshesAndResubscribesAfterEnable()
        {
            var source = new Source("Base");
            source.Values.Add("count", "Count: {0}");
            Add("Base", source);
            var go = new GameObject("LocalizedTMPText Test", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LocalizedTMPText));
            try
            {
                var text = go.GetComponent<TextMeshProUGUI>();
                var binding = go.GetComponent<LocalizedTMPText>();
                binding.SetKey("title");
                Assert.That(text.text, Is.EqualTo("Base A"));
                binding.SetContent("count", 3);
                Assert.That(text.text, Is.EqualTo("Count: 3"));
                binding.SetContent("title");
                Assert.That(text.text, Is.EqualTo("Base A"));
                Switch("B"); Assert.That(text.text, Is.EqualTo("Base B"));
                go.SetActive(false); Switch("A"); Assert.That(text.text, Is.EqualTo("Base B"));
                go.SetActive(true); Assert.That(text.text, Is.EqualTo("Base A"));
            }
            finally { UnityEngine.Object.Destroy(go); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator LocalizedUGUITextFormatsAndRefreshesOnLanguageChange()
        {
            var source = new Source("Base");
            source.Values.Add("count", "Count: {0}");
            Add("Base", source);
            var go = new GameObject("LocalizedUGUIText Test", typeof(RectTransform), typeof(UnityEngine.UI.Text), typeof(LocalizedUGUIText));
            try
            {
                var text = go.GetComponent<UnityEngine.UI.Text>();
                LocalizedTextBase binding = go.GetComponent<LocalizedUGUIText>();
                binding.SetContent("count", 3);
                Assert.That(text.text, Is.EqualTo("Count: 3"));
                binding.SetArguments(7);
                Assert.That(text.text, Is.EqualTo("Count: 7"));
                binding.SetContent("title");
                Assert.That(text.text, Is.EqualTo("Base A"));
                Switch("B"); Assert.That(text.text, Is.EqualTo("Base B"));
            }
            finally { UnityEngine.Object.Destroy(go); }
            yield return null;
        }

        private sealed class Source : ILocalizationTableSource
        {
            private readonly string _id;
            internal string Prefix;
            internal bool Fail;
            internal int Releases;
            internal readonly List<string> Requests = new();
            internal readonly Dictionary<string, string> Values = new();
            internal UniTaskCompletionSource<LoadedLocalizationTable> Pending;
            internal Source(string id) { _id = id; Prefix = id; }
            public UniTask<LoadedLocalizationTable> LoadAsync(string locale)
            {
                Requests.Add(locale);
                if (Fail) return UniTask.FromException<LoadedLocalizationTable>(new InvalidOperationException("missing table"));
                return Pending != null ? Pending.Task : UniTask.FromResult(Create(locale));
            }
            internal LoadedLocalizationTable Create(string locale)
            {
                var asset = ScriptableObject.CreateInstance<LocalizationLanguageTable>();
                asset.TableId = _id; asset.Locale = locale;
                asset.Entries.Add(new LocalizationEntry("title", Prefix + " " + locale));
                foreach (var pair in Values) asset.Entries.Add(new LocalizationEntry(pair.Key, pair.Value));
                return new LoadedLocalizationTable(asset, () => { Releases++; UnityEngine.Object.DestroyImmediate(asset); });
            }
        }
    }
}
