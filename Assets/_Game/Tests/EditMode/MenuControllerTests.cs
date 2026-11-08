using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.App.Game;
using Game.App.Scenes;
using Game.Foundation;
using Game.Integration.Menu;
using Game.UI;
using Game.Gameplay.Config;
using Game.Integration.Game;
using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Foundation.Tests
{
    public sealed class MenuControllerTests
    {
        private sealed class Scenes : ISceneService
        {
            public int Calls;
            public string Destination;
            public UniTask LoadSceneAsync(string name, CancellationToken token)
            { Calls++; Destination = name; return UniTask.CompletedTask; }
        }
        private sealed class View : IMenuView
        {
            public event Action PlayRequested;
            public event Action WallRequested;
            public event Action SettingsRequested;
            public event Action<int> SlimeColorRequested;
            public int SelectedColor = -1;
            public void ChooseColor(int index) => SlimeColorRequested?.Invoke(index);
            public void SetSlimeColors(IReadOnlyList<SlimeColorPreset> presets, int selectedIndex) => SelectedColor = selectedIndex;
            public void SetSelectedSlimeColor(int index) => SelectedColor = index;
            public bool IsVisible { get; private set; }
            public bool Interactable;
            public void Play() => PlayRequested?.Invoke();
            public void Wall() => WallRequested?.Invoke();
            public void Settings() => SettingsRequested?.Invoke();
            public void SetInteractable(bool value) => Interactable = value;
            public UniTask ShowAsync(CancellationToken token = default) { token.ThrowIfCancellationRequested(); ShowInstant(); return UniTask.CompletedTask; }
            public UniTask HideAsync(CancellationToken token = default) { token.ThrowIfCancellationRequested(); HideInstant(); return UniTask.CompletedTask; }
            public void ShowInstant() => IsVisible = true;
            public void HideInstant() => IsVisible = false;
        }

        private sealed class Popups : IPopupService
        {
            public int Calls;
            public readonly UniTaskCompletionSource Closed = new UniTaskCompletionSource();
            public UniTask ShowAsync<TPopup>(CancellationToken token = default) where TPopup : ISimplePopupController
            { Calls++; return Closed.Task.AttachExternalCancellation(token); }
            public UniTask ShowAsync<TPopup, TModel>(TModel model, CancellationToken token = default) where TPopup : IPopupController<TModel> => throw new NotSupportedException();
            public UniTask<TResult> ShowAsync<TPopup, TModel, TResult>(TModel model, CancellationToken token = default) where TPopup : IPopupController<TModel, TResult> => throw new NotSupportedException();
        }

        [UnityTest]
        public IEnumerator Color_selection_is_saved_and_blocked_while_a_popup_is_open() => UniTask.ToCoroutine(async () =>
        {
            var hadColor = PlayerPrefs.HasKey(SlimeColorPreferences.Key);
            var savedColor = PlayerPrefs.GetString(SlimeColorPreferences.Key);
            var config = ScriptableObject.CreateInstance<SlimeGameConfig>();
            try
            {
                PlayerPrefs.DeleteKey(SlimeColorPreferences.Key);
                var view = new View();
                var popups = new Popups();
                using var navigation = new GameNavigation(new Scenes());
                using var controller = new MenuController(view, new GameSession(), popups, navigation, config: config);
                await controller.InitializeAsync();
                Assert.AreEqual(0, view.SelectedColor);
                view.ChooseColor(3);
                Assert.AreEqual(3, view.SelectedColor);
                Assert.AreEqual(config.SlimeColors[3].Id, PlayerPrefs.GetString(SlimeColorPreferences.Key));
                Assert.AreEqual(3, SlimeColorPreferences.SelectedIndex(config));
                view.ChooseColor(-1);
                view.ChooseColor(999);
                Assert.AreEqual(3, view.SelectedColor);
                view.Settings();
                view.ChooseColor(1);
                Assert.AreEqual(3, view.SelectedColor);
                popups.Closed.TrySetResult();
                view.ChooseColor(1);
                Assert.AreEqual(1, view.SelectedColor);
                controller.Dispose();
                view.ChooseColor(4);
                Assert.AreEqual(1, SlimeColorPreferences.SelectedIndex(config));
            }
            finally
            {
                if (hadColor) PlayerPrefs.SetString(SlimeColorPreferences.Key, savedColor);
                else PlayerPrefs.DeleteKey(SlimeColorPreferences.Key);
                PlayerPrefs.Save();
                UnityEngine.Object.DestroyImmediate(config);
            }
        });

        [Test]
        public void Saved_color_survives_config_reordering_and_a_removed_color_falls_back()
        {
            var hadColor = PlayerPrefs.HasKey(SlimeColorPreferences.Key);
            var savedColor = PlayerPrefs.GetString(SlimeColorPreferences.Key);
            var config = ScriptableObject.CreateInstance<SlimeGameConfig>();
            try
            {
                Assert.IsTrue(SlimeColorPreferences.Select(config, 3));
                var serialized = new UnityEditor.SerializedObject(config);
                serialized.FindProperty("slimeColors").MoveArrayElement(3, 0);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.AreEqual(0, SlimeColorPreferences.SelectedIndex(config));
                serialized.FindProperty("slimeColors").DeleteArrayElementAtIndex(0);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.AreEqual(0, SlimeColorPreferences.SelectedIndex(config));
                serialized.FindProperty("slimeColors").arraySize = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.AreEqual(-1, SlimeColorPreferences.SelectedIndex(config));
                Assert.IsFalse(SlimeColorPreferences.Select(config, 0));
            }
            finally
            {
                if (hadColor) PlayerPrefs.SetString(SlimeColorPreferences.Key, savedColor);
                else PlayerPrefs.DeleteKey(SlimeColorPreferences.Key);
                PlayerPrefs.Save();
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [UnityTest]
        public IEnumerator Play_preserves_the_round_and_navigates_once_to_gameplay() => UniTask.ToCoroutine(async () =>
        {
            var view = new View();
            var session = new GameSession();
            var starts = 0;
            session.RoundStarted += () => starts++;
            var scenes = new Scenes();
            using var navigation = new GameNavigation(scenes);
            using var controller = new MenuController(view, session, new Popups(), navigation);
            await controller.InitializeAsync();
            await controller.InitializeAsync();
            Assert.IsTrue(view.IsVisible);
            view.Play(); view.Play();
            Assert.AreEqual(GamePhase.Playing, session.Phase);
            Assert.AreEqual(1, starts);
            Assert.AreEqual(GameNavigation.GameScene, scenes.Destination);
            Assert.AreEqual(1, scenes.Calls);
            Assert.IsFalse(view.Interactable);
        });

        [UnityTest]
        public IEnumerator Settings_is_single_flight_and_does_not_start_the_game() => UniTask.ToCoroutine(async () =>
        {
            var view = new View();
            var session = new GameSession();
            var popups = new Popups();
            using var navigation = new GameNavigation(new Scenes());
            using var controller = new MenuController(view, session, popups, navigation);
            await controller.InitializeAsync();
            view.Settings(); view.Settings(); view.Play();
            Assert.AreEqual(1, popups.Calls);
            Assert.AreEqual(GamePhase.Menu, session.Phase);
            Assert.IsFalse(view.Interactable);
            popups.Closed.TrySetResult();
            Assert.IsTrue(view.Interactable);
            Assert.IsTrue(view.IsVisible);
        });

        [UnityTest]
        public IEnumerator Disposing_controller_cancels_settings_and_removes_button_listeners() => UniTask.ToCoroutine(async () =>
        {
            var view = new View();
            var session = new GameSession();
            var popups = new Popups();
            using var navigation = new GameNavigation(new Scenes());
            var controller = new MenuController(view, session, popups, navigation);
            await controller.InitializeAsync();
            view.Settings();
            controller.Dispose(); controller.Dispose();
            view.Play(); view.Settings();
            Assert.AreEqual(1, popups.Calls);
            Assert.AreEqual(GamePhase.Menu, session.Phase);
        });
    }
}
