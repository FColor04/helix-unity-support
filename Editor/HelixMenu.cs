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
            if (CodeEditor.CurrentEditor is HelixEditor editor)
                editor.SyncAll();
            else
                HelixProject.GenerateAll();
        }
    }
}
