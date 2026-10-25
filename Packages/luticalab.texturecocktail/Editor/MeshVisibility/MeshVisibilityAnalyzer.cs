using System;
using System.Collections.Generic;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// Tests current-pose opaque triangles with a median-split bounding volume hierarchy.
    /// Samples use centroid (1), centroid plus inset corners (4), or those plus inset
    /// edge midpoints (7). Segments run between each sample and the camera/directional
    /// near plane. Intersections start at the sample to preserve small nearby gaps even
    /// when the view is distant. No source meshes, objects or arrays are modified.
    /// </summary>
    public static class MeshVisibilityAnalyzer
    {
        private static readonly Vector3[] Samples =
        {
            new Vector3(1f / 3f, 1f / 3f, 1f / 3f),
            new Vector3(0.8f, 0.1f, 0.1f), new Vector3(0.1f, 0.8f, 0.1f), new Vector3(0.1f, 0.1f, 0.8f),
            new Vector3(0.45f, 0.45f, 0.1f), new Vector3(0.1f, 0.45f, 0.45f), new Vector3(0.45f, 0.1f, 0.45f)
        };

        /// <summary>
        /// twoSided controls both target facing and occluder backface culling.
        /// cancel receives progress in [0,1]; returning true throws OperationCanceledException.
        /// Results contain targets only, while every valid surface can block a sample.
        /// </summary>
        public static MeshVisibilityReport Analyze(IReadOnlyList<MeshVisibilitySurface> surfaces,
            MeshVisibilityView view, bool twoSided, int samplesPerTriangle, Func<float, bool> cancel = null)
        {
            ValidateView(ref view);
            return AnalyzeViews(surfaces, new[] { view }, twoSided, samplesPerTriangle, null, cancel);
        }

        /// <summary>
        /// Fits orthographic views in a nested set of directions around the bounds.
        /// Include targets AND blockers in bounds. A triangle is HiddenFromAllDirections
        /// only when no checked view sees it, at least one view establishes occlusion,
        /// and no view clips it out. This finite sample is not a proof for every direction.
        /// The captured geometry and its hierarchy are reused for all views.
        /// </summary>
        public static MeshVisibilityReport AnalyzeAllDirections(IReadOnlyList<MeshVisibilitySurface> surfaces,
            Bounds bounds, bool twoSided, int samplesPerTriangle, int directionResolution,
            Func<float, bool> cancel = null)
        {
            IReadOnlyList<Vector3> directions = MeshVisibilityDirections.Create(directionResolution);
            var views = new MeshVisibilityView[directions.Count];
            for (int i = 0; i < views.Length; i++)
            {
                views[i] = MeshVisibilityView.FromDirection(bounds, directions[i]);
                ValidateView(ref views[i]);
            }
            return AnalyzeViews(surfaces, views, twoSided, samplesPerTriangle, directions, cancel);
        }

        private static void ValidateView(ref MeshVisibilityView view)
        {
            if (!MeshVisibilityView.IsFinite(view.Position) || !MeshVisibilityView.IsFinite(view.Forward)
                || view.Forward.sqrMagnitude < 1e-12f || !MeshVisibilityView.IsFinite(view.NearClip)
                || (!view.Orthographic && view.NearClip < 0f))
                throw new ArgumentException("View position, direction and near clip must be valid.", nameof(view));
            for (int i = 0; i < 16; i++)
                if (!MeshVisibilityView.IsFinite(view.WorldToClip[i]))
                    throw new ArgumentException("View projection must be finite.", nameof(view));
            view.Forward.Normalize();
        }

        private static MeshVisibilityReport AnalyzeViews(IReadOnlyList<MeshVisibilitySurface> surfaces,
            IReadOnlyList<MeshVisibilityView> views, bool twoSided, int samplesPerTriangle,
            IReadOnlyList<Vector3> checkedDirections, Func<float, bool> cancel)
        {
            if (surfaces == null) throw new ArgumentNullException(nameof(surfaces));
            if (samplesPerTriangle != 1 && samplesPerTriangle != 4 && samplesPerTriangle != 7)
                throw new ArgumentOutOfRangeException(nameof(samplesPerTriangle), "Choose 1, 4 or 7 samples.");
            float lastProgress = 0f;
            Action<float> progress = value =>
            {
                // View-boundary and triangle-count formulas can round differently.
                lastProgress = Mathf.Max(lastProgress, Mathf.Clamp01(value));
                if (cancel != null && cancel(lastProgress)) throw new OperationCanceledException();
            };
            progress(0f);

            long totalTriangles = 0;
            var results = new List<MeshVisibilitySurfaceResult>();
            var resultBySurface = new MeshVisibilitySurfaceResult[surfaces.Count];
            for (int s = 0; s < surfaces.Count; s++)
            {
                MeshVisibilitySurface surface = surfaces[s];
                if (surface == null || surface.Vertices == null || surface.Triangles == null
                    || surface.Triangles.Length % 3 != 0)
                    throw new ArgumentException("Surfaces require vertices and complete triangle index triplets.", nameof(surfaces));
                int count = surface.Triangles.Length / 3;
                totalTriangles += count;
                if (!surface.IsTarget) continue;
                var states = new MeshVisibilityState[count];
                for (int t = 0; t < count; t++) states[t] = MeshVisibilityState.Degenerate;
                var result = new MeshVisibilitySurfaceResult(surface, states);
                results.Add(result);
                resultBySurface[s] = result;
            }

            var triangles = new List<Triangle>();
            var targets = new List<Triangle>();
            long collected = 0;
            for (int s = 0; s < surfaces.Count; s++)
            {
                MeshVisibilitySurface surface = surfaces[s];
                for (int t = 0; t < surface.Triangles.Length / 3; t++)
                {
                    if (TryCreateTriangle(surface, s, t, out Triangle triangle))
                    {
                        triangles.Add(triangle);
                        if (surface.IsTarget) targets.Add(triangle);
                    }
                    if ((++collected & 1023) == 0) progress(0.2f * collected / Math.Max(1L, totalTriangles));
                }
            }
            progress(0.2f);
            var bvh = new TriangleHierarchy(triangles.ToArray(), progress);
            progress(0.45f);

            bool multiView = checkedDirections != null;
            var evidence = multiView ? new VisibilityEvidence[targets.Count] : null;
            var summaries = new List<MeshVisibilityDirectionSummary>();
            int samplesTested = 0;
            long classified = 0;
            long classificationCount = (long)targets.Count * views.Count;
            for (int v = 0; v < views.Count; v++)
            {
                int visibleTriangles = 0;
                for (int t = 0; t < targets.Count; t++)
                {
                    Triangle triangle = targets[t];
                    MeshVisibilityState state = Classify(triangle, views[v], twoSided,
                        samplesPerTriangle, bvh, ref samplesTested);
                    if (state == MeshVisibilityState.Visible) visibleTriangles++;
                    if (multiView) evidence[t] |= ToEvidence(state);
                    else resultBySurface[triangle.SurfaceIndex].States[triangle.TriangleIndex] = state;
                    if ((++classified & 127) == 0)
                        progress(0.45f + 0.55f * classified / Math.Max(1L, classificationCount));
                }
                if (multiView)
                    summaries.Add(new MeshVisibilityDirectionSummary(checkedDirections[v], visibleTriangles));
                // Poll at every view boundary too, including small or empty meshes.
                progress(0.45f + 0.55f * (v + 1) / views.Count);
            }
            if (multiView)
            {
                for (int t = 0; t < targets.Count; t++)
                {
                    Triangle triangle = targets[t];
                    resultBySurface[triangle.SurfaceIndex].States[triangle.TriangleIndex] = Aggregate(evidence[t]);
                }
            }
            progress(1f);
            return multiView
                ? new MeshVisibilityReport(results.AsReadOnly(), samplesTested, checkedDirections, summaries.AsReadOnly())
                : new MeshVisibilityReport(results.AsReadOnly(), samplesTested);
        }

        [Flags]
        private enum VisibilityEvidence { None = 0, Visible = 1, Occluded = 2, BackFacing = 4, OutsideView = 8 }

        private static VisibilityEvidence ToEvidence(MeshVisibilityState state)
        {
            switch (state)
            {
                case MeshVisibilityState.Visible: return VisibilityEvidence.Visible;
                case MeshVisibilityState.Occluded: return VisibilityEvidence.Occluded;
                case MeshVisibilityState.BackFacing: return VisibilityEvidence.BackFacing;
                case MeshVisibilityState.OutsideView: return VisibilityEvidence.OutsideView;
                default: return VisibilityEvidence.None;
            }
        }

        private static MeshVisibilityState Aggregate(VisibilityEvidence evidence)
        {
            if ((evidence & VisibilityEvidence.Visible) != 0) return MeshVisibilityState.Visible;
            if ((evidence & VisibilityEvidence.OutsideView) != 0) return MeshVisibilityState.OutsideView;
            if ((evidence & VisibilityEvidence.Occluded) != 0) return MeshVisibilityState.HiddenFromAllDirections;
            return (evidence & VisibilityEvidence.BackFacing) != 0
                ? MeshVisibilityState.BackFacing : MeshVisibilityState.Degenerate;
        }

        private static MeshVisibilityState Classify(Triangle triangle, MeshVisibilityView view, bool twoSided,
            int sampleCount, TriangleHierarchy bvh, ref int tested)
        {
            bool inView = false;
            Vector3 towardTriangle = view.Orthographic ? view.Forward : triangle.Centroid - view.Position;
            bool backFacing = !twoSided && Vector3.Dot(triangle.Normal, towardTriangle) >= 0f;
            float triangleScale = Mathf.Max(triangle.Edge1.magnitude, triangle.Edge2.magnitude);
            for (int i = 0; i < sampleCount; i++)
            {
                Vector3 weights = Samples[i];
                Vector3 sample = triangle.A + triangle.Edge1 * weights.y + triangle.Edge2 * weights.z;
                tested = checked(tested + 1);
                Vector4 clip = view.WorldToClip * new Vector4(sample.x, sample.y, sample.z, 1f);
                float limit = clip.w * 1.00001f;
                if (clip.w <= 0f || clip.x < -limit || clip.x > limit || clip.y < -limit || clip.y > limit
                    || clip.z < -limit || clip.z > limit) continue;
                inView = true;
                if (backFacing) continue;

                Vector3 direction;
                float distance;
                if (view.Orthographic)
                {
                    direction = view.Forward;
                    distance = Vector3.Dot(sample - view.Position, direction) - view.NearClip;
                }
                else
                {
                    Vector3 delta = sample - view.Position;
                    float length = delta.magnitude;
                    if (length < 1e-12f) return MeshVisibilityState.Visible;
                    direction = delta / length;
                    float alignment = Vector3.Dot(direction, view.Forward);
                    if (alignment <= 0f) continue;
                    float nearDistance = view.NearClip / alignment;
                    distance = length - nearDistance;
                }
                // Keep the sample-end exclusion local: a distant viewer must not erase a
                // small gap between the target and a nearby blocker. The far endpoint has
                // its own float-precision allowance so geometry on the near plane counts.
                float sampleScale = Mathf.Max(triangleScale,
                    Mathf.Max(Mathf.Abs(sample.x), Mathf.Max(Mathf.Abs(sample.y), Mathf.Abs(sample.z))));
                float sampleTolerance = Mathf.Max(1e-7f, sampleScale * 2.4e-7f);
                float nearPlaneTolerance = Mathf.Max(1e-7f, Mathf.Abs(distance) * 2.4e-7f);
                if (distance <= sampleTolerance || !bvh.IsOccluded(sample, -direction,
                    distance + nearPlaneTolerance, sampleTolerance, triangle.SurfaceIndex,
                    triangle.TriangleIndex, twoSided)) return MeshVisibilityState.Visible;
            }
            if (!inView) return MeshVisibilityState.OutsideView;
            return backFacing ? MeshVisibilityState.BackFacing : MeshVisibilityState.Occluded;
        }

        private static bool TryCreateTriangle(MeshVisibilitySurface surface, int surfaceIndex, int triangleIndex,
            out Triangle triangle)
        {
            triangle = default;
            int offset = triangleIndex * 3;
            int ia = surface.Triangles[offset], ib = surface.Triangles[offset + 1], ic = surface.Triangles[offset + 2];
            if (ia < 0 || ib < 0 || ic < 0 || ia >= surface.Vertices.Length || ib >= surface.Vertices.Length
                || ic >= surface.Vertices.Length) return false;
            Vector3 a = surface.Vertices[ia], b = surface.Vertices[ib], c = surface.Vertices[ic];
            if (!MeshVisibilityView.IsFinite(a) || !MeshVisibilityView.IsFinite(b) || !MeshVisibilityView.IsFinite(c)) return false;
            Vector3 edge1 = b - a, edge2 = c - a;
            Vector3 normal = Vector3.Cross(edge1, edge2);
            if (normal.sqrMagnitude <= 1e-12f * edge1.sqrMagnitude * edge2.sqrMagnitude) return false;
            triangle = new Triangle
            {
                A = a, Edge1 = edge1, Edge2 = edge2, Normal = normal,
                Minimum = Vector3.Min(a, Vector3.Min(b, c)), Maximum = Vector3.Max(a, Vector3.Max(b, c)),
                Centroid = (a + b + c) / 3f, SurfaceIndex = surfaceIndex, TriangleIndex = triangleIndex
            };
            return true;
        }

        private struct Triangle
        {
            public Vector3 A, Edge1, Edge2, Normal, Minimum, Maximum, Centroid;
            public int SurfaceIndex, TriangleIndex;
        }

        private sealed class TriangleHierarchy
        {
            private const int LeafSize = 8;
            private readonly Triangle[] _triangles;
            private readonly List<Node> _nodes = new List<Node>();
            private readonly Action<float> _progress;
            private int _builtTriangles;
            private static readonly AxisComparer[] Comparers = { new AxisComparer(0), new AxisComparer(1), new AxisComparer(2) };

            internal TriangleHierarchy(Triangle[] triangles, Action<float> progress)
            {
                _triangles = triangles;
                _progress = progress;
                if (triangles.Length > 0) Build(0, triangles.Length);
            }

            private int Build(int start, int count)
            {
                Vector3 min = _triangles[start].Minimum, max = _triangles[start].Maximum;
                Vector3 centroidMin = _triangles[start].Centroid, centroidMax = centroidMin;
                for (int i = start + 1; i < start + count; i++)
                {
                    min = Vector3.Min(min, _triangles[i].Minimum);
                    max = Vector3.Max(max, _triangles[i].Maximum);
                    centroidMin = Vector3.Min(centroidMin, _triangles[i].Centroid);
                    centroidMax = Vector3.Max(centroidMax, _triangles[i].Centroid);
                }
                int index = _nodes.Count;
                _nodes.Add(default);
                var node = new Node { Minimum = min, Maximum = max, Start = start, Count = count, Left = -1, Right = -1 };
                if (count > LeafSize)
                {
                    Vector3 span = centroidMax - centroidMin;
                    int axis = span.x >= span.y && span.x >= span.z ? 0 : span.y >= span.z ? 1 : 2;
                    Array.Sort(_triangles, start, count, Comparers[axis]);
                    int leftCount = count / 2;
                    node.Left = Build(start, leftCount);
                    node.Right = Build(start + leftCount, count - leftCount);
                }
                else
                {
                    _builtTriangles += count;
                    if ((_nodes.Count & 127) == 0 || _builtTriangles == _triangles.Length)
                        _progress(0.2f + 0.25f * _builtTriangles / Math.Max(1, _triangles.Length));
                }
                _nodes[index] = node;
                return index;
            }

            internal bool IsOccluded(Vector3 origin, Vector3 direction, float maxDistance, float sampleTolerance,
                int surfaceIndex, int triangleIndex, bool twoSided)
            {
                return _nodes.Count > 0 && Query(0, origin, direction, maxDistance, sampleTolerance, surfaceIndex, triangleIndex, twoSided);
            }

            private bool Query(int index, Vector3 origin, Vector3 direction, float maxDistance, float sampleTolerance,
                int surfaceIndex, int triangleIndex, bool twoSided)
            {
                Node node = _nodes[index];
                if (!IntersectsBounds(node, origin, direction, maxDistance)) return false;
                if (node.Left >= 0)
                    return Query(node.Left, origin, direction, maxDistance, sampleTolerance, surfaceIndex, triangleIndex, twoSided)
                        || Query(node.Right, origin, direction, maxDistance, sampleTolerance, surfaceIndex, triangleIndex, twoSided);
                for (int i = node.Start; i < node.Start + node.Count; i++)
                {
                    Triangle triangle = _triangles[i];
                    if (triangle.SurfaceIndex == surfaceIndex && triangle.TriangleIndex == triangleIndex) continue;
                    Vector3 p = Vector3.Cross(direction, triangle.Edge2);
                    float determinant = Vector3.Dot(triangle.Edge1, p);
                    float parallelTolerance = triangle.Normal.magnitude * 1e-7f;
                    // Rays travel toward the viewer, so single-sided facing uses the
                    // opposite determinant sign from a conventional camera-to-scene ray.
                    if (twoSided ? Mathf.Abs(determinant) <= parallelTolerance : determinant >= -parallelTolerance) continue;
                    float inverse = 1f / determinant;
                    Vector3 fromA = origin - triangle.A;
                    float u = Vector3.Dot(fromA, p) * inverse;
                    if (u < -1e-6f || u > 1.000001f) continue;
                    Vector3 q = Vector3.Cross(fromA, triangle.Edge1);
                    float v = Vector3.Dot(direction, q) * inverse;
                    if (v < -1e-6f || u + v > 1.000001f) continue;
                    float hit = Vector3.Dot(triangle.Edge2, q) * inverse;
                    if (hit > sampleTolerance && hit <= maxDistance) return true;
                }
                return false;
            }

            private static bool IntersectsBounds(Node node, Vector3 origin, Vector3 direction, float maxDistance)
            {
                float lower = 0f, upper = maxDistance;
                for (int axis = 0; axis < 3; axis++)
                {
                    if (Mathf.Abs(direction[axis]) < 1e-12f)
                    {
                        if (origin[axis] < node.Minimum[axis] || origin[axis] > node.Maximum[axis]) return false;
                        continue;
                    }
                    float a = (node.Minimum[axis] - origin[axis]) / direction[axis];
                    float b = (node.Maximum[axis] - origin[axis]) / direction[axis];
                    lower = Mathf.Max(lower, Mathf.Min(a, b));
                    upper = Mathf.Min(upper, Mathf.Max(a, b));
                    if (upper < lower) return false;
                }
                return true;
            }

            private struct Node
            {
                public Vector3 Minimum, Maximum;
                public int Start, Count, Left, Right;
            }

            private sealed class AxisComparer : IComparer<Triangle>
            {
                private readonly int _axis;
                internal AxisComparer(int axis) { _axis = axis; }
                public int Compare(Triangle x, Triangle y) => x.Centroid[_axis].CompareTo(y.Centroid[_axis]);
            }
        }
    }
}
