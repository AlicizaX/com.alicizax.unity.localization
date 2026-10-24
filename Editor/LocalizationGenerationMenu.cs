using System;
using System.Diagnostics;
using System.IO;
using AlicizaX.Editor.Extension;
using AlicizaX.Localization.Runtime;
using UnityEditor;
using UnityEngine;

namespace AlicizaX.Localization.Editor
{
    public static class LocalizationGenerationMenu
    {
        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
        private static string ConfigPath => Path.GetFullPath(Path.Combine(ProjectRoot, "../Config/Excels/Localization/Localization.ini"));
        private static string PackageRoot => UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(LocalizationGenerationMenu).Assembly).resolvedPath;

        [MenuItem("AlicizaX/Localization/Initialize")]
        public static void Initialize()
        {
            Run("init");
            AssetDatabase.Refresh();
        }

        [MenuItem("AlicizaX/Config/Generate Localization")]
        [EditorToolFunction("Gen-Localization")]
        public static void Generate()
        {
            Run("generate");
            AssetDatabase.Refresh();
            LocalizationSettings.Reload();
            UnityEngine.Debug.Log("Localization generation completed.");
        }

        [EditorToolFunction("Open-LocalizationConfig")]
        public static void OpenSource() => EditorUtility.RevealInFinder(Path.GetDirectoryName(ConfigPath));

        private static void Run(string command)
        {
            string package = PackageRoot;
            string cli = Path.Combine(package, "Editor/Generation/Cli~/Localization.Cli.dll");
            if (!File.Exists(cli)) throw new FileNotFoundException("Localization CLI is missing from the package.", cli);
            static string Quote(string value) => "\"" + value.TrimEnd('\\').Replace("\"", "\\\"") + "\"";
            string arguments = Quote(cli) + " " + command + " --project " + Quote(ProjectRoot) +
                               " --package " + Quote(package) + " --config " + Quote(ConfigPath);
            var start = new ProcessStartInfo("dotnet", arguments)
            {
                WorkingDirectory = ProjectRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not start Localization CLI."))
            {
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                string message = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
                if (process.ExitCode != 0) throw new InvalidOperationException("Localization CLI failed:\n" + message);
                if (message.Length != 0) UnityEngine.Debug.Log(message);
            }
        }
    }
}
