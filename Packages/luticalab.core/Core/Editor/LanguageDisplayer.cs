#if UNITY_EDITOR
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization.Editor;
using UnityEngine;

namespace LuticaLab
{
    public enum LuticaLabSupportLanguage
    {
        English,
        Korean,
        Japanese
    }
    [Serializable]
    public class Langattributs
    {
        public string key;
        public string translated_text;
    }
    public class LanguageDisplayer:ScriptableObject
    {
        private static readonly Lazy<LanguageDisplayer> _languageDisplayer = new(() =>
        {
            LanguageDisplayer instance = CreateInstance<LanguageDisplayer>();
            instance.name = "LanguageDisplayer";
            return instance;
        });
        public static LanguageDisplayer Instance => _languageDisplayer.Value;
        // ID-Value...
        Dictionary<string, string> _languageDictionary = new();
        LuticaLabSupportLanguage _lang = LuticaLabSupportLanguage.English;
        private void OnEnable()
        {
            NowLanguage = GenerateToLuticaLabSupportLanguage(Application.systemLanguage);
            LoadLanguageDict(NowLanguage);
        }
        public LuticaLabSupportLanguage NowLanguage
        {
            get => _lang;
            set
            {
                if (_lang != value)
                {
                    _lang = value;
                    _languageDictionary.Clear();
                    LoadLanguageDict(_lang);
                }
            }
        }
        void LoadLanguageDict(LuticaLabSupportLanguage lang)
        {
            string assetPath = $"Packages/luticalab.core/Languages/{lang}.json";
            string json = null;

            var jsonload = AssetDatabase.LoadAssetAtPath(assetPath, typeof(TextAsset)) as TextAsset;
            if (jsonload != null && jsonload.text != null)
            {
                json = jsonload.text;
            }
            else
            {
                // Fallback: AssetDatabase may not be ready during early domain reload.
                // Resolve the package's actual on-disk location via PackageInfo so this
                // works for registry-installed packages (which live under Library/PackageCache),
                // not just embedded/local packages where the relative path happens to exist.
                try
                {
                    string fullPath = ResolvePackageAssetPath(assetPath);
                    if (!string.IsNullOrEmpty(fullPath) && System.IO.File.Exists(fullPath))
                    {
                        json = System.IO.File.ReadAllText(fullPath);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Language fallback read failed for {lang}: {e.Message}");
                }
            }

            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError($"Failed to load language bundle for {lang}");
                return;
            }

            try
            {
                var dict = JObject.Parse(json)["data"] as JObject;
                if (dict == null)
                {
                    Debug.LogError($"Language file {lang} missing 'data' object");
                    return;
                }
                foreach (var item in dict)
                {
                    _languageDictionary[item.Key.ToString()] = item.Value.ToString();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to parse language file for {lang}: {e.Message}");
            }
        }
        private static string ResolvePackageAssetPath(string assetPath)
        {
            // Prefer the resolved on-disk path from PackageInfo; works for packages
            // installed via UPM (Library/PackageCache/...) as well as embedded ones.
            var pkgInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);
            if (pkgInfo != null && !string.IsNullOrEmpty(pkgInfo.resolvedPath))
            {
                const string prefix = "Packages/";
                if (assetPath.StartsWith(prefix, StringComparison.Ordinal))
                {
                    int slash = assetPath.IndexOf('/', prefix.Length);
                    if (slash >= 0)
                    {
                        string remainder = assetPath.Substring(slash + 1);
                        return System.IO.Path.Combine(pkgInfo.resolvedPath, remainder);
                    }
                }
            }
            // Last-resort fallback for embedded/local packages where the project-relative
            // path is real on disk.
            return System.IO.Path.GetFullPath(assetPath);
        }

        public bool IsSupportedLanguage(SystemLanguage lang)
        {
            return lang == SystemLanguage.English 
                || lang == SystemLanguage.Korean 
                || lang == SystemLanguage.Japanese;
        }
        static public LuticaLabSupportLanguage GenerateToLuticaLabSupportLanguage(SystemLanguage lang,bool error = false)
        {
            return lang switch
            {
                SystemLanguage.English => LuticaLabSupportLanguage.English,
                SystemLanguage.Korean => LuticaLabSupportLanguage.Korean,
                SystemLanguage.Japanese => LuticaLabSupportLanguage.Japanese,
                _ => error ? throw new ArgumentException($"{lang} is Unsupported Language!") : LuticaLabSupportLanguage.English
            };
        }
        public string GetTranslatedLanguage(string key) =>
            _languageDictionary.TryGetValue(key, out var value) ? value : key;
    }
}
#endif