# Texture Cocktail

> compact, flexable, fast Texture Editor for edit on non-stop      
> sub-project of SKID-Project

## How To use?

- select shader for modify Image,
- after then, select Image for apply shader.
- Edit and Save ! That's all !

## Find hidden polygons

1. Select an avatar or object root and open **LuticaLab > TextureCocktail Mesh Visibility**. The Mesh Preview window also has a **Find Hidden Polygons** button.
2. Choose the current Scene view, a scene Camera, or a direction of travel for the viewing rays. Direction mode frames the captured geometry automatically.
3. Choose surface sample quality and whether other scene meshes can occlude the selected object, then run the analysis. Active/enabled static and skinned meshes are captured at their current pose.
4. Review per-renderer counts for visible, occluded, back-facing, outside-view and degenerate triangles. Toggle reason colors and X-ray display in the Scene view; select a result to locate its Renderer.

Results are visibility **candidates for the captured view and pose**, not proof that geometry is unused. Small visible areas can be missed by sampling. The analysis treats surfaces as opaque geometry and does not reproduce material transparency/alpha clipping, shader deformation, camera layer filtering or LOD selection. Re-run after changing the pose, object transforms or view. Analysis and overlays do not modify or delete the original meshes.

## How to write Third-Party Image/Texture Editor on Unity via Texture Cocktail

- Provide a `Shader` and optionally a custom `TextureCocktailContent` editor.
- Register custom editors with `[TextureCocktailShader("Hidden/MyShader")]`. The registry connects the actual class type, so the class name, namespace and assembly may differ from the shader name.
- Editors without a shader attribute use the plugin registry: the shader basename matches the class name, including plugins in other namespaces or loaded assemblies. Explicit shader attributes take precedence over this fallback.
- `TextureCocktailShaderPackage` assets override picker metadata while an attributed editor remains connected.
- Override `OnGUI` and `OnShaderValueChanged`; the host calls `Initialize` and owns the editor instance. `UseDefaultLayout` and `OnValuepdate` remain compatibility members and are not invoked by the host.
- Use `baseWindow.RenderPreview()` for new render requests. Requests made inside the host's value-change callback are combined into one render afterward. Existing `CompileShader()` calls remain synchronous, including inside callbacks.
- Shader property ranges are defined by the shader. Use `ShaderParameterGUI` for built-in numeric controls; map modes explicitly to shader keywords or named passes.

Rendering and PNG export live in `TexturePreviewRenderer` and `TextureExportUtility`. `PolygonMaskEditor` owns polygon interaction and mask texture lifetime; mask commands notify the owning window automatically. `TextureCocktail` coordinates these components and the content UI.

Regression checks are kept in `Packages/luticalab.texturecocktail/Tests~` so fixtures never appear in the normal shader picker. Run `Run-RegressionChecks.ps1` with the Unity Editor executable matching the project version. The runner copies fixtures to a unique temporary Assets folder, runs Unity in batch mode, and removes only that folder afterward.

## Usable Editor

- Unity 2022.01
- Unity 6000.01

## How Can I contribute?

- Clone this repo,
- open on Your editor, and wite your contribute
- after your write code, open Pull Request, and wait for review from admin.

## After plan

- MCP/A2A Image Editor,
- functional recipe.
