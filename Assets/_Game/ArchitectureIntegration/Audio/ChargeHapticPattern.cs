using System;
using UnityEngine;

namespace Game.Integration.Audio
{
    /// <summary>A finite native envelope: one submission per hold, with no motor restarts every frame.</summary>
    public sealed class ChargeHapticPattern
    {
        private const float MaxSeconds = 10f;
        private const int MillisecondsPerSecond = 1000;
        private const int MaxMilliseconds = 10000;
        private const int AmplitudeStepMs = 20;
        private const int DutyStepMs = 50;
        private const int SlotsPerDutyStep = 2;
        private const float SmoothStepA = 3f;
        private const float SmoothStepB = 2f;
        private const float MinAmplitude = 26f;
        private const float MaxAmplitude = 184f;
        private const float MinDuty = 0.12f;
        private const float MaxDuty = 0.7f;
        // Android DEFAULT_AMPLITUDE, never an unsupported custom strength.
        private const int DefaultAmplitude = -1;

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
            var total = Mathf.Clamp(Mathf.CeilToInt(Mathf.Min(seconds, MaxSeconds) * MillisecondsPerSecond), 1, MaxMilliseconds);
            var step = amplitudeControl ? AmplitudeStepMs : DutyStepMs;
            var count = (total + step - 1) / step;
            var slots = amplitudeControl ? 1 : SlotsPerDutyStep;
            var timings = new long[count * slots];
            var amplitudes = new int[timings.Length];
            for (var i = 0; i < count; i++)
            {
                var duration = Math.Min(step, total - i * step);
                var progress = Mathf.Clamp01(initialCharge + chargePerSecond * (i * step / (float)MillisecondsPerSecond));
                var eased = progress * progress * (SmoothStepA - SmoothStepB * progress);
                if (amplitudeControl)
                {
                    timings[i] = duration;
                    amplitudes[i] = Mathf.RoundToInt(Mathf.Lerp(MinAmplitude, MaxAmplitude, eased));
                }
                else
                {
                    // On/off-only motors approximate the ramp through increasing duty cycle.
                    var on = Mathf.Clamp(Mathf.RoundToInt(duration * Mathf.Lerp(MinDuty, MaxDuty, eased)), 1, duration);
                    var off = i * SlotsPerDutyStep;
                    timings[off] = duration - on;
                    timings[off + 1] = on;
                    amplitudes[off] = 0;
                    amplitudes[off + 1] = DefaultAmplitude;
                }
            }
            return new ChargeHapticPattern(timings, amplitudes);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
