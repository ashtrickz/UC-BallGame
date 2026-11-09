using System;

namespace Game.Integration.Game
{
    /// <summary>The attempt in play and whether a view action chain is running for it.</summary>
    public sealed class CurrentAttempt : IDisposable
    {
        public AttemptContext Context { get; private set; }

        public bool IsBusy { get; set; }

        public bool Is(AttemptContext context)
        {
            return Context == context;
        }

        public void Replace(AttemptContext context)
        {
            Context?.Dispose();
            Context = context;
            IsBusy = false;
        }

        public void Dispose()
        {
            Replace(null);
        }
    }
}
