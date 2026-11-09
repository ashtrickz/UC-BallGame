using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Slime.App.Loading
{
    /// <summary>The company's intro over the loading screen: the game loads underneath while it plays.</summary>
    public interface IIntroScreen
    {
        bool IsOver { get; }
        UniTask WaitAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>No intro (none in the build, or a test): nothing to wait for.</summary>
    public sealed class NoIntroScreen : IIntroScreen
    {
        public bool IsOver => true;
        public UniTask WaitAsync(CancellationToken cancellationToken = default) => UniTask.CompletedTask;
    }

    /// <summary>
    /// What stands between "everything is loaded" and opening the game: the intro's end, and, when loading was done before it, one more second
    /// of the full loading bar, so the loading screen does not flash for a frame after the intro fades into it. When the intro ended first, the
    /// player has watched the real loading already and no second is added.
    /// </summary>
    public static class IntroGate
    {
        public const int SettleMs = 1000;

        public static async UniTask PassAsync(IIntroScreen intro, Func<CancellationToken, UniTask> settle, CancellationToken cancellationToken = default)
        {
            if (intro == null)
                throw new ArgumentNullException(nameof(intro));

            if (settle == null)
                throw new ArgumentNullException(nameof(settle));

            if (intro.IsOver)
                return;

            await intro.WaitAsync(cancellationToken);
            await settle(cancellationToken);
        }
    }
}
