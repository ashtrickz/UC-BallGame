using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Slime.App.Game;
using Slime.App.Loading;
using Slime.App.Scenes;
using Slime.Gameplay.Core.Model;
using Slime.Composition;
using Slime.Infrastructure.Frame;
using Slime.Diagnostics;
using Slime.Gameplay.Presentation;
using Slime.Menu;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;

namespace Slime.Gameplay.Tests.PlayMode
{
    /// <summary>The full attempt in the real Game scene: animations, waits, confirmations, pause and restart.</summary>
    public sealed partial class GameplayLifecycleTests
    {
        private const int Seed = 424242;

        private static async UniTask<ProjectScope> StartMenuAsync()
        {
            void OnLoaded(Scene scene, LoadSceneMode mode) { if (scene.name == "Loading") SceneManager.SetActiveScene(scene); }
            SceneManager.sceneLoaded += OnLoaded;
            try { await SceneManager.LoadSceneAsync("Loading", LoadSceneMode.Additive).ToUniTask(); }
            finally { SceneManager.sceneLoaded -= OnLoaded; }
            await UniTask.WaitUntil(() => ProjectScope.Instance != null &&
                ProjectScope.Instance.Container.Resolve<IInitializationModel>().IsComplete).Timeout(TimeSpan.FromSeconds(30));
            Assert.AreEqual(GameNavigation.MenuScene, SceneManager.GetActiveScene().name);
            Assert.IsNull(UnityEngine.Object.FindFirstObjectByType<GameScope>());
            return ProjectScope.Instance;
        }

        private static async UniTask<GameplayHandle> StartGameAsync()
        {
            var project = await StartMenuAsync();
            project.Container.Resolve<GameSession>().StartRound();
            await project.Container.Resolve<GameNavigation>().OpenAsync(GameNavigation.GameScene);
            return await LoadedGameplayAsync();
        }

        private static async UniTask<GameplayHandle> LoadedGameplayAsync()
        {
            await UniTask.WaitUntil(() => !ProjectScope.Instance.Container.Resolve<GameNavigation>().IsTransitioning)
                .Timeout(TimeSpan.FromSeconds(10));
            await UniTask.WaitUntil(() => UnityEngine.Object.FindFirstObjectByType<GameScope>() != null).Timeout(TimeSpan.FromSeconds(10));
            var scope = UnityEngine.Object.FindFirstObjectByType<GameScope>();
            await scope.Ready.Timeout(TimeSpan.FromSeconds(10));
            var gameplay = new GameplayHandle(scope.Container);
            gameplay.TutorialEnabled = false;
            gameplay.SkipTutorial();
            Assert.IsNotNull(gameplay.Current, "the first attempt exists when the scene is ready");
            Assert.AreEqual(PauseReason.None, gameplay.Pause);
            return gameplay;
        }

        private static int? _savedProgress;
        private static string _savedColor;

        [SetUp]
        public void SaveProgress()
        {
            _savedProgress = PlayerPrefs.HasKey(PlayerPrefsCampaignProgress.Key) ? PlayerPrefs.GetInt(PlayerPrefsCampaignProgress.Key) : (int?)null;
            _savedColor = PlayerPrefs.HasKey(PlayerPrefsSlimeColorPreferences.Key) ? PlayerPrefs.GetString(PlayerPrefsSlimeColorPreferences.Key) : null;
        }

        [TearDown]
        public void RestoreProgress()
        {
            if (_savedProgress.HasValue) PlayerPrefs.SetInt(PlayerPrefsCampaignProgress.Key, _savedProgress.Value);
            else PlayerPrefs.DeleteKey(PlayerPrefsCampaignProgress.Key);
            if (_savedColor != null) PlayerPrefs.SetString(PlayerPrefsSlimeColorPreferences.Key, _savedColor);
            else PlayerPrefs.DeleteKey(PlayerPrefsSlimeColorPreferences.Key);
            PlayerPrefs.Save();
        }

