using UnityEngine;

namespace Game.Gameplay.View
{
    /// <summary>
    /// Splash of the colony pool when the slime dives in: a fountain of jelly drops, a crown of drops thrown out
    /// around the entry point and expanding ripples on the surface. Secondary effect: never awaited.
    /// </summary>
    public sealed class PoolSplashEffect : MonoBehaviour
    {
        [SerializeField] private ParticleSystem fountain;
        [SerializeField] private ParticleSystem crown;
        [SerializeField] private ParticleSystem ripples;

        public bool IsPlaying => (fountain != null && fountain.IsAlive()) || (crown != null && crown.IsAlive()) || (ripples != null && ripples.IsAlive());

        /// <param name="surfacePoint">Entry point on the pool surface.</param>
        /// <param name="strength">Scales the amount and reach of the drops (1 = full-size slime).</param>
        public void Play(Vector3 surfacePoint, float strength = 1f)
        {
            transform.position = surfacePoint;
            strength = Mathf.Clamp(strength, 0.4f, 1.5f);
            Emit(fountain, Mathf.RoundToInt(46 * strength));
            Emit(crown, Mathf.RoundToInt(34 * strength));
            if (ripples != null)
            {
                ripples.Clear(true);
                ripples.Play(true);
            }
        }

        public void Clear()
        {
            foreach (var system in new[] { fountain, crown, ripples })
                if (system != null) system.Clear(true);
        }

        private static void Emit(ParticleSystem system, int count)
        {
            if (system == null) return;
            system.Clear(true);
            system.Emit(count);
        }
    }
}
