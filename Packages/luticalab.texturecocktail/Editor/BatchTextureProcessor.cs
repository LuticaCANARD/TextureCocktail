using UiText = LuticaLab.TextureCocktail.TextureCocktailLocalization;
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
            GetWindow<BatchTextureProcessor>(UiText.Text("Batch Processor"));
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
            titleContent.text = UiText.Text("Batch Processor");
            GUILayout.Label(UiText.Text("Batch Texture Processor"), EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // Shader selection
            selectedShader = (Shader)EditorGUILayout.ObjectField(UiText.Text("apply_shader"), selectedShader, typeof(Shader), false);
            
            EditorGUILayout.Space();
            
            // Common parameters
            GUILayout.Label(UiText.Text("Common Parameters"), EditorStyles.boldLabel);
            brightness = EditorGUILayout.Slider(UiText.Text("Brightness"), brightness, -1f, 1f);
            contrast = EditorGUILayout.Slider(UiText.Text("Contrast"), contrast, 0f, 3f);
            saturation = EditorGUILayout.Slider(UiText.Text("Saturation"), saturation, 0f, 2f);
            gamma = EditorGUILayout.Slider(UiText.Text("Gamma"), gamma, 0.1f, 3f);
            
            EditorGUILayout.Space();
            
            // Output path
            EditorGUILayout.BeginHorizontal();
            outputPath = EditorGUILayout.TextField(UiText.Text("Output Path"), outputPath);
            if (GUILayout.Button(UiText.Text("ab_browse"), GUILayout.Width(80)))
            {
                string path = EditorUtility.OpenFolderPanel(UiText.Text("Select Output Folder"), "Assets", "");
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
            GUILayout.Label(UiText.Text("Textures to Process"), EditorStyles.boldLabel);
            
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
            if (GUILayout.Button(UiText.Text("Add Texture")))
            {
                textures.Add(null);
            }
            if (GUILayout.Button(UiText.Text("Add Selected Textures")))
            {
                AddSelectedTextures();
            }
            if (GUILayout.Button(UiText.Text("Clear All")))
            {
                textures.Clear();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // Process button
            GUI.enabled = selectedShader != null && textures.Count > 0 && !processing;
            if (GUILayout.Button(UiText.Text("Process All Textures"), GUILayout.Height(40)))
            {
                ProcessTextures();
            }
            GUI.enabled = true;
            
            // Progress bar
            if (processing)
            {
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), progress, UiText.Format("Processing: {0}%", (int)(progress * 100)));
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
            Material material = null;
            try
            {
                material = new Material(selectedShader);
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
            }
            finally
            {
                if (material != null) Object.DestroyImmediate(material);
                processing = false;
            }

            progress = 1f;
            
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(UiText.Text("Batch Processing Complete"),
                UiText.Format("Processed {0} textures successfully!", textures.Count), UiText.Text("OK"));
        }
        
        private void ProcessSingleTexture(Texture2D sourceTexture, Material material)
        {
            string fileName = $"{sourceTexture.name}_processed.png";
            string fullPath = Path.Combine(outputPath, fileName);
            TextureExportUtility.SaveMaterialPng(sourceTexture, material, fullPath);
        }
    }
}
