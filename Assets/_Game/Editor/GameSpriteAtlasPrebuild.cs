// [UNITY-SKILL:SPRITEATLAS]
// [TYPE:PREBUILD]
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace Slime.Editor
{
    /// <summary>Builds the local UI atlases from actual scene and Addressables dependencies.</summary>
    public sealed class GameSpriteAtlasPrebuild : IPreprocessBuildWithReport
    {
        public const string CommonAtlas = "Assets/_Game/Art/UI/CartoonContour/CartoonContour.spriteatlasv2";
        public const string LoadingAtlas = "Assets/_Game/Art/UI/Loading.spriteatlasv2";
        private const string UiFolder = "Assets/_Game/Art/UI/";
        private const string ContourFolder = UiFolder + "CartoonContour/Sprites/";

        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report) => Prepare();

        // Also called before Addressables so its dependency calculation sees the final atlases.
        public static void Prepare()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Prepare sprite atlases outside Play Mode.");

            EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
            if (EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2)
                throw new BuildFailedException("Sprite Atlas V2 packing must be enabled.");

            var roots = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToList();
            var addressables = AddressableAssetSettingsDefaultObject.Settings;
            if (addressables != null)
                roots.AddRange(addressables.groups.Where(g => g != null).SelectMany(g => g.entries)
                    .Select(e => e.AssetPath).Where(p => p.StartsWith("Assets/", StringComparison.Ordinal)
                        && !p.EndsWith(".spriteatlasv2", StringComparison.Ordinal)
                        && !p.EndsWith(".spriteatlas", StringComparison.Ordinal)));

            var dependencies = AssetDatabase.GetDependencies(roots.Distinct().ToArray(), true);
            var common = dependencies.Where(p => p.StartsWith(ContourFolder, StringComparison.Ordinal)
                || p == UiFolder + "popup_panel.png").OrderBy(p => p, StringComparer.Ordinal).ToArray();
            var loading = dependencies.Where(p => p == UiFolder + "bar_empty.png"
                || p == UiFolder + "bar_fill.png" || p == UiFolder + "bar_filled.png")
                .OrderBy(p => p, StringComparer.Ordinal).ToArray();

            PrepareAtlas(CommonAtlas, common);
            PrepareAtlas(LoadingAtlas, loading);
        }

        private static void PrepareAtlas(string path, IReadOnlyList<string> sources)
        {
            if (sources.Count == 0)
                throw new BuildFailedException("No referenced sprites found for " + path);

            var packables = new List<Object>();
            foreach (var source in sources)
            {
                if (!source.StartsWith("Assets/", StringComparison.Ordinal)
                    || !(AssetImporter.GetAtPath(source) is TextureImporter texture)
                    || texture.textureType != TextureImporterType.Sprite)
                    throw new BuildFailedException("Expected a project sprite: " + source);

                // Configure sources before the atlas importer reads them. Preserve GUIDs, borders and pivots.
                var before = EditorJsonUtility.ToJson(texture);
                GameAssetImportPolicy.ConfigureTexture(texture);
                if (before != EditorJsonUtility.ToJson(texture))
                    texture.SaveAndReimport();
                packables.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(source));
            }

            var exists = File.Exists(path);
            // GetPackables is the editor read API for the imported runtime representation.
            var current = exists ? AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path).GetPackables() : Array.Empty<Object>();
            if (!current.Select(AssetDatabase.GetAssetPath).OrderBy(p => p, StringComparer.Ordinal)
                .SequenceEqual(sources.OrderBy(p => p, StringComparer.Ordinal)))
            {
                var asset = exists ? SpriteAtlasAsset.Load(path) : new SpriteAtlasAsset();
                if (current.Length > 0) asset.Remove(current);
                asset.Add(packables.ToArray());
                SpriteAtlasAsset.Save(asset, path);
                AssetDatabase.ImportAsset(path);
            }

            var importer = AssetImporter.GetAtPath(path) as SpriteAtlasImporter;
            if (importer == null)
                throw new BuildFailedException("Could not import sprite atlas: " + path);
            var original = EditorJsonUtility.ToJson(importer);
            importer.includeInBuild = true;
            var packing = importer.packingSettings;
            packing.padding = 4;
            packing.enableRotation = false;
            packing.enableTightPacking = false;
            packing.enableAlphaDilation = true;
            importer.packingSettings = packing;
            var settings = importer.textureSettings;
            settings.readable = false;
            settings.generateMipMaps = false;
            settings.sRGB = true;
            settings.filterMode = FilterMode.Bilinear;
            settings.anisoLevel = 0;
            importer.textureSettings = settings;
            SetPlatform(importer, "DefaultTexturePlatform", TextureImporterFormat.Automatic, false);
            SetPlatform(importer, "Android", TextureImporterFormat.ASTC_4x4, true);
            SetPlatform(importer, "Standalone", TextureImporterFormat.BC7, true);
            if (original != EditorJsonUtility.ToJson(importer)) importer.SaveAndReimport();
        }

        private static void SetPlatform(SpriteAtlasImporter importer, string name, TextureImporterFormat format, bool overridden)
        {
            var settings = importer.GetPlatformSettings(name);
            settings.name = name;
            settings.overridden = overridden;
            settings.maxTextureSize = 1024;
            settings.format = format;
            settings.textureCompression = TextureImporterCompression.CompressedHQ;
            settings.compressionQuality = 80;
            settings.crunchedCompression = false;
            settings.allowsAlphaSplitting = false;
            importer.SetPlatformSettings(settings);
        }
    }
}
