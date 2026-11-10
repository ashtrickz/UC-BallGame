using Slime.Foundation;
using System;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using Slime.Gameplay.View;
using Slime.Composition;
using Slime.Gameplay.Presentation;
using Slime.UI;
using UnityEngine;

namespace Slime.Tests.PlayMode
{
    /// <summary>Exactly the awarded stars launch and arrive; pause freezes flight and cancellation cannot fill a new result.</summary>
    public sealed class WorldStarsViewTests
    {
        private GameObject _root;
        private WorldStarsView _flight;
        private StarRowView _row;
        private Camera _camera;
        private GameClock _clock;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Star animation tests");
            var cameraObject = new GameObject("Camera", typeof(Camera));
            cameraObject.transform.SetParent(_root.transform);
            cameraObject.transform.position = new Vector3(0, 0, -10);
            _camera = cameraObject.GetComponent<Camera>();
            _camera.orthographic = true;
            var canvasObject = new GameObject("Popup", typeof(RectTransform), typeof(Canvas), typeof(StarRowView));
            canvasObject.transform.SetParent(_root.transform);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _row = canvasObject.GetComponent<StarRowView>();
            Set(_row, "slots", Images(canvasObject.transform));
            var world = new GameObject("World stars", typeof(RectTransform), typeof(Canvas), typeof(WorldStarsView));
            world.transform.SetParent(_root.transform);
            world.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            _flight = world.GetComponent<WorldStarsView>();
            Set(_flight, "stars", Images(world.transform));
            _clock = new GameClock();
        }

        [TearDown]
        public void TearDown()
        {
            _clock.Stop();
            UnityEngine.Object.DestroyImmediate(_root);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Only_earned_stars_launch_arrive_and_activate_slots(int count)
        {
            var launched = 0;
            var arrived = 0;
            _flight.Launched += _ => launched++;
            _flight.Arrived += _ => arrived++;
            var task = _flight.PlayAsync(count, Vector3.zero, _camera, _row, _clock, CancellationToken.None);
            Assert.AreEqual(0, _row.FilledCount);
            for (var i = 0; i < 80; i++)
                _clock.Tick(0.05f);
            task.GetAwaiter().GetResult();
            Assert.AreEqual(count, launched);
            Assert.AreEqual(count, arrived);
            Assert.AreEqual(count, _row.FilledCount);
            Assert.AreEqual(0, _flight.FlyingCount);
            Assert.IsFalse(_flight.IsPlaying);
        }

        [Test]
        public void Paused_flight_does_not_advance_and_cancelled_flight_cannot_claim_stars()
        {
            using var cancellation = new CancellationTokenSource();
            var arrived = 0;
            _flight.Arrived += _ => arrived++;
            var task = _flight.PlayAsync(3, Vector3.zero, _camera, _row, _clock, cancellation.Token);
            _clock.Tick(0.2f);
            _clock.Paused = true;
            for (var i = 0; i < 10; i++)
                _clock.Tick(1);
            Assert.AreEqual(0, arrived);
            Assert.IsTrue(_flight.IsPlaying);
            cancellation.Cancel();
            _clock.Tick(0);
            Assert.Catch<OperationCanceledException>(() => task.GetAwaiter().GetResult());
            Assert.AreEqual(0, _row.FilledCount);
            Assert.AreEqual(0, _flight.FlyingCount);
        }

        private static UnityEngine.UI.Image[] Images(Transform parent)
        {
            var images = new UnityEngine.UI.Image[StarRowView.SlotCount];
            for (var i = 0; i < images.Length; i++)
            {
                var child = new GameObject("Star " + i, typeof(RectTransform), typeof(UnityEngine.UI.Image));
                child.transform.SetParent(parent, false);
                images[i] = child.GetComponent<UnityEngine.UI.Image>();
                images[i].rectTransform.sizeDelta = Vector2.one;
            }

            return images;
        }

        private static void Set(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }
    }
}
