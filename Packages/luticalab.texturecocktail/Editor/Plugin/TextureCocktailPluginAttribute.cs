using System;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// Optional attribute that provides metadata for a TextureCocktail plugin.
    /// Apply this to classes that inherit from <see cref="TextureCocktailContent"/>.
    ///
    /// Usage:
    /// <code>
    /// [TextureCocktailPlugin("My Effect", "Applies a custom effect", "YourName", "1.0.0")]
    /// public class MyEffect : TextureCocktailContent { ... }
    /// </code>
    ///
    /// The plugin will be automatically discovered by <see cref="TextureCocktailPluginRegistry"/>
    /// and associated with a shader whose last path segment matches the class name (e.g.
    /// "Hidden/MyEffect" → class "MyEffect").
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class TextureCocktailPluginAttribute : Attribute
    {
        /// <summary>Human-readable name shown in the plugin browser.</summary>
        public string DisplayName { get; }

        /// <summary>Short description of what the plugin does.</summary>
        public string Description { get; }

        /// <summary>Plugin author name.</summary>
        public string Author { get; }

        /// <summary>Plugin version string.</summary>
        public string Version { get; }

        /// <param name="displayName">Human-readable plugin name.</param>
        /// <param name="description">Short description.</param>
        /// <param name="author">Author name.</param>
        /// <param name="version">Version string (e.g. "1.0.0").</param>
        public TextureCocktailPluginAttribute(
            string displayName,
            string description = "",
            string author = "",
            string version = "1.0.0")
        {
            DisplayName = displayName;
            Description = description;
            Author = author;
            Version = version;
        }
    }
}
