using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using LuticaLab;
using LuticaLab.TextureCocktail;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Checks imported locale assets and language changes without modifying user preferences.</summary>
public static class LocalizationRegressionChecks
{
    private const string PreferenceKey = "LuticaLab.Language";
    private static readonly LuticaLabSupportLanguage[] Languages =
    {
        LuticaLabSupportLanguage.English,
        LuticaLabSupportLanguage.Korean,
        LuticaLabSupportLanguage.Japanese,
        LuticaLabSupportLanguage.ChineseSimplified
    };

    public static int Run()
    {
        bool hadPreference = EditorPrefs.HasKey(PreferenceKey);
        string previousPreference = EditorPrefs.GetString(PreferenceKey, string.Empty);
        LanguageDisplayer displayer = LanguageDisplayer.Instance;
        LuticaLabSupportLanguage previousLanguage = displayer.NowLanguage;
        int passed = 0;
        try
        {
            Check("language identities, native Chinese caption and system-language routing", TestLanguageMapping, ref passed);
            Check("imported Chinese catalogue covers English and preserves composite formats", TestChineseCatalogue, ref passed);
            Check("switching all four languages reloads values and preserves missing-key fallback", TestLanguageSwitching, ref passed);
            Check("translated option labels preserve dispatch names, categories and custom plugins", TestOptionLabels, ref passed);
            Check("saved language survives provider recreation and invalid preferences fall back", TestSavedChoice, ref passed);
            Check("invalid language selections preserve the active language and preference", TestInvalidSelection, ref passed);
            return passed;
        }
        finally
        {
            try { displayer.NowLanguage = previousLanguage; }
            finally
            {
                if (hadPreference) EditorPrefs.SetString(PreferenceKey, previousPreference);
                else EditorPrefs.DeleteKey(PreferenceKey);
            }
        }
    }

    private static void TestLanguageMapping()
    {
        Require((int)LuticaLabSupportLanguage.English == 0 && (int)LuticaLabSupportLanguage.Korean == 1
            && (int)LuticaLabSupportLanguage.Japanese == 2 && (int)LuticaLabSupportLanguage.ChineseSimplified == 3,
            "Adding Chinese must not renumber existing language selections.");
        FieldInfo chinese = typeof(LuticaLabSupportLanguage).GetField(nameof(LuticaLabSupportLanguage.ChineseSimplified));
        var caption = chinese.GetCustomAttribute<InspectorNameAttribute>();
        Require(caption != null && caption.displayName == "简体中文", "The language picker must expose the native Simplified Chinese name.");
        var mappings = new Dictionary<SystemLanguage, LuticaLabSupportLanguage>
        {
            { SystemLanguage.English, LuticaLabSupportLanguage.English },
            { SystemLanguage.Korean, LuticaLabSupportLanguage.Korean },
            { SystemLanguage.Japanese, LuticaLabSupportLanguage.Japanese },
            { SystemLanguage.ChineseSimplified, LuticaLabSupportLanguage.ChineseSimplified },
            { SystemLanguage.Chinese, LuticaLabSupportLanguage.ChineseSimplified }
        };
        foreach (var entry in mappings)
        {
            Require(LanguageDisplayer.Instance.IsSupportedLanguage(entry.Key), "Expected supported system language: " + entry.Key);
            Require(LanguageDisplayer.GenerateToLuticaLabSupportLanguage(entry.Key, true) == entry.Value,
                "Wrong locale for system language: " + entry.Key);
        }
        foreach (var unsupported in new[] { SystemLanguage.ChineseTraditional, SystemLanguage.French, SystemLanguage.Unknown })
        {
            Require(!LanguageDisplayer.Instance.IsSupportedLanguage(unsupported), "Unsupported language must not claim a dedicated translation.");
            Require(LanguageDisplayer.GenerateToLuticaLabSupportLanguage(unsupported) == LuticaLabSupportLanguage.English,
                "Unsupported system languages must retain the English fallback.");
            RequireArgumentException(() => LanguageDisplayer.GenerateToLuticaLabSupportLanguage(unsupported, true),
                "Explicit unsupported-language errors must still throw.");
        }
    }

