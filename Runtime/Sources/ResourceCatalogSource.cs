using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AlicizaX.Localization.Runtime
{
    public sealed class ResourceCatalogSource : ILocalizationCatalogSource
    {
        private readonly string _template;
        public ResourceCatalogSource(string template)
        {
            if (string.IsNullOrEmpty(template) || !template.Contains("{locale}"))
                throw new ArgumentException("Catalog address must contain {locale}.", nameof(template));
            _template = template;
        }

        public async UniTask<byte[]> LoadAsync(string locale)
        {
            string address = _template.Replace("{locale}", locale);
            using var asset = await GameApp.Resource.LoadLeaseAsync<TextAsset>(address);
            if (!asset.IsValid) throw new InvalidOperationException($"Missing localization catalog: {address}");
            return asset.Asset.bytes;
        }
    }
}
