using System;

namespace Game.App.Game
{
    public enum GamePhase { Menu, Playing }

    /// <summary>What the menu started: the fixed campaign levels or the generated crystal wall.</summary>
    public enum GameMode { Campaign, Wall }

    // Project lifetime: the selected mode and level survive Menu / Game scene changes.
    public sealed class GameSession
    {
        public GamePhase Phase { get; private set; } = GamePhase.Menu;
        public int RoundNumber { get; private set; }
        public GameMode Mode { get; private set; } = GameMode.Campaign;
        /// <summary>Campaign level chosen in the level grid (1-based), or 0 to continue from the progress.</summary>
        public int RequestedLevel { get; private set; }
        public event Action Changed;
        public event Action RoundStarted;

        public void StartRound(GameMode mode = GameMode.Campaign, int level = 0)
        {
            if (Phase == GamePhase.Playing) return;
            Mode = mode;
            RequestedLevel = level;
            RoundNumber++;
            Phase = GamePhase.Playing;
            Changed?.Invoke();
            RoundStarted?.Invoke();
        }

        public void ReturnToMenu()
        {
            if (Phase == GamePhase.Menu) return;
            Phase = GamePhase.Menu;
            Changed?.Invoke();
        }
    }
}
