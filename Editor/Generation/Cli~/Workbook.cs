using System.IO.Compression;
using System.Xml.Linq;

internal static class Workbook
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Relationship = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    internal static List<Dictionary<int, string?>> Read(string path, string name)
    {
        using var archive = ZipFile.OpenRead(path);
        var sheets = SheetPaths(archive);
        if (!sheets.TryGetValue(name, out string? sheet)) throw new InvalidOperationException($"Missing {name} sheet: {path}");
        return ReadSheet(archive, sheet, SharedStrings(archive));
    }

    internal static IEnumerable<List<Dictionary<int, string?>>> ReadAll(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        string[] strings = SharedStrings(archive);
        foreach (string sheet in SheetPaths(archive).Values)
            yield return ReadSheet(archive, sheet, strings);
    }

    private static Dictionary<string, string> SheetPaths(ZipArchive archive)
    {
        XDocument workbook = Load(archive, "xl/workbook.xml");
        XDocument relationships = Load(archive, "xl/_rels/workbook.xml.rels");
        var targets = relationships.Descendants().Where(element => element.Name.LocalName == "Relationship")
            .ToDictionary(element => (string)element.Attribute("Id")!, element => (string)element.Attribute("Target")!);
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (XElement sheet in workbook.Descendants(Main + "sheet"))
        {
            string id = (string)sheet.Attribute(Relationship + "id")!;
            string target = targets[id].Replace('\\', '/').TrimStart('/');
            if (!target.StartsWith("xl/", StringComparison.Ordinal)) target = "xl/" + target;
            paths.Add((string)sheet.Attribute("name")!, target);
        }
        return paths;
    }

    private static string[] SharedStrings(ZipArchive archive)
    {
        ZipArchiveEntry? entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry == null) return Array.Empty<string>();
        using Stream stream = entry.Open();
        return XDocument.Load(stream).Descendants(Main + "si")
            .Select(item => string.Concat(item.Descendants(Main + "t").Select(text => text.Value))).ToArray();
    }

    private static List<Dictionary<int, string?>> ReadSheet(ZipArchive archive, string path, string[] strings)
    {
        XDocument document = Load(archive, path);
        var rows = new List<Dictionary<int, string?>>();
        foreach (XElement row in document.Descendants(Main + "sheetData").Elements(Main + "row"))
        {
            int number = (int)row.Attribute("r")!;
            while (rows.Count < number) rows.Add(new Dictionary<int, string?>());
            var cells = rows[number - 1];
            foreach (XElement cell in row.Elements(Main + "c"))
            {
                string address = (string)cell.Attribute("r")!;
                int column = 0;
                foreach (char character in address)
                {
                    if (!char.IsLetter(character)) break;
                    column = column * 26 + char.ToUpperInvariant(character) - 'A' + 1;
                }
                string? value = (string?)cell.Element(Main + "v");
                string? type = (string?)cell.Attribute("t");
                if (type == "s" && value != null) value = strings[int.Parse(value)];
                else if (type == "inlineStr") value = string.Concat(cell.Descendants(Main + "t").Select(text => text.Value));
                cells[column - 1] = value;
            }
        }
        return rows;
    }

    private static XDocument Load(ZipArchive archive, string path)
    {
        using Stream stream = archive.GetEntry(path)?.Open() ?? throw new InvalidOperationException($"Missing workbook entry {path}.");
        return XDocument.Load(stream);
    }
}
