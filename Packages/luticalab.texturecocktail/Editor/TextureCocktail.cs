using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// ColorMakerByShader is a MonoBehaviour that can be used to create colors using shaders.
    /// </summary>
    public class TextureCocktail : EditorWindow
    {
        [MenuItem("LuticaLab/TextureCocktail")]
        public static void ShowWindow()
        {
            GetWindow<TextureCocktail>("TextureCocktail");
        }
        private Shader _shader;
        private Texture2D _targetTexture;
        private RenderTexture _preview;
        private Material _calcMaterial;
        private bool _valueChanged = false;
        string[] _shaderKeys;
        private MethodInfo _getShaderKeywordsMethod;
        private bool _shaderOptionOnOff = false;
        private TextureCocktailContent _shaderWindow;
        private bool _shaderChanged = false;
        private readonly Dictionary<string,bool> _keywordOnOff = new Dictionary<string, bool>();
        const string _mainTexProperty = "_MainTex";

        private class PolygonMaskShape
        {
            public List<Vector2> Points = new List<Vector2>();
            public bool Closed = false;
        }

        private enum PolygonInteractionMode { None, DraggingVertex, DraggingShape }

        private readonly List<PolygonMaskShape> _polygonMaskShapes = new List<PolygonMaskShape>();
        private bool _polygonMaskEnabled = true;
        private PolygonInteractionMode _polygonInteractionMode = PolygonInteractionMode.None;
        private int _draggingPolygonIndex = -1;
        private int _draggingPolygonPoint = -1;
        private Vector2 _polygonDragPreviousMaskPos;
        private bool _polygonMaskDirty = false;
        private Texture2D _polygonMaskTexture;
        private Material _polygonCompositeMaterial;
        private const string _polygonCompositeShaderName = "Hidden/TextureCocktail/PolygonMaskComposite";
        private const RenderTextureFormat _previewTextureFormat = RenderTextureFormat.ARGB32;
        private const float _polygonVertexHitDistance = 10f;
        private Color32[] _maskPixels;

        virtual protected bool ShaderUpdateDefaultAction
        {
            get => true;
        }

        public Texture2D TargetTexture => _targetTexture;
        public RenderTexture PreviewTexture => _preview;
        public Texture2D PolygonMaskTexture => _polygonMaskTexture;
        public bool HasActivePolygonMask => _polygonMaskEnabled && HasAnyClosedPolygon();
        public event System.Action OnPreviewUpdated;

        // Quick shader selection
        private static readonly string[] _quickShaderNames = new string[]
        {
            "None",
            "FastImageConverter",
            "FeatureExtractor", 
            "ColorCorrection",
            "TextureBlender",
            "ArtisticEffects",
            "ImageFilter (HSVMover)",
            "ImageSync",
            "InverseFilter",
            "NormalMapGenerator"
        };
        
        private static readonly string[] _quickShaderPaths = new string[]
        {
            "",
            "Hidden/FastImageConverter",
            "Hidden/FeatureExtractor",
            "Hidden/ColorCorrection",
            "Hidden/TextureBlender",
            "Luticalab/ArtisticEffects",
            "Luticalab/ImageFilter",
            "Luticalab/ImageSync",
            "Luticalab/ImageEffect",
            "Luticalab/NormalMapGenerator"
        };
        
        private int _selectedQuickShaderIndex = 0;
        
        private void OnGUI()
        {
            GUILayout.Label("TextureCocktail", EditorStyles.boldLabel);
            
            // Quick shader selector
            GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("quick_shader_select"), EditorStyles.boldLabel);
            int newShaderIndex = EditorGUILayout.Popup(LanguageDisplayer.Instance.GetTranslatedLanguage("select_shader"), _selectedQuickShaderIndex, _quickShaderNames);
            if (newShaderIndex != _selectedQuickShaderIndex)
            {
                _selectedQuickShaderIndex = newShaderIndex;
                if (newShaderIndex > 0)
                {
                    var shader = Shader.Find(_quickShaderPaths[newShaderIndex]);
                    if (shader != null)
                    {
                        OnShaderChange(shader);
                    }
                }
                else
                {
                    OnShaderChange(null);
                }
            }
            
            GUILayout.Space(5);

            // Shader field - clickable when assigned, selectable when not
            EditorGUILayout.BeginHorizontal();
            if (_shader != null)
            {
                // Show as read-only label with click functionality
                EditorGUILayout.LabelField(LanguageDisplayer.Instance.GetTranslatedLanguage("apply_shader"), _shader.name);
                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    OnShaderChange(null);
                }
            }
            else
            {
                var changed = (Shader)EditorGUILayout.ObjectField(
                    LanguageDisplayer.Instance.GetTranslatedLanguage("apply_shader"), _shader, typeof(Shader), false);
                OnShaderChange(changed);
            }
            EditorGUILayout.EndHorizontal();
            
            // Target texture field with view button
            EditorGUILayout.BeginHorizontal();
            var changedTexture = (Texture2D)EditorGUILayout.ObjectField(
                LanguageDisplayer.Instance.GetTranslatedLanguage("target_texture"), _targetTexture, typeof(Texture2D), false);
            
            // Add a button to view the original texture
            if (_targetTexture != null)
            {
                if (GUILayout.Button(LanguageDisplayer.Instance.GetTranslatedLanguage("view"), GUILayout.Width(50)))
                {
                    ImageViewerWindow.ShowWindow(_targetTexture, _targetTexture.name);
                }
            }
            EditorGUILayout.EndHorizontal();
            
            OnTextureChanged(changedTexture);

            if (_valueChanged)
            {
                OnShaderValueChange();
            }

            if (_shaderWindow != null) {
                _shaderWindow.OnGUI();
            } else {
                ShowShadersWindow();
            }
        }
        //------------- APIs -------------
        public void DisplayPassedIamge()
        {
            // Create a clickable image preview
            Rect previewRect = GUILayoutUtility.GetRect(200, 200);
            Event currentEvent = Event.current;
            
            if (_preview != null)
            {
                // Draw the preview texture
                GUI.DrawTexture(previewRect, _preview, ScaleMode.ScaleToFit);
                Rect imageDrawRect = GetImageDrawRect(previewRect, _preview.width, _preview.height);
                DrawPolygonMaskOverlay(imageDrawRect);
                HandlePolygonMaskInteraction(imageDrawRect, currentEvent);
                
                // Add a subtle border
                GUI.Box(previewRect, "", EditorStyles.helpBox);
                
                // Show click hint on hover and handle click
                if (previewRect.Contains(Event.current.mousePosition))
                {
                    EditorGUI.DrawRect(new Rect(previewRect.x, previewRect.y + previewRect.height - 20, previewRect.width, 20), 
                        new Color(0, 0, 0, 0.7f));
                    string localizedHint = LanguageDisplayer.Instance.GetTranslatedLanguage("click_to_view_fullsize");
                    GUI.Label(new Rect(previewRect.x, previewRect.y + previewRect.height - 20, previewRect.width, 20), 
                        $"{localizedHint} ×2", 
                        new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, normal = new GUIStyleState { textColor = Color.white } });
                    
                    // Handle mouse double click
                    if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && currentEvent.clickCount == 2)
                    {
                        ImageViewerWindow.ShowWindow(_preview, _targetTexture != null ? _targetTexture.name + " - Preview" : "Preview");
                        currentEvent.Use();
                    }
                    
                    Repaint();
                }

                EditorGUILayout.BeginHorizontal();
                bool newPolygonMaskEnabled = EditorGUILayout.ToggleLeft("Enable Polygon Mask", _polygonMaskEnabled);
                if (newPolygonMaskEnabled != _polygonMaskEnabled)
                {
                    _polygonMaskEnabled = newPolygonMaskEnabled;
                    CompileShader();
                }
                if (GUILayout.Button("Finish Polygon", GUILayout.Width(120)))
                {
                    if (FinalizeOpenPolygon())
                    {
                        CompileShader();
                    }
                }
                if (GUILayout.Button("Reset Polygons", GUILayout.Width(120)))
                {
                    ClearPolygonMask();
                    CompileShader();
                }
                if (GUILayout.Button(LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_open"), GUILayout.Width(160)))
                {
                    MeshPreviewWindow.ShowWindowFor(this);
                }
                EditorGUILayout.EndHorizontal();
                if (_polygonMaskShapes.Count > 0)
                {
                    int closedCount = 0;
                    int openCount = 0;
                    foreach (var shape in _polygonMaskShapes)
                    {
                        if (shape.Closed) closedCount++; else openCount++;
                    }
                    EditorGUILayout.LabelField($"Polygons: {closedCount} closed, {openCount} open. Drag inside a polygon to move it.", EditorStyles.miniLabel);
                }
            }
            else
            {
                GUI.Box(previewRect, LanguageDisplayer.Instance.GetTranslatedLanguage("no_preview_available"));
            }
        }
        public void DisplayShaderOptions()
        {
            if (_shaderKeys == null || _shaderKeys.Length == 0)
            {
                GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("no_shader_options"));
                return;
            }
            
            EditorGUILayout.BeginVertical(
                GUILayout.MaxWidth(500)
            );
            foreach (var keyword in _shaderKeys)
            {
                if (!_keywordOnOff.ContainsKey(keyword))
                    _keywordOnOff[keyword] = false;

                EditorGUILayout.BeginHorizontal();
                _keywordOnOff[keyword] = EditorGUILayout.ToggleLeft(keyword, _keywordOnOff[keyword]);
                EditorGUILayout.EndHorizontal();
                ApplyShaderDict(keyword);
            }
            EditorGUILayout.EndVertical();
            if (GUILayout.Button(LanguageDisplayer.Instance.GetTranslatedLanguage("apply_shader_execute")))
                CompileShader();
        }
        public void ShowShaderInfo()
        {
            if (_shader == null)
            {
                return;
            }
            
            HashSet<string> dontWantDisplayShaderProperties = new HashSet<string>();
            if (_shaderWindow != null && _shaderWindow.DontWantDisplayPropertyName != null)
            {
                for (int i = 0; i < _shaderWindow.DontWantDisplayPropertyName.Length; i++)
                {
                    dontWantDisplayShaderProperties.Add(_shaderWindow.DontWantDisplayPropertyName[i]);
                }
            }
            for (int i = 0; i < ShaderUtil.GetPropertyCount(_shader); i++)
            {
                string propertyName = ShaderUtil.GetPropertyName(_shader, i);
                string displayName = ShaderUtil.GetPropertyDescription(_shader, i);
                if (propertyName == _mainTexProperty)
                {
                    continue;
                }
                if(_shaderWindow != null && dontWantDisplayShaderProperties.Contains(propertyName))
                {
                    continue;
                }
                GUILayout.BeginHorizontal();

                ShaderUtil.ShaderPropertyType propertyType = ShaderUtil.GetPropertyType(_shader, i);

                switch (propertyType)
                {
                    case ShaderUtil.ShaderPropertyType.Color:
                        GUILayout.Label($"{displayName} ({propertyName}, {propertyType})");
                        Color colorValue = _calcMaterial.GetColor(propertyName);
                        Color newColorValue = EditorGUILayout.ColorField(colorValue);
                        if (newColorValue != colorValue)
                        {
                            _calcMaterial.SetColor(propertyName, newColorValue);
                            _valueChanged = true;
                        }
                        break;
                    case ShaderUtil.ShaderPropertyType.Float:
                        GUILayout.Label($"{displayName} ({propertyName}, {propertyType})");
                        float floatValue = _calcMaterial.GetFloat(propertyName);
                        float newFloatValue = EditorGUILayout.FloatField(floatValue);
                        if (newFloatValue != floatValue)
                        {
                            _calcMaterial.SetFloat(propertyName, newFloatValue);
                            _valueChanged = true;
                        }
                        break;
                    case ShaderUtil.ShaderPropertyType.Range:
                        GUILayout.Label($"{displayName} ({propertyName}, {propertyType})");
                        float rangeValue = _calcMaterial.GetFloat(propertyName);
                        float newRangeValue = EditorGUILayout.Slider(rangeValue, 0f, 1f);
                        if (newRangeValue != rangeValue)
                        {
                            _calcMaterial.SetFloat(propertyName, newRangeValue);
                            _valueChanged = true;
                        }
                        break;
                    case ShaderUtil.ShaderPropertyType.Vector:
                        EditorGUILayout.BeginVertical();
                        GUILayout.Label($"{displayName} ({propertyName}, {propertyType})");
                        Vector4 vectorValue = _calcMaterial.GetVector(propertyName);
                        Vector4 newVectorValue = EditorGUILayout.Vector4Field(propertyName, vectorValue);
                        EditorGUILayout.EndVertical();
                        if (newVectorValue != vectorValue)
                        {
                            _calcMaterial.SetVector(propertyName, newVectorValue);
                            _valueChanged = true;
                        }
                        break;
                    case ShaderUtil.ShaderPropertyType.TexEnv:
                        GUILayout.Label($"{displayName} ({propertyName}, {propertyType})");
                        Texture textureValue = _calcMaterial.GetTexture(propertyName);
                        Texture newTextureValue = (Texture)EditorGUILayout.ObjectField(textureValue, typeof(Texture), false);
                        if (newTextureValue != textureValue)
                        {
                            _calcMaterial.SetTexture(propertyName, newTextureValue);
                            _valueChanged = true;
                        }
                        break;
                    case ShaderUtil.ShaderPropertyType.Int:
                        GUILayout.Label($"{displayName} ({propertyName}, {propertyType})");

                        int intValue = _calcMaterial.GetInt(propertyName);
                        int newIntValue = EditorGUILayout.IntField(intValue);
                        if (newIntValue != intValue)
                        {
                            _calcMaterial.SetInt(propertyName, newIntValue);
                            _valueChanged = true;
                        }
                        break;
                    default:
                        GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("unsupported_property"));
                        break;
                }
                GUILayout.EndHorizontal();
            }
        }
        public void SaveTexture()
        {
            string path = EditorUtility.SaveFilePanel(
                LanguageDisplayer.Instance.GetTranslatedLanguage("save_texture")
                , "Assets"
                , _targetTexture.name + ".png", "png"
            );
            if (!string.IsNullOrEmpty(path))
            {
                RenderTexture.active = _preview;
                Texture2D textureToSave = new(_preview.width, _preview.height, TextureFormat.RGBA32, false);
                textureToSave.ReadPixels(new Rect(0, 0, _preview.width, _preview.height), 0, 0);
                textureToSave.Apply();
                System.IO.File.WriteAllBytes(path, textureToSave.EncodeToPNG());

                AssetDatabase.Refresh();
                var relativePath = "Assets" + path.Substring(Application.dataPath.Length);
                var importer = AssetImporter.GetAtPath(relativePath);
                if (importer != null)
                {
                    if( importer is TextureImporter)
                    {
                        var textureImporter = importer as TextureImporter;
                        Debug.Log("Setting texture import settings for: " + path);
                        textureImporter.npotScale = TextureImporterNPOTScale.None;
                        textureImporter.SaveAndReimport();
                    }

                }
                string reply = string.Format(LanguageDisplayer.Instance.GetTranslatedLanguage("save_texture_success"), path);
                Debug.Log(reply);
            }
        }
        public void OpenABTestWindow()
        {
            if (_targetTexture == null)
            {
                Debug.LogWarning(LanguageDisplayer.Instance.GetTranslatedLanguage("ab_no_target"));
                return;
            }
            if (_preview == null)
            {
                EnsurePreviewTexture();
                if (_calcMaterial != null)
                {
                    CompileShader();
                }
            }
            TextureABTestWindow.ShowWindow(_targetTexture, _preview, _targetTexture.name);
        }
        public void SetMaterialKeyword(string keyword, bool value)
        {
            // Update dictionary if keyword exists in it
            if (_keywordOnOff.ContainsKey(keyword))
            {
                _keywordOnOff[keyword] = value;
            }
            
            // Always apply to material if material exists
            if (_calcMaterial != null)
            {
                if (value)
                    _calcMaterial.EnableKeyword(keyword);
                else
                    _calcMaterial.DisableKeyword(keyword);
            }
        }
        public void CompileShader()
        {
            if (_calcMaterial != null && _targetTexture != null)
            {
                EnsurePreviewTexture();
                _calcMaterial.shader = _shader;
                foreach (var keyword in _keywordOnOff)
                {
                    ApplyShaderDict(keyword.Key);
                }
                ShaderUtil.CompilePass(_calcMaterial, 0);
                if (_polygonMaskEnabled && HasAnyClosedPolygon())
                {
                    UpdatePolygonMaskTexture();
                    EnsurePolygonCompositeMaterial();
                    if (_polygonCompositeMaterial != null && _polygonMaskTexture != null)
                    {
                        RenderTexture processedTexture = RenderTexture.GetTemporary(_targetTexture.width, _targetTexture.height, 0, _previewTextureFormat);
                        try
                        {
                            Graphics.Blit(_targetTexture, processedTexture, _calcMaterial);
                            _polygonCompositeMaterial.SetTexture("_OriginalTex", _targetTexture);
                            _polygonCompositeMaterial.SetTexture("_ProcessedTex", processedTexture);
                            _polygonCompositeMaterial.SetTexture("_MaskTex", _polygonMaskTexture);
                            Graphics.Blit(null, _preview, _polygonCompositeMaterial);
                        }
                        finally
                        {
                            RenderTexture.ReleaseTemporary(processedTexture);
                        }
                    }
                    else
                    {
                        RenderTexture.active = _preview;
                        Graphics.Blit(_targetTexture, _preview, _calcMaterial);
                        RenderTexture.active = null;
                    }
                }
                else
                {
                    RenderTexture.active = _preview;
                    Graphics.Blit(_targetTexture, _preview, _calcMaterial);
                    RenderTexture.active = null;
                }
                OnPreviewUpdated?.Invoke();
            }
            else
            {
                Debug.LogWarning(
                    LanguageDisplayer.Instance.GetTranslatedLanguage("apply_shader_execute_error_not_create")
                );
            }
        }
        // ----------------- Basical GUI ------------------
        private void ShowShadersWindow()
        {
            if (_shader != null)
            {
                ShowShaderInfo();
                if (_targetTexture != null)
                {
                    if (_calcMaterial != null)
                    {
                        DisplayPassedIamge();
                        _shaderOptionOnOff = EditorGUILayout.BeginFoldoutHeaderGroup(_shaderOptionOnOff, LanguageDisplayer.Instance.GetTranslatedLanguage("shader_compile_options"));
                        if (_shaderOptionOnOff) DisplayShaderOptions();
                        EditorGUILayout.EndFoldoutHeaderGroup();
                        EditorGUILayout.BeginHorizontal();
                        if (GUILayout.Button(LanguageDisplayer.Instance.GetTranslatedLanguage("save_texture")))
                            SaveTexture();
                        using (new EditorGUI.DisabledScope(_preview == null || _targetTexture == null))
                        {
                            if (GUILayout.Button(LanguageDisplayer.Instance.GetTranslatedLanguage("ab_open_button"), GUILayout.Width(140)))
                                OpenABTestWindow();
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    else
                    {
                        GUILayout.Label(
                            LanguageDisplayer.Instance.GetTranslatedLanguage("material_is_not_created"),
                            EditorStyles.boldLabel
                        );
                    }
                }
            }
        }
        private void ApplyShaderDict(string keyword)
        {
            if (!_keywordOnOff.ContainsKey(keyword))
                return;
                
            if (_keywordOnOff[keyword]) _calcMaterial.EnableKeyword(keyword);
            else _calcMaterial.DisableKeyword(keyword);
        }
        //--------------------- Actions ---------------------
        private void OnShaderChange(Shader changeTo)
        {
            if (_shader == changeTo) return;
            _shader = changeTo;
            
            // Clean up shader window when shader is set to null
            if (_shader == null)
            {
                _shaderWindow = null;
                _calcMaterial = null;
                _keywordOnOff.Clear();
                _shaderKeys = null;
                return;
            }
            
            _calcMaterial = new Material(_shader);
            Debug.Log($"Shader changed to: {_shader.name}");
            string shaderLastName = _shader.name.Split('/')[^1];
            _shaderWindow = LoadShaderWindow(shaderLastName);
            if (_shaderWindow != null)
            {
                _shaderWindow.Initialize(this);
            }
            if (_targetTexture != null)
            {
                _calcMaterial.SetTexture(_mainTexProperty, _targetTexture);
            }
            _keywordOnOff.Clear();
            if (this._getShaderKeywordsMethod == null)
            {
                _getShaderKeywordsMethod = typeof(ShaderUtil).GetMethod("GetShaderGlobalKeywords", BindingFlags.Static | BindingFlags.NonPublic);
            }
            string[] keywords = (string[])_getShaderKeywordsMethod.Invoke(null, new object[] { _shader });
            _shaderKeys = keywords;
            foreach (var keyword in _shaderKeys)
            {
                _keywordOnOff[keyword] = false; // Initialize all keywords to false
            }
            _valueChanged = true;
        }
        /// <summary>
        /// Found shader window by name.
        /// </summary>
        /// <param name="shaderName">
        ///     shader name with namespace prefix, for example "ImageSync"
        ///     window script most be in LuticaLab.TextureCocktail namespace
        /// </param>
        /// <returns></returns>
        private TextureCocktailContent LoadShaderWindow(string shaderName)
        {
            var foundType = Type.GetType("LuticaLab.TextureCocktail." + shaderName);
            if (foundType == null)
            {
                Debug.LogWarning($"Shader window type '{shaderName}' not found. Ensure it is in the correct namespace and assembly.");
                return null;
            }
            if (foundType.IsSubclassOf(typeof(TextureCocktailContent)))
            {
                var shaderWindow = (TextureCocktailContent)CreateInstance(foundType);
                return shaderWindow;
            }
            else
            {
                Debug.LogWarning($"Shader window type '{shaderName}' is not a subclass of TextureCocktailContent.");
                return null;
            }
        }
        private void OnTextureChanged(Texture2D newTexture)
        {
            if (_targetTexture == newTexture) return;
            _targetTexture = newTexture;
            if (_calcMaterial != null && _targetTexture != null)
            {
                _calcMaterial.SetTexture(_mainTexProperty, _targetTexture);
            }
            _valueChanged = true;
        }
        public void OnShaderValueChange()
        {
            _valueChanged = false;

            if(_shaderWindow != null && _shaderWindow.ShaderUpdateDefaultAction == false)
            {
                _shaderWindow.OnShaderValueChanged();
                return;
            }
            if (_calcMaterial != null)
            {
                _calcMaterial.SetTexture(_mainTexProperty, _targetTexture);
            }
            ShaderUtil.CompilePass(_calcMaterial, 0);
            if(_targetTexture == null)
            {
                return;
            }
            EnsurePreviewTexture();
            if(_shaderWindow != null)
            {
                _shaderWindow.OnShaderValueChanged();
            }
            CompileShader();
        }

        private Rect GetImageDrawRect(Rect previewRect, int textureWidth, int textureHeight)
        {
            float scaleX = previewRect.width / textureWidth;
            float scaleY = previewRect.height / textureHeight;
            float scale = Mathf.Min(scaleX, scaleY);
            float scaledWidth = textureWidth * scale;
            float scaledHeight = textureHeight * scale;
            float x = previewRect.x + (previewRect.width - scaledWidth) * 0.5f;
            float y = previewRect.y + (previewRect.height - scaledHeight) * 0.5f;
            return new Rect(x, y, scaledWidth, scaledHeight);
        }

        private void DrawPolygonMaskOverlay(Rect imageDrawRect)
        {
            if (_polygonMaskShapes.Count == 0)
            {
                return;
            }

            Handles.BeginGUI();
            Color previousColor = Handles.color;
            Color closedColor = _polygonMaskEnabled ? new Color(0.1f, 1f, 0.4f, 0.95f) : new Color(0.8f, 0.8f, 0.8f, 0.85f);
            Color openColor = _polygonMaskEnabled ? new Color(1f, 0.85f, 0.2f, 0.95f) : new Color(0.7f, 0.7f, 0.7f, 0.75f);
            Color activeShapeColor = _polygonMaskEnabled ? new Color(0.2f, 0.7f, 1f, 1f) : closedColor;

            for (int shapeIndex = 0; shapeIndex < _polygonMaskShapes.Count; shapeIndex++)
            {
                var shape = _polygonMaskShapes[shapeIndex];
                if (shape.Points.Count == 0)
                {
                    continue;
                }
                bool isActiveDragShape = _polygonInteractionMode == PolygonInteractionMode.DraggingShape && _draggingPolygonIndex == shapeIndex;
                Handles.color = isActiveDragShape ? activeShapeColor : (shape.Closed ? closedColor : openColor);

                for (int i = 0; i < shape.Points.Count; i++)
                {
                    Vector2 currentPoint = MaskPointToGUI(shape.Points[i], imageDrawRect);
                    Vector2 nextPoint;
                    if (i == shape.Points.Count - 1)
                    {
                        if (!shape.Closed)
                        {
                            continue;
                        }
                        nextPoint = MaskPointToGUI(shape.Points[0], imageDrawRect);
                    }
                    else
                    {
                        nextPoint = MaskPointToGUI(shape.Points[i + 1], imageDrawRect);
                    }
                    Handles.DrawAAPolyLine(2.0f, currentPoint, nextPoint);
                }

                for (int i = 0; i < shape.Points.Count; i++)
                {
                    Vector2 point = MaskPointToGUI(shape.Points[i], imageDrawRect);
                    float size = i == 0 && !shape.Closed ? 5f : 4f;
                    Handles.DrawSolidDisc(point, Vector3.forward, size);
                }
            }

            Handles.color = previousColor;
            Handles.EndGUI();
        }

        private void HandlePolygonMaskInteraction(Rect imageDrawRect, Event currentEvent)
        {
            if (!_polygonMaskEnabled)
            {
                ResetPolygonInteractionState();
                return;
            }

            if (_polygonInteractionMode != PolygonInteractionMode.None)
            {
                if (currentEvent.type == EventType.MouseDrag && currentEvent.button == 0)
                {
                    ContinuePolygonDrag(currentEvent.mousePosition, imageDrawRect);
                    currentEvent.Use();
                    return;
                }
                if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0)
                {
                    if (_polygonMaskDirty)
                    {
                        CompileShader();
                    }
                    ResetPolygonInteractionState();
                    currentEvent.Use();
                    return;
                }
            }

            if (!imageDrawRect.Contains(currentEvent.mousePosition))
            {
                return;
            }

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 1)
            {
                if (DeletePolygonAtMouse(currentEvent.mousePosition, imageDrawRect))
                {
                    CompileShader();
                }
                else
                {
                    ClearPolygonMask();
                    CompileShader();
                }
                currentEvent.Use();
                return;
            }

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
            {
                int hitShapeIndex, hitVertexIndex;
                if (TryGetNearestPolygonVertex(currentEvent.mousePosition, imageDrawRect, _polygonVertexHitDistance, out hitShapeIndex, out hitVertexIndex))
                {
                    var hitShape = _polygonMaskShapes[hitShapeIndex];
                    if (!hitShape.Closed && hitVertexIndex == 0 && hitShape.Points.Count >= 3)
                    {
                        hitShape.Closed = true;
                        CompileShader();
                        currentEvent.Use();
                        return;
                    }
                    if (hitShape.Closed)
                    {
                        _polygonInteractionMode = PolygonInteractionMode.DraggingVertex;
                        _draggingPolygonIndex = hitShapeIndex;
                        _draggingPolygonPoint = hitVertexIndex;
                        currentEvent.Use();
                        return;
                    }
                }

                int openIndex = GetOpenPolygonIndex();
                if (openIndex >= 0)
                {
                    _polygonMaskShapes[openIndex].Points.Add(MouseToMaskPoint(currentEvent.mousePosition, imageDrawRect));
                    CompileShader();
                    currentEvent.Use();
                    return;
                }

                Vector2 maskPos = MouseToMaskPoint(currentEvent.mousePosition, imageDrawRect);
                int containingIndex = FindClosedPolygonContainingMaskPoint(maskPos);
                if (containingIndex >= 0 && !currentEvent.shift)
                {
                    _polygonInteractionMode = PolygonInteractionMode.DraggingShape;
                    _draggingPolygonIndex = containingIndex;
                    _polygonDragPreviousMaskPos = maskPos;
                    currentEvent.Use();
                    return;
                }

                var newShape = new PolygonMaskShape();
                newShape.Points.Add(maskPos);
                _polygonMaskShapes.Add(newShape);
                CompileShader();
                currentEvent.Use();
                return;
            }
        }

        private void ContinuePolygonDrag(Vector2 mousePosition, Rect imageDrawRect)
        {
            if (_draggingPolygonIndex < 0 || _draggingPolygonIndex >= _polygonMaskShapes.Count)
            {
                ResetPolygonInteractionState();
                return;
            }
            var shape = _polygonMaskShapes[_draggingPolygonIndex];
            Vector2 currentMaskPos = MouseToMaskPoint(mousePosition, imageDrawRect);

            if (_polygonInteractionMode == PolygonInteractionMode.DraggingVertex)
            {
                if (_draggingPolygonPoint < 0 || _draggingPolygonPoint >= shape.Points.Count)
                {
                    ResetPolygonInteractionState();
                    return;
                }
                shape.Points[_draggingPolygonPoint] = currentMaskPos;
                _polygonMaskDirty = true;
                return;
            }

            if (_polygonInteractionMode == PolygonInteractionMode.DraggingShape)
            {
                if (shape.Points.Count == 0)
                {
                    ResetPolygonInteractionState();
                    return;
                }
                Vector2 delta = currentMaskPos - _polygonDragPreviousMaskPos;
                float minX = 1f, maxX = 0f, minY = 1f, maxY = 0f;
                for (int i = 0; i < shape.Points.Count; i++)
                {
                    Vector2 p = shape.Points[i];
                    if (p.x < minX) minX = p.x;
                    if (p.x > maxX) maxX = p.x;
                    if (p.y < minY) minY = p.y;
                    if (p.y > maxY) maxY = p.y;
                }
                float dx = Mathf.Clamp(delta.x, -minX, 1f - maxX);
                float dy = Mathf.Clamp(delta.y, -minY, 1f - maxY);
                if (Mathf.Approximately(dx, 0f) && Mathf.Approximately(dy, 0f))
                {
                    return;
                }
                for (int i = 0; i < shape.Points.Count; i++)
                {
                    Vector2 p = shape.Points[i];
                    shape.Points[i] = new Vector2(p.x + dx, p.y + dy);
                }
                _polygonDragPreviousMaskPos = new Vector2(_polygonDragPreviousMaskPos.x + dx, _polygonDragPreviousMaskPos.y + dy);
                _polygonMaskDirty = true;
            }
        }

        private void ResetPolygonInteractionState()
        {
            _polygonInteractionMode = PolygonInteractionMode.None;
            _draggingPolygonIndex = -1;
            _draggingPolygonPoint = -1;
            _polygonMaskDirty = false;
        }

        private bool TryGetNearestPolygonVertex(Vector2 mousePosition, Rect imageDrawRect, float threshold, out int shapeIndex, out int vertexIndex)
        {
            float bestDistance = threshold;
            shapeIndex = -1;
            vertexIndex = -1;
            for (int s = 0; s < _polygonMaskShapes.Count; s++)
            {
                var shape = _polygonMaskShapes[s];
                for (int i = 0; i < shape.Points.Count; i++)
                {
                    Vector2 pointGui = MaskPointToGUI(shape.Points[i], imageDrawRect);
                    float distance = Vector2.Distance(pointGui, mousePosition);
                    if (distance <= bestDistance)
                    {
                        bestDistance = distance;
                        shapeIndex = s;
                        vertexIndex = i;
                    }
                }
            }
            return shapeIndex >= 0;
        }

        private int FindClosedPolygonContainingMaskPoint(Vector2 maskPoint)
        {
            for (int s = _polygonMaskShapes.Count - 1; s >= 0; s--)
            {
                var shape = _polygonMaskShapes[s];
                if (!shape.Closed || shape.Points.Count < 3)
                {
                    continue;
                }
                if (IsPointInsidePolygon(maskPoint, shape.Points))
                {
                    return s;
                }
            }
            return -1;
        }

        private int GetOpenPolygonIndex()
        {
            for (int i = 0; i < _polygonMaskShapes.Count; i++)
            {
                if (!_polygonMaskShapes[i].Closed)
                {
                    return i;
                }
            }
            return -1;
        }

        private bool DeletePolygonAtMouse(Vector2 mousePosition, Rect imageDrawRect)
        {
            int hitShapeIndex, hitVertexIndex;
            if (TryGetNearestPolygonVertex(mousePosition, imageDrawRect, _polygonVertexHitDistance, out hitShapeIndex, out hitVertexIndex))
            {
                _polygonMaskShapes.RemoveAt(hitShapeIndex);
                ResetPolygonInteractionState();
                return true;
            }
            Vector2 maskPos = MouseToMaskPoint(mousePosition, imageDrawRect);
            int containingIndex = FindClosedPolygonContainingMaskPoint(maskPos);
            if (containingIndex >= 0)
            {
                _polygonMaskShapes.RemoveAt(containingIndex);
                ResetPolygonInteractionState();
                return true;
            }
            return false;
        }

        private bool FinalizeOpenPolygon()
        {
            int openIndex = GetOpenPolygonIndex();
            if (openIndex < 0)
            {
                return false;
            }
            var openShape = _polygonMaskShapes[openIndex];
            if (openShape.Points.Count >= 3)
            {
                openShape.Closed = true;
            }
            else
            {
                _polygonMaskShapes.RemoveAt(openIndex);
            }
            ResetPolygonInteractionState();
            return true;
        }

        private bool HasAnyClosedPolygon()
        {
            for (int i = 0; i < _polygonMaskShapes.Count; i++)
            {
                var shape = _polygonMaskShapes[i];
                if (shape.Closed && shape.Points.Count >= 3)
                {
                    return true;
                }
            }
            return false;
        }

        private Vector2 MouseToMaskPoint(Vector2 mousePosition, Rect imageDrawRect)
        {
            float normalizedX = Mathf.Clamp01((mousePosition.x - imageDrawRect.x) / imageDrawRect.width);
            float normalizedYFromTop = Mathf.Clamp01((mousePosition.y - imageDrawRect.y) / imageDrawRect.height);
            return new Vector2(normalizedX, 1f - normalizedYFromTop);
        }

        private Vector2 MaskPointToGUI(Vector2 normalizedPoint, Rect imageDrawRect)
        {
            float x = imageDrawRect.x + Mathf.Clamp01(normalizedPoint.x) * imageDrawRect.width;
            float y = imageDrawRect.y + (1f - Mathf.Clamp01(normalizedPoint.y)) * imageDrawRect.height;
            return new Vector2(x, y);
        }

        private void ClearPolygonMask()
        {
            _polygonMaskShapes.Clear();
            ResetPolygonInteractionState();
        }

        private void EnsurePreviewTexture()
        {
            if (_targetTexture == null)
            {
                return;
            }
            if (_preview != null && (_preview.width != _targetTexture.width || _preview.height != _targetTexture.height))
            {
                _preview.Release();
                DestroyImmediate(_preview);
                _preview = null;
            }
            if (_preview == null)
            {
                _preview = new RenderTexture(_targetTexture.width, _targetTexture.height, 0, _previewTextureFormat);
                _preview.Create();
            }
        }

        private void EnsurePolygonCompositeMaterial()
        {
            if (_polygonCompositeMaterial != null)
            {
                return;
            }
            Shader polygonCompositeShader = Shader.Find(_polygonCompositeShaderName);
            if (polygonCompositeShader == null)
            {
                Debug.LogWarning($"Polygon composite shader not found: {_polygonCompositeShaderName}");
                return;
            }
            _polygonCompositeMaterial = new Material(polygonCompositeShader);
        }

        private void UpdatePolygonMaskTexture()
        {
            if (_targetTexture == null || !HasAnyClosedPolygon())
            {
                return;
            }

            int width = _targetTexture.width;
            int height = _targetTexture.height;
            if (_polygonMaskTexture == null || _polygonMaskTexture.width != width || _polygonMaskTexture.height != height)
            {
                if (_polygonMaskTexture != null)
                {
                    DestroyImmediate(_polygonMaskTexture);
                }
                _polygonMaskTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                _polygonMaskTexture.wrapMode = TextureWrapMode.Clamp;
                _polygonMaskTexture.filterMode = FilterMode.Bilinear;
            }

            int totalPixels = width * height;
            if (_maskPixels == null || _maskPixels.Length != totalPixels)
            {
                _maskPixels = new Color32[totalPixels];
            }
            else
            {
                Array.Clear(_maskPixels, 0, totalPixels);
            }
            Color32[] pixels = _maskPixels;
            Color32 insideColor = new Color32(255, 255, 255, 255);

            for (int s = 0; s < _polygonMaskShapes.Count; s++)
            {
                var shape = _polygonMaskShapes[s];
                if (!shape.Closed || shape.Points.Count < 3)
                {
                    continue;
                }

                Vector2[] pixelPoints = new Vector2[shape.Points.Count];
                int minX = width;
                int maxX = 0;
                int minY = height;
                int maxY = 0;

                for (int i = 0; i < shape.Points.Count; i++)
                {
                    int px = Mathf.Clamp(Mathf.RoundToInt(shape.Points[i].x * (width - 1)), 0, width - 1);
                    int py = Mathf.Clamp(Mathf.RoundToInt(shape.Points[i].y * (height - 1)), 0, height - 1);
                    pixelPoints[i] = new Vector2(px, py);
                    if (px < minX) minX = px;
                    if (px > maxX) maxX = px;
                    if (py < minY) minY = py;
                    if (py > maxY) maxY = py;
                }

                minX = Mathf.Clamp(minX, 0, width - 1);
                maxX = Mathf.Clamp(maxX, 0, width - 1);
                minY = Mathf.Clamp(minY, 0, height - 1);
                maxY = Mathf.Clamp(maxY, 0, height - 1);

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        int idx = y * width + x;
                        if (pixels[idx].a == 255)
                        {
                            continue;
                        }
                        if (IsPointInsidePolygon(new Vector2(x + 0.5f, y + 0.5f), pixelPoints))
                        {
                            pixels[idx] = insideColor;
                        }
                    }
                }
            }

            _polygonMaskTexture.SetPixels32(pixels);
            _polygonMaskTexture.Apply(false, false);
        }

        private bool IsPointInsidePolygon(Vector2 point, IReadOnlyList<Vector2> polygonPoints)
        {
            bool isInside = false;
            int count = polygonPoints.Count;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Vector2 pointI = polygonPoints[i];
                Vector2 pointJ = polygonPoints[j];
                float edgeDeltaY = pointJ.y - pointI.y;
                if (Mathf.Abs(edgeDeltaY) < Mathf.Epsilon)
                {
                    continue;
                }
                bool intersects = ((pointI.y > point.y) != (pointJ.y > point.y)) &&
                                  (point.x < (pointJ.x - pointI.x) * (point.y - pointI.y) / edgeDeltaY + pointI.x);
                if (intersects)
                {
                    isInside = !isInside;
                }
            }
            return isInside;
        }

        private void OnDisable()
        {
            if (_preview != null)
            {
                _preview.Release();
                DestroyImmediate(_preview);
                _preview = null;
            }
            if (_polygonMaskTexture != null)
            {
                DestroyImmediate(_polygonMaskTexture);
                _polygonMaskTexture = null;
            }
            if (_polygonCompositeMaterial != null)
            {
                DestroyImmediate(_polygonCompositeMaterial);
                _polygonCompositeMaterial = null;
            }
        }

    }

}
