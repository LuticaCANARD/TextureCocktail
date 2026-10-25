using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// Owns only temporary world-space candidate meshes and overlay materials. Source mesh
    /// assets are never modified. UVs are deliberately unused so overlapping UV islands do
    /// not cause unrelated triangles to be highlighted.
    /// </summary>
    internal sealed class MeshVisibilityOverlay : IDisposable
    {
        private const string ShaderName = "Hidden/TextureCocktail/MeshVisibilityOverlay";
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int ZTestProperty = Shader.PropertyToID("_ZTest");
        private static readonly MeshVisibilityState[] CandidateStates =
        {
            MeshVisibilityState.Occluded, MeshVisibilityState.BackFacing, MeshVisibilityState.OutsideView,
            MeshVisibilityState.HiddenFromAllDirections
        };

        private sealed class Group
        {
            internal Mesh Mesh;
            internal MeshVisibilityState State;
        }

        private readonly List<Group> _groups = new List<Group>();
        private readonly Dictionary<MeshVisibilityState, Material> _materials = new Dictionary<MeshVisibilityState, Material>();
        internal bool IsAvailable { get; private set; }

        internal void Build(MeshVisibilityReport report)
        {
            Dispose();
            Shader shader = Shader.Find(ShaderName);
            if (shader == null) return;
            foreach (var state in CandidateStates)
            {
                var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                material.SetColor(ColorProperty, ColorFor(state));
                _materials.Add(state, material);
            }
            foreach (var result in report.Surfaces)
            {
                foreach (var state in CandidateStates)
                {
                    var triangles = new List<int>();
                    for (int face = 0; face < result.States.Length; face++)
                    {
                        if (result.States[face] != state) continue;
                        int first = face * 3;
                        triangles.Add(result.Surface.Triangles[first]);
                        triangles.Add(result.Surface.Triangles[first + 1]);
                        triangles.Add(result.Surface.Triangles[first + 2]);
                    }
                    if (triangles.Count == 0) continue;
                    var mesh = new Mesh
                    {
                        name = "TextureCocktail_Visibility_" + state,
                        hideFlags = HideFlags.HideAndDontSave,
                        indexFormat = IndexFormat.UInt32
                    };
                    // Track ownership before assigning buffers so a failed allocation is cleaned up.
                    _groups.Add(new Group { Mesh = mesh, State = state });
                    mesh.vertices = result.Surface.Vertices;
                    mesh.SetTriangles(triangles, 0);
                    mesh.RecalculateBounds();
                }
            }
            IsAvailable = true;
        }

        internal void Draw(Camera camera, bool occluded, bool backFacing, bool outsideView, bool xray)
        {
            DrawGroups(camera, occluded, backFacing, outsideView, false, xray);
        }

        internal void DrawHiddenFromAllDirections(Camera camera, bool show, bool xray)
        {
            DrawGroups(camera, false, false, false, show, xray);
        }

        private void DrawGroups(Camera camera, bool occluded, bool backFacing, bool outsideView, bool hiddenFromAllDirections, bool xray)
        {
            if (!IsAvailable || camera == null) return;
            foreach (var group in _groups)
            {
                bool show = group.State == MeshVisibilityState.Occluded ? occluded
                    : group.State == MeshVisibilityState.BackFacing ? backFacing
                    : group.State == MeshVisibilityState.OutsideView ? outsideView : hiddenFromAllDirections;
                if (!show) continue;
                Material material = _materials[group.State];
                material.SetInt(ZTestProperty, (int)(xray ? CompareFunction.Always : CompareFunction.LessEqual));
                Graphics.DrawMesh(group.Mesh, Matrix4x4.identity, material, 0, camera, 0, null, ShadowCastingMode.Off, false);
            }
        }

        internal static Color ColorFor(MeshVisibilityState state)
        {
            switch (state)
            {
                case MeshVisibilityState.HiddenFromAllDirections: return new Color(0.85f, 0.2f, 1f, 0.6f);
                case MeshVisibilityState.Occluded: return new Color(1f, 0.2f, 0.2f, 0.5f);
                case MeshVisibilityState.BackFacing: return new Color(1f, 0.65f, 0.1f, 0.5f);
                case MeshVisibilityState.OutsideView: return new Color(0.2f, 0.6f, 1f, 0.45f);
                default: return Color.clear;
            }
        }

        public void Dispose()
        {
            foreach (var group in _groups)
                if (group.Mesh != null) UnityEngine.Object.DestroyImmediate(group.Mesh);
            _groups.Clear();
            foreach (var material in _materials.Values)
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
            _materials.Clear();
            IsAvailable = false;
        }
    }
}
