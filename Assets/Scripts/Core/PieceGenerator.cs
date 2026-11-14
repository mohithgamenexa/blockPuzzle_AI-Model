using System;
using System.Collections.Generic;
using BlockPuzzle.Core.Monetization;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// Deals trays of pieces. Weighted random, steered by a <see cref="DealMode"/>:
    ///  * Normal: difficulty ramps with score (tiny pieces rarer, big ones more common),
    ///    at least one piece fits whenever possible (two on a crowded board), and no
    ///    identical orientation appears twice in a tray.
    ///  * Assist: every piece fits, and the tray is the candidate with the most
    ///    line-clear potential.
    ///  * Squeeze: biased to big pieces; picks a tray where nothing fits if one turns up,
    ///    otherwise the one with the fewest legal placements.
    /// </summary>
    public sealed class PieceGenerator
    {
        public const int TraySize = 3;
        const int Candidates = 16;
        const float CrowdedFill = 0.5f;
        const int ScoreForMaxDifficulty = 5000;

        readonly Random _rng;
        readonly IReadOnlyList<PieceShape> _pool;
        readonly float[] _weights;

        public PieceGenerator(int seed, IReadOnlyList<PieceShape> pool = null)
        {
            _rng = new Random(seed);
            _pool = pool ?? PieceCatalog.All;
            _weights = new float[_pool.Count];
        }

        public PieceShape[] NextTray(Board board, int score, DealMode mode = DealMode.Normal)
        {
            UpdateWeights(score, mode);
            if (mode == DealMode.Assist) return AssistTray(board, score);
            if (mode == DealMode.Squeeze) return SqueezeTray(board);

            int fitsAtAll = 0;
            foreach (var p in _pool)
                if (board.CanPlaceAnywhere(p)) { fitsAtAll++; if (fitsAtAll >= 2) break; }

            float fill = board.FilledCount / (float)(board.Width * board.Height);
            int wanted = Math.Min(fill >= CrowdedFill ? 2 : 1, fitsAtAll);

            PieceShape[] best = null;
            int bestFits = -1;
            for (int attempt = 0; attempt < Candidates; attempt++)
            {
                var tray = DrawTray();
                int fits = 0;
                foreach (var p in tray) if (board.CanPlaceAnywhere(p)) fits++;
                if (fits >= wanted) return tray;
                if (fits > bestFits) { best = tray; bestFits = fits; }
            }

            // Couldn't satisfy the rule by luck: swap in a piece that fits.
            if (bestFits < wanted && fitsAtAll > 0)
            {
                for (int i = 0; i < best.Length && bestFits < wanted; i++)
                {
                    if (board.CanPlaceAnywhere(best[i])) continue;
                    var replacement = DrawFitting(board);
                    if (replacement != null) { best[i] = replacement; bestFits++; }
                }
            }
            return best;
        }

        PieceShape[] AssistTray(Board board, int score)
        {
            PieceShape[] best = null;
            int bestPotential = -1;
            for (int attempt = 0; attempt < Candidates; attempt++)
            {
                var tray = DrawTray();
                int potential = 0;
                bool allFit = true;
                foreach (var p in tray)
                {
                    int lines = BestLines(board, p);
                    if (lines < 0) { allFit = false; break; }
                    potential += lines;
                }
                if (!allFit) continue;
                if (potential > bestPotential) { best = tray; bestPotential = potential; }
            }
            // Board too crowded for a tray where everything fits: fall back to fair dealing.
            return best ?? NextTray(board, score, DealMode.Normal);
        }

        /// <summary>Most lines the piece can complete in one placement; -1 if it does not fit.</summary>
        int BestLines(Board board, PieceShape piece)
        {
            int best = -1;
            for (int y = 0; y <= board.Height - piece.Height; y++)
                for (int x = 0; x <= board.Width - piece.Width; x++)
                {
                    if (!board.CanPlace(piece, x, y)) continue;
                    board.FindCompletedLines(piece, x, y, _rows, _cols);
                    int lines = _rows.Count + _cols.Count;
                    if (lines > best) best = lines;
                }
            return best;
        }

        PieceShape[] SqueezeTray(Board board)
        {
            PieceShape[] best = null;
            int bestPlacements = int.MaxValue;
            for (int attempt = 0; attempt < Candidates; attempt++)
            {
                var tray = DrawTray();
                int placements = 0;
                foreach (var p in tray) placements += board.CountPlacements(p);
                if (placements == 0) return tray;
                if (placements < bestPlacements) { best = tray; bestPlacements = placements; }
            }
            return best;
        }

        readonly List<int> _rows = new List<int>();
        readonly List<int> _cols = new List<int>();

        void UpdateWeights(int score, DealMode mode)
        {
            float t = Math.Min(1f, Math.Max(0f, score / (float)ScoreForMaxDifficulty));
            for (int i = 0; i < _pool.Count; i++)
            {
                var p = _pool[i];
                float factor = 1f;
                if (p.Size <= 2) factor = Lerp(1.1f, 0.55f, t);
                else if (p.Size >= 5) factor = Lerp(0.75f, 1.4f, t);
                if (mode == DealMode.Squeeze) factor *= p.Size >= 4 ? 3f : p.Size <= 2 ? 0.1f : 1f;
                _weights[i] = p.Weight * factor;
            }
        }

        PieceShape[] DrawTray()
        {
            var tray = new PieceShape[TraySize];
            for (int i = 0; i < TraySize; i++)
            {
                PieceShape p;
                int guard = 0;
                do p = DrawWeighted(null); while (Contains(tray, i, p) && ++guard < 10);
                tray[i] = p;
            }
            return tray;
        }

        PieceShape DrawFitting(Board board)
        {
            var fitting = new List<PieceShape>();
            foreach (var p in _pool) if (board.CanPlaceAnywhere(p)) fitting.Add(p);
            return fitting.Count == 0 ? null : DrawWeighted(fitting);
        }

        PieceShape DrawWeighted(List<PieceShape> subset)
        {
            float total = 0f;
            if (subset == null) foreach (float w in _weights) total += w;
            else foreach (var p in subset) total += _weights[IndexOf(p)];

            float roll = (float)_rng.NextDouble() * total;
            if (subset == null)
            {
                for (int i = 0; i < _pool.Count; i++)
                {
                    roll -= _weights[i];
                    if (roll <= 0f) return _pool[i];
                }
                return _pool[_pool.Count - 1];
            }
            foreach (var p in subset)
            {
                roll -= _weights[IndexOf(p)];
                if (roll <= 0f) return p;
            }
            return subset[subset.Count - 1];
        }

        int IndexOf(PieceShape p)
        {
            for (int i = 0; i < _pool.Count; i++) if (ReferenceEquals(_pool[i], p)) return i;
            return 0;
        }

        static bool Contains(PieceShape[] tray, int count, PieceShape p)
        {
            for (int i = 0; i < count; i++) if (ReferenceEquals(tray[i], p)) return true;
            return false;
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
