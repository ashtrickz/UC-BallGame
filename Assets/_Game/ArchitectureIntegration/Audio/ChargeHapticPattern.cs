using System;
using UnityEngine;

namespace Game.Integration.Audio
{
    /// <summary>A finite native envelope: one submission per hold, with no motor restarts every frame.</summary>
    public sealed class ChargeHapticPattern
    {
        public long[] Timings { get; }
        public int[] Amplitudes { get; }

        private ChargeHapticPattern(long[] timings, int[] amplitudes)
        {
            Timings = timings;
            Amplitudes = amplitudes;
        }

        public static ChargeHapticPattern Build(float initialCharge, float chargePerSecond, float seconds, bool amplitudeControl)
        {
            if (!Finite(initialCharge) || !Finite(chargePerSecond) || !Finite(seconds) || seconds <= 0 || chargePerSecond < 0)
                return new ChargeHapticPattern(Array.Empty<long>(), Array.Empty<int>());
            // A finite upper bound also stops the motor if an interrupted app misses its cancel callback.
            var total = Mathf.Clamp(Mathf.CeilToInt(Mathf.Min(seconds, 10f) * 1000), 1, 10000);
            var step = amplitudeControl ? 20 : 50;
            var count = (total + step - 1) / step;
            var timings = new long[count * (amplitudeControl ? 1 : 2)];
            var amplitudes = new int[timings.Length];
            for (var i = 0; i < count; i++)
            {
                var duration = Math.Min(step, total - i * step);
                var progress = Mathf.Clamp01(initialCharge + chargePerSecond * (i * step / 1000f));
                var eased = progress * progress * (3 - 2 * progress);
                if (amplitudeControl)
                {
                    timings[i] = duration;
                    amplitudes[i] = Mathf.RoundToInt(Mathf.Lerp(26, 184, eased));
                }
                else
                {
                    // On/off-only motors approximate the ramp through increasing duty cycle.
                    var on = Mathf.Clamp(Mathf.RoundToInt(duration * Mathf.Lerp(0.12f, 0.7f, eased)), 1, duration);
                    timings[i * 2] = duration - on;
                    timings[i * 2 + 1] = on;
                    amplitudes[i * 2] = 0;
                    amplitudes[i * 2 + 1] = -1; // Android DEFAULT_AMPLITUDE, never an unsupported custom strength.
                }
            }
            return new ChargeHapticPattern(timings, amplitudes);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
