using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AlicizaX.Localization.Runtime
{
    public sealed class ResourceCatalogSource : ILocalizationCatalogSource
    {
        private readonly string _prefix;
        public ResourceCatalogSource(string prefix = "localization_patch_") => _prefix = prefix;

        public async UniTask<byte[]> LoadAsync(string locale)
        {
            using var asset = await GameApp.Resource.LoadLeaseAsync<TextAsset>(_prefix + locale);
            if (!asset.IsValid) throw new InvalidOperationException($"Missing localization catalog: {_prefix}{locale}");
            return asset.Asset.bytes;
        }
    }
}
