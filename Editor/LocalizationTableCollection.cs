using System.Collections.Generic;
using AlicizaX.Localization.Runtime;
using UnityEngine;

namespace AlicizaX.Localization.Editor
{
    public sealed class LocalizationTableCollection : ScriptableObject
    {
        public string TableId;
        public LocalizationSettings Settings;
        public List<string> Keys = new();
        public List<LocalizationLanguageTable> Tables = new();
        public string AssetDirectory = "Assets/Bundles/Configs/sciptableObject";
        public string AddressTemplate = "{table}_{locale}";
        public string CodeOutput;
        public LocalizationLanguageTable Find(string locale) => Tables.Find(t => t != null && t.Locale == locale);
    }
}
