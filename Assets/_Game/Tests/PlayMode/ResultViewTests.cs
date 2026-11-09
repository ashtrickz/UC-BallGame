using Slime.Foundation;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Slime.Composition;
using Slime.Diagnostics;
using Slime.Gameplay.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Slime.Tests.PlayMode
{
    public sealed class ResultViewTests
    {
        private GameObject _root;
        private ResultView _view;
        private UnityEngine.UI.Button _primary, _secondary, _menu;
        private TMP_Text _title;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Result view tests", typeof(RectTransform));
            _root.SetActive(false);
            _view = _root.AddComponent<ResultView>();
            _primary = Child<UnityEngine.UI.Button>("Primary");
            _secondary = Child<UnityEngine.UI.Button>("Secondary");
            _menu = Child<UnityEngine.UI.Button>("Menu");
            _title = Child<TextMeshProUGUI>("Title");
            Set("primaryButton", _primary);
            Set("secondaryButton", _secondary);
            Set("menuButton", _menu);
            Set("title", _title);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [TestCase("primary")]
        [TestCase("secondary")]
        [TestCase("menu")]
        public void Model_action_closes_the_card_and_cannot_be_invoked_twice(string action)
        {
            var calls = 0;
            void Run()
            {
                Assert.IsFalse(_root.activeSelf);
                calls++;
            }
            _view.Initialize(new ResultViewModel("Result", "Stats", Run, Run, Run));
            _view.Show();
            var button = action == "primary" ? _primary : action == "secondary" ? _secondary : _menu;
            button.onClick.Invoke();
            button.onClick.Invoke();
            Assert.AreEqual(1, calls);
            Assert.IsFalse(_view.IsAnimating);
        }

        [UnityTest]
        public IEnumerator Updating_model_preserves_animation_and_replaces_copy_and_actions()
        {
            var oldCalls = 0;
            var newCalls = 0;
            _view.Initialize(new ResultViewModel("Old", "", () => oldCalls++, () => oldCalls++));
            _view.Show();
            yield return null;
            Assert.IsTrue(_view.IsAnimating);
            _view.Initialize(new ResultViewModel("Translated", "", () => newCalls++));
            Assert.AreEqual("Translated", _title.text);
            Assert.IsTrue(_view.IsAnimating);
            Assert.IsFalse(_secondary.gameObject.activeSelf);
            Assert.IsFalse(_menu.gameObject.activeSelf);
            _primary.onClick.Invoke();
            Assert.AreEqual(0, oldCalls);
            Assert.AreEqual(1, newCalls);
        }

        [Test]
        public void Hiding_result_discards_actions_from_the_previous_attempt()
        {
            var calls = 0;
            _view.Initialize(new ResultViewModel("Result", "", () => calls++));
            _view.Show();
            _view.Hide();
            _primary.onClick.Invoke();
            Assert.AreEqual(0, calls);
            Assert.IsFalse(_view.IsAnimating);
        }

        private T Child<T>(string name) where T : Component
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(_root.transform, false);
            return child.AddComponent<T>();
        }

        private void Set(string field, Object value) => typeof(ResultView)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_view, value);
    }
}
