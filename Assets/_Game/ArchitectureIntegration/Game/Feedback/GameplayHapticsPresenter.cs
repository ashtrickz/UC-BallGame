using System;
using Game.Audio;
using Game.Gameplay.View;

namespace Game.Integration.Audio
{
    /// <summary>Drives gameplay feedback independently from audio volume and library availability.</summary>
    public sealed class GameplayHapticsPresenter : IDisposable
    {
        private const int LandingMilliseconds = 22;
        private const float LandingIntensity = 0.45f;
        private const int SplashMilliseconds = 28;
        private const float SplashIntensity = 0.55f;
        private readonly IHaptics _haptics;
        private bool _paused;
        private bool _charging;
        private bool _disposed;

        public GameplayHapticsPresenter(IHaptics haptics)
        {
            _haptics = haptics;
        }

        public void OnSlimeCue(SlimePresentationCue cue)
        {
            if (_paused || _disposed)
                return;

            if (cue == SlimePresentationCue.Landed)
                _haptics.Vibrate(LandingMilliseconds, LandingIntensity);
            else if (cue == SlimePresentationCue.PoolSplash)
                _haptics.Vibrate(SplashMilliseconds, SplashIntensity);
        }

        public void BeginCharge(float initialCharge, float chargePerSecond, float seconds)
        {
            CancelCharge();
            if (_paused || _disposed)
                return;

            _charging = true;
            _haptics.StartCharge(initialCharge, chargePerSecond, seconds);
        }

        public void CancelCharge()
        {
            if (!_charging)
                return;

            _charging = false;
            _haptics.Cancel();
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
            if (paused)
            {
                _charging = false;
                _haptics.Cancel();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _charging = false;
            _haptics.Cancel();
        }
    }
}
