using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// Information record for a discovered TextureCocktail plugin.
    /// </summary>
    public sealed class TextureCocktailPluginInfo
    {
        /// <summary>C# class name (used to match the shader's last path segment).</summary>
        public string TypeName { get; internal set; }

        /// <summary>The actual <see cref="Type"/> of the plugin.</summary>
        public Type PluginType { get; internal set; }

        /// <summary>Human-readable display name (from attribute, or falls back to TypeName).</summary>
        public string DisplayName { get; internal set; }

        /// <summary>Plugin description (from attribute).</summary>
        public string Description { get; internal set; }

        /// <summary>Plugin author (from attribute).</summary>
        public string Author { get; internal set; }

        /// <summary>Plugin version string (from attribute).</summary>
        public string Version { get; internal set; }

        /// <summary>Assembly that defines the plugin.</summary>
        public string AssemblyName { get; internal set; }
    }

    /// <summary>
    /// Discovers and caches every <see cref="TextureCocktailContent"/> subclass found in all
    /// assemblies that are currently loaded in the AppDomain.
    ///
    /// Third-party plugins are picked up automatically — no manual registration is needed.
    /// Optionally decorate your class with <see cref="TextureCocktailPluginAttribute"/> to
    /// supply display metadata shown in the plugin browser.
    ///
    /// HOW TO CREATE A PLUGIN
    /// ───────────────────────
    /// 1. Create a shader whose last path segment is your class name, e.g.:
    ///       Shader "YourNamespace/MyEffect" { ... }
    /// 2. Create a C# class in any assembly:
    ///       [TextureCocktailPlugin("My Effect", "Does something cool", "YourName")]
    ///       public class MyEffect : TextureCocktailContent { ... }
    /// 3. TextureCocktail will load your UI automatically when the user selects the shader.
    /// </summary>
    public static class TextureCocktailPluginRegistry
    {
        private static Dictionary<string, Type> _typesByName;
        private static List<TextureCocktailPluginInfo> _infos;

        /// <summary>Mapping from class name (case-insensitive) → plugin type.</summary>
        public static IReadOnlyDictionary<string, Type> TypesByName
        {
            get
            {
                EnsureLoaded();
                return _typesByName;
            }
        }

        /// <summary>All discovered plugin info records.</summary>
        public static IReadOnlyList<TextureCocktailPluginInfo> AllPlugins
        {
            get
            {
                EnsureLoaded();
                return _infos;
            }
        }

        /// <summary>
        /// Forces a full re-scan of all loaded assemblies.
        /// Called automatically the first time the registry is accessed and on domain reload.
        /// </summary>
        [InitializeOnLoadMethod]
        public static void Refresh()
        {
            _typesByName = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            _infos = new List<TextureCocktailPluginInfo>();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                TryScanAssembly(assembly);
            }

            Debug.Log($"[TextureCocktail] Plugin registry refreshed — {_infos.Count} plugin(s) found.");
        }

        /// <summary>
        /// Creates an instance of the plugin whose class name matches <paramref name="shaderLastName"/>.
        /// Returns <c>null</c> if no matching plugin is registered.
        /// </summary>
        public static TextureCocktailContent CreatePlugin(string shaderLastName)
        {
            EnsureLoaded();
            if (_typesByName.TryGetValue(shaderLastName, out Type type))
            {
                return (TextureCocktailContent)ScriptableObject.CreateInstance(type);
            }
            return null;
        }

        // ── private ─────────────────────────────────────────────────────────────

        private static void EnsureLoaded()
        {
            if (_typesByName == null)
                Refresh();
        }

        private static void TryScanAssembly(Assembly assembly)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Partial results — still process what we got
                types = ex.Types;
            }
            catch
            {
                return;
            }

            foreach (Type type in types)
            {
                if (type == null || type.IsAbstract || !type.IsSubclassOf(typeof(TextureCocktailContent)))
                    continue;

                var attr = type.GetCustomAttribute<TextureCocktailPluginAttribute>();
                var info = new TextureCocktailPluginInfo
                {
                    TypeName = type.Name,
                    PluginType = type,
                    DisplayName = attr?.DisplayName ?? type.Name,
                    Description = attr?.Description ?? string.Empty,
                    Author = attr?.Author ?? string.Empty,
                    Version = attr?.Version ?? string.Empty,
                    AssemblyName = assembly.GetName().Name,
                };

                _typesByName[type.Name] = type;
                _infos.Add(info);
            }
        }
    }
}
