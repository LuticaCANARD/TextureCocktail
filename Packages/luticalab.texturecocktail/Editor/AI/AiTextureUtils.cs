using System;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// Shared texture encoding/decoding utilities used by all AI backend implementations.
    /// </summary>
    public static class AiTextureUtils
    {
        /// <summary>
        /// Encodes a <see cref="Texture2D"/> to a base64 PNG string.
        /// Handles non-readable textures by blitting through a temporary RenderTexture.
        /// Returns <c>null</c> on failure.
        /// </summary>
        public static string TextureToBase64(Texture2D tex)
        {
            if (tex == null) return null;
            try
            {
                byte[] pngBytes;
                if (tex.isReadable)
                {
                    pngBytes = tex.EncodeToPNG();
                }
                else
                {
                    var rt = new RenderTexture(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
                    Graphics.Blit(tex, rt);
                    RenderTexture.active = rt;
                    var readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                    readable.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    readable.Apply();
                    RenderTexture.active = null;
                    rt.Release();
                    pngBytes = readable.EncodeToPNG();
                    UnityEngine.Object.DestroyImmediate(readable);
                }
                return Convert.ToBase64String(pngBytes);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AiTextureUtils] Could not encode texture to base64: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Decodes a base64-encoded image (PNG/JPEG) into a <see cref="Texture2D"/>.
        /// Returns <c>null</c> on failure.
        /// </summary>
        public static Texture2D Base64ToTexture(string base64)
        {
            if (string.IsNullOrEmpty(base64)) return null;
            try
            {
                byte[] bytes = Convert.FromBase64String(base64);
                var tex = new Texture2D(2, 2);
                tex.LoadImage(bytes);
                return tex;
            }
            catch
            {
                return null;
            }
        }
    }
}
