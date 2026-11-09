using System.Collections.Generic;
using System.Linq;
using Game.App.Loading;
using Game.Gameplay.View;
using Game.Integration.Localization;
using Game.Localization;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Foundation.Tests
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
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/UI/SettingsPopup.prefab");
            Assert.IsNull(root.transform.Find("RootTransform/CopyPlayerId"));
            CheckLabels(root);
            var view = root.GetComponent<Game.Integration.Gameplay.SettingsPopupView>();
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

        private static void CheckLabels(GameObject root)
        {
            var table = LocalizationTable.FromJson(Resources.Load<TextAsset>("Localization/fallback").text);
            var fonts = Resources.Load<LocalizationFonts>(LocalizationFonts.ResourcePath);
            Assert.IsNotNull(fonts);
            fonts.ConfigureFallbacks();
            var dynamic = new HashSet<TMP_Text>();
            foreach (var view in root.GetComponentsInChildren<Game.Integration.Game.GameView>(true))
                dynamic.UnionWith(view.DynamicTextLabels);
            foreach (var view in root.GetComponentsInChildren<Game.Integration.Game.ResultView>(true))
                dynamic.UnionWith(view.DynamicTextLabels);
            foreach (var loading in root.GetComponentsInChildren<LoadingScreenView>(true))
                dynamic.UnionWith(loading.DynamicTextLabels);
            foreach (var tutorial in root.GetComponentsInChildren<TutorialOverlay>(true))
                dynamic.Add(tutorial.MessageLabel);
            foreach (var view in root.GetComponentsInChildren<Game.Integration.Gameplay.SettingsPopupView>(true))
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
