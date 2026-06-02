using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Unity.VisualStudio.Editor;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HelixUnitySupport
{
    public static class HelixProject
    {
        private const string ProjectGenerationFlagEditorPref = "unity_project_generation_flag";

        private const ProjectGenerationFlag DesiredProjectGenerationFlags =
            ProjectGenerationFlag.Embedded |
            ProjectGenerationFlag.Local |
            ProjectGenerationFlag.Registry |
            ProjectGenerationFlag.Git |
            ProjectGenerationFlag.BuiltIn |
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

                var generator = new ProjectGeneration();
                generator.Sync();
                Debug.Log("[HelixUnity] Generated Unity project files.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HelixUnity] Unity project generator failed. Project files were not modified by Helix fallback logic.\n{ex}");
                return false;
            }
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
