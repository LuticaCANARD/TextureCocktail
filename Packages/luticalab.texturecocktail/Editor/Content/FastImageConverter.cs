using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    public enum FilterMode
    {
        None,
        EdgeDetection,
        Blur,
        Sharpen,
        Point
    }
    
    public class FastImageConverter : TextureCocktailContent
    {
        public override bool UseDefaultLayout { get => false; }
        public override string[] DontWantDisplayPropertyName { get => new string[] { }; }
        
        private FilterMode filterMode = FilterMode.None;
        private bool _showBasicAdjustments = true;
        private bool _showFilters = false;
        private bool _showAdvanced = false;
        private bool _showPreview = true;
        
        private static readonly string[] _filterKeywords = new string[] 
        { 
            "",  // None
            "EDGE_DETECTION", 
            "BLUR_FILTER", 
            "SHARPEN_FILTER" 
        };
        
        // Quick presets
        private readonly Dictionary<string, Dictionary<string, float>> _presets = new Dictionary<string, Dictionary<string, float>>()
        {
            {"Reset", new Dictionary<string, float> { {"_Brightness", 0}, {"_Contrast", 1}, {"_Saturation", 1}, {"_Gamma", 1} }},
            {"Brighten", new Dictionary<string, float> { {"_Brightness", 0.2f}, {"_Contrast", 1.1f}, {"_Saturation", 1.05f}, {"_Gamma", 1} }},
            {"Darken", new Dictionary<string, float> { {"_Brightness", -0.2f}, {"_Contrast", 1.1f}, {"_Saturation", 0.95f}, {"_Gamma", 1} }},
            {"High Contrast", new Dictionary<string, float> { {"_Brightness", 0}, {"_Contrast", 1.5f}, {"_Saturation", 1.2f}, {"_Gamma", 1} }},
            {"Desaturate", new Dictionary<string, float> { {"_Brightness", 0}, {"_Contrast", 1}, {"_Saturation", 0}, {"_Gamma", 1} }},
            {"Vibrant", new Dictionary<string, float> { {"_Brightness", 0.1f}, {"_Contrast", 1.2f}, {"_Saturation", 1.5f}, {"_Gamma", 0.9f} }},
        };
        
        public override void Initialize(TextureCocktail baseWindow)
        {
            base.Initialize(baseWindow);
        }
        
        public override void OnGUI()
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            
            try
            {
                // Title
                GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("fast_image_converter_title"), EditorStyles.boldLabel);
                GUILayout.Space(5);
                
                // Quick Presets
                GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("quick_presets"), EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                foreach (var preset in _presets.Keys)
                {
                    if (GUILayout.Button(preset, GUILayout.Height(25)))
                    {
                        ApplyPreset(preset);
                    }
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(10);
                
                // Filter Mode Selection
                GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("filter_mode"), EditorStyles.boldLabel);
                var newFilterMode = (FilterMode)EditorGUILayout.EnumPopup(LanguageDisplayer.Instance.GetTranslatedLanguage("filter_type"), filterMode);
                if (newFilterMode != filterMode)
                {
                    filterMode = newFilterMode;
                    ApplyFilterMode();
                }
                
                // Display filter description
                switch (filterMode)
                {
                    case FilterMode.None:
                        EditorGUILayout.HelpBox(LanguageDisplayer.Instance.GetTranslatedLanguage("filter_none_desc"), MessageType.Info);
                        break;
                    case FilterMode.EdgeDetection:
                        EditorGUILayout.HelpBox(LanguageDisplayer.Instance.GetTranslatedLanguage("filter_edge_desc"), MessageType.Info);
                        break;
                    case FilterMode.Blur:
                        EditorGUILayout.HelpBox(LanguageDisplayer.Instance.GetTranslatedLanguage("filter_blur_desc"), MessageType.Info);
                        break;
                    case FilterMode.Sharpen:
                        EditorGUILayout.HelpBox(LanguageDisplayer.Instance.GetTranslatedLanguage("filter_sharpen_desc"), MessageType.Info);
                        break;
                }
                
                GUILayout.Space(10);
                
                // Basic Adjustments
                _showBasicAdjustments = EditorGUILayout.BeginFoldoutHeaderGroup(_showBasicAdjustments, 
                    LanguageDisplayer.Instance.GetTranslatedLanguage("basic_adjustments"));
                if (_showBasicAdjustments)
                {
                    ShowFilterSpecificParameters();
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
            }
            finally
            {
                GUILayout.EndScrollView();
            }
        }
        
        private void ShowFilterSpecificParameters()
        {
            var material = GetMaterial();
            if (material == null)
            {
                baseWindow.ShowShaderInfo();
                return;
            }
            
            EditorGUI.BeginChangeCheck();
            
            // Always show basic image adjustment parameters
            if (material.HasProperty("_Brightness"))
            {
                float brightness = material.GetFloat("_Brightness");
                brightness = EditorGUILayout.Slider("Brightness", brightness, -1, 1);
                material.SetFloat("_Brightness", brightness);
            }
            
            if (material.HasProperty("_Contrast"))
            {
                float contrast = material.GetFloat("_Contrast");
                contrast = EditorGUILayout.Slider("Contrast", contrast, 0, 2);
                material.SetFloat("_Contrast", contrast);
            }
            
            if (material.HasProperty("_Saturation"))
            {
                float saturation = material.GetFloat("_Saturation");
                saturation = EditorGUILayout.Slider("Saturation", saturation, 0, 2);
                material.SetFloat("_Saturation", saturation);
            }
            
            if (material.HasProperty("_Gamma"))
            {
                float gamma = material.GetFloat("_Gamma");
                gamma = EditorGUILayout.Slider("Gamma", gamma, 0.1f, 3);
                material.SetFloat("_Gamma", gamma);
            }
            
            // Show filter-specific parameters based on filter mode
            switch (filterMode)
            {
                case FilterMode.Blur:
                    if (material.HasProperty("_BlurSize"))
                    {
                        float blurSize = material.GetFloat("_BlurSize");
                        blurSize = EditorGUILayout.Slider("Blur Size", blurSize, 0, 5);
                        material.SetFloat("_BlurSize", blurSize);
                    }
                    break;
                    
                case FilterMode.Sharpen:
                    if (material.HasProperty("_SharpenStrength"))
                    {
                        float sharpen = material.GetFloat("_SharpenStrength");
                        sharpen = EditorGUILayout.Slider("Sharpen Strength", sharpen, 0, 2);
                        material.SetFloat("_SharpenStrength", sharpen);
                    }
                    break;
                    
                case FilterMode.EdgeDetection:
                    if (material.HasProperty("_EdgeThreshold"))
                    {
                        float edgeThreshold = material.GetFloat("_EdgeThreshold");
                        edgeThreshold = EditorGUILayout.Slider("Edge Threshold", edgeThreshold, 0, 1);
                        material.SetFloat("_EdgeThreshold", edgeThreshold);
                    }
                    if (material.HasProperty("_EdgeColor"))
                    {
                        Color edgeColor = material.GetColor("_EdgeColor");
                        GUILayout.Label("Edge Color");
                        edgeColor = EditorGUILayout.ColorField(edgeColor);
                        material.SetColor("_EdgeColor", edgeColor);
                    }
                    break;
            }
            
            if (EditorGUI.EndChangeCheck())
            {
                baseWindow.OnShaderValueChange();
            }
        }
        
        private void ApplyPreset(string presetName)
        {
            if (!_presets.ContainsKey(presetName))
                return;
            
            var preset = _presets[presetName];
            foreach (var kvp in preset)
            {
                // Find and set the material property
                if (baseWindow != null)
                {
                    var material = GetMaterial();
                    if (material != null && material.HasProperty(kvp.Key))
                    {
                        material.SetFloat(kvp.Key, kvp.Value);
                    }
                }
            }
            
            baseWindow.CompileShader();
        }
        
        private void ApplyFilterMode()
        {
            // Clear all filter keywords first
            foreach (var keyword in _filterKeywords)
            {
                if (!string.IsNullOrEmpty(keyword))
                {
                    baseWindow.SetMaterialKeyword(keyword, false);
                }
            }
            
            // Enable the selected filter keyword
            string targetKeyword = _filterKeywords[(int)filterMode];
            if (!string.IsNullOrEmpty(targetKeyword))
            {
                baseWindow.SetMaterialKeyword(targetKeyword, true);
            }
            
            baseWindow.CompileShader();
        }
        
        private Material GetMaterial()
        {
            // Access the material through reflection since it's private
            var type = baseWindow.GetType();
            var field = type.GetField("_calcMaterial", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(baseWindow) as Material;
        }
        
        public override void OnShaderValueChanged()
        {
            // Auto-apply filter mode when shader values change
            ApplyFilterMode();
        }
    }
}
