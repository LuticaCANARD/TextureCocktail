// Copy this file into Assets/Editor, then use
// LuticaLab > Examples > Create Mesh Visibility Demo.
// A separate scene and a new asset folder are created on every run.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TextureCocktailDocumentation
{
    /// <summary>
    /// A self-contained workshop example for visibility inspection.
    /// Its open doorway faces -Z; the rear wall is on +Z. No external assets are used.
    /// </summary>
    public static class MeshVisibilityDemo
    {
        public const string RootName = "Workshop visibility demo";
        private const string AssetRoot = "Assets/TextureCocktailVisibilityDemo";
        private const string SaveScenesMessage = "Save untitled scenes before creating the visibility example, then run this command again. Existing scenes are unchanged.";

        [MenuItem("LuticaLab/Examples/Create Mesh Visibility Demo")]
        public static void CreateFromMenu()
        {
            if (HasUntitledScenes())
            {
                EditorUtility.DisplayDialog("Save untitled scenes first", SaveScenesMessage, "OK");
                return;
            }
            GameObject root = Create();
            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();
        }

        /// <summary>
        /// Preserves open scenes and existing assets. Returns the workshop root in a new
        /// additive scene; save that scene normally if you want to keep the example.
        /// </summary>
        public static GameObject Create()
        {
            // Validate before creating folders or assets; never save or close user scenes.
            if (HasUntitledScenes()) throw new InvalidOperationException(SaveScenesMessage);
            Shader shader = Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException("This example requires the Built-in Render Pipeline Standard shader.");

            if (!AssetDatabase.IsValidFolder(AssetRoot))
                AssetDatabase.CreateFolder("Assets", "TextureCocktailVisibilityDemo");
            string folder = AssetDatabase.GenerateUniqueAssetPath(AssetRoot + "/Example");
            AssetDatabase.CreateFolder(AssetRoot, folder.Substring(folder.LastIndexOf('/') + 1));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create visibility example");
            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create visibility example");

            Material support = MaterialAsset(folder, "Steel supports", new Color(0.28f, 0.40f, 0.43f), shader);
            Material plaster = MaterialAsset(folder, "Warm plaster", new Color(0.82f, 0.79f, 0.68f), shader);
            Material roof = MaterialAsset(folder, "Blue roof", new Color(0.10f, 0.26f, 0.38f), shader);
            Material timber = MaterialAsset(folder, "Timber trim", new Color(0.36f, 0.20f, 0.09f), shader);
            Material crate = MaterialAsset(folder, "Ochre crates", new Color(0.88f, 0.48f, 0.12f), shader);
            Material concrete = MaterialAsset(folder, "Concrete", new Color(0.40f, 0.44f, 0.45f), shader);
            Mesh box = BoxMesh(); SaveMesh(folder, box, "Flat shaded box");
            Mesh gable = GableMesh(); SaveMesh(folder, gable, "Gable infill");

            Transform supports = Group(root.transform, "Supports parts");
            Transform walls = Group(root.transform, "Walls parts");
            Transform crates = Group(root.transform, "Crates parts");
            Transform roofParts = Group(root.transform, "Roof parts");
            Transform entrance = Group(root.transform, "Entrance parts");
            Transform floor = Group(root.transform, "Floor parts");

            // Each support lies strictly inside a solid wall, with clearance on every side.
            foreach (int side in new[] {-1, 1})
            {
                foreach (float z in new[] {-0.95f, 0f, 0.95f})
                    Part(supports, "Wall post", box, support, new Vector3(side * 1.6f, 1.5f, z), new Vector3(0.14f, 2.2f, 0.14f));
                Part(supports, "Wall beam", box, support, new Vector3(side * 1.6f, 2.5f, 0), new Vector3(0.16f, 0.16f, 2.4f));
                Part(supports, "Rear post", box, support, new Vector3(side * 1.0f, 1.5f, 1.4f), new Vector3(0.14f, 2.2f, 0.14f));
                Part(walls, "Side wall", box, plaster, new Vector3(side * 1.6f, 1.5f, 0), new Vector3(0.32f, 2.6f, 2.8f));
                Part(walls, "Doorway side", box, plaster, new Vector3(side * 1.23f, 1.5f, -1.4f), new Vector3(1.06f, 2.6f, 0.32f));
            }
            Part(supports, "Rear beam", box, support, new Vector3(0, 2.5f, 1.4f), new Vector3(2.7f, 0.16f, 0.16f));
            Part(walls, "Rear wall", box, plaster, new Vector3(0, 1.5f, 1.4f), new Vector3(3.52f, 2.6f, 0.32f));
            Part(walls, "Doorway lintel", box, plaster, new Vector3(0, 2.52f, -1.4f), new Vector3(1.4f, 0.56f, 0.32f));
            Part(walls, "Front gable", gable, plaster, new Vector3(0, 2.8f, -1.4f), new Vector3(3.2f, 0.65f, 0.32f));
            Part(walls, "Rear gable", gable, plaster, new Vector3(0, 2.8f, 1.4f), new Vector3(3.2f, 0.65f, 0.32f));

            // Crates are visible through the doorway from the front, but hidden by the rear wall.
            Part(crates, "Lower crate", box, crate, new Vector3(0, 0.68f, -0.10f), new Vector3(1.05f, 0.90f, 0.80f));
            Part(crates, "Upper crate", box, crate, new Vector3(0.05f, 1.47f, 0.10f), new Vector3(0.78f, 0.68f, 0.70f));
            foreach (int side in new[] {-1, 1})
            {
                Part(crates, "Lower crate batten", box, timber, new Vector3(side * 0.32f, 0.68f, -0.515f), new Vector3(0.065f, 0.82f, 0.035f));
                Part(crates, "Upper crate batten", box, timber, new Vector3(0.05f + side * 0.20f, 1.47f, -0.2675f), new Vector3(0.055f, 0.60f, 0.035f));
            }

            // Two pitched roof slabs provide a clear building silhouette without an open roof.
            float slope = Mathf.Atan2(0.76f, 1.85f) * Mathf.Rad2Deg;
            float length = Mathf.Sqrt(1.85f * 1.85f + 0.76f * 0.76f);
            Part(roofParts, "Left roof slope", box, roof, new Vector3(-0.925f, 3.17f, 0), new Vector3(length, 0.16f, 3.42f), Quaternion.Euler(0, 0, slope));
            Part(roofParts, "Right roof slope", box, roof, new Vector3(0.925f, 3.17f, 0), new Vector3(length, 0.16f, 3.42f), Quaternion.Euler(0, 0, -slope));

            foreach (int side in new[] {-1, 1})
                Part(entrance, "Door jamb", box, timber, new Vector3(side * 0.73f, 1.22f, -1.60f), new Vector3(0.12f, 2.0f, 0.14f));
            Part(entrance, "Door frame header", box, timber, new Vector3(0, 2.24f, -1.60f), new Vector3(1.58f, 0.12f, 0.14f));
            Part(entrance, "Upper step", box, concrete, new Vector3(0, 0.125f, -1.75f), new Vector3(1.60f, 0.15f, 0.50f));
            Part(entrance, "Lower step", box, concrete, new Vector3(0, 0.055f, -2.05f), new Vector3(1.90f, 0.11f, 0.40f));
            Part(floor, "Foundation", box, concrete, new Vector3(0, 0.06f, 0), new Vector3(3.82f, 0.12f, 3.46f));
            Part(floor, "Floor slab", box, concrete, new Vector3(0, 0.17f, 0), new Vector3(3.52f, 0.10f, 3.12f));

            MergeParts(root.transform, folder, "01 Wall-cavity supports", supports.GetComponentsInChildren<MeshRenderer>());
            MergeParts(root.transform, folder, "02 Exterior walls", walls.GetComponentsInChildren<MeshRenderer>());
            MergeParts(root.transform, folder, "03 Interior crates", crates.GetComponentsInChildren<MeshRenderer>());
            MergeParts(root.transform, folder, "04 Roof and trim", roofParts.GetComponentsInChildren<MeshRenderer>());
            MergeParts(root.transform, folder, "05 Door frame and steps", entrance.GetComponentsInChildren<MeshRenderer>());
            MergeParts(root.transform, folder, "06 Floor and foundation", floor.GetComponentsInChildren<MeshRenderer>());
            foreach (Transform parts in new[] {supports, walls, crates, roofParts, entrance, floor})
                Undo.DestroyObjectImmediate(parts.gameObject);
            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            Debug.Log("Workshop visibility example created in a new scene. Assets: " + folder + ". Doorway: -Z; rear wall: +Z.", root);
            return root;
        }

        private static bool HasUntitledScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && string.IsNullOrEmpty(scene.path)) return true;
            }
            return false;
        }

        private static Transform Group(Transform parent, string name)
        {
            var obj = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(obj, "Create visibility example group");
            obj.transform.SetParent(parent, false);
            return obj.transform;
        }

        private static void Part(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 scale, Quaternion? rotation = null)
        {
            var obj = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(obj, "Create visibility example part");
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            obj.transform.localRotation = rotation ?? Quaternion.identity;
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Material MaterialAsset(string folder, string name, Color color, Shader shader)
        {
            var material = new Material(shader) {name = name, color = color};
            material.SetFloat("_Glossiness", 0.12f);
            AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".mat"));
            return material;
        }

        private static void SaveMesh(string folder, Mesh mesh, string name)
        {
            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".asset"));
        }

        private static void MergeParts(Transform root, string folder, string name, IEnumerable<MeshRenderer> sources)
        {
            var byMaterial = new Dictionary<Material, List<CombineInstance>>();
            foreach (MeshRenderer source in sources)
            {
                Material material = source.sharedMaterial;
                if (!byMaterial.TryGetValue(material, out var instances))
                    byMaterial.Add(material, instances = new List<CombineInstance>());
                instances.Add(new CombineInstance {mesh = source.GetComponent<MeshFilter>().sharedMesh,
                    transform = root.worldToLocalMatrix * source.localToWorldMatrix});
            }
            var intermediate = new List<Mesh>();
            var materials = new List<Material>();
            try
            {
                foreach (var entry in byMaterial)
                {
                    var part = new Mesh(); intermediate.Add(part);
                    part.CombineMeshes(entry.Value.ToArray(), true, true);
                    materials.Add(entry.Key);
                }
                var merged = new Mesh();
                merged.CombineMeshes(intermediate.Select(m => new CombineInstance {mesh = m, transform = Matrix4x4.identity}).ToArray(), false, false);
                SaveMesh(folder, merged, name);
                var obj = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(obj, "Create visibility example category");
                obj.transform.SetParent(root, false);
                obj.AddComponent<MeshFilter>().sharedMesh = merged;
                obj.AddComponent<MeshRenderer>().sharedMaterials = materials.ToArray();
            }
            finally
            {
                foreach (Mesh part in intermediate) UnityEngine.Object.DestroyImmediate(part);
            }
        }

        private static Mesh FlatMesh(Vector3[] corners, int[] indices)
        {
            // Separate vertices keep the wall, crate and roof faces flat shaded.
            var vertices = new Vector3[indices.Length];
            var triangles = new int[indices.Length];
            for (int i = 0; i < indices.Length; i++) {vertices[i] = corners[indices[i]]; triangles[i] = i;}
            var mesh = new Mesh {vertices = vertices, triangles = triangles};
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BoxMesh()
        {
            return FlatMesh(new[]
            {
                new Vector3(-0.5f,-0.5f,-0.5f), new Vector3(0.5f,-0.5f,-0.5f),
                new Vector3(0.5f,0.5f,-0.5f), new Vector3(-0.5f,0.5f,-0.5f),
                new Vector3(-0.5f,-0.5f,0.5f), new Vector3(0.5f,-0.5f,0.5f),
                new Vector3(0.5f,0.5f,0.5f), new Vector3(-0.5f,0.5f,0.5f)
            }, new[] {0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5});
        }

        private static Mesh GableMesh()
        {
            return FlatMesh(new[]
            {
                new Vector3(-0.5f,0,-0.5f), new Vector3(0.5f,0,-0.5f), new Vector3(0,1,-0.5f),
                new Vector3(-0.5f,0,0.5f), new Vector3(0.5f,0,0.5f), new Vector3(0,1,0.5f)
            }, new[] {0,2,1,3,4,5,0,1,4,0,4,3,0,3,5,0,5,2,1,2,5,1,5,4});
        }
    }
}
#endif
