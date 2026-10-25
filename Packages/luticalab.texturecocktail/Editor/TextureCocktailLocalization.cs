using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>Translates display text without changing shader IDs, preset keys or enum values.</summary>
    internal static class TextureCocktailLocalization
    {
        private static readonly Dictionary<string, string> ExistingLabelKeys = new Dictionary<string, string>
        {
            { "Artistic Effects", "artistic_effects_title" },
            { "Blend Mode", "blend_mode" },
            { "Fast Image Converter", "fast_image_converter_title" },
            { "Feature Extractor", "feature_extractor_title" },
            { "Height Scale", "height_scale" },
            { "Highlight Color", "mesh_preview_highlight_color" },
            { "Normal Map Generator", "normal_map_generator_title" },
            { "Normal Strength", "normal_strength" },
            { "Pulse Speed", "mesh_preview_pulse_speed" },
            { "Pulse Strength", "mesh_preview_pulse_strength" },
            { "Texture Blender", "texture_blender_title" },
            { "Use Mask", "use_mask" },
        };

        internal static string Text(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;
            if (ExistingLabelKeys.TryGetValue(key, out string mapped)) key = mapped;
            return LanguageDisplayer.Instance.GetTranslatedLanguage(key);
        }
        internal static string Format(string key, params object[] values) => string.Format(Text(key), values);

        internal static string[] Options(string[] names)
        {
            var labels = new string[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                // Unity popup slashes denote categories, not identifiers used by dispatch.
                string[] parts = names[i].Split('/');
                for (int j = 0; j < parts.Length; j++) parts[j] = Text(parts[j]);
                labels[i] = string.Join("/", parts);
            }
            return labels;
        }

        internal static Enum EnumPopup(string label, Enum selected, params GUILayoutOption[] options)
            => EnumPopup(label, selected, value => ObjectNames.NicifyVariableName(value.ToString()), options);

        internal static Enum EnumPopup(string label, Enum selected, Func<Enum, string> displayName,
            params GUILayoutOption[] options)
        {
            Array values = Enum.GetValues(selected.GetType());
            var labels = new string[values.Length];
            int index = -1;
            for (int i = 0; i < values.Length; i++)
            {
                var value = (Enum)values.GetValue(i);
                labels[i] = Text(displayName(value));
                if (value.Equals(selected)) index = i;
            }
            int updated = EditorGUILayout.Popup(Text(label), index, labels, options);
            return updated >= 0 && updated < values.Length ? (Enum)values.GetValue(updated) : selected;
        }
    }
}
