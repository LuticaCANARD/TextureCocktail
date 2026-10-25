using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// Draws numeric shader controls using the shader's declared range as the source of truth.
    /// Call inside an EditorGUI change check when the caller needs to request a preview update.
    /// </summary>
    internal static class ShaderParameterGUI
    {
        internal static bool TryGetRange(Shader shader, string propertyName, out Vector2 limits)
        {
            limits = default;
            if (shader == null) return false;

            for (int i = 0; i < ShaderUtil.GetPropertyCount(shader); i++)
            {
                if (ShaderUtil.GetPropertyName(shader, i) != propertyName) continue;
                if (ShaderUtil.GetPropertyType(shader, i) != ShaderUtil.ShaderPropertyType.Range)
                    return false;

                limits = new Vector2(
                    ShaderUtil.GetRangeLimits(shader, i, 1),
                    ShaderUtil.GetRangeLimits(shader, i, 2));
                return true;
            }
            return false;
        }

        internal static void DrawRange(Material material, string propertyName, string label)
        {
            if (material == null || !material.HasProperty(propertyName)) return;

            float value = material.GetFloat(propertyName);
            float updated = TryGetRange(material.shader, propertyName, out Vector2 limits)
                ? EditorGUILayout.Slider(label, value, limits.x, limits.y)
                : EditorGUILayout.FloatField(label, value);
            if (updated != value) material.SetFloat(propertyName, updated);
        }
    }
}
