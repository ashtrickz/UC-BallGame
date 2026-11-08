using UnityEngine;

namespace Game.Integration.Settings
{
    public sealed class SettingsPersistence : MonoBehaviour
    {
        private void OnApplicationPause(bool paused) { if (paused) PlayerPrefs.Save(); }
        private void OnApplicationQuit() => PlayerPrefs.Save();
        private void OnDestroy() => PlayerPrefs.Save();
    }
}
