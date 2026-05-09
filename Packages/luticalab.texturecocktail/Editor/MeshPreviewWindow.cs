using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    public class MeshPreviewWindow : EditorWindow
    {
        private const string _highlightShaderName = "Hidden/TextureCocktail/MeshHighlight";
        private const string _overlayShaderName = "Hidden/TextureCocktail/MeshHighlightOverlay";

        private TextureCocktail _source;

        private Renderer _renderer;
        private int _materialSlot;
        private string[] _textureProperties = new string[0];
        private string[] _textureLabels = new string[0];
        private int _selectedTexProperty;
        private bool _autoSyncProperty = true;

        private Color _highlightColor = new Color(0.2f, 1.0f, 0.4f, 1.0f);
        private float _highlightIntensity = 1.6f;
        private float _pulseSpeed = 2.4f;
        private float _pulseStrength = 0.35f;
        private float _baseDim = 0.0f;
        private bool _showInSceneView = false;
        private bool _autoRotate = false;

        private PreviewRenderUtility _previewUtility;
        private Material _highlightMaterial;
        private Material _overlayMaterial;
        private Mesh _bakedMesh;

        private Vector2 _previewYawPitch = new Vector2(120f, -10f);
        private float _previewDistance = 2.2f;
        private double _lastUpdateTime;
        private Vector2 _scrollPosition;

        public static MeshPreviewWindow ShowWindowFor(TextureCocktail source)
        {
            var window = GetWindow<MeshPreviewWindow>(LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_title"));
            window.minSize = new Vector2(420f, 520f);
            window.Connect(source);
            window.Show();
            window.Focus();
            return window;
        }

        [MenuItem("LuticaLab/TextureCocktail Mesh Preview")]
        public static void ShowWindowMenu()
        {
            var window = GetWindow<MeshPreviewWindow>(LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_title"));
            window.minSize = new Vector2(420f, 520f);
            window.TryAutoConnect();
            window.Show();
        }

        private void OnEnable()
        {
            _lastUpdateTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
            SceneView.duringSceneGui += OnSceneGUI;
            TryAutoConnect();
            EnsureMaterials();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            SceneView.duringSceneGui -= OnSceneGUI;
            DisconnectFromSource();
            DisposePreview();
            if (_highlightMaterial != null) { DestroyImmediate(_highlightMaterial); _highlightMaterial = null; }
            if (_overlayMaterial != null) { DestroyImmediate(_overlayMaterial); _overlayMaterial = null; }
            if (_bakedMesh != null) { DestroyImmediate(_bakedMesh); _bakedMesh = null; }
        }

        private void Connect(TextureCocktail source)
        {
            if (_source == source) return;
            DisconnectFromSource();
            _source = source;
            if (_source != null)
            {
                _source.OnPreviewUpdated += OnSourcePreviewUpdated;
                RefreshTextureProperties();
            }
            Repaint();
        }

        private void DisconnectFromSource()
        {
            if (_source != null)
            {
                _source.OnPreviewUpdated -= OnSourcePreviewUpdated;
                _source = null;
            }
        }

        private void TryAutoConnect()
        {
            if (_source != null) return;
            var windows = Resources.FindObjectsOfTypeAll<TextureCocktail>();
            if (windows != null && windows.Length > 0)
            {
                Connect(windows[0]);
            }
        }

        private void OnSourcePreviewUpdated()
        {
            if (_autoSyncProperty)
            {
                RefreshTextureProperties();
            }
            Repaint();
            if (_showInSceneView)
            {
                SceneView.RepaintAll();
            }
        }

        private void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = (float)(now - _lastUpdateTime);
            _lastUpdateTime = now;

            bool needsRepaint = false;
            if (_autoRotate)
            {
                _previewYawPitch.x = Mathf.Repeat(_previewYawPitch.x + delta * 30f, 360f);
                needsRepaint = true;
            }
            if (_pulseStrength > 0.001f)
            {
                needsRepaint = true;
            }

            if (needsRepaint)
            {
                Repaint();
                if (_showInSceneView) SceneView.RepaintAll();
            }
        }

        private void OnGUI()
        {
            EnsureMaterials();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawSourceSection();
            EditorGUILayout.Space(4);
            DrawRendererSection();
            EditorGUILayout.Space(4);
            DrawHighlightSection();
            EditorGUILayout.Space(6);
            DrawPreviewArea();
            EditorGUILayout.Space(4);
            DrawSceneViewSection();
            EditorGUILayout.EndScrollView();
        }

        private void DrawSourceSection()
        {
            EditorGUILayout.LabelField(LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_source"), EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            string sourceLabel = _source != null
                ? string.Format(LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_source_connected"), _source.titleContent.text)
                : LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_source_none");
            EditorGUILayout.LabelField(sourceLabel);
            if (GUILayout.Button(LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_source_reconnect"), GUILayout.Width(120)))
            {
                TryAutoConnect();
            }
            EditorGUILayout.EndHorizontal();

            if (_source == null)
            {
                EditorGUILayout.HelpBox(LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_source_help"), MessageType.Info);
            }
            else
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField(LanguageDisplayer.Instance.GetTranslatedLanguage("target_texture"), _source.TargetTexture, typeof(Texture2D), false);
                }
            }
        }

        private void DrawRendererSection()
        {
            EditorGUILayout.LabelField(LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_renderer_section"), EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            var newRenderer = (Renderer)EditorGUILayout.ObjectField(LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_renderer"), _renderer, typeof(Renderer), true);
            if (EditorGUI.EndChangeCheck())
            {
                _renderer = newRenderer;
                _materialSlot = 0;
                RefreshTextureProperties();
            }

            if (_renderer == null)
            {
                EditorGUILayout.HelpBox(LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_renderer_help"), MessageType.Info);
                return;
            }

            var mats = _renderer.sharedMaterials;
            if (mats == null || mats.Length == 0)
            {
                EditorGUILayout.HelpBox(LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_no_materials"), MessageType.Warning);
                return;
            }

            string[] slotLabels = new string[mats.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                slotLabels[i] = $"[{i}] {(mats[i] != null ? mats[i].name : "<null>")}";
            }
            int newSlot = EditorGUILayout.Popup(LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_material_slot"), Mathf.Clamp(_materialSlot, 0, mats.Length - 1), slotLabels);
            if (newSlot != _materialSlot)
            {
                _materialSlot = newSlot;
                RefreshTextureProperties();
            }

            if (_textureProperties.Length == 0)
            {
                EditorGUILayout.HelpBox(LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_no_tex_props"), MessageType.Info);
            }
            else
            {
                int newProp = EditorGUILayout.Popup(LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_texture_property"), Mathf.Clamp(_selectedTexProperty, 0, _textureProperties.Length - 1), _textureLabels);
                if (newProp != _selectedTexProperty)
                {
                    _selectedTexProperty = newProp;
                    _autoSyncProperty = false;
                }
                _autoSyncProperty = EditorGUILayout.ToggleLeft(LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_auto_sync_property"), _autoSyncProperty);
            }
        }

        private void DrawHighlightSection()
        {
            EditorGUILayout.LabelField(LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_highlight_section"), EditorStyles.boldLabel);
            _highlightColor = EditorGUILayout.ColorField(LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_highlight_color"), _highlightColor);
            _highlightIntensity = EditorGUILayout.Slider(LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_highlight_intensity"), _highlightIntensity, 0f, 4f);
            _pulseSpeed = EditorGUILayout.Slider(LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_pulse_speed"), _pulseSpeed, 0f, 10f);
            _pulseStrength = EditorGUILayout.Slider(LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_pulse_strength"), _pulseStrength, 0f, 1f);
            _baseDim = EditorGUILayout.Slider(LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_base_dim"), _baseDim, 0f, 1f);
        }

        private void DrawSceneViewSection()
        {
            EditorGUILayout.LabelField(LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_scene_section"), EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            bool newShow = EditorGUILayout.ToggleLeft(LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_show_in_scene"), _showInSceneView);
            if (EditorGUI.EndChangeCheck())
            {
                _showInSceneView = newShow;
                if (_showInSceneView) SceneView.RepaintAll();
            }
            if (_showInSceneView && _renderer == null)
            {
                EditorGUILayout.HelpBox(LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_renderer_help"), MessageType.Warning);
            }
        }

        private void DrawPreviewArea()
        {
            EditorGUILayout.LabelField(LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_3d_section"), EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _autoRotate = EditorGUILayout.ToggleLeft(LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_auto_rotate"), _autoRotate, GUILayout.Width(140));
            _previewDistance = EditorGUILayout.Slider(LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_distance"), _previewDistance, 0.4f, 6f);
            EditorGUILayout.EndHorizontal();

            Rect rect = GUILayoutUtility.GetRect(10, 240, GUILayout.ExpandWidth(true), GUILayout.Height(280));
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));

            Mesh mesh = ResolveMesh(updateBaked: true);
            if (mesh == null || _highlightMaterial == null)
            {
                GUI.Label(rect, LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_no_mesh"), new GUIStyle(EditorStyles.centeredGreyMiniLabel) { alignment = TextAnchor.MiddleCenter });
                return;
            }

            HandlePreviewInput(rect);

            if (Event.current.type == EventType.Repaint)
            {
                EnsurePreviewUtility();
                UpdateHighlightMaterial();

                Bounds b = mesh.bounds;
                float radius = Mathf.Max(b.extents.magnitude, 0.001f);
                float fov = _previewUtility.cameraFieldOfView;
                float distance = radius / Mathf.Sin(Mathf.Deg2Rad * fov * 0.5f) * _previewDistance;

                _previewUtility.camera.transform.position = -Vector3.forward * distance;
                _previewUtility.camera.transform.rotation = Quaternion.identity;
                _previewUtility.camera.nearClipPlane = Mathf.Max(0.01f, distance * 0.1f);
                _previewUtility.camera.farClipPlane = distance * 10f + radius * 4f;

                Quaternion rot = Quaternion.Euler(_previewYawPitch.y, _previewYawPitch.x, 0f);
                Matrix4x4 trs = Matrix4x4.TRS(rot * -b.center, rot, Vector3.one);

                _previewUtility.BeginPreview(rect, GUIStyle.none);
                int submeshCount = Mathf.Max(1, mesh.subMeshCount);
                int focusSlot = Mathf.Clamp(_materialSlot, 0, submeshCount - 1);
                _previewUtility.DrawMesh(mesh, trs, _highlightMaterial, focusSlot);
                _previewUtility.camera.Render();
                Texture preview = _previewUtility.EndPreview();
                GUI.DrawTexture(rect, preview, ScaleMode.StretchToFill, false);
            }

            GUI.Label(new Rect(rect.x + 6, rect.yMax - 18, rect.width - 12, 16), LanguageDisplayer.Instance.GetTranslatedLanguage("mesh_preview_drag_hint"), EditorStyles.miniLabel);
        }

        private void HandlePreviewInput(Rect rect)
        {
            Event e = Event.current;
            if (!rect.Contains(e.mousePosition)) return;

            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                _previewYawPitch.x += e.delta.x * 0.6f;
                _previewYawPitch.y = Mathf.Clamp(_previewYawPitch.y - e.delta.y * 0.5f, -89f, 89f);
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.ScrollWheel)
            {
                _previewDistance = Mathf.Clamp(_previewDistance + e.delta.y * 0.1f, 0.4f, 6f);
                e.Use();
                Repaint();
            }
        }

        private void OnSceneGUI(SceneView sv)
        {
            if (!_showInSceneView) return;
            if (_renderer == null || _overlayMaterial == null) return;
            if (Event.current.type != EventType.Repaint) return;

            Mesh mesh = ResolveMesh(updateBaked: true);
            if (mesh == null) return;

            UpdateOverlayMaterial();

            Matrix4x4 matrix;
            if (_renderer is SkinnedMeshRenderer)
            {
                matrix = Matrix4x4.TRS(_renderer.transform.position, _renderer.transform.rotation, Vector3.one);
            }
            else
            {
                matrix = _renderer.transform.localToWorldMatrix;
            }

            int passCount = Mathf.Max(1, mesh.subMeshCount);
            for (int i = 0; i < passCount; i++)
            {
                Graphics.DrawMesh(mesh, matrix, _overlayMaterial, 0, sv.camera, i);
            }
        }

        private Mesh ResolveMesh(bool updateBaked)
        {
            if (_renderer == null) return null;
            if (_renderer is SkinnedMeshRenderer smr)
            {
                if (smr.sharedMesh == null) return null;
                if (_bakedMesh == null) _bakedMesh = new Mesh { name = "TC_BakedSkinned" };
                if (updateBaked)
                {
                    smr.BakeMesh(_bakedMesh, true);
                }
                return _bakedMesh;
            }
            var mf = _renderer.GetComponent<MeshFilter>();
            return mf != null ? mf.sharedMesh : null;
        }

        private void RefreshTextureProperties()
        {
            _textureProperties = new string[0];
            _textureLabels = new string[0];
            _selectedTexProperty = 0;
            if (_renderer == null) return;
            var mats = _renderer.sharedMaterials;
            if (mats == null || mats.Length == 0) return;
            _materialSlot = Mathf.Clamp(_materialSlot, 0, mats.Length - 1);
            var mat = mats[_materialSlot];
            if (mat == null || mat.shader == null) return;

            var props = new List<string>();
            var labels = new List<string>();
            int count = ShaderUtil.GetPropertyCount(mat.shader);
            for (int i = 0; i < count; i++)
            {
                if (ShaderUtil.GetPropertyType(mat.shader, i) != ShaderUtil.ShaderPropertyType.TexEnv) continue;
                string name = ShaderUtil.GetPropertyName(mat.shader, i);
                string desc = ShaderUtil.GetPropertyDescription(mat.shader, i);
                Texture current = mat.GetTexture(name);
                string currentName = current != null ? current.name : "<empty>";
                props.Add(name);
                labels.Add($"{desc} ({name}) — {currentName}");
            }
            _textureProperties = props.ToArray();
            _textureLabels = labels.ToArray();

            if (_textureProperties.Length == 0) return;

            int matched = -1;
            if (_source != null && _source.TargetTexture != null)
            {
                for (int i = 0; i < _textureProperties.Length; i++)
                {
                    if (mat.GetTexture(_textureProperties[i]) == _source.TargetTexture)
                    {
                        matched = i;
                        break;
                    }
                }
            }
            if (matched < 0)
            {
                for (int i = 0; i < _textureProperties.Length; i++)
                {
                    if (_textureProperties[i] == "_MainTex" || _textureProperties[i] == "_BaseMap")
                    {
                        matched = i;
                        break;
                    }
                }
            }
            _selectedTexProperty = Mathf.Max(0, matched);
        }

        private void EnsureMaterials()
        {
            if (_highlightMaterial == null)
            {
                Shader sh = Shader.Find(_highlightShaderName);
                if (sh != null) _highlightMaterial = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            }
            if (_overlayMaterial == null)
            {
                Shader sh = Shader.Find(_overlayShaderName);
                if (sh != null) _overlayMaterial = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            }
        }

        private void EnsurePreviewUtility()
        {
            if (_previewUtility != null) return;
            _previewUtility = new PreviewRenderUtility();
            _previewUtility.cameraFieldOfView = 30f;
            _previewUtility.camera.clearFlags = CameraClearFlags.SolidColor;
            _previewUtility.camera.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            _previewUtility.lights[0].intensity = 1.2f;
            _previewUtility.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
            _previewUtility.lights[1].intensity = 0.6f;
            _previewUtility.lights[1].transform.rotation = Quaternion.Euler(-30f, -30f, 0f);
            _previewUtility.ambientColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        }

        private void DisposePreview()
        {
            if (_previewUtility != null)
            {
                _previewUtility.Cleanup();
                _previewUtility = null;
            }
        }

        private Texture ResolveBaseTextureForHighlight()
        {
            if (_source != null && _source.PreviewTexture != null) return _source.PreviewTexture;
            if (_renderer != null && _textureProperties.Length > 0)
            {
                var mats = _renderer.sharedMaterials;
                if (mats != null && _materialSlot < mats.Length && mats[_materialSlot] != null)
                {
                    return mats[_materialSlot].GetTexture(_textureProperties[Mathf.Clamp(_selectedTexProperty, 0, _textureProperties.Length - 1)]);
                }
            }
            return Texture2D.whiteTexture;
        }

        private Texture ResolveMaskTexture()
        {
            if (_source != null && _source.HasActivePolygonMask && _source.PolygonMaskTexture != null) return _source.PolygonMaskTexture;
            return Texture2D.blackTexture;
        }

        private void UpdateHighlightMaterial()
        {
            if (_highlightMaterial == null) return;
            _highlightMaterial.SetTexture("_MainTex", ResolveBaseTextureForHighlight());
            _highlightMaterial.SetTexture("_MaskTex", ResolveMaskTexture());
            _highlightMaterial.SetColor("_HighlightColor", _highlightColor);
            _highlightMaterial.SetFloat("_HighlightIntensity", _highlightIntensity);
            _highlightMaterial.SetFloat("_PulseSpeed", _pulseSpeed);
            _highlightMaterial.SetFloat("_PulseStrength", _pulseStrength);
            _highlightMaterial.SetFloat("_BaseDim", _baseDim);
            _highlightMaterial.SetFloat("_AmbientLevel", 0.35f);
        }

        private void UpdateOverlayMaterial()
        {
            if (_overlayMaterial == null) return;
            _overlayMaterial.SetTexture("_MaskTex", ResolveMaskTexture());
            _overlayMaterial.SetColor("_HighlightColor", _highlightColor);
            _overlayMaterial.SetFloat("_HighlightIntensity", _highlightIntensity);
            _overlayMaterial.SetFloat("_PulseSpeed", _pulseSpeed);
            _overlayMaterial.SetFloat("_PulseStrength", _pulseStrength);
        }
    }
}
