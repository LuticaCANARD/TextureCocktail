using System;
using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// Owns the preview render texture and mask-composite material. Input textures/materials
    /// are borrowed. Render always restores RenderTexture.active, including on failure.
    /// </summary>
    internal sealed class TexturePreviewRenderer : IDisposable
    {
        private const RenderTextureFormat PreviewFormat = RenderTextureFormat.ARGB32;
        private const string CompositeShaderName = "Hidden/TextureCocktail/PolygonMaskComposite";
        private Material _compositeMaterial;
        internal RenderTexture Preview { get; private set; }

        internal void EnsurePreview(Texture source)
        {
            if (source == null) return;
            if (Preview != null && (Preview.width != source.width || Preview.height != source.height))
                ReleasePreview();
            if (Preview == null)
            {
                Preview = new RenderTexture(source.width, source.height, 0, PreviewFormat);
                Preview.Create();
            }
        }

        internal void Render(Texture source, Material material, int passIndex, Texture mask)
        {
            EnsurePreview(source);
            ShaderUtil.CompilePass(material, passIndex);
            RenderTexture previous = RenderTexture.active;
            RenderTexture processed = null;
            try
            {
                if (mask != null && EnsureCompositeMaterial())
                {
                    processed = RenderTexture.GetTemporary(source.width, source.height, 0, PreviewFormat);
                    Graphics.Blit(source, processed, material, passIndex);
                    _compositeMaterial.SetTexture("_OriginalTex", source);
                    _compositeMaterial.SetTexture("_ProcessedTex", processed);
                    _compositeMaterial.SetTexture("_MaskTex", mask);
                    Graphics.Blit(source, Preview, _compositeMaterial);
                }
                else
                {
                    Graphics.Blit(source, Preview, material, passIndex);
                }
            }
            finally
            {
                RenderTexture.active = previous;
                if (processed != null) RenderTexture.ReleaseTemporary(processed);
            }
        }

        private bool EnsureCompositeMaterial()
        {
            if (_compositeMaterial != null) return true;
            Shader shader = Shader.Find(CompositeShaderName);
            if (shader == null)
            {
                Debug.LogWarning($"Polygon composite shader not found: {CompositeShaderName}");
                return false;
            }
            _compositeMaterial = new Material(shader);
            return true;
        }

        private void ReleasePreview()
        {
            if (Preview == null) return;
            Preview.Release();
            UnityEngine.Object.DestroyImmediate(Preview);
            Preview = null;
        }

        public void Dispose()
        {
            ReleasePreview();
            if (_compositeMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(_compositeMaterial);
                _compositeMaterial = null;
            }
        }
    }
}
