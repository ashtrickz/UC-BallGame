using Slime.Foundation;
using System;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Slime.Editor;
using Slime.Infrastructure.Localization;
using Slime.Localization;
using Slime.Settings;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using SettingsService = Slime.Settings.SettingsService;

namespace Slime.Tests.EditMode
{
    /// <summary>Protects the Chinese subset, its build dependencies and the fallback for unsupported remote translations.</summary>
    public sealed class LocalizationChineseFontTests
    {
        [Test]
        public void Chinese_atlas_matches_the_reviewed_sources_and_has_no_runtime_otf_dependency()
        {
            Assert.DoesNotThrow(LocalizationChineseFontBuilder.Validate);
        }

        [Test]
        public void Character_manifest_has_numbers_but_no_format_placeholders_or_other_language_names()
        {
            var characters = File.ReadAllText(LocalizationChineseFontBuilder.CharactersPath);
            foreach (var digit in "0123456789")
                StringAssert.Contains(digit.ToString(), characters);
            Assert.IsFalse(characters.Contains("{"));
            Assert.IsFalse(characters.Contains("}"));
            Assert.IsFalse(characters.Any(character => character >= '\u0400' && character <= '\u04FF'));
            Assert.IsFalse(characters.Contains('\n'));
        }

        [TestCase("zh-CN", "\u6C49", "U+6C49")]
        [TestCase("en", "\u6C49", "U+6C49")]
        [TestCase("zh-CN", "\U00020000", "U+20000")]
        public void New_remote_cjk_characters_are_rejected_with_the_key_and_codepoint(string language, string text, string codepoint)
        {
            var fonts = Resources.Load<LocalizationFonts>(LocalizationFonts.ResourcePath);
            var table = LocalizationTable.FromCsv("key," + language + "\nnew.text," + text);
            var exception = Assert.Throws<FormatException>(() => fonts.ValidateChineseCoverage(table));
            StringAssert.Contains(codepoint, exception.Message);
            StringAssert.Contains(language + "/new.text", exception.Message);
        }

        [Test]
        public void Validation_does_not_add_characters_to_the_baked_atlas()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LocalizationChineseFontBuilder.FontPath);
            var before = font.characterTable.Select(character => character.unicode).ToArray();
            var fonts = Resources.Load<LocalizationFonts>(LocalizationFonts.ResourcePath);
            fonts.ValidateChineseCoverage(LocalizationTable.FromCsv(File.ReadAllText("Docs/LocalizationTemplate.csv")));
            CollectionAssert.AreEqual(before, font.characterTable.Select(character => character.unicode).ToArray());
        }

        [Test]
        public void Unsupported_remote_text_keeps_bundled_chinese_available()
        {
            var fonts = Resources.Load<LocalizationFonts>(LocalizationFonts.ResourcePath);
            var fallback = LocalizationTable.FromJson(Resources.Load<TextAsset>("Localization/fallback").text);
            var remote = LocalizationTable.FromCsv("key,zh-CN\nmenu.levels,\u6C49");
            var settings = new SettingsService(new ChineseSettingsRuntime());
            using var service = new LocalizationService(settings, fallback, new ValidatingSource(fonts, remote));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Localization: remote translations unavailable;.*U\\+6C49"));
            service.InitializeAsync(default).GetAwaiter().GetResult();
            Assert.IsTrue(fallback.TryGet("menu.levels", "zh-CN", out var expected));
            Assert.AreEqual(expected, service.Get("menu.levels"));
            Assert.IsInstanceOf<FormatException>(service.LastLoadError);
        }

        private sealed class ValidatingSource : ILocalizationSource
        {
            private readonly LocalizationFonts _fonts;
            private readonly LocalizationTable _table;

            public ValidatingSource(LocalizationFonts fonts, LocalizationTable table)
            {
                _fonts = fonts;
                _table = table;
            }

            public UniTask<LocalizationTable> LoadAsync(System.Threading.CancellationToken cancellationToken)
            {
                _fonts.ValidateChineseCoverage(_table);
                return UniTask.FromResult(_table);
            }
        }

        private sealed class ChineseSettingsRuntime : ISettingsRuntime
        {
            public SettingsPack Pack => SettingsPack.Mobile;
            public ResolutionOption AppliedResolution => default;
            public int AppliedFps => 60;
            public System.Collections.Generic.IReadOnlyList<ResolutionOption> Resolutions => Array.Empty<ResolutionOption>();
            public System.Collections.Generic.IReadOnlyList<LanguageOption> Languages => Array.Empty<LanguageOption>();
            public int CursorCount => 0;
            public int CursorIndex => 0;
            public int TargetFps => 60;
            public int MaximumFps => 120;
            public ResolutionOption Resolution => default;
            public WindowMode WindowMode => WindowMode.Windowed;
            public string CurrentLanguage => "zh-CN";
            public float GetVolume(VolumeChannel channel) => 1;
            public bool GetToggle(PreferenceToggle toggle) => false;
            public void SetVolume(VolumeChannel channel, float value) { }
            public void SetToggle(PreferenceToggle toggle, bool value) { }
            public void SetTargetFps(int value) { }
            public void SetCursor(int index) { }
            public void SetLanguage(LanguageOption language) { }
            public void SetDisplay(ResolutionOption resolution, WindowMode mode) { }
            public void ResetSetting(SettingId setting) { }
        }
    }
}
