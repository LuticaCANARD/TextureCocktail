using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    public enum ArtisticEffect
    {
        None,
        Pixelate,
        Posterize,
        Halftone,
        OilPaint,
        Emboss,
        Cartoon
    }
    
    public class ArtisticEffects : TextureCocktailContent
    {
        public override bool UseDefaultLayout { get => false; }
        public override string[] DontWantDisplayPropertyName { get => new string[] { }; }
        
        private ArtisticEffect currentEffect = ArtisticEffect.None;
        private bool _showEffectSettings = true;
        private bool _showPreview = true;
        
        private static readonly string[] _effectKeywords = new string[] 
        { 
            "",  // None
            "PIXELATE",
            "POSTERIZE",
            "HALFTONE",
            "OILPAINT",
            "EMBOSS",
            "CARTOON"
        };
        
        public override void Initialize(TextureCocktail baseWindow)
        {
            base.Initialize(baseWindow);
        }
        
        public override void OnGUI()
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            
            // Title
            GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("artistic_effects_title"), EditorStyles.boldLabel);
            GUILayout.Space(5);
            
            // Effect Selection
            GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("effect_selection"), EditorStyles.boldLabel);
            var newEffect = (ArtisticEffect)EditorGUILayout.EnumPopup(LanguageDisplayer.Instance.GetTranslatedLanguage("effect"), currentEffect);
            if (newEffect != currentEffect)
            {
                currentEffect = newEffect;
                ApplyEffect();
            }
            
            // Display effect description
            EditorGUILayout.HelpBox(GetEffectDescription(), MessageType.Info);
            
            GUILayout.Space(10);
            
            // Effect-specific settings - show only relevant parameters
            if (currentEffect != ArtisticEffect.None)
            {
                _showEffectSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showEffectSettings, 
                    LanguageDisplayer.Instance.GetTranslatedLanguage("effect_settings"));
                if (_showEffectSettings)
                {
                    ShowEffectSpecificParameters();
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }
            
            // Warning for Oil Paint
            if (currentEffect == ArtisticEffect.OilPaint)
            {
                EditorGUILayout.HelpBox(LanguageDisplayer.Instance.GetTranslatedLanguage("oilpaint_warning"), MessageType.Warning);
                
                if (GUILayout.Button(LanguageDisplayer.Instance.GetTranslatedLanguage("apply_oilpaint"), GUILayout.Height(35)))
                {
                    baseWindow.CompileShader();
                }
            }
            
            // Preview
            _showPreview = EditorGUILayout.BeginFoldoutHeaderGroup(_showPreview, 
                LanguageDisplayer.Instance.GetTranslatedLanguage("preview"));
            if (_showPreview)
            {
                baseWindow.DisplayPassedIamge();
                
                // Quick actions
                EditorGUILayout.BeginHorizontal();
                if (currentEffect != ArtisticEffect.OilPaint)
                {
                    if (GUILayout.Button(LanguageDisplayer.Instance.GetTranslatedLanguage("apply_quick"), GUILayout.Height(30)))
                    {
                        baseWindow.CompileShader();
                    }
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
        
        private void ShowEffectSpecificParameters()
        {
            var material = GetMaterial();
            if (material == null) return;
            
            EditorGUI.BeginChangeCheck();
            
            switch (currentEffect)
            {
                case ArtisticEffect.Pixelate:
                    if (material.HasProperty("_PixelSize"))
                    {
                        float pixelSize = material.GetFloat("_PixelSize");
                        pixelSize = EditorGUILayout.Slider("Pixel Size", pixelSize, 1, 100);
                        material.SetFloat("_PixelSize", pixelSize);
                    }
                    if (material.HasProperty("_PixelCenterX"))
                    {
                        float centerX = material.GetFloat("_PixelCenterX");
                        centerX = EditorGUILayout.Slider("Center X", centerX, 0, 1);
                        material.SetFloat("_PixelCenterX", centerX);
                    }
                    if (material.HasProperty("_PixelCenterY"))
                    {
                        float centerY = material.GetFloat("_PixelCenterY");
                        centerY = EditorGUILayout.Slider("Center Y", centerY, 0, 1);
                        material.SetFloat("_PixelCenterY", centerY);
                    }
                    break;
                    
                case ArtisticEffect.Posterize:
                    if (material.HasProperty("_ColorLevels"))
                    {
                        float colorLevels = material.GetFloat("_ColorLevels");
                        colorLevels = EditorGUILayout.Slider("Color Levels", colorLevels, 2, 256);
                        material.SetFloat("_ColorLevels", colorLevels);
                    }
                    break;
                    
                case ArtisticEffect.Halftone:
                    if (material.HasProperty("_DotSize"))
                    {
                        float dotSize = material.GetFloat("_DotSize");
                        dotSize = EditorGUILayout.Slider("Dot Size", dotSize, 1, 20);
                        material.SetFloat("_DotSize", dotSize);
                    }
                    if (material.HasProperty("_DotAngle"))
                    {
                        float dotAngle = material.GetFloat("_DotAngle");
                        dotAngle = EditorGUILayout.Slider("Dot Angle", dotAngle, 0, 360);
                        material.SetFloat("_DotAngle", dotAngle);
                    }
                    break;
                    
                case ArtisticEffect.OilPaint:
                    if (material.HasProperty("_Radius"))
                    {
                        float radius = material.GetFloat("_Radius");
                        radius = EditorGUILayout.Slider("Oil Paint Radius", radius, 1, 10);
                        material.SetFloat("_Radius", radius);
                    }
                    break;
                    
                case ArtisticEffect.Emboss:
                    if (material.HasProperty("_EmbossStrength"))
                    {
                        float embossStrength = material.GetFloat("_EmbossStrength");
                        embossStrength = EditorGUILayout.Slider("Emboss Strength", embossStrength, 0, 5);
                        material.SetFloat("_EmbossStrength", embossStrength);
                    }
                    break;
                    
                case ArtisticEffect.Cartoon:
                    if (material.HasProperty("_EdgeThreshold"))
                    {
                        float edgeThreshold = material.GetFloat("_EdgeThreshold");
                        edgeThreshold = EditorGUILayout.Slider("Edge Threshold", edgeThreshold, 0, 1);
                        material.SetFloat("_EdgeThreshold", edgeThreshold);
                    }
                    if (material.HasProperty("_ColorSteps"))
                    {
                        float colorSteps = material.GetFloat("_ColorSteps");
                        colorSteps = EditorGUILayout.Slider("Color Steps", colorSteps, 2, 10);
                        material.SetFloat("_ColorSteps", colorSteps);
                    }
                    break;
            }
            
            if (EditorGUI.EndChangeCheck())
            {
                // Only auto-compile for effects other than Oil Paint
                if (currentEffect != ArtisticEffect.OilPaint)
                {
                    baseWindow.CompileShader();
                }
            }
        }
        
        private string GetEffectDescription()
        {
            switch (currentEffect)
            {
                case ArtisticEffect.None:
                    return LanguageDisplayer.Instance.GetTranslatedLanguage("effect_none_desc");
                case ArtisticEffect.Pixelate:
                    return LanguageDisplayer.Instance.GetTranslatedLanguage("effect_pixelate_desc");
                case ArtisticEffect.Posterize:
                    return LanguageDisplayer.Instance.GetTranslatedLanguage("effect_posterize_desc");
                case ArtisticEffect.Halftone:
                    return LanguageDisplayer.Instance.GetTranslatedLanguage("effect_halftone_desc");
                case ArtisticEffect.OilPaint:
                    return LanguageDisplayer.Instance.GetTranslatedLanguage("effect_oilpaint_desc");
                case ArtisticEffect.Emboss:
                    return LanguageDisplayer.Instance.GetTranslatedLanguage("effect_emboss_desc");
                case ArtisticEffect.Cartoon:
                    return LanguageDisplayer.Instance.GetTranslatedLanguage("effect_cartoon_desc");
                default:
                    return "";
            }
        }
        
        private void ApplyEffect()
        {
            // Clear all effect keywords first
            foreach (var keyword in _effectKeywords)
            {
                if (!string.IsNullOrEmpty(keyword))
                {
                    baseWindow.SetMaterialKeyword(keyword, false);
                }
            }
            
            // Enable the selected effect keyword
            string targetKeyword = _effectKeywords[(int)currentEffect];
            if (!string.IsNullOrEmpty(targetKeyword))
            {
                baseWindow.SetMaterialKeyword(targetKeyword, true);
            }
            
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
            ApplyEffect();
        }
    }
}
