using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Slime.Gameplay.View
{
    /// <summary>
    /// Wave of infection and shattering outward from the blast centre, with debris bursts. Destroyed crystals stay
    /// hidden until the next attempt; debris never becomes an obstacle.
    /// </summary>
    public sealed class CrystalShatterAnimator
    {
        private const float MinRadius = 0.02f;
        private const float InfectedBrightness = 1.4f;
        private const float TopHeight = 0.45f;
        private const float BaseHeight = 0.15f;
        private const float BigCrystalScale = 1.1f;
        private const float SwellPart = 0.3f;
        private const float SwellAmount = 0.18f;
        private const float SwellPeak = 1.18f;
        private const int TopDebrisMin = 4;
        private const int TopDebrisMax = 8;
        private const float TopSpeedMin = 0.65f;
        private const float TopSpeedMax = 1.35f;
        private const float TopSizeMin = 0.85f;
        private const float TopSizeMax = 1.2f;
        private const int BaseDebrisMin = 2;
        private const int BaseDebrisMax = 4;
        private const float BaseSpeedMin = 0.3f;
        private const float BaseSpeedMax = 0.65f;
        private const float BaseSize = 0.75f;
        private const float NoTint = 0f;

        private readonly IReadOnlyList<CrystalView> _crystals;
        private readonly CrystalTinter _tinter;
        private readonly CrystalHighlighter _highlighter;
        private readonly SplashPool _debris;
        private readonly GameClock _clock;
        private readonly SlimeViewConfig _config;

        public CrystalShatterAnimator(IReadOnlyList<CrystalView> crystals, CrystalTinter tinter, CrystalHighlighter highlighter,
            SplashPool debris, GameClock clock, SlimeViewConfig config)
        {
            _crystals = crystals;
            _tinter = tinter;
            _highlighter = highlighter;
            _debris = debris;
            _clock = clock;
            _config = config;
        }

        public event Action<bool> Shattered;

        public async UniTask ClearAsync(IReadOnlyList<int> ids, Vector3 center, float radius, CancellationToken token)
        {
            _highlighter.Release();
            if (ids == null || ids.Count == 0)
            {
                _highlighter.Show(null, false);
                return;
            }

            foreach (var crystal in _crystals)
            {
                if (!crystal.Destroyed && !crystal.Highlighted)
                    _tinter.Tint(crystal, NoTint, CrystalTinter.Neutral);
            }

            var strength = _config.ImpactStrength(radius);
            var waveDuration = _config.ImpactWaveDuration(radius);
            var tasks = new List<UniTask>(ids.Count);
            foreach (var id in ids)
            {
                var crystal = _crystals[id];
                var flat = crystal.Root.position - center;
                flat.y = 0;
                var delay = Mathf.Clamp01(flat.magnitude / Mathf.Max(MinRadius, radius)) * waveDuration;
                tasks.Add(ShatterAsync(crystal, delay, strength, token));
            }

            await UniTask.WhenAll(tasks);
        }

        private async UniTask ShatterAsync(CrystalView crystal, float delay, float strength, CancellationToken token)
        {
            await _clock.Wait(delay, token);
            crystal.Highlighted = false;
            await _clock.Animate(_config.infectDuration, t =>
            {
                var amount = Mathf.Lerp(_config.previewTint, 1, t);
                var brightness = Mathf.Lerp(_config.previewBrightness, InfectedBrightness, t);
                _tinter.Tint(crystal, amount, brightness);
            }, token);

            var top = crystal.Root.position + Vector3.up * (TopHeight * crystal.Scale.y);
            var material = crystal.Materials.Length > 0
                ? crystal.Materials[0]
                : null;
            Shattered?.Invoke(crystal.Scale.y >= BigCrystalScale);
            if (_debris != null)
            {
                _debris.Burst(
                    top,
                    Mathf.RoundToInt(Mathf.Lerp(TopDebrisMin, TopDebrisMax, strength)),
                    Mathf.Lerp(TopSpeedMin, TopSpeedMax, strength),
                    material,
                    Mathf.Lerp(TopSizeMin, TopSizeMax, strength));
            }

            await _clock.Animate(_config.shatterDuration, t =>
            {
                var scale = t < SwellPart
                    ? 1 + SwellAmount * GameClock.Smooth(t / SwellPart)
                    : SwellPeak * (1 - GameClock.Smooth((t - SwellPart) / (1 - SwellPart)));
                crystal.Root.localScale = crystal.Scale * Mathf.Max(0, scale);
            }, token);

            crystal.Destroyed = true;
            crystal.Root.gameObject.SetActive(false);
            if (_debris != null)
            {
                _debris.Burst(
                    crystal.Root.position + Vector3.up * BaseHeight,
                    Mathf.RoundToInt(Mathf.Lerp(BaseDebrisMin, BaseDebrisMax, strength)),
                    Mathf.Lerp(BaseSpeedMin, BaseSpeedMax, strength),
                    material,
                    BaseSize);
            }
        }
    }
}
