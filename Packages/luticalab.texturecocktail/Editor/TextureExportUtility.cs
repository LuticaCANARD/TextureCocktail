using System;
using System.IO;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// Shared PNG export. Inputs remain owned by the caller; temporary GPU/CPU
    /// textures are released here, including when rendering or file writes fail.
    /// Asset import, dialogs, and output-path selection remain with the caller.
    /// </summary>
    internal static class TextureExportUtility
    {
        /// <summary>
        /// Renders at the source resolution using ARGB32, then exports RGBA32 PNG.
        /// A pass of -1 preserves Graphics.Blit's default of drawing all passes.
        /// Sets the caller's material _MainTex to source without taking ownership.
        /// </summary>
        internal static void SaveMaterialPng(Texture source, Material material, string path, int pass = -1)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (material == null) throw new ArgumentNullException(nameof(material));

            RenderTexture previous = RenderTexture.active;
            RenderTexture rendered = null;
            try
            {
                rendered = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGB32);
                rendered.Create();
                material.SetTexture("_MainTex", source);
                RenderTexture.active = rendered;
                Graphics.Blit(source, rendered, material, pass);
                SaveRenderTexturePng(rendered, path);
            }
            finally
            {
                RenderTexture.active = previous;
                if (rendered != null)
                {
                    rendered.Release();
                    UnityEngine.Object.DestroyImmediate(rendered);
                }
            }
        }

        /// <summary>
        /// Exports an existing render texture without releasing or modifying it.
        /// Restores RenderTexture.active after readback and file I/O.
        /// </summary>
        internal static void SaveRenderTexturePng(RenderTexture source, string path)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            RenderTexture previous = RenderTexture.active;
            Texture2D result = null;
            try
            {
                RenderTexture.active = source;
                result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                result.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                result.Apply();
                File.WriteAllBytes(path, result.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (result != null) UnityEngine.Object.DestroyImmediate(result);
            }
        }
    }
}
