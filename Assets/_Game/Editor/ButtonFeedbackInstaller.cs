using System;
using Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    /// <summary>Changes button scripts in place so serialized references and click listeners survive.</summary>
    public static class ButtonFeedbackInstaller
    {
        [MenuItem("Tools/Ball Game/UI/Install Button Feedback")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Install button feedback outside Play Mode.");

            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save scene changes before installing button feedback.");

            var probe = new GameObject("FeedbackButton script", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
            probe.SetActive(false);
            try
            {
                var script = MonoScript.FromMonoBehaviour(probe.AddComponent<FeedbackButton>());
                var count = 0;
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game" }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        var changed = ConvertButtons(root, script);
                        if (changed == 0) continue;
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        count += changed;
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }

                foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Game" }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var scene = SceneManager.GetSceneByPath(path);
                    var alreadyLoaded = scene.IsValid() && scene.isLoaded;
                    if (!alreadyLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    try
                    {
                        var changed = 0;
                        foreach (var root in scene.GetRootGameObjects()) changed += ConvertButtons(root, script);
                        if (changed == 0) continue;
                        EditorSceneManager.MarkSceneDirty(scene);
                        if (!EditorSceneManager.SaveScene(scene))
                            throw new InvalidOperationException("Could not save " + path);
                        count += changed;
                    }
                    finally { if (!alreadyLoaded) EditorSceneManager.CloseScene(scene, true); }
                }
                Debug.Log($"Button feedback installed: {count} buttons migrated.");
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
        }

        private static int ConvertButtons(GameObject root, MonoScript script)
        {
            var count = 0;
            foreach (var button in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                if (button.GetType() != typeof(UnityEngine.UI.Button)) continue;
                var serialized = new SerializedObject(button);
                serialized.FindProperty("m_Script").objectReferenceValue = script;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                count++;
            }
            return count;
        }
    }
}
