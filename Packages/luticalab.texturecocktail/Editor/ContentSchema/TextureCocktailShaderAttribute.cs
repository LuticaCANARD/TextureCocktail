using System;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// Marks a <see cref="TextureCocktailContent"/> subclass as the editor UI for
    /// a specific shader and registers that shader with the TextureCocktail picker.
    ///
    /// Usage:
    /// <code>
    /// [TextureCocktailShader("Hidden/MyShader", DisplayName = "My Shader", Category = "Filters")]
    /// public class MyContent : TextureCocktailContent { ... }
    /// </code>
    ///
    /// Use this for shaders that ship with code-defined UI. For data-only
    /// registration (no Content class), create a
    /// <see cref="TextureCocktailShaderPackage"/> ScriptableObject asset instead.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class TextureCocktailShaderAttribute : Attribute
    {
        /// <summary>The full Unity shader path, e.g. "Hidden/FastImageConverter".</summary>
        public string ShaderPath { get; }

        /// <summary>Optional display label. Falls back to the shader name.</summary>
        public string DisplayName { get; set; }

        /// <summary>Optional grouping label used by the picker.</summary>
        public string Category { get; set; }

        /// <summary>Optional localization key. If set, looked up via LanguageDisplayer; falls back to DisplayName.</summary>
        public string LocalizationKey { get; set; }

        /// <summary>Optional pass index used by Graphics.Blit. Defaults to 0.</summary>
        public int PassIndex { get; set; }

        /// <summary>
        /// Properties the shader must expose to be considered compatible.
        /// Defaults to ["_MainTex"]. Set to an empty array to skip property checks.
        /// </summary>
        public string[] RequiredProperties { get; set; }

        public TextureCocktailShaderAttribute(string shaderPath)
        {
            ShaderPath = shaderPath;
        }
    }
}
