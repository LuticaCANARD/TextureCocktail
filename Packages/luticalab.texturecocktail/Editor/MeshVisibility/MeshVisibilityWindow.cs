using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// Captures the selected hierarchy, runs an explicit visibility analysis and displays
    /// candidate triangles without modifying source meshes, renderers or materials.
    /// </summary>
    public sealed class MeshVisibilityWindow : EditorWindow
    {
        private enum ViewMode { SceneView, Camera, Direction }
        private enum AnalysisMode { SingleView, AllDirections }
        private static readonly int[] DirectionResolutions = { 1, 2, 4 };
        private static readonly Vector3[] AxisDirections =
            { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        private static readonly string[] AxisLabels = { "+X", "-X", "+Y", "-Y", "+Z", "-Z" };
        private static readonly int[] SampleCounts = { 1, 4, 7 };

        [SerializeField] private GameObject _root;
        [SerializeField] private ViewMode _viewMode;
        [SerializeField] private AnalysisMode _analysisMode = AnalysisMode.AllDirections;
        [SerializeField] private int _directionQuality = 1;
        [SerializeField] private bool _showHiddenFromAllDirections = true;
        [SerializeField] private bool _showRayDirections = true;
        [SerializeField] private Camera _camera;
        [SerializeField] private Vector3 _direction = Vector3.forward;
        [SerializeField] private bool _includeSceneBlockers;
        [SerializeField] private bool _twoSided;
        [SerializeField] private int _quality = 1;
        [SerializeField] private bool _showOverlay = true;
        [SerializeField] private bool _xray = true;
        [SerializeField] private bool _showOccluded = true;
        [SerializeField] private bool _showBackFacing = true;
        [SerializeField] private bool _showOutsideView;

        private MeshVisibilityReport _report;
        private Bounds _analysisBounds;
        private readonly Dictionary<MeshVisibilityState, int> _totals = new Dictionary<MeshVisibilityState, int>();
        private readonly Dictionary<MeshVisibilitySurfaceResult, Dictionary<MeshVisibilityState, int>> _surfaceCounts
            = new Dictionary<MeshVisibilitySurfaceResult, Dictionary<MeshVisibilityState, int>>();
        private int _triangleCount;
        private MeshVisibilityOverlay _overlay;
        private string[] _warnings = Array.Empty<string>();
        private string _status;
        private MessageType _statusType = MessageType.Info;
        private string _analyzedView;
        private bool _settingsChanged;
        private bool _showWarnings;
        private Vector2 _scroll;

        [MenuItem("LuticaLab/TextureCocktail Mesh Visibility")]
        public static void ShowWindow()
        {
            ShowWindowFor(Selection.activeGameObject);
        }

        public static void ShowWindowFor(GameObject root)
        {
            var window = GetWindow<MeshVisibilityWindow>(Text("mesh_visibility_title", "Mesh Visibility"));
            window.minSize = new Vector2(480f, 560f);
            if (root != null && window._root != root)
            {
                window._root = root;
                window._settingsChanged = window._report != null;
            }
            window.Show();
            window.Focus();
        }

        internal static string Text(string key, string fallback)
        {
            string value = LanguageDisplayer.Instance.GetTranslatedLanguage(key);
            return string.IsNullOrEmpty(value) || value == key ? fallback : value;
        }

        private void OnEnable()
        {
            if (_root == null) _root = Selection.activeGameObject;
            SceneView.duringSceneGui += DrawSceneOverlay;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneOverlay;
            _overlay?.Dispose();
            _overlay = null;
            _report = null;
            _surfaceCounts.Clear();
            _totals.Clear();
            SceneView.RepaintAll();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField(Text("mesh_visibility_title", "Mesh Visibility"), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(Text("mesh_visibility_intro", "Find triangles hidden from a camera or multiple viewing directions. Results are candidates for inspection; the source mesh stays unchanged."), MessageType.Info);
            DrawAnalysisInputs();

            using (new EditorGUI.DisabledScope(!CanAnalyze()))
            {
                if (GUILayout.Button(Text("mesh_visibility_analyze", "Analyze Current Pose"), GUILayout.Height(32f))) Analyze();
            }
            if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, _statusType);
            DrawWarnings();
            EditorGUILayout.Space(5f);
            DrawResults();
            EditorGUILayout.EndScrollView();
        }

        private void DrawAnalysisInputs()
        {
            EditorGUI.BeginChangeCheck();
            _root = (GameObject)EditorGUILayout.ObjectField(Text("mesh_visibility_root", "Target Root"), _root, typeof(GameObject), true);
            if (GUILayout.Button(Text("mesh_visibility_use_selection", "Use Selected Object")))
            {
                _root = Selection.activeGameObject;
                GUI.changed = true;
            }
            _analysisMode = (AnalysisMode)EditorGUILayout.Popup(Text("mesh_visibility_mode", "Analysis Mode"), (int)_analysisMode, new[]
            {
                Text("mesh_visibility_single_view", "Single view"),
                Text("mesh_visibility_all_directions", "All directions (XYZ + diagonals)")
            });
            if (_analysisMode == AnalysisMode.AllDirections)
            {
                _directionQuality = EditorGUILayout.Popup(Text("mesh_visibility_direction_quality", "Direction Coverage"),
                    Mathf.Clamp(_directionQuality, 0, DirectionResolutions.Length - 1), new[]
                    {
                        Text("mesh_visibility_direction_fast", "Quick (26 directions)"),
                        Text("mesh_visibility_direction_balanced", "Balanced (98 directions)"),
                        Text("mesh_visibility_direction_detailed", "Detailed (386 directions)")
                    });
                EditorGUILayout.HelpBox(Text("mesh_visibility_multiview_help", "Checks both signs of X, Y and Z plus diagonal directions. A triangle seen in any checked direction is excluded. These are sampled directions, not proof of invisibility from every possible viewpoint."), MessageType.Info);
            }
            else
            {
                _viewMode = (ViewMode)EditorGUILayout.Popup(Text("mesh_visibility_view", "View Source"), (int)_viewMode, new[]
                {
                    Text("mesh_visibility_scene_view", "Current Scene View"),
                    Text("mesh_visibility_camera", "Camera"),
                    Text("mesh_visibility_direction", "Direction")
                });
                if (_viewMode == ViewMode.Camera)
                    _camera = (Camera)EditorGUILayout.ObjectField(Text("mesh_visibility_camera", "Camera"), _camera, typeof(Camera), true);
                else if (_viewMode == ViewMode.Direction)
                    _direction = EditorGUILayout.Vector3Field(Text("mesh_visibility_ray_direction", "Ray Direction Toward Model"), _direction);
            }
            _includeSceneBlockers = EditorGUILayout.ToggleLeft(Text("mesh_visibility_scene_blockers", "Include other scene renderers as blockers"), _includeSceneBlockers);
            _twoSided = EditorGUILayout.ToggleLeft(Text("mesh_visibility_two_sided", "Treat all surfaces as two-sided"), _twoSided);
            _quality = EditorGUILayout.Popup(Text("mesh_visibility_quality", "Surface Sampling"), Mathf.Clamp(_quality, 0, SampleCounts.Length - 1), new[]
            {
                Text("mesh_visibility_quality_fast", "Fast (1 sample per triangle)"),
                Text("mesh_visibility_quality_balanced", "Balanced (4 samples per triangle)"),
                Text("mesh_visibility_quality_detailed", "Detailed (7 samples per triangle)")
            });
            if (EditorGUI.EndChangeCheck()) _settingsChanged = _report != null;

            if (_root == null)
                EditorGUILayout.HelpBox(Text("mesh_visibility_select_root", "Select a scene object. Its enabled child mesh renderers are analyzed together and can block one another."), MessageType.Info);
            else if (EditorUtility.IsPersistent(_root))
                EditorGUILayout.HelpBox(Text("mesh_visibility_scene_object_required", "Choose an object in the scene rather than a prefab asset."), MessageType.Warning);
            if (_analysisMode == AnalysisMode.SingleView && _viewMode != ViewMode.Direction && ResolveCamera() == null)
                EditorGUILayout.HelpBox(Text("mesh_visibility_camera_required", "Open a Scene View or assign a camera before analyzing."), MessageType.Info);
            if (_analysisMode == AnalysisMode.SingleView && _viewMode == ViewMode.Direction && _direction.sqrMagnitude < 0.000001f)
                EditorGUILayout.HelpBox(Text("mesh_visibility_direction_required", "Enter a non-zero viewing direction."), MessageType.Info);

            EditorGUILayout.HelpBox(Text("mesh_visibility_limits", "Analysis treats geometry as opaque. Transparency, alpha cutouts, material culling, shader deformation, camera layer masks, LOD selection and render-pipeline effects are not reproduced. More samples reduce missed small visible areas but do not prove a triangle is unused."), MessageType.Info);
        }

        private Camera ResolveCamera()
        {
            if (_viewMode == ViewMode.Camera) return _camera;
            var sceneView = SceneView.lastActiveSceneView;
            return sceneView != null ? sceneView.camera : null;
        }

        private bool CanAnalyze()
        {
            if (_root == null || EditorUtility.IsPersistent(_root)) return false;
            return _analysisMode == AnalysisMode.AllDirections || (_viewMode == ViewMode.Direction ? _direction.sqrMagnitude >= 0.000001f : ResolveCamera() != null);
        }

        private void Analyze()
        {
            string title = Text("mesh_visibility_progress_title", "Analyze Mesh Visibility");
            string[] previousWarnings = _warnings;
            bool previousShowWarnings = _showWarnings;
            _warnings = Array.Empty<string>();
            try
            {
                if (EditorUtility.DisplayCancelableProgressBar(title, Text("mesh_visibility_progress_capture", "Capturing geometry..."), 0f))
                    throw new OperationCanceledException();
                var snapshot = MeshVisibilitySnapshot.Capture(_root, _includeSceneBlockers);
                _warnings = new List<string>(snapshot.Warnings).ToArray();
                bool hasTarget = false;
                foreach (var surface in snapshot.Surfaces) hasTarget |= surface.IsTarget;
                if (!hasTarget)
                    throw new InvalidOperationException(Text("mesh_visibility_no_targets", "No enabled mesh renderers with usable triangles were found under the target root."));

                Camera camera = ResolveCamera();
                Func<float, bool> cancel = progress => EditorUtility.DisplayCancelableProgressBar(title,
                    Text("mesh_visibility_progress_analyze", "Testing triangle visibility..."), progress);
                int samples = SampleCounts[Mathf.Clamp(_quality, 0, SampleCounts.Length - 1)];
                MeshVisibilityReport report;
                if (_analysisMode == AnalysisMode.AllDirections)
                    report = MeshVisibilityAnalyzer.AnalyzeAllDirections(snapshot.Surfaces, snapshot.Bounds,
                        _twoSided, samples, DirectionResolutions[Mathf.Clamp(_directionQuality, 0, DirectionResolutions.Length - 1)], cancel);
                else
                {
                    var view = _viewMode == ViewMode.Direction
                        ? MeshVisibilityView.FromDirection(snapshot.Bounds, _direction)
                        : MeshVisibilityView.FromCamera(camera);
                    report = MeshVisibilityAnalyzer.Analyze(snapshot.Surfaces, view, _twoSided, samples, cancel);
                }

                var overlay = new MeshVisibilityOverlay();
                try { overlay.Build(report); }
                catch { overlay.Dispose(); throw; }
                _overlay?.Dispose();
                _overlay = overlay;
                _report = report;
                _analysisBounds = snapshot.TargetBounds;
                CacheCounts(report);
                _settingsChanged = false;
                _status = null;
                _analyzedView = report.IsMultiView
                    ? string.Format(Text("mesh_visibility_multiview_snapshot", "{0} directions / captured pose"), report.DirectionsTested)
                    : _viewMode == ViewMode.Direction
                    ? string.Format(Text("mesh_visibility_direction_snapshot", "Direction: {0}"), _direction.normalized)
                    : string.Format(Text("mesh_visibility_camera_snapshot", "Camera: {0}"), _viewMode == ViewMode.SceneView ? Text("mesh_visibility_scene_view", "Current Scene View") : camera.name);
                SceneView.RepaintAll();
            }
            catch (OperationCanceledException)
            {
                _warnings = previousWarnings;
                _showWarnings = previousShowWarnings;
                _status = Text("mesh_visibility_canceled", "Analysis canceled. Previous results, if any, are unchanged.");
                _statusType = MessageType.Info;
            }
            catch (Exception exception)
            {
                _status = string.Format(Text("mesh_visibility_failed", "Analysis failed: {0}"), exception.Message);
                _statusType = MessageType.Error;
                _showWarnings = _warnings.Length > 0;
                Debug.LogException(exception);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void DrawResults()
        {
            if (_report == null) return;
            EditorGUILayout.LabelField(Text("mesh_visibility_results", "Analysis Results"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(_analyzedView, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.HelpBox(_report.IsMultiView
                ? Text("mesh_visibility_multiview_notice", "This result combines all checked directions for the captured pose. Rotating the inspection view does not require reanalysis; rerun after changing geometry, transforms, pose, blockers or analysis settings. Interior viewpoints and narrow openings may reveal additional faces.")
                : Text("mesh_visibility_snapshot_notice", "Results and overlays are a snapshot of this pose and view. Re-run after moving objects or the camera, changing poses, blend shapes, meshes or analysis settings. A face hidden from this view may be visible from another."), MessageType.Info);
            if (_settingsChanged)
                EditorGUILayout.HelpBox(Text("mesh_visibility_settings_changed", "Analysis settings have changed. Re-run to update these results."), MessageType.Warning);

            EditorGUILayout.LabelField(string.Format(Text("mesh_visibility_summary", "{0} renderers / {1} triangles / {2} surface samples"), _report.Surfaces.Count, _triangleCount, _report.SamplesTested), EditorStyles.wordWrappedLabel);
            DrawCounts(_totals);
            DrawOverlayControls();
            if (_report.IsMultiView) DrawAxisChecks();

            foreach (var result in _report.Surfaces)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField(result.Surface.Source, typeof(Renderer), true);
                using (new EditorGUI.DisabledScope(result.Surface.Source == null))
                {
                    if (GUILayout.Button(Text("mesh_visibility_select", "Select"), GUILayout.Width(70f)))
                    {
                        Selection.activeGameObject = result.Surface.Source.gameObject;
                        EditorGUIUtility.PingObject(result.Surface.Source);
                    }
                }
                EditorGUILayout.EndHorizontal();
                DrawCounts(_surfaceCounts[result]);
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawWarnings()
        {
            if (_warnings.Length == 0) return;
            _showWarnings = EditorGUILayout.Foldout(_showWarnings, string.Format(Text("mesh_visibility_warnings", "Geometry notes ({0})"), _warnings.Length));
            if (_showWarnings)
                foreach (string warning in _warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }

        private void DrawOverlayControls()
        {
            EditorGUI.BeginChangeCheck();
            _showOverlay = EditorGUILayout.ToggleLeft(Text("mesh_visibility_show_overlay", "Show candidate overlay in Scene View"), _showOverlay);
            using (new EditorGUI.DisabledScope(!_showOverlay))
            {
                _xray = EditorGUILayout.ToggleLeft(Text("mesh_visibility_xray", "X-ray: show candidates through other geometry"), _xray);
                if (_report.IsMultiView)
                {
                    DrawStateToggle(MeshVisibilityState.HiddenFromAllDirections, ref _showHiddenFromAllDirections,
                        Text("mesh_visibility_hidden_all", "Hidden in all checked directions"));
                    EditorGUILayout.LabelField(Text("mesh_visibility_candidate_only", "Only polygons hidden in every checked direction are highlighted."), EditorStyles.wordWrappedMiniLabel);
                }
                else
                {
                    DrawStateToggle(MeshVisibilityState.Occluded, ref _showOccluded, Text("mesh_visibility_occluded", "Occluded"));
                    DrawStateToggle(MeshVisibilityState.BackFacing, ref _showBackFacing, Text("mesh_visibility_back_facing", "Back-facing"));
                    DrawStateToggle(MeshVisibilityState.OutsideView, ref _showOutsideView, Text("mesh_visibility_outside_view", "Outside camera view"));
                }
            }
            if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
            if (_overlay != null && !_overlay.IsAvailable)
                EditorGUILayout.HelpBox(Text("mesh_visibility_overlay_unavailable", "The visibility overlay shader is unavailable. Analysis counts are still available."), MessageType.Warning);
        }

        private static void DrawStateToggle(MeshVisibilityState state, ref bool enabled, string label)
        {
            EditorGUILayout.BeginHorizontal();
            Rect swatch = GUILayoutUtility.GetRect(14f, 14f, GUILayout.Width(14f));
            Color color = MeshVisibilityOverlay.ColorFor(state);
            color.a = 1f;
            EditorGUI.DrawRect(swatch, color);
            enabled = EditorGUILayout.ToggleLeft(label, enabled);
            EditorGUILayout.EndHorizontal();
        }

        private void CacheCounts(MeshVisibilityReport report)
        {
            _totals.Clear();
            _surfaceCounts.Clear();
            _triangleCount = 0;
            foreach (var result in report.Surfaces)
            {
                var counts = new Dictionary<MeshVisibilityState, int>();
                AddCounts(counts, result.States);
                _surfaceCounts.Add(result, counts);
                foreach (var entry in counts) _totals[entry.Key] = Count(_totals, entry.Key) + entry.Value;
                _triangleCount += result.States.Length;
            }
        }

        private static void AddCounts(Dictionary<MeshVisibilityState, int> counts, MeshVisibilityState[] states)
        {
            foreach (var state in states) counts[state] = Count(counts, state) + 1;
        }

        private static int Count(Dictionary<MeshVisibilityState, int> counts, MeshVisibilityState state)
            => counts.TryGetValue(state, out int count) ? count : 0;

        private void DrawCounts(Dictionary<MeshVisibilityState, int> counts)
        {
            if (_report.IsMultiView)
            {
                int unresolved = Count(counts, MeshVisibilityState.Occluded) + Count(counts, MeshVisibilityState.BackFacing)
                    + Count(counts, MeshVisibilityState.OutsideView);
                EditorGUILayout.LabelField(string.Format(Text("mesh_visibility_multiview_counts", "Seen in any direction: {0} / Hidden in all checked directions: {1} / Unresolved: {2} / Degenerate: {3}"),
                    Count(counts, MeshVisibilityState.Visible), Count(counts, MeshVisibilityState.HiddenFromAllDirections),
                    unresolved, Count(counts, MeshVisibilityState.Degenerate)), EditorStyles.wordWrappedMiniLabel);
                return;
            }
            EditorGUILayout.LabelField(string.Format(Text("mesh_visibility_counts", "Visible: {0} / Occluded: {1} / Back-facing: {2} / Outside view: {3} / Degenerate: {4}"),
                Count(counts, MeshVisibilityState.Visible), Count(counts, MeshVisibilityState.Occluded), Count(counts, MeshVisibilityState.BackFacing), Count(counts, MeshVisibilityState.OutsideView), Count(counts, MeshVisibilityState.Degenerate)), EditorStyles.wordWrappedMiniLabel);
        }

        private MeshVisibilityDirectionSummary FindAxisCheck(Vector3 direction)
        {
            foreach (var summary in _report.DirectionSummaries)
                if (Vector3.Dot(summary.Forward, direction) > 0.99999f) return summary;
            return null;
        }

        private void DrawAxisChecks()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(Text("mesh_visibility_axes", "XYZ ray checks (visible triangles per direction)"), EditorStyles.boldLabel);
            for (int axis = 0; axis < 3; axis++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int sign = 0; sign < 2; sign++)
                {
                    int index = axis * 2 + sign;
                    var summary = FindAxisCheck(AxisDirections[index]);
                    string label = summary == null
                        ? string.Format(Text("mesh_visibility_axis_missing", "{0}: not checked"), AxisLabels[index])
                        : string.Format(Text("mesh_visibility_axis_checked", "{0}: checked ({1})"), AxisLabels[index], summary.VisibleTriangles);
                    EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.BeginChangeCheck();
            _showRayDirections = EditorGUILayout.ToggleLeft(Text("mesh_visibility_show_rays", "Show inspected XYZ ray directions"), _showRayDirections);
            if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
        }

        private void DrawSceneOverlay(SceneView sceneView)
        {
            if (_report == null || Event.current.type != EventType.Repaint) return;
            if (_showOverlay && _overlay != null)
            {
                if (_report.IsMultiView)
                    _overlay.DrawHiddenFromAllDirections(sceneView.camera, _showHiddenFromAllDirections, _xray);
                else
                    _overlay.Draw(sceneView.camera, _showOccluded, _showBackFacing, _showOutsideView, _xray);
            }
            if (_report.IsMultiView && _showRayDirections) DrawAxisRays();
        }

        private void DrawAxisRays()
        {
            float radius = Mathf.Max(_analysisBounds.extents.magnitude, 0.1f);
            Color previousColor = Handles.color;
            var previousDepth = Handles.zTest;
            try
            {
                Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
                for (int i = 0; i < AxisDirections.Length; i++)
                {
                    if (FindAxisCheck(AxisDirections[i]) == null) continue;
                    Vector3 direction = AxisDirections[i];
                    Vector3 origin = _analysisBounds.center - direction * radius * 1.25f;
                    Handles.color = i < 2 ? Handles.xAxisColor : i < 4 ? Handles.yAxisColor : Handles.zAxisColor;
                    Vector3 up = i == 2 || i == 3 ? Vector3.forward : Vector3.up;
                    Handles.ArrowHandleCap(0, origin, Quaternion.LookRotation(direction, up), radius * 0.35f, EventType.Repaint);
                    Handles.Label(origin, AxisLabels[i]);
                }
            }
            finally
            {
                Handles.color = previousColor;
                Handles.zTest = previousDepth;
            }
        }
    }
}
