using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Slime.Foundation;
using NUnit.Framework;
using Slime.App.Scenes;
using UnityEngine.TestTools;

namespace Slime.Tests.EditMode
{
    public sealed class SceneTransitionTests
    {
        private sealed class Runtime : ISceneRuntime
        {
            public string ActiveScene { get; private set; } = "Game";
            public string BootstrapScene => "Loading";
            public readonly HashSet<string> Loaded = new HashSet<string> {"Loading", "Game"};
            public readonly List<string> Calls = new List<string>();
            public UniTaskCompletionSource LoadGate;
            public Exception ReadyFailure;
            public Action OnLoad;
            public int MaxGameScenes = 1;
            public bool IsLoaded(string name) => Loaded.Contains(name);
            public string EnsureAnchor()
            {
                if (Loaded.Contains(BootstrapScene)) return BootstrapScene;
                if (Loaded.Add("Transit")) Calls.Add("anchor:Transit");
                return "Transit";
            }
            public void ValidateDestination(string name) { if (string.IsNullOrEmpty(name) || name == BootstrapScene || name == "Missing") throw new InvalidOperationException("Invalid destination"); }
            public void Activate(string name) { Assert.IsTrue(IsLoaded(name)); Calls.Add("activate:"+name); ActiveScene = name; }
            public async UniTask LoadAsync(string name, IProgress<float> progress)
            {
                Calls.Add("load:"+name); Loaded.Add(name); MaxGameScenes = Math.Max(MaxGameScenes, Loaded.Count - 1);
                OnLoad?.Invoke();
                if (LoadGate != null) await LoadGate.Task;
            }
            public UniTask WaitUntilReadyAsync(string name, CancellationToken token)
            { token.ThrowIfCancellationRequested(); if (ReadyFailure != null) throw ReadyFailure; Calls.Add("ready:"+name); return UniTask.CompletedTask; }
            public UniTask UnloadAsync(string name) { Calls.Add("unload:"+name); Loaded.Remove(name); return UniTask.CompletedTask; }
        }
        [UnityTest] public IEnumerator Old_scope_is_removed_before_new_scope_and_repeat_is_noop() => UniTask.ToCoroutine(async () =>
        {
            var r = new Runtime(); var transition = new SceneTransition(r);
            await transition.LoadAsync("SecondGame", default);
            CollectionAssert.AreEqual(new[] {"activate:Loading", "unload:Game", "load:SecondGame", "ready:SecondGame", "activate:SecondGame", "unload:Loading"}, r.Calls);
            Assert.AreEqual(1,r.MaxGameScenes);
            CollectionAssert.AreEquivalent(new[] {"SecondGame"}, r.Loaded, "the bootstrap scene goes once the game scene is up");
            int calls=r.Calls.Count; await transition.LoadAsync("SecondGame", default); Assert.AreEqual(calls,r.Calls.Count);
            // With the bootstrap scene gone, the next transition stands on an empty transit scene, which goes too.
            r.Calls.Clear();
            await transition.LoadAsync("Game", default);
            CollectionAssert.AreEqual(new[] {"anchor:Transit", "activate:Transit", "unload:SecondGame", "load:Game", "ready:Game", "activate:Game", "unload:Transit"}, r.Calls);
            CollectionAssert.AreEquivalent(new[] {"Game"}, r.Loaded);
            Assert.AreEqual(1,r.MaxGameScenes);
        });
        [UnityTest] public IEnumerator Failed_readiness_cleans_destination_and_can_retry() => UniTask.ToCoroutine(async () =>
        {
            var r=new Runtime {ReadyFailure=new InvalidOperationException("broken content")}; var t=new SceneTransition(r);
            try { await t.LoadAsync("SecondGame",default); Assert.Fail(); } catch(InvalidOperationException e) { Assert.AreEqual("broken content",e.Message); }
            CollectionAssert.AreEquivalent(new[] {"Loading"},r.Loaded); Assert.AreEqual("Loading",r.ActiveScene);
            r.ReadyFailure=null; await t.LoadAsync("SecondGame",default); Assert.AreEqual("SecondGame",r.ActiveScene);
        });
        [UnityTest] public IEnumerator Cancellation_during_uncancellable_load_waits_cleans_and_retains_exclusive_ownership() => UniTask.ToCoroutine(async () =>
        {
            var r=new Runtime {LoadGate=new UniTaskCompletionSource()};var t=new SceneTransition(r);using var cts=new CancellationTokenSource();
            var pending=t.LoadAsync("SecondGame",cts.Token);cts.Cancel();
            Assert.IsTrue(r.IsLoaded("SecondGame"));
            try { await t.LoadAsync("Other",default); Assert.Fail(); } catch(InvalidOperationException) { }
            r.LoadGate.TrySetResult();
            try { await pending; Assert.Fail(); } catch(OperationCanceledException) { }
            CollectionAssert.AreEquivalent(new[] {"Loading"},r.Loaded);
            r.LoadGate=null;await t.LoadAsync("Game",default);Assert.AreEqual("Game",r.ActiveScene);
        });
        [UnityTest] public IEnumerator Invalid_destination_and_precancelled_request_leave_current_scene_intact() => UniTask.ToCoroutine(async () =>
        {
            var r=new Runtime();var t=new SceneTransition(r);
            foreach(var name in new[] {"Missing","Loading", ""})
                try {await t.LoadAsync(name,default);Assert.Fail();} catch(InvalidOperationException) { }
            using var cts=new CancellationTokenSource();cts.Cancel();
            try {await t.LoadAsync("SecondGame",cts.Token);Assert.Fail();} catch(OperationCanceledException) { }
            Assert.IsEmpty(r.Calls);Assert.IsTrue(r.IsLoaded("Game"));
        });
        [UnityTest] public IEnumerator Failure_after_partial_load_is_cleaned_up() => UniTask.ToCoroutine(async () =>
        {
            var r=new Runtime {OnLoad=()=>throw new InvalidOperationException("load failed")};var t=new SceneTransition(r);
            try {await t.LoadAsync("SecondGame",default);Assert.Fail();} catch(InvalidOperationException) { }
            CollectionAssert.AreEquivalent(new[] {"Loading"},r.Loaded);
        });
    }
}
