using Cysharp.Threading.Tasks;

namespace AlicizaX.Localization.Runtime
{
    public interface ILocalizationCatalogSource
    {
        UniTask<byte[]> LoadAsync(string locale);
    }
}
