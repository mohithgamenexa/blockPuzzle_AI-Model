using System;
using System.Collections.Generic;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// Deals trays of pieces. Weighted random with three design rules:
    ///  * Difficulty ramps with score: tiny pieces get rarer, big ones more common.
    ///  * Fairness: whenever possible at least one dealt piece fits the board,
    ///    and on a crowded board at least two do.
    ///  * Variety: no identical orientation twice in a tray.
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

        public PieceShape[] NextTray(Board board, int score)
        {
            UpdateWeights(score);

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

        void UpdateWeights(int score)
        {
            float t = Math.Min(1f, Math.Max(0f, score / (float)ScoreForMaxDifficulty));
            for (int i = 0; i < _pool.Count; i++)
            {
                var p = _pool[i];
                float factor = 1f;
                if (p.Size <= 2) factor = Lerp(1.1f, 0.55f, t);
                else if (p.Size >= 5) factor = Lerp(0.75f, 1.4f, t);
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
