using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.View
{
    /// <summary>
    /// Shot preview on the crystal field: the predicted crystals pulse with bright edges, the rest dim.
    /// A committed preview stops pulsing and stays during the projectile flight.
    /// </summary>
    public sealed class CrystalHighlighter
    {
        private const float PulseSpeed = 7f;
        private const float PulseAmplitude = 0.06f;
        private const float NoTint = 0f;

        private readonly IReadOnlyList<CrystalView> _crystals;
        private readonly CrystalTinter _tinter;
        private readonly SlimeViewConfig _config;
        private readonly HashSet<int> _highlight = new HashSet<int>();

        private bool _committed;
        private float _time;

        public CrystalHighlighter(IReadOnlyList<CrystalView> crystals, CrystalTinter tinter, SlimeViewConfig config)
        {
            _crystals = crystals;
            _tinter = tinter;
            _config = config;
        }

        public void Show(IReadOnlyList<int> ids, bool committed)
        {
            var wasActive = _highlight.Count > 0;
            var wasCommitted = _committed;
            _highlight.Clear();
            if (ids != null)
            {
                foreach (var id in ids)
                {
                    if (id >= 0 && id < _crystals.Count && !_crystals[id].Destroyed)
                        _highlight.Add(id);
                }
            }

            _committed = committed;
            var active = _highlight.Count > 0;
            for (var i = 0; i < _crystals.Count; i++)
            {
                var crystal = _crystals[i];
                var on = _highlight.Contains(i) && !crystal.Destroyed;
                var unchanged = crystal.Highlighted == on && wasActive == active && wasCommitted == committed;
                if (crystal.Destroyed || unchanged)
                    continue;

                crystal.Highlighted = on;
                if (on)
                {
                    Pulse(crystal);
                }
                else
                {
                    var brightness = active
                        ? _config.surroundingCrystalBrightness
                        : CrystalTinter.Neutral;
                    _tinter.Tint(crystal, NoTint, brightness);
                }
            }
        }

        public void Release()
        {
            _highlight.Clear();
            _committed = false;
        }

        public void Reset()
        {
            Release();
            _time = 0;
        }

        public void Tick(float delta)
        {
            _time += delta;
            if (_highlight.Count == 0)
                return;

            foreach (var id in _highlight)
            {
                if (id < _crystals.Count && !_crystals[id].Destroyed)
                    Pulse(_crystals[id]);
            }
        }

        private void Pulse(CrystalView crystal)
        {
            // Keep the candy colours; the brighter prism edges identify the affected set.
            var pulse = _committed
                ? 0f
                : Mathf.Sin(_time * PulseSpeed) * PulseAmplitude;
            _tinter.Tint(crystal, _config.previewTint, _config.previewBrightness + pulse, true);
        }
    }
}
