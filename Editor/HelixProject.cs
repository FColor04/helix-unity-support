using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Unity.VisualStudio.Editor;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HelixUnitySupport
{
    public static class HelixProject
    {
        private const string ProjectGenerationFlagEditorPref = "unity_project_generation_flag";
        private const string HelixDirectoryName = ".helix";
        private const string LspRootDirectoryName = "lsp-root";
        private const string HelixSolutionFileName = "BallGame.sln";
        private const string CSharpProjectTypeGuid = "FAE04EC0-301F-11D3-BF4B-00C04F79EFBC";

        private const ProjectGenerationFlag DesiredProjectGenerationFlags =
            ProjectGenerationFlag.Embedded |
            ProjectGenerationFlag.Local |
            ProjectGenerationFlag.Git |
            ProjectGenerationFlag.Unknown |
            ProjectGenerationFlag.LocalTarBall;

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
            if (TryFindMetaFileWithMergeConflict(out string metaFile))
            {
                Debug.LogError($"[HelixUnity] Cannot generate project files while a Unity .meta file contains merge conflict markers: {metaFile}");
                return;
            }

            AssetDatabase.Refresh();

            GenerateUnityProjectFiles();
        }

        private static bool GenerateUnityProjectFiles()
        {
            try
            {
                ConfigureProjectGenerationFlags();

                IGenerator generator = CreateUnityProjectGenerator();
                generator.Sync();
                GenerateHelixLspFiles();
                Debug.Log("[HelixUnity] Generated Unity project files.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HelixUnity] Unity project generator failed. Project files were not modified by Helix fallback logic.\n{ex}");
                return false;
            }
        }

        private static IGenerator CreateUnityProjectGenerator()
        {
            Assembly assembly = typeof(ProjectGeneration).Assembly;
            Type generatorStyleType = assembly.GetType("Microsoft.Unity.VisualStudio.Editor.GeneratorStyle", true);
            Type generatorFactoryType = assembly.GetType("Microsoft.Unity.VisualStudio.Editor.GeneratorFactory", true);
            MethodInfo getInstance = generatorFactoryType.GetMethod("GetInstance", BindingFlags.Public | BindingFlags.Static);

            if (getInstance == null)
                throw new MissingMethodException(generatorFactoryType.FullName, "GetInstance");

            object sdkStyle = Enum.ToObject(generatorStyleType, 1);
            return (IGenerator)getInstance.Invoke(null, new[] { sdkStyle });
        }

        private static void ConfigureProjectGenerationFlags()
        {
            ProjectGenerationFlag currentFlags = (ProjectGenerationFlag)EditorPrefs.GetInt(
                ProjectGenerationFlagEditorPref,
                (int)(ProjectGenerationFlag.Local | ProjectGenerationFlag.Embedded));

            ProjectGenerationFlag nextFlags = currentFlags | DesiredProjectGenerationFlags;
            if (nextFlags == currentFlags)
                return;

            EditorPrefs.SetInt(ProjectGenerationFlagEditorPref, (int)nextFlags);
            Debug.Log($"[HelixUnity] Enabled Unity project generation for package sources: {nextFlags}");
        }

        private static void GenerateHelixLspFiles()
        {
            string helixDirectory = Path.Combine(HelixUtils.ProjectRoot, HelixDirectoryName);
            string lspRoot = Path.Combine(helixDirectory, LspRootDirectoryName);
            Directory.CreateDirectory(lspRoot);

            string[] projectFiles = Directory.GetFiles(HelixUtils.ProjectRoot, "*.csproj", SearchOption.TopDirectoryOnly)
                .Where(ShouldIncludeInFastSolution)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            WriteSolution(Path.Combine(lspRoot, HelixSolutionFileName), projectFiles);
            WriteCSharpLsWrapper(Path.Combine(helixDirectory, "csharp-ls-unity"));
            WriteLanguagesToml(Path.Combine(helixDirectory, "languages.toml"), Path.Combine(helixDirectory, "csharp-ls-unity"));

            Debug.Log($"[HelixUnity] Generated isolated Helix C# LSP solution with {projectFiles.Length} projects.");
        }

        private static void WriteSolution(string solutionPath, string[] projectFiles)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
            builder.AppendLine("# Visual Studio Version 17");
            builder.AppendLine("VisualStudioVersion = 17.0.31903.59");
            builder.AppendLine("MinimumVisualStudioVersion = 10.0.40219.1");

            foreach (string projectFile in projectFiles)
            {
                string projectName = Path.GetFileNameWithoutExtension(projectFile);
                string relativePath = HelixUtils.MakeRelativePath(projectFile, Path.GetDirectoryName(solutionPath));
                builder.AppendLine($"Project(\"{{{CSharpProjectTypeGuid}}}\") = \"{projectName}\", \"{relativePath}\", \"{{{ProjectGuid(relativePath)}}}\"");
                builder.AppendLine("EndProject");
            }

            builder.AppendLine("Global");
            builder.AppendLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
            builder.AppendLine("\t\tDebug|Any CPU = Debug|Any CPU");
            builder.AppendLine("\tEndGlobalSection");
            builder.AppendLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");

            foreach (string projectFile in projectFiles)
            {
                string relativePath = HelixUtils.MakeRelativePath(projectFile, Path.GetDirectoryName(solutionPath));
                builder.AppendLine($"\t\t{{{ProjectGuid(relativePath)}}}.Debug|Any CPU.ActiveCfg = Debug|Any CPU");
            }

            builder.AppendLine("\tEndGlobalSection");
            builder.AppendLine("EndGlobal");

            File.WriteAllText(solutionPath, builder.ToString());
        }

        private static void WriteCSharpLsWrapper(string wrapperPath)
        {
            string script = @"#!/usr/bin/env bash
set -e

script_dir=""$(cd ""$(dirname ""${BASH_SOURCE[0]}"")"" && pwd)""
server=""${CSHARP_LS:-}""

if [ -z ""$server"" ]; then
  if command -v csharp-ls >/dev/null 2>&1; then
    server=""$(command -v csharp-ls)""
  else
    server=""$HOME/.dotnet/tools/csharp-ls""
  fi
fi

cd ""$script_dir/lsp-root""
exec ""$server"" --solution """ + HelixSolutionFileName + @""" --loglevel warning ""$@""
";

            File.WriteAllText(wrapperPath, script);
        }

        private static void WriteLanguagesToml(string languagesPath, string wrapperPath)
        {
            string normalizedWrapperPath = HelixUtils.NormalizePath(wrapperPath);
            string text = @"# Generated by Helix Unity Support.
[language-server.csharp-ls-unity]
command = ""/usr/bin/bash""
args = [""" + normalizedWrapperPath + @"""]
timeout = 180

[[language]]
name = ""c-sharp""
language-servers = [""csharp-ls-unity""]
roots = [""Assets"", ""Packages"", ""ProjectSettings"", "".helix/lsp-root/" + HelixSolutionFileName + @"""]
";

            File.WriteAllText(languagesPath, text);
        }

        private static bool ShouldIncludeInFastSolution(string projectFile)
        {
            string projectName = Path.GetFileNameWithoutExtension(projectFile);
            if (projectName.StartsWith("Unity.", StringComparison.OrdinalIgnoreCase) ||
                projectName.StartsWith("UnityEngine.", StringComparison.OrdinalIgnoreCase) ||
                projectName.StartsWith("UnityEditor.", StringComparison.OrdinalIgnoreCase) ||
                projectName.StartsWith("com.unity.", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (projectName.StartsWith("Assembly-CSharp", StringComparison.OrdinalIgnoreCase))
                return true;

            string text = File.ReadAllText(projectFile);
            foreach (string compilePath in ExtractCompileIncludePaths(text))
            {
                string normalizedPath = compilePath.Replace('\\', '/');
                if (normalizedPath.StartsWith("Assets/Scripts/", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string ProjectGuid(string value)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(value.ToLowerInvariant()));
                return new Guid(hash).ToString().ToUpperInvariant();
            }
        }

        private static IEnumerable<string> ExtractCompileIncludePaths(string projectText)
        {
            const string marker = "<Compile Include=\"";
            int index = 0;

            while ((index = projectText.IndexOf(marker, index, StringComparison.Ordinal)) >= 0)
            {
                int start = index + marker.Length;
                int end = projectText.IndexOf('"', start);
                if (end < 0)
                    yield break;

                yield return projectText.Substring(start, end - start);
                index = end + 1;
            }
        }

        private static bool TryFindMetaFileWithMergeConflict(out string metaFile)
        {
            foreach (string root in ExistingProjectRoots())
            {
                foreach (string path in Directory.EnumerateFiles(root, "*.meta", SearchOption.AllDirectories))
                {
                    string text = File.ReadAllText(path);
                    if (!text.Contains("<<<<<<<") && !text.Contains("=======") && !text.Contains(">>>>>>>"))
                        continue;

                    metaFile = HelixUtils.MakeRelativePath(path);
                    return true;
                }
            }

            metaFile = null;
            return false;
        }

        private static IEnumerable<string> ExistingProjectRoots()
        {
            string assetsPath = Path.Combine(HelixUtils.ProjectRoot, "Assets");
            if (Directory.Exists(assetsPath))
                yield return assetsPath;

            string packagesPath = Path.Combine(HelixUtils.ProjectRoot, "Packages");
            if (Directory.Exists(packagesPath))
                yield return packagesPath;
        }
    }
}
