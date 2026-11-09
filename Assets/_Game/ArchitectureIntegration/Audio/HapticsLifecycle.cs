using Slime.Audio;
using UnityEngine;

namespace Slime.Infrastructure.Audio
{
    /// <summary>Cancels native vibration when the application loses focus or its root is destroyed.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Slime.Integration.Audio", sourceAssembly: "Game.Services", sourceClassName: "HapticsLifecycle")]
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
