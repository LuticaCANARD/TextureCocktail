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
        
        private enum QuickOperation
        {
            Brighten,
            Darken,
            HighContrast,
            Desaturate,
            Vibrant,
            Blur,
            Sharpen,
            EdgeDetect
        }

        private readonly struct QuickOperationPreset
        {
            public readonly QuickOperation Operation;
            public readonly string DisplayName;
            public readonly string FileSuffix;
            public readonly string ShaderName;

            public QuickOperationPreset(QuickOperation operation, string displayName, string fileSuffix, string shaderName)
            {
                Operation = operation;
                DisplayName = displayName;
                FileSuffix = fileSuffix;
                ShaderName = shaderName;
            }
        }

        // Display text can change without changing dispatch or exported file names.
        private static readonly QuickOperationPreset[] Presets =
        {
            new QuickOperationPreset(QuickOperation.Brighten, "Brighten", "brighten", "Hidden/FastImageConverter"),
            new QuickOperationPreset(QuickOperation.Darken, "Darken", "darken", "Hidden/FastImageConverter"),
            new QuickOperationPreset(QuickOperation.HighContrast, "High Contrast", "high_contrast", "Hidden/FastImageConverter"),
            new QuickOperationPreset(QuickOperation.Desaturate, "Desaturate", "desaturate", "Hidden/FastImageConverter"),
            new QuickOperationPreset(QuickOperation.Vibrant, "Vibrant", "vibrant", "Hidden/FastImageConverter"),
            new QuickOperationPreset(QuickOperation.Blur, "Blur", "blur", "Hidden/FastImageConverter"),
            new QuickOperationPreset(QuickOperation.Sharpen, "Sharpen", "sharpen", "Hidden/FastImageConverter"),
            new QuickOperationPreset(QuickOperation.EdgeDetect, "Edge Detect", "edge_detect", "Hidden/FeatureExtractor")
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
                
                foreach (var preset in Presets)
                {
                    if (GUILayout.Button(preset.DisplayName, GUILayout.Height(35)))
                    {
                        ApplyQuickOperation(preset);
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
        
        private void ApplyQuickOperation(QuickOperationPreset preset)
        {
            if (selectedTexture == null)
                return;
            
            string path = EditorUtility.SaveFilePanel(
                $"Save {preset.DisplayName} Texture",
                "Assets",
                selectedTexture.name + $"_{preset.FileSuffix}.png",
                "png"
            );
            
            if (string.IsNullOrEmpty(path))
                return;
            
            Shader shader = Shader.Find(preset.ShaderName);
            if (shader == null)
            {
                EditorUtility.DisplayDialog("Error", "Shader not found for operation", "OK");
                return;
            }
            
            Material material = null;
            try
            {
                material = new Material(shader);
                ConfigureMaterialForOperation(material, preset.Operation);
                TextureExportUtility.SaveMaterialPng(selectedTexture, material, path);
            }
            finally
            {
                if (material != null) Object.DestroyImmediate(material);
            }

            AssetDatabase.Refresh();
            
            EditorUtility.DisplayDialog("Success", 
                $"Texture processed with {preset.DisplayName} and saved!", "OK");
        }

        private void ConfigureMaterialForOperation(Material material, QuickOperation operation)
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
            switch (operation)
            {
                case QuickOperation.Brighten:
                    if (material.HasProperty("_Brightness"))
                        material.SetFloat("_Brightness", 0.2f);
                    if (material.HasProperty("_Contrast"))
                        material.SetFloat("_Contrast", 1.1f);
                    if (material.HasProperty("_Saturation"))
                        material.SetFloat("_Saturation", 1.05f);
                    break;
                    
                case QuickOperation.Darken:
                    if (material.HasProperty("_Brightness"))
                        material.SetFloat("_Brightness", -0.2f);
                    if (material.HasProperty("_Contrast"))
                        material.SetFloat("_Contrast", 1.1f);
                    if (material.HasProperty("_Saturation"))
                        material.SetFloat("_Saturation", 0.95f);
                    break;
                    
                case QuickOperation.HighContrast:
                    if (material.HasProperty("_Contrast"))
                        material.SetFloat("_Contrast", 1.5f);
                    if (material.HasProperty("_Saturation"))
                        material.SetFloat("_Saturation", 1.2f);
                    break;
                    
                case QuickOperation.Desaturate:
                    if (material.HasProperty("_Saturation"))
                        material.SetFloat("_Saturation", 0f);
                    break;
                    
                case QuickOperation.Vibrant:
                    if (material.HasProperty("_Brightness"))
                        material.SetFloat("_Brightness", 0.1f);
                    if (material.HasProperty("_Contrast"))
                        material.SetFloat("_Contrast", 1.2f);
                    if (material.HasProperty("_Saturation"))
                        material.SetFloat("_Saturation", 1.5f);
                    if (material.HasProperty("_Gamma"))
                        material.SetFloat("_Gamma", 0.9f);
                    break;
                    
                case QuickOperation.Blur:
                    material.EnableKeyword("BLUR_FILTER");
                    if (material.HasProperty("_BlurAmount"))
                        material.SetFloat("_BlurAmount", 0.5f);
                    break;
                    
                case QuickOperation.Sharpen:
                    material.EnableKeyword("SHARPEN_FILTER");
                    if (material.HasProperty("_SharpAmount"))
                        material.SetFloat("_SharpAmount", 1.0f);
                    break;
                    
                case QuickOperation.EdgeDetect:
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
            window.SetTargetTexture(selectedTexture);
        }
    }
}
