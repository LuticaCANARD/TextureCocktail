using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    public enum BlendMode
    {
        Normal,
        Multiply,
        Screen,
        Overlay,
        SoftLight,
        HardLight,
        ColorDodge,
        ColorBurn,
        Darken,
        Lighten,
        Difference,
        Exclusion
    }
    
    public class TextureBlender : TextureCocktailContent
    {
        public override bool UseDefaultLayout { get => false; }
        public override string[] DontWantDisplayPropertyName { get => new string[] { "_BlendMode", "_UseMask", "_MaskChannel" }; }
        
        private BlendMode blendMode = BlendMode.Normal;
        private bool _showBlendSettings = true;
        private bool _showUVControls = false;
        private bool _showMaskSettings = false;
        private bool _showPreview = true;
        private bool _useMask = false;
        
        private static readonly string[] _blendKeywords = new string[] 
        { 
            "_BLENDMODE_NORMAL",
            "_BLENDMODE_MULTIPLY",
            "_BLENDMODE_SCREEN",
            "_BLENDMODE_OVERLAY",
            "_BLENDMODE_SOFTLIGHT",
            "_BLENDMODE_HARDLIGHT",
            "_BLENDMODE_COLORDODGE",
            "_BLENDMODE_COLORBURN",
            "_BLENDMODE_DARKEN",
            "_BLENDMODE_LIGHTEN",
            "_BLENDMODE_DIFFERENCE",
            "_BLENDMODE_EXCLUSION"
        };
        
        public override void Initialize(TextureCocktail baseWindow)
        {
            base.Initialize(baseWindow);
        }
        
        public override void OnGUI()
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            
            // Title
            GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("texture_blender_title"), EditorStyles.boldLabel);
            GUILayout.Space(5);
            
            // Blend Mode Selection
            GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("blend_mode_selection"), EditorStyles.boldLabel);
            var newMode = (BlendMode)EditorGUILayout.EnumPopup(LanguageDisplayer.Instance.GetTranslatedLanguage("blend_mode"), blendMode);
            if (newMode != blendMode)
            {
                blendMode = newMode;
                ApplyBlendMode();
            }
            
            // Display blend mode description
            EditorGUILayout.HelpBox(GetBlendModeDescription(), MessageType.Info);
            
            GUILayout.Space(10);
            
            // Blend Settings
            _showBlendSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showBlendSettings, 
                LanguageDisplayer.Instance.GetTranslatedLanguage("blend_settings"));
            if (_showBlendSettings)
            {
                baseWindow.ShowShaderInfo();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            // UV Controls
            _showUVControls = EditorGUILayout.BeginFoldoutHeaderGroup(_showUVControls, 
                LanguageDisplayer.Instance.GetTranslatedLanguage("uv_controls"));
            if (_showUVControls)
            {
                EditorGUILayout.HelpBox(LanguageDisplayer.Instance.GetTranslatedLanguage("uv_controls_help"), MessageType.None);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            // Mask Settings
            _showMaskSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showMaskSettings, 
                LanguageDisplayer.Instance.GetTranslatedLanguage("mask_settings"));
            if (_showMaskSettings)
            {
                var newUseMask = EditorGUILayout.Toggle(LanguageDisplayer.Instance.GetTranslatedLanguage("use_mask"), _useMask);
                if (newUseMask != _useMask)
                {
                    _useMask = newUseMask;
                    var material = GetMaterial();
                    if (material != null && material.HasProperty("_UseMask"))
                    {
                        material.SetFloat("_UseMask", _useMask ? 1.0f : 0.0f);
                        baseWindow.CompileShader();
                    }
                }
                
                if (_useMask)
                {
                    EditorGUILayout.HelpBox(LanguageDisplayer.Instance.GetTranslatedLanguage("mask_help"), MessageType.Info);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            // Preview
            _showPreview = EditorGUILayout.BeginFoldoutHeaderGroup(_showPreview, 
                LanguageDisplayer.Instance.GetTranslatedLanguage("preview"));
            if (_showPreview)
            {
                baseWindow.DisplayPassedIamge();
                
                // Quick actions
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(LanguageDisplayer.Instance.GetTranslatedLanguage("apply_quick"), GUILayout.Height(30)))
                {
                    baseWindow.CompileShader();
                }
                if (GUILayout.Button(LanguageDisplayer.Instance.GetTranslatedLanguage("save_texture"), GUILayout.Height(30)))
                {
                    baseWindow.SaveTexture();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            GUILayout.EndScrollView();
        }
        
        private string GetBlendModeDescription()
        {
            switch (blendMode)
            {
                case BlendMode.Normal:
                    return LanguageDisplayer.Instance.GetTranslatedLanguage("blend_normal_desc");
                case BlendMode.Multiply:
                    return LanguageDisplayer.Instance.GetTranslatedLanguage("blend_multiply_desc");
                case BlendMode.Screen:
                    return LanguageDisplayer.Instance.GetTranslatedLanguage("blend_screen_desc");
                case BlendMode.Overlay:
                    return LanguageDisplayer.Instance.GetTranslatedLanguage("blend_overlay_desc");
                case BlendMode.SoftLight:
                    return LanguageDisplayer.Instance.GetTranslatedLanguage("blend_softlight_desc");
                case BlendMode.HardLight:
                    return LanguageDisplayer.Instance.GetTranslatedLanguage("blend_hardlight_desc");
                case BlendMode.ColorDodge:
                    return LanguageDisplayer.Instance.GetTranslatedLanguage("blend_colordodge_desc");
                case BlendMode.ColorBurn:
                    return LanguageDisplayer.Instance.GetTranslatedLanguage("blend_colorburn_desc");
                case BlendMode.Darken:
                    return LanguageDisplayer.Instance.GetTranslatedLanguage("blend_darken_desc");
                case BlendMode.Lighten:
                    return LanguageDisplayer.Instance.GetTranslatedLanguage("blend_lighten_desc");
                case BlendMode.Difference:
                    return LanguageDisplayer.Instance.GetTranslatedLanguage("blend_difference_desc");
                case BlendMode.Exclusion:
                    return LanguageDisplayer.Instance.GetTranslatedLanguage("blend_exclusion_desc");
                default:
                    return "";
            }
        }
        
        private void ApplyBlendMode()
        {
            // Clear all blend mode keywords first
            foreach (var keyword in _blendKeywords)
            {
                baseWindow.SetMaterialKeyword(keyword, false);
            }
            
            // Enable the selected blend mode keyword
            string targetKeyword = _blendKeywords[(int)blendMode];
            baseWindow.SetMaterialKeyword(targetKeyword, true);
            
            baseWindow.CompileShader();
        }
        
        private Material GetMaterial()
        {
            var type = baseWindow.GetType();
            var field = type.GetField("_calcMaterial", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(baseWindow) as Material;
        }
        
        public override void OnShaderValueChanged()
        {
            ApplyBlendMode();
        }
    }
}
