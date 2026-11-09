using System;
using System.Collections.Generic;
using System.IO;
using Slime.Gameplay.Config;
using Slime.Gameplay.Core.Config;
using Slime.Gameplay.Core.Geometry;
using Slime.Gameplay.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Slime.Gameplay.Editor
{
    /// <summary>
    /// Creates the gameplay assets: game copies of the SlimeLab materials (the lab stays untouched), the crystal
    /// catalog with baked XZ contours, and the rules / view configs. Safe to run repeatedly.
    /// </summary>
    public static class GameplayAssetsBuilder
    {
        public const string Root = "Assets/_Game/Gameplay";
        public const string Art = Root + "/Art";
        public const string ConfigFolder = Root + "/Config";
        public const string LabMaterials = "Assets/_Game/Prototypes/SlimeLab/Materials";
        public const string CatalogPath = ConfigFolder + "/CrystalCatalog.asset";
        public const string GameConfigPath = ConfigFolder + "/SlimeGameConfig.asset";
        public const string ViewConfigPath = ConfigFolder + "/SlimeViewConfig.asset";

        public sealed class Set
        {
            public Material Jelly, Neck, Ink, Catchlight, CrystalCoral, CrystalAmethyst, Backdrop, Plinth, Rim, Glow;
            public Material Lane, LaneEdge, BlastRing, DoorLeaf, BlobShadow, Spotlight, SplashDrop, SplashRipple;
            public Mesh CrystalMesh, Sphere, Cube, Cylinder, Quad;
            public VolumeProfile Volume;
            public CrystalCatalog Catalog;
            public SlimeGameConfig GameConfig;
            public SlimeViewConfig ViewConfig;
        }

        [MenuItem("Tools/Ball Game/Gameplay/Create Or Update Assets")]
        public static void CreateFromMenu() => Ensure();

        public static Set Ensure()
        {
            Directory.CreateDirectory(Art);
            Directory.CreateDirectory(ConfigFolder);
            AssetDatabase.Refresh();
            var set = new Set
            {
                Jelly = CopyLab<Material>("Jelly Mint.mat", "Slime Jelly.mat"),
                Neck = CopyLab<Material>("Jelly Neck.mat", "Slime Neck.mat"),
                Ink = CopyLab<Material>("Face Ink.mat", "Face Ink.mat"),
                Catchlight = CopyLab<Material>("Eye Catchlight.mat", "Eye Catchlight.mat"),
                CrystalCoral = CopyLab<Material>("Crystal Coral.mat", "Crystal Coral.mat"),
                CrystalAmethyst = CopyLab<Material>("Crystal Amethyst.mat", "Crystal Amethyst.mat"),
                Backdrop = CopyLab<Material>("Backdrop.mat", "Backdrop.mat"),
                Plinth = CopyLab<Material>("Ceramic Plinth.mat", "Ceramic Course.mat"),
                Rim = CopyLab<Material>("Rim.mat", "Rim.mat"),
                Glow = CopyLab<Material>("Colony Glow.mat", "Colony Glow.mat"),
                CrystalMesh = CopyLab<Mesh>("Faceted Crystal.asset", "Faceted Crystal.asset"),
                Volume = CopyLab<VolumeProfile>("Slime Lab Volume.asset", "Gameplay Volume.asset"),
            };
            set.Lane = Lit("Lane.mat", Hex("3E6A70"), 0.3f);
            set.LaneEdge = Lit("Lane Edge.mat", Hex("E1FF86"), 0.5f, Hex("E1FF86") * 0.9f);
            set.DoorLeaf = Lit("Door Leaf.mat", Hex("5A8A8C"), 0.45f);
            set.BlastRing = TransparentUnlit("Blast Ring.mat", new Color(0.88f, 1f, 0.53f, 0.6f));
            set.BlobShadow = TransparentUnlit("Blob Shadow.mat", new Color(0.02f, 0.06f, 0.08f, 0.55f));
            set.BlobShadow.SetTexture("_BaseMap", SoftDisc());
            set.Quad = Builtin(PrimitiveType.Quad);
            set.Spotlight = ShaderMaterial("Tutorial Spotlight.mat", "BallGame/UI/Tutorial Spotlight");
            set.SplashDrop = ParticleMaterial("Splash Drop.mat", Color.white, false, null);
            set.SplashRipple = ParticleMaterial("Splash Ripple.mat", new Color(0.88f, 1f, 0.53f, 1f), true, RingTexture());
            set.Sphere = Builtin(PrimitiveType.Sphere);
            set.Cube = Builtin(PrimitiveType.Cube);
            set.Cylinder = Builtin(PrimitiveType.Cylinder);
            set.Catalog = EnsureCatalog(set);
            set.GameConfig = EnsureAsset<SlimeGameConfig>(GameConfigPath);
            var so = new SerializedObject(set.GameConfig);
            so.FindProperty("crystals").objectReferenceValue = set.Catalog;
            var campaign = AssetDatabase.LoadAssetAtPath<CampaignAsset>(ConfigFolder + "/Campaign.asset");
            if (campaign != null) so.FindProperty("campaign").objectReferenceValue = campaign;
            if (so.FindProperty("wall.RouteLength").doubleValue <= 0) SetWallDefaults(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            set.ViewConfig = EnsureAsset<SlimeViewConfig>(ViewConfigPath);
            AssetDatabase.SaveAssets();
            return set;
        }

        public static void SetWallDefaults(SerializedObject config)
        {
            var d = WallValues.Defaults;
            void D(string name, double value) => config.FindProperty("wall." + name).doubleValue = value;
            D("RouteLength", d.RouteLength); D("WallStartZ", d.WallStartZ); D("WallEndBeforeDoor", d.WallEndBeforeDoor);
            D("FieldHalfWidth", d.FieldHalfWidth); D("Spacing", d.Spacing); D("Jitter", d.Jitter); D("CoreHalfWidth", d.CoreHalfWidth);
            D("CoreFillMin", d.CoreFillMin); D("CoreFillMax", d.CoreFillMax); D("SideFill", d.SideFill);
            D("ScaleMin", d.ScaleMin); D("ScaleMax", d.ScaleMax);
            config.FindProperty("wall.MaxCandidates").intValue = d.MaxCandidates;
        }

        // ------------------------------------------------------------------ crystal catalog

        private static CrystalCatalog EnsureCatalog(Set set)
        {
            var catalog = EnsureAsset<CrystalCatalog>(CatalogPath);
            catalog.materials = new[] { set.CrystalCoral, set.CrystalAmethyst };
            CrystalCatalog.Shard S(float sx, float sy, Vector3 position, Vector3 euler, int material) => new CrystalCatalog.Shard
            {
                mesh = set.CrystalMesh, materialIndex = material, localPosition = position, localEuler = euler,
                localScale = new Vector3(sx, sy, sx),
            };
            // Shards follow the SlimeLab crystal: hexagonal base, taper and an offset tip, with tilted instances.
            catalog.variants = new[]
            {
                new CrystalCatalog.Variant { name = "Spire", shards = new[] { S(0.64f, 0.95f, Vector3.zero, new Vector3(8, 30, 6), 0) } },
                new CrystalCatalog.Variant { name = "Stout", shards = new[] { S(0.8f, 0.7f, Vector3.zero, new Vector3(-9, 58, -3), 1) } },
                new CrystalCatalog.Variant
                {
                    name = "Twin", shards = new[]
                    {
                        S(0.56f, 0.92f, new Vector3(-0.12f, 0, 0.05f), new Vector3(10, 10, -8), 0),
                        S(0.42f, 0.62f, new Vector3(0.2f, 0, -0.08f), new Vector3(-12, 70, 14), 1),
                    }
                },
                new CrystalCatalog.Variant
                {
                    name = "Cluster", shards = new[]
                    {
                        S(0.5f, 1.02f, Vector3.zero, new Vector3(5, 0, 4), 1),
                        S(0.36f, 0.6f, new Vector3(0.22f, 0, 0.12f), new Vector3(-14, 40, 18), 0),
                        S(0.34f, 0.55f, new Vector3(-0.2f, 0, 0.14f), new Vector3(12, 100, -16), 0),
                    }
                },
            };
            Bake(catalog);
            return catalog;
        }

        [MenuItem("Tools/Ball Game/Gameplay/Bake Crystal Contours")]
        public static void BakeFromMenu()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CrystalCatalog>(CatalogPath);
            if (catalog == null) throw new InvalidOperationException("Create the gameplay assets first.");
            Bake(catalog);
            AssetDatabase.SaveAssets();
        }

        public static void Bake(CrystalCatalog catalog)
        {
            foreach (var variant in catalog.variants)
            {
                var hull = CrystalCatalog.ComputeContour(variant);
                var points = new Vector2[hull.Count];
                for (var i = 0; i < points.Length; i++) points[i] = new Vector2((float)hull.Vertices[i].X, (float)hull.Vertices[i].Z);
                // Float storage must stay counter-clockwise and convex; the hull of the rounded points guarantees it.
                var rounded = new List<Vec2>();
                foreach (var p in points) rounded.Add(new Vec2(p.x, p.y));
                var check = ConvexPolygon.Hull(rounded);
                variant.contour = new Vector2[check.Count];
                for (var i = 0; i < check.Count; i++) variant.contour[i] = new Vector2((float)check.Vertices[i].X, (float)check.Vertices[i].Z);
                Debug.Log($"[Gameplay] baked '{variant.name}': {variant.contour.Length} contour vertices, radius {check.BoundingRadius:0.###}");
            }
            EditorUtility.SetDirty(catalog);
        }

        // ------------------------------------------------------------------ helpers

        private static T CopyLab<T>(string labName, string gameName) where T : UnityEngine.Object
        {
            var target = Art + "/" + gameName;
            var existing = AssetDatabase.LoadAssetAtPath<T>(target);
            if (existing != null) return existing;
            var source = LabMaterials + "/" + labName;
            if (!AssetDatabase.CopyAsset(source, target)) throw new InvalidOperationException("Could not copy " + source);
            return AssetDatabase.LoadAssetAtPath<T>(target);
        }

        private static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static Material Lit(string name, Color color, float smoothness, Color? emission = null)
        {
            var path = Art + "/" + name;
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = Path.GetFileNameWithoutExtension(name) };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                material.SetColor("_EmissionColor", emission.Value);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material TransparentUnlit(string name, Color color)
        {
            var path = Art + "/" + name;
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = Path.GetFileNameWithoutExtension(name) };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material ParticleMaterial(string name, Color color, bool transparent, Texture2D texture)
        {
            var path = Art + "/" + name;
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = Path.GetFileNameWithoutExtension(name) };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            if (texture != null) material.SetTexture("_BaseMap", texture);
            if (transparent)
            {
                material.SetFloat("_Surface", 1);
                material.SetFloat("_Blend", 0);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0);
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D RingTexture()
        {
            var path = Art + "/Ripple Ring.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var d = new Vector2(x - size / 2f + 0.5f, y - size / 2f + 0.5f).magnitude / (size / 2f);
                    var a = Mathf.Clamp01(1 - Mathf.Abs(d - 0.82f) / 0.12f);
                    texture.SetPixel(x, y, new Color(1, 1, 1, a * a * (3 - 2 * a)));
                }
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Texture2D SoftDisc()
        {
            var path = Art + "/Soft Disc.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var d = new Vector2(x - size / 2f + 0.5f, y - size / 2f + 0.5f).magnitude / (size / 2f);
                    var a = Mathf.Clamp01(1 - d);
                    texture.SetPixel(x, y, new Color(1, 1, 1, a * a * (3 - 2 * a)));
                }
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material ShaderMaterial(string name, string shaderName)
        {
            var path = Art + "/" + name;
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find(shaderName) ?? throw new InvalidOperationException("Shader not imported: " + shaderName);
            material = new Material(shader) { name = Path.GetFileNameWithoutExtension(name) };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Mesh Builtin(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(go);
            return mesh;
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }
    }
}
