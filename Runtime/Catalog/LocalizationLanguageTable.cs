using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlicizaX.Localization.Runtime
{
    public sealed class LocalizationLanguageTable : ScriptableObject
    {
        public string TableId;
        public string Locale;
        public List<LocalizationEntry> Entries = new();

        public Dictionary<string, string> Read()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in Entries)
                if (string.IsNullOrWhiteSpace(entry.Key) || !result.TryAdd(entry.Key, entry.Value))
                    throw new FormatException($"Invalid or duplicate key in {TableId}/{Locale}: {entry.Key}");
            return result;
        }
    }

    [Serializable]
    public sealed class LocalizationEntry
    {
        public string Key;
        public string Value;
        public LocalizationEntry(string key, string value) { Key = key; Value = value; }
    }
}
