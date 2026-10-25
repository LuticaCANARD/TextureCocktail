using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// Contract describing a shader exposed to the TextureCocktail picker.
    ///
    /// A "package" is the canonical way to declare that a Shader is intended to
    /// be used inside TextureCocktail. The picker builds its menu from
    /// <see cref="TextureCocktailShaderRegistry"/>, which aggregates two sources:
    ///   1. <see cref="TextureCocktailShaderPackage"/> ScriptableObject assets
    ///      anywhere in the project (asset-driven registration).
    ///   2. <see cref="TextureCocktailContent"/> subclasses tagged with
    ///      <see cref="TextureCocktailShaderAttribute"/> (code-driven registration).
    ///
    /// Implementations must:
    ///   - Return a non-null <see cref="Shader"/>.
    ///   - Expose at least the shader properties listed in
    ///     <see cref="RequiredProperties"/> (default: "_MainTex").
    ///   - Be an image-effect shader compatible with Graphics.Blit. The selected
    ///     PassIndex is used for generic UI; a custom Content's non-negative PassOrder
    ///     takes precedence. Both single-pass and multi-pass shaders are supported.
    /// </summary>
    public interface ITextureCocktailShaderPackage
    {
        Shader Shader { get; }
        string DisplayName { get; }
        string Category { get; }
        string Description { get; }
        Texture2D Icon { get; }
        int PassIndex { get; }
        string[] RequiredProperties { get; }
        bool IsCompatible(out string reason);
    }
}
