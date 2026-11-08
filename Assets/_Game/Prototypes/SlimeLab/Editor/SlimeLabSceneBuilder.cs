using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Game.Prototypes.SlimeLab.Editor
{
    public static class SlimeLabSceneBuilder
    {
        public const string Root = "Assets/_Game/Prototypes/SlimeLab";
        public const string ScenePath = Root + "/Scenes/SlimeLab.unity";
        private const string Materials = Root + "/Materials";

        [MenuItem("Tools/Ball Game/Slime Lab/Open Test Scene")]
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath)) { CreateScene(); return; }
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before opening Slime Lab.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Tools/Ball Game/Slime Lab/Create Test Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before creating Slime Lab.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Slime Lab already exists. Use Open Test Scene.");
            // Keep the user's current scenes open and untouched while authoring this isolated scene.
            Directory.CreateDirectory(Root + "/Scenes");
            Directory.CreateDirectory(Materials);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var jelly = JellyMaterial("Jelly Mint", SlimeLabPalette.Jelly, 0.017f);
            var neck = JellyMaterial("Jelly Neck", SlimeLabPalette.Jelly, 0);
            var ground = LitMaterial("Backdrop", SlimeLabPalette.Floor, 0.15f);
            var platform = LitMaterial("Ceramic Plinth", SlimeLabPalette.Platform, 0.36f);
            var trim = LitMaterial("Rim", SlimeLabPalette.Trim, 0.45f);
            var dark = LitMaterial("Face Ink", SlimeLabPalette.Ink, 0.7f);
            var light = LitMaterial("Eye Catchlight", SlimeLabPalette.JellyLight, 0.4f);
            var crystal = CrystalMaterial("Crystal Coral", SlimeLabPalette.Coral, false);
            var crystalPurple = CrystalMaterial("Crystal Amethyst", SlimeLabPalette.Amethyst, true);
            var glow = LitMaterial("Colony Glow", SlimeLabPalette.JellyLight, 0.55f);
            var shardMesh = CreateCrystalMesh();
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", SlimeLabPalette.JellyLight * 1.2f);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = SlimeLabPalette.Muted * 0.65f;
            RenderSettings.ambientEquatorColor = SlimeLabPalette.Trim * 0.65f;
            RenderSettings.ambientGroundColor = SlimeLabPalette.Background;
            RenderSettings.fog = false;
            var key = new GameObject("Warm studio key").AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = SlimeLabPalette.Key;
            key.intensity = 1.25f;
            key.shadows = LightShadows.Soft;
            key.shadowBias = 0.025f;
            key.transform.rotation = Quaternion.Euler(43, -35, 0);

            var camera = new GameObject("Slime Lab Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(5.8f, 5.1f, -10.8f);
            camera.transform.LookAt(new Vector3(0.35f, 0.8f, 0));
            camera.orthographic = true;
            camera.orthographicSize = 4.5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 60;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = SlimeLabPalette.Background;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            Primitive("Backdrop floor", PrimitiveType.Cube, null, ground,
                new Vector3(0, -0.68f, 0), new Vector3(200, 0.2f, 200));
            var stage = new GameObject("Ceramic test course").transform;
            Primitive("Rounded plinth", PrimitiveType.Cylinder, stage, platform,
                new Vector3(0.7f, -0.29f, 0), new Vector3(10.8f, 0.28f, 4.6f));
            Primitive("Plinth lower rim", PrimitiveType.Cylinder, stage, trim,
                new Vector3(0.7f, -0.43f, 0), new Vector3(10.95f, 0.06f, 4.75f));
            // A restrained line of stepping stones gives the hops a visible travel reference.
            for (var i = 0; i < 9; i++)
                Primitive("Path marker " + i, PrimitiveType.Cylinder, stage, trim,
                    new Vector3(-2.9f + i * 0.78f, 0.005f, 0), new Vector3(0.35f, 0.009f, 0.35f));

            var root = new GameObject("Slime Lab");
            var demo = root.AddComponent<SlimeLabDemo>();
            var body = Primitive("Slime body - ordinary Unity sphere", PrimitiveType.Sphere, root.transform, jelly,
                new Vector3(-2.45f, 1, 0), Vector3.one * 1.9f).transform;
            var eyes = new Transform[2];
            for (var i = 0; i < 2; i++)
            {
                eyes[i] = Primitive("Eye " + i, PrimitiveType.Sphere, body, dark,
                    new Vector3(i == 0 ? -0.16f : 0.16f, 0.06f, -0.455f), new Vector3(0.105f, 0.16f, 0.065f)).transform;
                Primitive("Catchlight", PrimitiveType.Sphere, eyes[i], light,
                    new Vector3(-0.13f, 0.21f, -0.46f), Vector3.one * 0.25f);
            }
            var drop = Primitive("Separated drop - ordinary Unity sphere", PrimitiveType.Sphere, root.transform,
                jelly, Vector3.zero, Vector3.one).transform;
            drop.gameObject.SetActive(false);
            var bridgeObject = new GameObject("Stretching neck", typeof(MeshFilter), typeof(MeshRenderer));
            bridgeObject.transform.SetParent(root.transform, false);
            bridgeObject.GetComponent<MeshRenderer>().sharedMaterial = neck;
            bridgeObject.SetActive(false);

            var target = new GameObject("Drop impact point").transform;
            target.SetParent(root.transform, false);
            target.position = new Vector3(2.5f, 0.7f, 0);
            var cluster = new GameObject("Dissolvable crystal crust").transform;
            var crystalPositions = new[]
            {
                new Vector3(2.5f, 0, 0), new Vector3(2.9f, 0, 0.52f), new Vector3(3.0f, 0, -0.5f),
                new Vector3(3.55f, 0, 0.2f), new Vector3(3.45f, 0, -0.8f), new Vector3(2.4f, 0, 0.95f)
            };
            var crystals = new Transform[crystalPositions.Length];
            for (var i = 0; i < crystals.Length; i++)
            {
                var anchor = new GameObject("Crystal " + i).transform;
                anchor.SetParent(cluster, false);
                anchor.position = crystalPositions[i];
                var height = 0.7f + i % 3 * 0.24f;
                var shard = new GameObject("Shard", typeof(MeshFilter), typeof(MeshRenderer));
                shard.transform.SetParent(anchor, false);
                shard.transform.localScale = new Vector3(0.64f, height, 0.64f);
                shard.GetComponent<MeshFilter>().sharedMesh = shardMesh;
                shard.GetComponent<MeshRenderer>().sharedMaterial = i % 2 == 0 ? crystal : crystalPurple;
                shard.transform.localRotation = Quaternion.Euler(i % 2 == 0 ? 8 : -9, 30 + i * 28, 6 - i * 3);
                crystals[i] = anchor;
            }
            var colony = new GameObject("Colony / home").transform;
            colony.position = new Vector3(4.5f, 0, 0);
            Primitive("Colony basin", PrimitiveType.Cylinder, colony, trim,
                new Vector3(0, 0.04f, 0), new Vector3(1.2f, 0.06f, 1.2f));
            Primitive("Living pool", PrimitiveType.Sphere, colony, jelly,
                new Vector3(0, 0.15f, 0), new Vector3(0.95f, 0.3f, 0.95f));
            for (var i = 0; i < 13; i++)
            {
                var a = Mathf.Lerp(-10, 190, i / 12f) * Mathf.Deg2Rad;
                Primitive("Membrane arch " + i, PrimitiveType.Sphere, colony, glow,
                    new Vector3(0, 0.2f + Mathf.Sin(a) * 1.2f, Mathf.Cos(a) * 0.68f), Vector3.one * 0.23f);
            }

            Set(demo, "body", body);
            Set(demo, "drop", drop);
            Set(demo, "bridge", bridgeObject.GetComponent<MeshFilter>());
            Set(demo, "aimPoint", target);
            Set(demo, "jellyMaterial", jelly);
            Set(demo, "viewCamera", camera);
            SetArray(demo, "crystals", crystals);
            SetArray(demo, "eyes", eyes);
            CreateHud(demo, camera);
            CreateVolume();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(0.3f, 0.8f, 0), camera.transform.rotation, 8);
            Debug.Log("Slime Lab created. Play: automatic demo. Hold pointer/Space to charge; release to split. J: hop, A: auto, R: reset.");
        }

        private static void CreateVolume()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<Tonemapping>(true).mode.value = TonemappingMode.Neutral;
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.value = 0.2f;
            bloom.threshold.value = 1.1f;
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = 0.15f;
            vignette.color.value = SlimeLabPalette.Background;
            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.value = 4;
            color.contrast.value = 4;
            AssetDatabase.CreateAsset(profile, Root + "/Materials/Slime Lab Volume.asset");
            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            var volume = new GameObject("Slime Lab grading").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10;
            volume.sharedProfile = profile;
        }

        private static void CreateHud(SlimeLabDemo demo, Camera camera)
        {
            var canvasObject = new GameObject("Slime Lab HUD", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900);
            scaler.matchWidthOrHeight = 0.5f;
            var hud = canvasObject.AddComponent<SlimeLabHud>();
            var top = Rect("Header", canvas.transform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0.5f, 1), new Vector2(0, -32), new Vector2(-88, 110));
            var title = Label("Title", top, "SLIME / MOTION LAB", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(0, -8), new Vector2(650, 48), 32, SlimeLabPalette.Text);
            title.fontStyle = FontStyles.Bold;
            Label("Subtitle", top, "A LITTLE BODY. A LONG WAY HOME.", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(0, -60), new Vector2(650, 30), 14, SlimeLabPalette.Muted);
            var mass = Label("Mass", top, "BODY  100%", new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(0, -12), new Vector2(260, 36), 20, SlimeLabPalette.JellyLight);
            mass.alignment = TextAlignmentOptions.Right;
            var track = Rect("Mass track", top, Vector2.one, Vector2.one, Vector2.one,
                new Vector2(0, -58), new Vector2(230, 6));
            track.gameObject.AddComponent<UnityEngine.UI.Image>().color = SlimeLabPalette.Trim;
            var fill = Rect("Mass fill", track, Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            fill.gameObject.AddComponent<UnityEngine.UI.Image>().color = SlimeLabPalette.Jelly;
            var footer = Rect("Footer", canvas.transform, Vector2.zero, Vector2.right,
                new Vector2(0.5f, 0), new Vector2(0, 34), new Vector2(-88, 100));
            var state = Label("State", footer, "IDLE / SOFT JELLY", new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(0, 45), new Vector2(680, 32), 18, SlimeLabPalette.JellyLight);
            Label("Controls hint", footer, "HOLD SPACE / POINTER TO CHARGE   |   RELEASE TO SPLIT", Vector2.zero, Vector2.zero,
                new Vector2(0, 8), new Vector2(720, 28), 13, SlimeLabPalette.Muted);
            var controls = Rect("Buttons", footer, Vector2.right, Vector2.right, Vector2.right,
                Vector2.zero, new Vector2(500, 80));
            var auto = Button("Auto", controls, "AUTO DEMO / ON", new Vector2(-370, 14), new Vector2(190, 48));
            var hop = Button("Hop", controls, "HOP / J", new Vector2(-205, 14), new Vector2(112, 48));
            var reset = Button("Reset", controls, "RESET / R", new Vector2(-65, 14), new Vector2(126, 48));
            Set(hud, "demo", demo);
            Set(hud, "stateLabel", state);
            Set(hud, "massLabel", mass);
            Set(hud, "massFill", fill);
            Set(hud, "autoLabel", auto.GetComponentInChildren<TMP_Text>());
            Set(hud, "controls", controls);
            Set(hud, "autoButton", auto);
            Set(hud, "hopButton", hop);
            Set(hud, "resetButton", reset);
            Set(demo, "hud", hud);
            var events = new GameObject("Slime Lab EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private static Material JellyMaterial(string name, Color color, float wobble)
        {
            var shader = Shader.Find("BallGame/Prototypes/Slime Jelly");
            if (shader == null) throw new InvalidOperationException("Slime Jelly shader has not imported yet.");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetColor("_RimColor", SlimeLabPalette.JellyLight);
            material.SetColor("_TopColor", SlimeLabPalette.JellyTop);
            material.SetColor("_HighlightColor", SlimeLabPalette.Highlight * 1.6f);
            material.SetColor("_ShadowTint", SlimeLabPalette.Hex("3DC2AB"));
            material.SetFloat("_Gloss", 48);
            material.SetFloat("_Brightness", 1.05f);
            material.SetFloat("_RimStrength", 0.9f);
            material.SetFloat("_BubbleStrength", 0.22f);
            material.SetFloat("_Wobble", wobble);
            AssetDatabase.CreateAsset(material, Materials + "/" + name + ".mat");
            return material;
        }

        private static Material CrystalMaterial(string name, Color color, bool purple)
        {
            var shader = Shader.Find("BallGame/Prototypes/Cartoon Crystal");
            if (shader == null) throw new InvalidOperationException("Cartoon Crystal shader has not imported yet.");
            var path = Materials + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor("_BaseColor", color);
            material.SetColor("_ShadowTint", SlimeLabPalette.Hex(purple ? "7759B9" : "A65B99"));
            material.SetColor("_TipColor", SlimeLabPalette.Hex(purple ? "F4C8FF" : "FFD09C"));
            material.SetColor("_EdgeColor", SlimeLabPalette.Hex(purple ? "F2D4FF" : "FFE0B3"));
            material.SetFloat("_Brightness", 1.05f);
            material.SetFloat("_EdgeWidth", 0.006f);
            material.SetFloat("_EdgeStrength", 0.28f);
            EditorUtility.SetDirty(material);
            return material;
        }

        [MenuItem("Tools/Ball Game/Slime Lab/Apply Reference Cartoon Look")]
        public static void ApplyCartoonLook()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before applying the saved look.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("Open the Slime Lab test scene first.");
            foreach (var name in new[] { "Jelly Mint", "Jelly Neck" })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/" + name + ".mat");
                if (material == null) throw new InvalidOperationException("Missing Slime Lab material: " + name);
                material.SetColor("_BaseColor", SlimeLabPalette.Jelly);
                material.SetColor("_TopColor", SlimeLabPalette.JellyTop);
                material.SetColor("_RimColor", SlimeLabPalette.JellyLight);
                material.SetColor("_HighlightColor", SlimeLabPalette.Highlight * 1.6f);
                material.SetColor("_ShadowTint", SlimeLabPalette.Hex("3DC2AB"));
                material.SetFloat("_Gloss", 48);
                material.SetFloat("_Brightness", 1.05f);
                material.SetFloat("_RimStrength", 0.9f);
                material.SetFloat("_BubbleStrength", 0.22f);
                EditorUtility.SetDirty(material);
            }
            var coral = CrystalMaterial("Crystal Coral", SlimeLabPalette.Coral, false);
            var purple = CrystalMaterial("Crystal Amethyst", SlimeLabPalette.Amethyst, true);
            var cluster = GameObject.Find("Dissolvable crystal crust");
            if (cluster == null) throw new InvalidOperationException("Missing Slime Lab crystal cluster.");
            for (var i = 0; i < cluster.transform.childCount; i++)
                cluster.transform.GetChild(i).GetComponentInChildren<MeshRenderer>().sharedMaterial = i % 2 == 0 ? coral : purple;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Reference cartoon look applied to Slime Lab jelly and crystals.");
        }

        public static Mesh CreateCrystalMesh()
        {
            var path = Root + "/Materials/Faceted Crystal.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            var points = new System.Collections.Generic.List<Vector3>();
            for (var side = 0; side < 6; side++)
            {
                var a = side * Mathf.PI / 3;
                var b = (side + 1) * Mathf.PI / 3;
                var lowerA = new Vector3(Mathf.Cos(a) * 0.5f, 0, Mathf.Sin(a) * 0.5f);
                var lowerB = new Vector3(Mathf.Cos(b) * 0.5f, 0, Mathf.Sin(b) * 0.5f);
                var upperA = new Vector3(lowerA.x * 0.82f, 0.7f, lowerA.z * 0.82f);
                var upperB = new Vector3(lowerB.x * 0.82f, 0.7f, lowerB.z * 0.82f);
                var tip = new Vector3(0.08f, 1, -0.04f);
                points.AddRange(new[] { lowerA, upperA, lowerB, lowerB, upperA, upperB, upperA, tip, upperB });
            }
            var indices = new int[points.Count];
            for (var i = 0; i < indices.Length; i++) indices[i] = i;
            var mesh = new Mesh { name = "Faceted Crystal" };
            mesh.SetVertices(points);
            mesh.triangles = indices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Material LitMaterial(string name, Color color, float smoothness)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, Materials + "/" + name + ".mat");
            return material;
        }

        private static GameObject Primitive(string name, PrimitiveType type, Transform parent, Material material,
            Vector3 position, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pivot,
            Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static TextMeshProUGUI Label(string name, Transform parent, string value, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size, float fontSize, Color color)
        {
            var rect = Rect(name, parent, anchor, anchor, pivot, position, size);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.text = value;
            label.fontSize = fontSize;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        private static UnityEngine.UI.Button Button(string name, Transform parent, string value, Vector2 position, Vector2 size)
        {
            var rect = Rect(name, parent, Vector2.right, Vector2.right, new Vector2(0.5f, 0), position, size);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = SlimeLabPalette.Platform;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            var colors = button.colors;
            colors.normalColor = SlimeLabPalette.Text;
            colors.highlightedColor = SlimeLabPalette.JellyLight;
            colors.pressedColor = SlimeLabPalette.Jelly;
            button.colors = colors;
            var label = Label("Label", rect, value, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, size, 13, SlimeLabPalette.Text);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(UnityEngine.Object target, string field, Transform[] values)
        {
            var serialized = new SerializedObject(target);
            var array = serialized.FindProperty(field);
            array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
