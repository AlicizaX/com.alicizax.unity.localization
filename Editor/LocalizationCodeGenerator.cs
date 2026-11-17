using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using AlicizaX.Localization.Runtime;
using UnityEditor;
using UnityEngine;

namespace AlicizaX.Localization.Editor
{
public static class LocalizationCodeGenerator
{
    private sealed class Row { internal string Key; internal int ArgumentCount; }
    public static Dictionary<string, int> Validate(LocalizationTableCollection table,
        Dictionary<string, Dictionary<string, string>> changes = null)
    {
        if (table.Settings == null) throw new LocalizationValidationException(table, "Missing settings: " + table.TableId);
        try { table.Settings.Validate(); }
        catch (FormatException error) { throw new LocalizationValidationException(table, error.Message) { SettingsError = true }; }
        if (string.IsNullOrWhiteSpace(table.TableId)) throw new LocalizationValidationException(table, "TableId is required.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (string key in table.Keys)
            if (string.IsNullOrWhiteSpace(key) || !keys.Add(key))
                throw new LocalizationValidationException(table, $"Invalid or duplicate key: {table.TableId}/{key}", key);
        var tables = LocalizationTableEditing.FindAll();
        foreach (var other in tables.Where(t => t != table))
        {
            if (other.TableId == table.TableId) throw new LocalizationValidationException(table, "Duplicate TableId: " + table.TableId) { RelatedTable = other };
            if (!string.IsNullOrEmpty(table.CodeOutput) && !string.IsNullOrEmpty(other.CodeOutput) &&
                (Path.GetFullPath(table.CodeOutput).Equals(Path.GetFullPath(other.CodeOutput), StringComparison.OrdinalIgnoreCase) ||
                 Path.GetFileNameWithoutExtension(table.CodeOutput) == Path.GetFileNameWithoutExtension(other.CodeOutput)))
                throw new LocalizationValidationException(table, $"Code output collision: {table.TableId} / {other.TableId}") { RelatedTable = other };
        }
        var locales = new HashSet<string>(StringComparer.Ordinal);
        foreach (var language in table.Tables.Where(t => t != null))
        {
            if (language.TableId != table.TableId || !table.Settings.Supports(language.Locale) || !locales.Add(language.Locale))
                throw new LocalizationValidationException(table, "Invalid language asset: " + language.name, locale: language.Locale);
            var entries = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in language.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || !entries.Add(entry.Key))
                    throw new LocalizationValidationException(table, $"Invalid or duplicate key: {table.TableId}/{entry.Key}/{language.Locale}", entry.Key, language.Locale);
                if (!keys.Contains(entry.Key))
                    throw new LocalizationValidationException(table, $"Undefined key: {table.TableId}/{entry.Key}/{language.Locale}", entry.Key, language.Locale);
            }
        }
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string key in table.Keys.Concat(changes?.Keys ?? Enumerable.Empty<string>()).Distinct(StringComparer.Ordinal))
        {
            string signature = null;
            LocalizationTableCollection signatureTable = null;
            string signatureLocale = null;
            int count = 0;
            foreach (var other in tables.Where(t => t != table).Append(table))
            {
                foreach (string locale in other.Settings?.Locales ?? Array.Empty<string>())
                {
                    string value = other == table && changes != null && changes.TryGetValue(key, out var row) && row.TryGetValue(locale, out var changed)
                        ? changed : LocalizationTableEditing.Value(other, key, locale);
                    if (string.IsNullOrEmpty(value)) continue;
                    string found;
                    int arguments;
                    try { found = Signature(value, out arguments); }
                    catch (FormatException error) { throw new LocalizationValidationException(other, $"Invalid format: {other.TableId}/{key}/{locale}\n{error.Message}", key, locale); }
                    if (signature != null && signature != found)
                        throw new LocalizationValidationException(other, $"Format arguments differ: {key}, {signatureTable.TableId}/{signatureLocale} / {other.TableId}/{locale}", key, locale)
                            { RelatedTable = signatureTable, RelatedLocale = signatureLocale };
                    signature = found;
                    signatureTable = other;
                    signatureLocale = locale;
                    count = arguments;
                }
            }
            counts.Add(key, count);
        }
        return counts;
    }

    public static string Signature(string text, out int count)
    {
        var used = new HashSet<int>();
        object[] arguments = Enumerable.Range(0, 257).Select(i => (object)new Marker(i, used)).ToArray();
        string.Format(CultureInfo.InvariantCulture, text, arguments);
        var indexes = used.OrderBy(i => i).ToArray();
        count = indexes.Length == 0 ? 0 : indexes[indexes.Length - 1] + 1;
        return string.Join(",", indexes);
    }
    private sealed class Marker : IFormattable
    {
        private readonly int _index;
        private readonly HashSet<int> _used;
        internal Marker(int index, HashSet<int> used) { _index = index; _used = used; }
        public string ToString(string format, IFormatProvider provider) { _used.Add(_index); return "x"; }
    }

    public static void Generate(LocalizationTableCollection table)
    {
        var counts = Validate(table);
        if (string.IsNullOrWhiteSpace(table.CodeOutput)) return;
        string className = Path.GetFileNameWithoutExtension(table.CodeOutput);
        if (!IsIdentifier(className) || !table.CodeOutput.EndsWith(".cs", StringComparison.Ordinal))
            throw new LocalizationValidationException(table, "Invalid code output: " + table.CodeOutput);
        string content = Accessor(table, className, counts.Select(p => new Row { Key = p.Key, ArgumentCount = p.Value }));
        if (File.Exists(table.CodeOutput) && File.ReadAllText(table.CodeOutput) == content) return;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(table.CodeOutput)));
        File.WriteAllText(table.CodeOutput, content, new UTF8Encoding(false));
        if (table.CodeOutput.StartsWith("Assets/", StringComparison.Ordinal)) AssetDatabase.ImportAsset(table.CodeOutput);
    }
    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
    private static string Accessor(LocalizationTableCollection table, string className, IEnumerable<Row> rows)
    {
        var output = new StringBuilder("using AlicizaX.Localization.Runtime;\n\n");
        output.Append("public static class ").Append(className).AppendLine().AppendLine("{");
        var classNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in rows.OrderBy(row => row.Key, StringComparer.Ordinal)
                     .GroupBy(row => row.Key.Contains('.') ? row.Key[..row.Key.IndexOf('.')] : "Common", StringComparer.Ordinal))
        {
            string groupName = Identifier(group.Key);
            if (groupName == className || !classNames.Add(groupName)) throw new LocalizationValidationException(table, $"Constant class name collision: {group.Key}", group.First().Key);
            output.Append("    public static class ").Append(groupName).AppendLine().AppendLine("    {");
            var members = new HashSet<string>(StringComparer.Ordinal);
            foreach (Row row in group)
            {
                string remainder = row.Key.Contains('.') ? row.Key[(row.Key.IndexOf('.') + 1)..] : row.Key;
                string name = Identifier(remainder.Replace('.', '_')).ToUpperInvariant();
                if (name == groupName || !members.Add(name)) throw new LocalizationValidationException(table, $"Constant member name collision: {row.Key}", row.Key);
                output.Append("        public const string ").Append(name).Append("_Raw = ")
                    .Append(Quote(row.Key)).AppendLine(";");
                if (row.ArgumentCount == 0)
                {
                    output.Append("        public static string ").Append(name).Append(" => LocalizationKit.Get(")
                        .Append(name).AppendLine("_Raw);");
                    continue;
                }
                output.Append("        public static string ").Append(name).Append('<')
                    .Append(string.Join(", ", Enumerable.Range(0, row.ArgumentCount).Select(i => "T" + i))).Append(">(")
                    .Append(string.Join(", ", Enumerable.Range(0, row.ArgumentCount).Select(i => "T" + i + " arg" + i)))
                    .Append(") => LocalizationKit.Format(").Append(name).Append("_Raw, ")
                    .Append(string.Join(", ", Enumerable.Range(0, row.ArgumentCount).Select(i => "arg" + i))).AppendLine(");");
            }
            output.AppendLine("    }");
        }
        output.AppendLine("}");
        return output.ToString().Replace("\r\n", "\n");
    }

    private static readonly HashSet<string> Keywords = new("abstract as base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is lock long namespace new null object operator out override params private protected public readonly ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using virtual void volatile while".Split(' '));

    private static bool IsIdentifier(string value) => !Keywords.Contains(value) && value.Length > 0 && (char.IsLetter(value[0]) || value[0] == '_')
        && value.Skip(1).All(character => char.IsLetterOrDigit(character) || character == '_');

    private static string Identifier(string source)
    {
        var result = new StringBuilder();
        foreach (char character in source) result.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        if (result.Length == 0 || char.IsDigit(result[0])) result.Insert(0, '_');
        string identifier = result.ToString();
        return Keywords.Contains(identifier) ? "_" + identifier : identifier;
    }
}
}
