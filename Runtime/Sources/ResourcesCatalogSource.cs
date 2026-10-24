using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AlicizaX.Localization.Runtime
{
    public sealed class ResourcesCatalogSource : ILocalizationCatalogSource
    {
        private readonly string _prefix;
        public ResourcesCatalogSource(string prefix) => _prefix = prefix;

        public byte[] Load(string locale) => ReadAndRelease(Resources.Load<TextAsset>(_prefix + locale), locale);

        public async UniTask<byte[]> LoadAsync(string locale)
        {
            var asset = await Resources.LoadAsync<TextAsset>(_prefix + locale);
            return ReadAndRelease(asset as TextAsset, locale);
        }

        private byte[] ReadAndRelease(TextAsset asset, string locale)
        {
            if (asset == null) throw new InvalidOperationException($"Missing localization catalog: {_prefix}{locale}");
            byte[] bytes = asset.bytes;
            Resources.UnloadAsset(asset);
            return bytes;
        }
    }
}
