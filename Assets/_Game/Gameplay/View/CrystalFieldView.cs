using System.Collections.Generic;
using System.Threading;
using System;
using Cysharp.Threading.Tasks;
using Slime.Foundation;
using Slime.Gameplay.Config;
using Slime.Gameplay.Core.Config;
using Slime.Gameplay.Core.Level;
using UnityEngine;

namespace Slime.Gameplay.View
{
    /// <summary>
    /// The level's crystals by id, rented from the crystal view pool and returned on the next build. The shot preview
    /// is drawn by <see cref="CrystalHighlighter"/>, the clearing wave by <see cref="CrystalShatterAnimator"/>.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Slime.Gameplay.View", sourceAssembly: "Game.Gameplay.View", sourceClassName: "CrystalFieldView")]
    public sealed class CrystalFieldView : MonoBehaviour, IFrameTickable
    {
        [SerializeField] private SplashPool debris;

        private readonly List<CrystalView> _crystals = new List<CrystalView>();

        private ICrystalViewFactory _views;
        private CrystalHighlighter _highlighter;
        private CrystalShatterAnimator _shatter;

        public event Action<bool> Shattered;

        public LevelData Level { get; private set; }

        public ICrystalViewFactory Views => _views ??= new CrystalViewPool(transform);

        public void Bind(GameClock clock, SlimeViewConfig config)
        {
            if (_shatter != null)
                _shatter.Shattered -= OnShattered;

            var tinter = new CrystalTinter(config);
            _highlighter = new CrystalHighlighter(_crystals, tinter, config);
            _shatter = new CrystalShatterAnimator(_crystals, tinter, _highlighter, debris, clock, config);
            _shatter.Shattered += OnShattered;
        }

        public void Build(LevelData level, CrystalCatalog catalog)
        {
            Clear();
            Level = level;
            foreach (var placement in level.Crystals)
                _crystals.Add(Views.Rent(placement, catalog));
        }

        public void Clear()
        {
            foreach (var crystal in _crystals)
                Views.Return(crystal);

            _crystals.Clear();
            _highlighter?.Reset();
            if (debris != null)
                debris.Clear();

            Level = null;
        }

        public bool IsDestroyed(int id)
        {
            return id >= 0 && id < _crystals.Count && _crystals[id].Destroyed;
        }

        public void SetPreview(IReadOnlyList<int> ids)
        {
            _highlighter.Show(ids, false);
        }

        public void CommitPreview(IReadOnlyList<int> ids)
        {
            _highlighter.Show(ids, true);
        }

        public UniTask ClearAsync(IReadOnlyList<int> ids, Vector3 center, float radius, CancellationToken token)
        {
            return _shatter.ClearAsync(ids, center, radius, token);
        }

        public void Tick(float deltaTime)
        {
            _highlighter?.Tick(deltaTime);
            if (debris != null)
                debris.Tick(deltaTime);
        }

        private void OnShattered(bool big)
        {
            Shattered?.Invoke(big);
        }

        private void OnDestroy()
        {
            if (_shatter != null)
                _shatter.Shattered -= OnShattered;
        }
    }
}
