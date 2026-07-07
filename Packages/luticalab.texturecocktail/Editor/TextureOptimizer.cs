using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// Unity texture optimization analysis and batch-fix tool.
    /// Open via: LuticaLab → Texture Optimizer
    /// </summary>
    public class TextureOptimizer : EditorWindow
    {
        // ── Menu item ────────────────────────────────────────────────────────
        [MenuItem("LuticaLab/Texture Optimizer")]
        public static void ShowWindow()
        {
            GetWindow<TextureOptimizer>("Texture Optimizer");
        }

        // ── Enums ────────────────────────────────────────────────────────────
        public enum TargetPlatform { Desktop, Mobile, VR }

        // ── Inner data class ─────────────────────────────────────────────────
        private class TextureReport
        {
            public Texture2D Texture;
            public string AssetPath;
            public TextureImporter Importer;

            // Current state
            public int Width;
            public int Height;
            public bool IsPOT;
            public bool HasMipmaps;
            public TextureImporterCompression CurrentCompression;
            public int CurrentMaxSize;
            public long EstimatedSizeBytes;

            // Issues / recommendations
            public List<string> Warnings = new List<string>();
            public List<TextureOptimizationAction> SuggestedActions = new List<TextureOptimizationAction>();

            // UI state
            public bool IsSelected;
            public bool FoldoutOpen;
        }

        public enum TextureOptimizationAction
        {
            EnableMipmaps,
            ResizeToPOT,
            ReduceMaxSize,
            EnableCompression,
            EnableCrunchCompression,
        }

        // ── Window state ─────────────────────────────────────────────────────
        private string _scanFolder = "Assets";
        private TargetPlatform _targetPlatform = TargetPlatform.Desktop;
        private List<TextureReport> _reports = new List<TextureReport>();
        private Vector2 _scroll;
        private bool _scanning;
        private int _maxSizeThreshold = 2048;
        private bool _showOnlyWithIssues = true;

        // ── Action tracking ──────────────────────────────────────────────────
        private int _appliedCount;

        // ── GUI ──────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            GUILayout.Label("Texture Optimizer", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // Scan settings
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Scan Settings", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            _scanFolder = EditorGUILayout.TextField("Folder to Scan", _scanFolder);
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Folder", _scanFolder, "");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                        _scanFolder = "Assets" + path.Substring(Application.dataPath.Length);
                    else
                        _scanFolder = path;
                }
            }
            EditorGUILayout.EndHorizontal();

            _targetPlatform = (TargetPlatform)EditorGUILayout.EnumPopup("Target Platform", _targetPlatform);
            _maxSizeThreshold = EditorGUILayout.IntField("Max Allowed Size (px)", _maxSizeThreshold);
            _showOnlyWithIssues = EditorGUILayout.Toggle("Show Only Textures With Issues", _showOnlyWithIssues);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Scan Textures", GUILayout.Height(35)))
                ScanTextures();

            if (_reports.Count > 0)
            {
                EditorGUILayout.Space(4);
                DrawActionBar();
                EditorGUILayout.Space(4);
                DrawReportList();
            }
        }

        // ── Scan ─────────────────────────────────────────────────────────────
        private void ScanTextures()
        {
            _reports.Clear();

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { _scanFolder });
            int total = guids.Length;
            int processed = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (EditorUtility.DisplayCancelableProgressBar(
                    "Scanning Textures",
                    $"Analyzing: {Path.GetFileName(path)}",
                    (float)processed / Mathf.Max(total, 1)))
                {
                    break;
                }

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (tex == null || importer == null)
                {
                    processed++;
                    continue;
                }

                var report = BuildReport(tex, path, importer);
                if (!_showOnlyWithIssues || report.Warnings.Count > 0)
                    _reports.Add(report);

                processed++;
            }

            EditorUtility.ClearProgressBar();
            Repaint();
        }

        private TextureReport BuildReport(Texture2D tex, string path, TextureImporter importer)
        {
            var r = new TextureReport
            {
                Texture = tex,
                AssetPath = path,
                Importer = importer,
                Width = tex.width,
                Height = tex.height,
                IsPOT = IsPowerOfTwo(tex.width) && IsPowerOfTwo(tex.height),
                HasMipmaps = tex.mipmapCount > 1,
                CurrentCompression = importer.textureCompression,
                CurrentMaxSize = importer.maxTextureSize,
            };

            r.EstimatedSizeBytes = EstimateVRAM(tex, r.HasMipmaps);

            // --- Warnings & actions ---
            if (!r.IsPOT)
            {
                r.Warnings.Add("Texture dimensions are not power-of-two. GPU cannot generate mipmaps efficiently.");
                r.SuggestedActions.Add(TextureOptimizationAction.ResizeToPOT);
            }

            bool is3DTexture = importer.textureType != TextureImporterType.Sprite &&
                               importer.textureType != TextureImporterType.GUI;

            if (is3DTexture && !r.HasMipmaps)
            {
                r.Warnings.Add("Mipmaps are disabled on a non-UI texture. Enable mipmaps to reduce aliasing and improve performance.");
                r.SuggestedActions.Add(TextureOptimizationAction.EnableMipmaps);
            }

            if (r.CurrentCompression == TextureImporterCompression.Uncompressed)
            {
                r.Warnings.Add("Texture is uncompressed. Compression can reduce memory usage significantly.");
                r.SuggestedActions.Add(TextureOptimizationAction.EnableCompression);
            }

            if (r.Width > _maxSizeThreshold || r.Height > _maxSizeThreshold)
            {
                r.Warnings.Add($"Texture exceeds {_maxSizeThreshold}px threshold ({r.Width}×{r.Height}). Consider reducing max size.");
                r.SuggestedActions.Add(TextureOptimizationAction.ReduceMaxSize);
            }

            if (r.CurrentCompression != TextureImporterCompression.Uncompressed &&
                !importer.crunchedCompression &&
                r.EstimatedSizeBytes > 1024 * 1024)
            {
                r.Warnings.Add("Large compressed texture. Crunch compression can further reduce disk size.");
                r.SuggestedActions.Add(TextureOptimizationAction.EnableCrunchCompression);
            }

            return r;
        }

        // ── Action bar ───────────────────────────────────────────────────────
        private void DrawActionBar()
        {
            int selectedCount = 0;
            foreach (var r in _reports)
                if (r.IsSelected) selectedCount++;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Found {_reports.Count} texture(s). {selectedCount} selected.", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Select All", GUILayout.Width(90)))
                foreach (var r in _reports) r.IsSelected = true;
            if (GUILayout.Button("Deselect All", GUILayout.Width(95)))
                foreach (var r in _reports) r.IsSelected = false;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            GUI.enabled = selectedCount > 0;
            if (GUILayout.Button("Apply Recommended Fixes to Selected", GUILayout.Height(30)))
                ApplySelectedFixes();
            GUI.enabled = true;

            if (_appliedCount > 0)
            {
                EditorGUILayout.HelpBox($"Applied fixes to {_appliedCount} texture(s). Re-scan to verify.", MessageType.Info);
            }
        }

        // ── Report list ──────────────────────────────────────────────────────
        private void DrawReportList()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var r in _reports)
            {
                DrawReportEntry(r);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawReportEntry(TextureReport r)
        {
            bool hasIssues = r.Warnings.Count > 0;
            Color rowColor = hasIssues ? new Color(1f, 0.95f, 0.8f) : new Color(0.85f, 1f, 0.85f);

            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = rowColor;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = prevBg;

            // Header row
            EditorGUILayout.BeginHorizontal();
            r.IsSelected = EditorGUILayout.Toggle(r.IsSelected, GUILayout.Width(18));
            r.FoldoutOpen = EditorGUILayout.Foldout(r.FoldoutOpen,
                $"{r.Texture.name}  ({r.Width}×{r.Height})  {FormatBytes(r.EstimatedSizeBytes)}",
                true);
            GUILayout.FlexibleSpace();
            if (hasIssues)
                GUILayout.Label($"⚠ {r.Warnings.Count} issue(s)", EditorStyles.miniLabel);
            else
                GUILayout.Label("✓ OK", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            if (r.FoldoutOpen)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.LabelField("Path:", r.AssetPath, EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Compression:", r.CurrentCompression.ToString(), EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Max Size:", r.CurrentMaxSize.ToString(), EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Mipmaps:", r.HasMipmaps ? "Enabled" : "Disabled", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Power of Two:", r.IsPOT ? "Yes" : "No", EditorStyles.miniLabel);

                if (r.Warnings.Count > 0)
                {
                    GUILayout.Label("Issues:", EditorStyles.boldLabel);
                    foreach (var w in r.Warnings)
                        EditorGUILayout.HelpBox(w, MessageType.Warning);
                }

                // Quick-fix button
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Ping Asset", GUILayout.Width(90)))
                    EditorGUIUtility.PingObject(r.Texture);
                if (r.Warnings.Count > 0 && GUILayout.Button("Fix This Texture", GUILayout.Width(110)))
                {
                    ApplyFix(r);
                    _appliedCount++;
                    Repaint();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        // ── Fix logic ────────────────────────────────────────────────────────
        private void ApplySelectedFixes()
        {
            _appliedCount = 0;
            foreach (var r in _reports)
            {
                if (!r.IsSelected) continue;
                ApplyFix(r);
                _appliedCount++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ScanTextures(); // Refresh reports
        }

        private void ApplyFix(TextureReport r)
        {
            bool dirty = false;

            foreach (var action in r.SuggestedActions)
            {
                switch (action)
                {
                    case TextureOptimizationAction.EnableMipmaps:
                        r.Importer.mipmapEnabled = true;
                        dirty = true;
                        break;

                    case TextureOptimizationAction.EnableCompression:
                        r.Importer.textureCompression = GetRecommendedCompression();
                        dirty = true;
                        break;

                    case TextureOptimizationAction.EnableCrunchCompression:
                        r.Importer.crunchedCompression = true;
                        r.Importer.compressionQuality = 50;
                        dirty = true;
                        break;

                    case TextureOptimizationAction.ReduceMaxSize:
                        int newMax = _maxSizeThreshold;
                        // Clamp to nearest valid value
                        int[] validSizes = { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192 };
                        foreach (int s in validSizes)
                        {
                            if (s >= newMax) { newMax = s; break; }
                        }
                        r.Importer.maxTextureSize = newMax;
                        dirty = true;
                        break;

                    case TextureOptimizationAction.ResizeToPOT:
                        r.Importer.npotScale = TextureImporterNPOTScale.ToNearest;
                        dirty = true;
                        break;
                }
            }

            if (dirty)
                r.Importer.SaveAndReimport();
        }

        private TextureImporterCompression GetRecommendedCompression()
        {
            return _targetPlatform switch
            {
                TargetPlatform.Mobile => TextureImporterCompression.CompressedHQ,
                TargetPlatform.VR => TextureImporterCompression.CompressedHQ,
                _ => TextureImporterCompression.Compressed,
            };
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;

        private static long EstimateVRAM(Texture2D tex, bool hasMips)
        {
            // Rough estimate: width * height * bytesPerPixel (compressed ~0.5 BPP, uncompressed ~4 BPP)
            long base_ = (long)tex.width * tex.height;
            long bpp;
            switch (tex.format)
            {
                case TextureFormat.DXT1: bpp = 1; break;
                case TextureFormat.DXT5: bpp = 1; break;
                case TextureFormat.ETC_RGB4: bpp = 1; break;
                case TextureFormat.ETC2_RGBA8: bpp = 1; break;
                case TextureFormat.ASTC_4x4: bpp = 1; break;
                case TextureFormat.RGB24: bpp = 3; break;
                case TextureFormat.RGBA32: bpp = 4; break;
                default: bpp = 4; break;
            }
            long size = base_ * bpp;
            return hasMips ? size * 4 / 3 : size; // mips add ~33%
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024} KB";
            return $"{bytes / (1024 * 1024)} MB";
        }
    }
}
