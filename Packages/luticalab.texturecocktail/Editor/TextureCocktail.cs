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

        virtual protected bool ShaderUpdateDefaultAction
        {
            get => true;
        }
        
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
            "Hidden/ArtisticEffects",
            "Hidden/ImageFilter",
            "Hidden/ImageSync",
            "Hidden/ImageEffect",
            "Hidden/NormalMapGenerator"
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
            
            if (_preview != null)
            {
                // Draw the preview texture
                GUI.DrawTexture(previewRect, _preview, ScaleMode.ScaleToFit);
                
                // Add a subtle border
                GUI.Box(previewRect, "", EditorStyles.helpBox);
                
                // Show click hint on hover and handle click
                if (previewRect.Contains(Event.current.mousePosition))
                {
                    EditorGUI.DrawRect(new Rect(previewRect.x, previewRect.y + previewRect.height - 20, previewRect.width, 20), 
                        new Color(0, 0, 0, 0.7f));
                    GUI.Label(new Rect(previewRect.x, previewRect.y + previewRect.height - 20, previewRect.width, 20), 
                        LanguageDisplayer.Instance.GetTranslatedLanguage("click_to_view_fullsize"), 
                        new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, normal = new GUIStyleState { textColor = Color.white } });
                    
                    // Handle mouse click
                    if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                    {
                        ImageViewerWindow.ShowWindow(_preview, _targetTexture != null ? _targetTexture.name + " - Preview" : "Preview");
                        Event.current.Use();
                    }
                    
                    Repaint();
                }
            }
            else
            {
                GUI.Box(previewRect, LanguageDisplayer.Instance.GetTranslatedLanguage("no_preview_available"));
            }
        }
        public void DisplayShaderOptions()
        {
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
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.SaveAndReimport();
                }
                AssetDatabase.Refresh();
                string reply = string.Format(LanguageDisplayer.Instance.GetTranslatedLanguage("save_texture_success"), path);
                Debug.Log(reply);
            }
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
            if (_calcMaterial != null)
            {
                _calcMaterial.shader = _shader;
                foreach (var keyword in _keywordOnOff)
                {
                    ApplyShaderDict(keyword.Key);
                }
                ShaderUtil.CompilePass(_calcMaterial, 0);
                RenderTexture.active = _preview;
                Graphics.Blit(_targetTexture, _preview, _calcMaterial);
                RenderTexture.active = null;
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
                        if (GUILayout.Button(LanguageDisplayer.Instance.GetTranslatedLanguage("save_texture")))
                            SaveTexture();
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
            _preview = new RenderTexture(_targetTexture.width, _targetTexture.height, 0, RenderTextureFormat.ARGB32);
            _preview.Create();
            if(_shaderWindow != null)
            {
                _shaderWindow.OnShaderValueChanged();
            }
            CompileShader();
        }

    }

}
