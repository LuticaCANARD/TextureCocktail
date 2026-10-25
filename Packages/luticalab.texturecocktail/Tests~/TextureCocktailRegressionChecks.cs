using System;
using System.IO;
using System.Reflection;
using LuticaLab.TextureCocktail;
using TextureCocktailRegressionFixtures;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Native Unity regression checks without a test-framework dependency. This folder is ignored
/// by Unity; Run-RegressionChecks.ps1 copies these fixtures to a temporary Assets/Editor folder.
/// </summary>
public static class TextureCocktailRegressionChecks
{
    private const string ExplicitShaderName = "Hidden/TextureCocktailTests/ExplicitBinding";
    private const string LegacyShaderName = "Hidden/TextureCocktailTests/TextureCocktailLegacyFixture";
    private const string PluginShaderName = "Hidden/TextureCocktailTests/texturecocktailpluginfixture";
    private const string ReorderedShaderName = "Hidden/TextureCocktailTests/ReorderedPasses";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int _passed;

    public static void Run()
    {
        _passed = 0;
        Require(SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null,
            "These image checks require a graphics device; run Unity without -nographics.");
        Shader fixture = RequiredShader(ExplicitShaderName);
        string fixtureRoot = Path.GetDirectoryName(AssetDatabase.GetAssetPath(fixture)).Replace('\\', '/');
        Require(fixtureRoot.StartsWith("Assets/", StringComparison.Ordinal),
            "Copy the fixtures to a temporary folder inside Assets before running.");
        string outputDirectory = Path.Combine(Path.GetFullPath(fixtureRoot), "Generated-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);
        try
        {
            Check("attribute binding uses the declared Content type", TestExplicitBinding);
            Check("asset metadata override retains attributed Content", () => TestAssetOverride(fixtureRoot));
            Check("legacy same-name Content fallback", TestLegacyBinding);
            Check("plugin fallback preserves namespace discovery and explicit binding priority", TestPluginFallback);
            Check("shader range metadata and missing properties", TestRanges);
            Check("feature modes follow named passes after reordering", TestNamedPasses);
            Check("preview, PNG pixels and active-target restoration", () => TestRenderAndExport(outputDirectory));
            Check("export failure restores active target and caller ownership", () => TestExportFailure(outputDirectory));
            Check("new preview requests coalesce during callbacks", TestCoalescedCallback);
            Check("legacy CompileShader remains synchronous in callbacks", TestLegacySynchronousCallback);
            Check("null textures preserve default and custom callback contracts", TestNullTextureCallback);
            Check("keyword selection clears the previous variant", TestKeywordTransition);
            Check("FeatureExtractor masks and preview notifications", TestFeatureMask);
            _passed += LocalizationRegressionChecks.Run();
            _passed += MeshVisibilityRegressionChecks.Run();
            Debug.Log("TEXTURE_COCKTAIL_CHECKS_PASSED " + _passed + " groups");
        }
        finally
        {
            UnrelatedControls.DuringValueChange = null;
            UnrelatedControls.UseDefaultRender = true;
            if (Directory.Exists(outputDirectory)) Directory.Delete(outputDirectory, true);
            string outputMeta = outputDirectory + ".meta";
            if (File.Exists(outputMeta)) File.Delete(outputMeta);
            TextureCocktailShaderRegistry.Refresh();
        }
    }

    private static void Check(string name, Action check)
    {
        check();
        _passed++;
        Debug.Log("TextureCocktail regression PASS: " + name);
    }

    private static void TestExplicitBinding()
    {
        TextureCocktailShaderRegistry.Refresh();
        var content = TextureCocktailShaderRegistry.CreateContent(RequiredShader(ExplicitShaderName));
        try
        {
            Require(content is UnrelatedControls, "Content must use its attributed type despite the different namespace and name.");
            Require(TextureCocktailShaderRegistry.CreateContent(null) == null, "Null shaders must not create Content.");
        }
        finally { if (content != null) Object.DestroyImmediate(content); }
    }

    private static void TestAssetOverride(string fixtureRoot)
    {
        var shader = RequiredShader(ExplicitShaderName);
        var package = ScriptableObject.CreateInstance<TextureCocktailShaderPackage>();
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(fixtureRoot + "/RegistryOverride.asset");
        TextureCocktailContent content = null;
        try
        {
            var serialized = new SerializedObject(package);
            serialized.FindProperty("_shader").objectReferenceValue = shader;
            serialized.FindProperty("_displayName").stringValue = "Regression asset override";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(package, assetPath);
            TextureCocktailShaderRegistry.Refresh();
            Require(ReferenceEquals(TextureCocktailShaderRegistry.FindByShader(shader), package),
                "Asset metadata must take precedence over attribute metadata.");
            Require(TextureCocktailShaderRegistry.FindByShader(shader).DisplayName == "Regression asset override",
                "Picker must expose the asset's display metadata.");
            content = TextureCocktailShaderRegistry.CreateContent(shader);
            Require(content is UnrelatedControls, "Asset metadata must not erase the explicit Content binding.");
        }
        finally
        {
            if (content != null) Object.DestroyImmediate(content);
            AssetDatabase.DeleteAsset(assetPath);
            if (package != null) Object.DestroyImmediate(package);
            TextureCocktailShaderRegistry.Refresh();
        }
    }

    private static void TestLegacyBinding()
    {
        var content = TextureCocktailShaderRegistry.CreateContent(RequiredShader(LegacyShaderName));
        try { Require(content is TextureCocktailLegacyFixture, "Unattributed legacy Content must still resolve by shader basename."); }
        finally { if (content != null) Object.DestroyImmediate(content); }
    }

    private static void TestPluginFallback()
    {
        TextureCocktailPluginRegistry.Refresh();
        TextureCocktailShaderRegistry.Refresh();
        TextureCocktailContent fallback = null;
        TextureCocktailContent explicitContent = null;
        try
        {
            Type expected = typeof(TextureCocktailRegressionPluginFixtures.TextureCocktailPluginFixture);
            Require(TextureCocktailPluginRegistry.TypesByName.TryGetValue("texturecocktailpluginfixture", out Type discovered)
                && discovered == expected,
                "Plugin discovery must keep case-insensitive class-name matching outside the built-in namespace.");
            fallback = TextureCocktailShaderRegistry.CreateContent(RequiredShader(PluginShaderName));
            Require(fallback != null && fallback.GetType() == expected,
                "A plugin with no ShaderAttribute must resolve through the shared Content factory.");
            bool metadataPreserved = false;
            foreach (var info in TextureCocktailPluginRegistry.AllPlugins)
                if (info.PluginType == expected)
                    metadataPreserved = info.DisplayName == "Independent plugin display label"
                        && info.AssemblyName == expected.Assembly.GetName().Name;
            Require(metadataPreserved,
                "Plugin display metadata must remain available without replacing the class-name binding key.");

            Require(TextureCocktailPluginRegistry.TypesByName.TryGetValue("ExplicitBinding", out Type nameMatch)
                && nameMatch == typeof(TextureCocktailRegressionPluginFixtures.ExplicitBinding),
                "The fixture must supply a competing name-based plugin for the explicitly bound shader.");
            explicitContent = TextureCocktailShaderRegistry.CreateContent(RequiredShader(ExplicitShaderName));
            Require(explicitContent is UnrelatedControls,
                "An explicit ShaderAttribute binding must win over a same-basename plugin.");
        }
        finally
        {
            if (fallback != null) Object.DestroyImmediate(fallback);
            if (explicitContent != null) Object.DestroyImmediate(explicitContent);
        }
    }

    private static void TestRanges()
    {
        Vector2 limits;
        Require(ShaderParameterGUI.TryGetRange(RequiredShader("Hidden/FastImageConverter"), "_Contrast", out limits),
            "Contrast must expose shader range metadata.");
        Require(limits == new Vector2(0, 3), "Contrast controls must use the shader's 0..3 range.");
        Require(!ShaderParameterGUI.TryGetRange(RequiredShader(ExplicitShaderName), "_MainTex", out limits),
            "Texture properties must not be treated as numeric ranges.");
        Require(!ShaderParameterGUI.TryGetRange(RequiredShader(ExplicitShaderName), "_Missing", out limits),
            "A missing property must not return a fabricated range.");
    }

    private static void TestNamedPasses()
    {
        var material = new Material(RequiredShader(ReorderedShaderName));
        try
        {
            Require(material.FindPass("EdgeDetection") == 3, "The fixture must deliberately reorder EdgeDetection.");
            Require(FeatureExtractor.ResolvePassIndex(material, FeatureExtractionMode.EdgeDetection) == 3,
                "EdgeDetection must follow its name rather than enum ordinal 0.");
            Require(FeatureExtractor.ResolvePassIndex(material, FeatureExtractionMode.CannyEdge) == 2, "CannyEdge pass mismatch.");
            Require(FeatureExtractor.ResolvePassIndex(material, FeatureExtractionMode.ColorSegmentation) == 1, "ColorSegmentation pass mismatch.");
            Require(FeatureExtractor.ResolvePassIndex(material, FeatureExtractionMode.HistogramEnhance) == 0, "HistogramEnhance pass mismatch.");
        }
        finally { Object.DestroyImmediate(material); }
        material = new Material(RequiredShader(ExplicitShaderName));
        try
        {
            Require(FeatureExtractor.ResolvePassIndex(material, FeatureExtractionMode.HistogramEnhance) == 0,
                "An unavailable named/indexed pass must fall back inside the material's pass range.");
        }
        finally { Object.DestroyImmediate(material); }
    }

    private static void TestRenderAndExport(string outputDirectory)
    {
        var source = SolidTexture(4, Color.red);
        var material = new Material(RequiredShader(ExplicitShaderName));
        var sentinel = new RenderTexture(2, 2, 0);
        var renderer = new TexturePreviewRenderer();
        RenderTexture previous = RenderTexture.active;
        Texture2D png = null;
        try
        {
            sentinel.Create();
            RenderTexture.active = sentinel;
            renderer.Render(source, material, 0, null);
            Require(RenderTexture.active == sentinel, "Preview rendering must restore the previous render target.");
            RequireColor(ReadPixel(renderer.Preview, 1, 1), Color.red, "Preview pixel");
            string previewPath = Path.Combine(outputDirectory, "preview.png");
            TextureExportUtility.SaveRenderTexturePng(renderer.Preview, previewPath);
            Require(RenderTexture.active == sentinel, "Preview PNG readback must restore the active target.");
            string materialPath = Path.Combine(outputDirectory, "material.png");
            TextureExportUtility.SaveMaterialPng(source, material, materialPath, 0);
            Require(RenderTexture.active == sentinel, "Material PNG rendering must restore the active target.");
            png = new Texture2D(1, 1);
            Require(png.LoadImage(File.ReadAllBytes(materialPath)), "Exported bytes must decode as an image.");
            Require(png.width == 4 && png.height == 4, "PNG dimensions must preserve source resolution.");
            foreach (var pixel in png.GetPixels()) RequireColor(pixel, Color.red, "PNG pixel");
            Require(renderer.Preview != null && renderer.Preview.IsCreated(), "Export must not release the caller's preview.");
        }
        finally
        {
            RenderTexture.active = previous;
            if (png != null) Object.DestroyImmediate(png);
            renderer.Dispose();
            sentinel.Release();
            Object.DestroyImmediate(sentinel);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(source);
        }
    }

    private static void TestExportFailure(string outputDirectory)
    {
        var source = SolidTexture(4, Color.green);
        var material = new Material(RequiredShader(ExplicitShaderName));
        var sentinel = new RenderTexture(2, 2, 0);
        var renderer = new TexturePreviewRenderer();
        RenderTexture previous = RenderTexture.active;
        try
        {
            sentinel.Create();
            RenderTexture.active = sentinel;
            string invalidPath = Path.Combine(outputDirectory, "missing-directory", "cannot-save.png");
            RequireIOException(() => TextureExportUtility.SaveMaterialPng(source, material, invalidPath, 0));
            Require(RenderTexture.active == sentinel, "Failed material export must restore the previous render target.");
            Require(source != null && material != null, "Failed export must not destroy caller-owned inputs.");
            renderer.Render(source, material, 0, null);
            RequireIOException(() => TextureExportUtility.SaveRenderTexturePng(renderer.Preview, invalidPath));
            Require(RenderTexture.active == sentinel, "Failed preview export must restore the previous render target.");
            Require(renderer.Preview.IsCreated(), "Failed export must leave the caller's preview usable.");
            RequireColor(ReadPixel(renderer.Preview, 1, 1), Color.green, "Preview after failed export");
        }
        finally
        {
            RenderTexture.active = previous;
            renderer.Dispose();
            sentinel.Release();
            Object.DestroyImmediate(sentinel);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(source);
        }
    }

    private static void TestCoalescedCallback()
    {
        var source = SolidTexture(4, Color.white);
        TextureCocktail window = null;
        try
        {
            window = CreateWindow(RequiredShader(ExplicitShaderName), source);
            int updates = 0;
            window.OnPreviewUpdated += () => updates++;
            UnrelatedControls.UseDefaultRender = true;
            UnrelatedControls.DuringValueChange = host =>
            {
                host.RenderPreview();
                host.RenderPreview();
                Require(updates == 0, "New preview requests should wait until the settings callback completes.");
            };
            window.OnShaderValueChange();
            Require(updates == 1, "Repeated new preview requests must produce one completed preview update.");
        }
        finally
        {
            UnrelatedControls.DuringValueChange = null;
            if (window != null) Object.DestroyImmediate(window);
            Object.DestroyImmediate(source);
        }
    }

    private static void TestLegacySynchronousCallback()
    {
        var source = SolidTexture(4, Color.white);
        TextureCocktail window = null;
        try
        {
            window = CreateWindow(RequiredShader(ExplicitShaderName), source);
            int updates = 0;
            window.OnPreviewUpdated += () => updates++;
            UnrelatedControls.UseDefaultRender = false;
            UnrelatedControls.DuringValueChange = host =>
            {
                host.CalculationMaterial.SetFloat("_Gain", 0);
                host.CompileShader();
                Require(updates == 1, "Legacy CompileShader must publish its preview before returning to the callback.");
                RequireColor(ReadPixel(host.PreviewTexture, 1, 1), Color.black, "Synchronous legacy readback");
            };
            window.OnShaderValueChange();
            Require(updates == 1, "A custom-action legacy callback must not acquire an additional implicit render.");
        }
        finally
        {
            UnrelatedControls.DuringValueChange = null;
            UnrelatedControls.UseDefaultRender = true;
            if (window != null) Object.DestroyImmediate(window);
            Object.DestroyImmediate(source);
        }
    }

    private static void TestNullTextureCallback()
    {
        TextureCocktail window = null;
        try
        {
            window = CreateWindow(RequiredShader(ExplicitShaderName), null);
            int callbacks = 0;
            UnrelatedControls.DuringValueChange = host => callbacks++;
            UnrelatedControls.UseDefaultRender = true;
            window.OnShaderValueChange();
            Require(callbacks == 0, "Default-action Content must skip value-change callbacks without a target texture.");
            UnrelatedControls.UseDefaultRender = false;
            window.OnShaderValueChange();
            Require(callbacks == 1, "Custom-action Content must retain its callback-first contract without a texture.");
            Require(window.PreviewTexture == null, "A null target must not allocate a preview texture.");
        }
        finally
        {
            UnrelatedControls.DuringValueChange = null;
            UnrelatedControls.UseDefaultRender = true;
            if (window != null) Object.DestroyImmediate(window);
        }
    }

    private static void TestKeywordTransition()
    {
        var source = SolidTexture(4, Color.white);
        TextureCocktail window = null;
        try
        {
            window = CreateWindow(RequiredShader("Hidden/FastImageConverter"), source);
            var content = (FastImageConverter)GetField(window, "_shaderWindow");
            FieldInfo mode = typeof(FastImageConverter).GetField("filterMode", PrivateInstance);
            Require(mode != null, "The filter fixture field was renamed; update this test adapter.");
            mode.SetValue(content, FastImageFilterMode.Blur);
            window.OnShaderValueChange();
            Require(window.CalculationMaterial.IsKeywordEnabled("BLUR_FILTER"), "Blur must enable its shader keyword.");
            mode.SetValue(content, FastImageFilterMode.Sharpen);
            window.OnShaderValueChange();
            Require(window.CalculationMaterial.IsKeywordEnabled("SHARPEN_FILTER"), "Sharpen must enable its shader keyword.");
            Require(!window.CalculationMaterial.IsKeywordEnabled("BLUR_FILTER"), "Switching modes must clear the previous keyword.");
            mode.SetValue(content, FastImageFilterMode.None);
            window.OnShaderValueChange();
            Require(!window.CalculationMaterial.IsKeywordEnabled("SHARPEN_FILTER"), "None must clear the previous effect.");
        }
        finally
        {
            if (window != null) Object.DestroyImmediate(window);
            Object.DestroyImmediate(source);
        }
    }

    private static void TestFeatureMask()
    {
        var source = SolidTexture(64, new Color(0.25f, 0.25f, 0.25f, 1));
        var originalMaterial = new Material(RequiredShader(ExplicitShaderName));
        var originalRenderer = new TexturePreviewRenderer();
        TextureCocktail window = null;
        try
        {
            originalRenderer.Render(source, originalMaterial, 0, null);
            Color original = ReadPixel(originalRenderer.Preview, 48, 32);
            window = CreateWindow(RequiredShader("Hidden/FeatureExtractor"), source);
            var content = (FeatureExtractor)GetField(window, "_shaderWindow");
            typeof(FeatureExtractor).GetField("extractionMode", PrivateInstance)
                .SetValue(content, FeatureExtractionMode.HistogramEnhance);
            window.OnShaderValueChange();
            Color processed = ReadPixel(window.PreviewTexture, 16, 32);
            Require(Mathf.Abs(processed.r - original.r) > 0.02f, "The fixture must visibly change the input so mask checks are meaningful.");

            AddOpenPolygonFixture(window, new[]
            {
                new Vector2(0.1f, 0.1f), new Vector2(0.45f, 0.1f),
                new Vector2(0.45f, 0.9f), new Vector2(0.1f, 0.9f)
            });
            int updates = 0;
            window.OnPreviewUpdated += () => updates++;
            Require(window.FinalizeOpenPolygon(), "The test polygon must finalize.");
            Require(updates == 1, "Finalizing a polygon must publish exactly one preview update.");
            Require(window.HasActivePolygonMask, "A finalized polygon must activate the mask.");
            window.OnShaderValueChange();
            Require(updates == 2, "Feature settings must use the host preview notification path.");
            RequireColor(ReadPixel(window.PreviewTexture, 16, 32), processed, "Processed pixel inside mask");
            RequireColor(ReadPixel(window.PreviewTexture, 48, 32), original, "Original pixel outside mask");
            window.PolygonMaskEnabled = false;
            Require(updates == 3, "Disabling a mask must publish exactly one preview update.");
            RequireColor(ReadPixel(window.PreviewTexture, 48, 32), processed, "Processed pixel with mask disabled");
            window.PolygonMaskEnabled = true;
            Require(updates == 4, "Enabling a mask must publish exactly one preview update.");
            RequireColor(ReadPixel(window.PreviewTexture, 48, 32), original, "Original pixel with mask reenabled");
            window.ClearPolygonMask();
            Require(updates == 5, "Clearing a mask must publish exactly one preview update.");
            Require(!window.HasActivePolygonMask, "Clearing a mask must remove all active shapes.");
            RequireColor(ReadPixel(window.PreviewTexture, 48, 32), processed, "Processed pixel after clearing mask");
        }
        finally
        {
            if (window != null) Object.DestroyImmediate(window);
            originalRenderer.Dispose();
            Object.DestroyImmediate(originalMaterial);
            Object.DestroyImmediate(source);
        }
    }

    /// <summary>
    /// Batch executeMethod has no IMGUI event loop. Seed only the open-shape input state;
    /// the test still uses the real finalize, rasterize, composite, toggle and clear paths.
    /// Keep reflection confined to this adapter so a model rename fails with a useful message.
    /// </summary>
    private static void AddOpenPolygonFixture(TextureCocktail window, Vector2[] points)
    {
        PropertyInfo property = typeof(TextureCocktail).GetProperty("PolygonMask", PrivateInstance);
        Require(property != null, "The polygon model accessor was renamed; update the test adapter.");
        object model = property.GetValue(window);
        var shapes = (System.Collections.IList)GetField(model, "_polygonMaskShapes");
        Require(shapes.Count == 0, "The mask fixture expects a fresh editor without existing shapes.");
        Type shapeType = model.GetType().GetNestedType("PolygonMaskShape", BindingFlags.NonPublic);
        Require(shapeType != null, "The polygon shape type was renamed; update the test adapter.");
        object shape = Activator.CreateInstance(shapeType, true);
        FieldInfo pointField = shapeType.GetField("Points", BindingFlags.Public | BindingFlags.Instance);
        Require(pointField != null, "The polygon points field was renamed; update the test adapter.");
        ((System.Collections.Generic.List<Vector2>)pointField.GetValue(shape)).AddRange(points);
        shapes.Add(shape);
        window.GetPolygonShapeCounts(out int closed, out int open);
        Require(closed == 0 && open == 1 && !window.HasActivePolygonMask,
            "The test adapter must prepare exactly one open polygon without activating the mask.");
    }

    private static TextureCocktail CreateWindow(Shader shader, Texture2D source)
    {
        var window = ScriptableObject.CreateInstance<TextureCocktail>();
        try
        {
            typeof(TextureCocktail).GetMethod("OnShaderChange", PrivateInstance).Invoke(window, new object[] { shader });
            window.SetTargetTexture(source);
            return window;
        }
        catch { Object.DestroyImmediate(window); throw; }
    }

    private static object GetField(object target, string name)
    {
        var field = target.GetType().GetField(name, PrivateInstance);
        Require(field != null, "Missing test adapter field: " + name);
        return field.GetValue(target);
    }

    private static Shader RequiredShader(string name)
    {
        var shader = Shader.Find(name);
        Require(shader != null && shader.isSupported, "Required shader is missing or unsupported: " + name);
        return shader;
    }

    private static Texture2D SolidTexture(int size, Color color)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var colors = new Color[size * size];
        for (int i = 0; i < colors.Length; i++) colors[i] = color;
        texture.SetPixels(colors);
        texture.Apply();
        return texture;
    }

