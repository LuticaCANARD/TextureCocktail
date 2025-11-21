using NUnit.Framework.Internal;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    public enum FeatureExtractionMode
    {
        EdgeDetection,
        CannyEdge,
        ColorSegmentation,
        HistogramEnhance
    }
    
    public enum AnalysisChannel
    {
        RGB = 0,
        Red = 1,
        Green = 2,
        Blue = 3,
        Luminance = 4
    }
    
    public class FeatureExtractor : TextureCocktailContent
    {
        public override bool UseDefaultLayout { get => false; }
        public override bool ShaderUpdateDefaultAction { get => false; }
        public override string[] DontWantDisplayPropertyName { get => new string[] { "_AnalysisChannel" }; }
        private int _passOrder = 1;
        public override int PassOrder { get => _passOrder; }

        private FeatureExtractionMode extractionMode = FeatureExtractionMode.EdgeDetection;
        private AnalysisChannel analysisChannel = AnalysisChannel.RGB;
        private bool _showSettings = true;
        private bool _showPreview = true;
        private bool _showAdvanced = false;
        
        // For histogram visualization
        private Texture2D histogramTexture;
        private int[] histogramData = new int[256];
        private bool _showHistogram = false;
        
        public override void Initialize(TextureCocktail baseWindow)
        {
            base.Initialize(baseWindow);
            InitializeHistogram();
        }
        
        private void InitializeHistogram()
        {
            histogramTexture = new Texture2D(256, 100, TextureFormat.RGBA32, false);
            histogramTexture.filterMode = (UnityEngine.FilterMode)FilterMode.Point;
        }
        
        public override void OnGUI()
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            
            // Title
            GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("feature_extractor_title"), EditorStyles.boldLabel);
            GUILayout.Space(5);
            
            // Extraction Mode Selection
            GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("extraction_mode"), EditorStyles.boldLabel);
            var newMode = (FeatureExtractionMode)EditorGUILayout.EnumPopup(
                LanguageDisplayer.Instance.GetTranslatedLanguage("mode"), extractionMode);
            
            if (newMode != extractionMode)
            {
                extractionMode = newMode;
                ApplyExtractionMode();
            }
            
            // Display mode description
            EditorGUILayout.HelpBox(GetModeDescription(), MessageType.Info);
            
            GUILayout.Space(10);
            
            // Mode-specific settings
            _showSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showSettings, 
                LanguageDisplayer.Instance.GetTranslatedLanguage("extraction_settings"));
            if (_showSettings)
            {
                EditorGUI.indentLevel++;
                
                switch (extractionMode)
                {
                    case FeatureExtractionMode.EdgeDetection:
                    case FeatureExtractionMode.CannyEdge:
                        ShowEdgeDetectionSettings();
                        break;
                    case FeatureExtractionMode.ColorSegmentation:
                        ShowColorSegmentationSettings();
                        break;
                    case FeatureExtractionMode.HistogramEnhance:
                        ShowHistogramSettings();
                        break;
                }
                
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            // Analysis Channel (for histogram mode)
            if (extractionMode == FeatureExtractionMode.HistogramEnhance)
            {
                GUILayout.Space(5);
                GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("analysis_channel"), EditorStyles.boldLabel);
                var newChannel = (AnalysisChannel)EditorGUILayout.EnumPopup(
                    LanguageDisplayer.Instance.GetTranslatedLanguage("channel"), analysisChannel);
                
                if (newChannel != analysisChannel)
                {
                    analysisChannel = newChannel;
                    UpdateAnalysisChannel();
                }
            }
            
            // Histogram Visualization
            if (extractionMode == FeatureExtractionMode.HistogramEnhance)
            {
                _showHistogram = EditorGUILayout.BeginFoldoutHeaderGroup(_showHistogram, 
                    LanguageDisplayer.Instance.GetTranslatedLanguage("histogram_view"));
                if (_showHistogram)
                {
                    DrawHistogram();
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }
            
            // Common shader properties
            GUILayout.Space(10);
            _showAdvanced = EditorGUILayout.BeginFoldoutHeaderGroup(_showAdvanced, 
                LanguageDisplayer.Instance.GetTranslatedLanguage("advanced_settings"));
            if (_showAdvanced)
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
                    if (extractionMode == FeatureExtractionMode.HistogramEnhance)
                    {
                        CalculateHistogram();
                    }
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
            switch (extractionMode)
            {
                case FeatureExtractionMode.EdgeDetection:
                    return LanguageDisplayer.Instance.GetTranslatedLanguage("mode_edge_desc");
                case FeatureExtractionMode.CannyEdge:
                    return LanguageDisplayer.Instance.GetTranslatedLanguage("mode_canny_desc");
                case FeatureExtractionMode.ColorSegmentation:
                    return LanguageDisplayer.Instance.GetTranslatedLanguage("mode_color_seg_desc");
                case FeatureExtractionMode.HistogramEnhance:
                    return LanguageDisplayer.Instance.GetTranslatedLanguage("mode_histogram_desc");
                default:
                    return "";
            }
        }
        
        private void ShowEdgeDetectionSettings()
        {
            var material = GetMaterial();
            if (material == null) return;
            
            EditorGUI.BeginChangeCheck();
            
            GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("edge_detection_settings"));
            
            if (material.HasProperty("_EdgeSensitivity"))
            {
                float sensitivity = material.GetFloat("_EdgeSensitivity");
                sensitivity = EditorGUILayout.Slider("Edge Sensitivity", sensitivity, 0, 1);
                material.SetFloat("_EdgeSensitivity", sensitivity);
                EditorGUILayout.HelpBox(LanguageDisplayer.Instance.GetTranslatedLanguage("edge_sensitivity_help"), MessageType.None);
            }
            if (material.HasProperty("_FeatureColor"))
            {
                Color color_ = material.GetColor("_FeatureColor");
                color_ = EditorGUILayout.ColorField(color_);
                GUILayout.Label("Feature Color");
                material.SetColor("_FeatureColor", color_);
                EditorGUILayout.HelpBox(LanguageDisplayer.Instance.GetTranslatedLanguage("edge_color_help"), MessageType.None);

            }


            if (EditorGUI.EndChangeCheck())
            {
                this.OnShaderValueChanged();
            }
        }
        
        private void ShowColorSegmentationSettings()
        {
            var material = GetMaterial();
            if (material == null) return;
            
            EditorGUI.BeginChangeCheck();
            
            GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("color_segmentation_settings"));
            
            if (material.HasProperty("_ColorThreshold"))
            {
                float threshold = material.GetFloat("_ColorThreshold");
                threshold = EditorGUILayout.Slider("Quantization Level", threshold, 0, 1);
                material.SetFloat("_ColorThreshold", threshold);
            }
            
            EditorGUILayout.HelpBox(LanguageDisplayer.Instance.GetTranslatedLanguage("color_threshold_help"), MessageType.None);

            if (EditorGUI.EndChangeCheck())
            {
                this.OnShaderValueChanged();
            }
        }
        
        private void ShowHistogramSettings()
        {
            var material = GetMaterial();
            if (material == null) return;
            
            GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("histogram_settings"));
            EditorGUILayout.HelpBox(LanguageDisplayer.Instance.GetTranslatedLanguage("histogram_help"), MessageType.None);
        }
        
        private void ApplyExtractionMode()
        {
            var material = GetMaterial();
            if (material == null) return;
            
            // Get source and preview textures
            var sourceTex = GetSourceTexture();
            var preview = GetPreviewTexture();

            if (sourceTex == null || preview == null)
            {
                baseWindow.CompileShader();
                return;
            }
            
            // Select the correct pass based on extraction mode
            // Pass 0: Edge Detection (Sobel)
            // Pass 1: Canny Edge
            // Pass 2: Color Segmentation
            // Pass 3: Histogram Enhancement
            int pass = (int)extractionMode;
            _passOrder = pass;
            // Render using the specific pass
            RenderTexture.active = preview;
            Graphics.Blit(sourceTex, preview, material, pass);
            RenderTexture.active = null;

        }

        private void UpdateAnalysisChannel()
        {
            var material = GetMaterial();
            if (material == null) return;
            
            if (material.HasProperty("_AnalysisChannel"))
            {
                material.SetInt("_AnalysisChannel", (int)analysisChannel);
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
        
        private RenderTexture GetPreviewTexture()
        {
            var type = baseWindow.GetType();
            var field = type.GetField("_preview", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(baseWindow) as RenderTexture;
        }
        
        private Texture GetSourceTexture()
        {
            var type = baseWindow.GetType();
            var field = type.GetField("_targetTexture", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(baseWindow) as Texture;
        }
        
        private void CalculateHistogram()
        {
            // Reset histogram
            for (int i = 0; i < histogramData.Length; i++)
            {
                histogramData[i] = 0;
            }
            
            var previewTex = GetPreviewTexture();
            if (previewTex == null) return;
            
            // Read pixels from preview texture
            RenderTexture.active = previewTex;
            Texture2D readTex = new Texture2D(previewTex.width, previewTex.height, TextureFormat.RGBA32, false);
            readTex.ReadPixels(new Rect(0, 0, previewTex.width, previewTex.height), 0, 0);
            readTex.Apply();
            RenderTexture.active = null;
            
            Color[] pixels = readTex.GetPixels();
            
            // Calculate histogram based on analysis channel
            foreach (var pixel in pixels)
            {
                float value = 0;
                switch (analysisChannel)
                {
                    case AnalysisChannel.RGB:
                        value = (pixel.r + pixel.g + pixel.b) / 3.0f;
                        break;
                    case AnalysisChannel.Red:
                        value = pixel.r;
                        break;
                    case AnalysisChannel.Green:
                        value = pixel.g;
                        break;
                    case AnalysisChannel.Blue:
                        value = pixel.b;
                        break;
                    case AnalysisChannel.Luminance:
                        value = 0.299f * pixel.r + 0.587f * pixel.g + 0.114f * pixel.b;
                        break;
                }
                
                int bin = Mathf.Clamp(Mathf.FloorToInt(value * 255), 0, 255);
                histogramData[bin]++;
            }
            
            Object.DestroyImmediate(readTex);
            
            // Update histogram texture
            UpdateHistogramTexture();
        }
        
        private void UpdateHistogramTexture()
        {
            if (histogramTexture == null) return;
            
            // Find max value for normalization
            int maxValue = 0;
            foreach (var value in histogramData)
            {
                if (value > maxValue) maxValue = value;
            }
            
            if (maxValue == 0) return;
            
            // Clear texture
            Color[] pixels = new Color[256 * 100];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.black;
            }
            
            // Draw histogram
            for (int x = 0; x < 256; x++)
            {
                int height = Mathf.FloorToInt((float)histogramData[x] / maxValue * 99);
                for (int y = 0; y <= height; y++)
                {
                    pixels[y * 256 + x] = GetChannelColor();
                }
            }
            
            histogramTexture.SetPixels(pixels);
            histogramTexture.Apply();
        }
        
        private Color GetChannelColor()
        {
            switch (analysisChannel)
            {
                case AnalysisChannel.Red:
                    return Color.red;
                case AnalysisChannel.Green:
                    return Color.green;
                case AnalysisChannel.Blue:
                    return Color.blue;
                default:
                    return Color.white;
            }
        }
        
        private void DrawHistogram()
        {
            if (histogramTexture != null)
            {
                GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("histogram"), EditorStyles.boldLabel);
                GUILayout.Box(histogramTexture, GUILayout.Width(256), GUILayout.Height(100));
            }
            else
            {
                GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("histogram_not_available"));
            }
        }
        
        public override void OnShaderValueChanged()
        {
            ApplyExtractionMode();
        }
    }
}
