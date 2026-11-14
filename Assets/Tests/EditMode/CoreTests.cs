using System.Collections.Generic;
using System.Linq;
using BlockPuzzle.Core;
using NUnit.Framework;

namespace BlockPuzzle.Tests
{
    public class BoardTests
    {
        static PieceShape Find(string family, int width, int height) =>
            PieceCatalog.All.First(p => p.Family == family && p.Width == width && p.Height == height);

        [Test]
        public void PlacesPieceAndRejectsOverlap()
        {
            var board = new Board();
            var square = Find("Square", 2, 2);

            Assert.IsTrue(board.CanPlace(square, 0, 0));
            board.Place(square, 0, 0, 1);
            Assert.AreEqual(4, board.FilledCount);
            Assert.IsFalse(board.CanPlace(square, 1, 1));
            Assert.IsFalse(board.CanPlace(square, 7, 0), "out of bounds");
        }

        [Test]
        public void ClearsRowAndColumnTogether()
        {
            var board = new Board();
            var dot = Find("Dot", 1, 1);
            for (int x = 1; x < 8; x++) board.Place(dot, x, 0, 0);
            for (int y = 1; y < 8; y++) board.Place(dot, 0, y, 0);

            var rows = new List<int>();
            var cols = new List<int>();
            board.FindCompletedLines(dot, 0, 0, rows, cols);
            CollectionAssert.AreEqual(new[] { 0 }, rows);
            CollectionAssert.AreEqual(new[] { 0 }, cols);

            var result = board.Place(dot, 0, 0, 3);
            Assert.AreEqual(2, result.LinesCleared);
            Assert.AreEqual(15, result.ClearedCells.Count);
            Assert.IsTrue(result.BoardCleared);
            Assert.AreEqual(0, board.FilledCount);
        }

        [Test]
        public void CatalogHasUniqueOrientations()
        {
            var all = PieceCatalog.All;
            Assert.AreEqual(2, all.Count(p => p.Family == "Line5"));
            Assert.AreEqual(4, all.Count(p => p.Family == "Corner"));
            Assert.AreEqual(8, all.Count(p => p.Family == "L"));
            Assert.AreEqual(4, all.Count(p => p.Family == "S"));
            Assert.AreEqual(1, all.Count(p => p.Family == "Big"));
            Assert.IsTrue(all.All(p => p.Cells.All(c => c.X >= 0 && c.Y >= 0)));
        }
    }

    public class ScoreTests
    {
        [Test]
        public void LineClearsScaleAndComboMultiplies()
        {
            var s = new ScoreKeeper();
            Assert.AreEqual(4, s.Register(4, 0, false).Points);
            Assert.AreEqual(3 + 10, s.Register(3, 1, false).Points);   // combo 1
            Assert.AreEqual(3 + 60, s.Register(3, 2, false).Points);   // 30 * combo 2
            Assert.AreEqual(2, s.Combo);
        }

        [Test]
        public void ComboSurvivesTwoMissesThenResets()
        {
            var s = new ScoreKeeper();
            s.Register(1, 1, false);
            s.Register(1, 0, false);
            s.Register(1, 0, false);
            Assert.AreEqual(1, s.Combo);
            s.Register(1, 0, false);
            Assert.AreEqual(0, s.Combo);
        }

        [Test]
        public void PerfectClearAddsBonus()
        {
            var s = new ScoreKeeper();
            Assert.AreEqual(1 + 10 + ScoreKeeper.PerfectBonus, s.Register(1, 1, true).Points);
        }
    }

    public class GeneratorTests
    {
        [Test]
        public void DealsAtLeastOnePlayablePieceWhenPossible()
        {
            // Fill everything except a single hole: only the Dot fits.
            var board = new Board();
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    if (!(x == 3 && y == 3)) board.Set(x, y, 1);

            for (int seed = 0; seed < 50; seed++)
            {
                var tray = new PieceGenerator(seed).NextTray(board, 0);
                Assert.AreEqual(PieceGenerator.TraySize, tray.Length);
                Assert.IsTrue(tray.Any(board.CanPlaceAnywhere), $"seed {seed}");
            }
        }
    }
}
