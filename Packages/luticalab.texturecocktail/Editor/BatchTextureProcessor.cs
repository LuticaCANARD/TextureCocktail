using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    public class BatchTextureProcessor : EditorWindow
    {
        [MenuItem("LuticaLab/TextureCocktail Batch Processor")]
        public static void ShowWindow()
        {
            GetWindow<BatchTextureProcessor>("Batch Processor");
        }
        
        private List<Texture2D> textures = new List<Texture2D>();
        private Shader selectedShader;
        private string outputPath = "Assets/ProcessedTextures";
        private Vector2 scrollPosition;
        private bool processing = false;
        private float progress = 0f;
        
        // Shader parameters (common ones)
        private float brightness = 0f;
        private float contrast = 1f;
        private float saturation = 1f;
        private float gamma = 1f;
        
        private void OnGUI()
        {
            GUILayout.Label("Batch Texture Processor", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // Shader selection
            selectedShader = (Shader)EditorGUILayout.ObjectField("Shader to Apply", selectedShader, typeof(Shader), false);
            
            EditorGUILayout.Space();
            
            // Common parameters
            GUILayout.Label("Common Parameters", EditorStyles.boldLabel);
            brightness = EditorGUILayout.Slider("Brightness", brightness, -1f, 1f);
            contrast = EditorGUILayout.Slider("Contrast", contrast, 0f, 3f);
            saturation = EditorGUILayout.Slider("Saturation", saturation, 0f, 2f);
            gamma = EditorGUILayout.Slider("Gamma", gamma, 0.1f, 3f);
            
            EditorGUILayout.Space();
            
            // Output path
            EditorGUILayout.BeginHorizontal();
            outputPath = EditorGUILayout.TextField("Output Path", outputPath);
            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    // Convert absolute path to relative path
                    if (path.StartsWith(Application.dataPath))
                    {
                        outputPath = "Assets" + path.Substring(Application.dataPath.Length);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // Texture list
            GUILayout.Label("Textures to Process", EditorStyles.boldLabel);
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
            
            for (int i = 0; i < textures.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                textures[i] = (Texture2D)EditorGUILayout.ObjectField(textures[i], typeof(Texture2D), false);
                if (GUILayout.Button("X", GUILayout.Width(30)))
                {
                    textures.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
            
            // Add texture buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Texture"))
            {
                textures.Add(null);
            }
            if (GUILayout.Button("Add Selected Textures"))
            {
                AddSelectedTextures();
            }
            if (GUILayout.Button("Clear All"))
            {
                textures.Clear();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // Process button
            GUI.enabled = selectedShader != null && textures.Count > 0 && !processing;
            if (GUILayout.Button("Process All Textures", GUILayout.Height(40)))
            {
                ProcessTextures();
            }
            GUI.enabled = true;
            
            // Progress bar
            if (processing)
            {
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), progress, $"Processing: {(int)(progress * 100)}%");
                Repaint();
            }
        }
        
        private void AddSelectedTextures()
        {
            foreach (Object obj in Selection.objects)
            {
                if (obj is Texture2D texture)
                {
                    if (!textures.Contains(texture))
                    {
                        textures.Add(texture);
                    }
                }
            }
        }
        
        private void ProcessTextures()
        {
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }
            
            processing = true;
            Material material = new Material(selectedShader);
            
            // Set common parameters if shader has them
            if (material.HasProperty("_Brightness"))
                material.SetFloat("_Brightness", brightness);
            if (material.HasProperty("_Contrast"))
                material.SetFloat("_Contrast", contrast);
            if (material.HasProperty("_Saturation"))
                material.SetFloat("_Saturation", saturation);
            if (material.HasProperty("_Gamma"))
                material.SetFloat("_Gamma", gamma);
            
            for (int i = 0; i < textures.Count; i++)
            {
                progress = (float)i / textures.Count;
                
                Texture2D texture = textures[i];
                if (texture == null) continue;
                
                ProcessSingleTexture(texture, material);
            }
            
            progress = 1f;
            processing = false;
            
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Batch Processing Complete", 
                $"Processed {textures.Count} textures successfully!", "OK");
        }
        
        private void ProcessSingleTexture(Texture2D sourceTexture, Material material)
        {
            // Create render texture
            RenderTexture rt = new RenderTexture(sourceTexture.width, sourceTexture.height, 0, RenderTextureFormat.ARGB32);
            rt.Create();
            
            // Set main texture
            material.SetTexture("_MainTex", sourceTexture);
            
            // Render
            RenderTexture.active = rt;
            Graphics.Blit(sourceTexture, rt, material);
            
            // Read pixels
            Texture2D result = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            result.Apply();
            
            RenderTexture.active = null;
            rt.Release();
            
            // Save
            string fileName = $"{sourceTexture.name}_processed.png";
            string fullPath = Path.Combine(outputPath, fileName);
            File.WriteAllBytes(fullPath, result.EncodeToPNG());
            
            // Cleanup
            Object.DestroyImmediate(result);
        }
    }
}
