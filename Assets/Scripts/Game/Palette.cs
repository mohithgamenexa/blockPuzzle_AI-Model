using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>All colours in one place so the look can be re-themed easily.</summary>
    public static class Palette
    {
        // Block colours, indexed by PieceShape.ColorIndex. Slightly over-bright on
        // purpose: the block sprite's face is ~85% grey, so tints land on target.
        static readonly Color[] Blocks =
        {
            Hex(0xFF5E6C), // coral
            Hex(0xFF9A3C), // orange
            Hex(0xFFD23F), // amber
            Hex(0x7BE05A), // lime
            Hex(0x2EE6C2), // teal
            Hex(0x42B8FF), // sky
            Hex(0x5C7CFF), // blue
            Hex(0xB26BFF), // violet
            Hex(0xFF6FD1), // pink
        };

        public static readonly Color BackgroundTop = Hex(0x283A78);
        public static readonly Color BackgroundBottom = Hex(0x0E1430);
        public static readonly Color BoardPanel = Hex(0x141B3C);
        public static readonly Color BoardShadow = new Color(0f, 0f, 0f, 0.35f);
        public static readonly Color EmptyCell = Hex(0x222C5C);
        public static readonly Color TrayPanel = new Color(1f, 1f, 1f, 0.05f);
        public static readonly Color GameOverBlock = Hex(0x5A6185);
        public static readonly Color Text = Color.white;
        public static readonly Color TextDim = new Color(1f, 1f, 1f, 0.6f);
        public static readonly Color Gold = Hex(0xFFD23F);
        public static readonly Color Overlay = new Color(0.03f, 0.04f, 0.1f, 0.78f);
        public static readonly Color Card = Hex(0x1C2552);

        public static Color Block(int index) => Blocks[((index % Blocks.Length) + Blocks.Length) % Blocks.Length];

        public static Color Hex(uint rgb, float a = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);

        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
