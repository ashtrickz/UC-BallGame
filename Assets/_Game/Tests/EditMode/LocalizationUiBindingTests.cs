using Slime.Foundation;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Slime.App.Loading;
using Slime.Gameplay.View;
using Slime.Composition;
using Slime.Diagnostics;
using Slime.Gameplay.Presentation;
using Slime.Infrastructure.Settings;
using Slime.Infrastructure.Localization;
using Slime.Localization;
using Slime.Settings;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Slime.Tests.EditMode
{
    public sealed class LocalizationUiBindingTests
    {
        [TestCase("Loading")]
        [TestCase("Menu")]
        [TestCase("Game")]
        public void Every_authored_label_has_a_key_or_a_dynamic_presenter(string name)
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/_Game/Scenes/" + name + ".unity");
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                    CheckLabels(root);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void Settings_prefab_has_all_four_language_buttons_without_player_id_controls()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/UI/Popups/SettingsPopup.prefab");
            Assert.IsNull(root.transform.Find("RootTransform/CopyPlayerId"));
            CheckLabels(root);
            var view = root.GetComponent<SettingsPopupView>();
            var serialized = new SerializedObject(view);
            var choices = serialized.FindProperty("languageChoices");
            Assert.AreEqual(4, choices.arraySize);
            var codes = new HashSet<string>();
            for (var index = 0; index < choices.arraySize; index++)
            {
                var choice = choices.GetArrayElementAtIndex(index);
                var code = choice.FindPropertyRelative("code").stringValue;
                codes.Add(code);
                var button = choice.FindPropertyRelative("button").objectReferenceValue as UnityEngine.UI.Button;
                Assert.IsNotNull(button, code);
                Assert.AreEqual("language." + code, button.GetComponentInChildren<LocalizedText>(true).Key);
            }
            CollectionAssert.AreEquivalent(new[] { "en", "uk", "es", "zh-CN" }, codes);
        }

        [Test]
        public void Settings_volume_and_fps_controls_have_distinct_bindings_and_correct_captions()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/UI/Popups/SettingsPopup.prefab");
            var serialized = new SerializedObject(root.GetComponent<SettingsPopupView>());
            var volumes = serialized.FindProperty("volumes");
            var channels = new HashSet<VolumeChannel>();
            var sliders = new HashSet<UnityEngine.UI.Slider>();
            Assert.AreEqual(4, volumes.arraySize);
            for (var index = 0; index < volumes.arraySize; index++)
            {
                var row = volumes.GetArrayElementAtIndex(index);
                var channel = (VolumeChannel)row.FindPropertyRelative("channel").intValue;
                var slider = row.FindPropertyRelative("slider").objectReferenceValue as UnityEngine.UI.Slider;
                Assert.IsNotNull(slider, channel.ToString());
                Assert.IsTrue(channels.Add(channel), "Duplicate volume channel");
                Assert.IsTrue(sliders.Add(slider), "Two channels share a slider");
                var caption = slider.transform.parent.GetComponentInChildren<LocalizedText>(true);
                Assert.AreEqual("settings." + channel.ToString().ToLowerInvariant() + "_volume", caption.Key);
            }
            CollectionAssert.AreEquivalent(new[] { VolumeChannel.Master, VolumeChannel.Music, VolumeChannel.Sfx, VolumeChannel.Ui }, channels);

            var choices = serialized.FindProperty("fpsChoices");
            var rates = new HashSet<int>();
            var buttons = new HashSet<UnityEngine.UI.Button>();
            Assert.AreEqual(3, choices.arraySize);
            for (var index = 0; index < choices.arraySize; index++)
            {
                var choice = choices.GetArrayElementAtIndex(index);
                var fps = choice.FindPropertyRelative("fps").intValue;
                var button = choice.FindPropertyRelative("button").objectReferenceValue as UnityEngine.UI.Button;
                Assert.IsNotNull(button, fps.ToString());
                Assert.IsTrue(rates.Add(fps), "Duplicate frame rate");
                Assert.IsTrue(buttons.Add(button), "Two frame rates share a button");
                var caption = button.GetComponentInChildren<LocalizedText>(true);
                Assert.AreEqual("settings.fps", caption.Key);
                var arguments = new SerializedObject(caption).FindProperty("arguments");
                Assert.AreEqual(1, arguments.arraySize);
                Assert.AreEqual(fps.ToString(), arguments.GetArrayElementAtIndex(0).stringValue);
            }
            CollectionAssert.AreEquivalent(new[] { 30, 60, 120 }, rates);
        }

        private static void CheckLabels(GameObject root)
        {
            var table = LocalizationTable.FromJson(Resources.Load<TextAsset>("Localization/fallback").text);
            var fonts = Resources.Load<LocalizationFonts>(LocalizationFonts.ResourcePath);
            Assert.IsNotNull(fonts);
            fonts.ConfigureFallbacks();
            var dynamic = new HashSet<TMP_Text>();
            foreach (var view in root.GetComponentsInChildren<GameView>(true))
                dynamic.UnionWith(view.DynamicTextLabels);
            foreach (var view in root.GetComponentsInChildren<ResultView>(true))
                dynamic.UnionWith(view.DynamicTextLabels);
            foreach (var loading in root.GetComponentsInChildren<LoadingScreenView>(true))
                dynamic.UnionWith(loading.DynamicTextLabels);
            foreach (var tutorial in root.GetComponentsInChildren<TutorialOverlay>(true))
                dynamic.Add(tutorial.MessageLabel);
            foreach (var view in root.GetComponentsInChildren<SettingsPopupView>(true))
                dynamic.Add(new SerializedObject(view).FindProperty("buildText").objectReferenceValue as TMP_Text);
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                var path = AnimationUtility.CalculateTransformPath(label.transform, root.transform);
                var binding = label.GetComponent<LocalizedText>();
                if (dynamic.Contains(label) || int.TryParse(label.text, out _))
                    continue;
                Assert.IsNotNull(binding, path + " has no localization key");
                foreach (var language in new[] { "en", "uk", "es", "zh-CN" })
                {
                    Assert.IsTrue(table.TryGet(binding.Key, language, out var translation), binding.Key + "/" + language);
                    var value = string.Format(translation, 30, 2);
                    // Dynamic Latin/Cyrillic fallbacks populate as they do when TMP renders; static Chinese cannot grow.
                    Assert.IsTrue(label.font.HasCharacters(value, out uint[] missing, true, true),
                        path + "/" + language + " has missing glyphs: " + string.Join(",", missing ?? new uint[0]));
                }
            }
        }
    }
}
