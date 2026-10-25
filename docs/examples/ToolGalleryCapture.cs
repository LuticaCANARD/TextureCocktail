// Documentation-only comparison-sheet generator. Copy to Assets/Editor to run.
// Uses TextureCocktail's actual preview pipeline; does not alter source assets or scenes.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using LuticaLab.TextureCocktail;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using TextureEditor = LuticaLab.TextureCocktail.TextureCocktail;

public static class ToolGalleryCapture
{
    const int Size = 512;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<Texture2D> owned = new List<Texture2D>();
    static readonly List<Sheet> sheets = new List<Sheet>();
    static string outputDirectory;
    static bool exitWhenDone;

    [Serializable]
    sealed class Pane
    {
        public string title, settings;
        public string path;
        [NonSerialized] public Texture2D texture;
        public Pane(string title, string settings, Texture2D texture)
        { this.title = title; this.settings = settings; this.texture = texture; }
    }
    [Serializable]
    sealed class Sheet
    {
        public string file, title, subtitle;
        public Pane[] panes;
        public Sheet(string file, string title, string subtitle, params Pane[] panes)
        { this.file = file; this.title = title; this.subtitle = subtitle; this.panes = panes; }
    }

    [Serializable]
    sealed class Manifest { public List<Sheet> sheets; }

    [MenuItem("LuticaLab/Examples/Export Tool Gallery Sources")]
    public static void CaptureInEditor() { Start(false); }
    public static void Run() { Start(true); }

