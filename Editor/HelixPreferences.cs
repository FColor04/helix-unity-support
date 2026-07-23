using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HelixUnitySupport
{
    public static class HelixPreferences
    {
        private static bool prefsLoaded;
        private static string preferredTerminal = "";

        private static readonly Dictionary<string, string> AvailableTerminals = new Dictionary<string, string>
        {
#if UNITY_EDITOR_LINUX
            ["xdg-terminal-exec"] = "",
            ["kitty"] = "",
#else
            ["xdg-terminal-exec"] = "-e",
#endif
            ["ghostty"] = "-e",
            ["alacritty"] = "-e",
            ["foot"] = "-e",
        };

        public static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider("Preferences/Helix Unity", SettingsScope.User)
            {
                label = "Helix Unity",
                guiHandler = _ =>
                {
                    if (!prefsLoaded)
                    {
                        preferredTerminal = EditorPrefs.GetString("HelixUnity_PreferredTerminal", "");
                        prefsLoaded = true;
                    }

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Terminal Settings", EditorStyles.boldLabel);

                    var options = new List<string> { "Autodetect" };
                    options.AddRange(AvailableTerminals.Keys);

                    int selectedIndex = Mathf.Max(0, options.IndexOf(string.IsNullOrEmpty(preferredTerminal) ? "Autodetect" : preferredTerminal));
                    selectedIndex = EditorGUILayout.Popup("Preferred Terminal", selectedIndex, options.ToArray());

                    string newSelection = selectedIndex == 0 ? "" : options[selectedIndex];

                    if (newSelection != preferredTerminal)
                    {
                        preferredTerminal = newSelection;
                        EditorPrefs.SetString("HelixUnity_PreferredTerminal", preferredTerminal);
                    }
                },
                keywords = new HashSet<string>(new[] { "Helix", "Terminal", "Editor" })
            };
        }

        public static string GetPreferredTerminal()
        {
            return EditorPrefs.GetString("HelixUnity_PreferredTerminal", "");
        }

        public static Dictionary<string, string> GetAvailableTerminals()
        {
            return AvailableTerminals;
        }
    }
}
