using System.Collections.Generic;
using AlicizaX.Localization.Catalog.Generated.Catalog;
using Luban;

namespace AlicizaX.Localization.Runtime
{
    public static class LocalizationCatalog
    {
        public static Dictionary<string, string> Read(byte[] bytes)
            => new TbText(ByteBuf.Wrap(bytes)).Values;
    }
}