    static void Start(bool exit)
    {
        exitWhenDone = exit;
        try
        {
            outputDirectory = Path.GetFullPath("Logs/ToolGalleryRaw");
            Directory.CreateDirectory(outputDirectory);
            sheets.Clear();
            var source = CreatePanel(false);
            var height = CreatePanel(true);
            var grunge = CreateGrunge();
            sheets.Add(new Sheet("tool-fast-image-converter", "Fast Image Converter", "Lift a dark panel, soften detail, or sharpen its edges.",
                new Pane("Original", "Procedural metal panel / 512 x 512", source),
                new Pane("Brighten", "Brightness 0.2 / contrast 1.1 / saturation 1.05", Render(source, "Hidden/FastImageConverter", (e,c,m) => Call(c, "ApplyPreset", "Brighten"))),
                new Pane("Blur", "Filter Type: Blur / amount 0.8", Render(source, "Hidden/FastImageConverter", (e,c,m) => { m.SetFloat("_BlurAmount", .8f); Set(c,"filterMode",FastImageFilterMode.Blur); Call(c,"ApplyFilterMode"); })),
                new Pane("Sharpen", "Filter Type: Sharpen / amount 1.5", Render(source, "Hidden/FastImageConverter", (e,c,m) => { m.SetFloat("_SharpAmount",1.5f); Set(c,"filterMode",FastImageFilterMode.Sharpen); Call(c,"ApplyFilterMode"); }))));
            sheets.Add(new Sheet("tool-color-correction", "Color Correction", "Balance the same material for a warm or cool environment.",
                new Pane("Original", "Neutral input", source),
                new Pane("Warm grade", "Basic / temperature +0.9 / exposure +0.5", Render(source,"Hidden/ColorCorrection",(e,c,m) => { m.SetFloat("_Temperature",.9f); m.SetFloat("_Exposure",.5f); })),
                new Pane("Cool grade", "Basic / temperature -0.9 / exposure +0.5", Render(source,"Hidden/ColorCorrection",(e,c,m) => { m.SetFloat("_Temperature",-.9f); m.SetFloat("_Exposure",.5f); }))));
            sheets.Add(new Sheet("tool-hsv-adjustment", "HSV Adjustment", "Make color variants while preserving the panel's painted detail.",
                new Pane("Original", "HSV convert and offset: 0", source),
                new Pane("Violet variant", "HSV Offset: hue +0.25 / saturation +0.1 / value 0", Render(source,"Luticalab/ImageFilter",(e,c,m) => m.SetVector("_hsvOffsetVector",new Vector4(.25f,.1f,0,0)))),
                new Pane("Red variant", "HSV Offset: hue +0.5 / saturation +0.1 / value 0", Render(source,"Luticalab/ImageFilter",(e,c,m) => m.SetVector("_hsvOffsetVector",new Vector4(.5f,.1f,0,0))))));
            sheets.Add(new Sheet("tool-texture-blender", "Texture Blender", "Blend a separate grime layer into a clean material.",
                new Pane("Base texture", "Clean metal panel", source),
                new Pane("Blend texture", "Procedural grime / same 512 x 512 UVs", grunge),
                new Pane("Multiply result", "Multiply / amount 0.75 / mask off", Render(source,"Hidden/TextureBlender",(e,c,m) => { m.SetTexture("_BlendTex",grunge); m.SetFloat("_BlendAmount",.75f); Set(c,"blendMode",BlendMode.Multiply); Call(c,"ApplyBlendMode"); }))));
            var artInput = Render(source,"Hidden/ColorCorrection",(e,c,m) => m.SetFloat("_Exposure",1.5f));
            sheets.Add(new Sheet("tool-artistic-effects", "Artistic Effects", "Turn a brighter material into pixel, print, or cartoon artwork.",
                new Pane("Input", "Color Correction / exposure +1.5", artInput),
                new Pane("Pixelate", "Pixel size 16 / center 0.5, 0.5", Render(artInput,"Hidden/ArtisticEffects",(e,c,m) => { m.SetFloat("_PixelSize",16); Set(c,"currentEffect",ArtisticEffect.Pixelate); Call(c,"ApplyEffect"); })),
                new Pane("Halftone", "Dot size 10 / dot angle 45 degrees", Render(artInput,"Hidden/ArtisticEffects",(e,c,m) => { m.SetFloat("_DotSize",10); Set(c,"currentEffect",ArtisticEffect.Halftone); Call(c,"ApplyEffect"); })),
                new Pane("Cartoon", "Color steps 8 / edge threshold 0.15", Render(artInput,"Hidden/ArtisticEffects",(e,c,m) => { m.SetFloat("_ColorSteps",8); m.SetFloat("_EdgeThreshold",.15f); Set(c,"currentEffect",ArtisticEffect.Cartoon); Call(c,"ApplyEffect"); }))));
            sheets.Add(new Sheet("tool-feature-extractor", "Feature Extractor", "Reveal seams and edge details with a colored overlay.",
                new Pane("Original", "Input luminance drives edge detection", source),
                new Pane("Edge Detection", "Sobel / sensitivity 0.5 / cyan / intensity 1", Render(source,"Hidden/FeatureExtractor",(e,c,m) => { m.SetColor("_FeatureColor",new Color(0,1,1)); m.SetFloat("_EdgeSensitivity",.5f); })),
                new Pane("Canny Edge", "Sensitivity 0.8 / cyan", Render(source,"Hidden/FeatureExtractor",(e,c,m) => { m.SetColor("_FeatureColor",new Color(0,1,1)); m.SetFloat("_EdgeSensitivity",.8f); Set(c,"extractionMode",FeatureExtractionMode.CannyEdge); Call(c,"ApplyExtractionMode"); }))));
            sheets.Add(new Sheet("tool-normal-map-generator", "Normal Map Generator", "Convert a grayscale height texture into tangent-space normals.",
                new Pane("Height input", "Raised panel, grooves, dial and fasteners", height),
                new Pane("Subtle relief", "Normal strength 2 / height scale 1", Render(height,"Luticalab/NormalMapGenerator",(e,c,m) => { m.SetFloat("_Strength",2); m.SetFloat("_HeightScale",1); })),
                new Pane("Strong relief", "Normal strength 10 / height scale 1", Render(height,"Luticalab/NormalMapGenerator",(e,c,m) => { m.SetFloat("_Strength",10); m.SetFloat("_HeightScale",1); }))));
            foreach(var sheet in sheets)
                for(int i=0;i<sheet.panes.Length;i++)
                {
                    var pane=sheet.panes[i];
                    pane.path=Path.Combine(outputDirectory,sheet.file+"-"+i+".png");
                    File.WriteAllBytes(pane.path,pane.texture.EncodeToPNG());
                }
            File.WriteAllText(Path.Combine(outputDirectory,"gallery.json"),JsonUtility.ToJson(new Manifest { sheets=sheets },true));
            Cleanup();Debug.Log("TEXTURE_COCKTAIL_TOOL_GALLERY_PASSED 7 sheets");
            if(exitWhenDone)EditorApplication.Exit(0);
        }
        catch(Exception ex) { Fail(ex); }
    }

