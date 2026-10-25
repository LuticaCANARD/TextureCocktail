# TextureCocktail

**English** · [한국어](README.ko.md) · [日本語](README.ja.md) · [简体中文](README.zh-CN.md)

English is the default documentation language. Use the links above to switch languages. All versions share the same screenshots, GIFs, and tool comparisons with English labels.

Adjust texture colors and effects in the Unity Editor, compare the result with the original, and save it as a PNG. You can also inspect avatars and objects from multiple directions to identify candidate polygons that remain hidden.

[Installation](#installation) · [Edit your first texture](#edit-your-first-texture) · [Find hidden polygons](#find-hidden-polygons) · [Development and releases (Korean)](docs/development.md)

![Editing a texture with TextureCocktail in Unity](docs/media/texture-editor.png)

The screenshots and GIFs show the tool running in the Unity Editor. The tool comparison images below use actual shader outputs rendered by TextureCocktail. See the [capture environment and examples (Korean)](docs/media/capture-notes.md) for details.

## Installation

The packages require **Unity 2022.3** or later. This repository's project uses **2022.3.22f1**. TextureCocktail also requires the `luticalab.core` package.

### Install through VCC

1. In VRChat Creator Companion, open **Settings → Packages → Add Repository**.
2. Enter the repository URL below and click **Add**. Review the package information shown, then add the repository.
3. In your project's package management screen, add **TextureCocktail** together with its required Core dependency.
4. Open the project in Unity and check for the **LuticaLab → TextureCocktail** menu.

```text
https://skid-cocktail.luticalab.dev/index.json
```

[VCC community repository instructions](https://vcc.docs.vrchat.com/guides/community-repositories/) · [Latest release](https://github.com/LuticaCANARD/TextureCocktail/releases/latest)

<details>
<summary>Install manually from ZIP files</summary>

Download the TextureCocktail ZIP and the Core ZIP from the same release. Extract them into your project so that each `package.json` is at the location shown below, then open Unity.

```text
YourProject/
  Packages/
    luticalab.core/package.json
    luticalab.texturecocktail/package.json
```

Do not install duplicate copies of a package through both VCC and manual copying. For projects installed through VCC, use VCC to update the packages.

</details>

## Edit your first texture

1. Select a texture in the Project window, right-click it, and choose **TextureCocktail → Open with TextureCocktail**. If the editor window is already open, drag the texture into **Target Texture**.
2. Under **Quick Shader Selection**, choose **Filters/Fast Image Converter**.
3. Click a preset such as **Brighten**, **High Contrast**, or **Vibrant**, or adjust brightness, contrast, saturation, and gamma. Check the result in the preview below.
4. Click **Save Texture** and use a new name, such as `example_edited.png`. The save dialog suggests `Assets` and the original filename by default, so choose a different filename if you want to keep the original.
5. Assign the saved PNG to the texture slot of the material that should use it.

![TextureCocktail's actual preview changing with color settings](docs/media/texture-workflow.gif)

The editor UI supports **English, Korean, Japanese, and Simplified Chinese**. On first launch, the UI uses **English**, regardless of your operating system language. Open **LuticaLab → Setting → Language** to choose another language, such as **简体中文**. Your explicit choice is retained when Unity restarts. Menu paths, shader identifiers, and external plugin names may appear in English.

### Compare the original and the result

Click **A/B Test** below the preview to view the original and edited result together. Choose **Side by Side**, **Split Slider**, or **Toggle** as the comparison mode.

![Comparing Before and After with the A/B split slider](docs/media/ab-comparison.gif)

The After image in the A/B window is a snapshot taken when the window opens. If you change the editing values, click **A/B Test** in the main window again to refresh it. You can also create comparison materials and Quads; the default output folder is `Assets/TextureCocktailABTest`.

### Edit only part of an image

Enable **Enable Polygon Mask** in the preview, place at least three points, then click the first point or the button that finishes the polygon. The effect applies only inside the closed region. Drag a vertex or the polygon's interior to adjust it, and use **Reset Polygons** to clear it.

Use **Edit Fullscreen** to zoom in when working on a small area. In **LuticaLab → TextureCocktail Mesh Preview**, specify the target Renderer, material slot, and texture property to inspect the edited region on the mesh.

## Choose a tool for the task

The following tools are available through the main window's quick shader selection.

| Task | Tool | Getting started |
| --- | --- | --- |
| Adjust brightness, contrast, and saturation; blur or sharpen | **Filters/Fast Image Converter** | Assign a texture → choose a preset or Filter Type → adjust values |
| Correct color temperature, exposure, and color balance | **Color/Color Correction** | Choose a preset → adjust the grading mode and color values |
| Change hue, saturation, and value | **Color/HSV Adjustment** | Adjust the HSV conversion and offsets |
| Blend two textures | **Effects/Texture Blender** | Assign base and blend textures → adjust blend mode and amount |
| Apply pixelation, halftone, or cartoon effects | **Effects/Artistic Effects** | Choose an effect → adjust its values |
| Extract edges and color features | **Analysis/Feature Extractor** | Choose an extraction mode → adjust channels and sensitivity |
| Generate a normal map from a height image | **Generators/Normal Map Generator** | Assign a texture → adjust strength and height scale |

### See what each tool produces

These comparisons use a procedural 512 × 512 metal control-panel texture, a grime texture, and a height map. Each result is rendered by the actual TextureCocktail shader; the sheets arrange the inputs and outputs with English labels. The values below reproduce the examples. See the [capture notes (Korean)](docs/media/capture-notes.md) for the generation steps.

#### Fast Image Converter — brightness and detail

Make a texture brighter, soften detail, or bring edges into focus. Shown: **Brighten** preset (Brightness **0.2**, Contrast **1.1**, Saturation **1.05**, Gamma **1**), **Blur 0.8**, and **Sharpen 1.5**.

![Original metal panel compared with Brighten, blur, and sharpen results](docs/media/tool-fast-image-converter.png)

#### Color Correction — warm and cool variants

Create warmer or cooler versions of the same prop. Both examples use **Basic** grading and Exposure **+0.5**; Temperature is **+0.9** for the warm version and **−0.9** for the cool version.

![Original metal panel compared with warm and cool color corrections](docs/media/tool-color-correction.png)

#### HSV Adjustment — recolor a surface

Change the painted surface color while retaining its shading. With HSV Convert **0**, the examples use HSV Offset **(0.25, 0.1, 0)** and **(0.5, 0.1, 0)**.

![Original metal panel compared with two hue and saturation offset variants](docs/media/tool-hsv-adjustment.png)

#### Texture Blender — add grime

Combine a base texture with a separate grime texture to weather a prop. The example uses **Multiply**, Blend Amount **0.75**, with the blend mask disabled.

![Base panel and grime texture alongside the multiplied blend result](docs/media/tool-texture-blender.png)

#### Artistic Effects — stylize a prop

This example first brightens the input with Color Correction (**Basic**, Exposure **+1.5**) to keep the stylized details readable. Compare **Pixelate 16** with center **(0.5, 0.5)**, **Halftone** with dot size **10** and angle **45°**, and **Cartoon** with **8** steps and edge threshold **0.15**.

![Brightened panel compared with pixelation, halftone, and cartoon effects](docs/media/tool-artistic-effects.png)

#### Feature Extractor — inspect edges

Extract the panel’s outlines for inspection or a graphic effect. Shown: **Sobel** with Sensitivity **0.5** and **Canny** with Sensitivity **0.8**; both use cyan edges at Intensity **1**.

![Original panel compared with cyan Sobel and Canny edge extraction](docs/media/tool-feature-extractor.png)

#### Normal Map Generator — add surface relief

Start with the grayscale height image and compare normal-map strengths **2** and **10**, both with Height Scale **1**. The stronger setting makes changes in height more pronounced.

![Grayscale height input and normal maps rendered at strengths two and ten](docs/media/tool-normal-map-generator.png)

### Apply the same adjustments to multiple textures

Open **LuticaLab → TextureCocktail Batch Processor** and select a shader. Select textures in the Project window and add them with **Add Selected Textures**. Review the shared settings and **Output Path**, then click **Process All Textures**.

The shared batch settings are brightness, contrast, saturation, and gamma. They apply when the selected shader has the corresponding properties. The default output is `Assets/ProcessedTextures/<original-name>_processed.png`. Results with the same filename may be overwritten, so choose an output folder for each job.

## Find hidden polygons

The tool **checks both signs of the X, Y, and Z axes plus diagonal directions, then highlights only triangles hidden in every checked direction**. A face seen in even one direction is excluded from the candidates. No actual Light object is needed. The tool identifies candidates for inspection without deleting the mesh.

![Unity highlighting building polygons hidden in all checked XYZ and diagonal directions](docs/media/mesh-visibility.png)

1. Select the root of an avatar or object in the **Hierarchy**. Use an object placed in the scene, rather than a prefab asset in the Project window.
2. Open **LuticaLab → TextureCocktail Mesh Visibility**. You can also use **Find Hidden Polygons…** in the Mesh Preview window.
3. Leave **Analysis Mode** at its default, **All directions (XYZ + diagonals)**. **Direction Coverage** offers Quick with 26 directions, Balanced with 98 directions by default, or Detailed with 386 directions. Higher coverage adds directions while retaining those from the lower settings.
4. Choose **Surface Sampling**. The default Balanced mode uses 4 sample points per triangle; Fast uses 1 and Detailed uses 7. Enable **Include other scene renderers as blockers** if needed.
5. Click **Analyze Current Pose**. Enabled child MeshRenderers and SkinnedMeshRenderers are analyzed together in their current pose.
6. Under **XYZ ray checks**, verify whether **+X, -X, +Y, -Y, +Z, and -Z** were checked, and see the number of visible triangles in each direction. Enable **Show inspected XYZ ray directions** to display ray direction arrows in the Scene view as well.
7. Only **Hidden in all checked directions** candidates are highlighted in **purple**. Use **X-ray** to see candidates through other geometry, and **Select** to locate the corresponding object.

XYZ inspection covers more than a horizontal orbit. It checks all six axis directions, including above and below, together with diagonals. Direction labels indicate **the direction a ray travels toward the model**. For example, a +Z ray enters from the model's -Z side.

### Example: inspect hidden polygons through a full 360-degree orbit

Compare the **posts and beams inside thick walls** of a small workshop with **crate faces visible through its doorway**. Because the tool combines results from multiple directions, any crate face seen through the doorway is excluded from the candidates. Faces that remain hidden inside the walls can be inspected with X-ray.

![Actual Unity demonstration orbiting a workshop through 360 degrees while retaining one 98-direction result and showing hidden candidates and XYZ rays](docs/media/mesh-visibility.gif)

The GIF shows the **Scene view rotating through 360 degrees while retaining a single analysis of 98 directions**. You do not need to rerun the analysis when only moving the inspection view. The candidate set remains the same even as different faces come into view.

To try it yourself, copy the [example generator](docs/examples/MeshVisibilityDemo.cs) into your project's `Assets/Editor` folder. Save any open, unnamed Untitled scenes first, then run **LuticaLab → Examples → Create Mesh Visibility Demo**. The example scene requires no separate model download. The [example environment and reproduction steps (Korean)](docs/media/capture-notes.md#메시-예제-재현) describe the required settings.

| All-directions result | Meaning |
| --- | --- |
| Seen in any direction | At least one sample point was visible in a checked direction, so the face is excluded from the candidates |
| Purple · Hidden in all checked directions | A candidate hidden in every checked direction. Only these faces are highlighted |
| Unresolved | The face could not be classified as hidden in all checked directions, so it is excluded from the candidates |
| Degenerate | A triangle unsuitable for analysis, such as one with no area. It is excluded from the candidates |

**“All directions” means the selected set of 26, 98, or 386 checked directions; it does not prove that a face is invisible from every possible viewpoint.** Small visible areas or narrow gaps may be missed. For buildings that users can enter, also inspect interior viewpoints. Rerun the analysis after changing objects, mesh geometry, poses, blend shapes, blockers, or analysis settings.

### Inspect a specific camera or one direction

The original analysis is available under **Analysis Mode: Single view**. Set **View Source** to the current Scene view, a scene Camera, or a specified Direction. For a specified direction, enter the direction a ray travels toward the model.

| Single-view color or classification | Meaning |
| --- | --- |
| Red · Occluded | The checked sample points are blocked by other faces |
| Orange · Back-facing | The face points away from the inspection view |
| Blue · Outside view | The face is outside the specified camera's view |
| Visible | A visible area was found at a checked sample point |
| Degenerate | A triangle unsuitable for analysis, such as one with no area |

Single-view results apply only to the view and pose captured during analysis. Rerun after moving the camera or Scene view. An interior crate hidden when viewed from behind the building may still be visible through the front doorway or from inside.

Both modes inspect opaque geometry. They do not reproduce transparency, alpha cutouts, material culling, shader deformation, camera layers, LOD selection, or render-pipeline effects. Analysis and highlighting leave the source mesh unchanged.

## Other utilities and troubleshooting

- **LuticaLab → Quick Access Toolbar**: Assign a texture, click a preset button, and save the processed result.
- **LuticaLab → Texture Optimizer**: Scan a folder with **Scan Textures**, then review the recommended settings. The Fix button changes the actual Texture Importer settings.
- **LuticaLab → AI Connector**: Connect to a separately running Ollama or OpenAI-compatible server, select a model, and send a prompt. The texture editing features themselves do not require an AI server.

| Symptom | What to check |
| --- | --- |
| The LuticaLab menu is missing | Check that both Core and TextureCocktail are installed, and look for compilation errors in the Console. |
| No preview appears | Check the target texture and quick shader selection, then expand the preview area. |
| The saved texture does not appear on the object | Assign the saved PNG to the material's texture slot. |
| A/B comparison shows an older result | Reopen A/B Test from the main window to refresh the snapshot. |
| The mesh analysis button is disabled | Check that the target is a scene object. In Single view mode, also check that a Scene view or Camera is available, or that the specified direction is non-zero. |

Windows shortcuts are **Ctrl+Alt+T** for the main window and **Ctrl+Alt+B** for batch processing. On macOS, use **Cmd+Option+T/B**.

## Learn more

- [Development, extensions, tests, and release automation (Korean)](docs/development.md)
- [Plugin development guide](Packages/luticalab.texturecocktail/PLUGIN_GUIDE.md)
- [Fast image conversion guide (Korean)](Packages/luticalab.texturecocktail/FAST_IMAGE_CONVERSION_KR.md) · [English guide](Packages/luticalab.texturecocktail/FAST_IMAGE_CONVERSION.md)
- [Color correction, blending, and effects in detail (English and Korean)](Packages/luticalab.texturecocktail/NEW_FEATURES.md)
- [Report an issue](https://github.com/LuticaCANARD/TextureCocktail/issues)

TextureCocktail is part of LuticaLab's SKID-Project.
