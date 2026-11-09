using System;
using Slime.Gameplay.Core.Levels;

namespace Slime.App.Game
{
    public enum GamePhase { Menu, Playing }

    /// <summary>What the menu started: the fixed campaign levels or the generated crystal wall.</summary>
    public enum GameMode { Campaign, Wall }

    /// <summary>
    /// Project-lifetime round state: the level request chosen in the menu survives the Menu and Game scene change.
    /// A null request means "continue the campaign from the saved progress".
    /// </summary>
    public sealed class GameSession
    {
        private const int ContinueCampaign = 0;

        public GamePhase Phase { get; private set; } = GamePhase.Menu;

        public int RoundNumber { get; private set; }

        public LevelRequest Request { get; private set; }

        public GameMode Mode => Request is WallLevelRequest ? GameMode.Wall : GameMode.Campaign;

        public int RequestedLevel => Request is CampaignLevelRequest campaign ? campaign.Number : ContinueCampaign;

        public event Action Changed;

        public event Action RoundStarted;

        public void StartRound(LevelRequest request)
        {
            if (Phase == GamePhase.Playing)
                return;

            Request = request;
            RoundNumber++;
            Phase = GamePhase.Playing;
            Changed?.Invoke();
            RoundStarted?.Invoke();
        }

        // Mode-based entry used by the campaign continuation in GameController and by the PlayMode tests.
        public void StartRound(GameMode mode = GameMode.Campaign, int level = ContinueCampaign)
        {
            StartRound(ToRequest(mode, level));
        }

        public void ReturnToMenu()
        {
            if (Phase == GamePhase.Menu)
                return;

            Phase = GamePhase.Menu;
            Changed?.Invoke();
        }

        private static LevelRequest ToRequest(GameMode mode, int level)
        {
            if (mode == GameMode.Wall)
                return new WallLevelRequest(null);

            return level > ContinueCampaign ? new CampaignLevelRequest(level) : null;
        }
    }
}
