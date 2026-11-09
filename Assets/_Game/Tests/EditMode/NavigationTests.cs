using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.App.Loading;
using Game.App.Scenes;
using Game.Foundation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Application.Tests
{
    public sealed class NavigationTests
    {
        private sealed class Loading : ILoadingScreen
        {
            public event Action RetryRequested { add { } remove { } }
            public bool Visible, CanRetry; public string Error;
            public UniTaskCompletionSource ShowGate, HideGate;
            public int HideCalls;
            public float Percentage;
            public void ShowLoading(float p, string m)
            {
                Visible = true;
                Percentage = p;
            }
            public void HideLoading()
            {
                Visible = false;
            }
            public UniTask ShowLoadingAsync(float p, string m, CancellationToken t = default)
            {
                t.ThrowIfCancellationRequested();
                Visible = true;
                return ShowGate?.Task ?? UniTask.CompletedTask;
            }
            public async UniTask HideLoadingAsync(CancellationToken t = default)
            {
                t.ThrowIfCancellationRequested();
                HideCalls++;
                if (HideGate != null)
                    await HideGate.Task;
                Visible = false;
            }
            public void ShowError(string m, bool retry)
            {
                Visible = true;
                Error = m;
                CanRetry = retry;
            }
        }
        private sealed class Scenes : ISceneService
        {
            public int Calls; public string Last; public bool Fail; public UniTaskCompletionSource Gate;
            public async UniTask LoadSceneAsync(string name, CancellationToken token)
            {
                Calls++;
                Last = name;
                if (Fail)
                    throw new InvalidOperationException("network");
                if (Gate != null)
                    await Gate.Task.AttachExternalCancellation(token);
            }
        }
        [UnityTest]
        public IEnumerator Rapid_navigation_is_single_flight_and_failure_retry_keeps_destination() => UniTask.ToCoroutine(async () =>
        {
            var scenes = new Scenes { Gate = new UniTaskCompletionSource() };
            using var n = new GameNavigation(scenes);
            var first = n.OpenAsync("SecondGame");
            await n.OpenAsync(GameNavigation.GameScene);
            Assert.AreEqual(1, scenes.Calls);
            Assert.IsTrue(n.IsTransitioning);
            scenes.Gate.TrySetResult();
            await first;
            Assert.IsFalse(n.IsTransitioning);
            scenes.Fail = true;
            try
            {
                await n.OpenAsync(GameNavigation.GameScene);
                Assert.Fail();
            }
            catch (InvalidOperationException) { }
            Assert.AreEqual("network", n.LastError);
            scenes.Fail = false;
            scenes.Gate = null;
            await n.RetryAsync();
            Assert.AreEqual(GameNavigation.GameScene, scenes.Last);
            Assert.IsNull(n.LastError);
        });
        [UnityTest]
        public IEnumerator Project_disposal_cancels_navigation_without_leaving_busy_state() => UniTask.ToCoroutine(async () =>
        {
            var scenes = new Scenes { Gate = new UniTaskCompletionSource() };
            var n = new GameNavigation(scenes);
            var pending = n.OpenAsync("SecondGame");
            n.Dispose();
            await pending;
            Assert.IsFalse(n.IsTransitioning);
            try
            {
                await n.OpenAsync(GameNavigation.GameScene);
                Assert.Fail();
            }
            catch (ObjectDisposedException) { }
        });
        [UnityTest]
        public IEnumerator Scene_service_reports_retryable_failure_and_releases_transition_guard() => UniTask.ToCoroutine(async () =>
        {
            var loading = new Loading();
            bool fail = true;
            int calls = 0;
            var service = new SceneService((_, reporter, t) => { calls++; if (fail) throw new InvalidOperationException("content failed"); return UniTask.CompletedTask; }, loading, new InitializationModel());
            try
            {
                await service.LoadSceneAsync("Cooking", default);
                Assert.Fail();
            }
            catch (InvalidOperationException) { }
            Assert.IsTrue(loading.Visible);
            Assert.IsTrue(loading.CanRetry);
            Assert.AreEqual("content failed", loading.Error);
            fail = false;
            await service.LoadSceneAsync("Cooking", default);
            Assert.AreEqual(2, calls);
            Assert.IsFalse(loading.Visible);
        });
        [UnityTest]
        public IEnumerator Scene_service_awaits_cover_then_loading_then_reveal_and_reports_real_progress() => UniTask.ToCoroutine(async () =>
        {
            var loading = new Loading { ShowGate = new UniTaskCompletionSource(), HideGate = new UniTaskCompletionSource() };
            var loaded = new UniTaskCompletionSource();
            IProgress<float> progress = null;
            var calls = 0;
            var service = new SceneService(async (_, reporter, token) =>
            {
                calls++;
                progress = reporter;
                reporter.Report(0.5f);
                await loaded.Task;
            }, loading, new InitializationModel());
            var transition = service.LoadSceneAsync(GameNavigation.GameScene, default);
            Assert.AreEqual(0, calls, "loading cannot start until the cover animation finishes");
            loading.ShowGate.TrySetResult();
            Assert.AreEqual(1, calls);
            Assert.AreEqual(50f, loading.Percentage);
            Assert.AreEqual(0, loading.HideCalls);
            loaded.TrySetResult();
            await UniTask.WaitUntil(() => loading.HideCalls == 1).Timeout(TimeSpan.FromSeconds(2));
            Assert.AreEqual(100f, loading.Percentage);
            Assert.AreEqual(1, loading.HideCalls);
            Assert.AreEqual(UniTaskStatus.Pending, transition.Status);
            loading.HideGate.TrySetResult();
            await transition;
            Assert.IsFalse(loading.Visible);
        });
    }
}
