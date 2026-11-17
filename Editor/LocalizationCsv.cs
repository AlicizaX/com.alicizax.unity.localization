using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AlicizaX.Localization.Runtime;
using UnityEditor;

namespace AlicizaX.Localization.Editor
{
    public static class LocalizationCsv
    {
        public static string Export(LocalizationTableCollection table)
        {
            var output = new StringBuilder();
            var locales = table.Settings.Locales;
            output.AppendLine(string.Join(",", new[] { "Key" }.Concat(locales).Select(Encode)));
            foreach (string key in table.Keys)
            {
                var cells = new List<string> { key };
                foreach (string locale in locales)
                {
                    string value = LocalizationTableEditing.Value(table, key, locale);
                    if (value == "\"\"") throw new FormatException($"Reserved CSV empty-text marker used as text: {key}/{locale}");
                    cells.Add(value == string.Empty ? "\"\"" : value ?? string.Empty);
                }
                output.AppendLine(string.Join(",", cells.Select(Encode)));
            }
            return output.ToString();
        }

        private static string Encode(string value) => value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;

        public static Dictionary<string, Dictionary<string, string>> Read(LocalizationTableCollection table, string text)
        {
            var rows = Parse(table, text.TrimStart('\uFEFF'), out var lines);
            if (rows.Count == 0 || rows[0].Count < 2 || rows[0][0] != "Key") throw CsvError(table, "CSV requires Key and language columns.", 1, 1);
            var header = rows[0];
            var columns = new HashSet<string>(StringComparer.Ordinal) { "Key" };
            for (int c = 1; c < header.Count; c++)
            {
                if (!columns.Add(header[c])) throw CsvError(table, "Duplicate CSV column: " + header[c], 1, c + 1);
                if (!table.Settings.Supports(header[c])) throw CsvError(table, "Unknown CSV language: " + header[c], 1, c + 1);
            }
            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            for (int i = 1; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.Count != header.Count || string.IsNullOrWhiteSpace(row[0])) throw CsvError(table, "Invalid CSV row.", lines[i], 1);
                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int c = 1; c < header.Count; c++) values.Add(header[c], row[c] == "" ? null : row[c] == "\"\"" ? "" : row[c]);
                if (!result.TryAdd(row[0], values)) throw CsvError(table, "Duplicate CSV key: " + row[0], lines[i], 1, row[0]);
            }
            try { LocalizationCodeGenerator.Validate(table, result); }
            catch (LocalizationValidationException error)
            {
                int row = rows.FindIndex(r => r[0] == error.Key);
                int column = header.IndexOf(error.Locale);
                if (error.Table == table && row > 0 && column > 0)
                {
                    var location = CsvError(table, error.Message, lines[row], column + 1, error.Key, error.Locale);
                    location.RelatedTable = error.RelatedTable;
                    location.RelatedLocale = error.RelatedLocale;
                    throw location;
                }
                throw;
            }
            return result;
        }

        public sealed class Change
        {
            public string Key, Locale, Before, After;
            public bool NewKey;
        }

        public sealed class ImportPlan
        {
            public LocalizationTableCollection Table { get; internal set; }
            public List<Change> Changes { get; } = new();
            public List<string> NewKeys { get; } = new();
            public int UnchangedCells { get; internal set; }
            internal Dictionary<string, Dictionary<string, string>> Rows;
            internal readonly List<Change> Original = new();
        }

        public static ImportPlan Preview(LocalizationTableCollection table, string text)
        {
            var rows = Read(table, text);
            foreach (string locale in rows.Values.SelectMany(r => r.Keys).Distinct())
                if (table.Find(locale) == null && rows.Values.Any(r => r.TryGetValue(locale, out var value) && value != null))
                    LocalizationLanguageImporter.ValidateEntryPath(table, locale);
            var plan = new ImportPlan { Table = table, Rows = rows };
            foreach (var row in rows)
            {
                bool added = !table.Keys.Contains(row.Key);
                if (added) plan.NewKeys.Add(row.Key);
                foreach (var cell in row.Value)
                {
                    var change = new Change { Key = row.Key, Locale = cell.Key, Before = LocalizationTableEditing.Value(table, row.Key, cell.Key), After = cell.Value, NewKey = added };
                    plan.Original.Add(change);
                    if (added || change.Before != change.After) plan.Changes.Add(change);
                    else plan.UnchangedCells++;
                }
            }
            return plan;
        }

        public static void Import(LocalizationTableCollection table, string text) => Import(Preview(table, text));

        public static void Import(ImportPlan plan)
        {
            var table = plan.Table;
            foreach (var cell in plan.Original)
                if (table.Keys.Contains(cell.Key) == cell.NewKey || LocalizationTableEditing.Value(table, cell.Key, cell.Locale) != cell.Before)
                    throw new InvalidOperationException("词库内容已改变，请刷新差异后再导入。");
            LocalizationCodeGenerator.Validate(table, plan.Rows);
            foreach (string locale in plan.Changes.Where(c => c.After != null).Select(c => c.Locale).Distinct())
                LocalizationLanguageImporter.ValidateEntryPath(table, locale);
            if (plan.Changes.Count == 0) return;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Import localization CSV");
            Undo.RecordObjects(table.Tables.Where(t => t != null).Cast<UnityEngine.Object>().Append(table).ToArray(), "Import localization CSV");
            foreach (string key in plan.NewKeys) table.Keys.Add(key);
            foreach (var cell in plan.Changes)
            {
                var language = table.Find(cell.Locale);
                if (language == null && cell.After == null) continue;
                language ??= LocalizationTableEditing.EnsureLanguage(table, cell.Locale);
                language.Entries.RemoveAll(e => e.Key == cell.Key);
                if (cell.After != null) language.Entries.Add(new LocalizationEntry(cell.Key, cell.After));
            }
            Undo.CollapseUndoOperations(group);
            LocalizationTableEditing.Commit(table);
        }

        private static LocalizationValidationException CsvError(LocalizationTableCollection table, string message, int line, int column, string key = null, string locale = null) =>
            new(table, $"CSV 第 {line} 行，第 {column} 列：{message}", key, locale) { CsvLine = line, CsvColumn = column };

        private static List<List<string>> Parse(LocalizationTableCollection table, string text, out List<int> lines)
        {
            lines = new List<int>();
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false, closed = false;
            int line = 1, startLine = 1;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                        else { quoted = false; closed = true; }
                    }
                    else { cell.Append(c); if (c == '\n') line++; }
                    continue;
                }
                if (c == ',' || c == '\r' || c == '\n')
                {
                    row.Add(cell.ToString()); cell.Clear(); closed = false;
                    if (c != ',')
                    {
                        rows.Add(row); lines.Add(startLine); row = new List<string>(); line++; startLine = line;
                        if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    }
                }
                else if (closed) throw CsvError(table, "Unexpected character after CSV quote.", line, row.Count + 1);
                else if (c == '"')
                {
                    if (cell.Length != 0) throw CsvError(table, "Unexpected CSV quote.", line, row.Count + 1);
                    quoted = true;
                }
                else cell.Append(c);
            }
            if (quoted) throw CsvError(table, "Unclosed CSV quote.", line, row.Count + 1);
            if (row.Count > 0 || cell.Length > 0 || closed) { row.Add(cell.ToString()); rows.Add(row); lines.Add(startLine); }
            return rows;
        }
    }
}
