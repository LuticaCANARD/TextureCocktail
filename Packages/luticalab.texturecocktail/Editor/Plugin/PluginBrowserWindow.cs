using UiText = LuticaLab.TextureCocktail.TextureCocktailLocalization;
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
            GetWindow<PluginBrowserWindow>(UiText.Text("TC Plugin Browser"));
        }

        private Vector2 _scroll;
        private string _searchFilter = "";

        private void OnEnable()
        {
            TextureCocktailPluginRegistry.Refresh();
        }

        private void OnGUI()
        {
            titleContent.text = UiText.Text("TC Plugin Browser");
            GUILayout.Label(UiText.Text("plugin_browser_title"), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                UiText.Text("plugin_browser_desc") + "\n" + UiText.Text("plugin_browser_howto"),
                MessageType.Info);

            EditorGUILayout.Space(4);

            // Search bar
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(UiText.Text("Search:"), GUILayout.Width(55));
            _searchFilter = EditorGUILayout.TextField(_searchFilter);
            if (GUILayout.Button(UiText.Text("plugin_refresh"), GUILayout.Width(70)))
                TextureCocktailPluginRegistry.Refresh();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            IReadOnlyList<TextureCocktailPluginInfo> plugins = TextureCocktailPluginRegistry.AllPlugins;
            GUILayout.Label(UiText.Format("plugin_count", plugins.Count), EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var info in plugins)
            {
                if (!string.IsNullOrEmpty(_searchFilter) &&
                    !info.DisplayName.ToLowerInvariant().Contains(_searchFilter.ToLowerInvariant()) &&
                    !UiText.Text(info.DisplayName).ToLowerInvariant().Contains(_searchFilter.ToLowerInvariant()) &&
                    !info.TypeName.ToLowerInvariant().Contains(_searchFilter.ToLowerInvariant()))
                    continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(UiText.Text(info.DisplayName), EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (!string.IsNullOrEmpty(info.Version))
                    GUILayout.Label($"v{info.Version}", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField(UiText.Text("plugin_class") + ":", info.TypeName, EditorStyles.miniLabel);
                EditorGUILayout.LabelField(UiText.Text("plugin_assembly") + ":", info.AssemblyName, EditorStyles.miniLabel);

                if (!string.IsNullOrEmpty(info.Author))
                    EditorGUILayout.LabelField(UiText.Text("plugin_author") + ":", info.Author, EditorStyles.miniLabel);

                if (!string.IsNullOrEmpty(info.Description))
                    EditorGUILayout.LabelField(UiText.Text("plugin_description") + ":", UiText.Text(info.Description), EditorStyles.miniLabel);

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                UiText.Text("Plugin How-to:\n1. Create a class inheriting TextureCocktailContent\n2. (Optional) Add [TextureCocktailPlugin(\"Name\", \"Desc\", \"Author\")] attribute\n3. Create a shader with the same name (last path segment)\n4. TextureCocktail auto-loads your UI when the shader is selected"),
                MessageType.None);
        }
    }
}
