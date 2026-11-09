using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Assigns the generated mascot icon set without changing other Player settings.</summary>
    public static class GameIconInstaller
    {
        public const string IconRoot = "Assets/_Game/Art/AppIcon/v01";
        private const string ReportPath = "Temp/ArtValidation/AppIcon/unity-validation.json";

        [Serializable] private sealed class Slot
        {
            public string platform, kind;
            public int width, height;
            public string[] textures;
        }

        [Serializable] private sealed class Report
        {
            public string unityVersion;
            public string activeBuildTarget;
            public string iosAssignment;
            public int pngFiles;
            public bool passed;
            public List<Slot> slots = new List<Slot>();
        }

        [MenuItem("Tools/Ball Game/Icons/Apply Generated Icon Set")]
        public static void Apply()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var file in Directory.GetFiles(IconRoot, "*.png", SearchOption.AllDirectories))
            {
                var path = file.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing icon importer: " + path);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.crunchedCompression = false;
                importer.maxTextureSize = 1024;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = path.Contains("/Foreground_") || path.Contains("/Round/");
                importer.isReadable = false;
                importer.SaveAndReimport();
            }

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { Load("SlimeHomecoming_1024.png") }, IconKind.Application);
            PlayerSettings.SetIcons(NamedBuildTarget.Standalone,
                PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Application)
                    .Select(size => Load($"Desktop/Icon_{size}.png")).ToArray(), IconKind.Application);

            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                var slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                foreach (var slot in slots)
                {
                    if (slot.minLayerCount == 2)
                        // Unity's adaptive layer order is background, foreground.
                        slot.SetTextures(Load($"Android/Adaptive/Background_{slot.width}.png"),
                            Load($"Android/Adaptive/Foreground_{slot.width}.png"));
                    else if (kind.ToString().StartsWith("Round", StringComparison.Ordinal))
                        slot.SetTexture(Load($"Android/Round/Round_{slot.width}.png"));
                    else
                        slot.SetTexture(Load($"Android/Legacy/Legacy_{slot.width}.png"));
                }
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, slots);
            }

            var iosKinds = PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.iOS);
            if (iosKinds.Length > 0)
            {
                foreach (var kind in iosKinds)
                {
                    var slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.iOS, kind);
                    foreach (var slot in slots) slot.SetTexture(Load($"iOS/Icon_{slot.width}.png"));
                    PlayerSettings.SetPlatformIcons(NamedBuildTarget.iOS, kind, slots);
                }
            }
            else
            {
                // The iOS module is not installed on this Windows Editor. Populate its existing
                // serialized platform slots through Unity so the settings travel with the project.
                PlayerSettings.SetIcons(NamedBuildTarget.iOS,
                    PlayerSettings.GetIconSizes(NamedBuildTarget.iOS, IconKind.Application)
                        .Select(size => Load($"iOS/Icon_{size}.png")).ToArray(), IconKind.Application);
                var settings = SerializedSettings();
                var platforms = settings.FindProperty("m_BuildTargetPlatformIcons");
                for (var i = 0; i < platforms.arraySize; i++)
                {
                    var platform = platforms.GetArrayElementAtIndex(i);
                    if (platform.FindPropertyRelative("m_BuildTarget").stringValue != "iPhone") continue;
                    var slots = platform.FindPropertyRelative("m_Icons");
                    for (var j = 0; j < slots.arraySize; j++)
                    {
                        var slot = slots.GetArrayElementAtIndex(j);
                        var size = slot.FindPropertyRelative("m_Width").intValue;
                        var textures = slot.FindPropertyRelative("m_Textures");
                        textures.arraySize = 1;
                        textures.GetArrayElementAtIndex(0).objectReferenceValue = Load($"iOS/Icon_{size}.png");
                    }
                }
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssets();
            Validate();
        }

        [MenuItem("Tools/Ball Game/Icons/Validate Icon Assignments")]
        public static void Validate()
        {
            var report = new Report
            {
                unityVersion = Application.unityVersion,
                activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                pngFiles = Directory.GetFiles(IconRoot, "*.png", SearchOption.AllDirectories).Length,
                iosAssignment = PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.iOS).Length == 0
                    ? "Serialized iOS slots assigned; iOS module is not installed in this Editor."
                    : "Assigned with PlayerSettings.SetPlatformIcons."
            };
            AddSlot(report, "Default", "Application", 1024,
                PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Application), "SlimeHomecoming_1024.png");
            var desktopSizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Application);
            var desktopIcons = PlayerSettings.GetIcons(NamedBuildTarget.Standalone, IconKind.Application);
            for (var i = 0; i < desktopSizes.Length; i++)
                AddSlot(report, "Standalone", "Application", desktopSizes[i], new[] { desktopIcons[i] },
                    $"Desktop/Icon_{desktopSizes[i]}.png");
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                foreach (var slot in PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind))
                {
                    var paths = slot.minLayerCount == 2
                        ? new[] { $"Android/Adaptive/Background_{slot.width}.png", $"Android/Adaptive/Foreground_{slot.width}.png" }
                        : new[] { kind.ToString().StartsWith("Round", StringComparison.Ordinal)
                            ? $"Android/Round/Round_{slot.width}.png" : $"Android/Legacy/Legacy_{slot.width}.png" };
                    AddSlot(report, "Android", kind.ToString(), slot.width, slot.GetTextures(), paths);
                }
            }
            var platforms = SerializedSettings().FindProperty("m_BuildTargetPlatformIcons");
            for (var i = 0; i < platforms.arraySize; i++)
            {
                var platform = platforms.GetArrayElementAtIndex(i);
                if (platform.FindPropertyRelative("m_BuildTarget").stringValue != "iPhone") continue;
                var slots = platform.FindPropertyRelative("m_Icons");
                for (var j = 0; j < slots.arraySize; j++)
                {
                    var slot = slots.GetArrayElementAtIndex(j);
                    var size = slot.FindPropertyRelative("m_Width").intValue;
                    var textures = slot.FindPropertyRelative("m_Textures");
                    var loaded = Enumerable.Range(0, textures.arraySize)
                        .Select(k => textures.GetArrayElementAtIndex(k).objectReferenceValue as Texture2D).ToArray();
                    AddSlot(report, "iOS", slot.FindPropertyRelative("m_SubKind").stringValue + "/" +
                        slot.FindPropertyRelative("m_Kind").intValue, size, loaded, $"iOS/Icon_{size}.png");
                }
            }
            report.passed = true;
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            Debug.Log($"Game icons verified: {report.slots.Count} assigned slots, {report.pngFiles} PNG assets. Report: {ReportPath}");
        }

        private static void AddSlot(Report report, string platform, string kind, int size,
            Texture2D[] textures, params string[] expected)
        {
            if (textures.Length != expected.Length)
                throw new InvalidOperationException($"Unexpected layer count for {platform}/{kind}/{size}.");
            for (var i = 0; i < textures.Length; i++)
            {
                if (textures[i] == null || AssetDatabase.GetAssetPath(textures[i]) != IconRoot + "/" + expected[i]
                    || textures[i].width != size || textures[i].height != size)
                    throw new InvalidOperationException($"Incorrect icon assignment for {platform}/{kind}/{size}, layer {i}.");
            }
            report.slots.Add(new Slot { platform = platform, kind = kind, width = size, height = size,
                textures = textures.Select(AssetDatabase.GetAssetPath).ToArray() });
        }

        private static Texture2D Load(string relative)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(IconRoot + "/" + relative);
            if (texture == null) throw new FileNotFoundException("Missing generated icon: " + relative);
            return texture;
        }

        private static SerializedObject SerializedSettings() => new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
    }
}
