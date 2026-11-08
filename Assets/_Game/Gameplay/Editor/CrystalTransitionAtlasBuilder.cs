using System;
using System.IO;
using Game.Gameplay.Config;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Gameplay.Editor
{
    /// <summary>Bakes alpha silhouettes of the actual gameplay shard meshes for the UI transition.</summary>
    public static class CrystalTransitionAtlasBuilder
    {
        public const string AtlasPath = "Assets/_Game/Architecture/Application/Loading/CrystalTransitionAtlas.png";
        private const int TileSize = 256;
        private const int Columns = 4;
        private const int Rows = 2;

        [MenuItem("Tools/Ball Game/Rebuild Crystal Transition Atlas")]
        public static void Rebuild()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CrystalCatalog>(GameplayAssetsBuilder.CatalogPath);
            if (!catalog || catalog.variants.Length != Columns || catalog.materials.Length < 2)
                throw new InvalidOperationException("The transition needs the four gameplay crystal variants and their two materials.");

            var atlas = new Texture2D(TileSize * Columns, TileSize * Rows, TextureFormat.RGBA32, false);
            try
            {
                for (var row = 0; row < Rows; row++)
                for (var column = 0; column < Columns; column++)
                {
                    var pixels = Render(catalog, catalog.variants[column], row);
                    atlas.SetPixels(column * TileSize, row * TileSize, TileSize, TileSize, pixels);
                }
                atlas.Apply();
                File.WriteAllBytes(AtlasPath, atlas.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(atlas); }

            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();

            var material = AssetDatabase.LoadAssetAtPath<Material>(SceneFlowInstaller.TransitionMaterialPath);
            if (material)
            {
                material.SetTexture("_CrystalAtlas", AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath));
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();
            }
            Debug.Log("[SceneFlow] Baked eight crystal silhouettes from the gameplay meshes.");
        }

        private static Color[] Render(CrystalCatalog catalog, CrystalCatalog.Variant variant, int palette)
        {
            var preview = new PreviewRenderUtility();
            Texture2D pixels = null;
            var previousTarget = RenderTexture.active;
            var previousSrgbWrite = GL.sRGBWrite;
            try
            {
                var camera = preview.camera;
                camera.orthographic = true;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 10f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.allowHDR = false;
                camera.allowMSAA = true;
                camera.transform.rotation = Quaternion.Euler(22f, palette == 0 ? -25f : 20f, 0f);
                preview.ambientColor = new Color(0.35f, 0.42f, 0.45f);
                preview.lights[0].intensity = 1.25f;
                preview.lights[0].transform.rotation = Quaternion.Euler(48f, -60f, 0f);
                preview.lights[1].intensity = 0.25f;
                preview.lights[1].transform.rotation = Quaternion.Euler(320f, 120f, 0f);

                var root = new GameObject("Crystal Transition Preview");
                preview.AddSingleGO(root);
                var inverseView = Quaternion.Inverse(camera.transform.rotation);
                var bounds = new Bounds();
                var first = true;
                foreach (var shard in variant.shards)
                {
                    var go = new GameObject("Shard", typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(root.transform, false);
                    go.transform.localPosition = shard.localPosition;
                    go.transform.localRotation = Quaternion.Euler(shard.localEuler);
                    go.transform.localScale = shard.localScale;
                    go.GetComponent<MeshFilter>().sharedMesh = shard.mesh;
                    go.GetComponent<MeshRenderer>().sharedMaterial = catalog.materials[(shard.materialIndex + palette) % catalog.materials.Length];
                    foreach (var vertex in shard.mesh.vertices)
                    {
                        var point = inverseView * go.transform.TransformPoint(vertex);
                        if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                        else bounds.Encapsulate(point);
                    }
                }
                camera.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.y) * 0.61f;
                camera.transform.position = camera.transform.rotation * bounds.center - camera.transform.forward * 4f;
                preview.BeginPreview(new Rect(0, 0, TileSize, TileSize), GUIStyle.none);
                preview.Render(true);
                // The first URP preview can initialise its renderer without drawing the mesh yet.
                preview.Render(true);
                var rendered = (RenderTexture)preview.EndPreview();
                RenderTexture.active = rendered;
                pixels = new Texture2D(TileSize, TileSize, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, TileSize, TileSize), 0, 0);
                pixels.Apply();
                var colors = pixels.GetPixels();
                var hasCrystal = false;
                for (var i = 0; i < colors.Length; i++)
                {
                    hasCrystal |= colors[i].a > 0.1f;
                    // Only the real mesh outline is used by the black transition shader.
                    colors[i] = new Color(1f, 1f, 1f, colors[i].a);
                }
                if (!hasCrystal) throw new InvalidOperationException("Empty crystal preview: " + variant.name);
                return colors;
            }
            finally
            {
                RenderTexture.active = previousTarget;
                GL.sRGBWrite = previousSrgbWrite;
                if (pixels) Object.DestroyImmediate(pixels);
                preview.Cleanup();
            }
        }
    }
}
