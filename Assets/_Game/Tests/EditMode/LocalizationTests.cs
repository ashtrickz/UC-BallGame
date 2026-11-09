using Slime.Foundation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Slime.Infrastructure.Localization;
using Slime.Localization;
using Slime.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace Slime.Tests.EditMode
{
    public sealed class LocalizationTests
    {
        private SettingsService _settings;

        [SetUp]
        public void SetUp() => _settings = new SettingsService(new SettingsRuntime());

        private static LocalizationTable Fallback() => LocalizationTable.FromJson(
            "{\"languages\":[{\"code\":\"en\",\"entries\":[" +
            "{\"key\":\"hello\",\"value\":\"Hello\"},{\"key\":\"only.en\",\"value\":\"English fallback\"}]}," +
            Fixture(0));

        [Test]
        public void Json_is_available_before_initialization_and_unknown_keys_are_readable()
        {
            using var service = new LocalizationService(_settings, Fallback());
            Assert.AreEqual("Hello", service.Get("hello"));
            Assert.AreEqual("missing.key", service.Get("missing.key"));
            Assert.AreEqual(string.Empty, service.Get(null));
            Assert.AreEqual(string.Empty, service.Get(string.Empty));
            _settings.SetLanguage(new LanguageOption("uk", "uk", Fixture(1)));
            Assert.AreEqual(Fixture(2), service.Get("hello"));
            Assert.AreEqual("English fallback", service.Get("only.en"));
        }

        [Test]
        public void Sheets_override_json_but_missing_cells_fall_back_in_the_selected_language_first()
        {
            var primary = new Source { Table = LocalizationTable.FromCsv("key,en,uk\nhello,Remote hello,\nremote,Remote default,\nonly.en,Remote fallback,") };
            using var service = new LocalizationService(_settings, Fallback(), primary);
            var changes = 0;
            service.Changed += () => changes++;
            service.InitializeAsync(default).GetAwaiter().GetResult();
            Assert.AreEqual("Remote hello", service.Get("hello"));
            Assert.AreEqual(1, changes);
            _settings.SetLanguage(new LanguageOption("uk", "uk", Fixture(1)));
            Assert.AreEqual(Fixture(2), service.Get("hello"));
            Assert.AreEqual("Remote default", service.Get("remote"));
            Assert.AreEqual("Remote fallback", service.Get("only.en"));
            Assert.AreEqual(2, changes);
            Assert.IsNull(service.LastLoadError);
        }

        [Test]
        public void Unavailable_remote_keeps_json_and_does_not_fail_initialization()
        {
            var exception = new IOException("offline");
            var primary = new Source { Error = exception };
            using var service = new LocalizationService(_settings, Fallback(), primary);
            LogAssert.Expect(LogType.Warning, "Localization: remote translations unavailable; using bundled JSON. offline");
            service.InitializeAsync(default).GetAwaiter().GetResult();
            Assert.AreEqual("Hello", service.Get("hello"));
            Assert.AreSame(exception, service.LastLoadError);
        }

        [Test]
        public void Cancellation_does_not_replace_data_or_become_a_remote_failure()
        {
            using var cancellation = new CancellationTokenSource();
            var pending = new UniTaskCompletionSource<LocalizationTable>();
            var primary = new Source { Pending = pending };
            using var service = new LocalizationService(_settings, Fallback(), primary);
            var task = service.InitializeAsync(cancellation.Token);
            cancellation.Cancel();
            pending.TrySetResult(LocalizationTable.FromCsv("key,en\nhello,Cancelled"));
            Assert.Throws<OperationCanceledException>(() => task.GetAwaiter().GetResult());
            Assert.AreEqual("Hello", service.Get("hello"));
            Assert.IsNull(service.LastLoadError);
        }

        [Test]
        public void Settings_updates_notify_only_on_language_changes_and_disposal_unsubscribes()
        {
            var service = new LocalizationService(_settings, Fallback());
            var changes = 0;
            service.Changed += () => changes++;
            _settings.SetVolume(VolumeChannel.Master, 0.4f);
            Assert.AreEqual(0, changes);
            _settings.SetLanguage(new LanguageOption("uk", "uk", Fixture(1)));
            Assert.AreEqual(1, changes);
            service.Dispose();
            _settings.ResetAll();
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void Csv_handles_bom_quotes_commas_crlf_and_multiline_unicode()
        {
            var table = LocalizationTable.FromCsv(Fixture(3));
            Assert.IsTrue(table.TryGet("hello", "en", out var english));
            Assert.AreEqual("Hello, \"player\"", english);
            Assert.IsTrue(table.TryGet("hello", "ZH-cn", out var chinese));
            Assert.AreEqual(Fixture(4), chinese);
            Assert.IsFalse(table.TryGet("short", "zh-CN", out _));
        }

        [TestCase("")]
        [TestCase("<html>Sign in</html>")]
        [TestCase("key,en\nhello,\"unfinished")]
        [TestCase("key,en\nhello,\"closed\"invalid")]
        [TestCase("key,en,EN\nhello,a,b")]
        [TestCase("key,en\nhello,first\nhello,second")]
        [TestCase("key,en\nhello,value,unexpected")]
        [TestCase("key,en\nhello,")]
        public void Invalid_csv_is_rejected_instead_of_replacing_valid_translations(string csv) =>
            Assert.Throws<FormatException>(() => LocalizationTable.FromCsv(csv));

        [TestCase("{}")]
        [TestCase("{\"languages\":[]}")]
        [TestCase("{\"languages\":[{\"code\":\"en\",\"entries\":[{\"key\":\"x\",\"value\":\"a\"},{\"key\":\"x\",\"value\":\"b\"}]}]}")]
        public void Invalid_json_is_rejected(string json) => Assert.Throws<FormatException>(() => LocalizationTable.FromJson(json));

        [Test]
        public void Missing_sheets_url_runs_offline_without_a_request()
        {
            Assert.IsNull(new GoogleSheetsLocalizationSource("").LoadAsync(default).GetAwaiter().GetResult());
        }

        [Test]
        public void Shipped_fallback_and_sheet_template_match_for_all_four_languages()
        {
            var local = LocalizationTable.FromJson(Resources.Load<TextAsset>("Localization/fallback").text);
            var csv = File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "../Docs/LocalizationTemplate.csv"));
            var remote = LocalizationTable.FromCsv(csv);
            foreach (var language in new[] { "en", "uk", "es", "zh-CN" })
                foreach (var key in new[] { "menu.levels", "menu.wall", "common.back", "common.close", "common.reset",
                         "settings.title", "settings.language" })
                {
                    Assert.IsTrue(local.TryGet(key, language, out var value), $"{key}/{language}");
                    Assert.IsTrue(remote.TryGet(key, language, out var sheetValue), $"{key}/{language}");
                    Assert.AreEqual(value, sheetValue);
                }
        }

        [Test]
        public void Localized_component_is_injected_and_updates_with_the_language()
        {
            using var service = new LocalizationService(_settings, Fallback());
            var builder = new ContainerBuilder();
            builder.RegisterInstance<ILocalizationService>(service);
            using var container = builder.Build();
            var go = new GameObject("Localized text", typeof(RectTransform), typeof(TextMeshProUGUI));
            try
            {
                var label = go.GetComponent<TMP_Text>();
                var localized = go.AddComponent<LocalizedText>();
                localized.SetKey("hello");
                container.Inject(localized);
                Assert.AreEqual("Hello", label.text);
                _settings.SetLanguage(new LanguageOption("uk", "uk", Fixture(1)));
                Assert.AreEqual(Fixture(2), label.text);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        /// <summary>Unicode test data kept outside C# sources.</summary>
        [Serializable]
        private sealed class UnicodeFixture
        {
            public string[] values = Array.Empty<string>();
        }

        private static string Fixture(int index)
        {
            var path = Path.Combine(UnityEngine.Application.dataPath, "_Game/Tests/EditMode/LocalizationUnicodeFixture.json");
            return JsonUtility.FromJson<UnicodeFixture>(File.ReadAllText(path)).values[index];
        }

        private sealed class Source : ILocalizationSource
        {
            public LocalizationTable Table;
            public Exception Error;
            public UniTaskCompletionSource<LocalizationTable> Pending;
            public UniTask<LocalizationTable> LoadAsync(CancellationToken cancellationToken)
            {
                if (Pending != null)
                    return Pending.Task;
                return Error == null ? UniTask.FromResult(Table) : UniTask.FromException<LocalizationTable>(Error);
            }
        }

        private sealed class SettingsRuntime : ISettingsRuntime
        {
            public SettingsPack Pack => SettingsPack.Mobile;
            public ResolutionOption AppliedResolution => default;
            public int AppliedFps => 60;
            public IReadOnlyList<ResolutionOption> Resolutions => Array.Empty<ResolutionOption>();
            public IReadOnlyList<LanguageOption> Languages
            {
                get;
            } = new[] {
                new LanguageOption("en", "en", "English"), new LanguageOption("uk", "uk", Fixture(1)) };
            public int CursorCount => 0;
            public int CursorIndex => 0;
            public int TargetFps => 60;
            public int MaximumFps => 120;
            public ResolutionOption Resolution => default;
            public WindowMode WindowMode => WindowMode.Windowed;
            public string CurrentLanguage { get; private set; } = "en";
            public float GetVolume(VolumeChannel channel) => 1;
            public bool GetToggle(PreferenceToggle toggle) => false;
            public void SetVolume(VolumeChannel channel, float value)
            {
            }
            public void SetToggle(PreferenceToggle toggle, bool value)
            {
            }
            public void SetTargetFps(int value)
            {
            }
            public void SetCursor(int index)
            {
            }
            public void SetLanguage(LanguageOption language) => CurrentLanguage = language.Name;
            public void SetDisplay(ResolutionOption resolution, WindowMode mode)
            {
            }
            public void ResetSetting(SettingId setting)
            {
                if (setting == SettingId.Language)
                    CurrentLanguage = "en";
            }
        }
    }
}
