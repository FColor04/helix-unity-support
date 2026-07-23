using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HelixUnitySupport
{
    public static class HelixUtils
    {
        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        public static string GetCurrentOS()
        {
#if UNITY_EDITOR_WIN
            return "Windows";
#elif UNITY_EDITOR_OSX
            return "OSX";
#else
            return "Linux";
#endif
        }

        public static string NormalizePath(string path)
        {
#if UNITY_EDITOR_WIN
            return path.Replace("/", "\\");
#else
            return path.Replace("\\", "/");
#endif
        }

        public static string MakeRelativePath(string path)
        {
            return MakeRelativePath(path, ProjectRoot);
        }

        public static string MakeRelativePath(string path, string root)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            string normalizedPath = NormalizePath(Path.GetFullPath(path));
            string normalizedRoot = NormalizePath(Path.GetFullPath(root));

            if (!normalizedRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
                normalizedRoot += Path.DirectorySeparatorChar;

            var rootUri = new Uri(normalizedRoot);
            var pathUri = new Uri(normalizedPath);
            string relativePath = Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString());
            return NormalizePath(string.IsNullOrEmpty(relativePath) ? "." : relativePath);
        }

        public static bool IsInAssetsFolder(string path)
        {
            return path.Replace('\\', '/').Contains("Assets/");
        }

        public static string GetHelixPath()
        {
            string configuredPath = Environment.GetEnvironmentVariable("HELIX_PATH");

            if (!string.IsNullOrEmpty(configuredPath))
                return File.Exists(configuredPath) ? configuredPath : null;

#if UNITY_EDITOR_WIN
            string[] fallbackPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Helix", "hx.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Links", "hx.exe")
            };
#else
            string[] fallbackPaths = new[]
            {
                "/usr/bin/hx",
                "/usr/bin/helix",
                "/usr/local/bin/hx",
                "/usr/local/bin/helix",
                "/opt/homebrew/bin/hx",
                "/opt/homebrew/bin/helix",
                "/run/current-system/sw/bin/hx",
                "/run/current-system/sw/bin/helix",
                Path.Combine("/etc/profiles/per-user", Environment.UserName, "bin/hx"),
                Path.Combine("/etc/profiles/per-user", Environment.UserName, "bin/helix"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nix-profile/bin/hx"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nix-profile/bin/helix"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cargo/bin/hx"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cargo/bin/helix"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/bin/hx"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/bin/helix"),
            };
#endif

            string fallbackPath = fallbackPaths.FirstOrDefault(File.Exists);
            if (!string.IsNullOrEmpty(fallbackPath))
                return fallbackPath;

#if UNITY_EDITOR_WIN
            return GetFullPath("hx.exe") ?? GetFullPath("helix.exe") ?? GetFullPath("hx") ?? GetFullPath("helix");
#else
            return GetFullPath("hx") ?? GetFullPath("helix");
#endif
        }

        public static bool IsHelixAvailable()
        {
            return !string.IsNullOrEmpty(GetHelixPath());
        }

        public static ProcessStartInfo BuildTerminalProcessStartInfo(string command, string arguments)
        {
#if UNITY_EDITOR_WIN
            return new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = true,
                CreateNoWindow = false
            };
#elif UNITY_EDITOR_OSX
            return CreateMacTerminalStartInfo(ProjectRoot, command, arguments);
#else
            Dictionary<string, string> terminals = HelixPreferences.GetAvailableTerminals();
            string preferredTerminal = HelixPreferences.GetPreferredTerminal();
            string quotedCommand = QuoteArgument(command);

            if (terminals.TryGetValue(preferredTerminal, out var preferredPrefix) && ExistsOnPath(preferredTerminal))
            {
                return CreateTerminalStartInfo(preferredTerminal, $"{preferredPrefix} {quotedCommand} {arguments}".Trim());
            }

            foreach (var terminal in terminals)
            {
                if (ExistsOnPath(terminal.Key))
                    return CreateTerminalStartInfo(terminal.Key, $"{terminal.Value} {quotedCommand} {arguments}".Trim());
            }

            UnityEngine.Debug.LogError("[HelixUnity] Failed to find a terminal for Helix.");
            return null;
