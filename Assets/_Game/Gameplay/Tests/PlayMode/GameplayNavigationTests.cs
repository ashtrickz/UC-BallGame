using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.App.Scenes;
using Game.Gameplay.Model;
using Game.Integration.Composition;
using Game.Integration.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using VContainer;

namespace Game.Gameplay.PlayModeTests
{
    public sealed partial class GameplayLifecycleTests
    {
        [UnityTest]
        [Category("GameplayNavigation")]
        public IEnumerator Gameplay_buttons_restart_during_charge_and_movement_and_return_to_menu() => UniTask.ToCoroutine(async () =>
        {
            try
            {
                var gameplay = await StartGameAsync();
                gameplay.PlayCampaignLevel(1);
                var hud = UnityEngine.Object.FindFirstObjectByType<GameView>();
                var burger = hud.transform.Find("BurgerButton").GetComponent<UnityEngine.UI.Button>();
                var restart = hud.transform.Find("ActionsMenu/RetryButton").GetComponent<UnityEngine.UI.Button>();
                var menu = hud.transform.Find("ActionsMenu/Menu").GetComponent<UnityEngine.UI.Button>();
                var changes = new List<bool>();
                hud.ActionsMenuChanged += changes.Add;
                Assert.IsFalse(hud.IsActionsMenuOpen);
                ClickVisible(burger);
                Assert.IsTrue(hud.IsActionsMenuOpen);
                ClickVisible(burger);
                Assert.IsFalse(hud.IsActionsMenuOpen);
                CollectionAssert.AreEqual(new[] { true, false }, changes);
                // Closing is the same event, and happens before the selected action starts.
                hud.RestartRequested += () => Assert.IsFalse(hud.IsActionsMenuOpen);
                hud.MenuRequested += () => Assert.IsFalse(hud.IsActionsMenuOpen);
                await WaitPhase(gameplay.Current.Model, AttemptPhase.Waiting);
                gameplay.Stage.Input.SetVirtualPointer(true);
                await WaitPhase(gameplay.Current.Model, AttemptPhase.Charging);
                var previous = gameplay.Current;
                ClickVisible(burger);
                await WaitActionsMenuAsync(hud);
                ClickVisible(restart);
                Assert.IsFalse(hud.IsActionsMenuOpen);
                CollectionAssert.AreEqual(new[] { true, false, true, false }, changes);
                Assert.IsTrue(previous.IsDisposed);
                Assert.IsTrue(previous.Token.IsCancellationRequested);
                Assert.AreNotSame(previous, gameplay.Current);
                Assert.AreEqual(previous.LevelNumber, gameplay.Current.LevelNumber);
                Assert.AreEqual(gameplay.Current.Rules.D0, gameplay.Current.Model.Diameter);

                // Restart is also available while presentation is moving the slime.
                Assert.IsTrue(gameplay.IsBusy);
                previous = gameplay.Current;
                ClickVisible(burger);
                await WaitActionsMenuAsync(hud);
                ClickVisible(restart);
                Assert.IsTrue(previous.IsDisposed);
                Assert.AreNotSame(previous, gameplay.Current);
                gameplay.Stage.Input.SetVirtualPointer(false);
                await WaitPhase(gameplay.Current.Model, AttemptPhase.Waiting);
                await UniTask.DelayFrame(3);
                Assert.AreEqual(0, gameplay.Current.Model.ShotsFired, "The old held pointer cannot shoot in the new attempt.");

                gameplay.Stage.Input.SetVirtualPointer(true);
                await WaitPhase(gameplay.Current.Model, AttemptPhase.Charging);
                previous = gameplay.Current;
                var navigation = ProjectScope.Instance.Container.Resolve<GameNavigation>();
                ClickVisible(burger);
                await WaitActionsMenuAsync(hud);
                ClickVisible(menu);
                await UniTask.WaitUntil(() => !navigation.IsTransitioning).Timeout(TimeSpan.FromSeconds(15));
                Assert.IsNull(navigation.LastError);
                Assert.IsTrue(previous.IsDisposed);
                Assert.IsNull(UnityEngine.Object.FindFirstObjectByType<GameScope>());
                Assert.AreEqual(GameNavigation.MenuScene, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            }
            finally { await TearDownAsync(); }
        });

        [UnityTest]
        [Category("GameplayNavigation")]
        public IEnumerator Settings_closes_the_burger_before_opening_and_returning_to_game() => UniTask.ToCoroutine(async () =>
        {
            try
            {
                var gameplay = await StartGameAsync();
                var hud = UnityEngine.Object.FindFirstObjectByType<GameView>();
                var burger = hud.transform.Find("BurgerButton").GetComponent<UnityEngine.UI.Button>();
                var settings = hud.transform.Find("ActionsMenu/Settings").GetComponent<UnityEngine.UI.Button>();
                var changes = new List<bool>();
                hud.ActionsMenuChanged += changes.Add;
                hud.SettingsRequested += () => Assert.IsFalse(hud.IsActionsMenuOpen);
                ClickVisible(burger);
                await WaitActionsMenuAsync(hud);
                ClickVisible(settings);
                CollectionAssert.AreEqual(new[] { true, false }, changes);
                Assert.IsFalse(hud.IsActionsMenuOpen);
                await UniTask.WaitUntil(() => UnityEngine.Object.FindFirstObjectByType<global::Game.Integration.Gameplay.SettingsPopupView>() != null)
                    .Timeout(TimeSpan.FromSeconds(10));
                Assert.IsTrue((gameplay.Pause & PauseReason.Settings) != 0);
                var popup = UnityEngine.Object.FindFirstObjectByType<global::Game.Integration.Gameplay.SettingsPopupView>();
                await UniTask.WaitUntil(() => popup.IsVisible && popup.GetComponent<CanvasGroup>().interactable).Timeout(TimeSpan.FromSeconds(10));
                ClickVisible(popup.GetComponentsInChildren<UnityEngine.UI.Button>().Single(button => button.name == "CloseButton"));
                await UniTask.WaitUntil(() => (gameplay.Pause & PauseReason.Settings) == 0).Timeout(TimeSpan.FromSeconds(10));
                Assert.IsFalse(hud.IsActionsMenuOpen);
                ClickVisible(burger);
                Assert.IsTrue(hud.IsActionsMenuOpen);
                hud.SetGameplayActionsVisible(false);
                Assert.IsFalse(hud.IsActionsMenuOpen);
                hud.SetGameplayActionsVisible(true);
                Assert.IsTrue(burger.gameObject.activeSelf);
                Assert.IsFalse(hud.IsActionsMenuOpen);
            }
            finally { await TearDownAsync(); }
        });

        [UnityTest]
        [Category("GameplayNavigation")]
        public IEnumerator Burger_cascades_in_unscaled_time_and_survives_rapid_reversals() => UniTask.ToCoroutine(async () =>
        {
            try
            {
                await StartGameAsync();
                var hud = UnityEngine.Object.FindFirstObjectByType<GameView>();
                var burger = hud.transform.Find("BurgerButton").GetComponent<UnityEngine.UI.Button>();
                var dropdown = hud.transform.Find("ActionsMenu");
                var buttons = dropdown.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                var groups = buttons.Select(button => button.GetComponent<CanvasGroup>()).ToArray();
                var layout = dropdown.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
                Time.timeScale = 0;
                ClickVisible(burger);
                Assert.IsTrue(hud.IsActionsMenuAnimating);
                Assert.IsFalse(layout.enabled, "Layout must not overwrite animated positions.");
                Assert.IsTrue(groups.All(group => group.alpha == 0 && !group.interactable));
                await UniTask.WaitUntil(() => groups[0].alpha > 0).Timeout(TimeSpan.FromSeconds(3), DelayType.UnscaledDeltaTime);
                Assert.GreaterOrEqual(groups[0].alpha, groups[1].alpha, "Menu appears before Restart.");
                Assert.GreaterOrEqual(groups[1].alpha, groups[2].alpha, "Restart appears before Settings.");
                await WaitActionsMenuAsync(hud);
                Assert.IsTrue(groups.All(group => group.alpha == 1 && group.interactable));
                Assert.IsTrue(layout.enabled);
                var positions = buttons.Select(button => ((RectTransform)button.transform).anchoredPosition).ToArray();

                // Reverse a running close, then an opening; the last requested state must win.
                ClickVisible(burger);
                Assert.IsFalse(hud.IsActionsMenuOpen);
                Assert.IsTrue(dropdown.gameObject.activeSelf, "Closing must fade out instead of disappearing instantly.");
                Assert.IsTrue(groups.All(group => !group.interactable && !group.blocksRaycasts));
                await UniTask.DelayFrame(2);
                ClickVisible(burger);
                await UniTask.DelayFrame(2);
                ClickVisible(burger);
                await WaitActionsMenuAsync(hud);
                Assert.IsFalse(dropdown.gameObject.activeSelf);
                Assert.IsTrue(groups.All(group => group.alpha == 0));
                ClickVisible(burger);
                await WaitActionsMenuAsync(hud);
                for (var i = 0; i < buttons.Length; i++)
                    Assert.Less(Vector2.Distance(positions[i], ((RectTransform)buttons[i].transform).anchoredPosition), 0.01f);
                hud.HideInstant();
                Assert.IsFalse(hud.IsActionsMenuOpen);
                Assert.IsFalse(hud.IsActionsMenuAnimating);
                Assert.IsFalse(dropdown.gameObject.activeSelf);
                hud.ShowInstant();
                Assert.IsFalse(hud.IsActionsMenuOpen);
            }
            finally { await TearDownAsync(); }
        });

        private static UniTask WaitActionsMenuAsync(GameView hud) =>
            UniTask.WaitUntil(() => !hud.IsActionsMenuAnimating).Timeout(TimeSpan.FromSeconds(3), DelayType.UnscaledDeltaTime);

        [UnityTest]
        [Category("GameplayNavigation")]
        public IEnumerator Result_actions_follow_mode_and_outcome_and_do_not_duplicate_menu() => UniTask.ToCoroutine(async () =>
        {
            try
            {
                var gameplay = await StartGameAsync();
                Time.timeScale = 4;
                var result = UnityEngine.Object.FindFirstObjectByType<GameScope>().Container.Resolve<ResultView>();
                var hud = UnityEngine.Object.FindFirstObjectByType<GameView>();
                foreach (var mode in new[] { AttemptMode.Campaign, AttemptMode.Wall, AttemptMode.Endless })
                {
                    foreach (var won in new[] { false, true })
                    {
                        if (mode == AttemptMode.Campaign) gameplay.PlayCampaignLevel(1);
                        else if (mode == AttemptMode.Wall) gameplay.PlayWall(Seed);
                        else gameplay.RestartWithSeed(Seed);
                        var phase = await GameplayAutoplay.PlayAsync(gameplay, won ? AutoplayMode.ControlPlan : AutoplayMode.Overcharge,
                            CancellationToken.None).Timeout(TimeSpan.FromSeconds(90));
                        Assert.AreEqual(won ? AttemptPhase.Won : AttemptPhase.Lost, phase);
                        await UniTask.WaitUntil(() => result.gameObject.activeSelf && !result.IsAnimating).Timeout(TimeSpan.FromSeconds(10));
                        Assert.IsFalse(hud.transform.Find("BurgerButton").gameObject.activeSelf, "Burger must not distract from result actions.");
                        Assert.IsFalse(hud.IsActionsMenuOpen);
                        var buttons = result.GetComponentsInChildren<UnityEngine.UI.Button>();
                        var hasAlternate = mode == AttemptMode.Campaign && won || mode == AttemptMode.Wall && !won;
                        CollectionAssert.AreEquivalent(hasAlternate ? new[] { "Primary", "Secondary", "Menu" } : new[] { "Primary", "Menu" },
                            buttons.Select(button => button.name), mode + "/" + won);
                        var primary = buttons.Single(button => button.name == "Primary");
                        var previous = gameplay.Current;
                        ClickVisible(primary);
                        Assert.IsFalse(result.gameObject.activeSelf);
                        Assert.IsTrue(hud.transform.Find("BurgerButton").gameObject.activeSelf, "Burger must return with the new attempt.");
                        Assert.IsFalse(hud.IsActionsMenuOpen);
                        Assert.IsTrue(previous.IsDisposed);
                        Assert.AreEqual(mode, gameplay.Current.Mode);
                        if (mode == AttemptMode.Campaign)
                            Assert.AreEqual(won ? 2 : 1, gameplay.Current.LevelNumber);
                        if (mode == AttemptMode.Wall && !won)
                            Assert.AreEqual(previous.Seed, gameplay.Current.Seed, "Defeat retries the same wall by default.");
                    }
                }
            }
            finally { await TearDownAsync(); }
        });

        private static void ClickVisible(UnityEngine.UI.Button button)
        {
            Canvas.ForceUpdateCanvases();
            Assert.IsTrue(button.IsActive() && button.IsInteractable(), button.name);
            var rect = (RectTransform)button.transform;
            var canvas = button.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left
            };
            // The Simulator routes EventSystem raycasts through the currently focused editor display.
            // Verify the target's own hit area, then dispatch the real Button pointer-click handler.
            Assert.IsNotNull(canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>());
            Assert.IsTrue(button.targetGraphic.raycastTarget, button.name);
            Assert.IsTrue(RectTransformUtility.RectangleContainsScreenPoint(rect, pointer.position, camera), button.name);
            Assert.IsTrue(button.targetGraphic.Raycast(pointer.position, camera), button.name + " is blocked by its CanvasGroup.");
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
    }
}
