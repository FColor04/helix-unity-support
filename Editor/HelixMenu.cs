using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Unity.CodeEditor;
using UnityEditor;

namespace HelixUnitySupport
{
    public static class HelixMenu
    {
        [MenuItem("Tools/Helix Code Editor/Set as Unity External Editor")]
        public static void SetAsDefaultEditor()
        {
            HelixEditor.SetAsDefaultEditor();
        }

        [MenuItem("Tools/Helix Code Editor/Regenerate Project Files")]
        public static void RegenerateProjectFiles()
        {
            HelixEditor.RegisterIfAvailable();

            if (CodeEditor.CurrentEditor is HelixEditor editor)
                editor.SyncAll();
            else
                HelixProject.GenerateAll();
        }

        [MenuItem("Assets/Open in Terminal", false, 2000)]
        public static void OpenSelectedPathInTerminal()
        {
            string directory = GetSelectedProjectViewDirectory();
            ProcessStartInfo processStartInfo = HelixUtils.BuildOpenTerminalStartInfo(directory);

            if (processStartInfo == null)
                return;

            try
            {
                Process.Start(processStartInfo);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[HelixUnity] Failed to open terminal: {ex.Message}");
            }
        }

        [MenuItem("Assets/Open in Terminal", true)]
        public static bool ValidateOpenSelectedPathInTerminal()
        {
            return true;
        }

        private static string GetSelectedProjectViewDirectory()
        {
            string assetPath = Selection.assetGUIDs
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(path => !string.IsNullOrEmpty(path));

            if (string.IsNullOrEmpty(assetPath) && Selection.activeObject != null)
                assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);

            if (string.IsNullOrEmpty(assetPath))
                return HelixUtils.ProjectRoot;

            string fullPath = GetFullPathForAssetPath(assetPath);

            if (Directory.Exists(fullPath))
                return fullPath;

            if (File.Exists(fullPath))
                return Path.GetDirectoryName(fullPath);

            return HelixUtils.ProjectRoot;
        }

        private static string GetFullPathForAssetPath(string assetPath)
        {
            if (assetPath.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                UnityEditor.PackageManager.PackageInfo packageInfo =
                    UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);

                if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.resolvedPath))
                {
                    string relativePath = assetPath.Length > packageInfo.assetPath.Length
                        ? assetPath.Substring(packageInfo.assetPath.Length).TrimStart('/', '\\')
                        : "";

                    return Path.GetFullPath(Path.Combine(packageInfo.resolvedPath, relativePath));
                }
            }

            return Path.GetFullPath(Path.Combine(HelixUtils.ProjectRoot, assetPath));
        }
    }
}
