using System;
using System.Diagnostics;
using System.Linq;
using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;

namespace HelixUnitySupport
{
    [InitializeOnLoad]
    public class HelixEditor : IExternalCodeEditor
    {
        private const string EditorName = "Helix Code Editor";

        public static string DefaultApp => EditorPrefs.GetString("kScriptsDefaultApp");

        static HelixEditor()
        {
            // Do not register an unavailable editor. Unity queries registered external
            // editors while opening Preferences, and a placeholder path such as "hx"
            // can leave that window loading indefinitely on machines without Helix.
            if (HelixUtils.IsHelixAvailable())
                CodeEditor.Register(new HelixEditor());
        }

        public string GetDisplayName()
        {
            return EditorName;
        }

        public static bool IsDefaultEditor()
        {
            return string.Equals(DefaultApp, HelixUtils.GetHelixPath(), StringComparison.Ordinal);
        }

        public static void SetAsDefaultEditor()
        {
            string helixPath = HelixUtils.GetHelixPath();
            if (string.IsNullOrEmpty(helixPath))
            {
                UnityEngine.Debug.LogWarning("[HelixUnity] Helix was not found. Install Helix or set HELIX_PATH before selecting it as the external editor.");
                return;
            }

            EditorPrefs.SetString("kScriptsDefaultApp", helixPath);
        }

        public bool OpenProject(string path, int line, int column)
        {
            if (string.IsNullOrEmpty(path))
            {
                path = HelixUtils.ProjectRoot;
            }
            else if (!HelixProject.SupportsFile(path))
            {
                return false;
            }

            if (!IsDefaultEditor())
                return false;

            if (!HelixProject.Exists())
                SyncAll();

            if (line <= 0) line = 1;
            if (column <= 0) column = 1;

            return OpenFile(path, line, column);
        }

        public bool OpenFile(string filePath, int line, int column)
        {
            try
            {
                string helixPath = HelixUtils.GetHelixPath();
                if (string.IsNullOrEmpty(helixPath))
                {
                    UnityEngine.Debug.LogWarning("[HelixUnity] Helix was not found. Install Helix or set HELIX_PATH before opening files.");
                    return false;
                }

                string target = filePath == HelixUtils.ProjectRoot
                    ? HelixUtils.QuoteArgument(filePath)
                    : HelixUtils.QuoteArgument($"{filePath}:{line}:{column}");

                ProcessStartInfo psi = HelixUtils.GetCurrentOS() == "Windows"
                    ? new ProcessStartInfo
                    {
                        FileName = helixPath,
                        Arguments = target,
                        WorkingDirectory = HelixUtils.ProjectRoot,
                        UseShellExecute = true,
                        CreateNoWindow = false
                    }
                    : HelixUtils.BuildTerminalProcessStartInfo(helixPath, target);

                if (psi == null)
                    return false;

                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[HelixUnity] Failed to start Helix: {ex.Message}");
                return false;
            }
        }

        public void OnGUI()
        {
            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Project Files", EditorStyles.boldLabel);

            if (GUILayout.Button("Regenerate project files"))
                SyncAll();

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10);
        }

        public void Initialize(string editorInstallationPath)
        {
        }

        public CodeEditor.Installation[] Installations =>
            string.IsNullOrEmpty(HelixUtils.GetHelixPath())
                ? Array.Empty<CodeEditor.Installation>()
                : new[]
                {
                    new CodeEditor.Installation
                    {
                        Name = EditorName,
                        Path = HelixUtils.GetHelixPath()
                    }
                };

        public void SyncAll()
        {
            HelixProject.GenerateAll();
        }

        public void SyncIfNeeded(string[] addedFiles, string[] deletedFiles, string[] movedFiles, string[] movedFromFiles, string[] importedFiles)
        {
            if (!HelixProject.Exists())
            {
                SyncAll();
                return;
            }

            var fileList = addedFiles.Concat(deletedFiles).Concat(movedFiles).Concat(movedFromFiles).Concat(importedFiles);
            bool hasCsInAssets = fileList.Any(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && HelixUtils.IsInAssetsFolder(path));

            if (hasCsInAssets)
                SyncAll();
        }

        public bool TryGetInstallationForPath(string path, out CodeEditor.Installation installation)
        {
            string helixPath = HelixUtils.GetHelixPath();
            if (!string.IsNullOrEmpty(helixPath) && path == helixPath)
            {
                installation = new CodeEditor.Installation
                {
                    Name = EditorName,
                    Path = helixPath
                };
                return true;
            }

            installation = default;
            return false;
        }
    }
}
