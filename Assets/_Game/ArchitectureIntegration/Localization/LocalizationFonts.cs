using System;
using System.Collections.Generic;
using Slime.Localization;
using TMPro;
using UnityEngine;

namespace Slime.Infrastructure.Localization
{
    [CreateAssetMenu(menuName = "Ball Game/Localization Fonts")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Slime.Integration.Localization", sourceAssembly: "Game.Services", sourceClassName: "LocalizationFonts")]
    public sealed class LocalizationFonts : ScriptableObject
    {
        private const int CjkStart = 0x2E80;
        private const int CjkEnd = 0x9FFF;
        private const int CompatibilityStart = 0xF900;
        private const int CompatibilityEnd = 0xFAFF;
        private const int FullwidthStart = 0xFF00;
        private const int FullwidthEnd = 0xFFEF;
        private const int SupplementaryStart = 0x20000;
        private const int SupplementaryEnd = 0x3FFFF;
        public const string ResourcePath = "Localization/Fonts";
        [SerializeField] private TMP_FontAsset multilingual;
        [SerializeField] private TMP_FontAsset simplifiedChinese;

        public void ValidateChineseCoverage(LocalizationTable table)
        {
            if (simplifiedChinese == null)
                throw new InvalidOperationException("The bundled Chinese font is missing.");

            foreach (var language in table.Languages)
            {
                foreach (var entry in table.GetEntries(language))
                {
                    for (var index = 0; index < entry.Value.Length; index++)
                    {
                        var unicode = char.ConvertToUtf32(entry.Value, index);
                        if (char.IsHighSurrogate(entry.Value[index]))
                            index++;

                        if (RequiresChineseFont(unicode) && !simplifiedChinese.HasCharacter(unicode))
                            throw new FormatException($"Chinese font has no U+{unicode:X4} for {language}/{entry.Key}. Rebuild the Chinese font and player before publishing new characters.");
                    }
                }
            }
        }

        public static bool RequiresChineseFont(int unicode)
        {
            return unicode >= CjkStart && unicode <= CjkEnd
                || unicode >= CompatibilityStart && unicode <= CompatibilityEnd
                || unicode >= FullwidthStart && unicode <= FullwidthEnd
                || unicode >= SupplementaryStart && unicode <= SupplementaryEnd;
        }

        public void ConfigureFallbacks()
        {
            AddFallback(multilingual, simplifiedChinese);
            foreach (var font in Resources.LoadAll<TMP_FontAsset>("Fonts & Materials"))
                AddFallback(font, multilingual);
            var global = TMP_Settings.fallbackFontAssets;
            if (global != null && multilingual != null && !global.Contains(multilingual))
                global.Add(multilingual);
        }

        private static void AddFallback(TMP_FontAsset font, TMP_FontAsset fallback)
        {
            if (font == null || fallback == null || font == fallback)
                return;

            if (font.fallbackFontAssetTable == null)
                font.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (!font.fallbackFontAssetTable.Contains(fallback))
                font.fallbackFontAssetTable.Add(fallback);
        }
    }
}
