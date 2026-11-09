using Slime.Audio;

namespace Slime.Infrastructure.Audio
{
    /// <summary>Provides harmless feedback calls on platforms without a native implementation.</summary>
    public sealed class NullHaptics : IHaptics
    {
        public bool Enabled { get; set; } = true;

        public void Play(Slime.Audio.HapticKind kind)
        {
        }

        public void Vibrate(int milliseconds, float intensity)
        {
        }

        public void StartCharge(float initialCharge, float chargePerSecond, float seconds)
        {
        }

        public void Cancel()
        {
        }

        public void Dispose()
        {
        }
    }
}
