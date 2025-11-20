using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    public enum ColorGradingMode
    {
        Basic,
        ColorGrading,
        SplitToning
    }
    
    public class ColorCorrection : TextureCocktailContent
    {
        public override bool UseDefaultLayout { get => false; }
        public override string[] DontWantDisplayPropertyName { get => new string[] { }; }
        
        private ColorGradingMode gradingMode = ColorGradingMode.Basic;
        private bool _showBasicSettings = true;
        private bool _showAdvancedSettings = false;
        private bool _showPreview = true;
        
        private static readonly string[] _modeKeywords = new string[] 
        { 
            "",  // Basic
            "COLOR_GRADING",
            "SPLIT_TONING"
        };
        
        // Presets
        private readonly Dictionary<string, System.Action> _presets = new Dictionary<string, System.Action>();
        
        public override void Initialize(TextureCocktail baseWindow)
        {
            base.Initialize(baseWindow);
            InitializePresets();
        }
        
        private void InitializePresets()
        {
            _presets["Reset"] = () => ApplyPreset(0, 0, 0, Color.white, Color.white, Color.white);
            _presets["Warm"] = () => ApplyPreset(0.3f, 0.1f, 0.2f, Color.white, Color.white, Color.white);
            _presets["Cool"] = () => ApplyPreset(-0.3f, -0.1f, 0.2f, Color.white, Color.white, Color.white);
            _presets["Vintage"] = () => ApplyPreset(0.2f, 0.2f, -0.3f, new Color(0.9f, 0.85f, 0.7f), Color.white, new Color(1.1f, 1.0f, 0.9f));
            _presets["Cinematic"] = () => ApplyPreset(0, -0.1f, 0.5f, new Color(0.95f, 0.95f, 1.0f), Color.white, new Color(1.0f, 0.95f, 0.9f));
            _presets["High Key"] = () => ApplyPreset(0, 0, 1.0f, new Color(1.1f, 1.1f, 1.1f), Color.white, Color.white);
            _presets["Low Key"] = () => ApplyPreset(0, 0, -0.8f, new Color(0.9f, 0.9f, 0.9f), Color.white, Color.white);
        }
        
        public override void OnGUI()
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            
            // Title
            GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("color_correction_title"), EditorStyles.boldLabel);
            GUILayout.Space(5);
            
            // Quick Presets
            GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("quick_presets"), EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            foreach (var preset in _presets.Keys)
            {
                if (GUILayout.Button(preset, GUILayout.Height(25)))
                {
                    _presets[preset]();
                }
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10);
            
            // Mode Selection
            GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("color_grading_mode"), EditorStyles.boldLabel);
            var newMode = (ColorGradingMode)EditorGUILayout.EnumPopup(LanguageDisplayer.Instance.GetTranslatedLanguage("mode"), gradingMode);
            if (newMode != gradingMode)
            {
                gradingMode = newMode;
                ApplyGradingMode();
            }
            
            // Display mode description
            EditorGUILayout.HelpBox(GetModeDescription(), MessageType.Info);
            
            GUILayout.Space(10);
            
            // Basic Settings
            _showBasicSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showBasicSettings, 
                LanguageDisplayer.Instance.GetTranslatedLanguage("basic_color_settings"));
            if (_showBasicSettings)
            {
                baseWindow.ShowShaderInfo();
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
        
        private string GetModeDescription()
        {
            switch (gradingMode)
            {
                case ColorGradingMode.Basic:
                    return LanguageDisplayer.Instance.GetTranslatedLanguage("color_grading_basic_desc");
                case ColorGradingMode.ColorGrading:
                    return LanguageDisplayer.Instance.GetTranslatedLanguage("color_grading_advanced_desc");
                case ColorGradingMode.SplitToning:
                    return LanguageDisplayer.Instance.GetTranslatedLanguage("color_grading_split_desc");
                default:
                    return "";
            }
        }
        
        private void ApplyPreset(float temp, float tint, float exposure, Color lift, Color gamma, Color gain)
        {
            var material = GetMaterial();
            if (material == null) return;
            
            if (material.HasProperty("_Temperature"))
                material.SetFloat("_Temperature", temp);
            if (material.HasProperty("_Tint"))
                material.SetFloat("_Tint", tint);
            if (material.HasProperty("_Exposure"))
                material.SetFloat("_Exposure", exposure);
            if (material.HasProperty("_Lift"))
                material.SetColor("_Lift", lift);
            if (material.HasProperty("_Gamma"))
                material.SetColor("_Gamma", gamma);
            if (material.HasProperty("_Gain"))
                material.SetColor("_Gain", gain);
            
            baseWindow.CompileShader();
        }
        
        private void ApplyGradingMode()
        {
            // Clear all mode keywords first
            foreach (var keyword in _modeKeywords)
            {
                if (!string.IsNullOrEmpty(keyword))
                {
                    baseWindow.SetMaterialKeyword(keyword, false);
                }
            }
            
            // Enable the selected mode keyword
            string targetKeyword = _modeKeywords[(int)gradingMode];
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
            ApplyGradingMode();
        }
    }
}
