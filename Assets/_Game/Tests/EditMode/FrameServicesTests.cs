using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Integration.Composition;
using Game.Integration.Audio;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Game.Foundation.Tests
{
    /// <summary>Protects frame registration, unscaled input and event-based layout behavior.</summary>
    public sealed class FrameServicesTests
    {
        [Test]
        public void Registry_samples_time_once_and_ignores_duplicate_registrations()
        {
            var time = new FrameTime { Delta = 0.125f };
            using var registry = new TickRegistry(time);
            var first = new CounterClient();
            var second = new CounterClient();
            registry.Register(first);
            registry.Register(first);
            registry.Register(second);
            registry.Tick();
            Assert.AreEqual(1, time.Reads);
            Assert.AreEqual(1, first.Count);
            Assert.AreEqual(1, second.Count);
            Assert.AreEqual(time.Delta, first.LastDelta);
            registry.Unregister(first);
            registry.Unregister(first);
            registry.Tick();
            Assert.AreEqual(1, first.Count);
            Assert.AreEqual(2, second.Count);
        }

        [Test]
        public void Removed_clients_stop_immediately_and_new_clients_wait_for_next_frame()
        {
            using var registry = new TickRegistry(new FrameTime());
            var removed = new CounterClient();
            var added = new CounterClient();
            var first = new CounterClient();
            first.Action = () =>
            {
                registry.Unregister(removed);
                registry.Register(added);
                first.Action = null;
            };
            registry.Register(first);
            registry.Register(removed);
            registry.Tick();
            Assert.AreEqual(0, removed.Count);
            Assert.AreEqual(0, added.Count);
            registry.Tick();
            Assert.AreEqual(2, first.Count);
            Assert.AreEqual(0, removed.Count);
            Assert.AreEqual(1, added.Count);
        }

        [Test]
        public void Reenabling_a_removed_client_during_a_tick_does_not_revive_its_old_registration()
        {
            using var registry = new TickRegistry(new FrameTime());
            var reenabling = new CounterClient();
            var first = new CounterClient();
            first.Action = () =>
            {
                registry.Unregister(reenabling);
                registry.Register(reenabling);
                first.Action = null;
            };
            registry.Register(first);
            registry.Register(reenabling);
            registry.Tick();
            Assert.AreEqual(0, reenabling.Count);
            registry.Tick();
            Assert.AreEqual(1, reenabling.Count);
            registry.Unregister(reenabling);
            registry.Tick();
            Assert.AreEqual(1, reenabling.Count);
        }

        [Test]
        public void One_unexpected_client_exception_does_not_drop_other_views_frames()
        {
            using var registry = new TickRegistry(new FrameTime());
            var broken = new CounterClient { Action = () => throw new InvalidOperationException("Tick failed") };
            var healthy = new CounterClient();
            registry.Register(broken);
            registry.Register(healthy);
            LogAssert.Expect(LogType.Exception, new Regex("Tick failed"));
            registry.Tick();
            Assert.AreEqual(1, healthy.Count);
        }

        [Test]
        public void Registry_disposal_during_a_frame_stops_remaining_clients()
        {
            var registry = new TickRegistry(new FrameTime());
            var remaining = new CounterClient();
            registry.Register(new CounterClient { Action = registry.Dispose });
            registry.Register(remaining);
            registry.Tick();
            registry.Tick();
            registry.Unregister(remaining);
            Assert.AreEqual(0, remaining.Count);
            Assert.Throws<ObjectDisposedException>(() => registry.Register(remaining));
        }

        [Test]
        public void Registry_steady_frames_allocate_no_managed_memory()
        {
            using var registry = new TickRegistry(new FrameTime());
            registry.Register(new CounterClient());
            for (var i = 0; i < 16; i++)
                registry.Tick();
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 512; i++)
                registry.Tick();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated);
        }

        [Test]
        public void Screen_changes_publish_one_coherent_snapshot_including_size_changes()
        {
            var source = new Metrics { SafeArea = new Rect(10, 20, 80, 160), Size = new Vector2Int(100, 200) };
            using var screen = new ScreenService(source);
            var changes = 0;
            screen.SafeAreaChanged += () =>
            {
                Assert.AreEqual(source.SafeArea, screen.SafeArea);
                Assert.AreEqual(source.Size, screen.Size);
                changes++;
            };
            screen.Tick();
            Assert.AreEqual(0, changes);
            source.Size = new Vector2Int(200, 400);
            screen.Tick();
            Assert.AreEqual(1, changes);
            source.SafeArea = new Rect(20, 40, 160, 320);
            screen.Tick();
            screen.Tick();
            Assert.AreEqual(2, changes);
            screen.Dispose();
            source.Size = Vector2Int.one;
            screen.Tick();
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void Safe_area_layout_subscribes_once_and_releases_its_event_on_disable()
        {
            var root = new GameObject("Safe area test", typeof(RectTransform), typeof(SafeAreaFitter));
            var screen = new TestScreen();
            var fitter = root.GetComponent<SafeAreaFitter>();
            try
            {
                fitter.BindScreen(screen);
                fitter.BindScreen(screen);
                Assert.AreEqual(1, screen.Subscribers);
                var rect = root.GetComponent<RectTransform>();
                Assert.AreEqual(new Vector2(0.1f, 0.1f), rect.anchorMin);
                Assert.AreEqual(new Vector2(0.9f, 0.9f), rect.anchorMax);
                InvokeLifecycle(fitter, "OnDisable");
                Assert.AreEqual(0, screen.Subscribers);
                screen.SafeArea = new Rect(0, 0, 200, 400);
                screen.Notify();
                Assert.AreEqual(new Vector2(0.1f, 0.1f), rect.anchorMin);
                InvokeLifecycle(fitter, "OnEnable");
                Assert.AreEqual(1, screen.Subscribers);
                Assert.AreEqual(Vector2.zero, rect.anchorMin);
                Assert.AreEqual(Vector2.one, rect.anchorMax);
                InvokeLifecycle(fitter, "OnDisable");
                fitter.BindScreen(new TestScreen());
                Assert.AreEqual(0, screen.Subscribers);
            }
            finally
            {
                InvokeLifecycle(fitter, "OnDestroy");
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void View_binding_reaches_the_safe_area_wrapper_above_the_view()
        {
            var wrapper = new GameObject("Safe wrapper", typeof(RectTransform), typeof(SafeAreaFitter));
            var screen = new TestScreen();
            try
            {
                var view = new GameObject("View", typeof(RectTransform));
                view.transform.SetParent(wrapper.transform, false);
                var bindings = new UiLifecycleBinder(new RecordingRegistry(), screen);
                bindings.Bind(view);
                bindings.Bind(view);
                Assert.AreEqual(1, screen.Subscribers);
                Assert.AreEqual(new Vector2(0.1f, 0.1f), wrapper.GetComponent<RectTransform>().anchorMin);
            }
            finally
            {
                InvokeLifecycle(wrapper.GetComponent<SafeAreaFitter>(), "OnDestroy");
                UnityEngine.Object.DestroyImmediate(wrapper);
            }
        }

        [Test]
        public void Hold_completion_and_cancelled_hold_rewind_follow_the_supplied_unscaled_delta()
        {
            var root = new GameObject("Hold test", typeof(RectTransform), typeof(HoldButton));
            var button = root.GetComponent<HoldButton>();
            var time = new FrameTime { Delta = 0.2f };
            using var registry = new TickRegistry(time);
            try
            {
                var serialized = new UnityEditor.SerializedObject(button);
                serialized.FindProperty("punchEnabled").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                button.HoldSeconds = 0.4f;
                button.BindTicks(registry);
                button.BindTicks(registry);
                var completed = 0;
                var cancelled = 0;
                button.HoldCompleted += () => completed++;
                button.HoldCancelled += () => cancelled++;
                var pointer = new PointerEventData(null) { button = PointerEventData.InputButton.Left };
                button.OnPointerDown(pointer);
                registry.Tick();
                Assert.AreEqual(0.5f, button.Progress);
                button.OnPointerUp(pointer);
                time.Delta = 0.125f;
                registry.Tick();
                Assert.AreEqual(0, button.Progress);
                Assert.AreEqual(1, cancelled);
                time.Delta = 0.2f;
                button.OnPointerDown(pointer);
                registry.Tick();
                registry.Tick();
                registry.Tick();
                Assert.AreEqual(1, completed);
                Assert.AreEqual(1, button.Progress);
                button.OnPointerUp(pointer);
                Assert.AreEqual(1, cancelled);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(typeof(HoldButton))]
        [TestCase(typeof(UiClickSounds))]
        [TestCase(typeof(IntroVideoView))]
        public void Dynamic_clients_rebind_and_unregister_without_container_access(Type type)
        {
            var root = new GameObject("Lifecycle test", typeof(RectTransform));
            var component = (MonoBehaviour)root.AddComponent(type);
            var client = (ITickRegistryClient)component;
            var first = new RecordingRegistry();
            var second = new RecordingRegistry();
            try
            {
                client.BindTicks(first);
                client.BindTicks(first);
                Assert.AreEqual(1, first.Clients.Count);
                client.BindTicks(second);
                Assert.AreEqual(0, first.Clients.Count);
                Assert.AreEqual(1, second.Clients.Count);
                InvokeLifecycle(component, "OnDisable");
                Assert.AreEqual(0, second.Clients.Count);
                InvokeLifecycle(component, "OnEnable");
                Assert.AreEqual(1, second.Clients.Count);
                InvokeLifecycle(component, "OnDestroy");
                Assert.AreEqual(0, second.Clients.Count);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Intro_clamps_large_frame_steps_like_the_original_update_loop()
        {
            var root = new GameObject("Intro timing test", typeof(IntroVideoView));
            try
            {
                var view = root.GetComponent<IntroVideoView>();
                view.Tick(10);
                var clock = typeof(IntroVideoView).GetField("_clock", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.AreEqual(0.1f, (float)clock.GetValue(view));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void InvokeLifecycle(MonoBehaviour component, string name)
        {
            var method = component.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(method, name);
            method.Invoke(component, null);
        }

        /// <summary>Provides controllable elapsed time and records sample count.</summary>
        private sealed class FrameTime : IFrameTimeSource
        {
            public float Delta = 0.125f;
            public int Reads;
            public float UnscaledDeltaTime
            {
                get
                {
                    Reads++;
                    return Delta;
                }
            }
        }

        /// <summary>Records ticks without allocating in the steady frame path.</summary>
        private sealed class CounterClient : IFrameTickable
        {
            public int Count;
            public float LastDelta;
            public Action Action;
            public void Tick(float deltaTime)
            {
                Count++;
                LastDelta = deltaTime;
                Action?.Invoke();
            }
        }

        /// <summary>Supplies mutable native metrics for deterministic layout tests.</summary>
        private sealed class Metrics : IScreenMetrics
        {
            public Rect SafeArea
            {
                get; set;
            }
            public Vector2Int Size
            {
                get; set;
            }
        }

        /// <summary>Records event ownership independently from the frame service.</summary>
        private sealed class TestScreen : IScreenService
        {
            private Action _changed;
            public Rect SafeArea { get; set; } = new Rect(20, 40, 160, 320);
            public Vector2Int Size => new Vector2Int(200, 400);
            public int Subscribers
            {
                get; private set;
            }
            public event Action SafeAreaChanged
            {
                add
                {
                    _changed += value;
                    Subscribers++;
                }
                remove
                {
                    _changed -= value;
                    Subscribers--;
                }
            }
            public void Notify() => _changed?.Invoke();
        }

        /// <summary>Records explicit registrations without an engine loop.</summary>
        private sealed class RecordingRegistry : ITickRegistry
        {
            public readonly HashSet<IFrameTickable> Clients = new HashSet<IFrameTickable>();
            public void Register(IFrameTickable client) => Clients.Add(client);
            public void Unregister(IFrameTickable client) => Clients.Remove(client);
        }
    }
}
