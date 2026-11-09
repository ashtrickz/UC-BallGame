using System;
using System.Collections;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.App.Game;
using Game.App.Loading;
using Game.App.Scenes;
using Game.Integration.Composition;
using Game.Integration.Game;
using Game.Integration.Gameplay;
using Game.Integration.Localization;
using Game.Integration.Menu;
using Game.Integration.Settings;
using Game.Localization;
using Game.Settings;
using Game.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;

namespace Game.Foundation.Tests
{
    public sealed class LocalizedGameUiTests
    {
        [UnityTest]
        public IEnumerator Language_buttons_update_menu_hud_tutorial_and_result_without_restarting() => UniTask.ToCoroutine(async () =>
        {
            var savedLanguage = PlayerPrefs.HasKey("Settings.Language") ? PlayerPrefs.GetString("Settings.Language") : null;
            var savedProgress = PlayerPrefs.HasKey(PlayerPrefsCampaignProgress.Key) ? PlayerPrefs.GetInt(PlayerPrefsCampaignProgress.Key) : (int?)null;
            var fps = UnityEngine.Application.targetFrameRate;
            var vsync = QualitySettings.vSyncCount;
            var testScene = SceneManager.GetActiveScene();
            PlayerPrefs.SetString("Settings.Language", "en");
            PlayerPrefs.SetInt(PlayerPrefsCampaignProgress.Key, 2);
            try
            {
                void OnLoaded(Scene scene, LoadSceneMode mode) { if (scene.name == "Loading") SceneManager.SetActiveScene(scene); }
                SceneManager.sceneLoaded += OnLoaded;
                try { await SceneManager.LoadSceneAsync("Loading", LoadSceneMode.Additive).ToUniTask(); }
                finally { SceneManager.sceneLoaded -= OnLoaded; }
                await UniTask.WaitUntil(() => ProjectScope.Instance != null && ProjectScope.Instance.Container.Resolve<IInitializationModel>().IsComplete)
                    .Timeout(TimeSpan.FromSeconds(30));
                var container = ProjectScope.Instance.Container;
                var settings = container.Resolve<ISettingsService>();
                var localization = container.Resolve<ILocalizationService>();
                var menu = UnityEngine.Object.FindFirstObjectByType<MenuView>();
                var popupTask = container.Resolve<IPopupService>().ShowAsync<SettingsPopupController>();
                await UniTask.WaitUntil(() => UnityEngine.Object.FindFirstObjectByType<SettingsPopupView>() != null).Timeout(TimeSpan.FromSeconds(5));
                var popup = UnityEngine.Object.FindFirstObjectByType<SettingsPopupView>();
                foreach (var code in new[] { "uk", "es", "zh-CN", "en" })
                {
                    var caption = popup.GetComponentsInChildren<LocalizedText>(true).Single(label => label.Key == "language." + code);
                    caption.GetComponentInParent<UnityEngine.UI.Button>().onClick.Invoke();
                    await UniTask.Yield();
                    Assert.AreEqual(code, settings.CurrentLanguage);
                    Assert.AreEqual(code, PlayerPrefs.GetString("Settings.Language"));
                    Assert.AreEqual(localization.Get("settings.title"), Label(popup.gameObject, "settings.title").text);
                    Assert.AreEqual(localization.Get("menu.levels"), Label(menu.gameObject, "menu.levels").text);
                    AssertNoMissingGlyphs(popup.gameObject);
                }
                Assert.AreEqual(settings.CurrentLanguage, new UnitySettingsRuntime(SettingsPack.Mobile).CurrentLanguage);
                popup.transform.Find("RootTransform/Close").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                await popupTask;

                Select(settings, "uk");
                var navigation = container.Resolve<GameNavigation>();
                container.Resolve<GameSession>().StartRound(GameMode.Campaign, 2);
                await navigation.OpenAsync(GameNavigation.GameScene);
                var gameScope = UnityEngine.Object.FindFirstObjectByType<GameScope>();
                await gameScope.Ready;
                var gameplay = gameScope.Container.Resolve<GameplayController>();
                var context = gameplay.Current;
                await UniTask.Yield();
                var labels = gameScope.Container.Resolve<GameView>().DynamicTextLabels.ToArray();
                var resultView = gameScope.Container.Resolve<ResultView>();
                var resultLabels = resultView.DynamicTextLabels.ToArray();
                Assert.AreEqual(localization.Format("hud.level", 2), labels[1].text);
                Assert.AreEqual(localization.Format("hud.size", 100), labels[0].text);
                Select(settings, "es");
                Assert.AreSame(context, gameplay.Current);
                Assert.AreEqual(localization.Format("hud.level", 2), labels[1].text);

                gameplay.PlayCampaignLevel(1);
                await UniTask.WaitUntil(() => gameplay.Tutorial != null && gameplay.Tutorial.Step == "tour").Timeout(TimeSpan.FromSeconds(10));
                var tutorial = gameplay.Tutorial;
                Select(settings, "zh-CN");
                Assert.AreEqual(localization.Get("tutorial.tour"), gameplay.Stage.Tutorial.Message);
                Assert.AreSame(tutorial, gameplay.Tutorial);
                Assert.AreEqual("tour", tutorial.Step);
                gameplay.SkipTutorial();
                await UniTask.WaitUntil(() => !tutorial.IsRunning).Timeout(TimeSpan.FromSeconds(5));
                await GameplayAutoplay.PlayAsync(gameplay, AutoplayMode.Overcharge, CancellationToken.None).Timeout(TimeSpan.FromSeconds(30));
                await UniTask.WaitUntil(() => !resultView.IsAnimating).Timeout(TimeSpan.FromSeconds(5));
                Assert.AreEqual(localization.Get("result.pop"), resultLabels[0].text);
                Assert.AreEqual(localization.Get("result.overcharge"), resultLabels[1].text);
                context = gameplay.Current;
                Select(settings, "uk");
                Assert.AreEqual(localization.Get("result.pop"), resultLabels[0].text);
                Assert.AreEqual(localization.Get("result.overcharge"), resultLabels[1].text);
                Assert.AreSame(context, gameplay.Current);
                Assert.IsFalse(resultView.IsAnimating);
                AssertNoMissingGlyphs(gameScope.Container.Resolve<GameView>().gameObject);
                AssertNoMissingGlyphs(resultView.gameObject);
            }
            finally
            {
                if (testScene.IsValid() && testScene.isLoaded) SceneManager.SetActiveScene(testScene);
                if (ProjectScope.Instance != null) UnityEngine.Object.Destroy(ProjectScope.Instance.gameObject);
                await UniTask.Yield();
                foreach (var name in new[] { "Menu", "Game", "Loading", "Transit" })
                    if (SceneManager.GetSceneByName(name).isLoaded) await SceneManager.UnloadSceneAsync(name).ToUniTask();
                if (savedLanguage == null) PlayerPrefs.DeleteKey("Settings.Language"); else PlayerPrefs.SetString("Settings.Language", savedLanguage);
                if (savedProgress.HasValue) PlayerPrefs.SetInt(PlayerPrefsCampaignProgress.Key, savedProgress.Value); else PlayerPrefs.DeleteKey(PlayerPrefsCampaignProgress.Key);
                PlayerPrefs.Save();
                UnityEngine.Application.targetFrameRate = fps;
                QualitySettings.vSyncCount = vsync;
            }
        });

        private static TMP_Text Label(GameObject root, string key) =>
            root.GetComponentsInChildren<LocalizedText>(true).Single(label => label.Key == key).GetComponent<TMP_Text>();

        private static void Select(ISettingsService settings, string code) => settings.SetLanguage(settings.Languages.Single(language => language.Code == code));

        private static void AssertNoMissingGlyphs(GameObject root)
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(false))
                Assert.IsTrue(text.font.HasCharacters(text.text, out uint[] missing, true, true), text.name + ": " + string.Join(",", missing ?? new uint[0]));
        }
    }
}
