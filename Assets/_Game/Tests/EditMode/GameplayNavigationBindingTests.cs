using Slime.Foundation;
using System.Linq;
using NUnit.Framework;
using Slime.Composition;
using Slime.Diagnostics;
using Slime.Gameplay.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Slime.Tests.EditMode
{
    public sealed class GameplayNavigationBindingTests
    {
        [Test]
        public void Gameplay_and_result_own_their_buttons_without_extra_actions()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/_Game/Scenes/Game.unity");
            try
            {
                var roots = scene.GetRootGameObjects();
                var hud = roots.SelectMany(root => root.GetComponentsInChildren<GameView>(true)).Single();
                var result = roots.SelectMany(root => root.GetComponentsInChildren<ResultView>(true)).Single();
                var hudData = new SerializedObject(hud);
                var restart = Button(hudData, "restartButton");
                var menu = Button(hudData, "menuButton");
                var settings = Button(hudData, "settingsButton");
                var burger = Button(hudData, "burgerButton");
                var dropdown = hudData.FindProperty("actionsMenu").objectReferenceValue as GameObject;
                Assert.IsNotNull(dropdown);
                Assert.AreSame(hud.transform, burger.transform.parent);
                Assert.AreEqual(Vector2.one, ((RectTransform)burger.transform).anchorMin);
                Assert.IsTrue(burger.gameObject.activeSelf);
                CollectionAssert.AreEqual(new[] { menu, restart, settings }, dropdown.GetComponentsInChildren<UnityEngine.UI.Button>(true));
                foreach (var button in new[] { menu, restart, settings })
                {
                    Assert.AreSame(dropdown.transform, button.transform.parent);
                    var rect = (RectTransform)button.transform;
                    Assert.AreEqual(rect.rect.width, rect.rect.height, button.name + " must be square.");
                    Assert.IsNotNull(button.transform.Find("Icon").GetComponent<UnityEngine.UI.Image>(), button.name);
                }
                Assert.IsTrue(restart.transform.IsChildOf(hud.transform), "Restart must reference the gameplay button.");
                Assert.IsTrue(menu.transform.IsChildOf(hud.transform), "Menu must be available during gameplay.");
                Assert.AreNotSame(restart, menu);
                Assert.IsTrue(restart.gameObject.activeSelf);
                Assert.IsTrue(menu.gameObject.activeSelf);

                var resultData = new SerializedObject(result);
                var primary = Button(resultData, "primaryButton");
                var secondary = Button(resultData, "secondaryButton");
                var resultMenu = Button(resultData, "menuButton");
                CollectionAssert.AreEquivalent(new[] { primary, secondary, resultMenu },
                    result.GetComponentsInChildren<UnityEngine.UI.Button>(true), "Every result button must be managed by ResultView.");
                foreach (var button in new[] { primary, secondary, resultMenu })
                {
                    Assert.IsNull(button.GetComponentInChildren<TMPro.TMP_Text>(true), "Result actions use icons.");
                    Assert.IsNotNull(button.transform.Find("Icon").GetComponent<UnityEngine.UI.Image>().sprite, button.name);
                }
                Assert.AreSame(secondary.transform.parent, resultMenu.transform.parent);
                var layout = secondary.GetComponentInParent<UnityEngine.UI.LayoutGroup>(true);
                Assert.IsNotNull(layout, "The alternate action and Menu must share a row that collapses hidden actions.");
                Assert.AreEqual(TextAnchor.MiddleCenter, layout.childAlignment);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static UnityEngine.UI.Button Button(SerializedObject view, string field)
        {
            var button = view.FindProperty(field).objectReferenceValue as UnityEngine.UI.Button;
            Assert.IsNotNull(button, field);
            return button;
        }
    }
}