#endif
        }

        public static ProcessStartInfo BuildOpenTerminalStartInfo(string workingDirectory)
        {
            string directory = string.IsNullOrEmpty(workingDirectory) ? ProjectRoot : workingDirectory;

            if (!Directory.Exists(directory))
            {
                UnityEngine.Debug.LogError($"[HelixUnity] Cannot open terminal because the directory does not exist: {directory}");
                return null;
            }

#if UNITY_EDITOR_WIN
            string systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string cmdPath = string.IsNullOrEmpty(systemRoot)
                ? "cmd.exe"
                : Path.Combine(systemRoot, "System32", "cmd.exe");

            return new ProcessStartInfo
            {
                FileName = File.Exists(cmdPath) ? cmdPath : "cmd.exe",
                Arguments = $"/K cd /d {QuoteWindowsCommandArgument(directory)}",
                WorkingDirectory = directory,
                UseShellExecute = true,
                CreateNoWindow = false
            };
#elif UNITY_EDITOR_OSX
            string script = $"cd {QuoteShellScriptArgument(directory)}";

            return new ProcessStartInfo
            {
                FileName = "osascript",
                Arguments = $"-e {QuoteArgument($"tell application \"Terminal\" to do script {QuoteAppleScriptString(script)}")}",
                WorkingDirectory = directory,
                UseShellExecute = true,
                CreateNoWindow = false
            };
#else
            Dictionary<string, string> terminals = HelixPreferences.GetAvailableTerminals();
            string preferredTerminal = HelixPreferences.GetPreferredTerminal();

            if (terminals.ContainsKey(preferredTerminal) && ExistsOnPath(preferredTerminal))
                return CreateTerminalStartInfo(preferredTerminal, "", directory);

            foreach (var terminal in terminals)
            {
                if (ExistsOnPath(terminal.Key))
                    return CreateTerminalStartInfo(terminal.Key, "", directory);
            }

            UnityEngine.Debug.LogError("[HelixUnity] Failed to find a terminal.");
            return null;
#endif
        }

        private static ProcessStartInfo CreateTerminalStartInfo(string terminal, string arguments)
        {
            return CreateTerminalStartInfo(terminal, arguments, ProjectRoot);
        }

        private static ProcessStartInfo CreateTerminalStartInfo(string terminal, string arguments, string workingDirectory)
        {
            return new ProcessStartInfo
            {
                FileName = terminal,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = true,
                CreateNoWindow = false
            };
        }

        private static ProcessStartInfo CreateMacTerminalStartInfo(string workingDirectory, string command, string arguments)
        {
            string script = $"cd {QuoteShellScriptArgument(workingDirectory)} && {QuoteShellScriptArgument(command)}";

            if (!string.IsNullOrWhiteSpace(arguments))
                script += $" {arguments}";

            return new ProcessStartInfo
            {
                FileName = "osascript",
                Arguments = $"-e {QuoteArgument($"tell application \"Terminal\" to do script {QuoteAppleScriptString(script)}")}",
                WorkingDirectory = workingDirectory,
                UseShellExecute = true,
                CreateNoWindow = false
            };
        }

        public static string QuoteArgument(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";

            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static string QuoteShellScriptArgument(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "''";

            return "'" + value.Replace("'", "'\\''") + "'";
        }

        private static string QuoteAppleScriptString(string value)
        {
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static string QuoteWindowsCommandArgument(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";

            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        public static bool ExistsOnPath(string fileName)
        {
            return GetFullPath(fileName) != null;
        }

        public static string GetFullPath(string fileName)
        {
            if (File.Exists(fileName))
                return Path.GetFullPath(fileName);

            string values = Environment.GetEnvironmentVariable("PATH");

            if (string.IsNullOrEmpty(values))
                return null;

            foreach (string path in values.Split(Path.PathSeparator))
            {
                string fullPath = Path.Combine(path, fileName);

                if (File.Exists(fullPath))
                    return fullPath;
            }

            return null;
        }
    }
}
