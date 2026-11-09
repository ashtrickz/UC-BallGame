using System;

namespace Game.Audio
{
    /// <summary>Native feedback presets shared by UI and gameplay integration.</summary>
    public enum HapticKind
    {
        Tap,
        Success
    }

    /// <summary>Owns platform feedback and cancellable charge envelopes for the project lifetime.</summary>
    public interface IHaptics : IDisposable
    {
        bool Enabled
        {
            get;
            set;
        }
        void Play(HapticKind kind);
        void Vibrate(int milliseconds, float intensity);
        void StartCharge(float initialCharge, float chargePerSecond, float seconds);
        void Cancel();
    }
}
