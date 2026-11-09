using Slime.Gameplay.Config;
using Slime.Gameplay.Core.Config;
using UnityEngine;

namespace Slime.Gameplay.View
{
    /// <summary>Scene object of one placed crystal: its shard renderers, their base look and the per-attempt state.</summary>
    public sealed class CrystalView
    {
        public CrystalView(CrystalCatalog.Variant variant, Transform root, MeshRenderer[] renderers)
        {
            Variant = variant;
            Root = root;
            Renderers = renderers;
            BaseColors = new Color[renderers.Length];
            BaseBrightness = new float[renderers.Length];
            Materials = new Material[renderers.Length];
        }

        public CrystalCatalog.Variant Variant { get; }

        public Transform Root { get; }

        public MeshRenderer[] Renderers { get; }

        public Color[] BaseColors { get; }

        public float[] BaseBrightness { get; }

        public Material[] Materials { get; }

        public Vector3 Scale { get; set; }

        public bool Highlighted { get; set; }

        public bool Destroyed { get; set; }
    }
}
