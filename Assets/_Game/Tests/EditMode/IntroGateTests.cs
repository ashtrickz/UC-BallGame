using Slime.Foundation;
using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Slime.App.Loading;
using UnityEngine.TestTools;

namespace Slime.Tests.EditMode
{
    public sealed class IntroGateTests
    {
        private sealed class Intro : IIntroScreen
        {
            public readonly UniTaskCompletionSource Ended = new UniTaskCompletionSource();
            public bool IsOver { get; set; }
            public UniTask WaitAsync(CancellationToken cancellationToken = default) => Ended.Task.AttachExternalCancellation(cancellationToken);
        }

        [UnityTest]
        public IEnumerator Loading_done_before_the_intro_ended_waits_for_it_then_settles() => UniTask.ToCoroutine(async () =>
        {
            var intro = new Intro();
            int settled = 0;
            var gate = IntroGate.PassAsync(intro, _ => { settled++; return UniTask.CompletedTask; });
            await UniTask.Yield();
            Assert.AreEqual(UniTaskStatus.Pending, gate.Status, "the game must not open under the intro");
            Assert.AreEqual(0, settled, "the extra second starts only after the intro");
            intro.IsOver = true;
            intro.Ended.TrySetResult();
            await gate;
            Assert.AreEqual(1, settled);
        });

        [UnityTest]
        public IEnumerator Intro_ended_first_adds_no_second() => UniTask.ToCoroutine(async () =>
        {
            int settled = 0;
            await IntroGate.PassAsync(new Intro { IsOver = true }, _ => { settled++; return UniTask.CompletedTask; });
            Assert.AreEqual(0, settled);
        });

        [UnityTest]
        public IEnumerator No_intro_adds_no_second() => UniTask.ToCoroutine(async () =>
        {
            int settled = 0;
            await IntroGate.PassAsync(new NoIntroScreen(), _ => { settled++; return UniTask.CompletedTask; });
            Assert.AreEqual(0, settled);
        });

        [UnityTest]
        public IEnumerator Leaving_while_the_intro_plays_cancels_and_never_settles() => UniTask.ToCoroutine(async () =>
        {
            int settled = 0;
            using var cts = new CancellationTokenSource();
            var gate = IntroGate.PassAsync(new Intro(), _ => { settled++; return UniTask.CompletedTask; }, cts.Token);
            cts.Cancel();
            bool cancelled = false;
            try { await gate; }
            catch (OperationCanceledException) { cancelled = true; }
            Assert.IsTrue(cancelled);
            Assert.AreEqual(0, settled);
        });
    }
}
