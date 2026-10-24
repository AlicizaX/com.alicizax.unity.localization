using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0) throw new ArgumentException("Expected init or generate.");
            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < args.Length; i += 2)
            {
                if (!args[i].StartsWith("--") || i + 1 >= args.Length) throw new ArgumentException("Expected --name value arguments.");
                options.Add(args[i][2..], args[i + 1]);
            }
            string project = Path.GetFullPath(options["project"]);
            string package = Path.GetFullPath(options["package"]);
            switch (args[0])
            {
                case "init": Initialize(project, package, options.GetValueOrDefault("config")); break;
                case "generate": LocalizationGeneration.Run(project, package, Path.GetFullPath(options["config"])); break;
                default: throw new ArgumentException("Expected init or generate.");
            }
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
    }

    private static void Initialize(string project, string package, string? config)
    {
        string source = Path.Combine(package, "Samples~", "Localization");
        if (config != null && !Path.IsPathFullyQualified(config)) throw new ArgumentException("--config must be an absolute path.");
        string destinationConfig = config == null
            ? Path.Combine(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(project))!, "Config", "Excels", "Localization", "Localization.ini")
            : Path.GetFullPath(config);
        if (Path.GetFileName(destinationConfig) != "Localization.ini") throw new ArgumentException("--config file name must be Localization.ini.");
        if (File.Exists(destinationConfig)) new LocalizationOptions(destinationConfig).RequireProject(project, destinationConfig);
        string destination = Path.GetDirectoryName(destinationConfig)!;
        Directory.CreateDirectory(destination);
        foreach (string path in Directory.GetFiles(source))
        {
            if (Path.GetFileName(path).Equals("Localization.ini", StringComparison.OrdinalIgnoreCase)) continue;
            string target = Path.Combine(destination, Path.GetFileName(path));
            if (!File.Exists(target)) File.Copy(path, target);
        }
        if (!File.Exists(destinationConfig))
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["unity_project"] = Path.GetRelativePath(destination, project).Replace('\\', '/'),
                ["source"] = ".",
                ["patch_data_out"] = Path.GetRelativePath(destination, Path.Combine(project, "Assets", "Resources", "Localization")).Replace('\\', '/'),
                ["patch_code_out"] = Path.GetRelativePath(destination, Path.Combine(project, "Assets", "Scripts", "Startup")).Replace('\\', '/')
            };
            string[] lines = File.ReadAllLines(Path.Combine(source, "Localization.ini"));
            for (int i = 0; i < lines.Length; i++)
            {
                int equals = lines[i].IndexOf('=');
                if (equals >= 0 && values.TryGetValue(lines[i][..equals].Trim(), out string? value))
                    lines[i] = lines[i][..(equals + 1)] + value;
            }
            File.WriteAllText(destinationConfig, string.Join(Environment.NewLine, lines) + Environment.NewLine, new UTF8Encoding(false));
        }
        Console.WriteLine(destinationConfig);
    }
}

internal sealed class LocalizationOptions
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _directory;

    internal LocalizationOptions(string file)
    {
        _directory = Path.GetDirectoryName(file)!;
        foreach (string line in File.ReadAllLines(file))
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed[0] is ';' or '#' or '[') continue;
            int equals = trimmed.IndexOf('=');
            if (equals < 0) throw new FormatException($"Invalid localization config line: {line}");
            _values[trimmed[..equals].Trim()] = trimmed[(equals + 1)..].Trim();
        }
    }

    internal string Get(string key) => _values.TryGetValue(key, out string? value) ? value : string.Empty;
    internal string PathFor(string key) => Get(key).Length == 0
        ? key == "source" ? _directory : string.Empty
        : Path.GetFullPath(Path.Combine(_directory, Get(key)));
    internal string[] Languages => Get("languages").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    internal void RequireProject(string project, string config)
    {
        string configuredProject = PathFor("unity_project");
        if (configuredProject.Length == 0) throw new InvalidOperationException($"unity_project is missing in {config}.");
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!Path.TrimEndingDirectorySeparator(configuredProject).Equals(Path.TrimEndingDirectorySeparator(project), comparison))
            throw new InvalidOperationException($"unity_project in {config} resolves to {configuredProject}, but --project is {project}.");
    }
}