        private static async UniTask TearDownAsync()
        {
            Time.timeScale = 1;
            if (ProjectScope.Instance != null) UnityEngine.Object.Destroy(ProjectScope.Instance.gameObject);
            await UniTask.Yield();
            foreach (var name in new[] { "Menu", "Game", "Loading", "Transit" })
                if (SceneManager.GetSceneByName(name).isLoaded) await SceneManager.UnloadSceneAsync(name).ToUniTask();
        }

        private static UniTask WaitPhase(AttemptModel model, AttemptPhase phase, float seconds = 20) =>
            UniTask.WaitUntil(() => model.Phase == phase).Timeout(TimeSpan.FromSeconds(seconds));

        [UnityTest]
        public IEnumerator Slime_colors_update_preview_and_survive_game_restart_and_return_to_menu() => UniTask.ToCoroutine(async () =>
        {
            void CheckColors(Slime.Gameplay.View.SlimeView slime, Slime.Gameplay.Config.SlimeColorPreset preset)
            {
                var count = 0;
                var block = new MaterialPropertyBlock();
                foreach (var renderer in slime.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (renderer.transform.parent != slime.transform || !renderer.sharedMaterial.HasProperty("_TopColor")) continue;
                    renderer.GetPropertyBlock(block);
                    Assert.Less(Vector4.Distance(preset.BaseColor, block.GetColor("_BaseColor")), 1e-5f, renderer.name + " base");
                    Assert.Less(Vector4.Distance(preset.TopColor, block.GetColor("_TopColor")), 1e-5f, renderer.name + " top");
                    count++;
                }
                Assert.AreEqual(3, count, "body, drop and neck all use the preset");
            }

            try
            {
                var project = await StartMenuAsync();
                var config = UnityEngine.Object.FindFirstObjectByType<MenuScope>().Container.Resolve<Slime.Gameplay.Config.SlimeGameConfig>();
                var menu = UnityEngine.Object.FindFirstObjectByType<MenuView>();
                var swatches = menu.GetComponentsInChildren<SlimeColorSwatch>();
                Assert.AreEqual(config.SlimeColors.Count, swatches.Length);
                var preview = UnityEngine.Object.FindFirstObjectByType<Slime.Gameplay.View.SlimeView>();
                var material = preview.transform.Find("Body").GetComponent<MeshRenderer>().sharedMaterial;
                var originalBase = material.GetColor("_BaseColor");
                var originalTop = material.GetColor("_TopColor");
                for (var i = 0; i < swatches.Length; i++)
                {
                    swatches[i].GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                    CheckColors(preview, config.SlimeColors[i]);
                }
                Assert.AreEqual(originalBase, material.GetColor("_BaseColor"), "shared material stays unchanged");
                Assert.AreEqual(originalTop, material.GetColor("_TopColor"));
                var selected = config.SlimeColors[swatches.Length - 1];
                project.Container.Resolve<GameSession>().StartRound(GameMode.Wall);
                var navigation = project.Container.Resolve<GameNavigation>();
                await navigation.OpenAsync(GameNavigation.GameScene);
                var gameplay = await LoadedGameplayAsync();
                CheckColors(gameplay.Stage.Slime, selected);
                gameplay.Restart();
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                CheckColors(gameplay.Stage.Slime, selected);
                project.Container.Resolve<GameSession>().ReturnToMenu();
                await navigation.OpenAsync(GameNavigation.MenuScene);
                CheckColors(UnityEngine.Object.FindFirstObjectByType<Slime.Gameplay.View.SlimeView>(), selected);
                Assert.AreEqual(swatches.Length - 1, new PlayerPrefsSlimeColorPreferences().SelectedIndex(config.SlimeColors));
            }
            finally { await TearDownAsync(); }
        });

        [UnityTest]
        public IEnumerator Control_plan_reaches_the_doors_through_the_real_presentation() => UniTask.ToCoroutine(async () =>
        {
            try
            {
                var gameplay = await StartGameAsync();
                gameplay.RestartWithSeed(Seed);
                var context = gameplay.Current;
                Assert.AreEqual(Seed, context.Seed);
                Time.timeScale = 4;
                var result = await GameplayAutoplay.PlayAsync(gameplay, AutoplayMode.ControlPlan, CancellationToken.None)
                    .Timeout(TimeSpan.FromSeconds(90));
                Assert.AreEqual(AttemptPhase.Won, result);
                Assert.IsTrue(context.Model.DoorsOpen);
                Assert.IsTrue(gameplay.Stage.Doors.IsOpen);
                Assert.AreEqual(context.Level.Control.TotalCost, context.Model.TotalSpent, 1e-9,
                    "the real presentation replays the validated control plan exactly");
                Assert.LessOrEqual(context.Model.TotalSpent, context.Rules.ControlBudgetLimit + context.Rules.Tolerance);
                Assert.AreEqual(context.Level.Control.HoldSeconds.Count, context.Model.ShotsFired);
            }
            finally { await TearDownAsync(); }
        });

