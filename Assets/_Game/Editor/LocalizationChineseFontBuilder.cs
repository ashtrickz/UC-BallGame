using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Game.Integration.Localization;
using Game.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Game.Editor
{
    /// <summary>Bakes a reproducible Chinese subset and rejects stale or dynamic fonts before a player build.</summary>
    public sealed class LocalizationChineseFontBuilder : IPreprocessBuildWithReport
    {
        public const string FontPath = "Assets/_Game/Art/Fonts/Localization/NotoSans Simplified Chinese SDF.asset";
        public const string CharactersPath = "Docs/ChineseFontCharacters.txt";
        public const string ManifestPath = "Docs/ChineseFontCharacters.csv";
        private const string SourcePath = "Assets/_Game/Art/Fonts/Localization/NotoSansCJKsc-Regular.otf";
        private const string FallbackPath = "Assets/_Game/Resources/Localization/fallback.json";
        private const string CsvPath = "Docs/LocalizationTemplate.csv";
        private const string Chinese = "zh-CN";
        private const string ChineseNameKey = "language.zh-CN";
        private const string RuntimeCharacters = "0123456789% ";
        private const int SamplingPointSize = 48;
        private const int Padding = 6;
        private const int AtlasSize = 1024;
        private static readonly Regex FormatArgument = new Regex(@"\{\d+(?:,-?\d+)?(?::[^{}]+)?\}");

        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            try { Validate(); }
            catch (Exception exception) { throw new BuildFailedException(exception.Message); }
        }

        [MenuItem("Tools/Ball Game/Localization/Rebuild Chinese Font")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before rebuilding the Chinese font.");

            var requirements = CollectRequirements();
            var characters = string.Concat(requirements.Keys.Select(char.ConvertFromUtf32));
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
            if (source == null)
                throw new InvalidOperationException("Chinese font source is missing: " + SourcePath);

            // Prepare the complete replacement before modifying the referenced asset or its sub-assets.
            var generated = TMP_FontAsset.CreateFontAsset(source, SamplingPointSize, Padding,
                GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, false);
            if (generated == null)
                throw new InvalidOperationException("Could not create the Chinese font.");
            try
            {
                if (!generated.TryAddCharacters(characters, out string missing))
                    throw new InvalidOperationException("Chinese glyphs are missing or do not fit the atlas: " + missing);

                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                if (font == null)
                    throw new InvalidOperationException("Install localization UI bindings first.");
                if (font.atlasTextures.Length != 1)
                    throw new InvalidOperationException("Expected one existing Chinese atlas; inspect its references before rebuilding.");

                var atlas = font.atlasTexture;
                var material = font.material;
                var fontName = font.name;
                EditorUtility.CopySerialized(generated.atlasTexture, atlas);
                EditorUtility.CopySerialized(generated.material, material);
                material.mainTexture = atlas;
                EditorUtility.CopySerialized(generated, font);
                font.name = fontName;
                font.atlasTextures = new[] { atlas };
                font.material = material;
                font.atlasPopulationMode = AtlasPopulationMode.Static;
                font.isMultiAtlasTexturesEnabled = false;
                var serialized = new SerializedObject(font);
                serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                font.ReadFontAssetDefinition();
                EditorUtility.SetDirty(atlas);
                EditorUtility.SetDirty(material);
                EditorUtility.SetDirty(font);
                AssetDatabase.SaveAssetIfDirty(font);

                File.WriteAllText(CharactersPath, characters, new UTF8Encoding(false));
                File.WriteAllText(ManifestPath, MakeManifest(requirements), new UTF8Encoding(false));
                Validate();
            }
            finally
            {
                foreach (var texture in generated.atlasTextures)
                    UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(generated.material);
                UnityEngine.Object.DestroyImmediate(generated);
            }
        }

        [MenuItem("Tools/Ball Game/Localization/Validate Chinese Font")]
        public static void Validate()
        {
            var requirements = CollectRequirements();
            var characters = string.Concat(requirements.Keys.Select(char.ConvertFromUtf32));
            if (!File.Exists(CharactersPath) || File.ReadAllText(CharactersPath) != characters
                || !File.Exists(ManifestPath) || File.ReadAllText(ManifestPath).Replace("\r\n", "\n") != MakeManifest(requirements))
                throw new InvalidOperationException("Chinese character manifest is stale. Run Tools/Ball Game/Localization/Rebuild Chinese Font.");

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null || font.atlasPopulationMode != AtlasPopulationMode.Static || font.sourceFontFile != null
                || font.isMultiAtlasTexturesEnabled || font.atlasTextures.Length != 1
                || font.atlasTexture.width != AtlasSize || font.atlasTexture.height != AtlasSize)
                throw new InvalidOperationException("Chinese font must be a static 1024 atlas without a runtime source font. Rebuild Chinese Font.");

            if (!font.HasCharacters(characters, out uint[] missing, false, false))
                throw new InvalidOperationException("Chinese atlas is missing: " + string.Join(", ", missing.Select(value => $"U+{value:X4}")));
            if (!font.characterTable.Select(character => character.unicode).OrderBy(value => value)
                .SequenceEqual(requirements.Keys.Select(value => (uint)value)))
                throw new InvalidOperationException("Chinese atlas contains characters outside its manifest. Rebuild Chinese Font.");
            if (AssetDatabase.GetDependencies(FontPath, true).Contains(SourcePath))
                throw new InvalidOperationException("Chinese font still has a build dependency on the full source OTF.");

            var fonts = Resources.Load<LocalizationFonts>(LocalizationFonts.ResourcePath);
            if (fonts == null)
                throw new InvalidOperationException("Localization fonts configuration is missing.");
            fonts.ValidateChineseCoverage(LocalizationTable.FromJson(File.ReadAllText(FallbackPath)));
            fonts.ValidateChineseCoverage(LocalizationTable.FromCsv(File.ReadAllText(CsvPath)));
        }

        public static SortedDictionary<int, SortedSet<string>> CollectRequirements()
        {
            var requirements = new SortedDictionary<int, SortedSet<string>>();
            AddCharacters(requirements, RuntimeCharacters, "runtime:numbers/percent/space");
            AddTable(requirements, LocalizationTable.FromJson(File.ReadAllText(FallbackPath)), "fallback");
            AddTable(requirements, LocalizationTable.FromCsv(File.ReadAllText(CsvPath)), "sheet-snapshot");
            return requirements;
        }

        private static void AddTable(SortedDictionary<int, SortedSet<string>> requirements, LocalizationTable table, string source)
        {
            foreach (var entry in table.GetEntries(Chinese))
            {
                if (!entry.Key.StartsWith("language.", StringComparison.Ordinal) || entry.Key == ChineseNameKey)
                    AddCharacters(requirements, entry.Value, source + ":" + Chinese + "/" + entry.Key);
            }
            foreach (var language in table.Languages)
            {
                foreach (var entry in table.GetEntries(language))
                    AddCharacters(requirements, entry.Value, source + ":" + language + "/" + entry.Key, true);
            }
        }

        private static void AddCharacters(SortedDictionary<int, SortedSet<string>> requirements, string text, string source, bool chineseOnly = false)
        {
            text = FormatArgument.Replace(text, string.Empty);
            for (var index = 0; index < text.Length; index++)
            {
                var unicode = char.ConvertToUtf32(text, index);
                if (char.IsHighSurrogate(text[index]))
                    index++;
                if (char.IsControl(text[index]) || chineseOnly && !LocalizationFonts.RequiresChineseFont(unicode))
                    continue;
                if (!requirements.TryGetValue(unicode, out var sources))
                    requirements.Add(unicode, sources = new SortedSet<string>(StringComparer.Ordinal));
                sources.Add(source);
            }
        }

        private static string MakeManifest(SortedDictionary<int, SortedSet<string>> requirements)
        {
            var result = new StringBuilder("codepoint,character,sources\n");
            foreach (var pair in requirements)
            {
                var character = char.ConvertFromUtf32(pair.Key).Replace("\"", "\"\"");
                result.Append($"U+{pair.Key:X4},\"{character}\",\"{string.Join(";", pair.Value)}\"\n");
            }
            return result.ToString();
        }
    }
}
