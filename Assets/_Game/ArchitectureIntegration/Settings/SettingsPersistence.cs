using UnityEngine;

namespace Slime.Infrastructure.Settings
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Slime.Integration.Settings", sourceAssembly: "Game.Services", sourceClassName: "SettingsPersistence")]
    public sealed class SettingsPersistence : MonoBehaviour
    {
        private void OnApplicationPause(bool paused)
        {
            if (paused)
                PlayerPrefs.Save();
        }
        private void OnApplicationQuit() => PlayerPrefs.Save();
        private void OnDestroy() => PlayerPrefs.Save();
    }
}
