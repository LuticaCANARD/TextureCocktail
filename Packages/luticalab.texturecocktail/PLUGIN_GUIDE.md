# TextureCocktail Plugin Development Guide

This guide explains how to create custom plugins (content editors) for TextureCocktail
so that **users, modders, and AI agents** can extend the tool with new texture effects.

---

## Quick Start

### Step 1 — Create a Shader

Create a Unity shader with any path you like. The **last segment** of the path becomes the
plugin identifier:

```hlsl
Shader "YourName/GrayscaleEffect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Strength ("Effect Strength", Range(0,1)) = 0.5
    }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Strength;

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                float gray = dot(col.rgb, fixed3(0.299, 0.587, 0.114));
                col.rgb = lerp(col.rgb, fixed3(gray, gray, gray), _Strength);
                return col;
            }
            ENDCG
        }
    }
}
```

---

### Step 2 — Create the Plugin Class

Create a C# class **whose name exactly matches the last path segment of the shader**
(e.g. `GrayscaleEffect`). It must:

- Be in **any assembly** (no specific namespace required)
- Inherit from `LuticaLab.TextureCocktail.TextureCocktailContent`
- Optionally carry `[TextureCocktailPlugin]` metadata

```csharp
using LuticaLab.TextureCocktail;
using UnityEditor;
using UnityEngine;

// Optional metadata for the Plugin Browser window
[TextureCocktailPlugin(
    displayName : "Grayscale Effect",
    description : "Converts the image to grayscale with adjustable strength",
    author      : "YourName",
    version     : "1.0.0")]
public class GrayscaleEffect : TextureCocktailContent
{
    public override bool UseDefaultLayout => false;

    public override void OnGUI()
    {
        GUILayout.Label("Grayscale Effect", EditorStyles.boldLabel);

        var mat = GetMaterial();
        if (mat == null) { baseWindow.ShowShaderInfo(); return; }

        EditorGUI.BeginChangeCheck();
        float strength = mat.GetFloat("_Strength");
        strength = EditorGUILayout.Slider("Effect Strength", strength, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            mat.SetFloat("_Strength", strength);
            baseWindow.OnShaderValueChange();
        }

        baseWindow.DisplayPassedIamge();

        if (GUILayout.Button("Save")) baseWindow.SaveTexture();
    }

    public override void OnShaderValueChanged() { }

    // Helper — access the material through the public API
    private Material GetMaterial()
    {
        var field = baseWindow.GetType().GetField("_calcMaterial",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field?.GetValue(baseWindow) as Material;
    }
}
```

---

## Plugin Discovery

TextureCocktail uses `TextureCocktailPluginRegistry` which scans **all assemblies** loaded
in the AppDomain on editor startup.  Your class does **not** need to be in the
`LuticaLab.TextureCocktail` namespace — any namespace works.

Open **LuticaLab → TextureCocktail Plugin Browser** to see all discovered plugins.

---

## TextureCocktailContent API Reference

| Member | Description |
|--------|-------------|
| `baseWindow` | Reference to the main `TextureCocktail` editor window |
| `scrollPosition` | Shared scroll position for the content area |
| `UseDefaultLayout` | Return `false` to take full control of the GUI |
| `DontWantDisplayPropertyName` | Property names to hide from the default shader inspector |
| `ShaderUpdateDefaultAction` | Return `false` to handle shader updates yourself |
| `PassOrder` | GPU pass index to compile (default 0) |
| `Initialize(window)` | Called once when the shader is selected |
| `OnGUI()` | Draw your custom Unity IMGUI here |
| `OnShaderValueChanged()` | Called when shader parameters change |
| `OnValuepdate()` | Called every editor update |

### TextureCocktail Window API

| Method | Description |
|--------|-------------|
| `DisplayPassedIamge()` | Renders the preview RenderTexture inline |
| `ShowShaderInfo()` | Renders the auto-generated shader property inspector |
| `DisplayShaderOptions()` | Renders keyword toggle UI |
| `CompileShader()` | Re-blits the texture through the material |
| `SaveTexture()` | Opens save dialog and writes the result PNG |
| `SetMaterialKeyword(name, on)` | Enable/disable a shader keyword |
| `OnShaderValueChange()` | Triggers a full shader re-compile |

---

## AI Agent Usage

AI agents can generate plugin code programmatically by following the same pattern:

1. Generate an HLSL shader string and write it to `Packages/<your-package>/Shader/Image/<EffectName>.shader`
2. Generate a C# class string inheriting `TextureCocktailContent` and write it to
   `Packages/<your-package>/Editor/Content/<EffectName>.cs`
3. Call `TextureCocktailPluginRegistry.Refresh()` to pick up the new class
4. The user can now select the shader in the TextureCocktail window

---

## Example Plugin with [TextureCocktailPlugin] Attribute

```csharp
[TextureCocktailPlugin("Vignette", "Adds a vignette darkening effect", "LuticaLab", "1.0.0")]
public class VignetteEffect : TextureCocktailContent { ... }
```

The metadata is visible in the Plugin Browser and can be inspected programmatically via
`TextureCocktailPluginRegistry.AllPlugins`.
