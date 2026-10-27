using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AlicizaX.Localization.Runtime
{
    public sealed class ResourcesCatalogSource : ILocalizationCatalogSource
    {
        private readonly string _template;
        public ResourcesCatalogSource(string template)
        {
            if (string.IsNullOrEmpty(template) || !template.Contains("{locale}"))
                throw new ArgumentException("Catalog address must contain {locale}.", nameof(template));
            _template = template;
        }

        public byte[] Load(string locale) => ReadAndRelease(Resources.Load<TextAsset>(Address(locale)), locale);

        public async UniTask<byte[]> LoadAsync(string locale)
        {
            var asset = await Resources.LoadAsync<TextAsset>(Address(locale));
            return ReadAndRelease(asset as TextAsset, locale);
        }

        private byte[] ReadAndRelease(TextAsset asset, string locale)
        {
            if (asset == null) throw new InvalidOperationException($"Missing localization catalog: {Address(locale)}");
            byte[] bytes = asset.bytes;
            Resources.UnloadAsset(asset);
            return bytes;
        }

        private string Address(string locale) => _template.Replace("{locale}", locale);
    }
}
