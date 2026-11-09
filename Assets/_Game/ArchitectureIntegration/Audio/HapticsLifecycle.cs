using Game.Audio;
using UnityEngine;

namespace Game.Integration.Audio
{
    /// <summary>Cancels native vibration when the application loses focus or its root is destroyed.</summary>
    public sealed class HapticsLifecycle : MonoBehaviour
    {
        private IHaptics _haptics;

        public void Bind(IHaptics haptics) => _haptics = haptics;

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                _haptics?.Cancel();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
                _haptics?.Cancel();
        }

        private void OnDestroy() => _haptics?.Cancel();
    }
}
