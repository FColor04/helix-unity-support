using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HelixUnitySupport
{
    public static class HelixProject
    {
        public static bool Exists()
        {
            string slnPath = Path.Combine(HelixUtils.ProjectRoot, $"{Path.GetFileName(HelixUtils.ProjectRoot)}.sln");
            return File.Exists(slnPath) || Directory.GetFiles(HelixUtils.ProjectRoot, "*.csproj", SearchOption.TopDirectoryOnly).Length > 0;
        }

        public static bool SupportsFile(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".cs":
                case ".uxml":
                case ".shader":
                case ".compute":
                case ".cginc":
                case ".hlsl":
                case ".glslinc":
                case ".template":
                case ".raytrace":
                    return true;
                default:
                    return false;
            }
        }

        public static void GenerateAll()
        {
            AssetDatabase.Refresh();

            if (GenerateUnityProjectFiles())
                return;

            Debug.LogWarning("[HelixUnity] Unity project generator was unavailable; falling back to existing root .csproj files.");
            EnsureSolutionIncludesAllProjects();
        }

        private static bool GenerateUnityProjectFiles()
        {
            try
            {
                var generator = new Microsoft.Unity.VisualStudio.Editor.ProjectGeneration();
                generator.Sync();
                EnsureSolutionIncludesAllProjects();
                Debug.Log("[HelixUnity] Generated Unity project files.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HelixUnity] Unity project generator failed: {ex.Message}");
                return false;
            }
        }

        private static void EnsureSolutionIncludesAllProjects()
        {
            if (!HelixUtils.ExistsOnPath("dotnet"))
            {
                Debug.LogWarning("[HelixUnity] dotnet is not on PATH, skipping solution update.");
                return;
            }

            string solutionPath = Path.Combine(HelixUtils.ProjectRoot, $"{Path.GetFileName(HelixUtils.ProjectRoot)}.sln");
            string[] projectFiles = Directory.GetFiles(HelixUtils.ProjectRoot, "*.csproj", SearchOption.TopDirectoryOnly);

            if (projectFiles.Length == 0)
                return;

            try
            {
                if (!File.Exists(solutionPath))
                    RunDotnet($"new sln --name {HelixUtils.QuoteArgument(Path.GetFileName(HelixUtils.ProjectRoot))}");

                string projectArgs = string.Join(" ", projectFiles.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).Select(HelixUtils.QuoteArgument));
                RunDotnet($"sln {HelixUtils.QuoteArgument(solutionPath)} add {projectArgs}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HelixUnity] Failed to update solution projects: {ex.Message}");
            }
        }

        private static void RunDotnet(string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
                WorkingDirectory = HelixUtils.ProjectRoot,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(psi))
            {
                process?.WaitForExit();
            }
        }
    }
}
