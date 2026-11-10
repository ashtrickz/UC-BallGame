using System;
using System.Collections.Generic;
using System.Linq;
using Slime.Audio;
using Slime.Gameplay.Config;
using Slime.Gameplay.Core.Config;
using Slime.Gameplay.View;
using Slime.Gameplay.Presentation;
using Slime.Infrastructure.Audio;
using Slime.Composition;
using Slime.Menu;
using Slime.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Slime.Gameplay.Editor
{
    /// <summary>Adds star presentation to the existing scenes and initializes editable targets without rebuilding gameplay.</summary>
    public static class StarRatingInstaller
    {
        private const string GameScene = "Assets/_Game/Scenes/Game.unity";
        private const string MenuScene = "Assets/_Game/Scenes/Menu.unity";
        private const string SpriteFolder = "Assets/_Game/Art/UI/CartoonContour/Sprites/";
        private const string ResultCameraName = "Result UI Camera";
        private const string UiLayerName = "UI";
        private const string UiRendererPath = "Assets/Settings/UI_Renderer.asset";
        private const string MobileRendererPath = "Assets/Settings/Mobile_Renderer.asset";
        private const int UiRendererIndex = 1;
        private static readonly string[] PipelinePaths =
        {
            "Assets/Settings/Mobile_RPAsset.asset",
            "Assets/Settings/PC_RPAsset.asset",
        };

        [MenuItem("Tools/Ball Game/Gameplay/Configure Result Rendering")]
        public static void ConfigureResultRendering()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop play mode before configuring result rendering.");

            var scene = SceneManager.GetActiveScene();
            if (scene.path != GameScene)
                throw new InvalidOperationException("Open the Game scene before configuring result rendering.");

            var roots = scene.GetRootGameObjects();
            var result = roots.SelectMany(root => root.GetComponentsInChildren<ResultView>(true)).Single();
            var stage = roots.SelectMany(root => root.GetComponentsInChildren<GameplayStage>(true)).Single();
            ConfigureResultRendering(result, stage.CameraRig.GetComponent<Camera>());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("Tools/Ball Game/Gameplay/Install Star Rating")]
        public static void Install()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop play mode before installing star presentation.");

            var scene = SceneManager.GetActiveScene();
            if (scene.path != GameScene)
                throw new InvalidOperationException("Open the Game scene before installing star presentation.");

            var empty = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "icon_star_empty.png");
            var filled = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "icon_star_filled.png");
            if (empty == null || filled == null)
                throw new InvalidOperationException("Both star sprites must be imported as sprites.");

            var result = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ResultView>(true)).Single();
            var stage = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameplayStage>(true)).Single();
            var camera = stage.CameraRig.GetComponent<Camera>();
            var resultCanvasObject = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Result Canvas");
            if (resultCanvasObject == null)
            {
                var original = result.GetComponentInParent<Canvas>().rootCanvas;
                resultCanvasObject = new GameObject("Result Canvas", typeof(RectTransform), typeof(Canvas),
                    typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
                EditorUtility.CopySerialized(original.GetComponent<UnityEngine.UI.CanvasScaler>(),
                    resultCanvasObject.GetComponent<UnityEngine.UI.CanvasScaler>());
                result.transform.SetParent(resultCanvasObject.transform, false);
            }

            var resultCanvas = resultCanvasObject.GetComponent<Canvas>();
            resultCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            resultCanvas.worldCamera = camera;
            resultCanvas.planeDistance = 5;
            resultCanvas.sortingOrder = 400;
            var card = result.transform.Find("RootTransform").GetComponent<RectTransform>();
            card.sizeDelta = new Vector2(card.sizeDelta.x, 850);
            var row = Row(card, "Stars", empty, filled, 135, 160, new Vector2(0, 200));
            var subtitle = card.Find("Subtitle").GetComponent<TMP_Text>();
            subtitle.rectTransform.anchoredPosition = Vector2.zero;
            subtitle.rectTransform.sizeDelta = new Vector2(subtitle.rectTransform.sizeDelta.x, 200);
            subtitle.enableAutoSizing = true;
            subtitle.fontSizeMin = 28;
            subtitle.fontSizeMax = 42;
            var world = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Victory Stars");
            if (world == null)
                world = new GameObject("Victory Stars", typeof(RectTransform), typeof(Canvas), typeof(WorldStarsView));

            var canvas = world.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 500;
            canvas.worldCamera = camera;
            var images = new UnityEngine.UI.Image[StarRowView.SlotCount];
            for (var i = 0; i < images.Length; i++)
            {
                var name = "Flying Star " + (i + 1);
                var child = world.transform.Find(name);
                var star = child != null ? child.gameObject : new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
                star.transform.SetParent(world.transform, false);
                images[i] = star.GetComponent<UnityEngine.UI.Image>();
                images[i].sprite = filled;
                images[i].preserveAspect = true;
                images[i].raycastTarget = false;
                star.SetActive(false);
            }

            var flight = world.GetComponent<WorldStarsView>();
            References(flight, "stars", images);
            var serializedResult = new SerializedObject(result);
            serializedResult.FindProperty("stars").objectReferenceValue = row;
            serializedResult.FindProperty("starFlight").objectReferenceValue = flight;
            serializedResult.ApplyModifiedPropertiesWithoutUndo();
            ConfigureResultRendering(result, camera);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            var menu = SceneManager.GetSceneByPath(MenuScene);
            var openedMenu = !menu.IsValid() || !menu.isLoaded;
            if (openedMenu)
                menu = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Additive);

            var select = menu.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LevelSelectView>(true)).Single();
            var template = (UnityEngine.UI.Button)new SerializedObject(select).FindProperty("cellTemplate").objectReferenceValue;
            Row(template.transform, "Stars", empty, filled, 38, 44, new Vector2(0, -55));
            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu);
            if (openedMenu)
                EditorSceneManager.CloseScene(menu, true);

            var config = AssetDatabase.LoadAssetAtPath<SlimeGameConfig>(GameplayAssetsBuilder.ConfigFolder + "/SlimeGameConfig.asset");
            config.Campaign.InitializeStarCriteria(config.CreateSnapshot());
            EditorUtility.SetDirty(config.Campaign);
            AssetDatabase.SaveAssetIfDirty(config.Campaign);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);
            InstallSounds();
        }

        private static void ConfigureResultRendering(ResultView result, Camera worldCamera)
        {
            var resultCanvas = result.GetComponentInParent<Canvas>().rootCanvas;
            var flightCanvas = result.StarFlight.GetComponent<Canvas>();
            var uiLayer = LayerMask.NameToLayer(UiLayerName);
            if (uiLayer < 0)
                throw new InvalidOperationException("The UI layer is missing.");

            ConfigureUiRenderer(uiLayer);
            var child = worldCamera.transform.Find(ResultCameraName);
            var cameraObject = child != null ? child.gameObject : new GameObject(ResultCameraName, typeof(Camera));
            cameraObject.transform.SetParent(worldCamera.transform, false);
            cameraObject.transform.localPosition = Vector3.zero;
            cameraObject.transform.localRotation = Quaternion.identity;
            var uiCamera = cameraObject.GetComponent<Camera>();
            uiCamera.CopyFrom(worldCamera);
            uiCamera.cullingMask = 1 << uiLayer;
            uiCamera.useOcclusionCulling = false;
            uiCamera.clearFlags = CameraClearFlags.Depth;
            var uiData = uiCamera.GetUniversalAdditionalCameraData();
            uiData.renderType = CameraRenderType.Overlay;
            uiData.renderPostProcessing = false;
            uiData.renderShadows = false;
            uiData.requiresColorTexture = false;
            uiData.requiresDepthTexture = false;
            uiData.volumeLayerMask = 0;
            uiData.antialiasing = AntialiasingMode.None;
            var uiSerialized = new SerializedObject(uiData);
            uiSerialized.FindProperty("m_ClearDepth").boolValue = true;
            uiSerialized.ApplyModifiedPropertiesWithoutUndo();
            uiData.SetRenderer(UiRendererIndex);
            var worldData = worldCamera.GetUniversalAdditionalCameraData();
            worldData.cameraStack.Remove(uiCamera);
            worldData.cameraStack.Add(uiCamera);
            worldCamera.cullingMask &= ~(1 << uiLayer);
            resultCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            resultCanvas.worldCamera = uiCamera;
            flightCanvas.worldCamera = uiCamera;
            foreach (var canvas in new[] { resultCanvas, flightCanvas })
                foreach (var transform in canvas.GetComponentsInChildren<Transform>(true))
                    transform.gameObject.layer = uiLayer;

            EditorUtility.SetDirty(worldCamera);
            EditorUtility.SetDirty(worldData);
            EditorUtility.SetDirty(uiCamera);
            EditorUtility.SetDirty(uiData);
            EditorUtility.SetDirty(resultCanvas);
            EditorUtility.SetDirty(flightCanvas);
        }

        private static void ConfigureUiRenderer(int uiLayer)
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(UiRendererPath);
            if (renderer == null)
            {
                if (!AssetDatabase.CopyAsset(MobileRendererPath, UiRendererPath))
                    throw new InvalidOperationException("Could not create the UI renderer.");

                renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(UiRendererPath);
            }

            renderer.renderingMode = RenderingMode.Forward;
            renderer.opaqueLayerMask = 0;
            renderer.transparentLayerMask = 1 << uiLayer;
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssetIfDirty(renderer);
            foreach (var path in PipelinePaths)
            {
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                var serialized = new SerializedObject(pipeline);
                var renderers = serialized.FindProperty("m_RendererDataList");
                if (renderers.arraySize > UiRendererIndex
                    && renderers.GetArrayElementAtIndex(UiRendererIndex).objectReferenceValue != renderer)
                    throw new InvalidOperationException("The reserved UI renderer slot is occupied in " + path);

                renderers.arraySize = Mathf.Max(renderers.arraySize, UiRendererIndex + 1);
                renderers.GetArrayElementAtIndex(UiRendererIndex).objectReferenceValue = renderer;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pipeline);
                AssetDatabase.SaveAssetIfDirty(pipeline);
            }
        }

        private static StarRowView Row(Transform parent, string name, Sprite empty, Sprite filled, float size, float spacing, Vector2 position)
        {
            var child = parent.Find(name);
            var root = child != null ? child.gameObject : new GameObject(name, typeof(RectTransform), typeof(StarRowView));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(spacing * StarRowView.SlotCount, size);
            var slots = new UnityEngine.UI.Image[StarRowView.SlotCount];
            for (var i = 0; i < slots.Length; i++)
            {
                var slotName = "Star " + (i + 1);
                var existing = root.transform.Find(slotName);
                var slot = existing != null ? existing.gameObject : new GameObject(slotName, typeof(RectTransform), typeof(UnityEngine.UI.Image));
                slot.transform.SetParent(root.transform, false);
                var slotRect = (RectTransform)slot.transform;
                slotRect.anchorMin = slotRect.anchorMax = slotRect.pivot = new Vector2(0.5f, 0.5f);
                slotRect.anchoredPosition = new Vector2((i - 1) * spacing, 0);
                slotRect.sizeDelta = Vector2.one * size;
                slots[i] = slot.GetComponent<UnityEngine.UI.Image>();
                slots[i].sprite = empty;
                slots[i].preserveAspect = true;
                slots[i].raycastTarget = false;
            }

            var row = root.GetComponent<StarRowView>();
            References(row, "slots", slots);
            var serialized = new SerializedObject(row);
            serialized.FindProperty("emptyStar").objectReferenceValue = empty;
            serialized.FindProperty("filledStar").objectReferenceValue = filled;
            serialized.FindProperty("filledStarMaterial").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Resources/UI/Star Shimmer.mat");
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return row;
        }

        private static void References(UnityEngine.Object target, string field, UnityEngine.Object[] values)
        {
            var serialized = new SerializedObject(target);
            var array = serialized.FindProperty(field);
            array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void InstallSounds()
        {
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>("Assets/_Game/Resources/Audio/AudioLibrary.asset");
            var sounds = new List<SoundDefinition>(library.Sounds);
            Add(GameSound.StarLaunch, "ui_panel_open_01.wav", 0.45f);
            Add(GameSound.StarCollect, "ui_confirm_01.wav", 0.85f);
            library.Sounds = sounds.ToArray();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssetIfDirty(library);

            void Add(GameSound id, string clipName, float volume)
            {
                if (sounds.Any(sound => sound.id == id))
                    return;

                sounds.Add(new SoundDefinition
                {
                    id = id,
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audio/Selected/UI/" + clipName),
                    volume = volume,
                    pitchRange = Vector2.one,
                    group = AudioGroup.UI,
                    maxVoices = 3,
                });
            }
        }
    }
}
