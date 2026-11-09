using System;
using Cysharp.Threading.Tasks;
using Game.App.Scenes;
using Game.Gameplay.Levels;

namespace Game.App.Game
{
    /// <summary>Starts a round for a level request: writes it into the session and opens the Game scene.</summary>
    public interface IGameLauncher
    {
        UniTask LaunchAsync(LevelRequest request);
    }

    /// <summary>Project-lifetime launcher; navigation survives the menu that asked for the round.</summary>
    public sealed class GameLauncher : IGameLauncher
    {
        private readonly GameSession _session;
        private readonly GameNavigation _navigation;

        public GameLauncher(GameSession session, GameNavigation navigation)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        }

        public async UniTask LaunchAsync(LevelRequest request)
        {
            _session.StartRound(request);
            await _navigation.OpenAsync(GameNavigation.GameScene);
        }
    }
}
