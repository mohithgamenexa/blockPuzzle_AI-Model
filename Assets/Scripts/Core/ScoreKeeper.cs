namespace BlockPuzzle.Core
{
    public struct ScoreEvent
    {
        public int Points;
        public int PlacementPoints;
        public int LinePoints;
        public int PerfectPoints;
        public int Lines;
        public int Combo;
        public bool Perfect;
    }

    /// <summary>
    /// Scoring rules:
    ///  * +1 per block placed.
    ///  * Clearing n lines at once: 10 * n(n+1)/2  (10, 30, 60, 100, ...),
    ///    multiplied by the current combo.
    ///  * Combo grows with every placement that clears a line. It survives up to
    ///    <see cref="ComboGrace"/> - 1 placements without a clear, then resets.
    ///  * Clearing the whole board: +300.
    /// </summary>
    public sealed class ScoreKeeper
    {
        public const int ComboGrace = 3;
        public const int PerfectBonus = 300;
        const int MaxComboMultiplier = 10;

        public int Score { get; private set; }
        public int Combo { get; private set; }
        public int MovesSinceClear { get; private set; }

        public ScoreEvent Register(int blocksPlaced, int linesCleared, bool boardCleared)
        {
            var e = new ScoreEvent { PlacementPoints = blocksPlaced, Lines = linesCleared };

            if (linesCleared > 0)
            {
                Combo++;
                MovesSinceClear = 0;
                int multiplier = Combo < MaxComboMultiplier ? Combo : MaxComboMultiplier;
                e.LinePoints = 10 * linesCleared * (linesCleared + 1) / 2 * multiplier;
            }
            else
            {
                MovesSinceClear++;
                if (MovesSinceClear >= ComboGrace) Combo = 0;
            }

            if (boardCleared)
            {
                e.Perfect = true;
                e.PerfectPoints = PerfectBonus;
            }

            e.Combo = Combo;
            e.Points = e.PlacementPoints + e.LinePoints + e.PerfectPoints;
            Score += e.Points;
            return e;
        }

        public void Reset()
        {
            Score = 0;
            Combo = 0;
            MovesSinceClear = 0;
        }
    }
}
