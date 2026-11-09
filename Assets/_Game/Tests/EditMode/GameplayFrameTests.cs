using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Gameplay.View;
using Game.Integration.Game;
using NUnit.Framework;
using UnityEngine;

namespace Game.Foundation.Tests
{
    /// <summary>The gameplay frame is driven by the scene clock, and gameplay behaviours have no Unity frame callbacks.</summary>
    public sealed class GameplayFrameTests
    {
        private const float Frame = 0.1f;
        private const float AnimationSeconds = 0.25f;
        private const string GameplayNamespace = "Game.Integration.Game";
        private const string ViewNamespace = "Game.Gameplay.View";


        // The frame recorder is a debug tool that captures at the end of the frame.
        private static readonly HashSet<string> Allowed = new HashSet<string>
        {
            "FrameRecorder.Capture",
        };

        [Test]
        public void Clock_resumes_frame_waiters_inside_its_tick()
        {
            var clock = new GameClock();
            var resumed = 0;
            Wait(clock, () => resumed++).Forget();

            Assert.AreEqual(0, resumed);
            clock.Tick(Frame);
            Assert.AreEqual(1, resumed, "the waiter resumes during Tick, so the frame order is set by the caller");
        }

        [Test]
        public void Paused_clock_keeps_frames_but_stops_time()
        {
            var clock = new GameClock();
            var progress = -1f;
            clock.Animate(AnimationSeconds, t => progress = t, CancellationToken.None).Forget();

            clock.Paused = true;
            clock.Tick(Frame);
            clock.Tick(Frame);
            Assert.AreEqual(0f, progress, "a paused frame advances nothing");

            clock.Paused = false;
            for (var i = 0; i < 3; i++)
                clock.Tick(Frame);

            Assert.AreEqual(1f, progress, "the animation finishes only on unpaused frames");
        }

        [Test]
        public void Stopped_clock_cancels_waiters()
        {
            var clock = new GameClock();
            var cancelled = false;
            WaitCancelled(clock, () => cancelled = true).Forget();

            clock.Stop();

            Assert.IsTrue(cancelled);
        }

        [Test]
        public void Gameplay_behaviours_have_no_coroutines()
        {
            var assemblies = new[] { typeof(GameClock).Assembly, typeof(GameplayFlow).Assembly };
            var offenders = assemblies
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => typeof(MonoBehaviour).IsAssignableFrom(type))
                .Where(type => type.Namespace == GameplayNamespace || type.Namespace == ViewNamespace)
                .SelectMany(Offending)
                .Where(name => !Allowed.Contains(name))
                .ToList();

            CollectionAssert.IsEmpty(offenders);
        }

        private static IEnumerable<string> Offending(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var method in type.GetMethods(flags))
            {
                if (method.ReturnType == typeof(IEnumerator))
                    yield return type.Name + "." + method.Name;
            }
        }

        private static async UniTaskVoid Wait(GameClock clock, Action resumed)
        {
            await clock.NextFrame(CancellationToken.None);
            resumed();
        }

        private static async UniTaskVoid WaitCancelled(GameClock clock, Action cancelled)
        {
            try
            {
                await clock.NextFrame(CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                cancelled();
            }
        }
    }
}
