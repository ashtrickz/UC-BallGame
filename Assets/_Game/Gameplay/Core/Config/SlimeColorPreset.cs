using System;
using UnityEngine;

namespace Game.Gameplay.Config
{
    /// <summary>A two-colour jelly palette, embedded in the main game config.</summary>
    [Serializable]
    public struct SlimeColorPreset
    {
        [Tooltip("Stable save ID. Keep it unchanged when reordering presets.")]
        [SerializeField] private string id;
        [SerializeField] private Color baseColor;
        [SerializeField] private Color topColor;

        public string Id => id;
        public Color BaseColor => baseColor;
        public Color TopColor => topColor;

        public SlimeColorPreset(string id, Color baseColor, Color topColor)
        {
            this.id = id;
            this.baseColor = baseColor;
            this.topColor = topColor;
        }

        public static SlimeColorPreset[] Defaults() => new[]
        {
            new SlimeColorPreset("lime", Rgb(0x58F044), Rgb(0x17DFB0)),
            new SlimeColorPreset("cyan", Rgb(0x00CBD8), Rgb(0x24F1EF)),
            new SlimeColorPreset("coral", Rgb(0xFF706F), Rgb(0xFFA090)),
            new SlimeColorPreset("violet", Rgb(0xA15CF2), Rgb(0xD18AFF)),
            new SlimeColorPreset("gold", Rgb(0xFFD300), Rgb(0xFFF052)),
        };

        private static Color Rgb(int rgb) => new Color(((rgb >> 16) & 255) / 255f,
            ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1);
    }
}
