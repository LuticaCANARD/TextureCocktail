using System.IO;
using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// Non-destructive A/B comparison window. Holds an in-memory snapshot of
    /// the "after" preview so the source asset is never modified, and can spawn
    /// sample Materials for in-scene comparison.
    /// </summary>
    public class TextureABTestWindow : EditorWindow
    {
        private enum DisplayMode { SideBySide, SplitSlider, Toggle }
        private enum SampleShader { Unlit, Standard, SpriteDefault }

        private const string _defaultOutputFolder = "Assets/TextureCocktailABTest";
        private const string _mainTexProperty = "_MainTex";
        private const string _baseMapProperty = "_BaseMap";
        private const float _previewMinHeight = 240f;
        private const float _previewMaxHeight = 720f;

        private Texture2D _beforeTexture;
        private Texture2D _afterTexture;
        private string _baseName = "Texture";

        private DisplayMode _mode = DisplayMode.SideBySide;
        private float _splitPosition = 0.5f;
        private bool _toggleShowAfter = true;
        private bool _showLabels = true;

        private SampleShader _sampleShader = SampleShader.Unlit;
        private string _outputFolder = _defaultOutputFolder;
        private bool _spawnComparisonQuads = false;

        public static void ShowWindow(Texture2D before, RenderTexture afterPreview, string baseName)
        {
            var window = GetWindow<TextureABTestWindow>(false, GetLocalized("ab_test_title"), true);
            window.minSize = new Vector2(520, 480);
            window.SetSnapshot(before, afterPreview, baseName);
            window.Show();
        }

        public void SetSnapshot(Texture2D before, RenderTexture afterPreview, string baseName)
        {
            _beforeTexture = before;
            _baseName = string.IsNullOrEmpty(baseName) ? (before != null ? before.name : "Texture") : baseName;

            ReleaseAfterTexture();
            _afterTexture = BakeRenderTexture(afterPreview);
            if (_afterTexture != null)
            {
                _afterTexture.name = _baseName + "_After";
            }
            Repaint();
        }

        private void OnDisable()
        {
            ReleaseAfterTexture();
        }

        private void OnGUI()
        {
            GUILayout.Label(GetLocalized("ab_test_title"), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(GetLocalized("ab_test_help"), MessageType.Info);

            DrawSnapshotInputs();
            EditorGUILayout.Space(4);
            DrawDisplayModeBar();
            EditorGUILayout.Space(2);
            DrawComparisonArea();
            EditorGUILayout.Space(6);
            DrawSampleMaterialSection();
            EditorGUILayout.Space(2);
            DrawSaveSection();
        }

        // ---------- GUI sections ----------

        private void DrawSnapshotInputs()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.BeginChangeCheck();
            var newBefore = (Texture2D)EditorGUILayout.ObjectField(
                GetLocalized("ab_before_texture"), _beforeTexture, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck())
            {
                _beforeTexture = newBefore;
                if (newBefore != null && string.IsNullOrEmpty(_baseName))
                {
                    _baseName = newBefore.name;
                }
            }

            using (new EditorGUI.DisabledScope(_afterTexture == null))
            {
                EditorGUILayout.ObjectField(
                    GetLocalized("ab_after_texture"), _afterTexture, typeof(Texture2D), false);
            }

            _baseName = EditorGUILayout.TextField(GetLocalized("ab_base_name"), _baseName);
            _showLabels = EditorGUILayout.ToggleLeft(GetLocalized("ab_show_labels"), _showLabels);
            EditorGUILayout.EndVertical();
        }

        private void DrawDisplayModeBar()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(GetLocalized("ab_display_mode"), GUILayout.Width(110));
            _mode = (DisplayMode)GUILayout.Toolbar((int)_mode, new[]
            {
                GetLocalized("ab_mode_side_by_side"),
                GetLocalized("ab_mode_split_slider"),
                GetLocalized("ab_mode_toggle")
            });
            EditorGUILayout.EndHorizontal();

            switch (_mode)
            {
                case DisplayMode.SplitSlider:
                    _splitPosition = EditorGUILayout.Slider(GetLocalized("ab_split_position"), _splitPosition, 0f, 1f);
                    break;
                case DisplayMode.Toggle:
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(_toggleShowAfter ? GetLocalized("ab_show_before") : GetLocalized("ab_show_after"), GUILayout.Height(22)))
                    {
                        _toggleShowAfter = !_toggleShowAfter;
                    }
                    GUILayout.Label(_toggleShowAfter ? GetLocalized("ab_currently_after") : GetLocalized("ab_currently_before"), EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                    break;
            }
        }

        private void DrawComparisonArea()
        {
            float height = Mathf.Clamp(position.height * 0.55f, _previewMinHeight, _previewMaxHeight);
            Rect area = GUILayoutUtility.GetRect(10, height, GUILayout.ExpandWidth(true), GUILayout.Height(height));
            EditorGUI.DrawRect(area, new Color(0.12f, 0.12f, 0.12f, 1f));

            if (_beforeTexture == null && _afterTexture == null)
            {
                GUI.Label(area, GetLocalized("ab_no_textures"), CenteredLabelStyle());
                return;
            }

            switch (_mode)
            {
                case DisplayMode.SideBySide:
                    DrawSideBySide(area);
                    break;
                case DisplayMode.SplitSlider:
                    DrawSplitSlider(area);
                    break;
                case DisplayMode.Toggle:
                    DrawToggle(area);
                    break;
            }
        }

        private void DrawSideBySide(Rect area)
        {
            float gap = 6f;
            float halfWidth = (area.width - gap) * 0.5f;
            Rect left = new Rect(area.x, area.y, halfWidth, area.height);
            Rect right = new Rect(area.x + halfWidth + gap, area.y, halfWidth, area.height);
            DrawTextureFitted(left, _beforeTexture, GetLocalized("ab_label_before"));
            DrawTextureFitted(right, _afterTexture, GetLocalized("ab_label_after"));
        }

        private void DrawSplitSlider(Rect area)
        {
            // Draw before across the full area, then overlay the after on the right portion.
            DrawTextureFitted(area, _beforeTexture, _showLabels ? GetLocalized("ab_label_before") : null);

            if (_afterTexture != null)
            {
                Rect fitted = ComputeFittedRect(area, _afterTexture);
                float splitX = fitted.x + fitted.width * Mathf.Clamp01(_splitPosition);
                float overlayWidth = (fitted.x + fitted.width) - splitX;
                if (overlayWidth > 0f)
                {
                    Rect overlay = new Rect(splitX, fitted.y, overlayWidth, fitted.height);
                    Rect uv = new Rect(Mathf.Clamp01(_splitPosition), 0f, 1f - Mathf.Clamp01(_splitPosition), 1f);
                    GUI.DrawTextureWithTexCoords(overlay, _afterTexture, uv, true);
                    if (_showLabels)
                    {
                        DrawLabel(new Rect(overlay.x + 4, overlay.y + 4, overlay.width - 8, 18), GetLocalized("ab_label_after"));
                    }
                    EditorGUI.DrawRect(new Rect(splitX - 1, fitted.y, 2, fitted.height), new Color(1f, 1f, 1f, 0.85f));
                }
            }
        }

        private void DrawToggle(Rect area)
        {
            Texture2D shown = _toggleShowAfter ? _afterTexture : _beforeTexture;
            string label = _toggleShowAfter ? GetLocalized("ab_label_after") : GetLocalized("ab_label_before");
            DrawTextureFitted(area, shown, _showLabels ? label : null);
        }

        private void DrawSampleMaterialSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(GetLocalized("ab_sample_material_section"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(GetLocalized("ab_sample_material_help"), EditorStyles.wordWrappedMiniLabel);

            _sampleShader = (SampleShader)EditorGUILayout.EnumPopup(GetLocalized("ab_sample_shader"), _sampleShader);

            EditorGUILayout.BeginHorizontal();
            _outputFolder = EditorGUILayout.TextField(GetLocalized("ab_output_folder"), _outputFolder);
            if (GUILayout.Button(GetLocalized("ab_browse"), GUILayout.Width(80)))
            {
                string picked = EditorUtility.OpenFolderPanel(GetLocalized("ab_output_folder"), "Assets", string.Empty);
                if (!string.IsNullOrEmpty(picked))
                {
                    if (picked.StartsWith(Application.dataPath))
                    {
                        _outputFolder = "Assets" + picked.Substring(Application.dataPath.Length);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog(
                            GetLocalized("ab_invalid_folder_title"),
                            GetLocalized("ab_invalid_folder_msg"),
                            "OK");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            _spawnComparisonQuads = EditorGUILayout.ToggleLeft(GetLocalized("ab_spawn_quads"), _spawnComparisonQuads);

            using (new EditorGUI.DisabledScope(!CanGenerateSampleMaterials()))
            {
                if (GUILayout.Button(GetLocalized("ab_generate_samples"), GUILayout.Height(28)))
                {
                    GenerateSampleMaterials();
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawSaveSection()
        {
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(_afterTexture == null))
            {
                if (GUILayout.Button(GetLocalized("ab_save_after"), GUILayout.Height(24)))
                {
                    SaveAfterTextureAsAsset(promptUser: true);
                }
            }
            if (GUILayout.Button(GetLocalized("ab_close"), GUILayout.Height(24), GUILayout.Width(100)))
            {
                Close();
            }
            EditorGUILayout.EndHorizontal();
        }

        // ---------- Sample material generation ----------

        private bool CanGenerateSampleMaterials()
        {
            if (_beforeTexture == null && _afterTexture == null) return false;
            if (string.IsNullOrEmpty(_outputFolder)) return false;
            if (!_outputFolder.StartsWith("Assets")) return false;
            return true;
        }

        private void GenerateSampleMaterials()
        {
            string folder = _outputFolder.Replace('\\', '/').TrimEnd('/');
            if (!EnsureAssetFolder(folder))
            {
                EditorUtility.DisplayDialog(
                    GetLocalized("ab_invalid_folder_title"),
                    GetLocalized("ab_invalid_folder_msg"),
                    "OK");
                return;
            }

            Shader shader = ResolveSampleShader();
            if (shader == null)
            {
                EditorUtility.DisplayDialog(
                    GetLocalized("ab_shader_not_found_title"),
                    GetLocalized("ab_shader_not_found_msg"),
                    "OK");
                return;
            }

            // Ensure we have an after texture stored on disk so the Material reference is persistent.
            Texture2D persistentAfter = EnsurePersistentAfterTexture(folder);

            Material beforeMat = null;
            Material afterMat = null;

            if (_beforeTexture != null)
            {
                beforeMat = CreateOrUpdateMaterial(folder, _baseName + "_BeforeMat", shader, _beforeTexture);
            }
            if (persistentAfter != null)
            {
                afterMat = CreateOrUpdateMaterial(folder, _baseName + "_AfterMat", shader, persistentAfter);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (beforeMat != null || afterMat != null)
            {
                if (_spawnComparisonQuads)
                {
                    SpawnComparisonQuads(beforeMat, afterMat);
                }

                Object pingTarget = (Object)afterMat ?? beforeMat;
                if (pingTarget != null) EditorGUIUtility.PingObject(pingTarget);

                EditorUtility.DisplayDialog(
                    GetLocalized("ab_samples_done_title"),
                    string.Format(GetLocalized("ab_samples_done_msg"), folder),
                    "OK");
            }
        }

        private Shader ResolveSampleShader()
        {
            switch (_sampleShader)
            {
                case SampleShader.Standard:
                    return Shader.Find("Standard");
                case SampleShader.SpriteDefault:
                    return Shader.Find("Sprites/Default");
                case SampleShader.Unlit:
                default:
                    return Shader.Find("Unlit/Texture") ?? Shader.Find("Unlit/Transparent");
            }
        }

        private Material CreateOrUpdateMaterial(string folder, string name, Shader shader, Texture2D texture)
        {
            string path = $"{folder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            AssignMainTexture(material, texture);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void AssignMainTexture(Material material, Texture2D texture)
        {
            if (material.HasProperty(_mainTexProperty))
            {
                material.SetTexture(_mainTexProperty, texture);
            }
            if (material.HasProperty(_baseMapProperty))
            {
                material.SetTexture(_baseMapProperty, texture);
            }
            material.mainTexture = texture;
        }

        private Texture2D EnsurePersistentAfterTexture(string folder)
        {
            if (_afterTexture == null) return null;
            string existingPath = AssetDatabase.GetAssetPath(_afterTexture);
            if (!string.IsNullOrEmpty(existingPath))
            {
                return _afterTexture;
            }
            return SaveAfterTextureToFolder(folder);
        }

        private Texture2D SaveAfterTextureToFolder(string folder)
        {
            if (_afterTexture == null) return null;
            string path = $"{folder}/{_baseName}_After.png";
            File.WriteAllBytes(path, _afterTexture.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private void SaveAfterTextureAsAsset(bool promptUser)
        {
            if (_afterTexture == null) return;
            string path;
            if (promptUser)
            {
                path = EditorUtility.SaveFilePanelInProject(
                    GetLocalized("ab_save_after"),
                    _baseName + "_After.png",
                    "png",
                    GetLocalized("ab_save_after_prompt"));
                if (string.IsNullOrEmpty(path)) return;
            }
            else
            {
                EnsureAssetFolder(_outputFolder);
                path = $"{_outputFolder}/{_baseName}_After.png";
            }

            File.WriteAllBytes(path, _afterTexture.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        }

        private void SpawnComparisonQuads(Material beforeMat, Material afterMat)
        {
            GameObject root = new GameObject($"{_baseName}_ABCompare");
            Undo.RegisterCreatedObjectUndo(root, "Create A/B Compare Quads");

            if (beforeMat != null)
            {
                var beforeQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                beforeQuad.name = "Before";
                beforeQuad.transform.SetParent(root.transform, false);
                beforeQuad.transform.localPosition = new Vector3(-0.55f, 0f, 0f);
                beforeQuad.GetComponent<MeshRenderer>().sharedMaterial = beforeMat;
            }
            if (afterMat != null)
            {
                var afterQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                afterQuad.name = "After";
                afterQuad.transform.SetParent(root.transform, false);
                afterQuad.transform.localPosition = new Vector3(0.55f, 0f, 0f);
                afterQuad.GetComponent<MeshRenderer>().sharedMaterial = afterMat;
            }
            Selection.activeGameObject = root;
        }

        // ---------- Helpers ----------

        private static bool EnsureAssetFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return false;
            folder = folder.Replace('\\', '/').TrimEnd('/');
            if (!folder.StartsWith("Assets")) return false;
            if (AssetDatabase.IsValidFolder(folder)) return true;

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
            return AssetDatabase.IsValidFolder(folder);
        }

        private static Texture2D BakeRenderTexture(RenderTexture rt)
        {
            if (rt == null) return null;
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D baked = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.DontSave
            };
            baked.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            baked.Apply(false, false);
            RenderTexture.active = previous;
            return baked;
        }

        private void ReleaseAfterTexture()
        {
            if (_afterTexture != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(_afterTexture)))
            {
                DestroyImmediate(_afterTexture);
            }
            _afterTexture = null;
        }

        private static Rect ComputeFittedRect(Rect container, Texture texture)
        {
            if (texture == null || texture.width == 0 || texture.height == 0)
            {
                return container;
            }
            float scale = Mathf.Min(container.width / texture.width, container.height / texture.height);
            float w = texture.width * scale;
            float h = texture.height * scale;
            float x = container.x + (container.width - w) * 0.5f;
            float y = container.y + (container.height - h) * 0.5f;
            return new Rect(x, y, w, h);
        }

        private void DrawTextureFitted(Rect container, Texture2D texture, string label)
        {
            EditorGUI.DrawRect(container, new Color(0.08f, 0.08f, 0.08f, 1f));
            if (texture == null)
            {
                GUI.Label(container, GetLocalized("ab_no_texture"), CenteredLabelStyle());
                return;
            }
            Rect fitted = ComputeFittedRect(container, texture);
            GUI.DrawTexture(fitted, texture, ScaleMode.ScaleToFit, true);
            if (_showLabels && !string.IsNullOrEmpty(label))
            {
                DrawLabel(new Rect(fitted.x + 4, fitted.y + 4, fitted.width - 8, 18), label);
            }
        }

        private static void DrawLabel(Rect rect, string text)
        {
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.55f));
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleLeft
            };
            GUI.Label(new Rect(rect.x + 4, rect.y, rect.width - 8, rect.height), text, style);
        }

        private static GUIStyle CenteredLabelStyle()
        {
            return new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            };
        }

        private static string GetLocalized(string key)
        {
            return LanguageDisplayer.Instance.GetTranslatedLanguage(key);
        }
    }
}
