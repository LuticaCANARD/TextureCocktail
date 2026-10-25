using UiText = LuticaLab.TextureCocktail.TextureCocktailLocalization;
using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// Copies currently posed triangle meshes into world-space arrays for CPU visibility checks.
    /// Scene objects and source mesh assets are borrowed and never changed. Temporary skin bakes
    /// are destroyed before Capture returns, so the snapshot has no native resources to dispose.
    /// </summary>
    public sealed class MeshVisibilitySnapshot
    {
        public IReadOnlyList<MeshVisibilitySurface> Surfaces { get; }
        /// <summary>Bounds of all captured geometry, including optional scene blockers.</summary>
        public Bounds Bounds { get; }
        /// <summary>Bounds of the selected hierarchy's captured geometry only.</summary>
        public Bounds TargetBounds { get; }
        public IReadOnlyList<string> Warnings { get; }

        private MeshVisibilitySnapshot(List<MeshVisibilitySurface> surfaces, Bounds bounds,
            Bounds targetBounds, List<string> warnings)
        {
            Surfaces = surfaces.AsReadOnly();
            Bounds = bounds;
            TargetBounds = targetBounds;
            Warnings = warnings.AsReadOnly();
        }

        /// <summary>
        /// Captures active, enabled MeshRenderer/SkinnedMeshRenderer children as targets.
        /// Optional blockers are other active, enabled mesh renderers in loaded non-preview
        /// scenes. Persistent assets, preview scenes, unsupported renderers and invalid meshes
        /// are excluded. Editor read-only mesh data supports disabled Read/Write import settings;
        /// unavailable or malformed geometry is reported through Warnings.
        /// </summary>
        public static MeshVisibilitySnapshot Capture(GameObject root, bool includeSceneBlockers)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));

            var surfaces = new List<MeshVisibilitySurface>();
            var warnings = new List<string>();
            var targetRenderers = new HashSet<Renderer>();
            Bounds targetBounds = new Bounds(root.transform.position, Vector3.zero);
            bool haveTargets = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(false))
            {
                if (!CanCapture(renderer)) continue;
                targetRenderers.Add(renderer);
                MeshVisibilitySurface surface = CaptureRenderer(renderer, true, warnings);
                if (surface == null) continue;
                surfaces.Add(surface);
                IncludeVertices(ref targetBounds, ref haveTargets, surface.Vertices);
            }

            if (!haveTargets)
            {
                warnings.Add(UiText.Text("The selected hierarchy has no active, enabled, readable triangle meshes to analyze."));
                return new MeshVisibilitySnapshot(surfaces, targetBounds, targetBounds, warnings);
            }

            Bounds bounds = targetBounds;
            bool haveBounds = true;
            if (includeSceneBlockers)
            {
                Renderer[] renderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
                Array.Sort(renderers, (left, right) => left.GetInstanceID().CompareTo(right.GetInstanceID()));
                foreach (Renderer renderer in renderers)
                {
                    if (targetRenderers.Contains(renderer) || !CanCapture(renderer)) continue;
                    MeshVisibilitySurface surface = CaptureRenderer(renderer, false, warnings);
                    if (surface == null) continue;
                    surfaces.Add(surface);
                    IncludeVertices(ref bounds, ref haveBounds, surface.Vertices);
                }
            }
            return new MeshVisibilitySnapshot(surfaces, bounds, targetBounds, warnings);
        }

        private static bool CanCapture(Renderer renderer)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) return false;
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) return false;
            if (EditorUtility.IsPersistent(renderer)) return false;
            var scene = renderer.gameObject.scene;
            return scene.IsValid() && scene.isLoaded && !EditorSceneManager.IsPreviewScene(scene);
        }

        private static MeshVisibilitySurface CaptureRenderer(Renderer renderer, bool isTarget, List<string> warnings)
        {
            Mesh baked = null;
            try
            {
                Mesh mesh;
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    if (skinned.sharedMesh == null)
                    {
                        Warn(warnings, renderer, "has no shared mesh.");
                        return null;
                    }
                    baked = new Mesh { name = "TextureCocktail Visibility Bake", hideFlags = HideFlags.HideAndDontSave };
                    // Compensate renderer scale during baking so the Transform matrix below
                    // applies it exactly once. BakeMesh(false) retains scale in its vertices.
                    skinned.BakeMesh(baked, true);
                    mesh = baked;
                }
                else
                {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    mesh = filter != null ? filter.sharedMesh : null;
                }

                if (mesh == null)
                {
                    Warn(warnings, renderer, "has no mesh to capture.");
                    return null;
                }
                // The Editor API can read imported mesh data even when runtime Read/Write
                // access is disabled. It does not change import settings or the source mesh.
                using (Mesh.MeshDataArray meshData = MeshUtility.AcquireReadOnlyMeshData(mesh))
                {
                    Mesh.MeshData data = meshData[0];
                    if (data.vertexCount == 0)
                    {
                        Warn(warnings, renderer, "has no vertices.");
                        return null;
                    }
                    Vector3[] vertices;
                    using (var nativeVertices = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp,
                        NativeArrayOptions.UninitializedMemory))
                    {
                        data.GetVertices(nativeVertices);
                        vertices = nativeVertices.ToArray();
                    }

                    // BakeMesh vertices are relative to the SkinnedMeshRenderer's Transform,
                    // not its rendering matrix. Static meshes keep the rendering matrix so
                    // coordinates adjusted by batching continue to be interpreted correctly.
                    Matrix4x4 toWorld = renderer is SkinnedMeshRenderer
                        ? renderer.transform.localToWorldMatrix
                        : renderer.localToWorldMatrix;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        vertices[i] = toWorld.MultiplyPoint3x4(vertices[i]);
                        if (!IsFinite(vertices[i]))
                        {
                            Warn(warnings, renderer, "contains non-finite vertex positions or transforms.");
                            return null;
                        }
                    }

                    var triangles = new List<int>();
                    for (int submesh = 0; submesh < data.subMeshCount; submesh++)
                    {
                        var descriptor = data.GetSubMesh(submesh);
                        if (descriptor.topology != MeshTopology.Triangles)
                        {
                            Warn(warnings, renderer, "submesh {0} is not triangle topology and was skipped.", submesh);
                            continue;
                        }
                        using (var indices = new NativeArray<int>(descriptor.indexCount, Allocator.Temp,
                            NativeArrayOptions.UninitializedMemory))
                        {
                            data.GetIndices(indices, submesh, true);
                            bool invalid = indices.Length % 3 != 0;
                            for (int i = 0; !invalid && i < indices.Length; i++)
                                invalid = indices[i] < 0 || indices[i] >= vertices.Length;
                            if (invalid)
                            {
                                Warn(warnings, renderer, "submesh {0} has invalid triangle indices and was skipped.", submesh);
                                continue;
                            }
                            for (int i = 0; i < indices.Length; i++) triangles.Add(indices[i]);
                        }
                    }
                    if (triangles.Count == 0)
                    {
                        Warn(warnings, renderer, "has no valid triangle submeshes.");
                        return null;
                    }
                    return new MeshVisibilitySurface(renderer, vertices, triangles.ToArray(), isTarget);
                }
            }
            catch (Exception exception)
            {
                Warn(warnings, renderer, "could not be captured: {0}", exception.Message);
                return null;
            }
            finally
            {
                if (baked != null) UnityEngine.Object.DestroyImmediate(baked);
            }
        }

        private static void IncludeVertices(ref Bounds bounds, ref bool initialized, Vector3[] vertices)
        {
            foreach (Vector3 vertex in vertices)
            {
                if (!initialized)
                {
                    bounds = new Bounds(vertex, Vector3.zero);
                    initialized = true;
                }
                else bounds.Encapsulate(vertex);
            }
        }

        private static bool IsFinite(Vector3 point)
        {
            return !(float.IsNaN(point.x) || float.IsInfinity(point.x)
                || float.IsNaN(point.y) || float.IsInfinity(point.y)
                || float.IsNaN(point.z) || float.IsInfinity(point.z));
        }

        private static void Warn(List<string> warnings, Renderer renderer, string message, params object[] values)
            => warnings.Add(renderer.name + " (" + renderer.GetType().Name + ") " + UiText.Format(message, values));
    }
}
