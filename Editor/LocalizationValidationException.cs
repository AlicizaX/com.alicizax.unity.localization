using System;

namespace AlicizaX.Localization.Editor
{
    public sealed class LocalizationValidationException : FormatException
    {
        public LocalizationTableCollection Table { get; }
        public string Key { get; }
        public string Locale { get; }
        public LocalizationTableCollection RelatedTable { get; set; }
        public string RelatedLocale { get; set; }
        public bool SettingsError { get; set; }
        public int CsvLine { get; set; }
        public int CsvColumn { get; set; }

        public LocalizationValidationException(LocalizationTableCollection table, string message, string key = null, string locale = null)
            : base(message)
        {
            Table = table;
            Key = key;
            Locale = locale;
        }

        public void Locate(bool related = false)
        {
            if (SettingsError) LocalizationSettingsProvider.OpenSettings();
            else LocalizationWindow.FocusEntry(related ? RelatedTable : Table, Key, related ? RelatedLocale : Locale);
        }
    }
}
