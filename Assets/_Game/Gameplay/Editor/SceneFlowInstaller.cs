using System;
using System.Linq;
using Game.App.Loading;
using Game.Integration.Composition;
using Game.Integration.Game;
using Game.Integration.Menu;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.Gameplay.Editor
{
    /// <summary>Migrates the existing menu without rebuilding its artwork or the gameplay world.</summary>
    public static class SceneFlowInstaller
    {
        public const string MenuPath = "Assets/_Game/Scenes/Menu.unity";
        private const string LoadingPath = "Assets/_Game/Scenes/Loading.unity";
        public const string TransitionMaterialPath = "Assets/_Game/Architecture/Application/Loading/CrystalTransition.mat";

        [MenuItem("Tools/Ball Game/Install Three Scene Flow")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save modified scenes before installing the scene flow.");

            var game = EditorSceneManager.OpenScene(GameplaySceneInstaller.ScenePath, OpenSceneMode.Single);
            var menu = Find<MenuView>(game);
            if (menu != null)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuPath) != null)
                    throw new InvalidOperationException("Both scenes contain a menu. Resolve the duplicate before migrating.");
                var levels = Find<LevelSelectView>(game);
                var gameScope = Find<GameScope>(game);
                var config = new SerializedObject(gameScope).FindProperty("gameConfig").objectReferenceValue;
                var sourceCanvas = menu.GetComponentInParent<Canvas>();
                var menuScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(menuScene);
                var camera = new GameObject("Menu Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.05f, 0.1f, 0.13f);
                camera.tag = "MainCamera";
                var canvas = new GameObject("MenuUI", typeof(RectTransform), typeof(Canvas),
                    typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
                EditorUtility.CopySerialized(sourceCanvas, canvas.GetComponent<Canvas>());
                EditorUtility.CopySerialized(sourceCanvas.GetComponent<UnityEngine.UI.CanvasScaler>(),
                    canvas.GetComponent<UnityEngine.UI.CanvasScaler>());
                var safeArea = Rect("SafeAreaWrapper", canvas.transform);
                Stretch(safeArea);
                safeArea.gameObject.AddComponent<SafeAreaFitter>();
                Move(menu.transform, safeArea, menuScene);
                Move(levels.transform, safeArea, menuScene);
                var scope = new GameObject("MenuScope").AddComponent<MenuScope>();
                Set(scope, "menuView", menu);
                Set(scope, "levelSelectView", levels);
                Set(scope, "gameConfig", config);
                Set(menu, "_initiallyVisible", true);
                Set(Find<GameView>(game), "_initiallyVisible", false);
                EditorSceneManager.SaveScene(menuScene, MenuPath);
                EditorSceneManager.SaveScene(game);
            }

            var loadingScene = EditorSceneManager.OpenScene(LoadingPath, OpenSceneMode.Single);
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuPath))
                throw new InvalidOperationException("Menu scene is missing and no legacy menu was found in Game.");
            ConfigureLoading(loadingScene);
            EditorSceneManager.SaveScene(loadingScene);
            // These are the three shipping scenes. Prototype scenes remain available in the project.
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(LoadingPath, true),
                new EditorBuildSettingsScene(MenuPath, true),
                new EditorBuildSettingsScene(GameplaySceneInstaller.ScenePath, true)
            };
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneFlow] Loading -> Menu <-> Game installed; shader and slider are wired.");
        }

        private static void ConfigureLoading(Scene scene)
        {
            var view = Find<LoadingScreenView>(scene);
            var shader = Shader.Find("Game/UI/Crystal Transition");
            if (!shader) throw new InvalidOperationException("Crystal Transition shader was not imported.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(TransitionMaterialPath);
            if (!material)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, TransitionMaterialPath);
            }
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(CrystalTransitionAtlasBuilder.AtlasPath);
            if (!atlas)
            {
                CrystalTransitionAtlasBuilder.Rebuild();
                atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(CrystalTransitionAtlasBuilder.AtlasPath);
            }
            material.SetTexture("_CrystalAtlas", atlas);
            EditorUtility.SetDirty(material);
            var background = view.GetComponent<UnityEngine.UI.Image>();
            background.material = material;
            background.color = Color.white;
            background.raycastTarget = true;
            var content = view.transform.Find("Content") as RectTransform;
            if (!content)
            {
                content = Rect("Content", view.transform);
                Stretch(content);
                foreach (var name in new[] { "Message", "ProgressTrack", "Retry" })
                {
                    var child = view.transform.Find(name);
                    if (child) child.SetParent(content, false);
                }
            }
            var group = content.GetComponent<CanvasGroup>();
            if (!group) group = content.gameObject.AddComponent<CanvasGroup>();
            var track = content.Find("ProgressTrack").GetComponent<RectTransform>();
            track.sizeDelta = new Vector2(650, 26);
            var fill = track.Find("Progress").GetComponent<UnityEngine.UI.Image>();
            fill.type = UnityEngine.UI.Image.Type.Simple;
            fill.color = new Color(0.63f, 0.94f, 0.48f);
            fill.raycastTarget = false;
            var slider = track.GetComponent<UnityEngine.UI.Slider>();
            if (!slider) slider = track.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.fillRect = fill.rectTransform;
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = 0;
            slider.interactable = false;
            slider.transition = UnityEngine.UI.Selectable.Transition.None;
            var percentage = content.Find("Percentage")?.GetComponent<TMP_Text>();
            if (!percentage)
            {
                var label = Rect("Percentage", content);
                label.anchoredPosition = new Vector2(0, -52);
                label.sizeDelta = new Vector2(650, 55);
                percentage = label.gameObject.AddComponent<TextMeshProUGUI>();
                percentage.font = TMP_Settings.defaultFontAsset;
                percentage.fontSize = 32;
                percentage.alignment = TextAlignmentOptions.Center;
                percentage.color = new Color(0.8f, 0.92f, 0.87f);
                percentage.raycastTarget = false;
                percentage.text = "0%";
            }
            var message = content.Find("Message").GetComponent<TMP_Text>();
            message.fontSize = 36;
            message.raycastTarget = false;
            var retry = content.Find("Retry").GetComponent<RectTransform>();
            retry.anchoredPosition = new Vector2(0, -145);
            retry.gameObject.SetActive(false);
            Set(view, "contentContainer", group);
            Set(view, "shaderMaterial", material);
            Set(view, "progressSlider", slider);
            Set(view, "percentageLabel", percentage);
            Set(view, "_initiallyVisible", true);
            Set(view, "_animateFade", false);
            Set(view, "_transitionDuration", 0.7f);

            // Popups use the same persistent canvas. Give the loader its own top sorting layer.
            var canvas = view.GetComponent<Canvas>();
            if (!canvas) canvas = view.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
            if (!view.GetComponent<UnityEngine.UI.GraphicRaycaster>()) view.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        private static T Find<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).SingleOrDefault();

        private static void Move(Transform child, Transform parent, Scene scene)
        {
            child.SetParent(null, false);
            SceneManager.MoveGameObjectToScene(child.gameObject, scene);
            child.SetParent(parent, false);
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            return rect;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }
        private static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Set(Object target, string field, bool value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Set(Object target, string field, float value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
