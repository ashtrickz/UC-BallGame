using System;
using System.Threading;
using Game.Gameplay.Config;
using Game.Gameplay.Level;
using Game.Gameplay.Levels;
using Game.Gameplay.Model;

namespace Game.Integration.Game
{
    /// <summary>Identity, built level, model, mode policy and cancellation of one attempt.</summary>
    public sealed class AttemptContext : IDisposable
    {
        private const int NotCampaign = 0;

        private readonly CancellationTokenSource _cancellation;

        public AttemptContext(int id, BuiltLevel built, AttemptModel model, IAttemptModePolicy policy, CancellationToken parent)
        {
            Id = id;
            Built = built ?? throw new ArgumentNullException(nameof(built));
            Model = model ?? throw new ArgumentNullException(nameof(model));
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(parent);
            Token = _cancellation.Token;
        }

        public int Id { get; }

        public BuiltLevel Built { get; }

        public LevelRequest Request => Built.Request;

        public int Seed => Built.Seed;

        public GameRules Rules => Built.Rules;

        public LevelData Level => Built.Level;

        public int LevelNumber => Request is CampaignLevelRequest campaign ? campaign.Number : NotCampaign;

        public bool IsCampaign => Request is CampaignLevelRequest;

        public AttemptModel Model { get; }

        public IAttemptModePolicy Policy { get; }

        public CancellationToken Token { get; }

        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;
            _cancellation.Cancel();
            _cancellation.Dispose();
        }
    }
}
