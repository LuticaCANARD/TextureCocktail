using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// Aggregates every shader package known to the TextureCocktail picker.
    /// Two sources are merged:
    ///   - Project assets of type <see cref="TextureCocktailShaderPackage"/>.
    ///   - <see cref="TextureCocktailContent"/> subclasses tagged with
    ///     <see cref="TextureCocktailShaderAttribute"/>.
    ///
    /// Asset-based registrations win on duplicates so authors can override
    /// metadata without recompiling.
    /// </summary>
    public static class TextureCocktailShaderRegistry
    {
        private static List<ITextureCocktailShaderPackage> _cached;

        public static IReadOnlyList<ITextureCocktailShaderPackage> All
        {
            get
            {
                if (_cached == null) Refresh();
                return _cached;
            }
        }

        public static ITextureCocktailShaderPackage FindByShader(Shader shader)
        {
            if (shader == null) return null;
            var list = All;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Shader == shader) return list[i];
            }
            return null;
        }

        public static bool IsRegistered(Shader shader) => FindByShader(shader) != null;

        public static void Refresh()
        {
            var collected = new List<ITextureCocktailShaderPackage>();
            CollectAssetPackages(collected);
            CollectAttributeBackedPackages(collected);
            collected.Sort(Compare);
            _cached = collected;
        }

        private static void CollectAssetPackages(List<ITextureCocktailShaderPackage> sink)
        {
            string[] guids;
            try
            {
                guids = AssetDatabase.FindAssets("t:" + nameof(TextureCocktailShaderPackage));
            }
            catch (Exception)
            {
                return;
            }
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                var pkg = AssetDatabase.LoadAssetAtPath<TextureCocktailShaderPackage>(path);
                if (pkg == null) continue;
                if (!pkg.IsCompatible(out var reason))
                {
                    Debug.LogWarning($"[TextureCocktail] Skipping package '{path}': {reason}");
                    continue;
                }
                sink.Add(pkg);
            }
        }

        private static void CollectAttributeBackedPackages(List<ITextureCocktailShaderPackage> sink)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (System.Reflection.ReflectionTypeLoadException ex) { types = ex.Types; }
                catch (Exception) { continue; }

                foreach (var t in types)
                {
                    if (t == null) continue;
                    var attr = (TextureCocktailShaderAttribute)Attribute.GetCustomAttribute(
                        t, typeof(TextureCocktailShaderAttribute));
                    if (attr == null) continue;
                    if (!typeof(TextureCocktailContent).IsAssignableFrom(t)) continue;

                    var shader = Shader.Find(attr.ShaderPath);
                    if (shader == null)
                    {
                        Debug.LogWarning($"[TextureCocktail] Shader '{attr.ShaderPath}' tagged on '{t.FullName}' could not be found.");
                        continue;
                    }
                    if (FindShaderInList(sink, shader) != null) continue;
                    sink.Add(new ReflectedPackage(shader, attr, t));
                }
            }
        }

        private static ITextureCocktailShaderPackage FindShaderInList(List<ITextureCocktailShaderPackage> list, Shader shader)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Shader == shader) return list[i];
            }
            return null;
        }

        private static int Compare(ITextureCocktailShaderPackage a, ITextureCocktailShaderPackage b)
        {
            int c = string.Compare(a.Category ?? "", b.Category ?? "", StringComparison.OrdinalIgnoreCase);
            if (c != 0) return c;
            return string.Compare(a.DisplayName ?? "", b.DisplayName ?? "", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class ReflectedPackage : ITextureCocktailShaderPackage
        {
            private readonly TextureCocktailShaderAttribute _attr;
            private readonly Type _contentType;

            public Shader Shader { get; }
            public string Category => _attr.Category;
            public string Description => null;
            public Texture2D Icon => null;
            public int PassIndex => _attr.PassIndex;
            public string[] RequiredProperties => _attr.RequiredProperties ?? new[] { "_MainTex" };

            public string DisplayName
            {
                get
                {
                    string localized = TryLocalize(_attr.LocalizationKey);
                    if (!string.IsNullOrEmpty(localized)) return localized;
                    if (!string.IsNullOrEmpty(_attr.DisplayName)) return _attr.DisplayName;
                    return Shader != null ? Shader.name : _contentType.Name;
                }
            }

            public ReflectedPackage(Shader shader, TextureCocktailShaderAttribute attr, Type contentType)
            {
                Shader = shader;
                _attr = attr;
                _contentType = contentType;
            }

            public bool IsCompatible(out string reason)
            {
                if (Shader == null) { reason = "Shader is null."; return false; }
                foreach (var prop in RequiredProperties)
                {
                    if (string.IsNullOrEmpty(prop)) continue;
                    if (Shader.FindPropertyIndex(prop) < 0)
                    {
                        reason = $"Shader '{Shader.name}' is missing required property '{prop}'.";
                        return false;
                    }
                }
                reason = null;
                return true;
            }

            private static string TryLocalize(string key)
            {
                if (string.IsNullOrEmpty(key)) return null;
                var displayer = LanguageDisplayer.Instance;
                if (displayer == null) return null;
                string s = displayer.GetTranslatedLanguage(key);
                return string.IsNullOrEmpty(s) || s == key ? null : s;
            }
        }
    }
}