        [UnityTest]
        public IEnumerator Holding_to_the_critical_size_is_a_defeat_that_ends_with_a_pop() => UniTask.ToCoroutine(async () =>
        {
            try
            {
                var gameplay = await StartGameAsync();
                gameplay.RestartWithSeed(Seed);
                var model = gameplay.Current.Model;
                Time.timeScale = 4;
                var result = await GameplayAutoplay.PlayAsync(gameplay, AutoplayMode.Overcharge, CancellationToken.None).Timeout(TimeSpan.FromSeconds(60));
                Assert.AreEqual(AttemptPhase.Lost, result);
                Assert.AreEqual(DefeatReason.Overcharge, model.Defeat);
                Assert.AreEqual(model.Rules.Dcrit, model.Diameter, 1e-9, "nothing is refunded");
                Assert.AreEqual(0, model.ShotsFired);
            }
            finally { await TearDownAsync(); }
        });

        [UnityTest]
        public IEnumerator Running_out_of_resource_ends_with_the_final_hop_and_pop() => UniTask.ToCoroutine(async () =>
        {
            try
            {
                var gameplay = await StartGameAsync();
                gameplay.RestartWithSeed(Seed);
                var model = gameplay.Current.Model;
                Time.timeScale = 4;
                var result = await GameplayAutoplay.PlayAsync(gameplay, AutoplayMode.Starve, CancellationToken.None).Timeout(TimeSpan.FromSeconds(90));
                Assert.AreEqual(AttemptPhase.Lost, result);
                Assert.AreEqual(DefeatReason.OutOfResource, model.Defeat);
                Assert.IsFalse(model.Rules.CanAffordMinShot(model.Diameter));
            }
            finally { await TearDownAsync(); }
        });

        [UnityTest]
        public IEnumerator Restart_creates_a_new_attempt_isolated_from_old_completions() => UniTask.ToCoroutine(async () =>
        {
            try
            {
                var gameplay = await StartGameAsync();
                gameplay.PlayEndless();
                var old = gameplay.Current;
                await WaitPhase(old.Model, AttemptPhase.Waiting);
                var input = gameplay.Stage.Input;
                input.SetVirtualPointer(true);
                await WaitPhase(old.Model, AttemptPhase.Charging);
                await UniTask.Delay(250);
                input.SetVirtualPointer(false);
                await WaitPhase(old.Model, AttemptPhase.Shooting, 2);
                input.SetVirtualPointer(null);

                gameplay.Restart();
                var fresh = gameplay.Current;
                Assert.AreNotSame(old, fresh);
                Assert.AreNotEqual(old.Id, fresh.Id);
                Assert.AreNotEqual(old.Seed, fresh.Seed, "a normal restart picks a new seed");
                Assert.IsTrue(old.Token.IsCancellationRequested);
                await WaitPhase(fresh.Model, AttemptPhase.Waiting);
                await UniTask.Delay(1500);
                Assert.AreEqual(AttemptPhase.Shooting, old.Model.Phase, "the cancelled flight never confirms into the old model");
                Assert.AreEqual(0, fresh.Model.ShotsFired);
                Assert.AreEqual(fresh.Level.Crystals.Count, fresh.Model.AliveCount);
                Assert.AreEqual(fresh.Rules.D0, fresh.Model.Diameter);

                // Rapid repeated restarts leave exactly the last attempt running.
                for (var i = 0; i < 5; i++) gameplay.Restart();
                var last = gameplay.Current;
                await WaitPhase(last.Model, AttemptPhase.Waiting);
                Assert.AreEqual(fresh.Id + 5, last.Id);
            }
            finally { await TearDownAsync(); }
        });

