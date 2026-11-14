using System;
using System.Collections.Generic;
using System.Linq;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// Every piece the game can deal. Families are defined once as ASCII art;
    /// rotations (and mirrors where requested) are generated and de-duplicated.
    /// </summary>
    public static class PieceCatalog
    {
        public const int ColorCount = 9;

        static PieceShape[] _all;

        public static IReadOnlyList<PieceShape> All => _all ??= Build();

        static PieceShape[] Build()
        {
            var list = new List<PieceShape>();
            //  family       color weight mirror  rows (top to bottom)
            Add(list, "Dot",     2, 0.45f, false, "#");
            Add(list, "Domino",  5, 0.90f, false, "##");
            Add(list, "Line3",   4, 1.10f, false, "###");
            Add(list, "Line4",   6, 0.90f, false, "####");
            Add(list, "Line5",   0, 0.60f, false, "#####");
            Add(list, "Corner",  3, 1.10f, false, "#.", "##");
            Add(list, "Square",  1, 1.10f, false, "##", "##");
            Add(list, "Big",     7, 0.35f, false, "###", "###", "###");
            Add(list, "Slab",    8, 0.55f, false, "###", "###");
            Add(list, "BigL",    6, 0.55f, false, "#..", "#..", "###");
            Add(list, "L",       7, 1.00f, true,  "#.", "#.", "##");
            Add(list, "T",       8, 0.85f, false, "###", ".#.");
            Add(list, "S",       3, 0.65f, true,  ".##", "##.");
            return list.ToArray();
        }

        static void Add(List<PieceShape> list, string family, int color, float weight, bool mirror, params string[] rows)
        {
            var baseCells = Parse(rows);
            var variants = new List<Cell[]>();

            AddRotations(variants, baseCells);
            if (mirror) AddRotations(variants, baseCells.Select(c => new Cell(-c.X, c.Y)).ToArray());

            float perVariant = weight / variants.Count;
            foreach (var v in variants)
                list.Add(new PieceShape(list.Count, family, v, color, perVariant));
        }

        static void AddRotations(List<Cell[]> variants, Cell[] cells)
        {
            var current = cells;
            for (int r = 0; r < 4; r++)
            {
                var normalised = Normalise(current);
                if (!variants.Any(v => SameCells(v, normalised))) variants.Add(normalised);
                current = current.Select(c => new Cell(c.Y, -c.X)).ToArray(); // rotate 90° clockwise
            }
        }

        static Cell[] Parse(string[] rows)
        {
            var cells = new List<Cell>();
            for (int r = 0; r < rows.Length; r++)
                for (int x = 0; x < rows[r].Length; x++)
                    if (rows[r][x] == '#') cells.Add(new Cell(x, rows.Length - 1 - r));
            if (cells.Count == 0) throw new ArgumentException("Empty piece pattern");
            return cells.ToArray();
        }

        static Cell[] Normalise(Cell[] cells)
        {
            int minX = cells.Min(c => c.X), minY = cells.Min(c => c.Y);
            return cells.Select(c => new Cell(c.X - minX, c.Y - minY))
                        .OrderBy(c => c.Y).ThenBy(c => c.X)
                        .ToArray();
        }

        static bool SameCells(Cell[] a, Cell[] b) => a.Length == b.Length && a.SequenceEqual(b);
    }
}
