using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// Asset-based registration of a shader as a TextureCocktail-compatible package.
    /// Create via "Assets ▸ Create ▸ LuticaLab ▸ TextureCocktail ▸ Shader Package".
    ///
    /// Use this when you want to expose a Shader to the TextureCocktail picker
    /// without writing a custom <see cref="TextureCocktailContent"/>. The shader
    /// will appear in the picker and use the generic property UI.
    ///
    /// For shaders that DO have a custom content window, prefer
    /// <see cref="TextureCocktailShaderAttribute"/> on the Content class instead —
    /// the asset takes precedence if both exist.
    /// </summary>
    [CreateAssetMenu(
        menuName = "LuticaLab/TextureCocktail/Shader Package",
        fileName = "NewTextureCocktailShaderPackage",
        order = 200)]
    public sealed class TextureCocktailShaderPackage : ScriptableObject, ITextureCocktailShaderPackage
    {
        [SerializeField, Tooltip("The Shader to expose in the TextureCocktail picker.")]
        private Shader _shader;

        [SerializeField, Tooltip("Display name shown in the picker. Falls back to shader.name when empty.")]
        private string _displayName;

        [SerializeField, Tooltip("Optional localization key. Resolved via LanguageDisplayer; falls back to DisplayName.")]
        private string _localizationKey;

        [SerializeField, Tooltip("Optional category used to group entries in the picker.")]
        private string _category;

        [SerializeField, TextArea(2, 5), Tooltip("Optional description shown in tooltips/help boxes.")]
        private string _description;

        [SerializeField, Tooltip("Optional icon shown beside the entry.")]
        private Texture2D _icon;

        [SerializeField, Tooltip("Pass index used when blitting through the shader.")]
        private int _passIndex = 0;

        [SerializeField, Tooltip("Properties the shader must expose to be considered compatible. Defaults to _MainTex.")]
        private string[] _requiredProperties = new[] { "_MainTex" };

        public Shader Shader => _shader;
        public string Category => _category;
        public Texture2D Icon => _icon;
        public int PassIndex => _passIndex;
        public string[] RequiredProperties => _requiredProperties ?? System.Array.Empty<string>();

        public string DisplayName
        {
            get
            {
                string localized = TryLocalize(_localizationKey);
                if (!string.IsNullOrEmpty(localized)) return localized;
                if (!string.IsNullOrEmpty(_displayName)) return _displayName;
                return _shader != null ? _shader.name : name;
            }
        }

        public string Description
        {
            get
            {
                string localized = TryLocalize(string.IsNullOrEmpty(_localizationKey) ? null : _localizationKey + "_desc");
                return !string.IsNullOrEmpty(localized) ? localized : _description;
            }
        }

        public bool IsCompatible(out string reason)
        {
            if (_shader == null)
            {
                reason = "Shader is not assigned.";
                return false;
            }
            foreach (var prop in RequiredProperties)
            {
                if (string.IsNullOrEmpty(prop)) continue;
                if (_shader.FindPropertyIndex(prop) < 0)
                {
                    reason = $"Shader '{_shader.name}' is missing required property '{prop}'.";
                    return false;
                }
            }
            reason = null;
            return true;
        }

        private static string TryLocalize(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            var displayer = LanguageDisplayer.Instance;
            if (displayer == null) return null;
            string s = displayer.GetTranslatedLanguage(key);
            return string.IsNullOrEmpty(s) || s == key ? null : s;
        }
    }
}
