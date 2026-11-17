using System;
using Cysharp.Threading.Tasks;

namespace AlicizaX.Localization.Runtime
{
    public sealed class AssetBundleTableSource : ILocalizationTableSource
    {
        private readonly string _template;
        private readonly string _packageName;
        public AssetBundleTableSource(string template, string packageName = null)
        {
            if (string.IsNullOrEmpty(template) || !template.Contains("{locale}"))
                throw new ArgumentException("Table address must contain {locale}.", nameof(template));
            _template = template;
            _packageName = packageName;
        }
        public async UniTask<LoadedLocalizationTable> LoadAsync(string locale)
        {
            string address = _template.Replace("{locale}", locale);
            var lease = await GameApp.Resource.LoadLeaseAsync<LocalizationLanguageTable>(address, packageName: _packageName);
            if (!lease.IsValid)
            {
                lease.Dispose();
                throw new InvalidOperationException($"Missing localization table: {_packageName}/{address}");
            }
            return new LoadedLocalizationTable(lease.Asset, lease.Dispose);
        }
    }
}
