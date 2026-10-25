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
    /// A small, self-contained clothing example for visibility inspection.
    /// The mannequin faces -Z; its backpack sits on +Z. No external models are used.
    /// </summary>
    public static class MeshVisibilityDemo
    {
        public const string RootName = "Clothed avatar visibility demo";
        private const string AssetRoot = "Assets/TextureCocktailVisibilityDemo";
        private const int Sides = 12;
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
        /// Preserves open scenes and existing assets. Returns the demo root in a new
        /// additive scene; save that scene normally if you want to keep the example.
        /// </summary>
        public static GameObject Create()
        {
            // Unity refuses additive scene creation while an untitled scene is open.
            // Validate before creating any folders/assets; never save or close user scenes.
            if (HasUntitledScenes())
                throw new InvalidOperationException(SaveScenesMessage);

            Shader shader = Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException("This example requires the Built-in Render Pipeline Standard shader.");

            if (!AssetDatabase.IsValidFolder(AssetRoot))
                AssetDatabase.CreateFolder("Assets", "TextureCocktailVisibilityDemo");
            string folder = AssetDatabase.GenerateUniqueAssetPath(AssetRoot + "/Example");
            string leaf = folder.Substring(folder.LastIndexOf('/') + 1);
            AssetDatabase.CreateFolder(AssetRoot, leaf);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create visibility example");
            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create visibility example");
            Transform body = Group(root.transform, "Body");
            Transform clothing = Group(root.transform, "Clothing");
            Transform backpack = Group(root.transform, "Backpack");

            Material skin = MaterialAsset(folder, "Mannequin", new Color(0.84f, 0.68f, 0.49f), shader);
            Material shirt = MaterialAsset(folder, "Teal shirt", new Color(0.08f, 0.52f, 0.58f), shader);
            Material pants = MaterialAsset(folder, "Navy trousers", new Color(0.15f, 0.23f, 0.34f), shader);
            Material shoes = MaterialAsset(folder, "Dark shoes", new Color(0.075f, 0.10f, 0.14f), shader);
            Material pack = MaterialAsset(folder, "Orange backpack", new Color(0.92f, 0.44f, 0.13f), shader);
            Material straps = MaterialAsset(folder, "Backpack straps", new Color(0.26f, 0.16f, 0.09f), shader);

            Mesh rounded = Ellipsoid();
            SaveMesh(folder, rounded, "Rounded low-poly shape");
            Mesh torso = Loft(new[] {1.18f, 1.40f, 1.62f}, new[] {0.245f, 0.27f, 0.285f}, new[] {0.145f, 0.155f, 0.16f});
            SaveMesh(folder, torso, "Body torso");
            Mesh top = Loft(new[] {1.15f, 1.42f, 1.62f, 1.70f}, new[] {0.34f, 0.33f, 0.335f, 0.13f}, new[] {0.21f, 0.205f, 0.20f, 0.12f});
            SaveMesh(folder, top, "Shirt torso");
            Mesh sleeve = Loft(new[] {-0.15f, 0.15f}, new[] {0.14f, 0.15f}, new[] {0.14f, 0.15f});
            SaveMesh(folder, sleeve, "Shirt sleeve");
            Mesh box = BoxMesh();
            SaveMesh(folder, box, "Box detail");

            Part(body, "Torso under shirt", torso, skin, Vector3.zero, Vector3.one);
            Part(body, "Exposed neck", rounded, skin, new Vector3(0, 1.735f, 0), new Vector3(0.095f, 0.13f, 0.095f));
            Part(body, "Exposed head", rounded, skin, new Vector3(0, 1.98f, 0), new Vector3(0.19f, 0.235f, 0.175f));
            Part(body, "Face marker - front", box, shoes, new Vector3(0, 2.025f, -0.172f), new Vector3(0.20f, 0.055f, 0.032f));
            Part(clothing, "Shirt covering torso", top, shirt, Vector3.zero, Vector3.one);

            for (int side = -1; side <= 1; side += 2)
            {
                string label = side < 0 ? "Left" : "Right";
                Quaternion armRotation = Quaternion.Euler(0, 0, side * 18f);
                Part(body, label + " upper arm under sleeve", rounded, skin, new Vector3(side * 0.395f, 1.535f, 0), new Vector3(0.095f, 0.125f, 0.105f), armRotation);
                Part(body, label + " exposed forearm", rounded, skin, new Vector3(side * 0.49f, 1.24f, 0), new Vector3(0.09f, 0.205f, 0.10f), armRotation);
                Part(body, label + " exposed hand", rounded, skin, new Vector3(side * 0.565f, 1.00f, 0), new Vector3(0.085f, 0.115f, 0.075f));
                Part(body, label + " trouser leg", rounded, pants, new Vector3(side * 0.155f, 0.665f, 0), new Vector3(0.135f, 0.51f, 0.15f));
                Part(body, label + " shoe", rounded, shoes, new Vector3(side * 0.155f, 0.14f, -0.065f), new Vector3(0.145f, 0.125f, 0.245f));
                Part(clothing, label + " shirt sleeve", sleeve, shirt, new Vector3(side * 0.395f, 1.535f, 0), Vector3.one, armRotation);
            }

            Part(backpack, "Pack - visible from behind", rounded, pack, new Vector3(0, 1.40f, 0.305f), new Vector3(0.25f, 0.27f, 0.13f));
            // The two straps share a single renderer, keeping the result list short.
            Mesh strapMesh = CombineBoxes(box, new[]
            {
                Matrix4x4.TRS(new Vector3(-0.16f, 1.445f, -0.208f), Quaternion.Euler(0, 0, -5), new Vector3(0.045f, 0.39f, 0.025f)),
                Matrix4x4.TRS(new Vector3(0.16f, 1.445f, -0.208f), Quaternion.Euler(0, 0, 5), new Vector3(0.045f, 0.39f, 0.025f))
            });
            SaveMesh(folder, strapMesh, "Front straps");
            Part(clothing, "Front shoulder straps", strapMesh, straps, Vector3.zero, Vector3.one);

            var bodyParts = body.GetComponentsInChildren<MeshRenderer>();
            MergeParts(root.transform, folder, "01 Body under shirt", bodyParts.Where(p => p.name == "Torso under shirt" || p.name.EndsWith("upper arm under sleeve")));
            MergeParts(root.transform, folder, "02 Shirt", clothing.GetComponentsInChildren<MeshRenderer>());
            MergeParts(root.transform, folder, "03 Backpack", backpack.GetComponentsInChildren<MeshRenderer>());
            MergeParts(root.transform, folder, "04 Exposed skin", bodyParts.Where(p => p.name.StartsWith("Exposed") || p.name.StartsWith("Face") || p.name.Contains("exposed")));
            MergeParts(root.transform, folder, "05 Trousers", bodyParts.Where(p => p.name.EndsWith("trouser leg")));
            MergeParts(root.transform, folder, "06 Shoes", bodyParts.Where(p => p.name.EndsWith("shoe")));
            Undo.DestroyObjectImmediate(body.gameObject);
            Undo.DestroyObjectImmediate(clothing.gameObject);
            Undo.DestroyObjectImmediate(backpack.gameObject);
            Undo.CollapseUndoOperations(undoGroup);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            Debug.Log("Visibility example created in a new scene. Assets: " + folder + ". Front: -Z; back: +Z.", root);
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
            var material = new Material(shader) {name = name};
            material.color = color;
            material.SetFloat("_Glossiness", 0.15f);
            AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".mat"));
            return material;
        }

        private static void SaveMesh(string folder, Mesh mesh, string name)
        {
            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".asset"));
        }

        // One renderer per inspection category; distinct materials remain separate submeshes.
        private static void MergeParts(Transform root, string folder, string name, IEnumerable<MeshRenderer> sources)
        {
            var byMaterial = new Dictionary<Material, List<CombineInstance>>();
            foreach (MeshRenderer source in sources)
            {
                Material material = source.sharedMaterial;
                if (!byMaterial.TryGetValue(material, out var instances))
                    byMaterial.Add(material, instances = new List<CombineInstance>());
                instances.Add(new CombineInstance
                {
                    mesh = source.GetComponent<MeshFilter>().sharedMesh,
                    transform = root.worldToLocalMatrix * source.localToWorldMatrix
                });
            }
            var intermediate = new List<Mesh>();
            var materials = new List<Material>();
            try
            {
                foreach (var entry in byMaterial)
                {
                    var part = new Mesh();
                    intermediate.Add(part);
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

        private static Mesh Loft(float[] y, float[] radiusX, float[] radiusZ, float? bottomPole = null, float? topPole = null)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int row = 0; row < y.Length; row++)
                for (int column = 0; column < Sides; column++)
                {
                    float angle = column * Mathf.PI * 2 / Sides;
                    vertices.Add(new Vector3(Mathf.Sin(angle) * radiusX[row], y[row], Mathf.Cos(angle) * radiusZ[row]));
                }
            for (int row = 0; row < y.Length - 1; row++)
                for (int column = 0; column < Sides; column++)
                {
                    int next = (column + 1) % Sides;
                    int a = row * Sides + column, b = row * Sides + next;
                    triangles.AddRange(new[] {a, b, b + Sides, a, b + Sides, a + Sides});
                }
            int lower = vertices.Count;
            vertices.Add(new Vector3(0, bottomPole ?? y[0], 0));
            int upper = vertices.Count;
            vertices.Add(new Vector3(0, topPole ?? y[y.Length - 1], 0));
            int last = (y.Length - 1) * Sides;
            for (int column = 0; column < Sides; column++)
            {
                int next = (column + 1) % Sides;
                triangles.AddRange(new[] {lower, next, column, upper, last + column, last + next});
            }
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh Ellipsoid()
        {
            const int rings = 7;
            var y = new float[rings];
            var radius = new float[rings];
            for (int i = 0; i < rings; i++)
            {
                float angle = (i + 1) * Mathf.PI / (rings + 1);
                y[i] = -Mathf.Cos(angle);
                radius[i] = Mathf.Sin(angle);
            }
            return Loft(y, radius, radius, -1, 1);
        }

        private static Mesh BoxMesh()
        {
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-0.5f,-0.5f,-0.5f), new Vector3(0.5f,-0.5f,-0.5f),
                new Vector3(0.5f,0.5f,-0.5f), new Vector3(-0.5f,0.5f,-0.5f),
                new Vector3(-0.5f,-0.5f,0.5f), new Vector3(0.5f,-0.5f,0.5f),
                new Vector3(0.5f,0.5f,0.5f), new Vector3(-0.5f,0.5f,0.5f)
            };
            mesh.triangles = new[] {0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CombineBoxes(Mesh box, Matrix4x4[] transforms)
        {
            var parts = new CombineInstance[transforms.Length];
            for (int i = 0; i < parts.Length; i++)
                parts[i] = new CombineInstance {mesh = box, transform = transforms[i]};
            var mesh = new Mesh();
            mesh.CombineMeshes(parts, true, true);
            return mesh;
        }
    }
}
#endif
