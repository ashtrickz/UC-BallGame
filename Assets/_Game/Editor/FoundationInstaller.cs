using System;
using System.IO;
using System.Linq;
using Game.App.Loading;
using Game.Integration.Composition;
using Game.Integration.Game;
using Game.Integration.Menu;
using Game.Integration.Gameplay;
using Game.Settings;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    public static class FoundationInstaller
    {
        public const string LoadingPath = "Assets/_Game/Scenes/Loading.unity";
        public const string GamePath = "Assets/_Game/Scenes/Game.unity";
        public const string PopupPath = "Assets/_Game/Prefabs/UI/SettingsPopup.prefab";

        [MenuItem("Tools/Ball Game/Create Foundation Scenes")]
        public static void CreateScenes()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before creating scenes.");
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the open scene before installing foundation scenes.");
            if (File.Exists(LoadingPath) || File.Exists(GamePath))
                throw new InvalidOperationException("Foundation scenes already exist. Edit them directly; the installer does not overwrite scenes.");
            Directory.CreateDirectory("Assets/_Game/Scenes");
            Directory.CreateDirectory("Assets/_Game/Prefabs/UI");
            AssetDatabase.Refresh();
            EnsureAddressables();
            var popup = CreateSettingsPopup();

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("ProjectScope");
            var scope = root.AddComponent<ProjectScope>();
            var canvas = Canvas("PersistentUI", root.transform, 100);
            var loading = Rect("LoadingScreen", canvas.transform);
            Stretch(loading);
            loading.gameObject.AddComponent<CanvasGroup>();
            loading.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0.04f, 0.06f, 0.09f);
            var view = loading.gameObject.AddComponent<LoadingScreenView>();
            var message = Label("Message", loading, "Loading...", new Vector2(0, 80), new Vector2(900, 80));
            var track = Rect("ProgressTrack", loading);
            track.sizeDelta = new Vector2(650, 18);
            track.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0.13f, 0.16f, 0.2f);
            var fill = Rect("Progress", track);
            Stretch(fill);
            var progress = fill.gameObject.AddComponent<UnityEngine.UI.Image>();
            progress.color = new Color(0.25f, 0.72f, 0.9f);
            progress.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            progress.type = UnityEngine.UI.Image.Type.Filled;
            progress.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            progress.fillAmount = 0;
            progress.type = UnityEngine.UI.Image.Type.Simple;
            var loadingSlider = track.gameObject.AddComponent<UnityEngine.UI.Slider>();
            loadingSlider.fillRect = fill;
            loadingSlider.interactable = false;
            loadingSlider.SetValueWithoutNotify(0f);
            var retry = Button("Retry", loading, "Retry", new Vector2(0, -100), new Vector2(220, 70));
            Set(view, "message", message);
            Set(view, "retryButton", retry);
            Set(view, "progressSlider", loadingSlider);
            Set(view, "_initiallyVisible", true);
            var popupRoot = Rect("PopupRoot", canvas.transform);
            Stretch(popupRoot);
            Set(scope, "loadingView", view);
            Set(scope, "popupRoot", popupRoot);
            Set(scope, "settingsPopupPrefab", popup);
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(root.transform);
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), LoadingPath);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camera.tag = "MainCamera";
            camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            camera.GetComponent<Camera>().backgroundColor = new Color(0.06f, 0.08f, 0.12f);
            var gameScope = new GameObject("GameScope").AddComponent<GameScope>();
            var gameCanvas = Canvas("GameUI", null, 0);
            var screen = Rect("GameScreen", gameCanvas.transform);
            Stretch(screen);
            screen.gameObject.AddComponent<CanvasGroup>();
            var gameView = screen.gameObject.AddComponent<GameView>();
            Label("Title", screen, "Ball Game", new Vector2(0, 110), new Vector2(900, 100)).fontSize = 64;
            var name = Label("PlayerName", screen, "Player", new Vector2(0, 15), new Vector2(900, 80));
            var settings = Button("Settings", screen, "Settings", new Vector2(0, -100), new Vector2(300, 80));
            Set(gameView, "playerName", name);
            Set(gameView, "settingsButton", settings);
            Set(gameScope, "view", gameView);
            CreateMenu(gameScope, gameCanvas.transform);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), GamePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(LoadingPath, true), new EditorBuildSettingsScene(GamePath, true) }
                .Concat(EditorBuildSettings.scenes.Where(s => s.path != LoadingPath && s.path != GamePath)).ToArray();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(LoadingPath);
            Debug.Log("Foundation scenes created: Loading -> Game.");
        }

        [MenuItem("Tools/Ball Game/Open Menu Scene")]
        public static void InstallMenuInGameScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            const string menuPath = "Assets/_Game/Scenes/Menu.unity";
            if (!File.Exists(menuPath)) throw new InvalidOperationException("Run Tools/Ball Game/Install Three Scene Flow first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(menuPath);
        }

        private static void EnsureGameSettingsButton(GameView game, MenuView menu)
        {
            var current = new SerializedObject(game).FindProperty("settingsButton").objectReferenceValue as UnityEngine.UI.Button;
            if (current != null && !current.transform.IsChildOf(menu.transform)) return;
            var template = new SerializedObject(menu).FindProperty("settingsButton").objectReferenceValue as UnityEngine.UI.Button;
            if (template == null) throw new InvalidOperationException("MenuView needs its Settings button.");
            var copy = UnityEngine.Object.Instantiate(template.gameObject, game.transform, false);
            copy.name = "Settings";
            var rect = copy.GetComponent<RectTransform>();
            var size = template.GetComponent<RectTransform>().rect.size;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-32, -32);
            rect.sizeDelta = size.x > 0 && size.y > 0 ? size : new Vector2(220, 70);
            Set(game, "settingsButton", copy.GetComponent<UnityEngine.UI.Button>());
        }

        private static void CreateMenu(GameScope scope, Transform canvas)
        {
            var existing = canvas.GetComponentInChildren<MenuView>(true);
            if (existing != null) return;
            var root = Rect("MenuScreen", canvas);
            Stretch(root);
            root.gameObject.AddComponent<CanvasGroup>();
            root.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0.06f, 0.08f, 0.12f);
            var view = root.gameObject.AddComponent<MenuView>();
            var content = Rect("SafeArea", root);
            Stretch(content);
            content.gameObject.AddComponent<global::Game.UI.SafeAreaFitter>();
            Label("Title", content, "Ball Game", new Vector2(0, 150), new Vector2(800, 100)).fontSize = 64;
            var play = Button("Play", content, "Play", Vector2.zero, new Vector2(300, 80));
            var settings = Button("Settings", content, "Settings", new Vector2(0, -110), new Vector2(300, 80));
            Set(view, "playButton", play);
            Set(view, "settingsButton", settings);
        }

        private static void EnsureAddressables()
        {
            if (AddressableAssetSettingsDefaultObject.Settings != null) return;
            Directory.CreateDirectory("Assets/AddressableAssetsData");
            AssetDatabase.Refresh();
            var settings = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>("Assets/_Game/Addressables/AddressableAssetSettings.asset")
                ?? AddressableAssetSettings.Create("Assets/_Game/Addressables", "AddressableAssetSettings", true, true);
            AddressableAssetSettingsDefaultObject.Settings = settings;
        }

        private static SettingsPopupView CreateSettingsPopup()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PopupPath);
            if (existing != null) return existing.GetComponent<SettingsPopupView>();
            var root = Rect("SettingsPopup", null);
            Stretch(root);
            root.gameObject.AddComponent<CanvasGroup>();
            root.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, 0.75f);
            var view = root.gameObject.AddComponent<SettingsPopupView>();
            var panel = Rect("Panel", root);
            panel.sizeDelta = new Vector2(720, 900);
            panel.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0.1f, 0.13f, 0.18f);
            Label("Title", panel, "Settings", new Vector2(0, 380), new Vector2(600, 80)).fontSize = 44;
            var close = Button("Close", panel, "Close", new Vector2(235, 375), new Vector2(150, 60));
            var channels = (VolumeChannel[])Enum.GetValues(typeof(VolumeChannel));
            var sliders = new UnityEngine.UI.Slider[channels.Length];
            for (var i = 0; i < channels.Length; i++)
            {
                var y = 270 - i * 95;
                var label = Label(channels[i] + "Label", panel, channels[i].ToString(), new Vector2(-210, y), new Vector2(180, 50));
                label.fontSize = 27;
                var rect = Rect(channels[i] + "Slider", panel);
                rect.anchoredPosition = new Vector2(85, y);
                rect.sizeDelta = new Vector2(360, 26);
                rect.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0.24f, 0.29f, 0.36f);
                var handle = Rect("Handle", rect);
                handle.sizeDelta = new Vector2(28, 44);
                handle.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0.25f, 0.72f, 0.9f);
                var slider = rect.gameObject.AddComponent<UnityEngine.UI.Slider>();
                slider.handleRect = handle;
                slider.targetGraphic = handle.GetComponent<UnityEngine.UI.Image>();
                slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
                slider.minValue = 0;
                slider.maxValue = 1;
                slider.value = 1;
                sliders[i] = slider;
            }
            var rates = new[] { 30, 60, 120 };
            var buttons = new UnityEngine.UI.Button[rates.Length];
            for (var i = 0; i < rates.Length; i++)
                buttons[i] = Button("Fps" + rates[i], panel, rates[i] + " FPS", new Vector2((i - 1) * 190, -230), new Vector2(175, 60));
            var reset = Button("Reset", panel, "Reset", new Vector2(-150, -325), new Vector2(260, 65));
            var copy = Button("CopyPlayerId", panel, "Copy player ID", new Vector2(150, -325), new Vector2(260, 65));
            var build = Label("Version", panel, "", new Vector2(0, -400), new Vector2(600, 45));
            build.fontSize = 24;
            Set(view, "_closeButton", close);
            Set(view, "_scaleRoot", panel);
            Set(view, "buildText", build);
            Set(view, "resetButton", reset);
            Set(view, "copyKeyButton", copy);
            Set(view, "copyKeyLabel", copy.GetComponentInChildren<TMP_Text>());
            var serialized = new SerializedObject(view);
            var volumes = serialized.FindProperty("volumes");
            volumes.arraySize = channels.Length;
            for (var i = 0; i < channels.Length; i++)
            {
                var row = volumes.GetArrayElementAtIndex(i);
                row.FindPropertyRelative("channel").enumValueIndex = (int)channels[i];
                row.FindPropertyRelative("slider").objectReferenceValue = sliders[i];
            }
            var fps = serialized.FindProperty("fpsChoices");
            fps.arraySize = rates.Length;
            for (var i = 0; i < rates.Length; i++)
            {
                var choice = fps.GetArrayElementAtIndex(i);
                choice.FindPropertyRelative("fps").intValue = rates[i];
                choice.FindPropertyRelative("button").objectReferenceValue = buttons[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, PopupPath);
            UnityEngine.Object.DestroyImmediate(root.gameObject);
            AssetDatabase.ImportAsset(PopupPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<GameObject>(PopupPath).GetComponent<SettingsPopupView>();
        }

        private static GameObject Canvas(string name, Transform parent, int order)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<UnityEngine.Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return root;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            return rect;
        }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static TextMeshProUGUI Label(string name, Transform parent, string text, Vector2 position, Vector2 size)
        {
            var rect = Rect(name, parent);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 32;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }
        private static UnityEngine.UI.Button Button(string name, Transform parent, string text, Vector2 position, Vector2 size)
        {
            var rect = Rect(name, parent);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.2f, 0.29f, 0.4f);
            var button = rect.gameObject.AddComponent<Game.UI.FeedbackButton>();
            button.targetGraphic = image;
            var label = Label("Label", rect, text, Vector2.zero, size);
            label.fontSize = 27;
            return button;
        }
        private static void Set(UnityEngine.Object target, string property, UnityEngine.Object value)
        { var serialized = new SerializedObject(target); serialized.FindProperty(property).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Set(UnityEngine.Object target, string property, bool value)
        { var serialized = new SerializedObject(target); serialized.FindProperty(property).boolValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
