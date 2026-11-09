using System;
using Game.App.Game;
using Game.Gameplay.Config;
using VContainer.Unity;

namespace Game.Integration.Menu
{
    /// <summary>Slime colour choice in the menu: shows the palette with the saved colour and stores a new choice.</summary>
    public sealed class SlimeColorController : IStartable, IDisposable
    {
        private readonly IMenuView _view;
        private readonly ISlimeColorPreferences _preferences;
        private readonly SlimeGameConfig _config;
        private readonly GameSession _session;

        private bool _started;
        private bool _disposed;

        public SlimeColorController(IMenuView view, ISlimeColorPreferences preferences, SlimeGameConfig config, GameSession session)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
            _config = config ? config : throw new ArgumentNullException(nameof(config));
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public void Start()
        {
            if (_started || _disposed)
                return;

            _started = true;
            _view.SlimeColorRequested += OnColorRequested;
            _view.SetSlimeColors(_config.SlimeColors, _preferences.SelectedIndex(_config.SlimeColors));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _view.SlimeColorRequested -= OnColorRequested;
        }

        private void OnColorRequested(int index)
        {
            if (_disposed || _session.Phase != GamePhase.Menu)
                return;

            if (_preferences.Select(_config.SlimeColors, index))
                _view.SetSelectedSlimeColor(index);
        }
    }
}
