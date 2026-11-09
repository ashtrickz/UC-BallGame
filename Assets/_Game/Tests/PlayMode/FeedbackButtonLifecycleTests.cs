using System;
using System.Collections;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Game.App.Game;
using Game.App.Loading;
using Game.App.Scenes;
using Game.Audio;
using Game.Integration.Composition;
using Game.Integration.Game;
using Game.Integration.Gameplay;
using Game.Integration.Menu;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;

namespace Game.Foundation.Tests
{
    public sealed class FeedbackButtonLifecycleTests
    {
        private static void AssertFeedback(GameObject root, IAudioService audio)
        {
            var buttons = root.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            Assert.IsNotEmpty(buttons, root.name);
            var dependency = typeof(FeedbackButton).GetField("_audio", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var button in buttons)
            {
                Assert.IsInstanceOf<FeedbackButton>(button, button.name + " must use shared feedback.");
                Assert.AreSame(audio, dependency.GetValue(button), button.name + " must receive the scene's audio service.");
            }
        }

        [UnityTest]
        public IEnumerator Loading_menu_dynamic_cells_popups_and_gameplay_receive_button_audio() => UniTask.ToCoroutine(async () =>
        {
            var testScene = SceneManager.GetActiveScene();
            var fps = UnityEngine.Application.targetFrameRate;
            var vsync = QualitySettings.vSyncCount;
            try
            {
                void OnLoaded(Scene scene, LoadSceneMode mode) { if (scene.name == "Loading") SceneManager.SetActiveScene(scene); }
                SceneManager.sceneLoaded += OnLoaded;
                try { await SceneManager.LoadSceneAsync("Loading", LoadSceneMode.Additive).ToUniTask(); }
                finally { SceneManager.sceneLoaded -= OnLoaded; }
                var scope = ProjectScope.Instance;
                Assert.IsNotNull(scope);
                var audio = scope.Container.Resolve<IAudioService>();
                AssertFeedback(scope.gameObject, audio);
                await UniTask.WaitUntil(() => scope.Container.Resolve<IInitializationModel>().IsComplete
                        && UnityEngine.Object.FindFirstObjectByType<MenuView>() != null)
                    .Timeout(TimeSpan.FromSeconds(30));

                var menu = UnityEngine.Object.FindFirstObjectByType<MenuView>();
                AssertFeedback(menu.gameObject, audio);
                Assert.IsNotEmpty(menu.GetComponentsInChildren<SlimeColorSwatch>(true));
                var levels = UnityEngine.Object.FindFirstObjectByType<LevelSelectView>(FindObjectsInactive.Include);
                levels.SetLevels(3, 2);
                Assert.AreEqual(3, levels.Cells.Count);
                AssertFeedback(levels.gameObject, audio);

                var popupTask = scope.Container.Resolve<IPopupService>().ShowAsync<SettingsPopupController>();
                await UniTask.WaitUntil(() => UnityEngine.Object.FindFirstObjectByType<SettingsPopupView>() != null)
                    .Timeout(TimeSpan.FromSeconds(5));
                var popup = UnityEngine.Object.FindFirstObjectByType<SettingsPopupView>();
                AssertFeedback(popup.gameObject, audio);
                popup.transform.Find("RootTransform/Close").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                await popupTask;

                scope.Container.Resolve<GameSession>().StartRound(GameMode.Wall);
                await scope.Container.Resolve<GameNavigation>().OpenAsync(GameNavigation.GameScene);
                var gameScope = UnityEngine.Object.FindFirstObjectByType<GameScope>();
                await gameScope.Ready;
                var game = gameScope.Container.Resolve<GameplayController>();
                AssertFeedback(UnityEngine.Object.FindFirstObjectByType<GameView>().gameObject, audio);
                AssertFeedback(gameScope.Container.Resolve<ResultView>().gameObject, audio);
                AssertFeedback(game.Stage.Tutorial.gameObject, audio);
            }
            finally
            {
                if (testScene.IsValid() && testScene.isLoaded) SceneManager.SetActiveScene(testScene);
                if (ProjectScope.Instance != null) UnityEngine.Object.Destroy(ProjectScope.Instance.gameObject);
                await UniTask.Yield();
                foreach (var name in new[] { "Menu", "Game", "Loading", "Transit" })
                    if (SceneManager.GetSceneByName(name).isLoaded) await SceneManager.UnloadSceneAsync(name).ToUniTask();
                UnityEngine.Application.targetFrameRate = fps;
                QualitySettings.vSyncCount = vsync;
            }
        });
    }
}
