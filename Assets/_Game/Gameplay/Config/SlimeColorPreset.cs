using System;
using UnityEngine;

namespace Game.Gameplay.Config
{
    /// <summary>A two-colour jelly palette, embedded in the main game config.</summary>
    [Serializable]
    public struct SlimeColorPreset
    {
        private const string LimeId = "lime";
        private const string CyanId = "cyan";
        private const string CoralId = "coral";
        private const string VioletId = "violet";
        private const string GoldId = "gold";
        private const int LimeBase = 0x58F044;
        private const int LimeTop = 0x17DFB0;
        private const int CyanBase = 0x00CBD8;
        private const int CyanTop = 0x24F1EF;
        private const int CoralBase = 0xFF706F;
        private const int CoralTop = 0xFFA090;
        private const int VioletBase = 0xA15CF2;
        private const int VioletTop = 0xD18AFF;
        private const int GoldBase = 0xFFD300;
        private const int GoldTop = 0xFFF052;
        private const int RedShift = 16;
        private const int GreenShift = 8;
        private const int ChannelMask = 255;
        private const float ChannelMax = 255f;

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
            new SlimeColorPreset(LimeId, Rgb(LimeBase), Rgb(LimeTop)),
            new SlimeColorPreset(CyanId, Rgb(CyanBase), Rgb(CyanTop)),
            new SlimeColorPreset(CoralId, Rgb(CoralBase), Rgb(CoralTop)),
            new SlimeColorPreset(VioletId, Rgb(VioletBase), Rgb(VioletTop)),
            new SlimeColorPreset(GoldId, Rgb(GoldBase), Rgb(GoldTop)),
        };

        private static Color Rgb(int rgb)
        {
            var red = ((rgb >> RedShift) & ChannelMask) / ChannelMax;
            var green = ((rgb >> GreenShift) & ChannelMask) / ChannelMax;
            var blue = (rgb & ChannelMask) / ChannelMax;

            return new Color(red, green, blue, 1);
        }
    }
}
