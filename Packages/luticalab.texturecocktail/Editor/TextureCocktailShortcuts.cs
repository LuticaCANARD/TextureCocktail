using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// Quick access shortcuts for TextureCocktail features
    /// </summary>
    public static class TextureCocktailShortcuts
    {
        // Main window shortcut: Alt+T
        [MenuItem("LuticaLab/Quick Access/Open TextureCocktail %&t")]
        public static void OpenTextureCocktail()
        {
            TextureCocktail.ShowWindow();
        }
        
        // Batch processor shortcut: Alt+B
        [MenuItem("LuticaLab/Quick Access/Open Batch Processor %&b")]
        public static void OpenBatchProcessor()
        {
            BatchTextureProcessor.ShowWindow();
        }
        
        // Quick convert selected texture with FastImageConverter
        [MenuItem("Assets/TextureCocktail/Quick Convert with Fast Converter", false, 20)]
        private static void QuickConvertWithFastConverter()
        {
            Texture2D texture = Selection.activeObject as Texture2D;
            if (texture != null)
            {
                QuickProcessTexture(texture, "Hidden/FastImageConverter");
            }
            else
            {
                EditorUtility.DisplayDialog("No Texture Selected", 
                    "Please select a Texture2D in the Project window.", "OK");
            }
        }
        
        [MenuItem("Assets/TextureCocktail/Quick Convert with Fast Converter", true)]
        private static bool ValidateQuickConvertWithFastConverter()
        {
            return Selection.activeObject is Texture2D;
        }
        
        // Quick edge detection
        [MenuItem("Assets/TextureCocktail/Quick Edge Detection", false, 21)]
        private static void QuickEdgeDetection()
        {
            Texture2D texture = Selection.activeObject as Texture2D;
            if (texture != null)
            {
                QuickProcessTexture(texture, "Hidden/FeatureExtractor", enableEdgeDetection: true);
            }
            else
            {
                EditorUtility.DisplayDialog("No Texture Selected", 
                    "Please select a Texture2D in the Project window.", "OK");
            }
        }
        
        [MenuItem("Assets/TextureCocktail/Quick Edge Detection", true)]
        private static bool ValidateQuickEdgeDetection()
        {
            return Selection.activeObject is Texture2D;
        }
        
        // Open with TextureCocktail
        [MenuItem("Assets/TextureCocktail/Open with TextureCocktail", false, 1)]
        private static void OpenWithTextureCocktail()
        {
            Texture2D texture = Selection.activeObject as Texture2D;
            if (texture != null)
            {
                var window = EditorWindow.GetWindow<TextureCocktail>("TextureCocktail");
                window.Show();
                
                // Set the texture through reflection
                var field = typeof(TextureCocktail).GetField("_targetTexture", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(window, texture);
                }
            }
        }
        
        [MenuItem("Assets/TextureCocktail/Open with TextureCocktail", true)]
        private static bool ValidateOpenWithTextureCocktail()
        {
            return Selection.activeObject is Texture2D;
        }
        
        // Copy texture settings
        private static Material copiedMaterial;
        
        [MenuItem("CONTEXT/Material/Copy Texture Shader Settings")]
        private static void CopyTextureShaderSettings(MenuCommand command)
        {
            Material material = command.context as Material;
            if (material != null)
            {
                copiedMaterial = new Material(material);
                Debug.Log($"Copied shader settings from material: {material.name}");
            }
        }
        
        [MenuItem("CONTEXT/Material/Paste Texture Shader Settings")]
        private static void PasteTextureShaderSettings(MenuCommand command)
        {
            if (copiedMaterial == null)
            {
                EditorUtility.DisplayDialog("No Settings Copied", 
                    "Please copy shader settings first.", "OK");
                return;
            }
            
            Material targetMaterial = command.context as Material;
            if (targetMaterial != null && copiedMaterial.shader == targetMaterial.shader)
            {
                targetMaterial.CopyPropertiesFromMaterial(copiedMaterial);
                Debug.Log($"Pasted shader settings to material: {targetMaterial.name}");
            }
            else
            {
                EditorUtility.DisplayDialog("Shader Mismatch", 
                    "Target material must use the same shader.", "OK");
            }
        }
        
        // Helper method to quickly process a texture
        private static void QuickProcessTexture(Texture2D sourceTexture, string shaderName, bool enableEdgeDetection = false)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                EditorUtility.DisplayDialog("Shader Not Found", 
                    $"Could not find shader: {shaderName}", "OK");
                return;
            }
            
            string path = EditorUtility.SaveFilePanel(
                "Save Processed Texture",
                "Assets",
                sourceTexture.name + "_processed.png",
                "png"
            );
            
            if (string.IsNullOrEmpty(path))
                return;
            
            Material material = new Material(shader);
            
            // Apply default settings for quick processing
            if (material.HasProperty("_Brightness"))
                material.SetFloat("_Brightness", 0f);
            if (material.HasProperty("_Contrast"))
                material.SetFloat("_Contrast", 1.2f);
            if (material.HasProperty("_Saturation"))
                material.SetFloat("_Saturation", 1.1f);
            
            if (enableEdgeDetection && material.HasProperty("_EdgeSensitivity"))
            {
                material.SetFloat("_EdgeSensitivity", 0.5f);
            }
            
            // Create render texture
            RenderTexture rt = new RenderTexture(sourceTexture.width, sourceTexture.height, 0, RenderTextureFormat.ARGB32);
            rt.Create();
            
            // Set main texture and render
            material.SetTexture("_MainTex", sourceTexture);
            RenderTexture.active = rt;
            Graphics.Blit(sourceTexture, rt, material);
            
            // Read pixels
            Texture2D result = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGBA32, false);
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
            Debug.Log($"Processed texture saved to: {path}");
            EditorUtility.DisplayDialog("Success", 
                $"Texture processed and saved to:\n{path}", "OK");
        }
    }
}
