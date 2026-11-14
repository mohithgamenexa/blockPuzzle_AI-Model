namespace BlockPuzzle.Core
{
    /// <summary>
    /// One concrete orientation of a block piece. Cells are normalised so the
    /// bottom-left of the bounding box is (0,0).
    /// </summary>
    public sealed class PieceShape
    {
        public readonly int Id;
        public readonly string Family;
        public readonly Cell[] Cells;
        public readonly int Width;
        public readonly int Height;
        public readonly int ColorIndex;
        public readonly float Weight;

        public int Size => Cells.Length;

        public PieceShape(int id, string family, Cell[] cells, int colorIndex, float weight)
        {
            Id = id;
            Family = family;
            Cells = cells;
            ColorIndex = colorIndex;
            Weight = weight;

            int w = 0, h = 0;
            foreach (var c in cells)
            {
                if (c.X + 1 > w) w = c.X + 1;
                if (c.Y + 1 > h) h = c.Y + 1;
            }
            Width = w;
            Height = h;
        }

        public override string ToString() => $"{Family}#{Id} {Width}x{Height}";
    }
}