        [UnityTest]
        public IEnumerator Pause_returns_an_unreleased_charge_and_freezes_a_released_shot() => UniTask.ToCoroutine(async () =>
        {
            try
            {
                var gameplay = await StartGameAsync();
                var model = gameplay.Current.Model;
                var input = gameplay.Stage.Input;
                await WaitPhase(model, AttemptPhase.Waiting);
                var before = model.Diameter;

                input.SetVirtualPointer(true);
                await WaitPhase(model, AttemptPhase.Charging);
                await UniTask.Delay(400);
                Assert.Less(model.Diameter, before);
                gameplay.SetPaused(PauseReason.Settings, true);
                Assert.AreEqual(AttemptPhase.Waiting, model.Phase);
                Assert.AreEqual(before, model.Diameter, "exact refund");
                gameplay.SetPaused(PauseReason.Background, true);
                gameplay.SetPaused(PauseReason.Settings, false);
                Assert.AreEqual(PauseReason.Background, gameplay.Pause, "one remaining reason keeps the pause");
                gameplay.SetPaused(PauseReason.Background, false);
                await UniTask.Delay(300);
                Assert.AreEqual(AttemptPhase.Waiting, model.Phase, "a finger still down after the pause does not charge");
                Assert.AreEqual(before, model.Diameter);

                input.SetVirtualPointer(false);
                await UniTask.Yield();
                await UniTask.Yield();
                input.SetVirtualPointer(true);
                await WaitPhase(model, AttemptPhase.Charging, 2);
                input.SetVirtualPointer(false);
                await WaitPhase(model, AttemptPhase.Shooting, 2);
                var spent = model.Diameter;
                gameplay.SetPaused(PauseReason.Settings, true);
                await UniTask.Delay(1200);
                Assert.AreEqual(AttemptPhase.Shooting, model.Phase, "the flight waits while paused");
                Assert.AreEqual(spent, model.Diameter, "a released shot stays spent");
                gameplay.SetPaused(PauseReason.Settings, false);
                await UniTask.WaitUntil(() => model.Phase != AttemptPhase.Shooting).Timeout(TimeSpan.FromSeconds(5));
                input.SetVirtualPointer(null);
            }
            finally { await TearDownAsync(); }
        });

