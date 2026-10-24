using System.Collections.Generic;
using AlicizaX.Localization.Catalog.Generated.Catalog;
using Luban;

namespace AlicizaX.Localization.Runtime
{
    public static class LocalizationCatalog
    {
        public static Dictionary<string, string> Read(byte[] bytes)
        {
            return new TbText(ByteBuf.Wrap(bytes)).Values;
        }

        public static void ApplyPatch(Dictionary<string, string> values, IReadOnlyDictionary<string, string> patch)
        {
            foreach (var entry in patch)
                if (entry.Value != null || !values.ContainsKey(entry.Key)) values[entry.Key] = entry.Value;
        }
    }
}