internal sealed record TextRow(string Key, string Layer, Dictionary<string, string?> Values, bool IsConstant, string Source);

internal static class LocalizationGeneration
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    internal static void Run(string project, string package, string config)
    {
        var options = new LocalizationOptions(config);
        options.RequireProject(project, config);
        string source = options.PathFor("source");
        string luban = options.PathFor("luban_dll");
        string schema = Path.Combine(package, "Editor", "CatalogSchema.xml");
        string[] languages = options.Languages;
        if (languages.Length == 0 || languages.Distinct(StringComparer.Ordinal).Count() != languages.Length) throw new InvalidOperationException("languages must contain unique names.");
        if (!languages.Contains(options.Get("default_locale"))) throw new InvalidOperationException("default_locale is not in languages.");
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException(source);
        if (!File.Exists(luban)) throw new FileNotFoundException("Luban.dll is missing.", luban);
        if (!File.Exists(schema)) throw new FileNotFoundException("Catalog schema is missing.", schema);

        var rows = new List<TextRow>();
        foreach (string workbook in Directory.GetFiles(source, "Localization*.xlsx").OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            if (Path.GetFileName(workbook).StartsWith("~$")) continue;
            bool isConstant = Path.GetFileName(workbook).Equals("LocalizationConst.xlsx", StringComparison.OrdinalIgnoreCase);
            rows.AddRange(ReadRows(workbook, languages, isConstant));
        }
        var keys = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["base"] = new(StringComparer.Ordinal),
            ["patch"] = new(StringComparer.Ordinal)
        };
        foreach (TextRow row in rows)
            if (!keys[row.Layer].Add(row.Key)) throw new InvalidOperationException($"Duplicate {row.Layer} key {row.Key}: {row.Source}");
        Dictionary<string, string> signatures = ValidateCrossLayerFormats(rows);

        bool baseEnabled = rows.Any(row => row.Layer == "base") && options.PathFor("base_data_out").Length > 0 && options.PathFor("base_code_out").Length > 0;
        bool patchEnabled = rows.Any(row => row.Layer == "patch") && options.PathFor("patch_data_out").Length > 0 && options.PathFor("patch_code_out").Length > 0;
        if (!baseEnabled && !patchEnabled) throw new InvalidOperationException("No localization layer has rows and both output paths configured.");
        var active = rows.Where(row => row.Layer == "base" ? baseEnabled : patchEnabled).ToArray();
        ValidateOrdinaryReferences(options.PathFor("ordinary_schema"), active.Select(row => row.Key).ToHashSet(StringComparer.Ordinal));

        string resourceData = baseEnabled ? options.PathFor("base_data_out") : options.PathFor("patch_data_out");
        string resourcePrefix = ResourcePrefix(project, resourceData, baseEnabled ? "base_" : "localization_patch_");
        string patchPrefix = baseEnabled && patchEnabled ? "localization_patch_" : string.Empty;
        foreach (string layer in new[] { "base", "patch" })
            foreach (string kind in new[] { "data", "code" })
            {
                string output = options.PathFor(layer + "_" + kind + "_out");
                if (output.Length > 0) RequireAssetsPath(project, output);
            }
        string settingsPath = Path.Combine(project, "Assets", "Resources", "Localization", "Settings.json");
        string stage = Path.Combine(project, "Library", "LocalizationGeneration", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        var outputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (string layer in new[] { "base", "patch" })
            {
                if (layer == "base" && !baseEnabled || layer == "patch" && !patchEnabled) continue;
                string dataOut = options.PathFor(layer + "_data_out");
                string codeOut = options.PathFor(layer + "_code_out");
                foreach (string language in languages)
                {
                    string bin = ExportCatalog(stage, schema, luban, layer, language, active.Where(row => row.Layer == layer));
                    string fileName = (layer == "base" ? "base_" : "localization_patch_") + language + ".bytes";
                    outputs.Add(Path.Combine(dataOut, fileName), bin);
                }
                string className = layer == "base" ? "BootstrapLocalizationKey" : "LocalizationKey";
                string accessor = WriteAccessor(className, active.Where(row => row.Layer == layer && row.IsConstant), signatures, options.Get("accessor_mode"));
                string stagedAccessor = Path.Combine(stage, className + ".cs");
                File.WriteAllText(stagedAccessor, accessor, new UTF8Encoding(false));
                outputs.Add(Path.Combine(codeOut, className + ".cs"), stagedAccessor);
            }
            var settings = new { Locales = languages, DefaultLocale = options.Get("default_locale"), ResourcePrefix = resourcePrefix, PatchAddressPrefix = patchPrefix };
            string stagedSettings = Path.Combine(stage, "Settings.json");
            File.WriteAllText(stagedSettings, JsonSerializer.Serialize(settings, Json).ReplaceLineEndings("\r\n"), new UTF8Encoding(false));
            outputs.Add(settingsPath, stagedSettings);
            foreach ((string target, string staged) in outputs)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (!File.Exists(target) || !File.ReadAllBytes(target).AsSpan().SequenceEqual(File.ReadAllBytes(staged))) File.Copy(staged, target, true);
            }
            CleanupObsolete(options, languages, outputs.Keys);
            Console.WriteLine($"Generated {outputs.Count} localization files.");
        }
        finally
        {
            Directory.Delete(stage, true);
        }
    }

    private static void CleanupObsolete(LocalizationOptions options, string[] languages, IEnumerable<string> expectedFiles)
    {
        var expected = expectedFiles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string layer in new[] { "base", "patch" })
        {
            string data = options.PathFor(layer + "_data_out");
            string code = options.PathFor(layer + "_code_out");
            if (data.Length > 0 && Directory.Exists(data))
            {
                string prefix = layer == "base" ? "base_" : "localization_patch_";
                foreach (string language in languages)
                {
                    string file = Path.Combine(data, prefix + language + ".bytes");
                    if (expected.Contains(file)) continue;
                    if (File.Exists(file)) File.Delete(file);
                    string meta = file + ".meta";
                    if (File.Exists(meta)) File.Delete(meta);
                }
            }
            if (code.Length > 0)
            {
                string accessor = Path.Combine(code, layer == "base" ? "BootstrapLocalizationKey.cs" : "LocalizationKey.cs");
                if (expected.Contains(accessor)) continue;
                if (File.Exists(accessor)) File.Delete(accessor);
                if (File.Exists(accessor + ".meta")) File.Delete(accessor + ".meta");
            }
        }
    }

    private static void RequireAssetsPath(string project, string path)
    {
        string assets = Path.GetFullPath(Path.Combine(project, "Assets")) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(assets, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Localization output must be inside Assets: {path}");
    }

    private static IEnumerable<TextRow> ReadRows(string path, string[] languages, bool isConstant)
    {
        var table = Workbook.Read(path, "Texts");
        if (table.Count < 2) throw new InvalidOperationException($"Missing Texts header: {path}");
        var names = table[0];
        int keyColumn = Column(names, "key");
        int layerColumn = Column(names, "layer");
        var localeColumns = languages.ToDictionary(language => language, language => Column(names, language), StringComparer.Ordinal);
        for (int i = 2; i < table.Count; i++)
        {
            var row = table[i];
            if (Cell(row, 0)?.StartsWith("##", StringComparison.Ordinal) == true) continue;
            string key = Cell(row, keyColumn)?.Trim() ?? string.Empty;
            if (key.Length == 0) continue;
            string layer = Cell(row, layerColumn)?.Trim().ToLowerInvariant() ?? string.Empty;
            if (layer is not ("base" or "patch")) throw new InvalidOperationException($"Invalid layer {layer} for {key} in {path}:{i + 1}");
            var values = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach ((string locale, int column) in localeColumns)
            {
                string? value = Cell(row, column);
                values.Add(locale, value == "\"\"" ? string.Empty : value);
            }
            ValidateFormats(key, values, path, i + 1);
            yield return new TextRow(key, layer, values, isConstant, $"{path}:{i + 1}");
        }
    }

    private static void ValidateFormats(string key, Dictionary<string, string?> values, string path, int row)
    {
        string? signature = null;
        foreach (string? value in values.Values)
        {
            if (value == null) continue;
            string current = FormatSignature(value);
            if (signature != null && signature != current) throw new InvalidOperationException($"Format arguments differ for {key} in {path}:{row}");
            signature = current;
        }
    }

    private static Dictionary<string, string> ValidateCrossLayerFormats(IEnumerable<TextRow> rows)
    {
        var signatures = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (TextRow row in rows)
        {
            string? value = row.Values.Values.FirstOrDefault(value => value != null);
            if (value == null) continue;
            string signature = FormatSignature(value);
            if (signatures.TryGetValue(row.Key, out string? previous) && previous != signature)
                throw new InvalidOperationException($"Format arguments differ across layers for {row.Key}: {row.Source}");
            signatures[row.Key] = signature;
        }
        return signatures;
    }

    private static string FormatSignature(string value) => string.Join(',', Regex.Matches(value, @"(?<!\{)\{(\d+)(?:[^{}]*)\}(?!\})")
        .Select(match => match.Groups[1].Value).Distinct().OrderBy(index => int.Parse(index)));

    private static string ExportCatalog(string stage, string schema, string luban, string layer, string language, IEnumerable<TextRow> rows)
    {
        string working = Path.Combine(stage, layer + "_" + language);
        Directory.CreateDirectory(working);
        File.Copy(schema, Path.Combine(working, "CatalogSchema.xml"));
        var items = rows.OrderBy(row => row.Key, StringComparer.Ordinal)
            .Select(row => new { key = row.Key, value = row.Values[language] }).ToArray();
        File.WriteAllText(Path.Combine(working, "catalog.json"), JsonSerializer.Serialize(new { items }), new UTF8Encoding(false));
        const string conf = "{\"groups\":[{\"names\":[\"c\"],\"default\":true}],\"schemaFiles\":[{\"fileName\":\"CatalogSchema.xml\",\"type\":\"\"}],\"dataDir\":\".\",\"targets\":[{\"name\":\"client\",\"manager\":\"Tables\",\"groups\":[\"c\"],\"topModule\":\"Catalog\"}]}";
        string config = Path.Combine(working, "luban.conf");
        File.WriteAllText(config, conf, new UTF8Encoding(false));
        string data = Path.Combine(working, "out");
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = working, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in new[] { luban, "--conf", config, "-t", "client", "-d", "bin", "-x", "outputDataDir=" + data }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start Luban.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        string message = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
        if (process.ExitCode != 0) throw new InvalidOperationException($"Luban failed for {layer}/{language}: {message}");
        string result = Path.Combine(data, "catalog_tbtext.bytes");
        if (!File.Exists(result)) throw new FileNotFoundException($"Luban did not produce catalog_tbtext.bytes: {message}", result);
        return result;
    }

    private static string WriteAccessor(string className, IEnumerable<TextRow> rows, Dictionary<string, string> signatures, string mode)
    {
        if (mode.Length == 0) mode = "resolved";
        if (mode is not ("resolved" or "raw")) throw new InvalidOperationException("accessor_mode must be resolved or raw.");
        var grouped = rows.OrderBy(row => row.Key, StringComparer.Ordinal)
            .GroupBy(row => row.Key.Contains('.') ? row.Key[..row.Key.IndexOf('.')] : "Common", StringComparer.Ordinal);
        var memberKeys = new HashSet<string>(StringComparer.Ordinal);
        var classKeys = new HashSet<string>(StringComparer.Ordinal);
        var output = new StringBuilder();
        if (mode == "resolved") output.AppendLine("using AlicizaX.Localization.Runtime;").AppendLine();
        output.Append("public static class ").AppendLine(className).AppendLine("{");
        foreach (var group in grouped)
        {
            string groupName = Identifier(group.Key);
            if (!classKeys.Add(groupName)) throw new InvalidOperationException($"Constant class name collision: {group.Key}");
            output.Append("    public static class ").Append(groupName).AppendLine().AppendLine("    {");
            foreach (TextRow row in group)
            {
                string remainder = row.Key.Contains('.') ? row.Key[(row.Key.IndexOf('.') + 1)..] : row.Key;
                string name = Identifier(remainder.Replace('.', '_')).ToUpperInvariant();
                if (!memberKeys.Add(groupName + "." + name)) throw new InvalidOperationException($"Constant member name collision: {row.Key}");
                int count = signatures.TryGetValue(row.Key, out string? signature) && signature.Length > 0
                    ? signature.Split(',').Select(int.Parse).Max() + 1 : 0;
                if (mode == "raw")
                {
                    output.Append("        public const string ").Append(name).Append(" = ").Append(JsonSerializer.Serialize(row.Key)).AppendLine(";");
                    continue;
                }
                output.Append("        public const string ").Append(name).Append("_Raw = ").Append(JsonSerializer.Serialize(row.Key)).AppendLine(";");
                if (count == 0) output.Append("        public static string ").Append(name).Append(" => LocalizationKit.Get(").Append(name).AppendLine("_Raw);");
                else
                {
                    output.Append("        public static string ").Append(name).Append('<').Append(string.Join(", ", Enumerable.Range(0, count).Select(i => "T" + i))).Append(">(");
                    output.Append(string.Join(", ", Enumerable.Range(0, count).Select(i => "T" + i + " arg" + i))).Append(") => LocalizationKit.Format(").Append(name).Append("_Raw, ").Append(string.Join(", ", Enumerable.Range(0, count).Select(i => "arg" + i))).AppendLine(");");
                }
            }
            output.AppendLine("    }");
        }
        output.AppendLine("}");
        return output.ToString().ReplaceLineEndings("\n");
    }

    private static string Identifier(string source)
    {
        var result = new StringBuilder();
        foreach (char character in source) result.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        if (result.Length == 0 || char.IsDigit(result[0])) result.Insert(0, '_');
        return result.ToString();
    }

    private static void ValidateOrdinaryReferences(string config, HashSet<string> keys)
    {
        if (config.Length == 0) return;
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(config));
        string dataDir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(config)!, schema.RootElement.GetProperty("dataDir").GetString()!));
        foreach (string workbook in Directory.GetFiles(dataDir, "*.xlsx", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(workbook).StartsWith("~$")) continue;
            foreach (var sheet in Workbook.ReadAll(workbook))
            {
                int header = sheet.FindIndex(row => Cell(row, 0)?.StartsWith("##var", StringComparison.Ordinal) == true);
                int type = sheet.FindIndex(row => Cell(row, 0) == "##type");
                if (header < 0 || type < 0 || type <= header) continue;
                var textColumns = sheet[type].Where(cell => cell.Value == "text").Select(cell => cell.Key).ToArray();
                for (int row = type + 1; row < sheet.Count; row++)
                {
                    if (Cell(sheet[row], 0)?.StartsWith("##", StringComparison.Ordinal) == true) continue;
                    foreach (int column in textColumns)
                    {
                        string? key = Cell(sheet[row], column);
                        if (key != null && key.Length > 0 && !key.StartsWith("##") && !keys.Contains(key))
                            throw new InvalidOperationException($"Unknown ordinary text key {key}: {workbook}:{row + 1}");
                    }
                }
            }
        }
    }

    private static string ResourcePrefix(string project, string dataDir, string filePrefix)
    {
        string assets = Path.Combine(project, "Assets") + Path.DirectorySeparatorChar;
        if (!dataDir.StartsWith(assets, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Initial catalog output must be inside Assets/Resources.");
        string relative = Path.GetRelativePath(assets, dataDir).Replace('\\', '/');
        int resources = relative.IndexOf("Resources/", StringComparison.OrdinalIgnoreCase);
        if (resources < 0 && !relative.Equals("Resources", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Initial catalog output must be inside Resources.");
        string prefix = resources < 0 ? "" : relative[(resources + "Resources/".Length)..].TrimEnd('/') + "/";
        return prefix + filePrefix;
    }

    private static int Column(Dictionary<int, string?> header, string name) =>
        header.FirstOrDefault(cell => cell.Value == name).Value == null
            ? throw new InvalidOperationException($"Missing {name} column.")
            : header.First(cell => cell.Value == name).Key;

    private static string? Cell(Dictionary<int, string?> row, int column) => row.TryGetValue(column, out string? value) ? value : null;
}
