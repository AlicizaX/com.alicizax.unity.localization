

using Luban;
namespace AlicizaX.Localization.Catalog.Generated.Catalog
{
public readonly struct TextEntry
{
    public TextEntry(ByteBuf _buf)
    {
        Key = _buf.ReadString();
        if(_buf.ReadBool()){ Value = _buf.ReadString(); } else { Value = null; }
    }
    public static TextEntry DeserializeTextEntry(ByteBuf buffer) => new TextEntry(buffer);
    public readonly string Key;
    public readonly string Value;
}
}

