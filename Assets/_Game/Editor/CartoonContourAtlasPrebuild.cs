// [UNITY-SKILL:SPRITEATLAS]
// [TYPE:PREBUILD]
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.U2D;
using UnityEngine;

namespace Game.Editor
{
    public sealed class CartoonContourAtlasPrebuild : IPreprocessBuildWithReport
    {
        public const string SpriteFolder = "Assets/_Game/Art/UI/CartoonContour/Sprites";
        public const string AtlasPath = "Assets/_Game/Art/UI/CartoonContour/CartoonContour.spriteatlasv2";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => EnsureAtlas();

        public static void EnsureAtlas()
        {
            var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(SpriteFolder);
            if (folder == null || !AssetDatabase.IsValidFolder(SpriteFolder))
                throw new InvalidOperationException("Cartoon Contour UI sprite folder is missing: " + SpriteFolder);

            EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
            if (EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2)
                throw new InvalidOperationException("SpriteAtlas V2 packing could not be enabled.");

            // A folder packable includes newly imported UI sprites on subsequent builds.
            // Saving the owned atlas data preserves its existing .meta GUID.
            Directory.CreateDirectory(Path.GetDirectoryName(AtlasPath));
            var asset = new SpriteAtlasAsset();
            asset.Add(new UnityEngine.Object[] { folder });
            SpriteAtlasAsset.Save(asset, AtlasPath);
            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);

            var importer = AssetImporter.GetAtPath(AtlasPath) as SpriteAtlasImporter;
            if (importer == null)
                throw new InvalidOperationException("SpriteAtlas importer was not created: " + AtlasPath);

            importer.includeInBuild = true;
            var texture = importer.textureSettings;
            texture.filterMode = FilterMode.Bilinear;
            texture.generateMipMaps = false;
            texture.readable = false;
            texture.sRGB = true;
            importer.textureSettings = texture;

            var packing = importer.packingSettings;
            packing.padding = 4;
            packing.enableRotation = false;
            packing.enableTightPacking = false;
            packing.enableAlphaDilation = true;
            importer.packingSettings = packing;

            var platform = importer.GetPlatformSettings("DefaultTexturePlatform");
            platform.maxTextureSize = 1024;
            platform.format = TextureImporterFormat.RGBA32;
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            platform.crunchedCompression = false;
            importer.SetPlatformSettings(platform);
            importer.SaveAndReimport();
        }
    }
}
