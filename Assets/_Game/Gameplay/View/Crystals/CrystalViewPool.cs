using System.Collections.Generic;
using Slime.Gameplay.Config;
using Slime.Gameplay.Core.Config;
using Slime.Gameplay.Core.Geometry;
using Slime.Gameplay.Core.Level;
using UnityEngine;

namespace Slime.Gameplay.View
{
    /// <summary>Hands out crystal views for placements and takes them back.</summary>
    public interface ICrystalViewFactory
    {
        int Created { get; }

        CrystalView Rent(CrystalPlacement placement, CrystalCatalog catalog);

        void Return(CrystalView view);
    }

    /// <summary>
    /// Keeps returned crystal views per catalog variant, so a restart reuses the shard objects instead of creating
    /// hundreds of new ones. A rented view is fully reset: transform, materials, tint and state.
    /// </summary>
    public sealed class CrystalViewPool : ICrystalViewFactory
    {
        private const string CrystalName = "Crystal";
        private const string ShardName = "Shard";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");

        private readonly Transform _parent;
        private readonly Dictionary<CrystalCatalog.Variant, Stack<CrystalView>> _free =
            new Dictionary<CrystalCatalog.Variant, Stack<CrystalView>>();

        public CrystalViewPool(Transform parent)
        {
            _parent = parent;
        }

        public int Created { get; private set; }

        public CrystalView Rent(CrystalPlacement placement, CrystalCatalog catalog)
        {
            var variant = catalog.variants[placement.VariantId];
            var view = Take(variant);
            var root = view.Root;
            var scale = Vector3.one * (float)placement.Scale;
            root.localPosition = new Vector3((float)placement.Position.X, 0, (float)placement.Position.Z);
            root.localRotation = Quaternion.Euler(0, ConvexPolygon.YawDegrees(placement.RotationStep), 0);
            root.localScale = scale;
#if UNITY_EDITOR
            root.name = $"{CrystalName} {placement.Id} ({variant.name})";
#endif
            view.Scale = scale;
            view.Highlighted = false;
            view.Destroyed = false;

            for (var s = 0; s < variant.shards.Length; s++)
            {
                // Alternate the two candy materials so neighbours read as separate crystals.
                var materialIndex = (variant.shards[s].materialIndex + placement.Id) % catalog.materials.Length;
                var material = catalog.materials[materialIndex];
                var renderer = view.Renderers[s];
                renderer.sharedMaterial = material;
                renderer.SetPropertyBlock(null);
                view.Materials[s] = material;
                view.BaseColors[s] = material.HasProperty(BaseColorId)
                    ? material.GetColor(BaseColorId)
                    : Color.white;
                view.BaseBrightness[s] = material.HasProperty(BrightnessId)
                    ? material.GetFloat(BrightnessId)
                    : 1f;
            }

            root.gameObject.SetActive(true);

            return view;
        }

        public void Return(CrystalView view)
        {
            if (view == null || view.Root == null)
                return;

            view.Root.gameObject.SetActive(false);
#if UNITY_EDITOR
            view.Root.name = CrystalName;
#endif
            Free(view.Variant).Push(view);
        }

        private CrystalView Take(CrystalCatalog.Variant variant)
        {
            var free = Free(variant);
            if (free.Count > 0)
                return free.Pop();

            return Create(variant);
        }

        private Stack<CrystalView> Free(CrystalCatalog.Variant variant)
        {
            if (!_free.TryGetValue(variant, out var free))
            {
                free = new Stack<CrystalView>();
                _free.Add(variant, free);
            }

            return free;
        }

        private CrystalView Create(CrystalCatalog.Variant variant)
        {
            var root = new GameObject(CrystalName).transform;
            root.SetParent(_parent, false);
            var renderers = new MeshRenderer[variant.shards.Length];
            for (var s = 0; s < variant.shards.Length; s++)
            {
                var shard = variant.shards[s];
                var shardObject = new GameObject(ShardName, typeof(MeshFilter), typeof(MeshRenderer));
                var shardTransform = shardObject.transform;
                shardTransform.SetParent(root, false);
                shardTransform.localPosition = shard.localPosition;
                shardTransform.localRotation = Quaternion.Euler(shard.localEuler);
                shardTransform.localScale = shard.localScale;
                shardObject.GetComponent<MeshFilter>().sharedMesh = shard.mesh;
                renderers[s] = shardObject.GetComponent<MeshRenderer>();
            }

            Created++;

            return new CrystalView(variant, root, renderers);
        }
    }
}
