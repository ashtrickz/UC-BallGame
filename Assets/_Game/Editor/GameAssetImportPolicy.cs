using System;
using UnityEditor;
using UnityEngine;

namespace Slime.Editor
{
    /// <summary>Import rules for the game's authored textures and selected audio, including regenerated assets.</summary>
    public sealed class GameAssetImportPolicy : AssetPostprocessor
    {
        private const string UiFolder = "Assets/_Game/Art/UI/";
        private const string ContourFolder = UiFolder + "CartoonContour/Sprites/";
        private const string AudioFolder = "Assets/_Game/Audio/Selected/";
        private const string TransitionAtlas = "Assets/_Game/Architecture/Application/Loading/CrystalTransitionAtlas.png";

        public override uint GetVersion() => 1;

        private void OnPreprocessTexture() => ConfigureTexture((TextureImporter)assetImporter);
        private void OnPreprocessAudio() => ConfigureAudio((AudioImporter)assetImporter);

        public static bool ConfigureTexture(TextureImporter importer)
        {
            var path = importer.assetPath;
            var ui = path.StartsWith(UiFolder, StringComparison.Ordinal);
            var transition = path == TransitionAtlas;
            var effect = path == "Assets/_Game/Gameplay/Art/Soft Disc.png"
                         || path == "Assets/_Game/Gameplay/Art/Ripple Ring.png";
            if (!ui && !transition && !effect) return false;

            var maxSize = TextureLimit(path);
            var androidFormat = path == UiFolder + "MenuBackground.png"
                ? TextureImporterFormat.ASTC_6x6
                : TextureImporterFormat.ASTC_4x4;

            importer.isReadable = false;
            importer.mipmapEnabled = transition;
            importer.streamingMipmaps = false;
            importer.anisoLevel = 0;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.crunchedCompression = false;
            importer.compressionQuality = 80;
            importer.filterMode = transition ? FilterMode.Trilinear : FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteGenerateFallbackPhysicsShape = false;
            if (ui && importer.textureType == TextureImporterType.Sprite)
                settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);

            SetTexturePlatform(importer, "Android", maxSize, androidFormat);
            SetTexturePlatform(importer, "Standalone", maxSize, TextureImporterFormat.BC7);
            return true;
        }

        private static int TextureLimit(string path)
        {
            if (path == UiFolder + "MenuBackground.png") return 2048;
            if (path == UiFolder + "SlimeLogo.png" || path == TransitionAtlas) return 1024;
            if (path.StartsWith(ContourFolder, StringComparison.Ordinal))
            {
                var name = System.IO.Path.GetFileName(path);
                return name.StartsWith("icon_", StringComparison.Ordinal)
                       || name.StartsWith("progress_milestone_", StringComparison.Ordinal)
                       || name == "accent_rays.png" ? 256 : 512;
            }
            if (path == "Assets/_Game/Gameplay/Art/Soft Disc.png") return 64;
            if (path == "Assets/_Game/Gameplay/Art/Ripple Ring.png") return 128;
            return path == UiFolder + "popup_panel.png" ? 512 : 1024;
        }

        private static void SetTexturePlatform(TextureImporter importer, string platformName,
            int maxSize, TextureImporterFormat format)
        {
            var platform = importer.GetPlatformTextureSettings(platformName);
            platform.name = platformName;
            platform.overridden = true;
            platform.maxTextureSize = maxSize;
            platform.format = format;
            platform.textureCompression = TextureImporterCompression.CompressedHQ;
            platform.compressionQuality = 80;
            platform.crunchedCompression = false;
            platform.allowsAlphaSplitting = false;
            importer.SetPlatformTextureSettings(platform);
        }

        public static bool ConfigureAudio(AudioImporter importer)
        {
            var path = importer.assetPath;
            if (!path.StartsWith(AudioFolder, StringComparison.Ordinal)) return false;
            var music = path.StartsWith(AudioFolder + "Music/", StringComparison.Ordinal);
            var noisyEffect = !path.Contains("_loop_")
                              && (path.StartsWith(AudioFolder + "Crystal/", StringComparison.Ordinal)
                              || path.StartsWith(AudioFolder + "Slime/", StringComparison.Ordinal)
                              || path.StartsWith(AudioFolder + "Shot/", StringComparison.Ordinal)
                              || path.StartsWith(AudioFolder + "Finish/", StringComparison.Ordinal));

            var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = music ? AudioCompressionFormat.Vorbis
                : noisyEffect ? AudioCompressionFormat.ADPCM : AudioCompressionFormat.PCM;
            settings.quality = music ? 0.7f : 1f;
            settings.sampleRateSetting = music ? AudioSampleRateSetting.PreserveSampleRate
                : AudioSampleRateSetting.OverrideSampleRate;
            if (!music) settings.sampleRateOverride = 24000;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = music;
            // The SFX masters are already mono; retain authored channels and normalization.
            importer.ClearSampleSettingOverride("Android");
            importer.ClearSampleSettingOverride("Standalone");
            return true;
        }

        public static int ApplyToExisting()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Apply import rules while the Editor is idle and outside Play Mode.");
            var changed = 0;
            foreach (var path in AssetDatabase.GetAllAssetPaths())
            {
                if (!path.StartsWith("Assets/_Game/", StringComparison.Ordinal)) continue;
                var importer = AssetImporter.GetAtPath(path);
                var applies = importer is TextureImporter texture ? ConfigureTexture(texture)
                    : importer is AudioImporter audio && ConfigureAudio(audio);
                if (!applies) continue;
                importer.SaveAndReimport();
                changed++;
            }
            return changed;
        }
    }
}