    static Texture2D Render(Texture2D input, string shaderName, Action<TextureEditor, object, Material> configure)
    {
        var editor = ScriptableObject.CreateInstance<TextureEditor>();
        try
        {
            var shader = Shader.Find(shaderName);
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("Shader unavailable: " + shaderName);
            editor.SetTargetTexture(input);
            Call(editor,"OnShaderChange",shader);
            var content = typeof(TextureEditor).GetField("_shaderWindow",Private).GetValue(editor);
            var material = (Material)typeof(TextureEditor).GetField("_calcMaterial",Private).GetValue(editor);
            configure(editor,content,material);
            editor.CompileShader();
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = editor.PreviewTexture;
                var result = NewTexture(shaderName);
                result.ReadPixels(new Rect(0,0,Size,Size),0,0); result.Apply();
                var pixels = result.GetPixels32();
                int changed=0, magenta=0;
                var before = input.GetPixels32();
                for(int i=0;i<pixels.Length;i++)
                {
                    if(pixels[i].r>245 && pixels[i].g<10 && pixels[i].b>245) magenta++;
                    if(Math.Abs(pixels[i].r-before[i].r)+Math.Abs(pixels[i].g-before[i].g)+Math.Abs(pixels[i].b-before[i].b)>8) changed++;
                }
                if(magenta>pixels.Length/2 || changed<pixels.Length/100) throw new InvalidOperationException("Invalid shader output: " + shaderName);
                Debug.Log("TOOL_GALLERY_RENDER " + shaderName + " changed="+changed+" / "+pixels.Length);
                return result;
            }
            finally { RenderTexture.active = previous; }
        }
        finally { Object.DestroyImmediate(editor); }
    }
    static void Set(object target,string field,object value) { target.GetType().GetField(field,Private).SetValue(target,value); }
    static void Call(object target,string method,params object[] values) { target.GetType().GetMethod(method,Private).Invoke(target,values); }
    static Texture2D NewTexture(string name)
    {
        var texture=new Texture2D(Size,Size,TextureFormat.RGBA32,false){name=name,hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
        owned.Add(texture);return texture;
    }

    static Texture2D CreatePanel(bool heightOnly)
    {
        var texture=NewTexture(heightOnly ? "Panel height" : "Metal panel");
        var pixels=new Color[Size*Size];
        for(int y=0;y<Size;y++) for(int x=0;x<Size;x++)
        {
            float u=(x+.5f)/Size, v=(y+.5f)/Size;
            float noise=Mathf.PerlinNoise(u*75,v*75)-.5f;
            Color color=new Color(.16f,.21f,.24f); float h=.18f;
            if(In(u,v,.045f,.045f,.955f,.955f)) { color=new Color(.28f,.35f,.38f);h=.38f; }
            if(In(u,v,.07f,.07f,.93f,.93f)) { color=new Color(.15f,.39f,.40f);h=.58f; }
            if(In(u,v,.085f,.28f,.915f,.32f)) { color=new Color(.04f,.07f,.09f);h=.13f; }
            if(In(u,v,.64f,.34f,.88f,.87f)) { color=new Color(.78f,.48f,.17f);h=.65f; }
            if(In(u,v,.665f,.365f,.855f,.845f)) { color=new Color(.13f,.18f,.19f);h=.25f; }
            if(In(u,v,.68f,.38f,.84f,.83f))
            {
                float slot=Mathf.Repeat((v-.38f)*10,1);
                color=slot<.4f?new Color(.055f,.08f,.09f):new Color(.41f,.48f,.49f);h=slot<.4f?.15f:.58f;
            }
            if(In(u,v,.11f,.12f,.89f,.23f))
            {
                bool stripe=Mathf.Repeat((u+v)*9,1)<.5f;
                color=stripe?new Color(.86f,.65f,.20f):new Color(.10f,.14f,.16f);h=.6f;
            }
            float d=Vector2.Distance(new Vector2(u,v),new Vector2(.35f,.61f));
            if(d<.215f) { color=new Color(.06f,.11f,.13f);h=.23f; }
            if(d<.198f) { color=new Color(.5f,.6f,.61f);h=.84f; }
            if(d<.177f) { color=new Color(.075f,.15f,.18f);h=.45f; }
            if(d<.154f)
            {
                color=new Color(.10f,.24f,.27f)*(.8f+.5f*v);
                h=.57f+.1f*Mathf.Sqrt(1-d*d/(.154f*.154f));
                float angle=Mathf.Atan2(v-.61f,u-.35f);
                if(d>.125f && Mathf.Abs(Mathf.Sin(angle*12))<.15f) color=new Color(.78f,.83f,.74f);
                if(Mathf.Abs((v-.61f)-.65f*(u-.35f))<.005f && u>.32f && u<.46f) color=new Color(.95f,.40f,.12f);
            }
            if(d<.028f) { color=new Color(.85f,.48f,.15f);h=.92f; }
            foreach(float bx in new[]{.105f,.895f}) foreach(float by in new[]{.095f,.905f})
            {
                float bolt=Vector2.Distance(new Vector2(u,v),new Vector2(bx,by));
                if(bolt<.025f) { color=new Color(.065f,.10f,.12f);h=.22f; }
                if(bolt<.017f) { color=new Color(.60f,.65f,.64f);h=.80f; }
                if(bolt<.012f && Mathf.Abs(u-bx)<.003f) { color=new Color(.1f,.14f,.15f);h=.50f; }
            }
            if(In(u,v,.16f,.80f,.53f,.835f)) { color=new Color(.75f,.81f,.75f);h=.6f; }
            if(In(u,v,.16f,.80f,.53f,.835f) && Mathf.Repeat(u*70,1)<.5f) color*=.55f;
            float shade=.78f+.23f*v+.05f*Mathf.Sin(u*4);
            color*=shade+noise*.08f;
            if(heightOnly) color=new Color(h,h,h);
            color.a=1;pixels[y*Size+x]=color;
        }
        texture.SetPixels(pixels);texture.Apply();return texture;
    }
    static bool In(float u,float v,float x0,float y0,float x1,float y1) { return u>x0&&u<x1&&v>y0&&v<y1; }
    static Texture2D CreateGrunge()
    {
        var texture=NewTexture("Grime overlay");var pixels=new Color[Size*Size];
        for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)
        {
            float u=x/(float)Size,v=y/(float)Size;
            float cloud=Mathf.PerlinNoise(u*8+12,v*8+31);
            float fine=Mathf.PerlinNoise(u*87,v*87);
            float dirt=Mathf.Clamp01((cloud-.3f)*1.9f+fine*.15f);
            pixels[y*Size+x]=Color.Lerp(new Color(.21f,.13f,.07f),new Color(.99f,.95f,.81f),dirt);
        }
        texture.SetPixels(pixels);texture.Apply();return texture;
    }

    static void Fail(Exception ex)
    {
        Debug.LogException(ex);Cleanup();if(exitWhenDone)EditorApplication.Exit(1);
    }
    static void Cleanup()
    {
        foreach(var texture in owned)if(texture!=null)Object.DestroyImmediate(texture);
        owned.Clear();sheets.Clear();
    }
}
