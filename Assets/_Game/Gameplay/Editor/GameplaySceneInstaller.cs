using System;
using Game.Gameplay.View;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Gameplay.Editor
{
    /// <summary>
    /// Installs (or reinstalls) the gameplay presentation into the Game scene: environment, lights, slime, crystals,
    /// lane, doors, colony, blast preview, camera rig and HUD, then wires GameScope. Re-running replaces only the
    /// objects it created.
    /// </summary>
    public static class GameplaySceneInstaller
    {
        public const string ScenePath = "Assets/_Game/Scenes/Game.unity";
        private const string StageName = "Gameplay Stage";
        private const string LightName = "Gameplay Key Light";
        private const string VolumeName = "Gameplay Grading";

        [MenuItem("Tools/Ball Game/Gameplay/Install Into Game Scene")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath);
            }
            var assets = GameplayAssetsBuilder.Ensure();
            foreach (var name in new[] { StageName, LightName, VolumeName })
            {
                var old = GameObject.Find(name);
                if (old != null) Object.DestroyImmediate(old);
            }

            ConfigureLighting(assets);
            var stage = new GameObject(StageName).AddComponent<GameplayStage>();
            var route = (float)RuleValuesRoute(assets);
            BuildEnvironment(stage.transform, assets, route);
            var track = BuildTrack(stage.transform, assets);
            var slime = BuildSlime(stage.transform, assets);
            var crystals = BuildCrystals(stage.transform, assets);
            var doors = BuildDoors(stage.transform, assets);
            var colony = BuildColony(stage.transform, assets);
            var preview = BuildPreview(stage.transform, assets);
            var input = stage.gameObject.AddComponent<PointerChargeInput>();
            var rig = ConfigureCamera(assets);
            var resultView = BuildGameView(assets);
            ConfigureMenu();
            var levelSelect = BuildLevelSelect();

            Set(stage, "viewConfig", assets.ViewConfig);
            Set(stage, "slime", slime);
            Set(stage, "crystals", crystals);
            Set(stage, "track", track);
            Set(stage, "doors", doors);
            Set(stage, "preview", preview);
            Set(stage, "cameraRig", rig);
            Set(stage, "input", input);
            Set(stage, "finishMarker", colony);
            Set(stage, "tutorial", BuildTutorial(assets));
            Set(stage, "poolSplash", BuildPoolSplash(colony, assets));

            var scope = Object.FindFirstObjectByType<Game.Integration.Game.GameScope>();
            if (scope == null) throw new InvalidOperationException("Game scene has no GameScope.");
            Set(scope, "gameConfig", assets.GameConfig);
            Set(scope, "gameplayStage", stage);
            Set(scope, "resultView", resultView);

            CanyonEnvironmentInstaller.Apply(stage);

            ConfigurePlayer();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Gameplay] installed into " + ScenePath);
        }

        /// <summary>The course must fit the longest route: endless rules or any campaign level.</summary>
        private static double RuleValuesRoute(GameplayAssetsBuilder.Set assets)
        {
            var route = assets.GameConfig.CreateSnapshot().FinishZ;
            if (assets.GameConfig.Campaign != null)
                foreach (var level in assets.GameConfig.Campaign.levels) route = System.Math.Max(route, level.routeLength);
            return route;
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
        }

        // ------------------------------------------------------------------ world

        private static void ConfigureLighting(GameplayAssetsBuilder.Set assets)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = GameplayAssetsBuilder.Hex("90B3B7") * 0.65f;
            RenderSettings.ambientEquatorColor = GameplayAssetsBuilder.Hex("456B70") * 0.65f;
            RenderSettings.ambientGroundColor = GameplayAssetsBuilder.Hex("101E29");
            RenderSettings.fog = false;
            var key = new GameObject(LightName).AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = GameplayAssetsBuilder.Hex("FFF0D6");
            key.intensity = 1.25f;
            key.shadows = LightShadows.Soft;
            key.shadowBias = 0.025f;
            key.transform.rotation = Quaternion.Euler(48, -60, 0);
            var volume = new GameObject(VolumeName).AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10;
            volume.sharedProfile = assets.Volume;
        }

        private static void BuildEnvironment(Transform parent, GameplayAssetsBuilder.Set a, float route)
        {
            var root = Group("Environment", parent);
            var length = route + 14;
            var center = route * 0.5f;
            Primitive("Backdrop floor", a.Cube, a.Backdrop, root, new Vector3(0, -0.9f, center), new Vector3(120, 0.2f, length + 80), false);
            Primitive("Ceramic course", a.Cube, a.Plinth, root, new Vector3(0, -0.2f, center), new Vector3(9.5f, 0.4f, length), true);
            Primitive("Course rim", a.Cube, a.Rim, root, new Vector3(0, -0.43f, center), new Vector3(9.8f, 0.08f, length + 0.3f), false);
            Primitive("Start pad", a.Cylinder, a.Rim, root, new Vector3(0, 0.004f, 0), new Vector3(2.2f, 0.004f, 2.2f), false);
        }

        private static TrackView BuildTrack(Transform parent, GameplayAssetsBuilder.Set a)
        {
            var root = Group("Lane", parent);
            var view = root.gameObject.AddComponent<TrackView>();
            var lane = Primitive("Lane surface", a.Cube, a.Lane, root, Vector3.zero, Vector3.one, true);
            var left = Primitive("Lane edge L", a.Cube, a.LaneEdge, root, Vector3.zero, Vector3.one, false);
            var right = Primitive("Lane edge R", a.Cube, a.LaneEdge, root, Vector3.zero, Vector3.one, false);
            Set(view, "lane", lane);
            Set(view, "leftEdge", left);
            Set(view, "rightEdge", right);
            return view;
        }

        private static SlimeView BuildSlime(Transform parent, GameplayAssetsBuilder.Set a)
        {
            var root = Group("Slime", parent);
            var view = root.gameObject.AddComponent<SlimeView>();
            var body = Primitive("Body", a.Sphere, a.Jelly, root, new Vector3(0, 0.8f, 0), Vector3.one * 1.6f, true);
            var eyes = new Transform[2];
            for (var i = 0; i < 2; i++)
            {
                eyes[i] = Primitive("Eye " + i, a.Sphere, a.Ink, body, new Vector3(i == 0 ? -0.16f : 0.16f, 0.06f, -0.455f),
                    new Vector3(0.105f, 0.16f, 0.065f), false);
                Primitive("Catchlight", a.Sphere, a.Catchlight, eyes[i], new Vector3(-0.13f, 0.21f, -0.46f), Vector3.one * 0.25f, false);
            }
            var shadow = Primitive("Contact shadow", a.Quad, a.BlobShadow, root, Vector3.zero, Vector3.one, false);
            shadow.localRotation = Quaternion.Euler(90, 0, 0);
            var drop = Primitive("Drop", a.Sphere, a.Jelly, root, Vector3.zero, Vector3.one, true);
            drop.gameObject.SetActive(false);
            var bridge = new GameObject("Neck", typeof(MeshFilter), typeof(MeshRenderer));
            bridge.transform.SetParent(root, false);
            bridge.GetComponent<MeshRenderer>().sharedMaterial = a.Neck;
            bridge.SetActive(false);
            var splashes = Group("Splashes", root).gameObject.AddComponent<SplashPool>();
            Set(splashes, "defaultMaterial", a.Jelly);
            Set(splashes, "speckMesh", a.Sphere);
            Set(view, "body", body);
            SetArray(view, "eyes", eyes);
            Set(view, "drop", drop);
            Set(view, "bridge", bridge.GetComponent<MeshFilter>());
            Set(view, "splashes", splashes);
            Set(view, "shadow", shadow);
            SetFloat(view, "bodyYaw", 250f);
            return view;
        }

        private static CrystalFieldView BuildCrystals(Transform parent, GameplayAssetsBuilder.Set a)
        {
            var root = Group("Crystals", parent);
            var view = root.gameObject.AddComponent<CrystalFieldView>();
            var debris = Group("Debris", parent).gameObject.AddComponent<SplashPool>();
            Set(debris, "defaultMaterial", a.Glow);
            Set(debris, "speckMesh", a.Sphere);
            Set(view, "debris", debris);
            return view;
        }

        private static DoorsView BuildDoors(Transform parent, GameplayAssetsBuilder.Set a)
        {
            var root = Group("Finish Doors", parent);
            var view = root.gameObject.AddComponent<DoorsView>();
            Primitive("Pillar L", a.Cube, a.Plinth, root, new Vector3(-1.75f, 1.45f, 0), new Vector3(0.4f, 2.9f, 0.45f), true);
            Primitive("Pillar R", a.Cube, a.Plinth, root, new Vector3(1.75f, 1.45f, 0), new Vector3(0.4f, 2.9f, 0.45f), true);
            Primitive("Lintel", a.Cube, a.Rim, root, new Vector3(0, 3.05f, 0), new Vector3(3.95f, 0.36f, 0.5f), true);
            Primitive("Lintel glow", a.Cube, a.LaneEdge, root, new Vector3(0, 2.84f, -0.26f), new Vector3(3.2f, 0.06f, 0.04f), false);
            var hinges = new Transform[2];
            for (var i = 0; i < 2; i++)
            {
                var side = i == 0 ? -1 : 1;
                hinges[i] = Group(i == 0 ? "Hinge L" : "Hinge R", root);
                hinges[i].localPosition = new Vector3(1.53f * side, 0, 0);
                Primitive("Leaf", a.Cube, a.DoorLeaf, hinges[i], new Vector3(-0.765f * side, 1.3f, 0), new Vector3(1.5f, 2.6f, 0.14f), true);
                Primitive("Leaf glow", a.Cube, a.LaneEdge, hinges[i], new Vector3(-0.765f * side, 1.3f, -0.08f), new Vector3(0.07f, 2.1f, 0.03f), false);
            }
            Set(view, "leftHinge", hinges[0]);
            Set(view, "rightHinge", hinges[1]);
            return view;
        }

        private static Transform BuildColony(Transform parent, GameplayAssetsBuilder.Set a)
        {
            var root = Group("Colony", parent);
            Primitive("Colony basin", a.Cylinder, a.Rim, root, new Vector3(0, 0.04f, 1.2f), new Vector3(2.6f, 0.06f, 2.6f), false);
            Primitive("Living pool", a.Sphere, a.Jelly, root, new Vector3(0, 0.15f, 1.2f), new Vector3(2.1f, 0.6f, 2.1f), false);
            for (var i = 0; i < 13; i++)
            {
                var angle = Mathf.Lerp(-10, 190, i / 12f) * Mathf.Deg2Rad;
                Primitive("Membrane arch " + i, a.Sphere, a.Glow, root,
                    new Vector3(Mathf.Cos(angle) * 1.4f, 0.25f + Mathf.Sin(angle) * 2.2f, 1.2f), Vector3.one * 0.32f, false);
            }
            return root;
        }

        private static ImpactPreviewView BuildPreview(Transform parent, GameplayAssetsBuilder.Set a)
        {
            var root = Group("Blast Preview", parent);
            var view = root.gameObject.AddComponent<ImpactPreviewView>();
            var ring = new GameObject("Ring", typeof(MeshFilter), typeof(MeshRenderer));
            ring.transform.SetParent(root, false);
            var renderer = ring.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = a.BlastRing;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var marker = Primitive("Contact marker", a.Cylinder, a.LaneEdge, root, Vector3.zero, new Vector3(0.16f, 0.004f, 0.16f), false);
            Set(view, "ring", ring.GetComponent<MeshFilter>());
            Set(view, "ringRenderer", renderer);
            Set(view, "marker", marker);
            return view;
        }

        private static CameraRig ConfigureCamera(GameplayAssetsBuilder.Set a)
        {
            var camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("Game scene has no main camera.");
            camera.orthographic = true;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = GameplayAssetsBuilder.Hex("101E29");
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            var rig = camera.GetComponent<CameraRig>();
            if (rig == null) rig = camera.gameObject.AddComponent<CameraRig>();
            // Initial editor framing so the saved scene already looks like the game.
            var euler = a.ViewConfig.cameraEuler;
            camera.transform.rotation = Quaternion.Euler(euler);
            camera.transform.position = Vector3.forward * a.ViewConfig.lookAhead - camera.transform.forward * a.ViewConfig.cameraDistance;
            camera.orthographicSize = 9;
            return rig;
        }

        // ------------------------------------------------------------------ game UI

        private static Game.Integration.Game.ResultView BuildGameView(GameplayAssetsBuilder.Set a)
        {
            var gameView = Object.FindFirstObjectByType<Game.Integration.Game.GameView>(FindObjectsInactive.Include);
            if (gameView == null) throw new InvalidOperationException("Game scene has no GameView.");
            var screen = gameView.transform;
            var buttonSquare = FindSprite(screen, "button_square_normal");
            var buttonPrimary = FindSprite(screen.root, "button_primary_normal");
            foreach (var name in new[] { "Size meter", "Retry", "Level", "Hint" })
            {
                var old = screen.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            var previousResult = screen.parent.GetComponentInChildren<Game.Integration.Game.ResultView>(true);
            if (previousResult != null) Object.DestroyImmediate(previousResult.gameObject);
            var root = screen;

            // Size meter: the player's resource (D between Dcrit and D0), with the share being charged highlighted.
            var meter = Rect("Size meter", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -52), new Vector2(560, 64));
            Fill(meter, new Color(0.06f, 0.12f, 0.16f, 0.85f));
            var charge = Rect("Charge", meter, Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            Fill(charge, GameplayAssetsBuilder.Hex("FF6654"));
            var fill = Rect("Fill", meter, Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            Fill(fill, GameplayAssetsBuilder.Hex("58F044"));
            var sizeLabel = Label("Size label", meter, "SIZE  100%", 34, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            sizeLabel.fontStyle = FontStyles.Bold;
            sizeLabel.outlineWidth = 0.2f;
            sizeLabel.outlineColor = new Color32(9, 47, 50, 255);

            var restart = MakeButton("Retry", root, buttonSquare, new Vector2(0, 1), new Vector2(32, -32), new Vector2(190, 125), "RETRY", 40);
            var seed = Label("Level", root, "LEVEL", 34, new Color(0.75f, 0.88f, 0.86f, 0.95f), Vector2.zero, Vector2.zero, new Vector2(30, 24), new Vector2(1000, 50));
            seed.alignment = TextAlignmentOptions.BottomLeft;
            seed.rectTransform.pivot = Vector2.zero;
            var hint = Label("Hint", root, "HOLD TO CHARGE  ·  RELEASE TO SHOOT", 38, new Color(0.89f, 0.95f, 0.92f, 0.95f),
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 250), new Vector2(980, 190));
            hint.enableWordWrapping = true;
            hint.outlineWidth = 0.18f;
            hint.outlineColor = new Color32(9, 30, 36, 255);
            hint.fontStyle = FontStyles.Bold;

            var panel = Rect("ResultView", screen.parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            panel.SetSiblingIndex(screen.GetSiblingIndex() + 1);
            var resultView = panel.gameObject.AddComponent<Game.Integration.Game.ResultView>();
            Fill(panel, new Color(0.04f, 0.08f, 0.1f, 0.6f)).raycastTarget = true;
            var card = Rect("Card", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 700));
            Fill(card, new Color(0.1f, 0.18f, 0.23f, 0.96f));
            var title = Label("Title", card, "HOME AT LAST!", 84, GameplayAssetsBuilder.Hex("E1FF86"), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -125), new Vector2(0, 130));
            title.fontStyle = FontStyles.Bold;
            title.rectTransform.pivot = new Vector2(0.5f, 1);
            var subtitle = Label("Subtitle", card, "", 40, new Color(0.89f, 0.95f, 0.92f), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 90), new Vector2(-80, 140));
            var again = MakeButton("Primary", card, buttonPrimary, new Vector2(0.5f, 0), new Vector2(0, 190), new Vector2(520, 150), "NEXT LEVEL", 50);
            again.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0);
            var secondary = MakeButton("Secondary", card, buttonSquare, new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(380, 115), "RETRY", 40);
            secondary.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0);

            Set(gameView, "sizeFill", fill);
            Set(gameView, "chargeFill", charge);
            Set(gameView, "sizeLabel", sizeLabel);
            Set(gameView, "seedLabel", seed);
            Set(gameView, "hintLabel", hint);
            Set(gameView, "restartButton", restart);
            Set(resultView, "title", title);
            Set(resultView, "subtitle", subtitle);
            Set(resultView, "primaryButton", again);
            Set(resultView, "primaryLabel", again.GetComponentInChildren<TMP_Text>());
            Set(resultView, "secondaryButton", secondary);
            Set(resultView, "secondaryLabel", secondary.GetComponentInChildren<TMP_Text>());
            var menu = MakeButton("Menu", card, buttonSquare, new Vector2(0, 1), new Vector2(24, -24), new Vector2(190, 90), "MENU", 34);
            Set(resultView, "menuButton", menu);
            Set(resultView, "card", card);
            Set(resultView, "dim", panel.GetComponent<UnityEngine.UI.Image>());
            panel.gameObject.SetActive(false);
            return resultView;
        }

        /// <summary>Particle splash of the colony pool: fountain, crown of drops, surface ripples.</summary>
        private static PoolSplashEffect BuildPoolSplash(Transform colony, GameplayAssetsBuilder.Set a)
        {
            var root = Group("Pool splash", colony);
            root.localPosition = new Vector3(0, 0.45f, 1.2f);
            var effect = root.gameObject.AddComponent<PoolSplashEffect>();
            var jelly = GameplayAssetsBuilder.Hex("58F044");
            var light = GameplayAssetsBuilder.Hex("E1FF86");
            var top = GameplayAssetsBuilder.Hex("17DFB0");

            var fountain = Particles("Fountain", root, a.SplashDrop, a.Sphere, 120);
            {
                var main = fountain.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.05f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(4.2f, 7.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
                main.gravityModifier = 1.5f;
                main.startColor = new ParticleSystem.MinMaxGradient(jelly, light);
                var shape = fountain.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 20;
                shape.radius = 0.3f;
                shape.rotation = new Vector3(-90, 0, 0);
                Shrink(fountain, 0.3f);
            }

            var crown = Particles("Crown", root, a.SplashDrop, a.Sphere, 100);
            {
                var main = crown.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(2.6f, 4.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
                main.gravityModifier = 1.8f;
                main.startColor = new ParticleSystem.MinMaxGradient(top, light);
                var shape = crown.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.55f;
                shape.radiusThickness = 0;
                shape.arc = 360;
                shape.rotation = new Vector3(90, 0, 0);
                var velocity = crown.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = new ParticleSystem.MinMaxCurve(0);
                velocity.y = new ParticleSystem.MinMaxCurve(3.4f);
                velocity.z = new ParticleSystem.MinMaxCurve(0);
                Shrink(crown, 0.25f);
            }

            var ripples = Particles("Ripples", root, a.SplashRipple, null, 6);
            {
                ripples.transform.localPosition = new Vector3(0, 0.02f, 0);
                var main = ripples.main;
                main.duration = 0.6f;
                main.startLifetime = 0.95f;
                main.startSpeed = 0;
                main.startSize = 1.1f;
                main.startColor = new Color(0.88f, 1f, 0.53f, 0.85f);
                var emission = ripples.emission;
                emission.enabled = true;
                emission.rateOverTime = 0;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0, 1), new ParticleSystem.Burst(0.14f, 1), new ParticleSystem.Burst(0.3f, 1) });
                var shape = ripples.shape;
                shape.enabled = false;
                var size = ripples.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 0.5f, 1, 3.6f));
                var color = ripples.colorOverLifetime;
                color.enabled = true;
                var fade = new Gradient();
                fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                    new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
                color.color = new ParticleSystem.MinMaxGradient(fade);
                var renderer = ripples.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            }

            Set(effect, "fountain", fountain);
            Set(effect, "crown", crown);
            Set(effect, "ripples", ripples);
            return effect;
        }

        private static ParticleSystem Particles(string name, Transform parent, Material material, Mesh mesh, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = system.emission;
            emission.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (mesh != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Mesh;
                renderer.mesh = mesh;
            }
            return system;
        }

        private static void Shrink(ParticleSystem system, float end)
        {
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, end));
        }

        /// <summary>Tutorial overlay on top of the game screen: shader dimmer with cut-outs, tap catcher, card, SKIP.</summary>
        private static TutorialOverlay BuildTutorial(GameplayAssetsBuilder.Set a)
        {
            var gameView = Object.FindFirstObjectByType<Game.Integration.Game.GameView>(FindObjectsInactive.Include);
            var screen = gameView.transform;
            var old = screen.Find("Tutorial");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var buttonSquare = FindSprite(screen.root, "button_square_normal");
            var root = Rect("Tutorial", screen, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            root.SetAsLastSibling();
            var overlay = root.gameObject.AddComponent<TutorialOverlay>();

            var dimmer = Rect("Dimmer", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 400));
            var dimImage = Fill(dimmer, Color.white);
            dimImage.material = a.Spotlight;
            dimImage.raycastTarget = true;

            var tap = Rect("Continue", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var tapImage = Fill(tap, new Color(0, 0, 0, 0));
            tapImage.raycastTarget = true;
            var tapButton = tap.gameObject.AddComponent<global::Game.UI.FeedbackButton>();
            tapButton.transition = Selectable.Transition.None;

            var card = Rect("Card", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 300), new Vector2(940, 300));
            Fill(card, new Color(0.07f, 0.13f, 0.17f, 0.94f));
            var message = Label("Message", card, "", 46, Color.white, Vector2.zero, Vector2.one, new Vector2(0, 22), new Vector2(-70, -80));
            message.enableWordWrapping = true;
            message.fontStyle = FontStyles.Bold;
            var hint = Label("Tap to continue", card, "TAP TO CONTINUE", 28, GameplayAssetsBuilder.Hex("E1FF86"), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 34), new Vector2(0, 40));

            var skip = MakeButton("Skip", root, buttonSquare, new Vector2(1, 0), new Vector2(-32, 40), new Vector2(200, 95), "SKIP", 36);

            Set(overlay, "dimmer", dimImage);
            Set(overlay, "card", card);
            Set(overlay, "message", message);
            Set(overlay, "continueLabel", hint);
            Set(overlay, "continueButton", tapButton);
            Set(overlay, "skipButton", skip);
            return overlay;
        }

        /// <summary>Mode choice in the existing menu: the old Play button becomes LEVELS, a WALL button is added above it.</summary>
        private static void ConfigureMenu()
        {
            var menu = Object.FindFirstObjectByType<Game.Integration.Menu.MenuView>(FindObjectsInactive.Include);
            if (menu == null) return; // Menu is in its own scene after the scene-flow installation.
            var play = new SerializedObject(menu).FindProperty("playButton").objectReferenceValue as Button;
            if (play == null) throw new InvalidOperationException("MenuView has no play button.");
            play.GetComponentInChildren<TMP_Text>().text = "LEVELS";
            var old = play.transform.parent.Find("WallButton");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var wall = Object.Instantiate(play.gameObject, play.transform.parent);
            wall.name = "WallButton";
            var rect = wall.GetComponent<RectTransform>();
            rect.anchoredPosition = play.GetComponent<RectTransform>().anchoredPosition + new Vector2(0, 220);
            wall.GetComponentInChildren<TMP_Text>().text = "WALL";
            Set(menu, "wallButton", wall.GetComponent<Button>());
        }

        /// <summary>Level grid over the menu: title, 5-column grid of level cells (cloned from a template), BACK.</summary>
        private static Game.Integration.Menu.LevelSelectView BuildLevelSelect()
        {
            var menu = Object.FindFirstObjectByType<Game.Integration.Menu.MenuView>(FindObjectsInactive.Include);
            if (menu == null) return null;
            var parent = menu.transform.parent;
            var old = parent.Find("LevelSelect");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var buttonSquare = FindSprite(parent.root, "button_square_normal");
            var buttonPrimary = FindSprite(parent.root, "button_primary_normal");

            var root = Rect("LevelSelect", parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            root.SetSiblingIndex(menu.transform.GetSiblingIndex() + 1);
            Fill(root, new Color(0.05f, 0.1f, 0.13f, 1f)).raycastTarget = true;
            var view = root.gameObject.AddComponent<Game.Integration.Menu.LevelSelectView>();
            var title = Label("Title", root, "LEVELS", 110, GameplayAssetsBuilder.Hex("E1FF86"), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -260), new Vector2(0, 160));
            title.fontStyle = FontStyles.Bold;

            var grid = Rect("Grid", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(930, 380));
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(150, 150);
            layout.spacing = new Vector2(40, 40);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 5;
            layout.childAlignment = TextAnchor.UpperCenter;

            var template = MakeButton("Cell", grid, buttonSquare, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 150), "1", 72);
            template.GetComponentInChildren<TMP_Text>().color = Color.white;
            template.GetComponentInChildren<TMP_Text>().outlineWidth = 0.15f;
            var colors = template.colors;
            colors.disabledColor = Color.white;
            template.colors = colors;
            template.gameObject.SetActive(false);

            var back = MakeButton("Back", root, buttonPrimary, new Vector2(0.5f, 0), new Vector2(0, 140), new Vector2(450, 150), "BACK", 52);
            back.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0);

            Set(view, "grid", grid);
            Set(view, "cellTemplate", template);
            Set(view, "backButton", back);
            root.gameObject.SetActive(false);
            return view;
        }

        private static Sprite FindSprite(Transform root, string name)
        {
            foreach (var image in root.GetComponentsInChildren<Image>(true))
                if (image.sprite != null && image.sprite.name == name) return image.sprite;
            return null;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }

        private static Image Fill(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI Label(string name, Transform parent, string text, float size, Color color, Vector2 min, Vector2 max,
            Vector2 position, Vector2 delta)
        {
            var rect = Rect(name, parent, min, max, new Vector2(0.5f, 0.5f), position, delta);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        private static Button MakeButton(string name, Transform parent, Sprite sprite, Vector2 anchor, Vector2 position, Vector2 size, string text, float fontSize)
        {
            var pivot = new Vector2(anchor.x, anchor.y);
            var rect = Rect(name, parent, anchor, anchor, pivot, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null && sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            var button = rect.gameObject.AddComponent<global::Game.UI.FeedbackButton>();
            var label = Label("Label", rect, text, fontSize, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label.fontStyle = FontStyles.Bold;
            return button;
        }

        // ------------------------------------------------------------------ helpers

        private static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Transform Primitive(string name, Mesh mesh, Material material, Transform parent, Vector3 position, Vector3 scale, bool shadows)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go.transform;
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name}.{field} not found");
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Object target, string field, Transform[] values)
        {
            var so = new SerializedObject(target);
            var array = so.FindProperty(field);
            array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
