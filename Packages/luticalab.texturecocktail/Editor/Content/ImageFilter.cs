using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    public class ImageFilter : TextureCocktailContent
    {
        public override bool UseDefaultLayout { get => false; }
        public override string[] DontWantDisplayPropertyName { get => new string[] { }; }
        
        private bool _showHSVConvert = true;
        private bool _showHSVOffset = true;
        private bool _showPreview = true;
        
        // Quick presets for common HSV adjustments
        private readonly Dictionary<string, System.Action> _presets = new Dictionary<string, System.Action>();
        
        public override void Initialize(TextureCocktail baseWindow)
        {
            base.Initialize(baseWindow);
            InitializePresets();
        }
        
        private void InitializePresets()
        {
            _presets["Reset"] = () => ApplyPreset(Vector3.zero, Vector3.zero);
            _presets["Warm Tone"] = () => ApplyPreset(new Vector3(0.05f, 0, 0.05f), Vector3.zero);
            _presets["Cool Tone"] = () => ApplyPreset(new Vector3(-0.05f, 0, 0), Vector3.zero);
            _presets["Increase Saturation"] = () => ApplyPreset(new Vector3(0, 0.2f, 0), Vector3.zero);
            _presets["Decrease Saturation"] = () => ApplyPreset(new Vector3(0, -0.2f, 0), Vector3.zero);
            _presets["Brighten"] = () => ApplyPreset(new Vector3(0, 0, 0.2f), Vector3.zero);
            _presets["Darken"] = () => ApplyPreset(new Vector3(0, 0, -0.2f), Vector3.zero);
        }
        
        public override void OnGUI()
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            
            // Title
            GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("hsv_mover_title"), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(LanguageDisplayer.Instance.GetTranslatedLanguage("hsv_mover_desc"), MessageType.Info);
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
            
            // HSV Convert section
            _showHSVConvert = EditorGUILayout.BeginFoldoutHeaderGroup(_showHSVConvert, 
                LanguageDisplayer.Instance.GetTranslatedLanguage("hsv_convert"));
            if (_showHSVConvert)
            {
                EditorGUILayout.HelpBox(LanguageDisplayer.Instance.GetTranslatedLanguage("hsv_convert_help"), MessageType.None);
                
                var material = GetMaterial();
                if (material != null && material.HasProperty("_hsvConvertVector"))
                {
                    Vector4 currentConvert = material.GetVector("_hsvConvertVector");
                    
                    EditorGUI.BeginChangeCheck();
                    
                    GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("hue_convert"), EditorStyles.boldLabel);
                    float h = EditorGUILayout.Slider("Hue", currentConvert.x, -1f, 1f);
                    
                    GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("saturation_convert"), EditorStyles.boldLabel);
                    float s = EditorGUILayout.Slider("Saturation", currentConvert.y, -1f, 1f);
                    
                    GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("value_convert"), EditorStyles.boldLabel);
                    float v = EditorGUILayout.Slider("Value", currentConvert.z, -1f, 1f);
                    
                    if (EditorGUI.EndChangeCheck())
                    {
                        material.SetVector("_hsvConvertVector", new Vector4(h, s, v, 0));
                        baseWindow.CompileShader();
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            // HSV Offset section
            _showHSVOffset = EditorGUILayout.BeginFoldoutHeaderGroup(_showHSVOffset, 
                LanguageDisplayer.Instance.GetTranslatedLanguage("hsv_offset"));
            if (_showHSVOffset)
            {
                EditorGUILayout.HelpBox(LanguageDisplayer.Instance.GetTranslatedLanguage("hsv_offset_help"), MessageType.None);
                
                var material = GetMaterial();
                if (material != null && material.HasProperty("_hsvOffsetVector"))
                {
                    Vector4 currentOffset = material.GetVector("_hsvOffsetVector");
                    
                    EditorGUI.BeginChangeCheck();
                    
                    GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("hue_offset"), EditorStyles.boldLabel);
                    float h = EditorGUILayout.Slider("Hue Offset", currentOffset.x, -1f, 1f);
                    
                    GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("saturation_offset"), EditorStyles.boldLabel);
                    float s = EditorGUILayout.Slider("Saturation Offset", currentOffset.y, -1f, 1f);
                    
                    GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("value_offset"), EditorStyles.boldLabel);
                    float v = EditorGUILayout.Slider("Value Offset", currentOffset.z, -1f, 1f);
                    
                    if (EditorGUI.EndChangeCheck())
                    {
                        material.SetVector("_hsvOffsetVector", new Vector4(h, s, v, 0));
                        baseWindow.CompileShader();
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            GUILayout.Space(10);
            
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
        
        private void ApplyPreset(Vector3 convert, Vector3 offset)
        {
            var material = GetMaterial();
            if (material == null) return;
            
            if (material.HasProperty("_hsvConvertVector"))
                material.SetVector("_hsvConvertVector", new Vector4(convert.x, convert.y, convert.z, 0));
            if (material.HasProperty("_hsvOffsetVector"))
                material.SetVector("_hsvOffsetVector", new Vector4(offset.x, offset.y, offset.z, 0));
            
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
            // Refresh when shader values change
        }
    }
}
