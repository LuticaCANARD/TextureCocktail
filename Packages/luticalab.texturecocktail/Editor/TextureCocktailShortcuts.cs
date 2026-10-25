using UiText = LuticaLab.TextureCocktail.TextureCocktailLocalization;
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
                EditorUtility.DisplayDialog(UiText.Text("No Texture Selected"),
                    UiText.Text("Please select a Texture2D in the Project window."), UiText.Text("OK"));
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
                EditorUtility.DisplayDialog(UiText.Text("No Texture Selected"),
                    UiText.Text("Please select a Texture2D in the Project window."), UiText.Text("OK"));
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
                window.SetTargetTexture(texture);
            }
        }
        
        [MenuItem("Assets/TextureCocktail/Open with TextureCocktail", true)]
        private static bool ValidateOpenWithTextureCocktail()
        {
            return Selection.activeObject is Texture2D;
        }
        
        // Open A/B test window directly (no preview yet)
        [MenuItem("LuticaLab/Quick Access/Open A/B Test Window")]
        public static void OpenABTestWindowEmpty()
        {
            TextureABTestWindow.ShowWindow(null, null, "Texture");
        }

        // Copy texture settings
        private static Material copiedMaterial;
        
        [MenuItem("CONTEXT/Material/Copy Texture Shader Settings")]
        private static void CopyTextureShaderSettings(MenuCommand command)
        {
            Material material = command.context as Material;
            if (material != null)
            {
                if (copiedMaterial != null)
                {
                    Object.DestroyImmediate(copiedMaterial);
                }
                copiedMaterial = new Material(material);
                Debug.Log($"Copied shader settings from material: {material.name}");
            }
        }
        
        [MenuItem("CONTEXT/Material/Paste Texture Shader Settings")]
        private static void PasteTextureShaderSettings(MenuCommand command)
        {
            if (copiedMaterial == null)
            {
                EditorUtility.DisplayDialog(UiText.Text("No Settings Copied"),
                    UiText.Text("Please copy shader settings first."), UiText.Text("OK"));
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
                EditorUtility.DisplayDialog(UiText.Text("Shader Mismatch"),
                    UiText.Text("Target material must use the same shader."), UiText.Text("OK"));
            }
        }
        
        // Helper method to quickly process a texture
        private static void QuickProcessTexture(Texture2D sourceTexture, string shaderName, bool enableEdgeDetection = false)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                EditorUtility.DisplayDialog(UiText.Text("ab_shader_not_found_title"),
                    UiText.Format("Could not find shader: {0}", shaderName), UiText.Text("OK"));
                return;
            }
            
            string path = EditorUtility.SaveFilePanel(
                UiText.Text("Save Processed Texture"),
                "Assets",
                sourceTexture.name + "_processed.png",
                "png"
            );
            
            if (string.IsNullOrEmpty(path))
                return;
            
            Material material = null;
            try
            {
                material = new Material(shader);

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

                TextureExportUtility.SaveMaterialPng(sourceTexture, material, path);
            }
            finally
            {
                if (material != null) Object.DestroyImmediate(material);
            }
            
            AssetDatabase.Refresh();
            Debug.Log($"Processed texture saved to: {path}");
            EditorUtility.DisplayDialog(UiText.Text("Success"),
                UiText.Format("Texture processed and saved to:\n{0}", path), UiText.Text("OK"));
        }
    }
}
