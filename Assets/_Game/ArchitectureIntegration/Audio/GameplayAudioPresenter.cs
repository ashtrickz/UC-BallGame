using System;
using System.Collections.Generic;
using Game.Audio;
using Game.Gameplay.View;
using UnityEngine;

namespace Game.Integration.Audio
{
    /// <summary>Owns one gameplay scene's sounds; view contacts drive the actual one-shots.</summary>
    public sealed class GameplayAudioPresenter : IDisposable
    {
        private readonly GameplayStage _stage;
        private readonly IAudioService _audio;
        private readonly List<AudioPlaybackHandle> _owned = new List<AudioPlaybackHandle>();
        private AudioPlaybackHandle _chargeBody, _chargeTension, _flight;
        private float _bodyPitch, _tensionPitch;
        private bool _paused, _pendingStart, _defeating, _shotHits = true, _warned;

        public GameplayAudioPresenter(GameplayStage stage, IAudioService audio)
        {
            _stage = stage;
            _audio = audio;
            stage.Slime.PresentationCue += OnSlimeCue;
            stage.Crystals.Shattered += OnCrystalShattered;
            stage.Doors.Opening += OnDoorOpening;
        }

        public void BeginAttempt()
        {
            StopOwned();
            _defeating = _warned = false;
            _shotHits = true;
            _pendingStart = _paused;
            _audio.ChangeTheme(MusicTheme.Gameplay);
            if (!_paused) Play(GameSound.RoundStart);
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
            _audio.SetGameplayPaused(paused);
            if (!paused && _pendingStart) { _pendingStart = false; Play(GameSound.RoundStart); }
        }

        public void PanelChanged(bool open) => _audio.PlaySound(open ? GameSound.UiPanelOpen : GameSound.UiPanelClose);

        public void BeginCharge()
        {
            EndCharge();
            Play(GameSound.ChargeStart);
            _chargeBody = Play(GameSound.ChargeLoop, 0.7f);
            _chargeTension = Play(GameSound.ChargeTensionLoop, 0);
            _bodyPitch = _chargeBody?.Pitch ?? 1;
            _tensionPitch = _chargeTension?.Pitch ?? 1;
        }

        public void UpdateCharge(float normalized, float budgetLeft)
        {
            normalized = Mathf.Clamp01(normalized);
            _chargeBody?.SetVolume(Mathf.Lerp(0.7f, 1f, normalized));
            _chargeBody?.SetPitch(_bodyPitch * Mathf.Lerp(1, 1.08f, normalized));
            _chargeTension?.SetVolume(normalized * 0.6f);
            _chargeTension?.SetPitch(_tensionPitch * Mathf.Lerp(1, 1.08f, normalized));
            if (budgetLeft > 0.2f) _warned = false;
            if (!_warned && budgetLeft <= 0.15f) { _warned = true; Play(GameSound.ResourceWarning); }
        }

        public void CancelCharge()
        {
            var hadCharge = _chargeBody != null || _chargeTension != null;
            EndCharge();
            if (hadCharge && !_paused) Play(GameSound.ChargeCancel);
        }

        private void EndCharge() { Stop(ref _chargeBody); Stop(ref _chargeTension); }

        public void PrepareShot(bool hits)
        {
            EndCharge();
            _shotHits = hits;
        }

        public void ClearingFinished(bool hadHits)
        {
            if (hadHits) Play(GameSound.CrystalDebris);
        }

        public void BeginDefeat() { _defeating = true; EndCharge(); Stop(ref _flight); }
        public void Won() => Play(GameSound.Victory);
        public void Lost() => Play(GameSound.Defeat);

        private void OnSlimeCue(SlimePresentationCue cue)
        {
            if (_paused) return;
            switch (cue)
            {
                case SlimePresentationCue.HopPreparing: Play(GameSound.SlimeSquash); break;
                case SlimePresentationCue.Takeoff: Play(GameSound.SlimeJump, _defeating ? 0.55f : 1); break;
                case SlimePresentationCue.Landed: Play(GameSound.SlimeLand, _defeating ? 0.55f : 1); break;
                case SlimePresentationCue.ShotReleased:
                    Play(GameSound.SlimeShoot);
                    Stop(ref _flight);
                    _flight = Play(GameSound.ProjectileFlightLoop);
                    break;
                case SlimePresentationCue.ShotImpact:
                    Stop(ref _flight);
                    Play(_shotHits ? GameSound.ProjectileImpact : GameSound.ProjectileFizzle);
                    break;
                case SlimePresentationCue.DefeatPreparing: Play(GameSound.FailSqueeze); break;
                case SlimePresentationCue.Popped: Play(GameSound.FailPop); break;
                case SlimePresentationCue.PoolSplash: Play(GameSound.ProjectileImpact, 0.7f); break;
            }
        }

        private void OnCrystalShattered(bool large)
        {
            if (!_paused) Play(large ? GameSound.CrystalBreakLarge : GameSound.CrystalBreakSmall, 0.7f);
        }

        private void OnDoorOpening() { if (!_paused) Play(GameSound.GateOpen); }

        private AudioPlaybackHandle Play(GameSound sound, float volume = 1)
        {
            if (_paused) return null;
            for (var i = _owned.Count - 1; i >= 0; i--)
                if (!_owned[i].IsActive || (!_owned[i].IsPlaying && !_owned[i].IsPaused)) _owned.RemoveAt(i);
            var handle = _audio.PlaySound(sound, volume);
            if (handle != null) _owned.Add(handle);
            return handle;
        }

        private static void Stop(ref AudioPlaybackHandle handle) { handle?.Stop(); handle = null; }

        private void StopOwned()
        {
            foreach (var handle in _owned) handle.Stop();
            _owned.Clear();
            _chargeBody = _chargeTension = _flight = null;
        }

        public void Dispose()
        {
            _stage.Slime.PresentationCue -= OnSlimeCue;
            _stage.Crystals.Shattered -= OnCrystalShattered;
            _stage.Doors.Opening -= OnDoorOpening;
            StopOwned();
            try
            {
                _audio.SetGameplayPaused(false);
                _audio.ChangeTheme(MusicTheme.MainMenu);
            }
            catch (ObjectDisposedException) { } // The persistent root can be destroyed before a child scene.
        }
    }
}
