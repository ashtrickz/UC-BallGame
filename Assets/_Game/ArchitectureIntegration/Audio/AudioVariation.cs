using UnityEngine;

namespace Slime.Infrastructure.Audio
{
    /// <summary>Selects nonrepeating takes and pitches from the audio-only random stream.</summary>
    public sealed class AudioVariation
    {
        private const float MinimumPitch = 0.1f;
        private const float MaximumPitch = 3f;
        private const float MinimumPitchWidth = 0.00001f;
        private const float MinimumPitchDifference = 0.015f;
        private const float PitchDifferenceShare = 0.2f;
        private const float HalfRange = 0.5f;
        private readonly AudioRandom _random;

        public AudioVariation(AudioRandom random)
        {
            _random = random;
        }

        public int PickClip(int count, int previous)
        {
            var last = previous >= count ? -1 : previous;
            var index = count == 1 ? 0 : last < 0 ? _random.Next(count) : _random.Next(count - 1);
            if (count > 1 && last >= 0 && index >= last)
                index++;

            return index;
        }

        public float PickPitch(Vector2 range, float previous)
        {
            var min = Mathf.Clamp(Mathf.Min(range.x, range.y), MinimumPitch, MaximumPitch);
            var max = Mathf.Clamp(Mathf.Max(range.x, range.y), min, MaximumPitch);
            var width = max - min;
            if (width < MinimumPitchWidth)
                return min;

            var pitch = min + (float)_random.NextDouble() * width;
            if (!float.IsNaN(previous) && Mathf.Abs(pitch - previous) < Mathf.Min(MinimumPitchDifference, width * PitchDifferenceShare))
                pitch = min + ((pitch - min + width * HalfRange) % width);
            return pitch;
        }
    }
}