    private static Color ReadPixel(RenderTexture texture, int x, int y)
    {
        RenderTexture previous = RenderTexture.active;
        Texture2D readback = null;
        try
        {
            RenderTexture.active = texture;
            readback = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            readback.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            readback.Apply();
            return readback.GetPixel(x, y);
        }
        finally
        {
            RenderTexture.active = previous;
            if (readback != null) Object.DestroyImmediate(readback);
        }
    }

    private static void RequireIOException(Action action)
    {
        bool failed = false;
        try { action(); }
        catch (IOException) { failed = true; }
        Require(failed, "Writing into a nonexistent directory must report an IOException.");
    }

    private static void RequireColor(Color actual, Color expected, string context)
    {
        const float tolerance = 0.025f;
        Require(Mathf.Abs(actual.r - expected.r) <= tolerance && Mathf.Abs(actual.g - expected.g) <= tolerance
            && Mathf.Abs(actual.b - expected.b) <= tolerance && Mathf.Abs(actual.a - expected.a) <= tolerance,
            context + ": expected " + expected + ", received " + actual);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("TextureCocktail regression failed: " + message);
    }
}

namespace TextureCocktailRegressionFixtures
{
    // Deliberately differs from both the shader's basename and the production namespace.
    [TextureCocktailShader("Hidden/TextureCocktailTests/ExplicitBinding", DisplayName = "Regression fixture")]
    public sealed class UnrelatedControls : TextureCocktailContent
    {
        public static Action<TextureCocktail> DuringValueChange;
        public static bool UseDefaultRender = true;
        public override bool ShaderUpdateDefaultAction => UseDefaultRender;
        public override void OnGUI() { }
        public override void OnShaderValueChanged() => DuringValueChange?.Invoke(baseWindow);
    }
}

namespace LuticaLab.TextureCocktail
{
    // No attribute: this exercises the compatibility lookup for old extensions.
    public sealed class TextureCocktailLegacyFixture : TextureCocktailContent
    {
        public override void OnGUI() { }
        public override void OnShaderValueChanged() { }
    }
}

namespace TextureCocktailRegressionPluginFixtures
{
    // Plugin metadata is optional and does not replace class-name shader matching.
    [TextureCocktailPlugin("Independent plugin display label", "Factory fallback regression", "TextureCocktail tests")]
    public sealed class TextureCocktailPluginFixture : TextureCocktailContent
    {
        public override void OnGUI() { }
        public override void OnShaderValueChanged() { }
    }

    // Deliberately competes with the separately attributed UnrelatedControls class.
    public sealed class ExplicitBinding : TextureCocktailContent
    {
        public override void OnGUI() { }
        public override void OnShaderValueChanged() { }
    }
}
