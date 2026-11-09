using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Game.Integration.Localization
{
    [CreateAssetMenu(menuName = "Ball Game/Localization Fonts")]
    public sealed class LocalizationFonts : ScriptableObject
    {
        public const string ResourcePath = "Localization/Fonts";
        [SerializeField] private TMP_FontAsset multilingual;
        [SerializeField] private TMP_FontAsset simplifiedChinese;

        public void ConfigureFallbacks()
        {
            AddFallback(multilingual, simplifiedChinese);
            foreach (var font in Resources.LoadAll<TMP_FontAsset>("Fonts & Materials")) AddFallback(font, multilingual);
            var global = TMP_Settings.fallbackFontAssets;
            if (global != null && multilingual != null && !global.Contains(multilingual)) global.Add(multilingual);
        }

        private static void AddFallback(TMP_FontAsset font, TMP_FontAsset fallback)
        {
            if (font == null || fallback == null || font == fallback) return;
            if (font.fallbackFontAssetTable == null) font.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (!font.fallbackFontAssetTable.Contains(fallback)) font.fallbackFontAssetTable.Add(fallback);
        }
    }
}
