using System.Collections.Generic;
using UnityEngine;

namespace BlockPuzzle.Core
{
    /// <summary>Pure game logic for the grid; no rendering, so an ML agent can use it headless.</summary>
    public sealed class Board
    {
        public readonly int Width;
        public readonly int Height;
        readonly bool[,] _cells;

        public int Score { get; private set; }

        public Board(int width = 8, int height = 8)
        {
            Width = width;
            Height = height;
            _cells = new bool[width, height];
        }

        public bool IsFilled(int x, int y) => _cells[x, y];

        public bool CanPlace(Shape shape, Vector2Int origin)
        {
            foreach (var c in shape.Cells)
            {
                int x = origin.x + c.x, y = origin.y + c.y;
                if (x < 0 || y < 0 || x >= Width || y >= Height || _cells[x, y]) return false;
            }
            return true;
        }

        /// <summary>Places the shape and clears full rows/columns. Returns lines cleared, or -1 if invalid.</summary>
        public int Place(Shape shape, Vector2Int origin)
        {
            if (!CanPlace(shape, origin)) return -1;
            foreach (var c in shape.Cells) _cells[origin.x + c.x, origin.y + c.y] = true;
            Score += shape.Cells.Length;

            int lines = ClearLines();
            Score += lines * lines * 10;
            return lines;
        }

        public IEnumerable<Vector2Int> ValidPlacements(Shape shape)
        {
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if (CanPlace(shape, new Vector2Int(x, y))) yield return new Vector2Int(x, y);
        }

        public bool HasAnyMove(IEnumerable<Shape> shapes)
        {
            foreach (var s in shapes)
                foreach (var _ in ValidPlacements(s)) return true;
            return false;
        }

        public void Reset()
        {
            System.Array.Clear(_cells, 0, _cells.Length);
            Score = 0;
        }

        int ClearLines()
        {
            var rows = new List<int>();
            var cols = new List<int>();
            for (int y = 0; y < Height; y++)
            {
                bool full = true;
                for (int x = 0; x < Width && full; x++) full = _cells[x, y];
                if (full) rows.Add(y);
            }
            for (int x = 0; x < Width; x++)
            {
                bool full = true;
                for (int y = 0; y < Height && full; y++) full = _cells[x, y];
                if (full) cols.Add(x);
            }
            foreach (int y in rows) for (int x = 0; x < Width; x++) _cells[x, y] = false;
            foreach (int x in cols) for (int y = 0; y < Height; y++) _cells[x, y] = false;
            return rows.Count + cols.Count;
        }
    }
}