    private static void TestChineseCatalogue()
    {
        JObject english = Catalogue(LuticaLabSupportLanguage.English);
        JObject chinese = Catalogue(LuticaLabSupportLanguage.ChineseSimplified);
        Require(english.Count > 0 && chinese.Count >= english.Count, "Chinese must cover the full English UI catalogue.");
        foreach (JProperty entry in english.Properties())
        {
            Require(chinese.TryGetValue(entry.Name, out JToken translated), "Missing Chinese key: " + entry.Name);
            Require(translated.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)translated),
                "Empty or invalid Chinese value: " + entry.Name);
            string[] sourceArguments = FormatArguments((string)entry.Value);
            string[] translatedArguments = FormatArguments((string)translated);
            Require(sourceArguments.SequenceEqual(translatedArguments), "Changed format arguments in Chinese key: " + entry.Name);
        }
        foreach (string key in new[] { "save_texture", "fast_image_converter_title", "mesh_visibility_title", "ab_open_button" })
        {
            string translated = (string)chinese[key];
            Require(ContainsHan(translated) && translated != (string)english[key],
                "Representative product UI must actually be translated: " + key);
        }
    }

    private static void TestLanguageSwitching()
    {
        LanguageDisplayer displayer = LanguageDisplayer.Instance;
        var sequence = new[]
        {
            LuticaLabSupportLanguage.ChineseSimplified,
            LuticaLabSupportLanguage.English,
            LuticaLabSupportLanguage.Japanese,
            LuticaLabSupportLanguage.Korean,
            LuticaLabSupportLanguage.ChineseSimplified
        };
        foreach (var language in sequence)
        {
            displayer.NowLanguage = language;
            Require(displayer.NowLanguage == language, "Language selection must update immediately.");
            foreach (JProperty entry in Catalogue(language).Properties())
                Require(displayer.GetTranslatedLanguage(entry.Name) == (string)entry.Value,
                    "Stale or incorrectly loaded translation for " + language + ": " + entry.Name);
            const string unknown = "TextureCocktail_Regression_Missing_Translation_Key";
            Require(displayer.GetTranslatedLanguage(unknown) == unknown, "Unknown keys must retain the original fallback text.");
        }
    }

    private static void TestOptionLabels()
    {
        var sourceNames = new[]
        {
            "Filters/Fast Image Converter",
            "Regression.Custom/Custom HDR • v2",
            "Effects/Texture Blender",
            "Generators/Normal Map Generator"
        };
        var originalNames = (string[])sourceNames.Clone();
        LanguageDisplayer displayer = LanguageDisplayer.Instance;
        displayer.NowLanguage = LuticaLabSupportLanguage.ChineseSimplified;
        string[] labels = TextureCocktailLocalization.Options(sourceNames);
        Require(!ReferenceEquals(labels, sourceNames) && sourceNames.SequenceEqual(originalNames),
            "Translating popup labels must not mutate the names used for shader or preset dispatch.");
        string[] expected =
        {
            displayer.GetTranslatedLanguage("Filters") + "/" + displayer.GetTranslatedLanguage("fast_image_converter_title"),
            originalNames[1],
            displayer.GetTranslatedLanguage("Effects") + "/" + displayer.GetTranslatedLanguage("texture_blender_title"),
            displayer.GetTranslatedLanguage("Generators") + "/" + displayer.GetTranslatedLanguage("normal_map_generator_title")
        };
        Require(labels.SequenceEqual(expected),
            "Option order and category separators must survive translation, including existing title-key aliases and unknown plugin names.");
        Require(ContainsHan(labels[0]) && labels[0] != originalNames[0],
            "Built-in option labels must actually display the selected Chinese translation.");
        for (int i = 0; i < labels.Length; i++)
            Require(labels[i].Count(character => character == '/') == sourceNames[i].Count(character => character == '/'),
                "Localization must preserve the popup category hierarchy at each dispatch index.");

        displayer.NowLanguage = LuticaLabSupportLanguage.English;
        string[] englishLabels = TextureCocktailLocalization.Options(sourceNames);
        Require(englishLabels.SequenceEqual(originalNames) && sourceNames.SequenceEqual(originalNames),
            "Switching back to English must rebuild display labels without losing original dispatch names or retaining Chinese labels.");
        Require(labels.SequenceEqual(expected), "A later language change must not rewrite an earlier returned label array.");
    }

    private static void TestSavedChoice()
    {
        foreach (var language in Languages)
        {
            LanguageDisplayer.Instance.NowLanguage = language;
            Require(EditorPrefs.GetString(PreferenceKey, string.Empty) == language.ToString(),
                "An explicit selection must persist its stable enum name.");
            RequireNewProvider(language);
        }
        EditorPrefs.DeleteKey(PreferenceKey);
        var systemDefault = LanguageDisplayer.GenerateToLuticaLabSupportLanguage(Application.systemLanguage);
        RequireNewProvider(systemDefault);
        foreach (string invalid in new[] { "NotALanguage", "999", string.Empty })
        {
            EditorPrefs.SetString(PreferenceKey, invalid);
            RequireNewProvider(systemDefault);
        }
    }

    private static void RequireNewProvider(LuticaLabSupportLanguage expected)
    {
        LanguageDisplayer fresh = ScriptableObject.CreateInstance<LanguageDisplayer>();
        try
        {
            Require(fresh.NowLanguage == expected, "A new provider must restore a saved choice or fall back to the system language.");
            Require(fresh.GetTranslatedLanguage("mesh_visibility_title") == (string)Catalogue(expected)["mesh_visibility_title"],
                "Restoring the language must load its catalogue before the first lookup.");
        }
        finally { Object.DestroyImmediate(fresh); }
    }

    private static void TestInvalidSelection()
    {
        LanguageDisplayer displayer = LanguageDisplayer.Instance;
        displayer.NowLanguage = LuticaLabSupportLanguage.ChineseSimplified;
        string title = displayer.GetTranslatedLanguage("mesh_visibility_title");
        string saved = EditorPrefs.GetString(PreferenceKey, string.Empty);
        RequireArgumentException(() => displayer.NowLanguage = (LuticaLabSupportLanguage)999,
            "Invalid enum values must be rejected before changing active state.");
        Require(displayer.NowLanguage == LuticaLabSupportLanguage.ChineseSimplified
            && displayer.GetTranslatedLanguage("mesh_visibility_title") == title
            && EditorPrefs.GetString(PreferenceKey, string.Empty) == saved,
            "Rejected language selection must not change the dictionary or saved preference.");
    }

    private static JObject Catalogue(LuticaLabSupportLanguage language)
    {
        string path = "Packages/luticalab.core/Languages/" + language + ".json";
        TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        Require(asset != null, "The installed locale must import as a real Unity TextAsset: " + path);
        var settings = new JsonLoadSettings
        {
            // Existing bundles have legacy duplicate entries; new Chinese must be unambiguous.
            DuplicatePropertyNameHandling = language == LuticaLabSupportLanguage.ChineseSimplified
                ? DuplicatePropertyNameHandling.Error : DuplicatePropertyNameHandling.Replace
        };
        JObject root = JObject.Parse(asset.text, settings);
        var data = root["data"] as JObject;
        Require(data != null, "Locale asset requires a data object: " + path);
        return data;
    }

    private sealed class FormatArgument
    {
        internal readonly int Index;
        internal FormatArgument(int index) { Index = index; }
    }

    private sealed class FormatRecorder : IFormatProvider, ICustomFormatter
    {
        internal readonly List<string> Arguments = new List<string>();
        public object GetFormat(Type type) => type == typeof(ICustomFormatter) ? this : null;
        public string Format(string format, object arg, IFormatProvider provider)
        {
            var argument = arg as FormatArgument;
            Require(argument != null, "Unexpected composite format argument.");
            Arguments.Add(argument.Index.ToString(CultureInfo.InvariantCulture) + ":" + (format ?? string.Empty));
            return "sample";
        }
    }

    private static string[] FormatArguments(string text)
    {
        int maximum = -1;
        foreach (Match match in Regex.Matches(text, @"\{([0-9]+)"))
        {
            Require(int.TryParse(match.Groups[1].Value, out int index) && index < 1024,
                "UI format argument index is invalid or unreasonably large.");
            maximum = Math.Max(maximum, index);
        }
        var arguments = new object[maximum + 1];
        for (int i = 0; i < arguments.Length; i++) arguments[i] = new FormatArgument(i);
        var recorder = new FormatRecorder();
        // The actual .NET formatter validates escaped braces, placeholder indices and format syntax.
        string.Format(recorder, text, arguments);
        recorder.Arguments.Sort(StringComparer.Ordinal);
        return recorder.Arguments.ToArray();
    }

    private static bool ContainsHan(string text)
        => text != null && text.Any(character => character >= '\u3400' && character <= '\u9fff');

    private static void RequireArgumentException(Action action, string message)
    {
        bool threw = false;
        try { action(); }
        catch (ArgumentException) { threw = true; }
        Require(threw, message);
    }

    private static void Check(string name, Action test, ref int passed)
    {
        test();
        passed++;
        Debug.Log("TextureCocktail localization PASS: " + name);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Localization regression: " + message);
    }
}
