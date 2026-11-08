using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.App.Bootstrap
{
    public static class BootstrapEntry
    {
        public const string SceneName = "Loading";
#if UNITY_EDITOR
        public const string HarnessSceneName = "NoBootloaderTests";
        public static bool IsHarnessActive => SceneManager.GetActiveScene().name == HarnessSceneName
            || SceneManager.GetActiveScene().name.StartsWith("InitTestScene", System.StringComparison.Ordinal)
            || SceneManager.GetActiveScene().path.StartsWith("Assets/_Game/Prototypes/", System.StringComparison.Ordinal);
#endif
        public static bool UsesProjectBootstrap => Application.CanStreamedLevelBeLoaded(SceneName);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void EnsureBootstrap()
        {
#if UNITY_EDITOR
            if (IsHarnessActive) 
                return;
#endif
            
            if (UsesProjectBootstrap && SceneManager.GetActiveScene().name != SceneName)
                SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
        }
    }
}
