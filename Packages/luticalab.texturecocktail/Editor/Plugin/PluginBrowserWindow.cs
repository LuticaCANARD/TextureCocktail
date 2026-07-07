using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// Editor window that lists all TextureCocktail plugins discovered in loaded assemblies.
    /// Open via: LuticaLab → TextureCocktail Plugin Browser
    /// </summary>
    public class PluginBrowserWindow : EditorWindow
    {
        [MenuItem("LuticaLab/TextureCocktail Plugin Browser")]
        public static void ShowWindow()
        {
            GetWindow<PluginBrowserWindow>("TC Plugin Browser");
        }

        private Vector2 _scroll;
        private string _searchFilter = "";

        private void OnEnable()
        {
            TextureCocktailPluginRegistry.Refresh();
        }

        private void OnGUI()
        {
            GUILayout.Label("TextureCocktail Plugin Browser", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "All TextureCocktailContent subclasses found in loaded assemblies are listed here.\n" +
                "To create a plugin: inherit from TextureCocktailContent and create a matching shader.",
                MessageType.Info);

            EditorGUILayout.Space(4);

            // Search bar
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Search:", GUILayout.Width(55));
            _searchFilter = EditorGUILayout.TextField(_searchFilter);
            if (GUILayout.Button("Refresh", GUILayout.Width(70)))
                TextureCocktailPluginRegistry.Refresh();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            IReadOnlyList<TextureCocktailPluginInfo> plugins = TextureCocktailPluginRegistry.AllPlugins;
            GUILayout.Label($"Registered Plugins ({plugins.Count})", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var info in plugins)
            {
                if (!string.IsNullOrEmpty(_searchFilter) &&
                    !info.DisplayName.ToLowerInvariant().Contains(_searchFilter.ToLowerInvariant()) &&
                    !info.TypeName.ToLowerInvariant().Contains(_searchFilter.ToLowerInvariant()))
                    continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(info.DisplayName, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (!string.IsNullOrEmpty(info.Version))
                    GUILayout.Label($"v{info.Version}", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField("Class:", info.TypeName, EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Assembly:", info.AssemblyName, EditorStyles.miniLabel);

                if (!string.IsNullOrEmpty(info.Author))
                    EditorGUILayout.LabelField("Author:", info.Author, EditorStyles.miniLabel);

                if (!string.IsNullOrEmpty(info.Description))
                    EditorGUILayout.LabelField("Description:", info.Description, EditorStyles.miniLabel);

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Plugin How-to:\n" +
                "1. Create a class inheriting TextureCocktailContent\n" +
                "2. (Optional) Add [TextureCocktailPlugin(\"Name\", \"Desc\", \"Author\")] attribute\n" +
                "3. Create a shader with the same name (last path segment)\n" +
                "4. TextureCocktail auto-loads your UI when the shader is selected",
                MessageType.None);
        }
    }
}
