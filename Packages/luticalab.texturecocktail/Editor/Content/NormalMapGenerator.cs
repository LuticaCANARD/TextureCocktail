using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    public class NormalMapGenerator : TextureCocktailContent
    {
        public override bool UseDefaultLayout { get => false; }
        public override string[] DontWantDisplayPropertyName { get => new string[] { }; }
        
        private bool _showSettings = true;
        private bool _showPreview = true;
        
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
                GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("normal_map_generator_title"), EditorStyles.boldLabel);
                GUILayout.Space(5);
                
                // Description
                EditorGUILayout.HelpBox(LanguageDisplayer.Instance.GetTranslatedLanguage("normal_map_description"), MessageType.Info);
                GUILayout.Space(10);
                
                // Settings
                _showSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showSettings, 
                    LanguageDisplayer.Instance.GetTranslatedLanguage("normal_map_settings"));
                if (_showSettings)
                {
                    ShowNormalMapSettings();
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
            }
            finally
            {
                GUILayout.EndScrollView();
            }
        }
        
        private void ShowNormalMapSettings()
        {
            var material = GetMaterial();
            if (material == null) return;
            
            EditorGUI.BeginChangeCheck();
            
            if (material.HasProperty("_Strength"))
            {
                float strength = material.GetFloat("_Strength");
                strength = EditorGUILayout.Slider(
                    LanguageDisplayer.Instance.GetTranslatedLanguage("normal_strength"), 
                    strength, 0, 10);
                material.SetFloat("_Strength", strength);
            }
            
            if (material.HasProperty("_HeightScale"))
            {
                float heightScale = material.GetFloat("_HeightScale");
                heightScale = EditorGUILayout.Slider(
                    LanguageDisplayer.Instance.GetTranslatedLanguage("height_scale"), 
                    heightScale, 0, 1);
                material.SetFloat("_HeightScale", heightScale);
            }
            
            if (EditorGUI.EndChangeCheck())
            {
                baseWindow.OnShaderValueChange();
            }
        }
        
        private string GetEffectDescription()
        {
            return LanguageDisplayer.Instance.GetTranslatedLanguage("normal_map_info");
        }
        
        private Material GetMaterial()
        {
            var materialField = typeof(TextureCocktail).GetField("_calcMaterial", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return materialField?.GetValue(baseWindow) as Material;
        }
        
        public override void OnShaderValueChanged()
        {
            // Called when shader values change
        }
    }
}
