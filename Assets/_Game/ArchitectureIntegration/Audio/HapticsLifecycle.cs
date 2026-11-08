using UnityEngine;

namespace Game.Integration.Audio
{
    public sealed class HapticsLifecycle : MonoBehaviour
    {
        private void OnApplicationPause(bool paused) { if (paused) Haptics.Cancel(); }
        private void OnApplicationFocus(bool focused) { if (!focused) Haptics.Cancel(); }
        private void OnDestroy() => Haptics.Cancel();
    }
}
