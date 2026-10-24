

using Luban;
using System.Collections.Generic;
namespace AlicizaX.Localization.Catalog.Generated.Catalog
{
public sealed class TbText
{
    public readonly Dictionary<string, string> Values;
    public TbText(ByteBuf buffer)
    {
        int count = buffer.ReadSize();
        Values = new Dictionary<string, string>(count, System.StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        {
            var entry = new Catalog.TextEntry(buffer);
            Values.Add(entry.Key, entry.Value);
        }
    }
}
}

