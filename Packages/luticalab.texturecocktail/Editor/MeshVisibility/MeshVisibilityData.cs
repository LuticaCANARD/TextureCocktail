using System;
using System.Collections.Generic;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>A snapshot of current-pose world-space triangles; the analyzer never changes its source.</summary>
    public sealed class MeshVisibilitySurface
    {
        public Renderer Source;
        public Vector3[] Vertices;
        public int[] Triangles;
        public bool IsTarget = true;

        public MeshVisibilitySurface() { }

        public MeshVisibilitySurface(Renderer source, Vector3[] vertices, int[] triangles, bool isTarget = true)
        {
            Source = source;
            Vertices = vertices;
            Triangles = triangles;
            IsTarget = isTarget;
        }
    }

    /// <summary>
    /// CPU projection with clip coordinates -w..w on all axes. Forward points from the
    /// viewer toward the scene. Geometry is treated as opaque; material alpha, renderer
    /// effects, camera culling masks and the final rendered image are not evaluated here.
    /// </summary>
    public struct MeshVisibilityView
    {
        public Matrix4x4 WorldToClip;
        public Vector3 Position;
        public Vector3 Forward;
        public bool Orthographic;
        public float NearClip;

        public static MeshVisibilityView FromCamera(Camera camera)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            return new MeshVisibilityView
            {
                WorldToClip = camera.projectionMatrix * camera.worldToCameraMatrix,
                Position = camera.transform.position,
                Forward = camera.transform.forward,
                Orthographic = camera.orthographic,
                NearClip = camera.nearClipPlane
            };
        }

        /// <summary>
        /// Fits an orthographic view around the supplied bounds. Bounds must include
        /// targets AND blockers so a blocker in front of the target is not near-clipped.
        /// direction is the viewing direction, e.g. +Z views the scene from its -Z side.
        /// </summary>
        public static MeshVisibilityView FromDirection(Bounds bounds, Vector3 direction)
        {
            if (!IsFinite(direction) || direction.sqrMagnitude < 1e-12f)
                throw new ArgumentException("Viewing direction must be finite and non-zero.", nameof(direction));
            if (!IsFinite(bounds.center) || !IsFinite(bounds.extents))
                throw new ArgumentException("View bounds must be finite.", nameof(bounds));

            Vector3 forward = direction.normalized;
            Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            Quaternion rotation = Quaternion.LookRotation(forward, up);
            Vector3 right = rotation * Vector3.right;
            up = rotation * Vector3.up;
            Vector3 extent = bounds.extents;
            float halfWidth = ProjectedExtent(extent, right);
            float halfHeight = ProjectedExtent(extent, up);
            float halfDepth = ProjectedExtent(extent, forward);
            float padding = Mathf.Max(extent.magnitude * 0.01f, 0.001f);
            float near = padding;
            Vector3 position = bounds.center - forward * (halfDepth + padding + near);
            Matrix4x4 worldToView = Matrix4x4.Scale(new Vector3(1f, 1f, -1f))
                * Matrix4x4.TRS(position, rotation, Vector3.one).inverse;
            Matrix4x4 projection = Matrix4x4.Ortho(
                -halfWidth - padding, halfWidth + padding,
                -halfHeight - padding, halfHeight + padding,
                near, near + 2f * (halfDepth + padding));
            return new MeshVisibilityView
            {
                WorldToClip = projection * worldToView,
                Position = position,
                Forward = forward,
                Orthographic = true,
                NearClip = near
            };
        }

        private static float ProjectedExtent(Vector3 extent, Vector3 axis)
            => Mathf.Abs(axis.x) * extent.x + Mathf.Abs(axis.y) * extent.y + Mathf.Abs(axis.z) * extent.z;

        internal static bool IsFinite(Vector3 value)
            => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public enum MeshVisibilityState
    {
        Visible, Occluded, BackFacing, OutsideView, Degenerate,
        // Keep existing values stable for saved selections and integrations.
        HiddenFromAllDirections
    }

    public sealed class MeshVisibilitySurfaceResult
    {
        public MeshVisibilitySurface Surface { get; }
        public MeshVisibilityState[] States { get; }

        public MeshVisibilitySurfaceResult(MeshVisibilitySurface surface, MeshVisibilityState[] states)
        {
            Surface = surface;
            States = states;
        }
    }

    /// <summary>One checked viewing direction and the number of target triangles it sees.</summary>
    public sealed class MeshVisibilityDirectionSummary
    {
        public Vector3 Forward { get; }
        public int VisibleTriangles { get; }

        public MeshVisibilityDirectionSummary(Vector3 forward, int visibleTriangles)
        {
            Forward = forward;
            VisibleTriangles = visibleTriangles;
        }
    }

    /// <summary>
    /// Sampled visibility candidates for target surfaces only. A triangle is Visible if
    /// any sample in any checked view is visible. HiddenFromAllDirections requires actual
    /// occlusion and no clipped-out views; it is not proof of continuous invisibility.
    /// Geometry represents the captured pose. Direction summaries are available for
    /// multi-view analyses; the original single-view constructor keeps them empty.
    /// </summary>
    public sealed class MeshVisibilityReport
    {
        public IReadOnlyList<MeshVisibilitySurfaceResult> Surfaces { get; }
        public int SamplesTested { get; }
        public bool IsMultiView { get; }
        public int DirectionsTested => CheckedDirections.Count;
        public IReadOnlyList<Vector3> CheckedDirections { get; }
        public IReadOnlyList<MeshVisibilityDirectionSummary> DirectionSummaries { get; }

        public MeshVisibilityReport(IReadOnlyList<MeshVisibilitySurfaceResult> surfaces, int samplesTested)
        {
            Surfaces = surfaces;
            SamplesTested = samplesTested;
            CheckedDirections = Array.Empty<Vector3>();
            DirectionSummaries = Array.Empty<MeshVisibilityDirectionSummary>();
        }

        public MeshVisibilityReport(IReadOnlyList<MeshVisibilitySurfaceResult> surfaces, int samplesTested,
            IReadOnlyList<Vector3> checkedDirections, IReadOnlyList<MeshVisibilityDirectionSummary> directionSummaries)
        {
            if (checkedDirections == null) throw new ArgumentNullException(nameof(checkedDirections));
            if (directionSummaries == null) throw new ArgumentNullException(nameof(directionSummaries));
            if (checkedDirections.Count == 0 || checkedDirections.Count != directionSummaries.Count)
                throw new ArgumentException("Multi-view reports require a summary for every checked direction.");
            Surfaces = surfaces;
            SamplesTested = samplesTested;
            IsMultiView = true;
            CheckedDirections = checkedDirections;
            DirectionSummaries = directionSummaries;
        }
    }
}
