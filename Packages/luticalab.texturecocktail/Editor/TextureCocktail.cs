using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// Coordinates shader selection, content UI and preview updates. Rendering, polygon editing
    /// and PNG encoding are delegated to dedicated components.
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
        private readonly TexturePreviewRenderer _previewRenderer = new TexturePreviewRenderer();
        private RenderTexture _preview => _previewRenderer.Preview;
        private PolygonMaskEditor _polygonMaskEditor;
        private PolygonMaskEditor PolygonMask => _polygonMaskEditor ??= new PolygonMaskEditor(OnPolygonMaskChanged);
        private bool _updatingShaderValues;
        private bool _previewRenderRequested;
        private Material _calcMaterial;
        private bool _valueChanged = false;
        string[] _shaderKeys;
        private MethodInfo _getShaderKeywordsMethod;
        private bool _shaderOptionOnOff = false;
        private TextureCocktailContent _shaderWindow;
        private readonly Dictionary<string,bool> _keywordOnOff = new Dictionary<string, bool>();
        const string _mainTexProperty = "_MainTex";

        /// <summary>Legacy subclass member; content update policy is defined by TextureCocktailContent.</summary>
        virtual protected bool ShaderUpdateDefaultAction
        {
            get => true;
        }

        public Texture2D TargetTexture => _targetTexture;
        public RenderTexture PreviewTexture => _preview;
        public Texture2D PolygonMaskTexture => _polygonMaskEditor?.MaskTexture;
        public bool HasActivePolygonMask => PolygonMask.Enabled && PolygonMask.HasClosedShapes;
        internal Material CalculationMaterial => _calcMaterial;
        public event System.Action OnPreviewUpdated;

        // Mask commands consistently update the preview; callers never render separately.
        internal bool PolygonMaskEnabled
        {
            get => PolygonMask.Enabled;
            set => PolygonMask.Enabled = value;
        }

        internal void GetPolygonShapeCounts(out int closedCount, out int openCount)
            => PolygonMask.GetPolygonShapeCounts(out closedCount, out openCount);

        private void OnPolygonMaskChanged()
        {
            if (_calcMaterial != null && _targetTexture != null) RenderPreview();
            Repaint();
        }

        private int _selectedQuickShaderIndex = 0;
        private bool _showAdvancedShaderPicker = false;
        private string[] _quickShaderLabelsCache;
        private ITextureCocktailShaderPackage[] _quickShaderPackagesCache;

        private void RebuildQuickShaderCache()
        {
            var packages = TextureCocktailShaderRegistry.All;
            _quickShaderPackagesCache = new ITextureCocktailShaderPackage[packages.Count];
            _quickShaderLabelsCache = new string[packages.Count + 1];
            _quickShaderLabelsCache[0] = LanguageDisplayer.Instance.GetTranslatedLanguage("quick_shader_none");
            if (string.IsNullOrEmpty(_quickShaderLabelsCache[0]) || _quickShaderLabelsCache[0] == "quick_shader_none")
            {
                _quickShaderLabelsCache[0] = "(None)";
            }
            for (int i = 0; i < packages.Count; i++)
            {
                _quickShaderPackagesCache[i] = packages[i];
                string cat = string.IsNullOrEmpty(packages[i].Category) ? "" : packages[i].Category + "/";
                _quickShaderLabelsCache[i + 1] = cat + packages[i].DisplayName;
            }
        }

        private int FindPackageIndexForShader(Shader shader)
        {
            if (shader == null || _quickShaderPackagesCache == null) return 0;
            for (int i = 0; i < _quickShaderPackagesCache.Length; i++)
            {
                if (_quickShaderPackagesCache[i].Shader == shader) return i + 1;
            }
            return 0;
        }

        private void OnGUI()
        {
            GUILayout.Label("TextureCocktail", EditorStyles.boldLabel);

            // Quick shader selector — registry-driven
            if (_quickShaderLabelsCache == null) RebuildQuickShaderCache();

            GUILayout.Label(LanguageDisplayer.Instance.GetTranslatedLanguage("quick_shader_select"), EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            int newShaderIndex = EditorGUILayout.Popup(
                LanguageDisplayer.Instance.GetTranslatedLanguage("select_shader"),
                _selectedQuickShaderIndex, _quickShaderLabelsCache);
            if (GUILayout.Button("⟳", EditorStyles.miniButton, GUILayout.Width(24)))
            {
                TextureCocktailShaderRegistry.Refresh();
                RebuildQuickShaderCache();
                _selectedQuickShaderIndex = FindPackageIndexForShader(_shader);
            }
            EditorGUILayout.EndHorizontal();
            if (newShaderIndex != _selectedQuickShaderIndex)
            {
                _selectedQuickShaderIndex = newShaderIndex;
                if (newShaderIndex > 0 && _quickShaderPackagesCache != null && newShaderIndex - 1 < _quickShaderPackagesCache.Length)
                {
                    OnShaderChange(_quickShaderPackagesCache[newShaderIndex - 1].Shader);
                }
                else
                {
                    OnShaderChange(null);
                }
            }

            // Show package description when one is selected
            if (_selectedQuickShaderIndex > 0 && _quickShaderPackagesCache != null
                && _selectedQuickShaderIndex - 1 < _quickShaderPackagesCache.Length)
            {
                var pkg = _quickShaderPackagesCache[_selectedQuickShaderIndex - 1];
                if (!string.IsNullOrEmpty(pkg.Description))
                {
                    EditorGUILayout.HelpBox(pkg.Description, MessageType.None);
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
            EditorGUILayout.EndHorizontal();

            // Advanced: assign an arbitrary shader (validated against the registry)
            _showAdvancedShaderPicker = EditorGUILayout.Foldout(
                _showAdvancedShaderPicker,
                LanguageDisplayer.Instance.GetTranslatedLanguage("advanced_shader_picker"));
            if (_showAdvancedShaderPicker)
            {
                EditorGUI.indentLevel++;
                var advancedShader = (Shader)EditorGUILayout.ObjectField(
                    LanguageDisplayer.Instance.GetTranslatedLanguage("apply_shader"),
                    _shader, typeof(Shader), false);
                if (advancedShader != _shader)
                {
                    if (advancedShader != null && !TextureCocktailShaderRegistry.IsRegistered(advancedShader))
                    {
                        Debug.LogWarning(string.Format(
                            LanguageDisplayer.Instance.GetTranslatedLanguage("shader_not_registered_warning"),
                            advancedShader.name));
                    }
                    OnShaderChange(advancedShader);
                    _selectedQuickShaderIndex = FindPackageIndexForShader(advancedShader);
                }
                if (_shader != null && !TextureCocktailShaderRegistry.IsRegistered(_shader))
                {
                    EditorGUILayout.HelpBox(
                        LanguageDisplayer.Instance.GetTranslatedLanguage("shader_not_registered_help"),
                        MessageType.Warning);
                }
                EditorGUI.indentLevel--;
            }
            
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
        /// <summary>Compatibility alias for existing content extensions.</summary>
        public void DisplayPassedIamge() => DrawPreview();

        public void DrawPreview()
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

                EditorGUILayout.BeginHorizontal();
                bool newPolygonMaskEnabled = EditorGUILayout.ToggleLeft(
                    LanguageDisplayer.Instance.GetTranslatedLanguage("polygon_mask_enable"),
                    PolygonMaskEnabled);
                if (newPolygonMaskEnabled != PolygonMaskEnabled)
                {
                    PolygonMaskEnabled = newPolygonMaskEnabled;
                }
                if (GUILayout.Button(LanguageDisplayer.Instance.GetTranslatedLanguage("polygon_finish"),
                    GUILayout.Width(120)))
                {
                    FinalizeOpenPolygon();
                }
                if (GUILayout.Button(LanguageDisplayer.Instance.GetTranslatedLanguage("polygon_reset"),
                    GUILayout.Width(120)))
                {
                    ClearPolygonMask();
                }
                if (GUILayout.Button(LanguageDisplayer.Instance.GetTranslatedLanguage("open_fullscreen_edit"), GUILayout.Width(160)))
                {
                    ImageViewerWindow.ShowWindow(this, _targetTexture != null ? _targetTexture.name + " - Preview" : "Preview");
                }
                if (GUILayout.Button(LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_open"), GUILayout.Width(160)))
                {
                    MeshPreviewWindow.ShowWindowFor(this);
                }
                using (new EditorGUI.DisabledScope(_targetTexture == null))
                {
                    if (GUILayout.Button(LanguageDisplayer.Instance.GetTranslatedLanguage("ab_open_button"), GUILayout.Width(140)))
                    {
                        OpenABTestWindow();
                    }
                }
                EditorGUILayout.EndHorizontal();
                if (PolygonMask.ShapeCount > 0)
                {
                    GetPolygonShapeCounts(out int closedCount, out int openCount);
                    EditorGUILayout.LabelField(string.Format(
                        LanguageDisplayer.Instance.GetTranslatedLanguage("polygon_count_label_with_hint"),
                        closedCount, openCount), EditorStyles.miniLabel);
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
                RenderPreview();
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
                        Vector2 limits = _shader.GetPropertyRangeLimits(i);
                        float newRangeValue = EditorGUILayout.Slider(rangeValue, limits.x, limits.y);
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
            if (_preview == null || _targetTexture == null)
            {
                Debug.LogWarning(
                    LanguageDisplayer.Instance.GetTranslatedLanguage("apply_shader_execute_error_not_create")
                );
                return;
            }

            string path = EditorUtility.SaveFilePanel(
                LanguageDisplayer.Instance.GetTranslatedLanguage("save_texture")
                , "Assets"
                , _targetTexture.name + ".png", "png"
            );
            if (!string.IsNullOrEmpty(path))
            {
                TextureExportUtility.SaveRenderTexturePng(_preview, path);

                AssetDatabase.Refresh();
                if (path.StartsWith(Application.dataPath, StringComparison.Ordinal))
                {
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
                    RenderPreview();
                }
            }
            TextureABTestWindow.ShowWindow(_targetTexture, _preview, _targetTexture.name);
        }
        public void SetTargetTexture(Texture2D texture)
        {
            OnTextureChanged(texture);
            if (_calcMaterial != null && texture != null)
            {
                _valueChanged = false;
                RenderPreview();
            }
            Repaint();
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
        private int ResolveActivePassIndex()
        {
            // Priority: per-shader content window's PassOrder (runtime/UI-driven, e.g. FeatureExtractor),
            // falling back to the registered package's PassIndex, then 0.
            if (_shaderWindow != null)
            {
                int pass = _shaderWindow.PassOrder;
                if (pass >= 0) return pass;
            }
            if (_shader != null)
            {
                var pkg = TextureCocktailShaderRegistry.FindByShader(_shader);
                if (pkg != null && pkg.PassIndex >= 0) return pkg.PassIndex;
            }
            return 0;
        }

        /// <summary>
        /// Legacy synchronous render API. Existing extensions may read PreviewTexture immediately
        /// after this call, even inside OnShaderValueChanged. New content can use RenderPreview
        /// to coalesce requests made during its value-change callback.
        /// </summary>
        public void CompileShader() => RenderPreviewImmediately();

        /// <summary>
        /// Renders the current material/pass and mask, then publishes OnPreviewUpdated.
        /// Requests inside OnShaderValueChanged are coalesced until that callback completes.
        /// </summary>
        public void RenderPreview()
        {
            if (_updatingShaderValues)
            {
                _previewRenderRequested = true;
                return;
            }
            RenderPreviewImmediately();
        }

        private void RenderPreviewImmediately()
        {
            if (_calcMaterial == null || _targetTexture == null)
            {
                Debug.LogWarning(LanguageDisplayer.Instance.GetTranslatedLanguage("apply_shader_execute_error_not_create"));
                return;
            }
            _calcMaterial.shader = _shader;
            foreach (var keyword in _keywordOnOff) ApplyShaderDict(keyword.Key);
            Texture mask = HasActivePolygonMask ? PolygonMask.GetTexture(_targetTexture) : null;
            _previewRenderer.Render(_targetTexture, _calcMaterial, ResolveActivePassIndex(), mask);
            OnPreviewUpdated?.Invoke();
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
                        DrawPreview();
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
            ReleaseShaderState();
            _shader = changeTo;
            
            // Clean up shader window when shader is set to null
            if (_shader == null)
            {
                _keywordOnOff.Clear();
                _shaderKeys = null;
                return;
            }
            
            _calcMaterial = new Material(_shader) { hideFlags = HideFlags.HideAndDontSave };
            Debug.Log($"Shader changed to: {_shader.name}");
            _shaderWindow = TextureCocktailShaderRegistry.CreateContent(_shader);
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
            string[] keywords = _getShaderKeywordsMethod != null
                ? (string[])_getShaderKeywordsMethod.Invoke(null, new object[] { _shader })
                : Array.Empty<string>();
            _shaderKeys = keywords;
            foreach (var keyword in _shaderKeys)
            {
                _keywordOnOff[keyword] = false; // Initialize all keywords to false
            }
            _valueChanged = true;
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
            bool useDefaultRender = _shaderWindow == null || _shaderWindow.ShaderUpdateDefaultAction;
            if (useDefaultRender)
            {
                if (_calcMaterial == null) return;
                _calcMaterial.SetTexture(_mainTexProperty, _targetTexture);
                if (_targetTexture == null) return;
                EnsurePreviewTexture();
            }
            // Custom-action extensions retain their original callback-first contract.
            _updatingShaderValues = true;
            _previewRenderRequested = false;
            bool completed = false;
            try
            {
                _shaderWindow?.OnShaderValueChanged();
                completed = true;
            }
            finally
            {
                _updatingShaderValues = false;
                bool render = completed && (useDefaultRender || _previewRenderRequested);
                _previewRenderRequested = false;
                if (render && _calcMaterial != null && _targetTexture != null) RenderPreview();
            }
        }

        private void ReleaseShaderState()
        {
            if (_shaderWindow != null)
            {
                DestroyImmediate(_shaderWindow);
                _shaderWindow = null;
            }
            if (_calcMaterial != null)
            {
                DestroyImmediate(_calcMaterial);
                _calcMaterial = null;
            }
        }

        internal Rect GetImageDrawRect(Rect rect, int width, int height)
            => PolygonMask.GetImageDrawRect(rect, width, height);
        internal void DrawPolygonMaskOverlay(Rect rect) => PolygonMask.DrawPolygonMaskOverlay(rect);
        internal void HandlePolygonMaskInteraction(Rect rect, Event currentEvent)
            => PolygonMask.HandlePolygonMaskInteraction(rect, currentEvent);
        internal bool FinalizeOpenPolygon() => PolygonMask.FinalizeOpenPolygon();
        internal void ClearPolygonMask() => PolygonMask.ClearPolygonMask();
        private void EnsurePreviewTexture() => _previewRenderer.EnsurePreview(_targetTexture);

        private void OnDisable()
        {
            ReleaseShaderState();
            _previewRenderer.Dispose();
            _polygonMaskEditor?.Dispose();
        }
    }
}
