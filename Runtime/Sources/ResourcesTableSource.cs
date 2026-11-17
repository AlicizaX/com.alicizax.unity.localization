using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AlicizaX.Localization.Runtime
{
    public sealed class ResourcesTableSource : ILocalizationTableSource
    {
        private static readonly Dictionary<LocalizationLanguageTable, int> References = new();
        private readonly string _template;
        public ResourcesTableSource(string template)
        {
            if (string.IsNullOrEmpty(template) || !template.Contains("{locale}"))
                throw new ArgumentException("Table address must contain {locale}.", nameof(template));
            _template = template;
        }
        public async UniTask<LoadedLocalizationTable> LoadAsync(string locale)
        {
            string address = _template.Replace("{locale}", locale);
            var asset = await Resources.LoadAsync<LocalizationLanguageTable>(address) as LocalizationLanguageTable;
            if (asset == null) throw new InvalidOperationException($"Missing localization table: {address}");
            References.TryGetValue(asset, out int count);
            References[asset] = count + 1;
            return new LoadedLocalizationTable(asset, () =>
            {
                if (--References[asset] != 0) return;
                References.Remove(asset);
                Resources.UnloadAsset(asset);
            });
        }
    }
}
