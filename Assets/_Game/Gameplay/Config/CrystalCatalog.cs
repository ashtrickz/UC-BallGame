using System;
using System.Collections.Generic;
using Slime.Gameplay.Core.Geometry;
using UnityEngine;
using Slime.Gameplay.Core.Config;

namespace Slime.Gameplay.Config
{
    /// <summary>
    /// Playable crystal variants: the visible shards of each variant and the convex XZ projection baked from those
    /// meshes in the variant's anchor space. The view builds shards from the same data, so contour and visuals share
    /// one coordinate system. Re-bake (Tools/Ball Game/Gameplay/Bake Crystal Contours) after changing shards.
    /// </summary>
    [CreateAssetMenu(menuName = "Ball Game/Crystal Catalog", fileName = "CrystalCatalog")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Slime.Gameplay.Config", sourceAssembly: "Game.Gameplay.Config", sourceClassName: "CrystalCatalog")]
    public sealed class CrystalCatalog : ScriptableObject
    {
        private const int MinContourVertices = 3;

        [Serializable]
        public sealed class Shard
        {
            public Mesh mesh;
            public int materialIndex;
            public Vector3 localPosition;
            public Vector3 localEuler;
            public Vector3 localScale = Vector3.one;
        }

        [Serializable]
        public sealed class Variant
        {
            public string name;
            public Shard[] shards = Array.Empty<Shard>();
            [Tooltip("Baked convex XZ hull in anchor space, counter-clockwise. Generated, do not edit.")]
            public Vector2[] contour = Array.Empty<Vector2>();
        }

        public Material[] materials = Array.Empty<Material>();
        public Variant[] variants = Array.Empty<Variant>();

        public IReadOnlyList<CrystalVariantData> ToVariantData()
        {
            var list = new List<CrystalVariantData>(variants.Length);
            for (var i = 0; i < variants.Length; i++)
            {
                var variant = variants[i];
                if (variant.contour == null || variant.contour.Length < MinContourVertices)
                    throw new InvalidOperationException($"Crystal variant {i} ({variant.name}) has no baked contour.");

                var points = new Vec2[variant.contour.Length];
                for (var p = 0; p < points.Length; p++)
                    points[p] = new Vec2(variant.contour[p].x, variant.contour[p].y);
                list.Add(new CrystalVariantData(i, variant.name, new ConvexPolygon(points)));
            }
            return list;
        }

        public static ConvexPolygon ComputeContour(Variant variant)
        {
            var points = new List<Vec2>();
            foreach (var shard in variant.shards)
            {
                if (shard?.mesh == null)
                    continue;

                var matrix = Matrix4x4.TRS(shard.localPosition, Quaternion.Euler(shard.localEuler), shard.localScale);
                foreach (var vertex in shard.mesh.vertices)
                {
                    var world = matrix.MultiplyPoint3x4(vertex);
                    points.Add(new Vec2(world.x, world.z));
                }
            }
            return ConvexPolygon.Hull(points);
        }
    }
}
