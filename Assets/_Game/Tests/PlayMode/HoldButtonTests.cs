using VContainer;
using VContainer.Unity;
using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Slime.Foundation;
using Slime.Composition;
using Slime.Infrastructure.Frame;
using Slime.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Slime.Tests.PlayMode
{
    public sealed class HoldButtonTests
    {
        private GameObject _root;
        private IObjectResolver _container;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.Destroy(_root);
            _container?.Dispose();
        }

        private HoldButton Make(float seconds)
        {
            _root = new GameObject("HoldButtonTest", typeof(Canvas));
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_root.transform, false);
            var button = go.AddComponent<HoldButton>();
            var builder = new ContainerBuilder();
            builder.Register<IFrameTimeSource, UnityFrameTimeSource>(Lifetime.Singleton);
            builder.RegisterEntryPoint<TickRegistry>().AsSelf();
            _container = builder.Build();
            button.BindTicks(_container.Resolve<ITickRegistry>());
            button.HoldSeconds = seconds;
            return button;
        }

        private static PointerEventData Pointer() => new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };

        // The hold is the confirmation: it fires once, only after the full time, never on a tap, and letting go early cancels it.
        [UnityTest]
        public IEnumerator Acts_once_after_the_full_hold_and_never_on_a_tap_or_an_early_release() => UniTask.ToCoroutine(async () =>
        {
            var button = Make(0.4f);
            int completed = 0, clicked = 0, cancelled = 0;
            button.OnHoldCompleted.AddListener(() => completed++);
            button.onClick.AddListener(() => clicked++);
            button.HoldCancelled += () => cancelled++;

            button.OnPointerDown(Pointer());
            await UniTask.Delay(150, ignoreTimeScale: true);
            button.OnPointerUp(Pointer());
            button.OnPointerClick(Pointer());
            Assert.AreEqual((0, 0, 1), (completed, clicked, cancelled), "released early: nothing happens, a tap is not a click");
            Assert.Greater(button.Progress, 0f);
            await UniTask.Delay(400, ignoreTimeScale: true);
            Assert.AreEqual(0f, button.Progress, "an unfinished hold rewinds");

            button.OnPointerDown(Pointer());
            await UniTask.Delay(200, ignoreTimeScale: true);
            Assert.AreEqual(0, completed, "half way");
            await UniTask.Delay(400, ignoreTimeScale: true);
            Assert.AreEqual(1, completed, "after the full hold");
            await UniTask.Delay(300, ignoreTimeScale: true);
            Assert.AreEqual(1, completed, "once, even if still held");
            Assert.AreEqual(1f, button.Progress, "stays full until pressed again");

            button.HoldSeconds = 0.01f;
            Assert.AreEqual(HoldButton.MinHoldSeconds, button.HoldSeconds, "too short a hold is clamped");

            button.RequireHold = false;
            button.OnPointerClick(Pointer());
            Assert.AreEqual((1, 1), (completed, clicked), "without RequireHold it is an ordinary button");
        });
    }
}
