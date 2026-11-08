using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Gameplay.Config;
using Game.Integration.Game;

namespace Game.Integration.Menu
{
    /// <summary>Picks a campaign level: returns the chosen level number, or null when the player goes back.</summary>
    public interface ILevelSelect
    {
        UniTask<int?> ChooseAsync(CancellationToken cancellationToken);
    }

    /// <summary>Shows the level grid with the saved progress; only unlocked levels can be chosen.</summary>
    public sealed class LevelSelectController : ILevelSelect, IDisposable
    {
        private readonly LevelSelectView _view;
        private readonly ICampaignProgress _progress;
        private readonly SlimeGameConfig _config;
        private UniTaskCompletionSource<int?> _pending;

        public LevelSelectController(LevelSelectView view, ICampaignProgress progress, SlimeGameConfig config)
        {
            _view = view ? view : throw new ArgumentNullException(nameof(view));
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _config = config ? config : throw new ArgumentNullException(nameof(config));
            _view.LevelChosen += OnChosen;
            _view.BackRequested += OnBack;
            _view.Hide();
        }

        public int Count => _config.Campaign != null ? _config.Campaign.Count : 0;

        public async UniTask<int?> ChooseAsync(CancellationToken cancellationToken)
        {
            _pending?.TrySetResult(null);
            var pending = _pending = new UniTaskCompletionSource<int?>();
            _view.Show(Count, Math.Min(Math.Max(1, _progress.Unlocked), Count + 1));
            try
            {
                return await pending.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                if (_pending == pending) _pending = null;
                if (_view) _view.Hide();
            }
        }

        private void OnChosen(int level)
        {
            if (level < 1 || level > Count || level > _progress.Unlocked) return;
            _pending?.TrySetResult(level);
        }

        private void OnBack() => _pending?.TrySetResult(null);

        public void Dispose()
        {
            _pending?.TrySetResult(null);
            if (_view)
            {
                _view.LevelChosen -= OnChosen;
                _view.BackRequested -= OnBack;
            }
        }
    }
}
