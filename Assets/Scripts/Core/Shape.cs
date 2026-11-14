using UnityEngine;

namespace BlockPuzzle.Core
{
    /// <summary>A block piece, stored as cell offsets from its bottom-left corner.</summary>
    public sealed class Shape
    {
        public readonly Vector2Int[] Cells;

        public Shape(params Vector2Int[] cells) => Cells = cells;

        static Vector2Int C(int x, int y) => new Vector2Int(x, y);

        public static readonly Shape[] All =
        {
            new Shape(C(0, 0)),                                     // single
            new Shape(C(0, 0), C(1, 0)),                            // domino H
            new Shape(C(0, 0), C(0, 1)),                            // domino V
            new Shape(C(0, 0), C(1, 0), C(2, 0)),                   // line 3 H
            new Shape(C(0, 0), C(0, 1), C(0, 2)),                   // line 3 V
            new Shape(C(0, 0), C(1, 0), C(2, 0), C(3, 0)),          // line 4 H
            new Shape(C(0, 0), C(0, 1), C(0, 2), C(0, 3)),          // line 4 V
            new Shape(C(0, 0), C(1, 0), C(0, 1), C(1, 1)),          // square 2x2
            new Shape(C(0, 0), C(1, 0), C(0, 1)),                   // small L
            new Shape(C(0, 0), C(1, 0), C(1, 1)),                   // small L (mirrored)
            new Shape(C(0, 0), C(1, 0), C(2, 0), C(0, 1), C(0, 2)), // big L
            new Shape(C(0, 0), C(1, 0), C(2, 0), C(1, 1)),          // T
        };
    }
}
