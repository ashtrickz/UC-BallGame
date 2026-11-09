using UnityEngine;

namespace Game.Gameplay.View
{
    /// <summary>
    /// Splash of the colony pool when the slime dives in: a fountain of jelly drops, a crown of drops thrown out
    /// around the entry point and expanding ripples on the surface. Secondary effect: never awaited.
    /// </summary>
    public sealed class PoolSplashEffect : MonoBehaviour
    {
        private const float MinStrength = 0.4f;
        private const float MaxStrength = 1.5f;
        private const float FountainDrops = 46f;
        private const float CrownDrops = 34f;

        [SerializeField] private ParticleSystem fountain;
        [SerializeField] private ParticleSystem crown;
        [SerializeField] private ParticleSystem ripples;

        public bool IsPlaying => IsAlive(fountain) || IsAlive(crown) || IsAlive(ripples);

        public void Play(Vector3 surfacePoint, float strength = 1f)
        {
            transform.position = surfacePoint;
            strength = Mathf.Clamp(strength, MinStrength, MaxStrength);
            Emit(fountain, Mathf.RoundToInt(FountainDrops * strength));
            Emit(crown, Mathf.RoundToInt(CrownDrops * strength));
            if (ripples != null)
            {
                ripples.Clear(true);
                ripples.Play(true);
            }
        }

        public void Clear()
        {
            ClearSystem(fountain);
            ClearSystem(crown);
            ClearSystem(ripples);
        }

        private static bool IsAlive(ParticleSystem system)
        {
            return system != null && system.IsAlive();
        }

        private static void ClearSystem(ParticleSystem system)
        {
            if (system != null)
                system.Clear(true);
        }

        private static void Emit(ParticleSystem system, int count)
        {
            if (system == null)
                return;

            system.Clear(true);
            system.Emit(count);
        }
    }
}