        [UnityTest]
        public IEnumerator Wall_mode_is_chosen_in_the_menu_won_and_left_back_to_the_menu() => UniTask.ToCoroutine(async () =>
        {
            try
            {
                var project = await StartMenuAsync();
                var session = project.Container.Resolve<GameSession>();
                var navigation = project.Container.Resolve<GameNavigation>();
                var menu = UnityEngine.Object.FindFirstObjectByType<MenuView>();
                ((UnityEngine.UI.Button)typeof(MenuView).GetField("wallButton",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(menu)).onClick.Invoke();
                var gameplay = await LoadedGameplayAsync();
                var wall = gameplay.Current;
                Assert.IsInstanceOf<Slime.Gameplay.Core.Levels.WallLevelRequest>(wall.Request);
                Assert.Greater(wall.Level.Crystals.Count, 200);

                Time.timeScale = 4;
                Assert.AreEqual(AttemptPhase.Won, await GameplayAutoplay.PlayAsync(gameplay, AutoplayMode.ControlPlan, CancellationToken.None)
                    .Timeout(TimeSpan.FromSeconds(90)));
                Assert.AreEqual(wall.Level.Control.TotalCost, wall.Model.TotalSpent, 1e-9);

                // The MENU button on the result card goes back to the mode choice.
                var menuButton = UnityEngine.Object.FindFirstObjectByType<ResultView>(FindObjectsInactive.Include)
                    .transform.Find("Card/Menu").GetComponent<UnityEngine.UI.Button>();
                // The popup appears after the dive into the pool.
                await UniTask.WaitUntil(() => menuButton.gameObject.activeInHierarchy).Timeout(TimeSpan.FromSeconds(5));
                menuButton.onClick.Invoke();
                await UniTask.WaitUntil(() => !navigation.IsTransitioning).Timeout(TimeSpan.FromSeconds(10));
                Assert.AreEqual(GamePhase.Menu, session.Phase);
                Assert.AreEqual(GameNavigation.MenuScene, SceneManager.GetActiveScene().name);
                Assert.IsNull(UnityEngine.Object.FindFirstObjectByType<GameScope>());
                Assert.IsFalse(SceneManager.GetSceneByName(GameNavigation.GameScene).isLoaded);

                session.StartRound(GameMode.Wall);
                await navigation.OpenAsync(GameNavigation.GameScene);
                gameplay = await LoadedGameplayAsync();
                Assert.IsInstanceOf<Slime.Gameplay.Core.Levels.WallLevelRequest>(gameplay.Current.Request);
                Assert.AreNotEqual(wall.Seed, gameplay.Current.Seed, "a new wall every time");
                await navigation.OpenAsync(GameNavigation.MenuScene);
                session.StartRound(GameMode.Campaign);
                await navigation.OpenAsync(GameNavigation.GameScene);
                gameplay = await LoadedGameplayAsync();
                Assert.IsInstanceOf<Slime.Gameplay.Core.Levels.CampaignLevelRequest>(gameplay.Current.Request, "LEVELS starts the campaign");
            }
            finally { await TearDownAsync(); }
        });

        [UnityTest]
        public IEnumerator Level_grid_shows_progress_and_starts_only_unlocked_levels() => UniTask.ToCoroutine(async () =>
        {
            try
            {
                PlayerPrefs.SetInt(PlayerPrefsCampaignProgress.Key, 3);
                var project = await StartMenuAsync();
                var session = project.Container.Resolve<GameSession>();
                var navigation = project.Container.Resolve<GameNavigation>();
                var config = UnityEngine.Object.FindFirstObjectByType<MenuScope>().Container.Resolve<Slime.Gameplay.Config.SlimeGameConfig>();
                var menu = UnityEngine.Object.FindFirstObjectByType<MenuView>();
                var levels = (UnityEngine.UI.Button)typeof(MenuView)
                    .GetField("playButton", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(menu);

                levels.onClick.Invoke();
                var grid = UnityEngine.Object.FindFirstObjectByType<LevelSelectView>();
                Assert.IsNotNull(grid);
                var group = grid.GetComponent<CanvasGroup>();
                Assert.IsNotNull(group, "level selection uses the shared view transition");
                Assert.IsFalse(group.interactable, "input is blocked while opening");
                await UniTask.WaitUntil(() => group.interactable).Timeout(TimeSpan.FromSeconds(5));
                Assert.AreEqual(1f, group.alpha);
                Assert.AreEqual(config.Campaign.Count, grid.Cells.Count);
                for (var i = 0; i < grid.Cells.Count; i++)
                    Assert.AreEqual(i < 3, grid.Cells[i].interactable, $"level {i + 1}");
                Assert.AreEqual(GamePhase.Menu, session.Phase);

                grid.Cells[5].onClick.Invoke(); // locked: ignored even if clicked programmatically
                await UniTask.Yield();
                Assert.AreEqual(GamePhase.Menu, session.Phase);
                Assert.IsTrue(grid.IsVisible);

                grid.Cells[1].onClick.Invoke();
                Assert.IsTrue(grid.gameObject.activeSelf, "the view stays active until its closing animation finishes");
                Assert.IsFalse(group.interactable, "input is blocked while closing");
                Assert.AreEqual(GamePhase.Menu, session.Phase, "the round starts after the view finishes closing");
                await UniTask.WaitUntil(() => grid == null || !grid.gameObject.activeSelf)
                    .Timeout(TimeSpan.FromSeconds(5));
                var gameplay = await LoadedGameplayAsync();
                Assert.AreEqual(GamePhase.Playing, session.Phase);
                Assert.AreEqual(2, gameplay.Current.LevelNumber);
                Assert.AreEqual(PauseReason.None, gameplay.Pause);

                // BACK closes the grid without loading gameplay.
                await navigation.OpenAsync(GameNavigation.MenuScene);
                menu = UnityEngine.Object.FindFirstObjectByType<MenuView>();
                levels = (UnityEngine.UI.Button)typeof(MenuView)
                    .GetField("playButton", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(menu);
                levels.onClick.Invoke();
                await UniTask.Yield();
                grid = UnityEngine.Object.FindFirstObjectByType<LevelSelectView>();
                group = grid.GetComponent<CanvasGroup>();
                await UniTask.WaitUntil(() => group.interactable).Timeout(TimeSpan.FromSeconds(5));
                ((UnityEngine.UI.Button)typeof(LevelSelectView)
                    .GetField("backButton", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .GetValue(grid)).onClick.Invoke();
                Assert.IsTrue(grid.gameObject.activeSelf, "BACK also waits for the closing animation");
                Assert.IsFalse(group.interactable);
                await UniTask.WaitUntil(() => !grid.gameObject.activeSelf).Timeout(TimeSpan.FromSeconds(5));
                Assert.IsFalse(grid.IsVisible);
                Assert.AreEqual(GamePhase.Menu, session.Phase);
                Assert.IsTrue(levels.IsInteractable(), "menu input is restored after closing");
            }
            finally { await TearDownAsync(); }
        });

        private static async UniTask WaitStep(GameplayHandle gameplay, string step, float seconds = 20)
        {
            await UniTask.WaitUntil(() => gameplay.Tutorial != null && gameplay.Tutorial.Step == step).Timeout(TimeSpan.FromSeconds(seconds));
            await UniTask.Delay(450, ignoreTimeScale: true); // the tap guard of an explanation card
        }

        private static void TapContinue(GameplayHandle gameplay) =>
            gameplay.Stage.Tutorial.transform.Find("Continue").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();

        private static async UniTask TapGameAsync(GameplayHandle gameplay)
        {
            var input = gameplay.Stage.Input;
            input.SetVirtualPointer(false);
            await UniTask.Yield();
            input.SetVirtualPointer(true);
            await UniTask.Yield();
            await UniTask.Yield();
            input.SetVirtualPointer(false);
            await UniTask.Yield();
            input.SetVirtualPointer(null);
        }

        [UnityTest]
        public IEnumerator Level_one_tutorial_explains_pauses_and_guides_both_shots() => UniTask.ToCoroutine(async () =>
        {
            try
            {
                PlayerPrefs.DeleteKey(PlayerPrefsCampaignProgress.Key);
                var gameplay = await StartGameAsync();
                gameplay.TutorialEnabled = true;
                gameplay.PlayCampaignLevel(1);
                var overlay = gameplay.Stage.Tutorial;
                var model = gameplay.Current.Model;

                await WaitStep(gameplay, "tour");
                Assert.IsTrue(overlay.IsVisible);
                Assert.IsTrue(overlay.WaitsForTap);
                Assert.AreEqual(PauseReason.Tutorial, gameplay.Pause, "explanations pause the game");
                Assert.IsTrue(gameplay.Stage.CameraRig.IsTouring, "the camera flies to the doors");
                TapContinue(gameplay);
                foreach (var step in new[] { "slime", "meter", "crystal" })
                {
                    await WaitStep(gameplay, step);
                    Assert.AreEqual(PauseReason.Tutorial, gameplay.Pause);
                    TapContinue(gameplay);
                }

                await WaitStep(gameplay, "shot1");
                Assert.AreEqual(PauseReason.None, gameplay.Pause, "the player acts for real");
                Assert.IsFalse(overlay.WaitsForTap, "touches go through to the game");
                await TapGameAsync(gameplay);
                await UniTask.WaitUntil(() => model.ShotsFired == 1).Timeout(TimeSpan.FromSeconds(5));

                await WaitStep(gameplay, "hop");
                Assert.AreEqual(AttemptPhase.Waiting, model.Phase);
                TapContinue(gameplay);
                await WaitStep(gameplay, "shot2");
                await TapGameAsync(gameplay);
                await WaitStep(gameplay, "doors");
                Assert.IsTrue(model.DoorsOpen);
                TapContinue(gameplay);

                await UniTask.WaitUntil(() => model.Phase == AttemptPhase.Won).Timeout(TimeSpan.FromSeconds(20));
                Assert.IsFalse(gameplay.Tutorial.IsRunning);
                Assert.IsFalse(overlay.IsVisible);
                Assert.AreEqual(PauseReason.None, gameplay.Pause);

                // SKIP ends it at once and leaves no pause behind.
                gameplay.PlayCampaignLevel(1);
                await WaitStep(gameplay, "tour");
                overlay.transform.Find("Skip").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                await UniTask.WaitUntil(() => !gameplay.Tutorial.IsRunning).Timeout(TimeSpan.FromSeconds(3));
                Assert.IsFalse(overlay.IsVisible);
                Assert.AreEqual(PauseReason.None, gameplay.Pause);
            }
            finally { await TearDownAsync(); }
        });

        [UnityTest]
        [Category("GameplayNavigation")]
        public IEnumerator Campaign_progresses_repeats_and_completes() => UniTask.ToCoroutine(async () =>
        {
            try
            {
                PlayerPrefs.DeleteKey(PlayerPrefsCampaignProgress.Key);
                var gameplay = await StartGameAsync();
                var progress = gameplay.Progress;
                Assert.AreEqual(1, progress.Unlocked);
                gameplay.PlayCampaignLevel(5);
                Assert.AreEqual(1, gameplay.Current.LevelNumber, "locked levels are not reachable");
                Assert.IsTrue(gameplay.Current.IsCampaign);

                Time.timeScale = 4;
                var first = gameplay.Current;
                Assert.AreEqual(AttemptPhase.Won, await GameplayAutoplay.PlayAsync(gameplay, AutoplayMode.ControlPlan, CancellationToken.None)
                    .Timeout(TimeSpan.FromSeconds(60)));
                Assert.AreEqual(2, progress.Unlocked, "a win unlocks the next level");
                Assert.AreEqual(2, PlayerPrefs.GetInt(PlayerPrefsCampaignProgress.Key), "progress is saved");

                // Retry: the same layout at full size.
                gameplay.Restart();
                var again = gameplay.Current;
                Assert.AreNotSame(first, again);
                Assert.AreEqual(1, again.LevelNumber);
                Assert.AreEqual(first.Level.Crystals.Count, again.Level.Crystals.Count);
                for (var i = 0; i < first.Level.Crystals.Count; i++) Assert.AreEqual(first.Level.Crystals[i].Position, again.Level.Crystals[i].Position);
                Assert.AreEqual(again.Rules.D0, again.Model.Diameter);

                gameplay.NextLevel();
                Assert.AreEqual(2, gameplay.Current.LevelNumber);

                // Completion: unlock everything, win the last level.
                for (var n = 1; n <= gameplay.CampaignLength - 1; n++) progress.MarkCompleted(n, gameplay.CampaignLength);
                gameplay.PlayCampaignLevel(gameplay.CampaignLength);
                Assert.AreEqual(gameplay.CampaignLength, gameplay.Current.LevelNumber);
                Assert.AreEqual(AttemptPhase.Won, await GameplayAutoplay.PlayAsync(gameplay, AutoplayMode.ControlPlan, CancellationToken.None)
                    .Timeout(TimeSpan.FromSeconds(120)));
                Assert.AreEqual(gameplay.CampaignLength + 1, progress.Unlocked, "campaign complete");
                Assert.AreEqual(gameplay.CampaignLength, gameplay.ContinueLevel);
                var result = UnityEngine.Object.FindFirstObjectByType<GameScope>().Container.Resolve<ResultView>();
                await UniTask.WaitUntil(() => result.gameObject.activeSelf && !result.IsAnimating).Timeout(TimeSpan.FromSeconds(10));
                Assert.AreEqual(3, result.GetComponentsInChildren<UnityEngine.UI.Button>().Length);
                ClickVisible(result.transform.Find("RootTransform/SecondaryActions/Secondary").GetComponent<UnityEngine.UI.Button>());
                Assert.IsFalse(result.gameObject.activeSelf);
                Assert.AreEqual(gameplay.CampaignLength, gameplay.Current.LevelNumber, "The final level can be replayed from its result.");
                gameplay.PlayEndless();
                Assert.IsFalse(gameplay.Current.IsCampaign);
            }
            finally { await TearDownAsync(); }
        });
    }
}
