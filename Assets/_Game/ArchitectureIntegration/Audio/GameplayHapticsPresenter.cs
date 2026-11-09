using System;
using Game.Gameplay.View;

namespace Game.Integration.Audio
{
    /// <summary>Gameplay vibration is independent of audio volume and of whether an audio library is assigned.</summary>
    public sealed class GameplayHapticsPresenter : IDisposable
    {
        private readonly Action<int, float> _pulse;
        private readonly Action<float, float, float> _charge;
        private readonly Action _cancel;
        private bool _paused, _charging, _disposed;

        public GameplayHapticsPresenter(Action<int, float> pulse = null,
            Action<float, float, float> charge = null, Action cancel = null)
        {
            _pulse = pulse ?? Haptics.Vibrate;
            _charge = charge ?? Haptics.StartCharge;
            _cancel = cancel ?? Haptics.Cancel;
        }

        public void OnSlimeCue(SlimePresentationCue cue)
        {
            if (_paused || _disposed) return;
            if (cue == SlimePresentationCue.Landed) _pulse(22, 0.45f);
            else if (cue == SlimePresentationCue.PoolSplash) _pulse(28, 0.55f);
        }

        public void BeginCharge(float initialCharge, float chargePerSecond, float seconds)
        {
            CancelCharge();
            if (_paused || _disposed) return;
            _charging = true;
            _charge(initialCharge, chargePerSecond, seconds);
        }

        public void CancelCharge()
        {
            if (!_charging) return;
            _charging = false;
            _cancel();
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
            if (paused)
            {
                _charging = false;
                _cancel();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _charging = false;
            _cancel();
        }
    }
}
