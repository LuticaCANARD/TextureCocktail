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
            
            // Effect-specific settings
            if (currentEffect != ArtisticEffect.None)
            {
                _showEffectSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showEffectSettings, 
                    LanguageDisplayer.Instance.GetTranslatedLanguage("effect_settings"));
                if (_showEffectSettings)
                {
                    baseWindow.ShowShaderInfo();
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }
            
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
