using System;
using System.Collections;
using System.Reflection;
using AlicizaX.Localization.Runtime;
#if TEXTMESHPRO_SUPPORT
using AlicizaX.Localization.UI;
#endif
using Cysharp.Threading.Tasks;
using Luban;
using NUnit.Framework;
#if TEXTMESHPRO_SUPPORT
using TMPro;
#endif
using UnityEngine;
using UnityEngine.TestTools;

namespace AlicizaX.Localization.Tests
{
    public sealed class LocalizationPlayModeTests
    {
        private const string Options = "{\"Locales\":[\"A\",\"B\"],\"DefaultLocale\":\"A\"}";

        [TestCase("A", "B", "A")]
        [TestCase("missing", "B", "B")]
        [TestCase(null, "B", "B")]
        [TestCase("missing", "missing", "A")]
        [TestCase(null, null, "A")]
        public void PlayerLocalePrefersSavedThenBuildThenProjectDefault(
            string savedLocale, string buildLocale, string expected)
        {
            MethodInfo select = typeof(LocalizationKit).GetMethod("SelectPlayerLocale",
                BindingFlags.Static | BindingFlags.NonPublic);
            var settings = LocalizationSettings.Parse(Options);
            Assert.That(select.Invoke(null, new object[] { settings, savedLocale, buildLocale }), Is.EqualTo(expected));
        }

        [Test]
        public void ConcurrentInitializationIsRejectedAndFailureCanRetry()
        {
            ResetKit();
            var pending = new PendingSource();
            UniTask first = LocalizationKit.InitializeAsync(Options, pending);
            try
            {
                Assert.Throws<InvalidOperationException>(() =>
                    LocalizationKit.InitializeAsync(Options, new Source()).GetAwaiter().GetResult());
                pending.Complete();
                first.GetAwaiter().GetResult();
                ResetKit();
                Assert.Throws<InvalidOperationException>(() =>
                    LocalizationKit.InitializeAsync(Options, new FailingSource()).GetAwaiter().GetResult());
                LocalizationKit.InitializeAsync(Options, new Source()).GetAwaiter().GetResult();
            }
            finally
            {
                pending.Complete();
                ResetKit();
            }
        }

#if TEXTMESHPRO_SUPPORT
        [UnityTest]
        public IEnumerator LocalizedTextRefreshesOnSwitchAndResubscribesAfterEnable()
        {
            ResetKit();
#if UNITY_EDITOR
            UnityEditor.EditorPrefs.SetString(LocalizationSettings.EditorLanguagePrefsKey, "A");
#endif
            LocalizationKit.InitializeAsync(Options, new Source()).GetAwaiter().GetResult();
            var go = new GameObject("LocalizedText PlayMode Test", typeof(RectTransform),
                typeof(TextMeshProUGUI), typeof(LocalizedText));
            try
            {
                var text = go.GetComponent<TextMeshProUGUI>();
                var binding = go.GetComponent<LocalizedText>();
                binding.SetKey("title");
                Assert.That(text.text, Is.EqualTo("Language A"));
                LocalizationKit.SetLanguageAsync("B").GetAwaiter().GetResult();
                Assert.That(text.text, Is.EqualTo("Language B"));
                go.SetActive(false);
                LocalizationKit.SetLanguageAsync("A").GetAwaiter().GetResult();
                Assert.That(text.text, Is.EqualTo("Language B"));
                go.SetActive(true);
                Assert.That(text.text, Is.EqualTo("Language A"));
            }
            finally { UnityEngine.Object.Destroy(go); }
            yield return null;
        }
#endif

        private static void ResetKit() => typeof(LocalizationKit)
            .GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

        private sealed class PendingSource : ILocalizationCatalogSource
        {
            private readonly UniTaskCompletionSource<byte[]> _completion = new UniTaskCompletionSource<byte[]>();
            private string _locale;

            public UniTask<byte[]> LoadAsync(string locale)
            {
                _locale = locale;
                return _completion.Task;
            }

            public void Complete() => _completion.TrySetResult(
                new Source().LoadAsync(_locale).GetAwaiter().GetResult());
        }

        private sealed class FailingSource : ILocalizationCatalogSource
        {
            public UniTask<byte[]> LoadAsync(string locale) =>
                UniTask.FromException<byte[]>(new InvalidOperationException("Catalog unavailable."));
        }

        private sealed class Source : ILocalizationCatalogSource
        {
            public UniTask<byte[]> LoadAsync(string locale)
            {
                var buffer = new ByteBuf();
                buffer.WriteSize(1);
                buffer.WriteString("title");
                buffer.WriteBool(true);
                buffer.WriteString(locale == "A" ? "Language A" : "Language B");
                var bytes = new byte[buffer.WriterIndex];
                Array.Copy(buffer.Bytes, bytes, bytes.Length);
                return UniTask.FromResult(bytes);
            }
        }
    }
}
