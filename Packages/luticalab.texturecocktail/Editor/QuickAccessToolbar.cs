using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// Quick access toolbar for fast texture operations
    /// </summary>
    public class QuickAccessToolbar : EditorWindow
    {
        [MenuItem("LuticaLab/Quick Access Toolbar")]
        public static void ShowWindow()
        {
            var window = GetWindow<QuickAccessToolbar>("Quick Tools");
            window.minSize = new Vector2(200, 400);
        }
        
        private Texture2D selectedTexture;
        private Vector2 scrollPosition;
        
        // Quick operation presets
        private readonly string[] presetNames = new string[]
        {
            "Brighten",
            "Darken",
            "High Contrast",
            "Desaturate",
            "Vibrant",
            "Blur",
            "Sharpen",
            "Edge Detect"
        };
        
        private void OnGUI()
        {
            GUILayout.Label("Quick Access Toolbar", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // Texture selection
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Select Texture", EditorStyles.boldLabel);
            
            selectedTexture = (Texture2D)EditorGUILayout.ObjectField(selectedTexture, typeof(Texture2D), false);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Use Selected"))
            {
                if (Selection.activeObject is Texture2D)
                {
                    selectedTexture = Selection.activeObject as Texture2D;
                }
            }
            if (GUILayout.Button("Clear"))
            {
                selectedTexture = null;
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();
            
            // Quick operations
            if (selectedTexture != null)
            {
                EditorGUILayout.BeginVertical("box");
                GUILayout.Label("Quick Operations", EditorStyles.boldLabel);
                
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                
                foreach (var presetName in presetNames)
                {
                    if (GUILayout.Button(presetName, GUILayout.Height(35)))
                    {
                        ApplyQuickOperation(presetName);
                    }
                }
                
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space();
                
                // Advanced options
                EditorGUILayout.BeginVertical("box");
                GUILayout.Label("Advanced", EditorStyles.boldLabel);
                
                if (GUILayout.Button("Open in TextureCocktail", GUILayout.Height(30)))
                {
                    OpenInTextureCocktail();
                }
                
                if (GUILayout.Button("Batch Process Folder", GUILayout.Height(30)))
                {
                    BatchTextureProcessor.ShowWindow();
                }
                
                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.HelpBox("Select a texture to enable quick operations", MessageType.Info);
            }
            
            EditorGUILayout.Space();
            
            // Info
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Keyboard Shortcuts", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Alt+T", "Open TextureCocktail");
            EditorGUILayout.LabelField("Alt+B", "Open Batch Processor");
            EditorGUILayout.EndVertical();
        }
        
        private void ApplyQuickOperation(string operationName)
        {
            if (selectedTexture == null)
                return;
            
            string path = EditorUtility.SaveFilePanel(
                $"Save {operationName} Texture",
                "Assets",
                selectedTexture.name + $"_{operationName.ToLower().Replace(" ", "_")}.png",
                "png"
            );
            
            if (string.IsNullOrEmpty(path))
                return;
            
            Shader shader = GetShaderForOperation(operationName);
            if (shader == null)
            {
                EditorUtility.DisplayDialog("Error", "Shader not found for operation", "OK");
                return;
            }
            
            Material material = new Material(shader);
            ConfigureMaterialForOperation(material, operationName);
            
            // Process
            RenderTexture rt = new RenderTexture(selectedTexture.width, selectedTexture.height, 0, RenderTextureFormat.ARGB32);
            rt.Create();
            
            material.SetTexture("_MainTex", selectedTexture);
            RenderTexture.active = rt;
            Graphics.Blit(selectedTexture, rt, material);
            
            Texture2D result = new Texture2D(selectedTexture.width, selectedTexture.height, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            result.Apply();
            
            RenderTexture.active = null;
            rt.Release();
            
            // Save
            System.IO.File.WriteAllBytes(path, result.EncodeToPNG());
            
            // Cleanup
            Object.DestroyImmediate(result);
            Object.DestroyImmediate(material);
            
            AssetDatabase.Refresh();
            
            EditorUtility.DisplayDialog("Success", 
                $"Texture processed with {operationName} and saved!", "OK");
        }
        
        private Shader GetShaderForOperation(string operationName)
        {
            switch (operationName)
            {
                case "Brighten":
                case "Darken":
                case "High Contrast":
                case "Desaturate":
                case "Vibrant":
                case "Blur":
                case "Sharpen":
                    return Shader.Find("Hidden/FastImageConverter");
                    
                case "Edge Detect":
                    return Shader.Find("Hidden/FeatureExtractor");
                    
                default:
                    return Shader.Find("Hidden/FastImageConverter");
            }
        }
        
        private void ConfigureMaterialForOperation(Material material, string operationName)
        {
            // Set default values first
            if (material.HasProperty("_Brightness"))
                material.SetFloat("_Brightness", 0f);
            if (material.HasProperty("_Contrast"))
                material.SetFloat("_Contrast", 1f);
            if (material.HasProperty("_Saturation"))
                material.SetFloat("_Saturation", 1f);
            if (material.HasProperty("_Gamma"))
                material.SetFloat("_Gamma", 1f);
            if (material.HasProperty("_BlurAmount"))
                material.SetFloat("_BlurAmount", 0f);
            if (material.HasProperty("_SharpAmount"))
                material.SetFloat("_SharpAmount", 0f);
            
            // Apply operation-specific settings
            switch (operationName)
            {
                case "Brighten":
                    if (material.HasProperty("_Brightness"))
                        material.SetFloat("_Brightness", 0.2f);
                    if (material.HasProperty("_Contrast"))
                        material.SetFloat("_Contrast", 1.1f);
                    if (material.HasProperty("_Saturation"))
                        material.SetFloat("_Saturation", 1.05f);
                    break;
                    
                case "Darken":
                    if (material.HasProperty("_Brightness"))
                        material.SetFloat("_Brightness", -0.2f);
                    if (material.HasProperty("_Contrast"))
                        material.SetFloat("_Contrast", 1.1f);
                    if (material.HasProperty("_Saturation"))
                        material.SetFloat("_Saturation", 0.95f);
                    break;
                    
                case "High Contrast":
                    if (material.HasProperty("_Contrast"))
                        material.SetFloat("_Contrast", 1.5f);
                    if (material.HasProperty("_Saturation"))
                        material.SetFloat("_Saturation", 1.2f);
                    break;
                    
                case "Desaturate":
                    if (material.HasProperty("_Saturation"))
                        material.SetFloat("_Saturation", 0f);
                    break;
                    
                case "Vibrant":
                    if (material.HasProperty("_Brightness"))
                        material.SetFloat("_Brightness", 0.1f);
                    if (material.HasProperty("_Contrast"))
                        material.SetFloat("_Contrast", 1.2f);
                    if (material.HasProperty("_Saturation"))
                        material.SetFloat("_Saturation", 1.5f);
                    if (material.HasProperty("_Gamma"))
                        material.SetFloat("_Gamma", 0.9f);
                    break;
                    
                case "Blur":
                    material.EnableKeyword("BLUR_FILTER");
                    if (material.HasProperty("_BlurAmount"))
                        material.SetFloat("_BlurAmount", 0.5f);
                    break;
                    
                case "Sharpen":
                    material.EnableKeyword("SHARPEN_FILTER");
                    if (material.HasProperty("_SharpAmount"))
                        material.SetFloat("_SharpAmount", 1.0f);
                    break;
                    
                case "Edge Detect":
                    if (material.HasProperty("_EdgeSensitivity"))
                        material.SetFloat("_EdgeSensitivity", 0.5f);
                    if (material.HasProperty("_FeatureColor"))
                        material.SetColor("_FeatureColor", Color.white);
                    break;
            }
        }
        
        private void OpenInTextureCocktail()
        {
            if (selectedTexture == null)
                return;
            
            var window = EditorWindow.GetWindow<TextureCocktail>("TextureCocktail");
            window.Show();
            
            // Set the texture through reflection
            var field = typeof(TextureCocktail).GetField("_targetTexture", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(window, selectedTexture);
            }
        }
    }
}
