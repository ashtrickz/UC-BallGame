using UnityEngine;

namespace Slime.Prototypes.SlimeLab
{
    // Slime Lab only: candy-colored cartoon jelly and illustrated crystal facets.
    // Turquoise upper body, lime transmitted edges, coral/amethyst obstacles.
    public static class SlimeLabPalette
    {
        public static readonly Color Background = Hex("101E29");
        public static readonly Color Floor = Hex("192F3B");
        public static readonly Color Platform = Hex("30515A");
        public static readonly Color Trim = Hex("456B70");
        public static readonly Color Jelly = Hex("58F044");
        public static readonly Color JellyTop = Hex("17DFB0");
        public static readonly Color JellyLight = Hex("E1FF86");
        public static readonly Color Highlight = Hex("FFFFE2");
        public static readonly Color Coral = Hex("FF6654");
        public static readonly Color Amethyst = Hex("BF70EF");
        public static readonly Color Text = Hex("E3F2EB");
        public static readonly Color Muted = Hex("90B3B7");
        public static readonly Color Ink = Hex("092F32");
        public static readonly Color Key = Hex("FFF0D6");

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }
    }
}
