using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.App.Loading;
using Game.App.Game;
using Game.App.Scenes;
using Game.Integration.Composition;
using Game.Integration.Gameplay;
using Game.Integration.Menu;
using Game.UI;
using Game.Users;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;

namespace Game.Foundation.Tests
{
    public sealed class FoundationLifecycleTests
    {
        private static UnityEngine.UI.Button AssignedButton(UnityEngine.Object view, string fieldName)
        {
            for (var type = view.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field == null) continue;
                var button = field.GetValue(view) as UnityEngine.UI.Button;
                Assert.IsNotNull(button, fieldName + " must be assigned.");
                return button;
            }
            throw new InvalidOperationException("Missing button field: " + fieldName);
        }

        private static async UniTask ChooseFirstLevelAsync()
        {
            await UniTask.Yield();
            var grid = UnityEngine.Object.FindFirstObjectByType<LevelSelectView>();
            Assert.IsNotNull(grid, "LEVELS shows the level grid");
            await UniTask.WaitUntil(() => grid.GetComponent<CanvasGroup>().interactable)
                .Timeout(TimeSpan.FromSeconds(5));
            Assert.IsTrue(grid.Cells[0].interactable);
            grid.Cells[0].onClick.Invoke();
            await UniTask.WaitUntil(() => grid == null || !grid.gameObject.activeSelf)
                .Timeout(TimeSpan.FromSeconds(5));
        }

        [UnityTest]
        public IEnumerator Menu_and_game_use_separate_scopes_and_share_persistent_services() => UniTask.ToCoroutine(async () =>
        {
            var testScene = SceneManager.GetActiveScene();
            void OnLoaded(Scene scene, LoadSceneMode mode)
            { if (scene.name == "Loading") SceneManager.SetActiveScene(scene); }
            SceneManager.sceneLoaded += OnLoaded;
            try { await SceneManager.LoadSceneAsync("Loading", LoadSceneMode.Additive).ToUniTask(); }
            finally { SceneManager.sceneLoaded -= OnLoaded; }
            try
            {
                await UniTask.WaitUntil(() => ProjectScope.Instance != null &&
                    ProjectScope.Instance.Container.Resolve<IInitializationModel>().IsComplete).Timeout(TimeSpan.FromSeconds(20));
                var root = ProjectScope.Instance;
                Assert.AreEqual(GameNavigation.MenuScene, SceneManager.GetActiveScene().name);
                Assert.IsFalse(SceneManager.GetSceneByName("Loading").isLoaded);
                Assert.IsFalse(SceneManager.GetSceneByName("Game").isLoaded);
                var menuScope = UnityEngine.Object.FindFirstObjectByType<MenuScope>();
                Assert.AreSame(root, menuScope.Parent);
                var users = root.Container.Resolve<IUserRepository>();
                Assert.IsTrue(users.HasProfile);
                Assert.AreSame(users, menuScope.Container.Resolve<IUserRepository>());
                var session = root.Container.Resolve<GameSession>();
                var navigation = root.Container.Resolve<GameNavigation>();
                var menu = UnityEngine.Object.FindFirstObjectByType<MenuView>();
                Assert.AreEqual(GamePhase.Menu, session.Phase);
                Assert.IsTrue(menu.IsVisible);
                Assert.IsNull(UnityEngine.Object.FindFirstObjectByType<global::Game.Integration.Game.GameScope>());
                AssignedButton(menu, "settingsButton").onClick.Invoke();
                await UniTask.Delay(400, ignoreTimeScale: true);
                var popup = UnityEngine.Object.FindFirstObjectByType<SettingsPopupView>();
                Assert.IsNotNull(popup);
                Assert.IsTrue(popup.IsVisible);
                AssignedButton(popup, "closeButton").onClick.Invoke();
                await UniTask.WaitUntil(() => popup == null).Timeout(TimeSpan.FromSeconds(5));
                Assert.IsTrue(popup == null);
                Assert.AreEqual(GamePhase.Menu, session.Phase);

                var play = AssignedButton(menu, "playButton");
                // LEVELS opens the level grid; a repeated click while it is open is ignored; level 1 starts the round.
                play.onClick.Invoke(); play.onClick.Invoke();
                await ChooseFirstLevelAsync();
                await UniTask.WaitUntil(() => !navigation.IsTransitioning).Timeout(TimeSpan.FromSeconds(10));
                Assert.AreEqual(GamePhase.Playing, session.Phase);
                Assert.AreEqual(1, session.RoundNumber);
                Assert.AreEqual(GameNavigation.GameScene, SceneManager.GetActiveScene().name);
                Assert.IsTrue(menu == null);
                Assert.IsTrue(menuScope == null);
                Assert.IsFalse(SceneManager.GetSceneByName(GameNavigation.MenuScene).isLoaded);
                var sceneScope = UnityEngine.Object.FindFirstObjectByType<global::Game.Integration.Game.GameScope>();
                Assert.AreSame(root, sceneScope.Parent);
                Assert.AreSame(session, sceneScope.Container.Resolve<GameSession>());
                Assert.AreSame(users, sceneScope.Container.Resolve<IUserRepository>());
                var game = UnityEngine.Object.FindFirstObjectByType<global::Game.Integration.Game.GameView>();
                Assert.IsTrue(game.IsVisible);

                await navigation.OpenAsync(GameNavigation.MenuScene);
                Assert.IsTrue(sceneScope == null);
                Assert.IsFalse(SceneManager.GetSceneByName(GameNavigation.GameScene).isLoaded);
                menu = UnityEngine.Object.FindFirstObjectByType<MenuView>();
                Assert.IsTrue(menu.IsVisible);
                Assert.AreEqual(GamePhase.Menu, session.Phase);
                AssignedButton(menu, "playButton").onClick.Invoke();
                await ChooseFirstLevelAsync();
                await UniTask.WaitUntil(() => !navigation.IsTransitioning).Timeout(TimeSpan.FromSeconds(10));
                Assert.AreEqual(2, session.RoundNumber);
                game = UnityEngine.Object.FindFirstObjectByType<global::Game.Integration.Game.GameView>();
                var duplicate = new GameObject("DuplicateProjectScope");
                duplicate.AddComponent<ProjectScope>();
                await UniTask.Yield();
                Assert.IsTrue(duplicate == null);
                Assert.AreSame(root, ProjectScope.Instance);
                Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<ProjectScope>(FindObjectsSortMode.None).Length);
                Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length);

                // Exercise the view -> controller route, then destroy the project while its popup is open.
                // Root disposal may precede disposal of the child scene: both cancellation paths must be quiet.
                var gameSettings = AssignedButton(game, "settingsButton");
                Assert.IsNotNull(gameSettings);
                Assert.IsTrue(gameSettings.gameObject.activeInHierarchy);
                Assert.IsTrue(gameSettings.IsInteractable());
                gameSettings.onClick.Invoke();
                await UniTask.Delay(300, ignoreTimeScale: true);
                Assert.IsTrue(UnityEngine.Object.FindFirstObjectByType<SettingsPopupView>().IsVisible);
            }
            finally
            {
                if (testScene.IsValid() && testScene.isLoaded) SceneManager.SetActiveScene(testScene);
                if (ProjectScope.Instance != null) UnityEngine.Object.Destroy(ProjectScope.Instance.gameObject);
                await UniTask.Yield();
                foreach (var name in new[] { "Menu", "Game", "Loading", "Transit" })
                    if (SceneManager.GetSceneByName(name).isLoaded) await SceneManager.UnloadSceneAsync(name).ToUniTask();
            }
        });
    }
}
