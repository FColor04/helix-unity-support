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

        public static bool IsInAssetsFolder(string path)
        {
            return path.Replace('\\', '/').Contains("Assets/");
        }

        public static string GetHelixPath()
        {
            string configuredPath = Environment.GetEnvironmentVariable("HELIX_PATH");

            if (!string.IsNullOrEmpty(configuredPath))
                return configuredPath;

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

            return fallbackPaths.FirstOrDefault(File.Exists) ?? "hx";
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

        private static ProcessStartInfo CreateTerminalStartInfo(string terminal, string arguments)
        {
            return new ProcessStartInfo
            {
                FileName = terminal,
                Arguments = arguments,
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
