using System;
using System.Collections.Generic;

namespace BlockPuzzle.Core
{
    /// <summary>A cell that was removed by a line clear, with the colour it had.</summary>
    public readonly struct ClearedCell
    {
        public readonly Cell Cell;
        public readonly int Color;

        public ClearedCell(Cell cell, int color)
        {
            Cell = cell;
            Color = color;
        }
    }

    public sealed class PlacementResult
    {
        public readonly List<Cell> PlacedCells = new List<Cell>();
        public readonly List<int> Rows = new List<int>();
        public readonly List<int> Columns = new List<int>();
        public readonly List<ClearedCell> ClearedCells = new List<ClearedCell>();
        public bool BoardCleared;

        public int LinesCleared => Rows.Count + Columns.Count;
    }

    /// <summary>
    /// Grid rules for the block puzzle. Engine-independent so it can be unit tested
    /// and driven headless by an AI agent.
    /// Cell values: 0 = empty, n &gt; 0 = filled with colour (n - 1).
    /// </summary>
    public sealed class Board
    {
        public const int Empty = 0;

        public readonly int Width;
        public readonly int Height;

        readonly int[] _cells;
        readonly bool[] _scratch;

        public Board(int width = 8, int height = 8)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException();
            Width = width;
            Height = height;
            _cells = new int[width * height];
            _scratch = new bool[width * height];
        }

        public int this[int x, int y] => _cells[y * Width + x];

        public bool IsFilled(int x, int y) => _cells[y * Width + x] != Empty;

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public int FilledCount
        {
            get
            {
                int n = 0;
                foreach (int v in _cells) if (v != Empty) n++;
                return n;
            }
        }

        public bool IsEmpty => FilledCount == 0;

        public void Set(int x, int y, int value) => _cells[y * Width + x] = value;

        public void Reset() => Array.Clear(_cells, 0, _cells.Length);

        public Board Clone()
        {
            var b = new Board(Width, Height);
            Array.Copy(_cells, b._cells, _cells.Length);
            return b;
        }

        public bool CanPlace(PieceShape piece, int ox, int oy)
        {
            if (ox < 0 || oy < 0 || ox + piece.Width > Width || oy + piece.Height > Height) return false;
            foreach (var c in piece.Cells)
                if (_cells[(oy + c.Y) * Width + ox + c.X] != Empty) return false;
            return true;
        }

        public bool CanPlaceAnywhere(PieceShape piece)
        {
            for (int y = 0; y <= Height - piece.Height; y++)
                for (int x = 0; x <= Width - piece.Width; x++)
                    if (CanPlace(piece, x, y)) return true;
            return false;
        }

        public int CountPlacements(PieceShape piece)
        {
            int n = 0;
            for (int y = 0; y <= Height - piece.Height; y++)
                for (int x = 0; x <= Width - piece.Width; x++)
                    if (CanPlace(piece, x, y)) n++;
            return n;
        }

        /// <summary>Rows/columns that would be completed by placing the piece (board is not modified).</summary>
        public void FindCompletedLines(PieceShape piece, int ox, int oy, List<int> rows, List<int> columns)
        {
            rows.Clear();
            columns.Clear();
            for (int i = 0; i < _cells.Length; i++) _scratch[i] = _cells[i] != Empty;
            foreach (var c in piece.Cells) _scratch[(oy + c.Y) * Width + ox + c.X] = true;
            CollectFullLines(_scratch, rows, columns);
        }

        /// <summary>Places the piece, clears completed lines and reports what happened.</summary>
        public PlacementResult Place(PieceShape piece, int ox, int oy, int colorIndex)
        {
            if (!CanPlace(piece, ox, oy))
                throw new InvalidOperationException($"Cannot place {piece} at ({ox},{oy})");

            var result = new PlacementResult();
            foreach (var c in piece.Cells)
            {
                int x = ox + c.X, y = oy + c.Y;
                _cells[y * Width + x] = colorIndex + 1;
                result.PlacedCells.Add(new Cell(x, y));
            }

            for (int i = 0; i < _cells.Length; i++) _scratch[i] = _cells[i] != Empty;
            CollectFullLines(_scratch, result.Rows, result.Columns);

            if (result.LinesCleared > 0)
            {
                Array.Clear(_scratch, 0, _scratch.Length); // reuse as "to clear" mask
                foreach (int y in result.Rows)
                    for (int x = 0; x < Width; x++) _scratch[y * Width + x] = true;
                foreach (int x in result.Columns)
                    for (int y = 0; y < Height; y++) _scratch[y * Width + x] = true;

                for (int y = 0; y < Height; y++)
                    for (int x = 0; x < Width; x++)
                    {
                        int i = y * Width + x;
                        if (!_scratch[i]) continue;
                        result.ClearedCells.Add(new ClearedCell(new Cell(x, y), _cells[i] - 1));
                        _cells[i] = Empty;
                    }

                result.BoardCleared = IsEmpty;
            }

            return result;
        }

        /// <summary>Empties the given rows and columns (used by revive). Reports cleared cells.</summary>
        public PlacementResult ClearLines(IEnumerable<int> rows, IEnumerable<int> columns)
        {
            var result = new PlacementResult();
            Array.Clear(_scratch, 0, _scratch.Length);
            foreach (int y in rows)
            {
                result.Rows.Add(y);
                for (int x = 0; x < Width; x++) _scratch[y * Width + x] = true;
            }
            foreach (int x in columns)
            {
                result.Columns.Add(x);
                for (int y = 0; y < Height; y++) _scratch[y * Width + x] = true;
            }
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x;
                    if (!_scratch[i] || _cells[i] == Empty) continue;
                    result.ClearedCells.Add(new ClearedCell(new Cell(x, y), _cells[i] - 1));
                    _cells[i] = Empty;
                }
            result.BoardCleared = IsEmpty;
            return result;
        }

        /// <summary>Indices of the most-filled rows (or columns), most-filled first.</summary>
        public List<int> FullestLines(bool rows, int count)
        {
            int n = rows ? Height : Width;
            var fill = new int[n];
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (_cells[y * Width + x] != Empty) fill[rows ? y : x]++;
            var order = new List<int>();
            for (int i = 0; i < n; i++) order.Add(i);
            order.Sort((a, b) => fill[b] != fill[a] ? fill[b].CompareTo(fill[a]) : a.CompareTo(b));
            return order.GetRange(0, Math.Min(count, n));
        }

        void CollectFullLines(bool[] filled, List<int> rows, List<int> columns)
        {
            for (int y = 0; y < Height; y++)
            {
                bool full = true;
                for (int x = 0; x < Width && full; x++) full = filled[y * Width + x];
                if (full) rows.Add(y);
            }
            for (int x = 0; x < Width; x++)
            {
                bool full = true;
                for (int y = 0; y < Height && full; y++) full = filled[y * Width + x];
                if (full) columns.Add(x);
            }
        }
    }
}
